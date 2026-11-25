// Fishing boats, drawn in code (placeholder until a boat is drawn for the game): a small open boat with a mast and sail, four facings
// matching AgentRenderer.Facing (bit 0: heading left on screen, bit 1: heading up), two frames so it bobs.
using System.Collections.Generic;
using UnityEngine;

namespace CrownAndHearth.Game
{
    public static class BoatArt
    {
        const int W = 21, H = 16;
        static readonly Dictionary<int, Sprite> Cache = new Dictionary<int, Sprite>();
        static readonly Color32 Hull = new Color32(112, 78, 44, 255), HullLight = new Color32(176, 130, 74, 255), Dark = new Color32(67, 44, 31, 255), Sail = new Color32(232, 222, 196, 255), SailShade = new Color32(190, 176, 146, 255);

        public static Sprite Get(int facing, int frame)
        {
            int key = facing * 2 + (frame & 1);
            if (Cache.TryGetValue(key, out var s) && s != null) return s;
            var px = new Color32[W * H]; int bob = frame & 1;
            int sx = (facing & 1) != 0 ? -1 : 1, sy = (facing & 2) != 0 ? -1 : 1;   // screen heading of the bow
            int cx = W / 2, cy = 4 + bob;
            void Set(int x, int y, Color32 c) { if (x >= 0 && y >= 0 && x < W && y < H) px[y * W + x] = c; }
            // hull: a long diamond along the heading (2 pixels across for every 1 down, as the tiles are), flipped to screen y-up for the texture
            for (int i = -5; i <= 5; i++)
            {
                int hx = cx + i * 2 * sx, hy = cy - i * sy;   // texture y grows upward, a bow heading "down" on screen has sy = +1
                int width = 3 - Mathf.Abs(i) / 3;
                for (int w = -width + 1; w < width; w++) { Set(hx + w, hy, Hull); Set(hx + w, hy + 1, HullLight); }
                Set(hx, hy - 1, Dark);
            }
            // mast and sail
            for (int y = cy + 1; y < cy + 9; y++) Set(cx, y, Dark);
            for (int y = cy + 3; y < cy + 9; y++) { int across = (9 - y + cy) / 2 + 1; for (int x = 1; x <= across; x++) Set(cx + x * -sx, y, x % 2 == 0 ? SailShade : Sail); }
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "boat" + key };
            tex.SetPixels32(px); tex.Apply(false, true);
            s = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.1f), IsoView.PPU, 0, SpriteMeshType.FullRect);
            s.name = tex.name; Cache[key] = s;
            return s;
        }
    }
}
