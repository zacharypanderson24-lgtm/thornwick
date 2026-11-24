// Scenarios: the big disasters. Each one is announced ScenarioLeadDays ahead, with a checklist of what would get the town through it and a
// readiness figure that follows the town as it is NOW (stores, wells, soldiers). When the day comes the damage is not a number on a card: it
// comes out of the real simulation, so a town that prepared loses little and one that did not can lose a great deal.
//   famine  Crops give a quarter of their yield for FamineDays. Food in store and silo room, and hunters, decide who starves.
//   fire    Fires start in several buildings and spread to neighbours within FireSpreadGap tiles; a building with a well within
//           FireWellReach tiles often puts its fire out and rarely catches. Wells and gaps between buildings are the defence, and you can
//           bulldoze a building in the fire's path to make a firebreak.
//   host    A warlord's host of raiders, far bigger than a camp's band, marches on the town. Soldiers, walls, a watchtower and a barracks
//           decide whether it is beaten or hits the Town Hall (World.Defeat.cs).
// One scenario at a time, from the second year, on the first day of a season, never during a raid, and at least ScenarioGapDays after the
// last one ended. Like the campaign they only run when CampaignOn is set, so tests that tick worlds are unaffected.
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using static CrownAndHearth.Sim.Defs;

namespace CrownAndHearth.Sim
{
    public enum ScenarioPhase : byte { Warned, Hitting }

    public sealed class Scenario
    {
        public string Id;
        public ScenarioPhase Phase;
        public int WarnedDay, ArriveDay, EndDay;
        public int Size;            // host: raiders
        public int Burned, Saved;   // fire: buildings lost and buildings that put their fire out
    }

    public sealed class ScenarioCheck
    {
        public string Text;
        public double Score, Weight;   // 0..1 how well the town meets it, and how much it counts
        public bool Ok => Score >= 1;
    }

    public partial class World
    {
        public const int ScenarioLeadDays = 30, ScenarioGapDays = 90, FirstScenarioYear = 2;
        public const int FamineDays = 90, FireBurnDays = 4, FireSpreadGap = 2, FireWellReach = 8, FireRebuildWood = 60;
        public const double FamineYield = 0.25;

        public Scenario CurrentScenario { get; private set; }
        /// <summary>The day the last scenario ended (saved), so they are spaced out.</summary>
        public int LastScenarioEnd { get; private set; } = -1000;

        readonly Dictionary<Building, int> _burning = new Dictionary<Building, int>();   // building -> days of fire left

        static readonly string[] BurnProof = { "road", "wall", "hall", "well", "cistern", "watchtower", "tent", "chieftent", "campfire", "rack", "supplies" };
        static bool Burnable(Building b) => Array.IndexOf(BurnProof, b.T) < 0;

        /// <summary>How much of its normal grain the fields give today (a famine cuts it).</summary>
        public double CropYield => CurrentScenario != null && CurrentScenario.Id == "famine" && CurrentScenario.Phase == ScenarioPhase.Hitting ? FamineYield : 1.0;

        public bool IsBurning(Building b) => _burning.ContainsKey(b);
        public IEnumerable<Building> Burning => _burning.Keys.Where(b => Blds.Contains(b));

        public static string ScenarioTitle(string id) => id switch
        {
            "famine" => "Failed harvest", "fire" => "Great fire", "host" => "The warlord's host", _ => id,
        };

        // ---------- announcing ----------

        /// <summary>Announce a scenario now; it strikes `lead` days from now. (The scheduler uses it; tests and scenarios can too.)</summary>
        public void RaiseScenario(string id, int lead = ScenarioLeadDays)
        {
            var s = new Scenario { Id = id, Phase = ScenarioPhase.Warned, WarnedDay = Day, ArriveDay = Day + lead };
            if (id == "host") s.Size = HostSize();
            CurrentScenario = s;
            Events.Add($"WARNING, {ScenarioTitle(id)}: " + ScenarioWarning(s));
        }

        /// <summary>The raiders in a warlord's host: it grows with the years and with the size of the town.</summary>
        public int HostSize() => Math.Min(40, 12 + 4 * (Year - 1) + (Peasants + Soldiers) / 15);

        public string ScenarioWarning(Scenario s)
        {
            switch (s.Id)
            {
                case "famine": return $"The crops are failing. In {s.ArriveDay - Day} days the fields will give a quarter of their grain for {FamineDays} days. Fill the granaries and silos before then.";
                case "fire": return $"A dry spell is coming. In {s.ArriveDay - Day} days fires will break out and spread. Wells put fires out, and gaps between buildings stop them spreading.";
                case "host": return $"A warlord is gathering a host of about {s.Size} raiders. In {s.ArriveDay - Day} days it marches on the town. Train soldiers, build walls and a watchtower.";
                default: return "";
            }
        }

