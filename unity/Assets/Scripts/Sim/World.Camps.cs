// Raider camps seen from the settlement: which camps exist (set by the game once the world map has been generated), raids that launch from
// them, and invasions. An invasion takes troops out of your army when you confirm it, the fight is a CampBattle, and the survivors march
// home (MarchKmPerDay) with whatever they carried: that arrives with them. Raiders march too: a camp's warband sets out across the world
// map and is spotted a few days before it arrives (sooner with a watchtower), giving you time to prepare. Destroyed camps, armies on the
// road and the warband on its way are saved.
using System;
using System.Collections.Generic;
using System.Linq;

namespace CrownAndHearth.Sim
{
    /// <summary>An army coming home from a camp with its loot.</summary>
    public sealed class Expedition
    {
        public int CampId, Infantry, Archers, ReturnDay;
        public readonly Dictionary<Res, int> Loot = new Dictionary<Res, int>();
    }

    /// <summary>Raiders marching from their camp to your settlement. When ArriveDay comes the raid begins.</summary>
    public sealed class Warband
    {
        public int CampId, Band, LeaveDay, ArriveDay;
        public bool Spotted;
    }

    public partial class World
    {
        public const int MarchKmPerDay = 15, RaidFarKm = 220;
        /// <summary>Camps leave a town alone until it has this many people (peasants and soldiers); tests lower it.</summary>
        public int RaidMinPopulation = 20;

        List<RaiderCamp> _camps = new List<RaiderCamp>();
        readonly Dictionary<int, int> _nextCampRaid = new Dictionary<int, int>();   // runtime: the day each camp raids next
        public int HomeCellX = WorldMap.Size / 2, HomeCellY = WorldMap.Size / 2;
        public readonly HashSet<int> DestroyedCamps = new HashSet<int>();
        public readonly List<Expedition> Expeditions = new List<Expedition>();

        public IReadOnlyList<RaiderCamp> Camps => _camps;
        public IEnumerable<RaiderCamp> LiveCamps => _camps.Where(c => !DestroyedCamps.Contains(c.Id));
        /// <summary>The last invasion fought (null before any): its report is shown until the next one.</summary>
        public CampBattle LastInvasion { get; private set; }

        public void SetCamps(IEnumerable<RaiderCamp> camps, int homeX, int homeY)
        {
            _camps = camps.ToList(); HomeCellX = homeX; HomeCellY = homeY; _nextCampRaid.Clear();
        }

        public int CampKm(RaiderCamp c) => (int)Math.Round(Math.Sqrt((c.X - HomeCellX) * (double)(c.X - HomeCellX) + (c.Y - HomeCellY) * (double)(c.Y - HomeCellY)));
        public int MarchDays(RaiderCamp c) => Math.Max(1, (CampKm(c) + MarchKmPerDay - 1) / MarchKmPerDay);

        /// <summary>The edge of the settlement map a camp lies towards: 0 north, 1 east, 2 south, 3 west.</summary>
        int CampSide(RaiderCamp c)
        {
            int dx = c.X - HomeCellX, dy = c.Y - HomeCellY;
            return Math.Abs(dx) > Math.Abs(dy) ? (dx > 0 ? 1 : 3) : (dy > 0 ? 2 : 0);
        }

        /// <summary>The camp a raid launches from: the bigger and nearer, the likelier. Null when no camps are set or all are destroyed.</summary>
        RaiderCamp PickRaidCamp()
        {
            var live = LiveCamps.ToList();
            if (live.Count == 0) return null;
            double Weight(RaiderCamp c) => c.Strength / (CampKm(c) + 20.0);
            double total = live.Sum(Weight), roll = Noise.Hash2(Day, Seed, 93) * total;
            foreach (var c in live) { roll -= Weight(c); if (roll <= 0) return c; }
            return live[live.Count - 1];
        }

        // ---------- raids that come on their own ----------

        /// <summary>Days before it arrives that a marching warband is seen: scouts see it this close, a watchtower's lookouts much sooner.</summary>
        public const int ScoutSpotDays = 2, TowerSpotDays = 5;
        public int SpotDays => Blds.Any(b => b.T == "watchtower") ? TowerSpotDays : ScoutSpotDays;

        /// <summary>The warband on its way here, or null. It is only shown to the player once Spotted.</summary>
        public Warband Warband { get; private set; }

        public RaiderCamp CampById(int id) => _camps.FirstOrDefault(c => c.Id == id);

        /// <summary>Where on the world map a warband is now (world cells): on the straight road from its camp to your settlement.</summary>
        public (double x, double y) WarbandCell(Warband wb)
        {
            var c = CampById(wb.CampId);
            if (c == null) return (HomeCellX, HomeCellY);
            double t = wb.ArriveDay <= wb.LeaveDay ? 1 : Math.Clamp((Day - wb.LeaveDay) / (double)(wb.ArriveDay - wb.LeaveDay), 0, 1);
            return (c.X + (HomeCellX - c.X) * t, c.Y + (HomeCellY - c.Y) * t);
        }

        /// <summary>The side of the settlement map a warband from this camp will come in from ("north", "east", ...).</summary>
        public string CampSideName(RaiderCamp c) => Sides[CampSide(c)];

