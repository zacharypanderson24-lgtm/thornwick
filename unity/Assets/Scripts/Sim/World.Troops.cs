// Troops: the first two kinds, infantry and archers. Both are trained from peasants at a served Barracks, both live in bunks, eat half as
// much again as a peasant and cost gold upkeep (World.Economy.cs). `Soldiers` stays the total, so older code and saves keep working; the
// archers are counted separately and the rest are infantry. The Troops menu sets the share of archers among new recruits.
using System;

namespace CrownAndHearth.Sim
{
    public enum Troop { Infantry, Archer }

    public partial class World
    {
        /// <summary>Days a barracks needs between two recruits.</summary>
        public const int TrainDays = 4;

        /// <summary>Whether working residents fight raiders with their trade tools (weakly, next to soldiers or alone). Set in the Troops menu.</summary>
        public bool MilitiaFights = true;
        public int Archers;
        /// <summary>Share of new recruits that are archers, 0..100 (the rest are infantry).</summary>
        public int ArcherPercent;
        public int Infantry => Soldiers - Archers;

        public static string TroopName(Troop t) => t == Troop.Archer ? "Archer" : "Infantry";

        /// <summary>What one recruit costs: infantry need iron for sword and mail, archers need timber for the bow and flax for the string.</summary>
        public static (Res res, int amount)[] TrainCost(Troop t) => t == Troop.Archer
            ? new[] { (Res.Wood, 3), (Res.Gold, 1), (Res.Flax, 1) }
            : new[] { (Res.Wood, 2), (Res.Gold, 1), (Res.Ore, 1) };

        public void SetArcherPercent(int percent)
        {
            int next = Math.Clamp(percent, 0, 100);
            if (ArcherPercent == next) return;
            ArcherPercent = next;
            Touch();
        }

        /// <summary>The kind the next recruit will be: an archer whenever the archers are below their share of the army.</summary>
        public Troop NextRecruit => Archers * 100 < ArcherPercent * (Soldiers + 1) ? Troop.Archer : Troop.Infantry;

        public bool CanAfford(Troop t)
        {
            foreach (var (res, amount) in TrainCost(t)) if (ResAmt[res] < amount) return false;
            return true;
        }

        void TrainOne(Troop t)
        {
            foreach (var (res, amount) in TrainCost(t)) ResAmt[res] -= amount;
            Soldiers++; Peasants--; Trained++;
            if (t == Troop.Archer) Archers++;
        }

        /// <summary>One soldier is lost (starvation, no bunk): from the bigger group, so the mix stays roughly as chosen.</summary>
        void LoseSoldier()
        {
            if (Soldiers <= 0) return;
            if (Archers > 0 && Archers >= Infantry) Archers--;
            Soldiers--;
        }
    }
}
