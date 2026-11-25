using System;
using System.Collections.Generic;
using CrownAndHearth.Sim;
using UnityEngine;

namespace CrownAndHearth.Game
{
    sealed class SettlementWorkArt : IDisposable
    {
        static readonly Color32 DarkWood = Color(67, 44, 31), Timber = Color(112, 75, 45), Oak = Color(159, 114, 65);
        static readonly Color32 Plaster = Color(198, 179, 146), PlasterShade = Color(153, 131, 100);
        static readonly Color32 Straw = Color(179, 134, 66), StrawLight = Color(212, 165, 85), StrawShade = Color(126, 90, 48);
        static readonly Color32 Stone = Color(110, 105, 94), StoneLight = Color(153, 142, 120);
        readonly Dictionary<(string kind, int view), Sprite> _cache = new();

        public Sprite Building(string kind, int view)
        {
            if (kind == "watermill") return Get("watermill", view, 33, 56, c => { DrawShop(c, false, view); DrawWheel(c); });
            if (kind == "dock") return Get("dock", view, 33, 30, DrawDock);
            if (kind == "fisher") return Get("fisher", view, 33, 56, c => { DrawShop(c, false, view); DrawFishRack(c); });
            if (kind != "storehouse" && kind != "smokehouse" && kind != "charcoal") return null;
            return Get(kind, view, kind == "storehouse" ? 65 : 33, 56, canvas =>
            {
                if (kind == "charcoal") DrawClamp(canvas);
                else DrawShop(canvas, kind == "storehouse", view);
            });
        }

        public Sprite Fence(int mask) => Get("fence", mask, 33, 28, c => DrawFence(c, mask));
        public Sprite Animal(Zone kind, bool step = false) =>
            Get("animal", (int)kind * 2 + (step ? 1 : 0), 17, 10, c => DrawAnimal(c, kind, step));

        Sprite Get(string kind, int view, int width, int height, Action<Pixels> draw)
        {
            if (_cache.TryGetValue((kind, view), out var sprite)) return sprite;
            var canvas = new Pixels(width, height);
            draw(canvas);
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = kind + "_" + view,
            };
            texture.SetPixels32(canvas.Buffer);
            texture.Apply(false, true);
            sprite = Sprite.Create(texture, new Rect(0, 0, width, height),
                new Vector2((width / 2) / (float)width, 0), IsoView.PPU, 0, SpriteMeshType.FullRect);
            sprite.name = texture.name;
            return _cache[(kind, view)] = sprite;
        }

        public void Dispose()
        {
            foreach (var sprite in _cache.Values)
            {
                UnityEngine.Object.Destroy(sprite.texture);
                UnityEngine.Object.Destroy(sprite);
            }
            _cache.Clear();
        }

        static void DrawFence(Pixels c, int mask)
        {
            // Back, right, front and left are corners of the 32×16 view diamond.
            var corners = new[] { (16, 16), (32, 8), (16, 0), (0, 8) };
            for (int edge = 0; edge < 4; edge++)
            {
                if ((mask & (1 << edge)) == 0) continue;
                var a = corners[edge];
                var b = corners[(edge + 1) % 4];
                for (int lift = 3; lift <= 7; lift += 4)
                {
                    c.Line((a.Item1, a.Item2 + lift), (b.Item1, b.Item2 + lift), DarkWood);
                    c.Line((a.Item1, a.Item2 + lift + 1), (b.Item1, b.Item2 + lift + 1), Oak);
                }
                for (int i = 0; i <= 2; i++)
                {
                    var post = ((a.Item1 * (2 - i) + b.Item1 * i) / 2, (a.Item2 * (2 - i) + b.Item2 * i) / 2);
                    c.Rect(post.Item1, post.Item2, 2, 10, Timber);
                    c.Set(post.Item1, post.Item2 + 9, Oak);
                }
            }
        }

        static void DrawAnimal(Pixels c, Zone kind, bool step)
        {
            int hoof = step ? 1 : 0;
            switch (kind)
            {
                case Zone.Pigs:
                    c.Set(5 + hoof, 0, DarkWood); c.Set(10 - hoof, 0, DarkWood);
                    c.Rect(4, 1, 7, 3, Color(159, 100, 89));
                    c.Rect(5, 4, 5, 1, Color(196, 137, 119));
                    c.Rect(10, 1, 3, 2, Color(199, 139, 123));
                    c.Set(11, 3, DarkWood); c.Set(10, 4, Color(169, 115, 100));
                    c.Set(3, 3, Color(191, 134, 117));
                    break;
                case Zone.Sheep:
                    c.Set(5 + hoof, 0, DarkWood); c.Set(10 - hoof, 0, DarkWood);
                    c.Rect(4, 1, 7, 3, Color(174, 160, 132));
                    c.Rect(5, 3, 5, 2, Color(226, 214, 181));
                    c.Rect(10, 2, 2, 2, DarkWood);
                    c.Set(11, 3, StoneLight);
                    break;
                case Zone.Chickens:
                    c.Set(7 + hoof, 0, Oak);
                    c.Rect(6, 1, 4, 2, Color(197, 169, 119));
                    c.Set(5, 2, Plaster); c.Set(9, 3, Plaster);
                    c.Set(9, 4, Color(166, 72, 46)); c.Set(10, 3, StrawLight);
                    break;
            }
        }