        /// <summary>Each living camp within RaidFarKm sends a warband now and then (bigger camps and nearer ones more often), once the town is
        /// worth raiding. The warband marches across the world map (MarchKmPerDay) and is spotted SpotDays before it arrives; then the raid begins.</summary>
        void StepCampRaids()
        {
            if (Warband != null) { StepWarband(); return; }
            if (_camps.Count == 0 || Day < 100 || Peasants + Soldiers < RaidMinPopulation) return;
            if (Raid != null && !Raid.Over) return;
            foreach (var c in LiveCamps)
            {
                int km = CampKm(c);
                if (km > RaidFarKm) continue;
                if (!_nextCampRaid.TryGetValue(c.Id, out int due)) { _nextCampRaid[c.Id] = Day + 60 + (int)(Noise.Hash2(c.Id, Day, 94) * 120) + km / 2; continue; }
                if (Day < due) continue;
                _nextCampRaid[c.Id] = Day + 160 - 14 * c.Strength + (int)(Noise.Hash2(c.Id, Day, 95) * 60) + km / 2;
                Warband = new Warband { CampId = c.Id, Band = 3 + 2 * c.Strength, LeaveDay = Day, ArriveDay = Day + MarchDays(c) };
                StepWarband();   // a camp close by may be in sight the day it sets out
                return;   // one warband at a time
            }
        }

        void StepWarband()
        {
            var wb = Warband; var c = CampById(wb.CampId);
            if (c == null) { Warband = null; return; }   // the camps were replaced (a new world): it never comes
            int left = wb.ArriveDay - Day;
            if (!wb.Spotted && left <= SpotDays)
            {
                wb.Spotted = true;
                string who = Blds.Any(b => b.T == "watchtower") ? "Lookouts on your watchtower" : "Scouts";
                string when = left <= 0 ? "today" : left == 1 ? "tomorrow" : $"in about {left} days";
                Events.Add($"{who} spotted a warband of {wb.Band} raiders from the {c.Name}! They will reach you {when}, from the {CampSideName(c)}.");
            }
            if (left > 0 || (Raid != null && !Raid.Over)) return;
            Warband = null;
            Events.Add($"The warband from the {c.Name} is here! Watch the {CampSideName(c)}.");
            BeginRaid(wb.Band, c);
        }

        // ---------- invading a camp ----------

        public Result CanInvade(RaiderCamp camp, IList<BattleGroup> groups)
        {
            if (camp == null || DestroyedCamps.Contains(camp.Id)) return Result.No("That camp is already destroyed");
            int inf = groups.Sum(g => g.Infantry), arc = groups.Sum(g => g.Archers);
            if (inf + arc <= 0) return Result.No("Choose at least one soldier to send");
            if (inf > Infantry || arc > Archers) return Result.No($"You only have {Infantry} infantry and {Archers} archers at home");
            foreach (var g in groups) if (g.Total > 0 && (!CampMap.IsEdge(g.X, g.Y))) return Result.No("Soldiers can only land on the edge of the camp map");
            return Result.Yes();
        }

        /// <summary>Confirm an invasion: the troops leave the settlement now and the battle is ready to be played.</summary>
        public CampBattle BeginInvasion(RaiderCamp camp, IList<BattleGroup> groups)
        {
            var ok = CanInvade(camp, groups);
            if (!ok.Ok) return null;
            int inf = groups.Sum(g => g.Infantry), arc = groups.Sum(g => g.Archers);
            Soldiers -= inf + arc; Archers -= arc;
            return new CampBattle(camp, groups);
        }

        /// <summary>The fight is over: a won camp is destroyed (its raids stop) and the survivors set off home with their loot.</summary>
        public void FinishInvasion(CampBattle battle)
        {
            if (battle == null || !battle.Over || battle.Settled) return;
            battle.Settled = true;
            LastInvasion = battle;
            if (battle.Victory) DestroyedCamps.Add(battle.Camp.Id);
            if (battle.Victory) battle.Camp.ReleaseSite();   // the camp is gone: its map is no longer kept
            int inf = battle.SurvivingInfantry, arc = battle.SurvivingArchers;
            if (inf + arc <= 0) { Events.Add($"The army sent against the {battle.Camp.Name} was lost."); return; }
            var e = new Expedition { CampId = battle.Camp.Id, Infantry = inf, Archers = arc, ReturnDay = Day + MarchDays(battle.Camp) };
            foreach (var kv in battle.Loot) if (kv.Value > 0) e.Loot[kv.Key] = kv.Value;
            Expeditions.Add(e);
            Events.Add(battle.Victory ? $"The {battle.Camp.Name} is destroyed. The army marches home with its spoils." : $"The army pulls out of the {battle.Camp.Name} and marches home.");
        }

        void StepExpeditions()
        {
            for (int i = Expeditions.Count - 1; i >= 0; i--)
            {
                var e = Expeditions[i];
                if (Day < e.ReturnDay) continue;
                Soldiers += e.Infantry + e.Archers; Archers += e.Archers;
                foreach (var kv in e.Loot) ResAmt[kv.Key] += kv.Value;
                string loot = e.Loot.Count == 0 ? "" : " with " + string.Join(", ", e.Loot.Select(k => $"{k.Value} {k.Key.ToString().ToLower()}"));
                Events.Add($"The army is home{loot}: {e.Infantry} infantry and {e.Archers} archers.");
                Expeditions.RemoveAt(i);
            }
        }
    }
}
