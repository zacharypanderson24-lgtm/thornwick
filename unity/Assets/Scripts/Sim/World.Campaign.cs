// The campaign: a short ladder of goals that give the settlement a direction, each with a gold reward. Done milestones are saved; the
// first one not yet done is the current goal. Finishing the last one marks the campaign complete and play carries on (a sandbox after).
// Checked once a day at the end of the tick; nothing here changes the economy except the gold reward.
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using static CrownAndHearth.Sim.Defs;

namespace CrownAndHearth.Sim
{
    public sealed class Milestone
    {
        public string Id, Title, Hint;
        public int Need, Reward;
        public Func<World, int> Have;
    }

    public partial class World
    {
        public static readonly Milestone[] Milestones =
        {
            new Milestone { Id = "year", Title = "Survive the first year", Hint = "Keep everyone fed and watered through a full year (360 days).", Need = 360, Reward = 40,
                Have = w => Math.Min(360, w.Day - 1) },
            new Milestone { Id = "village", Title = "A true village", Hint = "Grow to 25 people. Zone housing, farms and a well.", Need = 25, Reward = 40,
                Have = w => w.Peasants + w.Soldiers },
            new Milestone { Id = "guard", Title = "Raise a guard", Hint = "Train 8 soldiers at a barracks. Raiders come once the town is worth robbing.", Need = 8, Reward = 50,
                Have = w => w.Soldiers },
            new Milestone { Id = "repel", Title = "Drive off raiders", Hint = "Kill at least one raider in a raid. Soldiers, walls and watchtowers help.", Need = 1, Reward = 60,
                Have = w => w.Raid != null && w.Raid.Over && w.Raid.Killed > 0 ? 1 : 0 },
            new Milestone { Id = "town", Title = "A growing town", Hint = "Grow to 60 people.", Need = 60, Reward = 80,
                Have = w => w.Peasants + w.Soldiers },
            new Milestone { Id = "land", Title = "Claim more land", Hint = "Own 24 parcels (16 x 16 tiles each). Use Claim Land.", Need = 24, Reward = 100,
                Have = w => w.ClaimedParcels },
            new Milestone { Id = "power", Title = "A regional power", Hint = "Grow to 120 people with 20 soldiers.", Need = 120, Reward = 200,
                Have = w => w.Soldiers >= 20 ? w.Peasants + w.Soldiers : Math.Min(w.Peasants + w.Soldiers, 119) },
        };

        readonly HashSet<string> _milestonesDone = new HashSet<string>();

        public bool MilestoneDone(string id) => _milestonesDone.Contains(id);
        public int MilestonesDone => Milestones.Count(m => _milestonesDone.Contains(m.Id));
        public bool CampaignComplete => MilestonesDone == Milestones.Length;
        public Milestone CurrentGoal => Milestones.FirstOrDefault(m => !_milestonesDone.Contains(m.Id));

        /// <summary>How far the current goal is: (have, need), both 0 when the campaign is complete.</summary>
        public (int have, int need) GoalProgress()
        {
            var g = CurrentGoal;
            return g == null ? (0, 0) : (Math.Min(g.Need, g.Have(this)), g.Need);
        }

        /// <summary>Called once a day. Any milestone whose goal is met is done (earlier ones need not come first), and pays its reward once.</summary>
        void StepCampaign()
        {
            if (Defeated || !CampaignOn) return;
            foreach (var m in Milestones)
            {
                if (_milestonesDone.Contains(m.Id) || m.Have(this) < m.Need) continue;
                _milestonesDone.Add(m.Id);
                ResAmt[Res.Gold] += m.Reward;
                Events.Add(CampaignComplete
                    ? $"Goal reached: {m.Title} (+{m.Reward} gold). The campaign is won: your settlement is a regional power. Play on as long as you like."
                    : $"Goal reached: {m.Title} (+{m.Reward} gold). Next: {CurrentGoal.Title}.");
            }
        }

        JArray SaveCampaign() => new JArray(Milestones.Where(m => _milestonesDone.Contains(m.Id)).Select(m => m.Id));

        void LoadCampaign(JArray done, bool on)
        {
            _milestonesDone.Clear();
            CampaignOn = on;
            if (done != null) foreach (var t in done) _milestonesDone.Add((string)t);
        }

        /// <summary>Turn the campaign on (a new game, or an older save that never had it). What the town already beats is marked done without a reward or news.</summary>
        public void StartCampaign()
        {
            CampaignOn = true;
            foreach (var m in Milestones) if (m.Have(this) >= m.Need) _milestonesDone.Add(m.Id);
        }

        /// <summary>Off by default so tests and tools that tick worlds are unaffected (rewards would change their gold); the game turns it on.</summary>
        public bool CampaignOn { get; private set; }
    }
}