        /// <summary>The checklist for the current scenario, from the town as it is right now.</summary>
        public List<ScenarioCheck> ScenarioChecks()
        {
            var list = new List<ScenarioCheck>();
            var s = CurrentScenario;
            if (s == null) return list;
            switch (s.Id)
            {
                case "famine":
                {
                    double need = Math.Max(1, DailyFoodDemand * FamineDays * (1 - FamineYield));
                    double food = ResAmt[Res.Food], room = CropStorage(Res.Food);
                    list.Add(new ScenarioCheck { Text = $"Food in store: {food:0} of {need:0}", Score = Math.Min(1, food / need), Weight = 0.6 });
                    list.Add(new ScenarioCheck { Text = $"Room to keep it: {room:0} (build silos)", Score = Math.Min(1, room / need), Weight = 0.2 });
                    list.Add(new ScenarioCheck { Text = "A hunting lodge with hunters", Score = Blds.Any(b => b.T == "hunter" && b.Workers > 0) ? 1 : 0, Weight = 0.2 });
                    break;
                }
                case "fire":
                {
                    var burnable = Blds.Where(Burnable).ToList();
                    int wells = Blds.Count(b => b.T == "well");
                    double covered = burnable.Count == 0 ? 1 : burnable.Count(b => WellsNear(b) > 0) / (double)burnable.Count;
                    list.Add(new ScenarioCheck { Text = $"Buildings with a well within {FireWellReach} tiles: {covered * 100:0}% (aim for 80%)", Score = Math.Min(1, covered / 0.8), Weight = 0.6 });
                    list.Add(new ScenarioCheck { Text = $"Wells: {wells} (at least 2)", Score = Math.Min(1, wells / 2.0), Weight = 0.2 });
                    list.Add(new ScenarioCheck { Text = $"Wood to rebuild: {ResAmt[Res.Wood]:0} of {FireRebuildWood}", Score = Math.Min(1, ResAmt[Res.Wood] / FireRebuildWood), Weight = 0.2 });
                    break;
                }
                case "host":
                {
                    int rec = Math.Max(1, (int)Math.Ceiling(s.Size * 0.6));
                    int walls = Blds.Count(b => b.T == "wall");
                    list.Add(new ScenarioCheck { Text = $"Soldiers: {Soldiers} of {rec}", Score = Math.Min(1, Soldiers / (double)rec), Weight = 0.4 });
                    list.Add(new ScenarioCheck { Text = "A barracks to house and train them", Score = Blds.Any(b => b.T == "barracks") ? 1 : 0, Weight = 0.1 });
                    list.Add(new ScenarioCheck { Text = "A watchtower to see them coming", Score = Blds.Any(b => b.T == "watchtower") ? 1 : 0, Weight = 0.15 });
                    list.Add(new ScenarioCheck { Text = $"Palisade: {walls} of 16 wall tiles", Score = Math.Min(1, walls / 16.0), Weight = 0.35 });
                    break;
                }
            }
            return list;
        }

        /// <summary>0..100: how ready the town is for the current scenario.</summary>
        public int ScenarioReadiness()
        {
            var checks = ScenarioChecks();
            double w = checks.Sum(c => c.Weight);
            return w <= 0 ? 0 : (int)Math.Round(100 * checks.Sum(c => c.Score * c.Weight) / w);
        }

        // ---------- the daily step ----------

        void StepScenarios()
        {
            var s = CurrentScenario;
            if (s == null) return;
            if (s.Phase == ScenarioPhase.Warned)
            {
                int left = s.ArriveDay - Day;
                if (left <= 0) { Strike(s); return; }
                if (left == 14 || left == 7 || left == 3 || left == 1)
                    Events.Add($"{ScenarioTitle(s.Id)} in {left} {(left == 1 ? "day" : "days")}. The town is {ScenarioReadiness()}% ready.");
                return;
            }
            switch (s.Id)
            {
                case "famine": if (Day >= s.EndDay) EndScenario($"The failed harvest is over. The fields give their full grain again."); break;
                case "fire": StepFire(s); break;
                case "host": if (Raid == null || Raid.Over) EndScenario($"The warlord's host is gone. {Raid?.Summary()}"); break;
            }
        }

        void Strike(Scenario s)
        {
            if (Raid != null && !Raid.Over) return;   // one fight at a time: it waits for the raid in progress
            s.Phase = ScenarioPhase.Hitting;
            switch (s.Id)
            {
                case "famine":
                    s.EndDay = Day + FamineDays;
                    Events.Add($"The harvest has failed. The fields give a quarter of their grain for {FamineDays} days.");
                    break;
                case "fire":
                    var burnable = Blds.Where(Burnable).OrderBy(b => b.X).ThenBy(b => b.Y).ToList();
                    int starts = Math.Min(burnable.Count, Math.Min(4, 2 + (Year - 1) / 2));
                    for (int i = 0; i < starts; i++)
                    {
                        var b = burnable[(int)Math.Floor(Noise.Hash2(Day, Seed + i, 132) * burnable.Count)];
                        Ignite(b);
                    }
                    Events.Add(_burning.Count > 0 ? $"Fire! {_burning.Count} buildings are burning." : "Sparks fly, but nothing catches.");
                    if (_burning.Count == 0) EndScenario("");
                    break;
                case "host":
                    BeginRaid(s.Size);
                    Events.Add($"The warlord's host is here: {s.Size} raiders!");
                    break;
            }
        }

