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

        /// <summary>Find the foremost rendered ground triangle under the pointer.</summary>
        public static Vector2Int? PickTile(World w, Vector3 worldPos, int r)
        {
            var screen = ToScreenPx(worldPos);
            float difference = screen.x / 16f;
            float nearestSum = (screen.y + HMax * LvlPx) / 8f;
            float farthestSum = (screen.y + HMin * LvlPx) / 8f;
            int firstDiagonal = Mathf.Max(0, Mathf.CeilToInt(farthestSum - 2f));
            int lastDiagonal = Mathf.Min(DiagonalCount - 1, Mathf.FloorToInt(nearestSum));

            // A diamond spans one unit on either side of its horizontal centre.
            // Check candidates in reverse mesh draw order so overlapping slopes pick the visible tile.
            for (int diagonal = lastDiagonal; diagonal >= firstDiagonal; diagonal--)
            {
                int firstX = Mathf.Max(Mathf.Max(0, diagonal - (N - 1)), Mathf.CeilToInt((difference + diagonal - 1f) / 2f));
                int lastX = Mathf.Min(Mathf.Min(N - 1, diagonal), Mathf.FloorToInt((difference + diagonal + 1f) / 2f));
                for (int vx = lastX; vx >= firstX; vx--)
                {
                    int vy = diagonal - vx;
                    var top = ProjectVertex(w, vx, vy, r);
                    var right = ProjectVertex(w, vx + 1, vy, r);
                    var bottom = ProjectVertex(w, vx + 1, vy + 1, r);
                    var left = ProjectVertex(w, vx, vy + 1, r);
                    if (InTriangle(screen, top, right, bottom) || InTriangle(screen, top, bottom, left))
                        return FromView(vx, vy, r);
                }
            }
            return null;
        }

        static Vector2 ProjectVertex(World w, int x, int y, int r) =>
            ToScreenPx(VertexPos(x, y, VhView(w, x, y, r)));

        static float Cross(Vector2 a, Vector2 b, Vector2 p) =>
            (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);

        static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float area = Cross(a, b, c);
            if (area == 0) return false;
            float ab = Cross(a, b, p), bc = Cross(b, c, p), ca = Cross(c, a, p);
            return area > 0 ? ab >= 0 && bc >= 0 && ca >= 0 : ab <= 0 && bc <= 0 && ca <= 0;
        }

        /// <summary>Centre of a world tile's surface, in Unity units (for camera targeting).</summary>
        public static Vector3 TileCenter(World w, int x, int y, int r)
        {
            var v = ToView(x, y, r);
            float lvl = (float)w.TileLevel(x, y);
            float sx = (v.x - v.y) * 16f, sy = (v.x + v.y) * 8f + 8f - lvl * LvlPx;
            return new Vector3(sx / PPU, -sy / PPU, 0);
        }

        public const int DiagonalCount = 2 * N - 1;

        // Each diagonal draws its terrain, its highlights, then sprites from left to right.
        // Start at the signed 16-bit minimum to leave room for both mesh layers and sprites.
        public static int GroundOrder(int diagonal) => short.MinValue + diagonal * (N + 2);
        public static int OverlayOrder(int diagonal) => GroundOrder(diagonal) + 1;
        public static int SortOrder(int vx, int vy) => GroundOrder(vx + vy) + 2 + vx;
    }
}
