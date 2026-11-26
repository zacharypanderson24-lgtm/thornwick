// Makes one notable person, deterministically from (world seed, settlement, serial). Weighted, not uniform: most people are average, the background
// leans two attributes, and about one in twenty five has one exceptional attribute.
using System;
using System.Collections.Generic;

namespace CrownAndHearth.Sim
{
    /// <summary>What a settlement looks like to the generator: its size, its era and the real mix of its economy (a weight per Background).</summary>
    public sealed class PoolContext
    {
        public int SettlementId, Population, Seed, Era = 1;
        public string Climate = "temperate";
        public readonly double[] Weights = new double[7];   // Background order
    }

    public static class CharacterGenerator
    {
        // which attributes a background leans, and by how much: (attribute, bonus) twice
        static readonly (Attr a, int bonus, Attr b, int bonus2)[] Lean =
        {
            (Attr.Stewardship, 6, Attr.Administration, 4),    // Local
            (Attr.Stewardship, 14, Attr.Economics, 6),        // Farmer
            (Attr.Economics, 14, Attr.Diplomacy, 8),          // Merchant
            (Attr.Economics, 10, Attr.Stewardship, 8),        // Craftsman
            (Attr.Military, 14, Attr.Stewardship, 6),         // Veteran
            (Attr.Administration, 14, Attr.Diplomacy, 6),     // Educated
            (Attr.Diplomacy, 12, Attr.Economics, 6),          // Foreign
        };
        static readonly string[] Archetype = { "civil", "agricultural", "merchant", "civil", "military", "civil", "diplomatic" };
        static readonly string[][] Occupations =
        {
            new[] { "Villager", "Herdsman", "Fisher", "Woodcutter" },
            new[] { "Farmer", "Landowner", "Shepherd", "Miller" },
            new[] { "Merchant", "Trader", "Shopkeeper", "Peddler" },
            new[] { "Smith", "Carpenter", "Mason", "Charcoal burner" },
            new[] { "Veteran", "Retired guard", "Archer", "Sergeant" },
            new[] { "Scribe", "Priest", "Clerk", "Tutor" },
            new[] { "Newcomer", "Travelling trader", "Exile", "Craft worker" },
        };
        static readonly string[] First =
        {
            "Aldric", "Berta", "Cedric", "Daria", "Edmund", "Edith", "Fulk", "Godiva", "Hugh", "Isolde", "Jorund", "Kenna", "Leofric", "Matilda",
            "Nigel", "Orla", "Piers", "Quenna", "Roland", "Sibyl", "Tobias", "Una", "Viggo", "Wilda", "Osric", "Yara", "Bram", "Cora", "Dunstan", "Elga",
            "Ferris", "Gwen", "Harald", "Ivo", "Joan", "Kurt", "Liese", "Merek", "Nessa", "Oswin", "Petra", "Rurik", "Sunniva", "Tove", "Ulric", "Verna",
        };
        static readonly string[] Last =
        {
            "Ashby", "Brook", "Carter", "Dunmore", "Eastley", "Fenwick", "Garrow", "Hale", "Ironside", "Kemp", "Langley", "Marsh", "Northcott", "Oakes",
            "Pell", "Quill", "Ridley", "Stoker", "Thatcher", "Underhill", "Vane", "Wren", "Yarrow", "Alder", "Bowman", "Cooper", "Draycott", "Fisher",
            "Granger", "Hartley", "Mercer", "Pike", "Sutton", "Tanner", "Weaver", "Wright",
        };

        public static string BackgroundName(Background b) => b.ToString();

        /// <summary>Pick a background by the settlement's economy weights.</summary>
        public static Background PickBackground(PoolContext ctx, ref CharRng rng) => (Background)rng.Weighted(ctx.Weights);

