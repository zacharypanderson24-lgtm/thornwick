// FRONTIER INCOME: a raider camp you destroyed leaves safe country behind it. Settlers and herders move in and pay for the protection of your
// banner, every month, so a conquering town earns steadily from its victories (as well as the one-off spoils and the tribute of subdued
// settlements, World.Conquest.cs). Derived from the saved set of destroyed camps, so nothing new is saved.
using System;
using System.Linq;

namespace CrownAndHearth.Sim
{
    public partial class World
    {
        public const int FrontierGoldPerStrength = 3;

        /// <summary>Gold a month from every camp you have destroyed (its strength decides how rich the cleared country is).</summary>
        public int FrontierIncome => _camps.Where(c => DestroyedCamps.Contains(c.Id) && !c.IsTown).Sum(c => c.Strength * FrontierGoldPerStrength);

        public int FrontierCamps => _camps.Count(c => DestroyedCamps.Contains(c.Id) && !c.IsTown);

        void StepFrontier()
        {
            int gold = FrontierIncome;
            if (gold <= 0) return;
            ResAmt[Res.Gold] += gold;
            Events.Add($"The country cleared of {FrontierCamps} raider camp{(FrontierCamps == 1 ? "" : "s")} pays {gold} gold this month for your protection.");
        }
    }
}
