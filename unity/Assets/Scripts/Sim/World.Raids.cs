// Raids: a band of raiders hits the settlement, carries off a share of the stockpile and kills some defenders. Every soldier on the
// settlement holds off one raider; the more of the band is held off, the less is stolen. Used first by the tutorial (Tutorial.cs) to show
// why a settlement needs troops; later attacks can reuse it.
using System;
using System.Collections.Generic;

namespace CrownAndHearth.Sim
{
    public sealed class RaidReport
    {
        public int Raiders, Defenders, DefendersLost;
        public double HeldOff;   // 0..1, the share of the band the soldiers stopped
        public readonly List<(Res res, int amount)> Stolen = new List<(Res, int)>();

        public string Summary()
        {
            var parts = new List<string>();
            foreach (var (res, amount) in Stolen) if (amount > 0) parts.Add($"{amount} {res.ToString().ToLower()}");
            string loot = parts.Count == 0 ? "nothing" : string.Join(", ", parts);
            string defence = Defenders == 0 ? "You had no soldiers to stop them."
                : $"{Defenders} soldiers held off {HeldOff * 100:0}% of the band" + (DefendersLost > 0 ? $" and {DefendersLost} fell." : ".");
            return $"{Raiders} raiders swept in. {defence}\nThey made off with: {loot}.";
        }
    }

    public partial class World
    {
        /// <summary>The share of each stockpile a band that nobody stops carries off.</summary>
        public const double RaidTheft = 0.6;

        static readonly Res[] Plunder = { Res.Gold, Res.Food, Res.Wood, Res.Logs, Res.Stone, Res.Ore, Res.Flax };

        public RaidReport Raid(int raiders)
        {
            var r = new RaidReport { Raiders = raiders, Defenders = Soldiers };
            r.HeldOff = raiders <= 0 ? 1 : Math.Min(1.0, Soldiers / (double)raiders);
            double taken = RaidTheft * (1 - r.HeldOff);
            foreach (var res in Plunder)
            {
                int amount = (int)(ResAmt[res] * taken);
                ResAmt[res] -= amount;
                r.Stolen.Add((res, amount));
            }
            // fighting costs lives: a quarter of the soldiers who stood against the band
            r.DefendersLost = (int)(Math.Min(Soldiers, raiders) * 0.25);
            for (int i = 0; i < r.DefendersLost; i++) LoseSoldier();
            Touch();
            return r;
        }
    }
}
