// Art for a raider camp, drawn in code in the game's own isometric style (32 x 48, standing on a 32 x 16 tile like a 1 x 1 building): a red
// tent, camp props and supplies.
using System.Collections.Generic;
using CrownAndHearth.Sim;
using UnityEngine;

namespace CrownAndHearth.Game
{
    public sealed partial class CampArt
    {
        const int W = 32, H = 48, CX = 16, CY = 8;   // the tile centre, 8 pixels above the foot of the sprite
        readonly Dictionary<int, Sprite> _sprites = new Dictionary<int, Sprite>();
        readonly List<Texture2D> _textures = new List<Texture2D>();

        public Sprite Tent(int rotation) => Get(-1 - rotation, px => DrawTent(px, rotation));
        public Sprite ChiefTent(int rotation) => Get(-5 - rotation, px => DrawChiefTent(px, rotation));
        public Sprite Fire() => Get(-9, DrawFire);
        public Sprite Rack(int rotation) => Get(-10 - (rotation & 1), px => DrawRack(px, (rotation & 1) != 0));
        public Sprite Supplies(Res r) => Get((int)r, px =>
        {
            if (r == Res.Gold) DrawGold(px);
            else DrawSupplies(px, r);
        });

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
        static void Line(Color32[] px, (int x, int y) a, (int x, int y) b, Color32 color)
        {
            int steps = Mathf.Max(System.Math.Abs(b.x - a.x), System.Math.Abs(b.y - a.y));
            for (int i = 0; i <= steps; i++)
            {
                float t = steps == 0 ? 0f : (float)i / steps;
                Set(px, Mathf.RoundToInt(a.x + (b.x - a.x) * t), Mathf.RoundToInt(a.y + (b.y - a.y) * t), color);
            }
        }

        static void DrawTent(Color32[] px, int rotation)
        {
            bool mirrored = (rotation & 1) != 0;
            // The doorway and its pole stay at the same end of the tent as the camera turns.
            bool entranceVisible = rotation == 0 || rotation == 3;
            var litCanvas = new Color32(164, 86, 64, 255);
            var shadeCanvas = new Color32(123, 62, 49, 255);
            var litFold = new Color32(192, 111, 79, 255);
            var shadeFold = new Color32(146, 75, 56, 255);
            var hem = new Color32(71, 38, 33, 255);
            var wood = new Color32(88, 60, 42, 255);
            var endGrain = new Color32(157, 116, 73, 255);
            var stitching = new Color32(190, 146, 93, 255);
            var rope = new Color32(207, 164, 105, 255);

            (int x, int y) P(int x, int y) => (mirrored ? CX - x : CX + x, y);

            // The three visible corners are inset from the 32 x 16 tile edges.
            var left = P(-12, 8);
            var front = P(0, 2);
            var right = P(12, 8);
            var nearRidge = P(-6, 21);
            var farRidge = P(6, 27);
            Triangle(px, left, front, nearRidge, mirrored ? shadeCanvas : litCanvas);
            Quad(px, nearRidge, farRidge, right, front, mirrored ? litCanvas : shadeCanvas);

            // Broad folds under the ridge and a stitched patch on the roof.
            var roofFold = mirrored ? litFold : shadeFold;
            var roofShade = mirrored ? shadeCanvas : hem;
            Quad(px, P(-5, 20), P(5, 25), P(8, 20), P(-3, 15), roofFold);
            Quad(px, P(-1, 8), P(10, 13), right, front, roofShade);
            Line(px, P(-3, 13), P(9, 19), mirrored ? shadeFold : litCanvas);
            Line(px, P(-1, 10), P(10, 15), roofShade);
            Line(px, P(1, 4), P(11, 9), mirrored ? litFold : shadeFold);
            for (int x = 2; x <= 10; x += 3)
            {
                var tie = P(x, 3 + x / 2);
                Set(px, tie.x, tie.y, stitching);
            }

            int patch = entranceVisible ? 0 : -5;
            Quad(px, P(3 + patch, 15), P(6 + patch, 16), P(7 + patch, 13), P(4 + patch, 12),
                mirrored ? new Color32(167, 120, 78, 255) : new Color32(143, 100, 69, 255));
            Line(px, P(4 + patch, 12), P(7 + patch, 13), roofShade);
            Line(px, P(3 + patch, 15), P(6 + patch, 16), stitching);

            if (entranceVisible)
            {
                var opening = new Color32(42, 28, 24, 255);
                Triangle(px, P(-9, 7), P(-3, 4), P(-6, 17), opening);
                Triangle(px, P(-10, 7), P(-8, 7), P(-6, 18), mirrored ? shadeFold : litFold);
                Line(px, P(-6, 19), P(-3, 5), mirrored ? endGrain : stitching);
                Line(px, P(-9, 6), P(-4, 4), wood);
                Line(px, P(-8, 6), P(-5, 5), endGrain);
            }
            else
            {
                // The rear gable is closed, with a seam running down the canvas.
                Triangle(px, P(-7, 6), P(-5, 5), P(-6, 20), mirrored ? shadeFold : litFold);
            }

            Quad(px, P(-7, 21), P(6, 28), P(7, 27), P(-6, 20), wood);
            Quad(px, P(-7, 22), P(6, 29), P(6, 28), P(-7, 21), endGrain);
            Line(px, P(-7, 20), P(-5, 23), rope);
            Line(px, P(5, 27), P(6, 29), stitching);

            // Guy lines terminate at the two side pegs, inside the tile.
            Line(px, P(-7, 20), P(-12, 9), stitching);
            Line(px, P(6, 26), P(12, 9), new Color32(141, 102, 64, 255));
            foreach (var peg in new[] { left, front, right })
            {
                Rect(px, peg.x, peg.y - 1, 2, 4, wood);
                Set(px, peg.x, peg.y + 2, endGrain);
            }

            var pole = entranceVisible ? nearRidge : farRidge;
            Rect(px, pole.x, pole.y, 1, 8, wood);
            Set(px, pole.x, pole.y + 7, endGrain);
            Quad(px, (pole.x + 1, pole.y + 6), (pole.x + 7, pole.y + 5), (pole.x + 5, pole.y + 3), (pole.x + 1, pole.y + 3), shadeCanvas);
            Line(px, (pole.x + 1, pole.y + 6), (pole.x + 6, pole.y + 5), litFold);
            Set(px, pole.x, pole.y + 5, rope);
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
                default:
                    Rect(px, gx - 5, gy, 5, 2, c); Rect(px, gx + 1, gy, 5, 2, dark); Rect(px, gx - 2, gy + 2, 5, 2, c);
                    break;
            }
            Rect(px, CX + 4, CY + 5, 5, 3, c);   // a little of it on the small crate too
        }
    }
}
