// The 2:1 isometric projection and 4-way camera rotation, ported from src/ui.js (toView, fromView, vertW, screenToTile).
// "Screen pixels" are game pixels with y pointing down, as in the browser; 1 Unity unit = 32 game pixels (one tile width).
using CrownAndHearth.Sim;
using UnityEngine;
using static CrownAndHearth.Sim.Defs;

namespace CrownAndHearth.Game
{
    public static class IsoView
    {
        public const float PPU = 32f;

        /// <summary>World tile -> view tile for rotation r (JS toView).</summary>
        public static Vector2Int ToView(int x, int y, int r)
        {
            int m = N - 1;
            switch (r) { case 1: return new Vector2Int(m - y, x); case 2: return new Vector2Int(m - x, m - y); case 3: return new Vector2Int(y, m - x); default: return new Vector2Int(x, y); }
        }

        /// <summary>View tile -> world tile (JS fromView).</summary>
        public static Vector2Int FromView(int vx, int vy, int r)
        {
            int m = N - 1;
            switch (r) { case 1: return new Vector2Int(vy, m - vx); case 2: return new Vector2Int(m - vx, m - vy); case 3: return new Vector2Int(m - vy, vx); default: return new Vector2Int(vx, vy); }
        }

        /// <summary>View vertex -> world vertex (JS vertW).</summary>
        public static Vector2Int VertW(int a, int b, int r)
        {
            switch (r) { case 1: return new Vector2Int(b, N - a); case 2: return new Vector2Int(N - a, N - b); case 3: return new Vector2Int(N - b, a); default: return new Vector2Int(a, b); }
        }

        /// <summary>Height level of a view vertex.</summary>
        public static int VhView(World w, int a, int b, int r) { var v = VertW(a, b, r); return w.Vh(v.x, v.y); }

        /// <summary>Unity position of view vertex (a, b) lifted to height level z.</summary>
        public static Vector3 VertexPos(int a, int b, float z)
        {
            float sx = (a - b) * 16f, sy = (a + b) * 8f - z * LvlPx;
            return new Vector3(sx / PPU, -sy / PPU, 0);
        }

        /// <summary>Unity position -> game screen pixels (y down).</summary>
        public static Vector2 ToScreenPx(Vector3 p) => new Vector2(p.x * PPU, -p.y * PPU);

        /// <summary>Which world tile is under a Unity position, testing height levels from the top down (JS screenToTile).</summary>
        public static Vector2Int? PickTile(World w, Vector3 worldPos, int r)
        {
            var s = ToScreenPx(worldPos);
            float p = s.x / 16f;
            for (float L = HMax; L >= HMin; L -= 0.25f)
            {
                float q = (s.y + L * LvlPx) / 8f, fx = (p + q) / 2f, fy = (q - p) / 2f;
                int vx = Mathf.FloorToInt(fx), vy = Mathf.FloorToInt(fy);
                if (vx < 0 || vy < 0 || vx >= N || vy >= N) continue;
                float c0 = VhView(w, vx, vy, r), c1 = VhView(w, vx + 1, vy, r), c2 = VhView(w, vx, vy + 1, r), c3 = VhView(w, vx + 1, vy + 1, r);
                float uu = fx - vx, vv = fy - vy;
                float z = c0 + (c1 - c0) * uu + (c2 - c0) * vv + (c0 - c1 - c2 + c3) * uu * vv;
                if (Mathf.Abs(z - L) <= 0.2f) return FromView(vx, vy, r);
            }
            return null;
        }

        /// <summary>Centre of a world tile's surface, in Unity units (for camera targeting).</summary>
        public static Vector3 TileCenter(World w, int x, int y, int r)
        {
            var v = ToView(x, y, r);
            float lvl = (float)w.TileLevel(x, y);
            float sx = (v.x - v.y) * 16f, sy = (v.x + v.y) * 8f + 8f - lvl * LvlPx;
            return new Vector3(sx / PPU, -sy / PPU, 0);
        }

        /// <summary>Painter's order for something whose front tile is view (vx, vy): by diagonal first, then across it. A sortingOrder is a short
        /// (max 32767) and a 256-wide map has 511 diagonals, so each diagonal gets 64 steps and tiles share one when they are 4 apart across it
        /// (they sit side by side on screen, so their order hardly matters). The largest value is 510 * 64 + 63 = 32703.</summary>
        public static int SortOrder(int vx, int vy) => (vx + vy) * 64 + (vx >> 2);

        /// <summary>The block of view tiles that can show anywhere inside a world-space rectangle (Unity units). The margins, in pixels, allow for
        /// wide sprites (sideways), for ground that sits lower than its flat position (above) and for tall sprites and high ground (below).</summary>
        public static RectInt TileWindow(Rect world, float marginX = 100f, float marginTop = 64f, float marginBottom = 260f)
        {
            float sx0 = world.xMin * PPU - marginX, sx1 = world.xMax * PPU + marginX;
            float sy0 = -world.yMax * PPU - marginTop, sy1 = -world.yMin * PPU + marginBottom;   // screen y grows downwards
            float amin = float.MaxValue, amax = float.MinValue, bmin = float.MaxValue, bmax = float.MinValue;
            foreach (var (sx, sy) in new[] { (sx0, sy0), (sx1, sy0), (sx0, sy1), (sx1, sy1) })
            {
                // a tile (a, b) on flat ground sits at sx = (a - b) * 16, sy = (a + b) * 8
                float a = (sx / 16f + sy / 8f) / 2f, b = (sy / 8f - sx / 16f) / 2f;
                amin = Mathf.Min(amin, a); amax = Mathf.Max(amax, a); bmin = Mathf.Min(bmin, b); bmax = Mathf.Max(bmax, b);
            }
            int x0 = Mathf.Clamp(Mathf.FloorToInt(amin) - 1, 0, N - 1), x1 = Mathf.Clamp(Mathf.CeilToInt(amax) + 1, 0, N - 1);
            int y0 = Mathf.Clamp(Mathf.FloorToInt(bmin) - 1, 0, N - 1), y1 = Mathf.Clamp(Mathf.CeilToInt(bmax) + 1, 0, N - 1);
            return new RectInt(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
        }
    }
}