        public static Character Create(int id, PoolContext ctx, long serial, int day, Background? forced = null, int minAge = 18, int maxAge = 0)
        {
            var rng = new CharRng(ctx.Seed, ctx.SettlementId * 7919L + 13, serial);
            var bg = forced ?? PickBackground(ctx, ref rng);
            var c = new Character { Id = id, Background = bg, HomeSettlement = ctx.SettlementId, Settlement = ctx.SettlementId };
            c.Name = First[rng.Int(First.Length)] + " " + Last[rng.Int(Last.Length)];
            c.Occupation = Occupations[(int)bg][rng.Int(Occupations[(int)bg].Length)];
            c.Employment = Employment.Civilian;

            // age: most are adults, a few young, a few old; people are 18 or more
            int age;
            if (maxAge > 0) age = rng.Range(minAge, maxAge);
            else
            {
                double r = rng.Double();
                age = r < 0.25 ? rng.Range(18, 29) : r < 0.65 ? rng.Range(30, 44) : r < 0.90 ? rng.Range(45, 59) : rng.Range(60, 74);
            }
            c.BirthDay = day - age * CharacterBalance.YearLength - rng.Int(CharacterBalance.YearLength);
            c.Stage = c.StageAt(day);

            // attributes: a bell curve, the background leans two of them
            var lean = Lean[(int)bg];
            for (int i = 0; i < 5; i++)
            {
                double v = CharacterBalance.AttrMean + (rng.Bell() - 0.5) * CharacterBalance.AttrSpread;
                if (i == (int)lean.a) v += lean.bonus;
                if (i == (int)lean.b) v += lean.bonus2;
                c.Attr[i] = Math.Clamp((int)Math.Round(v), CharacterBalance.AttrMin, CharacterBalance.AttrMax);
            }
            if (rng.Chance(CharacterBalance.ExceptionalChance))   // a gifted person, usually in what their background suits
            {
                int i = rng.Chance(0.6) ? (int)(rng.Chance(0.5) ? lean.a : lean.b) : rng.Int(5);
                c.Attr[i] = Math.Max(c.Attr[i], CharacterBalance.ExceptionalFloor + rng.Int(CharacterBalance.AttrMax - CharacterBalance.ExceptionalFloor + 1));
            }

            // traits: one to three, no opposed pairs
            int n = rng.Chance(0.50) ? 1 : rng.Chance(0.70) ? 2 : 3;
            for (int guard = 0; c.Traits.Count < n && guard < 40; guard++)
            {
                var t = (Trait)rng.Int(Traits.All.Length);
                if (c.Traits.Contains(t)) continue;
                bool clash = false;
                foreach (var h in c.Traits) { var d = Traits.Get(h); var e = Traits.Get(t); if (d.Opposes == t || e.Opposes == h) clash = true; }
                if (!clash) c.Traits.Add(t);
            }

            // standing
            int loy = 50 + (int)Math.Round((rng.Bell() - 0.5) * 60), amb = 35 + (int)Math.Round((rng.Bell() - 0.5) * 60);
            foreach (var t in c.Traits) { loy += Traits.Get(t).Loyalty; amb += Traits.Get(t).Ambition; }
            c.Loyalty = Math.Clamp(loy, 0, 100); c.Ambition = Math.Clamp(amb, 0, 100);
            c.Health = Math.Clamp(100 - Math.Max(0, age - 35) * 8 / 10 - rng.Int(11), 20, 100);
            double exp = (age - 18) * 1.1 + rng.Int(9) + (bg == Background.Veteran || bg == Background.Educated ? 6 : 0);
            c.ExperienceHundredths = Math.Clamp((int)Math.Round(exp * 100), 0, 7000);

            // appearance: the seed fixes the face; the tendencies fix how the hair ages
            var look = c.Look;
            look.Seed = (int)(rng.Next() & 0x3FFFFFFF);
            look.Climate = ctx.Climate; look.Archetype = Archetype[(int)bg];
            look.GreyAge = rng.Range(38, 62);
            look.RecedeAge = rng.Chance(0.45) ? rng.Range(30, 60) : 0;
            look.BaldAge = look.RecedeAge > 0 && rng.Chance(0.5) ? look.RecedeAge + rng.Range(8, 25) : 0;

            c.Log(day, age >= 18 && day > 1 ? "Came to notice" : "Lived here before the town began");
            return c;
        }
    }
}
