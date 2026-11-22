// Art for a raider camp, drawn in code in the game's own isometric style (32 x 48, standing on a 32 x 16 tile like a 1 x 1 building): a red
// raider tent, and a pile of supplies (crates with the goods on top) in a colour for each resource.
using System.Collections.Generic;
using CrownAndHearth.Sim;
using UnityEngine;

namespace CrownAndHearth.Game
{
    public sealed class CampArt
    {
        const int W = 32, H = 48, CX = 16, CY = 8;   // the tile centre, 8 pixels above the foot of the sprite
        readonly Dictionary<int, Sprite> _sprites = new Dictionary<int, Sprite>();
        readonly List<Texture2D> _textures = new List<Texture2D>();

        public Sprite Tent() => Get(-1, DrawTent);
        public Sprite Supplies(Res r) => Get((int)r, px => DrawSupplies(px, r));

        public void Dispose() { foreach (var s in _sprites.Values) Object.Destroy(s); foreach (var t in _textures) Object.Destroy(t); _sprites.Clear(); _textures.Clear(); }

        Sprite Get(int key, System.Action<Color32[]> draw)
        {
            if (_sprites.TryGetValue(key, out var s)) return s;
            var px = new Color32[W * H];
            draw(px);
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "camp_" + key };
            tex.SetPixels32(px); tex.Apply();
            _textures.Add(tex);
            return _sprites[key] = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0), IsoView.PPU, 0, SpriteMeshType.FullRect);
        }

        static void Set(Color32[] px, int x, int y, Color32 c) { if (x >= 0 && y >= 0 && x < W && y < H) px[y * W + x] = c; }

        static void Rect(Color32[] px, int x0, int y0, int w, int h, Color32 c) { for (int y = y0; y < y0 + h; y++) for (int x = x0; x < x0 + w; x++) Set(px, x, y, c); }

        static void Triangle(Color32[] px, (int x, int y) a, (int x, int y) b, (int x, int y) c, Color32 fill)
        {
            int minX = Mathf.Min(a.x, Mathf.Min(b.x, c.x)), maxX = Mathf.Max(a.x, Mathf.Max(b.x, c.x)), minY = Mathf.Min(a.y, Mathf.Min(b.y, c.y)), maxY = Mathf.Max(a.y, Mathf.Max(b.y, c.y));
            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    float d1 = Sign(x, y, a, b), d2 = Sign(x, y, b, c), d3 = Sign(x, y, c, a);
                    bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
                    if (!(neg && pos)) Set(px, x, y, fill);
                }
        }

        static float Sign(int x, int y, (int x, int y) p1, (int x, int y) p2) => (x - p2.x) * (p1.y - p2.y) - (p1.x - p2.x) * (y - p2.y);

        static void Quad(Color32[] px, (int x, int y) a, (int x, int y) b, (int x, int y) c, (int x, int y) d, Color32 fill) { Triangle(px, a, b, c, fill); Triangle(px, a, c, d, fill); }

        // ---------- the tent ----------
        static void DrawTent(Color32[] px)
        {
            var apex = (CX, CY + 20);
            var left = (CX - 11, CY + 1); var right = (CX + 11, CY + 1); var front = (CX, CY - 5); var back = (CX, CY + 7);
            Triangle(px, left, back, apex, new Color32(120, 34, 32, 255));                 // the far side, in shadow
            Triangle(px, back, right, apex, new Color32(108, 30, 28, 255));
            Triangle(px, left, front, apex, new Color32(196, 62, 52, 255));                // the two faces towards the camera
            Triangle(px, front, right, apex, new Color32(150, 42, 38, 255));
            // seams and the doorway on the right face
            for (int i = 0; i <= 20; i++) { Set(px, CX, CY - 5 + i, new Color32(96, 26, 24, 255)); }
            Triangle(px, (CX + 3, CY - 3), (CX + 8, CY - 0), (CX + 5, CY + 8), new Color32(36, 20, 20, 255));
            for (int i = 0; i < 12; i++) { Set(px, left.Item1 + i, left.Item2 - i / 2, new Color32(84, 22, 22, 255)); Set(px, right.Item1 - i, right.Item2 - i / 2, new Color32(84, 22, 22, 255)); }
            Rect(px, CX, CY + 20, 1, 5, new Color32(70, 50, 36, 255));                      // the pole
            Rect(px, CX + 1, CY + 23, 4, 2, new Color32(40, 24, 24, 255));                  // a rag of a pennant
        }

        // ---------- supplies ----------
        static void Cube(Color32[] px, int cx, int cy, int half, int height, Color32 top, Color32 leftFace, Color32 rightFace)
        {
            var t = (cx, cy + half / 2 + height); var r = (cx + half, cy + height); var b = (cx, cy - half / 2 + height); var l = (cx - half, cy + height);
            Quad(px, l, b, (b.Item1, b.Item2 - height), (l.Item1, l.Item2 - height), leftFace);
            Quad(px, b, r, (r.Item1, r.Item2 - height), (b.Item1, b.Item2 - height), rightFace);
            Quad(px, t, r, b, l, top);
        }

        static Color32 Goods(Res r) => r switch
        {
            Res.Food => new Color32(228, 198, 88, 255), Res.Wood => new Color32(204, 160, 98, 255), Res.Logs => new Color32(126, 88, 52, 255),
            Res.Stone => new Color32(170, 170, 176, 255), Res.Ore => new Color32(120, 78, 62, 255), Res.Gold => new Color32(255, 218, 70, 255),
            _ => new Color32(120, 150, 232, 255),
        };

        static void DrawSupplies(Color32[] px, Res res)
        {
            var wood = (top: new Color32(190, 144, 88, 255), left: new Color32(142, 102, 60, 255), right: new Color32(104, 72, 42, 255));
            Cube(px, CX - 5, CY - 2, 7, 6, wood.top, wood.left, wood.right);   // a big crate
            Cube(px, CX + 6, CY + 0, 5, 5, wood.top, wood.left, wood.right);   // a smaller one
            var c = Goods(res);
            var dark = new Color32((byte)(c.r * 0.7f), (byte)(c.g * 0.7f), (byte)(c.b * 0.7f), 255);
            int gx = CX - 5, gy = CY - 2 + 6 - 1;   // sitting on the top of the big crate
            switch (res)
            {
                case Res.Food: case Res.Flax:   // sacks
                    Rect(px, gx - 4, gy, 4, 4, c); Rect(px, gx + 1, gy, 4, 4, c); Rect(px, gx - 2, gy + 3, 4, 3, dark);
                    break;
                case Res.Wood:   // planks
                    for (int i = 0; i < 3; i++) Rect(px, gx - 5, gy + i * 2, 10, 1, i % 2 == 0 ? c : dark);
                    break;
                case Res.Logs:   // log ends
                    for (int i = 0; i < 3; i++) Rect(px, gx - 5 + i * 4, gy, 3, 3, i % 2 == 0 ? c : dark);
                    Rect(px, gx - 3, gy + 3, 3, 3, c); Rect(px, gx + 1, gy + 3, 3, 3, dark);
                    break;
                case Res.Stone:   // blocks
                    Rect(px, gx - 5, gy, 5, 4, c); Rect(px, gx + 1, gy + 1, 5, 4, dark); Rect(px, gx - 2, gy + 4, 4, 3, c);
                    break;
                case Res.Ore:   // dark rock with orange flecks
                    Rect(px, gx - 5, gy, 10, 5, c); Set(px, gx - 2, gy + 2, new Color32(240, 130, 50, 255)); Set(px, gx + 2, gy + 3, new Color32(240, 130, 50, 255)); Set(px, gx, gy + 1, new Color32(240, 130, 50, 255));
                    break;
                default:   // gold: little bars
                    Rect(px, gx - 5, gy, 5, 2, c); Rect(px, gx + 1, gy, 5, 2, dark); Rect(px, gx - 2, gy + 2, 5, 2, c);
                    break;
            }
            Rect(px, CX + 4, CY + 5, 5, 3, c);   // a little of it on the small crate too
        }
    }
}
