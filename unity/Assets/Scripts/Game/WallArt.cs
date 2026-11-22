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
        const int Size = 32, CenterX = 16, CenterY = 8, StakeH = 13;
        static readonly Color32 Light = new Color32(196, 148, 90, 255), Mid = new Color32(152, 106, 60, 255), Dark = new Color32(102, 69, 39, 255), Rail = new Color32(128, 88, 50, 255);

        readonly Sprite[] _sprites = new Sprite[16];
        readonly Texture2D[] _textures = new Texture2D[16];

        public Sprite Get(int mask) => _sprites[mask] ??= Make(mask);

        public void Dispose()
        {
            for (int i = 0; i < 16; i++) { if (_sprites[i] != null) Object.Destroy(_sprites[i]); if (_textures[i] != null) Object.Destroy(_textures[i]); }
        }

        static (int dx, int dy) Half(int bit) => bit switch { E => (8, -4), W => (-8, 4), S => (-8, -4), _ => (8, 4) };

        Sprite Make(int mask)
        {
            var px = new Color32[Size * Size];
            void Set(int x, int y, Color32 c) { if (x >= 0 && y >= 0 && x < Size && y < Size) px[y * Size + x] = c; }
            var stakes = new System.Collections.Generic.List<(int x, int y)> { (CenterX, CenterY) };
            if (mask == 0) { stakes.Add((CenterX - 3, CenterY - 1)); stakes.Add((CenterX + 3, CenterY + 1)); }
            foreach (int bit in new[] { E, W, S, N })
            {
                if ((mask & bit) == 0) continue;
                var (dx, dy) = Half(bit);
                for (int step = 1; step <= 3; step++) stakes.Add((CenterX + Mathf.RoundToInt(dx * step / 3f), CenterY + Mathf.RoundToInt(dy * step / 3f)));
                // two rails along the segment
                for (int i = 0; i <= 16; i++)
                {
                    int x = CenterX + Mathf.RoundToInt(dx * i / 16f), y = CenterY + Mathf.RoundToInt(dy * i / 16f);
                    Set(x, y + 4, Rail); Set(x, y + 9, Rail);
                }
            }
            // far stakes first so nearer ones overlap them
            stakes.Sort((a, b) => b.y != a.y ? b.y.CompareTo(a.y) : a.x.CompareTo(b.x));
            foreach (var (x, y) in stakes)
            {
                for (int h = 0; h < StakeH; h++)
                {
                    bool tip = h == StakeH - 1, nearTip = h == StakeH - 2;
                    if (tip) { Set(x, y + h, Mid); continue; }
                    if (nearTip) { Set(x, y + h, Light); Set(x + 1, y + h, Mid); Set(x - 1, y + h, Mid); continue; }
                    Set(x - 1, y + h, h < 2 ? Dark : Light);
                    Set(x, y + h, h < 2 ? Dark : Mid);
                    Set(x + 1, y + h, Dark);
                }
            }
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "palisade_" + mask };
            tex.SetPixels32(px); tex.Apply();
            _textures[mask] = tex;
            return Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0), IsoView.PPU, 0, SpriteMeshType.FullRect);
        }
    }
}
