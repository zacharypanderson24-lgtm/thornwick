// UPRISINGS. When happiness stays low the workers rise. They are fought with the raid machinery (World.Raids.cs) so everything a raid has
// is already there: your soldiers fight them, the Town Hall can be hit, civilians run for shelter. The differences:
//   - the rebels are your own workers: they spawn at workplaces (drawn with the militia sprites of their trade), and each is one peasant
//     fewer from the moment the uprising begins;
//   - they march on the Town Hall and keep hitting it, every day, until they are killed (they never flee or loot);
//   - if the Hall is destroyed the settlement falls (World.Defeat.cs).
// Soldiers at home are the answer: they are what stops the rebels reaching the Hall.
//
// RISK. Below HappinessTuning.Unrest the chance each day grows from 0 at the threshold to RiskAtZero when happiness is 0. A town whose
// happiness stays at HappinessTuning.Misery or lower for MiseryDaysToRise days always rises. Never in the first UprisingFirstDay days,
// never with fewer than UprisingMinPeasants peasants, never while another fight is on, and not again within UprisingCooldown days.
using System;
using System.Collections.Generic;
using System.Linq;

namespace CrownAndHearth.Sim
{
    public partial class World
    {
        public const int UprisingFirstDay = 60, UprisingMinPeasants = 12, UprisingCooldown = 60, UprisingMin = 6, UprisingMax = 30;
        public const double RiskAtZero = 0.03, RebelHallHit = 3;

        /// <summary>The day the last uprising began (saved).</summary>
        public int LastUprisingDay { get; private set; } = -1000;
        bool _uprisingReported = true;

        /// <summary>The chance an uprising breaks out today: 0 when people are content, rising as happiness falls.</summary>
        public double UprisingRisk()
        {
            if (Happiness >= HappinessTuning.Unrest) return 0;
            return RiskAtZero * (HappinessTuning.Unrest - Happiness) / HappinessTuning.Unrest;
        }

        /// <summary>Why an uprising cannot happen now, or "" when it can.</summary>
        string UprisingBlocked()
        {
            if (Day < UprisingFirstDay) return "too early";
            if (Peasants < UprisingMinPeasants) return "too few people";
            if (Raid != null && !Raid.Over) return "a fight is on";
            if (Day - LastUprisingDay < UprisingCooldown) return "too soon";
            if (Hall == null) return "no Hall";
            return "";
        }

        void StepUprisings()
        {
            // an uprising that has just ended: say how it went and let the town breathe
            if (!_uprisingReported && Raid != null && Raid.IsUprising && Raid.Over)
            {
                _uprisingReported = true;
                Events.Add($"The uprising is crushed. {Raid.Killed} rebels fell and {Raid.SoldiersLost} of your soldiers.");
                Happiness = Math.Max(Happiness, 40);   // fear of the garrison, and the worst is out
                _miseryDays = 0;
                return;
            }
            if (UprisingBlocked() != "") return;
            bool forced = _miseryDays >= HappinessTuning.MiseryDaysToRise;
            if (!forced && !(Noise.Hash2(Day, Seed, 140) < UprisingRisk())) return;
            BeginUprising(Math.Clamp(Peasants / 4, UprisingMin, Math.Min(UprisingMax, Peasants - 2)));
        }

        /// <summary>The workers rise now: `rebels` peasants leave their work and march on the Town Hall. (The scheduler uses it; tests can too.)</summary>
        public RaidReport BeginUprising(int rebels)
        {
            rebels = Math.Clamp(rebels, 1, Math.Max(1, Peasants));
            var r = new RaidReport { Band = rebels, Defenders = Soldiers, StartDay = Day, Camp = null, IsUprising = true, From = "within" };
            Raid = r; _militiaWounds = 0;
            // they come out of the workplaces (and, when there are too few, the homes), a few more each day
            var sources = Blds.Where(b => b.Workers > 0 && RebelTrade(b.T) != null).OrderBy(b => b.X).ThenBy(b => b.Y).ToList();
            if (sources.Count == 0) sources = Blds.Where(b => b.T == "house").OrderBy(b => b.X).ThenBy(b => b.Y).ToList();
            if (sources.Count == 0) sources.Add(Hall);
            for (int i = 0; i < rebels; i++)
            {
                var from = sources[(int)Math.Floor(Noise.Hash2(Day, Seed + i, 141) * sources.Count)];
                var (sx, sy) = StandBeside(from);
                r.Raiders.Add(new Raider { Id = i, Delay = i / 4, X = sx, Y = sy, Px = sx, Py = sy, St = RaiderState.Waiting, Rebel = true, Trade = RebelTrade(from.T) ?? "farm" });
            }
            Peasants = Math.Max(0, Peasants - rebels);
            SpawnFighters(r);
            LastUprisingDay = Day; _uprisingReported = false;
            Events.Add($"UPRISING! {rebels} workers have taken up their tools and are marching on the Town Hall. Your soldiers must stop them.");
            Touch();
            return r;
        }

        /// <summary>Which militia sprite a rebel from this building wears: its trade, or null for buildings whose people do not rise.</summary>
        public static string RebelTrade(string buildingType) => buildingType switch
        {
            "farm" or "hunter" or "wood" or "forester" or "sawmill" or "quarry" or "mine" => buildingType,
            _ => null,
        };
    }
}
