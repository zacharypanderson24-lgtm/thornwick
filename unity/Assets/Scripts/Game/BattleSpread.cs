// Spreads soldiers and raiders out inside their tile when they are drawn, so a fight is a crowd and not one sprite stacked on another.
// Pure arithmetic (no UnityEngine): drawing only, the battle rules never see these offsets. Every unit gets its own steady wobble from
// its id, and units that share a tile (a squad marching one path, several soldiers round one raider) are placed round a ring in order.
using System;
using System.Collections.Generic;

namespace CrownAndHearth.Game
{
    public sealed class BattleSpread
    {
        /// <summary>Most a unit is moved from its tile centre, in screen pixels (a tile is 32 wide, 16 tall).</summary>
        public const int MaxX = 13, MaxY = 6;

        readonly Dictionary<long, int> _seen = new Dictionary<long, int>();

        /// <summary>Start a new frame: nobody has been placed yet.</summary>
        public void Reset() => _seen.Clear();

        /// <summary>Offset in screen pixels for one unit standing on tile (x, y). Call once per unit per frame, in a steady order.
        /// Bodies are kept apart from the living so a corpse does not push a soldier off its spot.</summary>
        public (int dx, int dy) Place(int id, int x, int y, bool dead)
        {
            long key = ((long)(y * 4096 + x) << 1) | (dead ? 1L : 0L);
            _seen.TryGetValue(key, out int n);
            _seen[key] = n + 1;
            return Offset(id, n);
        }

        /// <summary>The offset of the n-th unit (0 = first) on a tile, for the unit with this id.</summary>
        public static (int dx, int dy) Offset(int id, int n)
        {
            // a steady wobble of its own, so even a lone unit does not walk the exact centre line
            int dx = Hash(id, 37, 11) % 9 - 4, dy = Hash(id, 53, 5) % 5 - 2;
            if (n > 0)
            {
                // the next ones go round a ring, growing as the crowd does (golden angle keeps neighbours apart)
                double angle = n * 2.399963 + (id % 7) * 0.2, r = Math.Min(1.0, (4 + 3 * Math.Sqrt(n)) / MaxX);
                dx += (int)Math.Round(Math.Cos(angle) * r * (MaxX - 4));
                dy += (int)Math.Round(Math.Sin(angle) * r * (MaxY - 2));
            }
            return (Math.Max(-MaxX, Math.Min(MaxX, dx)), Math.Max(-MaxY, Math.Min(MaxY, dy)));
        }

        static int Hash(int id, int mul, int mod) => (int)(((long)Math.Abs(id) * mul + 17) % mod + mod) % mod;
    }
}
