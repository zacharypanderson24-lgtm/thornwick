// HAPPINESS. One 0..100 number for how the settlement feels, built so that adding or removing a cause is a one-line change.
//
// Three kinds of cause, all shown to the player in the same list (HappinessLedger):
//   1. FACTORS     what is true right now (people starving, wells running, jobs for everyone). One static method each, listed in the
//                  Factors table below. They are re-read every day, so they stop counting the moment the cause goes away.
//   2. MOODS       a temporary effect that lasts a number of days (a festival: +10 for 20 days). AddMood(id, delta, days, text).
//   3. SHIFTS      a one-off jump when something happens (a building burns: -4). HappinessShift(delta, text). It moves the number at once
//                  and then it drifts back toward what the factors say.
//
// HOW TO ADD A CAUSE: write `static HappinessEffect? MyThing(World w)` (return null when it does not apply) and add `{ "mything", MyThing }` to
// Factors. To add a temporary one call AddMood from wherever it begins; for a one-off call HappinessShift. Nothing else needs to change:
// the bar, the tooltip and the uprising risk all read the ledger. The numbers live in HappinessTuning so they can be changed in one place.
//
// The number drifts toward Target (50 plus every cause) a little each day, faster downward than upward. Below HappinessTuning.Unrest the
// risk of an uprising rises (World.Uprising.cs); a town that stays miserable long enough always rises.
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace CrownAndHearth.Sim
{
    /// <summary>All the numbers in one place.</summary>
    public static class HappinessTuning
    {
        public const double Start = 60, Baseline = 50;
        /// <summary>How much of the gap to the target closes each day: faster down than up.</summary>
        public const double FallRate = 0.12, RiseRate = 0.06;
        /// <summary>Below Unrest people are restless (the bar turns amber then red); Joyful above Content is a good place to be.</summary>
        public const double Content = 60, Unrest = 35, Misery = 8;
        public const int MiseryDaysToRise = 10;
        public const int MaxLog = 6;
    }

    /// <summary>One line of the ledger: what is moving happiness and by how much.</summary>
    public readonly struct HappinessEffect
    {
        public readonly double Delta;
        public readonly string Text;
        public HappinessEffect(double delta, string text) { Delta = delta; Text = text; }
    }

    public sealed class HappinessMood
    {
        public string Id, Text;
        public double Delta;
        public int EndDay;
    }

    public partial class World
    {
        // ============ the table: add a line here to add a cause ============

        delegate HappinessEffect? Factor(World w);

        static readonly (string id, Factor eval)[] Factors =
        {
            ("food", FoodFactor),
            ("water", WaterFactor),
            ("work", WorkFactor),
            ("danger", DangerFactor),
            ("disaster", DisasterFactor),
            ("pay", PayFactor),
            ("pride", PrideFactor),
        };

        static HappinessEffect? FoodFactor(World w)
        {
            if (w.Starving) return new HappinessEffect(-35, "People are starving");
            double days = w.DailyFoodDemand > 0 ? w.ResAmt[Res.Food] / w.DailyFoodDemand : 99;
            if (days < 5) return new HappinessEffect(-12, "The granaries are nearly empty");
            if (days < 10) return new HappinessEffect(-5, "Food is running low");
            if (days >= 30) return new HappinessEffect(+10, "Full granaries");
            return null;
        }

        static HappinessEffect? WaterFactor(World w)
        {
            if (w.WaterShortDays > 0 || (w.LastWater != null && w.LastWater.Unmet > 0.05)) return new HappinessEffect(-20, "Homes are short of water");
            return new HappinessEffect(+4, "Clean water for every home");
        }

        static HappinessEffect? WorkFactor(World w)
        {
            if (w.Peasants < WorkMinPeasants) return null;   // the opening village has idle hands by design
            double idle = w.D.Idle / (double)Math.Max(1, w.Peasants);
            if (idle >= IdleBadShare) return new HappinessEffect(-15, "Many people have no work");
            if (idle >= IdleWatchShare) return new HappinessEffect(-6, "Some people have no work");
            return new HappinessEffect(+6, "Work for nearly everyone");
        }

        static HappinessEffect? DangerFactor(World w)
        {
            if (w.Raid != null && !w.Raid.Over) return new HappinessEffect(-10, w.Raid.IsUprising ? "Blood in the streets" : "Raiders are in the land");
            if (w.HallHp < HallMaxHp - 0.5) return new HappinessEffect(-4, "The Town Hall is damaged");
            return null;
        }

        static HappinessEffect? DisasterFactor(World w)
        {
            var s = w.CurrentScenario;
            if (s != null && s.Phase == ScenarioPhase.Hitting) return new HappinessEffect(-15, World.ScenarioTitle(s.Id) + " has struck");
            if (s != null && w.ScenarioReadiness() < 40) return new HappinessEffect(-4, "Fear of the " + World.ScenarioTitle(s.Id).ToLower() + " to come");
            return null;
        }

        static HappinessEffect? PayFactor(World w)
        {
            if (w.Soldiers > 0 && w.ResAmt[Res.Gold] <= 0) return new HappinessEffect(-8, "The soldiers are not being paid");
            return null;
        }

        static HappinessEffect? PrideFactor(World w)
        {
            int done = w.MilestonesDone;
            return done > 0 ? new HappinessEffect(Math.Min(10, 2 * done), "Pride in what has been achieved") : (HappinessEffect?)null;
        }

        // ============ state ============

        /// <summary>0..100 (saved).</summary>
        public double Happiness { get; private set; } = HappinessTuning.Start;

        readonly List<HappinessMood> _moods = new List<HappinessMood>();
        readonly List<(int day, double delta, string text)> _happyLog = new List<(int, double, string)>();
        int _miseryDays;
        bool _warnedRestless, _warnedRising;

        public IReadOnlyList<HappinessMood> Moods => _moods;

        /// <summary>Recent one-off jumps, newest last.</summary>
        public IReadOnlyList<(int day, double delta, string text)> HappinessLog => _happyLog;

        // ============ the three kinds of cause ============

        /// <summary>A temporary effect: Delta added to the target for `days` days. A mood with the same id is replaced.</summary>
        public void AddMood(string id, double delta, int days, string text)
        {
            _moods.RemoveAll(m => m.Id == id);
            _moods.Add(new HappinessMood { Id = id, Delta = delta, EndDay = Day + days, Text = text });
        }

        /// <summary>A one-off jump now (positive or negative). It then drifts back toward the target like everything else.</summary>
        public void HappinessShift(double delta, string text)
        {
            Happiness = Math.Clamp(Happiness + delta, 0, 100);
            _happyLog.Add((Day, delta, text));
            if (_happyLog.Count > HappinessTuning.MaxLog) _happyLog.RemoveAt(0);
        }

        // ============ reading it ============

        /// <summary>Every cause in force today, biggest first: the factors that apply, then the moods.</summary>
        public List<HappinessEffect> HappinessLedger()
        {
            var list = new List<HappinessEffect>();
            foreach (var (_, eval) in Factors) { var e = eval(this); if (e is HappinessEffect effect && effect.Delta != 0) list.Add(effect); }
            foreach (var m in _moods) if (m.EndDay > Day) list.Add(new HappinessEffect(m.Delta, m.Text));
            return list.OrderByDescending(e => Math.Abs(e.Delta)).ToList();
        }

        /// <summary>Where happiness is heading: the baseline plus every cause in the ledger, kept in 0..100.</summary>
        public double HappinessTarget() => Math.Clamp(HappinessTuning.Baseline + HappinessLedger().Sum(e => e.Delta), 0, 100);

        public bool Restless => Happiness < HappinessTuning.Unrest;

        public string MoodName => Happiness >= 80 ? "Joyful" : Happiness >= HappinessTuning.Content ? "Content" : Happiness >= HappinessTuning.Unrest ? "Uneasy" :
            Happiness >= 15 ? "Restless" : "On the edge of revolt";

        // ============ the daily step ============

        /// <summary>Called once a day at the end of the tick (the campaign switch gates it, so tests that tick worlds are unaffected).</summary>
        void StepHappiness()
        {
            if (!CampaignOn || Defeated) return;
            _moods.RemoveAll(m => m.EndDay <= Day);
            double target = HappinessTarget();
            double gap = target - Happiness;
            double step = gap * (gap < 0 ? HappinessTuning.FallRate : HappinessTuning.RiseRate);
            if (Math.Abs(step) < 0.4) step = Math.Abs(gap) < 0.4 ? gap : Math.Sign(gap) * 0.4;
            Happiness = Math.Clamp(Happiness + step, 0, 100);

            _miseryDays = Happiness <= HappinessTuning.Misery ? _miseryDays + 1 : 0;
            if (Happiness < HappinessTuning.Unrest && !_warnedRestless)
            {
                _warnedRestless = true;
                Events.Add("The people are restless. Feed them, give them work, hold a festival, and keep soldiers at home.");
            }
            else if (Happiness >= HappinessTuning.Unrest + 10) { _warnedRestless = false; _warnedRising = false; }
            if (Happiness < 20 && !_warnedRising) { _warnedRising = true; Events.Add("The mood is ugly. An uprising could break out any day."); }

            StepUprisings();
        }

        // ============ festivals: the lever the player pulls ============

        public const int FestivalGold = 25, FestivalFood = 150, FestivalCooldown = 40, FestivalDays = 20;
        public const double FestivalShift = 20, FestivalMood = 10;

        /// <summary>The day the last festival was held (saved).</summary>
        public int LastFestivalDay { get; private set; } = -1000;

        public int FestivalReadyIn => Math.Max(0, LastFestivalDay + FestivalCooldown - Day);

        public Result CanHoldFestival()
        {
            if (Defeated) return Result.No("The settlement has fallen");
            if (Raid != null && !Raid.Over) return Result.No("Not while the town is under attack");
            if (FestivalReadyIn > 0) return Result.No($"The people need {FestivalReadyIn} more days to recover from the last festival");
            if (ResAmt[Res.Gold] < FestivalGold) return Result.No($"A festival costs {FestivalGold} gold");
            if (ResAmt[Res.Food] < FestivalFood) return Result.No($"A festival needs {FestivalFood} food for the feast");
            return Result.Yes();
        }

        public Result HoldFestival()
        {
            var can = CanHoldFestival();
            if (!can.Ok) return can;
            ResAmt[Res.Gold] -= FestivalGold;
            ResAmt[Res.Food] -= FestivalFood; _wheat = WheatStored - WheatShare(FestivalFood);
            LastFestivalDay = Day;
            HappinessShift(FestivalShift, "A festival");
            AddMood("festival", FestivalMood, FestivalDays, "Memories of the festival");
            Events.Add("The town holds a festival. Spirits are high.");
            Touch();
            return Result.Yes();
        }

        // ============ saving ============

        JObject SaveHappiness() => new JObject
        {
            ["value"] = Math.Round(Happiness, 2), ["misery"] = _miseryDays, ["festival"] = LastFestivalDay, ["uprising"] = LastUprisingDay,
            ["moods"] = new JArray(_moods.Select(m => new JObject { ["id"] = m.Id, ["text"] = m.Text, ["delta"] = m.Delta, ["end"] = m.EndDay })),
        };

        void LoadHappiness(JObject o)
        {
            _moods.Clear(); _happyLog.Clear();
            Happiness = o == null ? HappinessTuning.Start : Math.Clamp((double?)o["value"] ?? HappinessTuning.Start, 0, 100);
            _miseryDays = (int?)o?["misery"] ?? 0;
            LastFestivalDay = (int?)o?["festival"] ?? -1000;
            LastUprisingDay = (int?)o?["uprising"] ?? -1000;
            _warnedRestless = Happiness < HappinessTuning.Unrest; _warnedRising = Happiness < 20;
            if (o?["moods"] is JArray moods)
                foreach (var m in moods)
                    _moods.Add(new HappinessMood { Id = (string)m["id"], Text = (string)m["text"], Delta = (double)m["delta"], EndDay = (int)m["end"] });
        }
    }
}
