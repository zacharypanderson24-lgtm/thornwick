// Wooden palisades, drawn in code: a row of pointed stakes with two rails that runs through the middle of a tile towards each neighbouring
// palisade tile, so a wall covers only a thin line of the tile and joins up with the walls beside it. 16 sprites, one per combination of the
// four neighbours (in view space, so the wall still joins correctly when the camera is rotated).
using UnityEngine;

namespace CrownAndHearth.Game
{
    public sealed class WallArt
    {
        // neighbour bits in view space: 1 = +x (down right on screen), 2 = -x (up left), 4 = +y (down left), 8 = -y (up right)
        public const int E = 1, W = 2, S = 4, N = 8;
        const int Size = 32, CenterX = 16, CenterY = 8, StakeH = 16;
        static readonly int[] Directions = { E, W, S, N };
        static readonly Color32 Shadow = new Color32(48, 33, 24, 255);
        static readonly Color32 Bark = new Color32(77, 51, 34, 255);
        static readonly Color32 Grain = new Color32(128, 87, 52, 255);
        static readonly Color32 Timber = new Color32(155, 110, 66, 255);
        static readonly Color32 Sapwood = new Color32(205, 160, 98, 255);
        static readonly Color32 RailFace = new Color32(108, 72, 43, 255);
        static readonly Color32 RailEdge = new Color32(172, 125, 72, 255);

        readonly Sprite[] _sprites = new Sprite[16];
        readonly Texture2D[] _textures = new Texture2D[16];

        public Sprite Get(int mask, int level = 1, int insideX = 0, int insideY = 0)
        {
            string name = (level >= 2 ? "palisade2_" : "palisade_") + mask;
            if (level >= 2 && insideX != 0 && insideY != 0)
            {
                var facing = Resources.Load<Sprite>("Art/" + name + "_" + insideX + "_" + insideY);
                if (facing != null) return facing;
            }
            var sprite = Resources.Load<Sprite>("Art/" + name);
            return sprite != null ? sprite : (_sprites[mask] ??= Make(mask));
        }

        public void Dispose()
        {
            for (int i = 0; i < 16; i++) { if (_sprites[i] != null) Object.Destroy(_sprites[i]); if (_textures[i] != null) Object.Destroy(_textures[i]); }
        }

        static (int dx, int dy) Half(int bit) => bit switch { E => (8, -4), W => (-8, 4), S => (-8, -4), _ => (8, 4) };

        Sprite Make(int mask)
        {
            var px = new Color32[Size * Size];
            void Set(int x, int y, Color32 c) { if (x >= 0 && y >= 0 && x < Size && y < Size) px[y * Size + x] = c; }
            void Rail(int x, int y)
            {
                Set(x, y, Shadow);
                Set(x, y + 1, RailFace);
                Set(x, y + 2, RailEdge);
            }

            var stakes = new System.Collections.Generic.List<(int x, int y)> { (CenterX, CenterY) };
            if (mask == 0) { stakes.Add((CenterX - 3, CenterY - 1)); stakes.Add((CenterX + 3, CenterY + 1)); }
            foreach (int bit in Directions)
            {
                if ((mask & bit) == 0) continue;
                var (dx, dy) = Half(bit);
                for (int step = 1; step <= 3; step++) stakes.Add((CenterX + Mathf.RoundToInt(dx * step / 3f), CenterY + Mathf.RoundToInt(dy * step / 3f)));
            }
            // far stakes first so nearer ones overlap them
            stakes.Sort((a, b) => b.y != a.y ? b.y.CompareTo(a.y) : a.x.CompareTo(b.x));
            foreach (var (x, y) in stakes)
            {
                bool post = x == CenterX && y == CenterY;
                int pattern = (x + 2 * y) % 5;
                int height = post ? StakeH + 1 : StakeH - pattern % 3;
                for (int h = 0; h < height - 3; h++)
                {
                    Set(x - 1, y + h, h < 2 ? Bark : Timber);
                    Set(x, y + h, h < 2 || h == 6 + pattern ? Bark : Grain);
                    Set(x + 1, y + h, Bark);
                    if (post) Set(x + 2, y + h, Shadow);
                }
                Set(x - 1, y + height - 3, Timber);
                Set(x, y + height - 3, Sapwood);
                Set(x + 1, y + height - 3, Bark);
                Set(x, y + height - 2, Sapwood);
                Set(x, y + height - 1, Timber);
            }

            // Face-mounted rails stay visible across the grain. A peg at each end ties the run together.
            foreach (int bit in Directions)
            {
                if ((mask & bit) == 0) continue;
                var (dx, dy) = Half(bit);
                for (int i = 0; i <= Mathf.Abs(dx); i++)
                {
                    int x = CenterX + System.Math.Sign(dx) * i;
                    int y = CenterY + Mathf.RoundToInt(dy * i / (float)Mathf.Abs(dx));
                    Rail(x, y + 3);
                    Rail(x, y + 8);
                    if (i != 0 && i != 7) continue;
                    Set(x, y + 4, Shadow);
                    Set(x, y + 9, Shadow);
                }
            }
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "palisade_" + mask };
            tex.SetPixels32(px); tex.Apply();
            _textures[mask] = tex;
            return Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0), IsoView.PPU, 0, SpriteMeshType.FullRect);
        }
    }
}
