// Troops: the two kinds of professional soldier so far, infantry (spearmen) and archers. `Soldiers` is the total of professionals and
// `Archers` the archers among them, so the raid, camp and conquest code and old saves keep working. Soldiers are no longer made by barracks:
// the military system (Military/MilitarySystem.cs) recruits them from the workforce into companies, houses them in barracks, and keeps its
// companies in step with these two numbers. Each soldier eats half as much again as a peasant and costs gold upkeep (World.Economy.cs).
using System;

namespace CrownAndHearth.Sim
{
    public enum Troop { Infantry, Archer }

    public partial class World
    {
        /// <summary>Whether working residents fight raiders with their trade tools (weakly, next to soldiers or alone). Set in the Military menu.</summary>
        public bool MilitiaFights = true;
        public int Archers;
        public int Infantry => Soldiers - Archers;

        /// <summary>The settlement's military: companies, registered and mobilised militia, professionals.</summary>
        public MilitarySystem Mil { get; private set; }

        /// <summary>The current era (1 to 5). There is no tech tree yet, so it starts at MilitaryConfig.CurrentEra; buildings and units carry their own era and are
        /// refused until the settlement reaches it. Saved.</summary>
        public int Era = MilitaryConfig.CurrentEra;

        public static string TroopName(Troop t) => t == Troop.Archer ? "Archer" : "Infantry";

        /// <summary>One soldier is lost (starvation, casualties): from the bigger group, so the mix stays roughly as it was.</summary>
        void LoseSoldier()
        {
            if (Soldiers <= 0) return;
            if (Archers > 0 && Archers >= Infantry) Archers--;
            Soldiers--;
        }
    }
}