        void EndScenario(string news)
        {
            var s = CurrentScenario;
            if (s == null) return;
            if (s.Id == "fire") news = $"The fires are out. {s.Burned} buildings burned down and {s.Saved} were saved.";
            if (!string.IsNullOrEmpty(news)) Events.Add(news);
            LastScenarioEnd = Day;
            CurrentScenario = null;
            _burning.Clear();
        }

        // ---------- fire ----------

        /// <summary>Set a building alight (it burns FireBurnDays unless put out).</summary>
        public void Ignite(Building b)
        {
            if (b != null && Burnable(b) && !_burning.ContainsKey(b)) { _burning[b] = FireBurnDays; Touch(); }
        }

        static int Gap(Building a, Building b)
        {
            int gx = Math.Max(0, Math.Max(a.X, b.X) - Math.Min(a.X + a.Sz - 1, b.X + b.Sz - 1));
            int gy = Math.Max(0, Math.Max(a.Y, b.Y) - Math.Min(a.Y + a.Sz - 1, b.Y + b.Sz - 1));
            return Math.Max(gx, gy);
        }

        /// <summary>How many wells are within FireWellReach tiles of this building.</summary>
        public int WellsNear(Building b) => Blds.Count(w => w.T == "well" && Gap(b, w) <= FireWellReach);

        void StepFire(Scenario s)
        {
            foreach (var gone in _burning.Keys.Where(b => !Blds.Contains(b)).ToList()) _burning.Remove(gone);   // bulldozed: a firebreak
            var burning = _burning.Keys.OrderBy(b => b.X).ThenBy(b => b.Y).ToList();
            var spread = new List<Building>();
            foreach (var b in burning)
            {
                string name = Names.GetValueOrDefault(b.T, b.T);
                int wells = WellsNear(b);
                if (wells > 0 && Noise.Hash2(Day, b.X * N + b.Y, 130) < Math.Min(0.85, 0.35 * wells))
                {
                    _burning.Remove(b); s.Saved++;
                    Events.Add($"The {name} fire was put out.");
                    continue;
                }
                _burning[b]--;
                if (_burning[b] <= 0) { _burning.Remove(b); s.Burned++; Burn(b); continue; }
                foreach (var o in Blds)
                {
                    if (!Burnable(o) || _burning.ContainsKey(o) || spread.Contains(o) || Gap(b, o) > FireSpreadGap) continue;
                    double p = WellsNear(o) > 0 ? 0.12 : 0.4;
                    if (Noise.Hash2(Day, o.X * N + o.Y, 131 + b.X) < p) spread.Add(o);
                }
            }
            foreach (var o in spread) Ignite(o);
            if (spread.Count > 0) Events.Add($"The fire spreads: {_burning.Count} buildings are burning.");
            if (_burning.Count == 0) EndScenario("");
        }

        // ---------- saving ----------

        JObject SaveScenario() => CurrentScenario == null ? null : new JObject
        {
            ["id"] = CurrentScenario.Id, ["phase"] = (int)CurrentScenario.Phase, ["warned"] = CurrentScenario.WarnedDay, ["arrive"] = CurrentScenario.ArriveDay,
            ["end"] = CurrentScenario.EndDay, ["size"] = CurrentScenario.Size, ["burned"] = CurrentScenario.Burned, ["saved"] = CurrentScenario.Saved,
            ["burning"] = new JArray(Burning.OrderBy(b => b.X).ThenBy(b => b.Y).Select(b => new JArray(b.X, b.Y, _burning[b]))),
        };

        void LoadScenario(JObject o, int lastEnd)
        {
            LastScenarioEnd = lastEnd;
            _burning.Clear();
            CurrentScenario = o == null ? null : new Scenario
            {
                Id = (string)o["id"], Phase = (ScenarioPhase)(int)o["phase"], WarnedDay = (int)o["warned"], ArriveDay = (int)o["arrive"],
                EndDay = (int)o["end"], Size = (int)o["size"], Burned = (int)o["burned"], Saved = (int)o["saved"],
            };
            if (o?["burning"] is JArray list)
                foreach (var t in list)
                    if (Inb((int)t[0], (int)t[1]) && Grid[(int)t[1], (int)t[0]] is Building b) _burning[b] = (int)t[2];
        }
    }
}
