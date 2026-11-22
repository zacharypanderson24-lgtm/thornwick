// A raider camp drawn like a normal settlement: the invasion screen shows the camp as a small World (flat ground, a few trees and rocks, 1 x 1
// raider tents and 1 x 1 supply piles) that the ordinary ground and object renderers draw, so it looks like the rest of the game. It is
// only for looking at: it is never ticked or saved, the fight itself is the CampBattle, and only a 32 x 32 square of the map is in play.
using System;
using static CrownAndHearth.Sim.Defs;

namespace CrownAndHearth.Sim
{
    public partial class World
    {
        /// <summary>The map tile where the camp's 32 x 32 square starts (the square sits in the middle of the map).</summary>
        public const int CampOrigin = (N - CampMap.Size) / 2;

        (int x0, int y0, int x1, int y1)? _playArea;

        /// <summary>False outside the 32 x 32 camp square of a camp world; always true in a settlement.</summary>
        public bool InPlay(int x, int y) => !_playArea.HasValue || (x >= _playArea.Value.x0 && x <= _playArea.Value.x1 && y >= _playArea.Value.y0 && y <= _playArea.Value.y1);

        public bool IsCamp => _playArea.HasValue;

        public static World CreateCamp(RaiderCamp camp, CampMap map)
        {
            var w = new World(camp.Seed, false);
            w._playArea = (CampOrigin, CampOrigin, CampOrigin + CampMap.Size - 1, CampOrigin + CampMap.Size - 1);
            for (int i = 0; i < w._claimed.Length; i++) w.SetClaimed(i % ParcelsPerSide, i / ParcelsPerSide, true);   // nothing dimmed
            for (int y = 0; y < CampMap.Size; y++)
                for (int x = 0; x < CampMap.Size; x++)
                    w.DecoMap[CampOrigin + y, CampOrigin + x] = map.Ground[y, x] == CampTile.Tree ? Deco.Tree : map.Ground[y, x] == CampTile.Rock ? Deco.Rock : Deco.None;
            foreach (var t in map.Tents) { w.DecoMap[CampOrigin + t.y, CampOrigin + t.x] = Deco.None; w.AddBld("tent", CampOrigin + t.x, CampOrigin + t.y); }
            foreach (var p in map.Piles)
            {
                w.DecoMap[CampOrigin + p.Y, CampOrigin + p.X] = Deco.None;
                var b = w.AddBld("supplies", CampOrigin + p.X, CampOrigin + p.Y);
                b.Var = (int)p.Res;
            }
            return w;
        }
    }
}
