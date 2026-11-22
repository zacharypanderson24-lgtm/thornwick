// Brings the browser prototype's pixel art into Unity without touching the originals in ../assets.
// Menu: Crown & Hearth > Import Prototype Art. Writes PNGs into Assets/Resources/Art (committed), processed the way
// src/textures.js does it in the browser:
//   - flat magenta backgrounds are keyed out (flood fill from the corners, plus stray magenta pockets), alpha is snapped to 0/255
//   - ground tiles (grass_N, grassfert_N, 2048 x 1024) are cropped to their diamond and shrunk to 32 x 16 with hard-edged sampling,
//     rejected if the diamond has holes (grass_0 is 3:2 inside its canvas, so the built-in grass replaces it, as in the browser)
//   - a ground atlas is built: grass 0-3, fertile 0-1, mountain rock, snow, road, white (for overlays), and worked stone
//   - placeholder sprites (prefixed ph_) are drawn for everything that has no art yet, in the shared palette from TEXTURE_SPEC.md
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CrownAndHearth.EditorTools
{
    public static class PrototypeArtImporter
    {
        public const string OutDir = "Assets/Resources/Art";
        public static readonly string[] AtlasCells = { "grass_0", "grass_1", "grass_2", "grass_3", "grassfert_0", "grassfert_1", "mountain_rock", "mountain_snow", "road", "white", "worked_stone" };

        internal static string SourceDir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "assets"));

        /// <summary>Only the code-drawn placeholders (ph_*), without re-importing the browser art.</summary>
        [MenuItem("Crown & Hearth/Write Placeholder Art")]
        public static void WritePlaceholderArt() { Directory.CreateDirectory(OutDir); WritePlaceholders(); AssetDatabase.Refresh(); }

        [MenuItem("Crown & Hearth/Import Prototype Art")]
        public static void ImportAll()
        {
            Directory.CreateDirectory(OutDir);
            int objects = 0;
            var ground = new Dictionary<string, Color32[]>();
            var report = new List<string>();
            foreach (var file in Directory.GetFiles(SourceDir, "*.png"))
            {
                string name = Path.GetFileNameWithoutExtension(file);
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!tex.LoadImage(File.ReadAllBytes(file))) { report.Add("could not read " + name); continue; }
                var px = tex.GetPixels32(); int w = tex.width, h = tex.height;
                UnityEngine.Object.DestroyImmediate(tex);
                if (IsNativeSurface(name))
                {
                    if (w == 32 && h == 16 && Array.TrueForAll(px, color => color.a == 255)) ground[name] = px;
                    else report.Add(name + " rejected (expected a fully opaque 32 x 16 surface); built-in ground used");
                    continue;
                }
                KeyOut(px, w, h);
                if (name.StartsWith("grass_") || name.StartsWith("grassfert_"))
                {
                    var cell = FitGround(px, w, h);
                    if (cell != null) ground[name] = cell; else report.Add(name + " rejected (diamond is not 2:1 or has holes); built-in ground used");
                    continue;
                }
                WritePng(Path.Combine(OutDir, name + ".png"), px, w, h);
                objects++;
            }
            WriteAtlas(ground);
            int placeholders = WritePlaceholders();
            AssetDatabase.Refresh();
            Debug.Log($"[Crown & Hearth] Imported {objects} sprites, {ground.Count} ground tiles, {placeholders} placeholders from {SourceDir}." +
                      (report.Count > 0 ? "\n" + string.Join("\n", report) : ""));
        }

        // ---------- Background removal (textures.js keyOut) ----------
        static bool IsMagenta(Color32 c) => c.r > 200 && c.b > 200 && c.g < 110;

        internal static void KeyOut(Color32[] px, int w, int h)
        {
            int[] corners = { 0, w - 1, (h - 1) * w, h * w - 1 };
            bool transparent = true;
            foreach (int i in corners) if (px[i].a > 0) transparent = false;
            if (!transparent)
            {
                int kr = 0, kg = 0, kb = 0;
                foreach (int i in corners) { kr += px[i].r; kg += px[i].g; kb += px[i].b; }
                kr = Mathf.RoundToInt(kr / 4f); kg = Mathf.RoundToInt(kg / 4f); kb = Mathf.RoundToInt(kb / 4f);
                bool Near(Color32 c) { int dr = c.r - kr, dg = c.g - kg, db = c.b - kb; return dr * dr + dg * dg + db * db <= 60 * 60; }
                var seen = new bool[w * h]; var stack = new Stack<int>();
                void Push(int x, int y)
                {
                    if (x < 0 || y < 0 || x >= w || y >= h) return;
                    int k = y * w + x; if (seen[k]) return; seen[k] = true;
                    if (Near(px[k])) { px[k].a = 0; stack.Push(k); }
                }
                for (int x = 0; x < w; x++) { Push(x, 0); Push(x, h - 1); }
                for (int y = 0; y < h; y++) { Push(0, y); Push(w - 1, y); }
                while (stack.Count > 0) { int k = stack.Pop(), x = k % w, y = k / w; Push(x + 1, y); Push(x - 1, y); Push(x, y + 1); Push(x, y - 1); }
            }
            for (int i = 0; i < px.Length; i++)
            {
                if (IsMagenta(px[i])) px[i].a = 0;          // enclosed magenta pockets (gaps inside grove art)
                px[i].a = px[i].a < 128 ? (byte)0 : (byte)255;
            }
        }

        static bool IsNativeSurface(string name) => name == "mountain_rock" || name == "mountain_snow" || name == "worked_stone";

        // ---------- Ground tiles (textures.js fitToSlot + validGround) ----------
        internal static Color32[] FitGround(Color32[] px, int w, int h)
        {
            int x0 = w, y0 = h, x1 = -1, y1 = -1;
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) if (px[y * w + x].a > 40) { x0 = Math.Min(x0, x); x1 = Math.Max(x1, x); y0 = Math.Min(y0, y); y1 = Math.Max(y1, y); }
            if (x1 < 0) return null;
            int bw = x1 - x0 + 1, bh = y1 - y0 + 1;
            if (Mathf.RoundToInt(bh * 32f / bw) != 16) return null;
            var cell = new Color32[32 * 16];
            for (int oy = 0; oy < 16; oy++) for (int ox = 0; ox < 32; ox++)
            {
                int sx = x0 + (int)((ox + 0.5f) * bw / 32f), sy = y0 + (int)((oy + 0.5f) * bh / 16f);   // texture rows run bottom-up in both
                cell[oy * 32 + ox] = px[Math.Min(h - 1, sy) * w + Math.Min(w - 1, sx)];
            }
            // reject holes or magenta inside the diamond
            for (int oy = 0; oy < 16; oy++) for (int ox = 0; ox < 32; ox++)
            {
                float u = Math.Abs(ox + 0.5f - 16) / 16f, v = Math.Abs(oy + 0.5f - 8) / 8f;
                if (u + v >= 0.8f) continue;
                var c = cell[oy * 32 + ox];
                if (c.a < 128 || IsMagenta(c)) return null;
            }
            return FillOutsideDiamond(cell);
        }

        /// <summary>Copy the nearest diamond pixel into the corners so point sampling at the diamond edge never picks up transparency.</summary>
        static Color32[] FillOutsideDiamond(Color32[] cell)
        {
            var out_ = (Color32[])cell.Clone();
            for (int oy = 0; oy < 16; oy++) for (int ox = 0; ox < 32; ox++)
            {
                float u = Math.Abs(ox + 0.5f - 16) / 16f, v = Math.Abs(oy + 0.5f - 8) / 8f;
                if (u + v < 0.9f && cell[oy * 32 + ox].a == 255) continue;
                int bx = 16, by = 8; float best = 1e9f;
                for (int y = 0; y < 16; y++) for (int x = 0; x < 32; x++)
                {
                    float uu = Math.Abs(x + 0.5f - 16) / 16f, vv = Math.Abs(y + 0.5f - 8) / 8f;
                    if (uu + vv >= 0.85f || cell[y * 32 + x].a < 255) continue;
                    float d = (x - ox) * (x - ox) + 4 * (y - oy) * (y - oy);
                    if (d < best) { best = d; bx = x; by = y; }
                }
                out_[oy * 32 + ox] = cell[by * 32 + bx]; out_[oy * 32 + ox].a = 255;
            }
            return out_;
        }

        static void WriteAtlas(Dictionary<string, Color32[]> ground)
        {
            int cells = AtlasCells.Length, W = cells * 32, H = 16;
            var atlas = new Color32[W * H];
            for (int c = 0; c < cells; c++)
            {
                string name = AtlasCells[c];
                var cell = ground.TryGetValue(name, out var g) ? g : BuiltInGround(name, c);
                for (int y = 0; y < 16; y++) for (int x = 0; x < 32; x++) atlas[y * W + c * 32 + x] = cell[y * 32 + x];
            }
            WritePng(Path.Combine(OutDir, "ground_atlas.png"), atlas, W, H);
        }

        static Color32 Hex(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }

        /// <summary>Code-drawn ground in the shared palette, used where no valid art exists (like the browser's built-in grass).</summary>
        static Color32[] BuiltInGround(string name, int salt)
        {
            Color32 b, d, l;
            switch (name)
            {
                case "grassfert_0": case "grassfert_1": b = Hex("#6B9A3E"); d = Hex("#5A4A2E"); l = Hex("#86B055"); break;
                case "mountain_rock": b = Hex("#828783"); d = Hex("#565C5C"); l = Hex("#A6A59A"); break;
                case "mountain_snow": b = Hex("#E6EBF2"); d = Hex("#C9D2DF"); l = Hex("#FFFFFF"); break;
                case "worked_stone": b = Hex("#606153"); d = Hex("#3E4035"); l = Hex("#8A8777"); break;
                case "road": b = Hex("#9C7A4E"); d = Hex("#7E6040"); l = Hex("#B08F60"); break;
                case "white": b = d = l = new Color32(255, 255, 255, 255); break;
                default: b = Hex("#5F9E48"); d = Hex("#4F8C3B"); l = Hex("#79B65B"); break;
            }
            var cell = new Color32[32 * 16];
            for (int y = 0; y < 16; y++) for (int x = 0; x < 32; x++)
            {
                double r = Sim.Noise.Hash2(x, y, 900 + salt);
                if (IsNativeSurface(name))
                {
                    double grain = Sim.Noise.VNoise(x * 0.31, y * 0.43, 730 + salt);
                    double strata = (y + x * 0.29 + grain * 2.5) % 4;
                    cell[y * 32 + x] = strata < 0.85 && grain < 0.63 ? d :
                        strata < 1.65 || r > 0.94 ? l : b;
                    continue;
                }
                cell[y * 32 + x] = r < 0.08 ? d : r > 0.95 ? l : b;
            }
            return cell;
        }

        // ---------- Placeholders (iso blocks in the TEXTURE_SPEC palette) ----------
        class Block { public int Inset, Lift, H; public string L, R, Top; public bool Stripes, Specks; }

        static int WritePlaceholders()
        {
            var defs = new Dictionary<string, Block[]>
            {
                { "farm", new[] { new Block { Inset = 0, H = 1, L = "#7C5A22", R = "#6E4526", Top = "#C9A04A", Stripes = true } } },
                { "wood", new[] { new Block { Inset = 3, H = 9, L = "#6E4526", R = "#5A3820", Top = "#6E4526" }, new Block { Inset = 2, Lift = 9, H = 5, L = "#5E7A3A", R = "#4A6230", Top = "#6F8F45" } } },
                { "forester", new[] { new Block { Inset = 4, H = 8, L = "#8A5A34", R = "#6E4526", Top = "#8A5A34" }, new Block { Inset = 3, Lift = 8, H = 5, L = "#5E7A3A", R = "#4A6230", Top = "#79B65B" } } },
                { "sawmill", new[] { new Block { Inset = 2, H = 8, L = "#A0703C", R = "#8A5A34", Top = "#A0703C" }, new Block { Inset = 1, Lift = 8, H = 4, L = "#6B7080", R = "#565A68", Top = "#858A9C" } } },
                { "quarry", new[] { new Block { Inset = 1, H = 3, L = "#858A9C", R = "#666B7C", Top = "#4D5262" } } },
                { "mine", new[] { new Block { Inset = 2, H = 8, L = "#5A5E6B", R = "#474A55", Top = "#6A6E7A" }, new Block { Inset = 11, Lift = 0, H = 5, L = "#2A2420", R = "#1E1A17", Top = "#6E4526" } } },
                { "market", new[] { new Block { Inset = 4, H = 6, L = "#E8D9B0", R = "#CDBB8C", Top = "#E8D9B0" }, new Block { Inset = 3, Lift = 6, H = 3, L = "#B5533C", R = "#8F3F2E", Top = "#E8D9B0" } } },
                { "well", new[] { new Block { Inset = 9, H = 5, L = "#858A9C", R = "#666B7C", Top = "#4C8FD6" } } },
                { "cistern", new[] { new Block { Inset = 4, H = 5, L = "#858A9C", R = "#666B7C", Top = "#858A9C" }, new Block { Inset = 6, Lift = 5, H = 1, L = "#3E77B8", R = "#2F5F96", Top = "#4C8FD6" } } },
                { "house_2", new[] { new Block { Inset = 2, H = 6, L = "#858A9C", R = "#666B7C", Top = "#858A9C" }, new Block { Inset = 2, Lift = 6, H = 5, L = "#E8D9B0", R = "#CDBB8C", Top = "#E8D9B0" }, new Block { Inset = 1, Lift = 11, H = 5, L = "#B5533C", R = "#8F3F2E", Top = "#B5533C" } } },
                { "rock_0", new[] { new Block { Inset = 9, H = 4, L = "#858A9C", R = "#666B7C", Top = "#9DA2B2" } } },
                { "rock_1", new[] { new Block { Inset = 6, H = 7, L = "#858A9C", R = "#666B7C", Top = "#9DA2B2" } } },
                { "ore_0", new[] { new Block { Inset = 8, H = 5, L = "#5A5E6B", R = "#474A55", Top = "#6A6E7A", Specks = true } } },
                { "ore_1", new[] { new Block { Inset = 7, H = 6, L = "#5A5E6B", R = "#474A55", Top = "#6A6E7A", Specks = true } } },
                { "stump", new[] { new Block { Inset = 12, H = 3, L = "#8A5A34", R = "#6E4526", Top = "#C9A04A" } } },
                { "sapling", new[] { new Block { Inset = 13, H = 7, L = "#4F8C3B", R = "#3F7430", Top = "#79B65B" } } },
            };
            // 2x2 buildings that grow on zones (64 x 88 canvas)
            var big = new Dictionary<string, Block[]>
            {
                { "manor_1", new[] { new Block { Inset = 4, H = 12, L = "#E8D9B0", R = "#CDBB8C", Top = "#E8D9B0" }, new Block { Inset = 2, Lift = 12, H = 9, L = "#C9A04A", R = "#A8802F", Top = "#C9A04A" } } },
                { "manor_2", new[] { new Block { Inset = 4, H = 9, L = "#858A9C", R = "#666B7C", Top = "#858A9C" }, new Block { Inset = 4, Lift = 9, H = 9, L = "#E8D9B0", R = "#CDBB8C", Top = "#E8D9B0" }, new Block { Inset = 2, Lift = 18, H = 9, L = "#B5533C", R = "#8F3F2E", Top = "#B5533C" } } },
                { "farm2", new[] { new Block { Inset = 0, H = 1, L = "#7C5A22", R = "#6E4526", Top = "#C9A04A", Stripes = true }, new Block { Inset = 34, Lift = 1, H = 10, L = "#8F3F2E", R = "#7A3526", Top = "#6E4526" } } },
                { "market2", new[] { new Block { Inset = 0, H = 1, L = "#858A9C", R = "#666B7C", Top = "#9DA2B2" }, new Block { Inset = 20, Lift = 1, H = 7, L = "#E8D9B0", R = "#CDBB8C", Top = "#B5533C" } } },
            };
            foreach (var (set, W, H) in new[] { (defs, 32, 48), (big, 64, 88) })
                foreach (var kv in set)
                {
                    var px = new Color32[W * H];
                    foreach (var blk in kv.Value) DrawBlock(px, W, H, blk);
                    WritePng(Path.Combine(OutDir, "ph_" + kv.Key + ".png"), px, W, H);
                }
            return defs.Count + big.Count;
        }

        /// <summary>An iso box standing on a (possibly inset) footprint diamond whose bottom corner is the image's bottom centre.</summary>
        static void DrawBlock(Color32[] px, int W, int H, Block b)
        {
            Color32 cl = Hex(b.L), cr = Hex(b.R), ct = Hex(b.Top), dark = Hex("#7C5A22"), fleck = Hex("#E0873A");
            float hw = W / 2f - b.Inset, bottom = H - b.Inset / 2f - b.Lift;
            for (int x = 0; x < W; x++)
            {
                float dx = Math.Abs(x + 0.5f - W / 2f);
                if (dx > hw) continue;
                float yb = bottom - dx / 2f;          // front edge of the footprint diamond in this column
                float yt = bottom - hw + dx / 2f;     // back edge
                for (int y = 0; y < H; y++)
                {
                    float cy = y + 0.5f; Color32? c = null;
                    if (cy >= yb - b.H && cy < yb) c = x + 0.5f < W / 2f ? cl : cr;
                    else if (cy >= yt - b.H && cy < yb - b.H)
                    {
                        c = ct;
                        if (b.Stripes && ((x + 2 * y) / 3) % 2 == 0) c = dark;
                        if (b.Specks && Sim.Noise.Hash2(x, y, 31) > 0.82) c = fleck;
                    }
                    if (c.HasValue) px[(H - 1 - y) * W + x] = c.Value;   // our y runs top-down, texture rows bottom-up
                }
            }
        }

        static void WritePng(string path, Color32[] px, int w, int h)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            t.SetPixels32(px); t.Apply();
            File.WriteAllBytes(path, t.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(t);
        }
    }

    /// <summary>Import settings for everything in Assets/Resources/Art: crisp pixel sprites, 32 px per unit, pivot on the bottom corner.</summary>
    public class PixelArtPostprocessor : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').StartsWith(PrototypeArtImporter.OutDir)) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = 32;
            ti.filterMode = FilterMode.Point;
            ti.mipmapEnabled = false;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.alphaIsTransparency = true;
            string slot = Path.GetFileNameWithoutExtension(assetPath);
            ti.isReadable = slot == "hall" || slot == "barracks" ||
                slot.StartsWith("hall_", StringComparison.Ordinal) || slot.StartsWith("barracks_", StringComparison.Ordinal);
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.npotScale = TextureImporterNPOTScale.None;
            var s = new TextureImporterSettings();
            ti.ReadTextureSettings(s);
            s.spriteAlignment = (int)SpriteAlignment.Custom;
            s.spritePivot = new Vector2(0.5f, 0f);
            s.spriteMeshType = SpriteMeshType.FullRect;
            ti.SetTextureSettings(s);
        }
    }
}