        // a fishing hut is the small shop with a drying rack of fish in front (placeholder art until ChatGPT draws one)
        static void DrawFishRack(Pixels c)
        {
            var fish = Color(176, 196, 210); var fishDark = Color(112, 132, 150);
            c.Line((2, 1), (2, 12), DarkWood); c.Line((11, 1), (11, 12), DarkWood); c.Line((1, 12), (12, 12), Oak);
            for (int x = 3; x <= 9; x += 3) { c.Line((x, 11), (x, 7), fish); c.Set(x, 6, fishDark); c.Set(x + 1, 9, fish); }
            c.Line((3, 3), (10, 5), Timber);
        }

        // a fishing dock: a plank pier running to the water on posts, with a barrel and a coil of net (placeholder art)
        static void DrawDock(Pixels c)
        {
            var plank = Color(176, 130, 74); var plankDark = Color(120, 84, 46);
            c.Quad((2, 10), (16, 2), (30, 10), (16, 18), plankDark);
            c.Quad((3, 10), (16, 3), (29, 10), (16, 17), plank);
            for (int i = -3; i <= 3; i++) c.Line((16 + i * 4 - 6, 9 + i * 2 - 3 + 6), (16 + i * 4 + 6, 9 + i * 2 - 3 - 6 + 6), plankDark);
            foreach (var p in new[] { (3, 10), (16, 3), (29, 10), (16, 17) }) c.Line(p, (p.Item1, p.Item2 - 6), DarkWood);
            c.Rect(13, 11, 4, 5, Timber); c.Line((13, 13), (16, 13), DarkWood);
            c.Rect(20, 9, 5, 2, Straw);
        }

        // a watermill: the small shop with a water wheel at its side (placeholder art)
        static void DrawWheel(Pixels c)
        {
            var rim = Color(112, 75, 45); var spoke = Color(159, 114, 65); var water = Color(80, 140, 200);
            int cx = 5, cy = 9, r = 7;
            for (int a = 0; a < 24; a++) { double t = a * Math.PI / 12; c.Set(cx + (int)Math.Round(Math.Cos(t) * r), cy + (int)Math.Round(Math.Sin(t) * r * 0.9), rim); }
            c.Line((cx - r, cy), (cx + r, cy), spoke); c.Line((cx, cy - r + 1), (cx, cy + r - 1), spoke);
            c.Rect(cx - 3, 0, 8, 2, water);
        }

        static void DrawClamp(Pixels c)
        {
            var soot = Color(58, 52, 44);
            var earth = Color(118, 92, 60);
            c.Quad((3, 8), (16, 15), (29, 8), (16, 1), Stone);
            c.Quad((6, 7), (10, 15), (19, 18), (25, 10), earth);
            c.Quad((6, 7), (16, 3), (26, 8), (19, 18), Color(89, 68, 49));
            c.Quad((7, 7), (10, 14), (16, 17), (17, 12), Color(145, 116, 74));
            c.Rect(15, 14, 5, 2, soot);
            c.Rect(15, 4, 5, 4, soot);
            c.Set(17, 5, Color(196, 95, 43));
            c.Line((5, 9), (13, 4), Oak);
            c.Line((22, 5), (27, 8), StoneLight);
            c.Set(18, 20, Stone); c.Rect(19, 22, 2, 2, StoneLight);
        }

