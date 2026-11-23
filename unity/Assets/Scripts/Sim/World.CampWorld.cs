// A raider camp is generated exactly like a settlement map: a World of the same size made by the same terrain generator (hills, mountains
// where the region is mountainous, trees of every species, rock outcrops), with the camp's seed and the relief of the region it lies in. A
// 32 x 32 piece of that terrain is chosen (mostly open ground with a walkable edge to land on, mountains and woods where they happen to be) and
// the camp is built on it: tents round campfires, the warlord's tent, weapon racks and supply piles, all 1 x 1. The ordinary renderers draw it,
// only that 32 x 32 square is in play (World.InPlay), and it is never ticked or saved; the fight itself is the CampBattle.
using System;
using static CrownAndHearth.Sim.Defs;

namespace CrownAndHearth.Sim
{
    /// <summary>One camp's ground and layout: the little world to draw, where the 32 x 32 square sits on it, and the camp map the battle uses.</summary>
    public sealed class CampSite
    {
        public World World;
        public CampMap Map;
        public int X0, Y0;
    }

    public partial class World
    {
        (int x0, int y0, int x1, int y1)? _playArea;
        /// <summary>The map tile where a camp world's 32 x 32 square starts.</summary>
        public int CampX0 { get; private set; }
        public int CampY0 { get; private set; }

        /// <summary>False outside the 32 x 32 camp square of a camp world; always true in a settlement.</summary>
        public bool InPlay(int x, int y) => !_playArea.HasValue || (x >= _playArea.Value.x0 && x <= _playArea.Value.x1 && y >= _playArea.Value.y0 && y <= _playArea.Value.y1);

        public bool IsCamp => _playArea.HasValue;

        bool CampBuildable(int x, int y) => !IsSolid(x, y) && !HasTree(x, y) && DecoMap[y, x] != Deco.Rock && TileSpread(x, y) == 0;

        /// <summary>Choose where an n x n square sits: the first of 32 places picked from the seed that is mostly open, level ground with a
        /// walkable edge and a clearing `half` tiles round its middle.</summary>
        (int x0, int y0) PickWindow(int seed, int n, int half)
        {
            if (n >= N - 16) return ((N - n) / 2, (N - n) / 2);   // a map nearly as big as the world: all of it
            int bestX = Start - n / 2, bestY = bestX; double bestScore = -1;
            int mid = n / 2;
            for (int i = 0; i < 32; i++)
            {
                int ox = 8 + (int)(Noise.Hash2(i, 1, seed + 30) * (N - n - 16)), oy = 8 + (int)(Noise.Hash2(i, 2, seed + 30) * (N - n - 16));
                int open = 0, ring = 0, ringTotal = 0, core = 0, coreTotal = 0;
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        bool b = CampBuildable(ox + x, oy + y);
                        if (b) open++;
                        if (Math.Abs(x - mid) <= half && Math.Abs(y - mid) <= half) { coreTotal++; if (b) core++; }
                        if (x < 2 || y < 2 || x >= n - 2 || y >= n - 2) { ringTotal++; if (!IsSolid(ox + x, oy + y)) ring++; }
                    }
                double openFrac = open / (double)(n * n), ringFrac = ring / (double)ringTotal, coreFrac = core / (double)coreTotal;
                if (openFrac >= 0.55 && ringFrac >= 0.75 && coreFrac >= 0.7) return (ox, oy);
                double score = Math.Min(openFrac / 0.55, 1) + Math.Min(ringFrac / 0.75, 1) + Math.Min(coreFrac / 0.7, 1);
                if (score > bestScore) { bestScore = score; bestX = ox; bestY = oy; }
            }
            return (bestX, bestY);
        }

        /// <summary>Shared by camps and towns: the world, its square (x0, y0, n), nothing dimmed, and an open landing edge (woods and boulders
        /// cleared from the two outer rings; mountains stay).</summary>
        static World MakeBattleGround(int seed, Relief relief, int n, int half, out int x0, out int y0)
        {
            var w = new World(seed, false, TerrainStyle.Ranges, relief);
            w.GenTerrain();   // the same generator as a settlement
            (x0, y0) = w.PickWindow(seed, n, half);
            w._playArea = (x0, y0, x0 + n - 1, y0 + n - 1);
            w.CampX0 = x0; w.CampY0 = y0;
            for (int i = 0; i < w._claimed.Length; i++) w.SetClaimed(i % ParcelsPerSide, i / ParcelsPerSide, true);   // nothing dimmed
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                    if (x < 2 || y < 2 || x >= n - 2 || y >= n - 2)
                    {
                        var d = w.DecoMap[y0 + y, x0 + x];
                        if (d == Deco.Tree || d == Deco.Sapling || d == Deco.Stump || (d == Deco.Rock && !w.IsMountain(x0 + x, y0 + y))) w.DecoMap[y0 + y, x0 + x] = Deco.None;
                    }
            return w;
        }

        public static CampSite CreateCamp(RaiderCamp camp)
        {
            if (camp.IsTown) return CreateTown(camp);   // World.TownBuilder.cs: a real town, built like yours
            // the camp needs a clearing in the middle as wide as its fires reach (a strength 5 camp a big one)
            var w = MakeBattleGround(camp.Seed, camp.Relief, CampMap.Size, CampSize.Of(camp.Strength).Reach + 1, out int x0, out int y0);
            var map = CampMap.FromTerrain(w, x0, y0, camp);
            void Clear(int x, int y) => w.DecoMap[y0 + y, x0 + x] = Deco.None;
            foreach (var t in map.Tents) { Clear(t.x, t.y); w.AddBld("tent", x0 + t.x, y0 + t.y); }
            Clear(map.Chief.x, map.Chief.y); w.AddBld("chieftent", x0 + map.Chief.x, y0 + map.Chief.y);
            foreach (var f in map.Fires) { Clear(f.x, f.y); w.AddBld("campfire", x0 + f.x, y0 + f.y); }
            foreach (var r in map.Racks) { Clear(r.x, r.y); w.AddBld("rack", x0 + r.x, y0 + r.y); }
            foreach (var p in map.Piles) { Clear(p.X, p.Y); w.AddBld("supplies", x0 + p.X, y0 + p.Y).Var = (int)p.Res; }
            return new CampSite { World = w, Map = map, X0 = x0, Y0 = y0 };
        }

        /// <summary>A camp world: take away the supply piles that have been emptied (plundered, or carried off after a victory) so they are no
        /// longer drawn. Cheap; called every frame while the camp is shown. True if anything was taken away.</summary>
        public bool ClearLootedPiles(CampMap map)
        {
            if (!IsCamp || map == null) return false;
            bool changed = false;
            foreach (var p in map.Piles)
            {
                if (p.Amount > 0) continue;
                int x = CampX0 + p.X, y = CampY0 + p.Y;
                var b = Grid[y, x];
                if (b == null || b.T != "supplies") continue;
                Grid[y, x] = null; Blds.Remove(b); changed = true;
            }
            if (changed) Touch();
            return changed;
        }
    }
}
