// Raider camps on the world map: a handful of red dots scattered over the land of the 600 x 600 km world, generated from the world seed alone
// (nothing about a camp is saved except whether you have destroyed it). Raids on your settlement come from these camps (World.Camps.cs) and
// you can invade one: it opens as a small 32 x 32 map (CampBattle.cs).
using System;
using System.Collections.Generic;

namespace CrownAndHearth.Sim
{
    public sealed class RaiderCamp
    {
        public int Id, X, Y, Strength, Seed;   // X, Y: world cell (1 km); Strength 1..5 sets how many raiders live there and how big their raids are
        public string Name;
        public override string ToString() => $"{Name} ({X},{Y}) strength {Strength}";
    }

    public static class RaiderCamps
    {
        public const int Count = 14, MinApart = 45, NearMin = 50, NearMax = 220;
        static readonly string[] Adjectives = { "Black", "Red", "Grey", "Iron", "Ash", "Wolf", "Crow", "Pale", "Broken", "Bitter", "Hollow", "Cold" };
        static readonly string[] Nouns = { "Hand", "Fang", "Banner", "Spear", "Hollow", "Ford", "Cairn", "Knife", "Brand", "Tooth" };

        /// <summary>The camps of this world: on land, away from your home region, kept apart; the first ones within reach of your settlement.</summary>
        public static List<RaiderCamp> Generate(WorldMap map)
        {
            var camps = new List<RaiderCamp>();
            var home = map.HomeSite;
            int hx = home?.X ?? WorldMap.Size / 2, hy = home?.Y ?? WorldMap.Size / 2;
            int seed = map.Seed;
            for (int attempt = 0; attempt < 6000 && camps.Count < Count; attempt++)
            {
                int x = 8 + (int)(Noise.Hash2(attempt, seed, 701) * (WorldMap.Size - 16)), y = 8 + (int)(Noise.Hash2(attempt, seed, 702) * (WorldMap.Size - 16));
                if (!map.IsLand(x, y)) continue;
                int dist = Math.Max(Math.Abs(x - hx), Math.Abs(y - hy));
                if (dist < NearMin) continue;
                bool needNear = camps.Count < Count / 2;   // half of them are within a few days' march
                if (needNear && dist > NearMax) continue;
                bool apart = true;
                foreach (var c in camps) if (Math.Max(Math.Abs(c.X - x), Math.Abs(c.Y - y)) < MinApart) { apart = false; break; }
                if (!apart) continue;
                double s = Noise.Hash2(x, y, seed + 703);
                int strength = s < 0.25 ? 1 : s < 0.55 ? 2 : s < 0.8 ? 3 : s < 0.92 ? 4 : 5;
                int id = camps.Count;
                camps.Add(new RaiderCamp
                {
                    Id = id, X = x, Y = y, Strength = strength, Seed = seed * 31 + id * 977 + 13,
                    Name = $"{Adjectives[(int)(Noise.Hash2(id, seed, 704) * Adjectives.Length)]} {Nouns[(int)(Noise.Hash2(id, seed, 705) * Nouns.Length)]} camp",
                });
            }
            return camps;
        }
    }
}