        static void DrawShop(Pixels c, bool storehouse, int view)
        {
            int width = storehouse ? 30 : 14, wall = storehouse ? 17 : 14, roof = storehouse ? 13 : 10;
            int mid = c.Width / 2;
            (int x, int y) P(int u, int v, int z) => (mid + u - v, width - (u + v) / 2 + z + 1);
            void Wall(bool left, Color32 color)
            {
                (int x, int y) W(int x, int z) => left ? P(x, width, z) : P(width, x, z);
                c.Quad(W(0, 0), W(width, 0), W(width, wall), W(0, wall), color);
                c.Line(W(0, 2), W(width, 2), Stone);
                foreach (int x in new[] { 0, width / 2, width })
                    c.Line(W(x, 2), W(x, wall), DarkWood);
                c.Line(W(0, wall - 2), W(width, wall - 2), Timber);
                bool door = left ? view == 0 : view == 3;
                int lo = door && storehouse ? width / 4 : width / 3;
                int hi = door && storehouse ? width - width / 4 : width - width / 3;
                if (door)
                {
                    c.Quad(W(lo, 2), W(hi, 2), W(hi, wall - 3), W(lo, wall - 3), DarkWood);
                    for (int x = lo + 1; x < hi; x += 2) c.Line(W(x, 3), W(x, wall - 4), Oak);
                    c.Line(W(lo, 5), W(hi, wall - 5), Timber);
                    c.Line(W(lo, 2), W(hi, 2), StoneLight);
                }
                else
                {
                    c.Quad(W(lo, 6), W(hi, 6), W(hi, 10), W(lo, 10), DarkWood);
                    c.Line(W(lo, 5), W(hi, 5), Oak);
                }
            }
            Wall(true, storehouse ? Plaster : StoneLight);
            Wall(false, storehouse ? PlasterShade : Stone);
            var apex = P(width / 2, width / 2, wall + roof);
            c.Triangle(P(0, 0, wall), P(0, width, wall), apex, StrawShade);
            c.Triangle(P(0, 0, wall), P(width, 0, wall), apex, StrawShade);
            c.Triangle(P(0, width, wall), P(width, width, wall), apex, Straw);
            c.Triangle(P(width, 0, wall), P(width, width, wall), apex, StrawShade);
            for (int step = 1; step <= 3; step++)
            {
                int inset = width * step / 8;
                int z = wall + roof * step / 4;
                c.Line(P(inset, width - inset, z), P(width - inset, width - inset, z), StrawLight);
                c.Line(P(width - inset, inset, z), P(width - inset, width - inset, z), Straw);
            }
            c.Line(P(0, width, wall), P(width, width, wall), DarkWood);
            c.Line(P(width, 0, wall), P(width, width, wall), DarkWood);
            if (!storehouse)
            {
                var chimney = P(view < 2 ? width / 3 : width * 2 / 3, width / 2, wall + roof - 5);
                c.Rect(chimney.x, chimney.y, 4, 10, Stone);
                c.Rect(chimney.x, chimney.y + 7, 5, 2, StoneLight);
                c.Rect(chimney.x + 1, chimney.y + 9, 3, 1, DarkWood);
            }
        }

        static Color32 Color(byte r, byte g, byte b) => new Color32(r, g, b, 255);

        sealed class Pixels
        {
            public readonly Color32[] Buffer;
            public readonly int Width;
            readonly int _height;

            public Pixels(int width, int height) { Width = width; _height = height; Buffer = new Color32[width * height]; }

            public void Set(int x, int y, Color32 color)
            {
                if (x >= 0 && x < Width && y >= 0 && y < _height) Buffer[y * Width + x] = color;
            }

            public void Rect(int x, int y, int w, int h, Color32 color)
            {
                for (int row = y; row < y + h; row++)
                    for (int col = x; col < x + w; col++) Set(col, row, color);
            }

            public void Line((int x, int y) a, (int x, int y) b, Color32 color)
            {
                int steps = Mathf.Max(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y));
                for (int i = 0; i <= steps; i++)
                {
                    float t = steps == 0 ? 0 : (float)i / steps;
                    Set(Mathf.RoundToInt(Mathf.Lerp(a.x, b.x, t)), Mathf.RoundToInt(Mathf.Lerp(a.y, b.y, t)), color);
                }
            }

            public void Triangle((int x, int y) a, (int x, int y) b, (int x, int y) c, Color32 color)
            {
                static int Cross((int x, int y) p, (int x, int y) q, int x, int y) =>
                    (q.x - p.x) * (y - p.y) - (q.y - p.y) * (x - p.x);
                for (int y = Mathf.Min(a.y, b.y, c.y); y <= Mathf.Max(a.y, b.y, c.y); y++)
                    for (int x = Mathf.Min(a.x, b.x, c.x); x <= Mathf.Max(a.x, b.x, c.x); x++)
                    {
                        int ab = Cross(a, b, x, y), bc = Cross(b, c, x, y), ca = Cross(c, a, x, y);
                        if ((ab >= 0 && bc >= 0 && ca >= 0) || (ab <= 0 && bc <= 0 && ca <= 0)) Set(x, y, color);
                    }
            }

            public void Quad((int x, int y) a, (int x, int y) b, (int x, int y) c, (int x, int y) d, Color32 color)
            {
                Triangle(a, b, c, color);
                Triangle(a, c, d, color);
            }
        }
    }
}
