// Work crews and townsfolk on the map (JS draw(): the crewAt buckets, SPR.worker and SPR.folk).
// The sprites are drawn in code from the same pixels as src/ui.js: crew workers 6x10 (tunic by trade, a carried load on the way home),
// townsfolk 3x5 (six tunic colours, two stepping frames). Positions slide between tiles through the day (the browser's tickFrac).
// Pooled SpriteRenderers, updated every frame without allocations; hidden at the 0.25x/0.5x zooms like the browser's LOD.
using System.Collections.Generic;
using CrownAndHearth.Sim;
using UnityEngine;
using static CrownAndHearth.Sim.Defs;

namespace CrownAndHearth.Game
{
    public class AgentRenderer : MonoBehaviour
    {
        Material _mat;
        Sprite[,] _worker;   // [kind 0 tree / 1 rock / 2 ore, carrying 0/1]
        Sprite[,] _folk;     // [tunic, frame]
        readonly List<SpriteRenderer> _pool = new List<SpriteRenderer>();
        int _used;

        public void Init(Material mat) { _mat = mat; MakeSprites(); }

        /// <summary>Place every crew worker and villager who is out. frac = how far through the current day (0..1).</summary>
        public void Draw(World w, int rot, float frac, bool lod)
        {
            _used = 0;
            if (!lod)
            {
                foreach (var b in w.Blds)
                {
                    var kind = World.CrewKind(b.T); if (kind == Deco.None) continue;
                    int k = kind == Deco.Tree ? 0 : kind == Deco.Rock ? 1 : 2;
                    for (int i = 0; i < b.Crew.Count; i++)
                    {
                        var a = b.Crew[i]; if (a.St == CrewState.Home) continue;
                        int jit = (i * 3) % 5 - 2;
                        Put(w, rot, _worker[k, a.St == CrewState.Back && a.Carry > 0 ? 1 : 0], a.Px, a.Py, a.X, a.Y, frac, jit, 0);
                    }
                }
                foreach (var v in w.Folk)
                {
                    if (v.St == FolkState.Home) continue;
                    int frame = v.St == FolkState.Walk || v.St == FolkState.Back ? (v.I + v.Id) & 1 : 0;
                    Put(w, rot, _folk[v.V, frame], v.Px, v.Py, v.X, v.Y, frac, (v.Id * 3) % 5 - 2, (v.Id * 7) % 3 - 1);
                }
            }
            for (int i = _used; i < _pool.Count; i++) if (_pool[i].enabled) _pool[i].enabled = false;
        }

        void Put(World w, int rot, Sprite s, int px, int py, int x, int y, float frac, int jit, int jy)
        {
            float wx = px + (x - px) * frac, wy = py + (y - py) * frac;
            // view coordinates of the lerped point (JS toView on fractional tiles)
            float m = N - 1, vx, vy;
            switch (rot) { case 1: vx = m - wy; vy = wx; break; case 2: vx = m - wx; vy = m - wy; break; case 3: vx = wy; vy = m - wx; break; default: vx = wx; vy = wy; break; }
            float lvl = Mathf.Lerp((float)w.TileLevel(px, py), (float)w.TileLevel(x, y), frac);
            // feet one pixel below the tile centre, as in the browser
            float sx = Mathf.Round((vx - vy) * 16f + jit), sy = Mathf.Round((vx + vy) * 8f + 8f - lvl * LvlPx + 1 + jy);
            var near = IsoView.ToView(Mathf.RoundToInt(wx), Mathf.RoundToInt(wy), rot);

            SpriteRenderer r;
            if (_used < _pool.Count) r = _pool[_used];
            else
            {
                var go = new GameObject("agent");
                go.transform.SetParent(transform, false);
                r = go.AddComponent<SpriteRenderer>(); r.sharedMaterial = _mat;
                _pool.Add(r);
            }
            _used++;
            r.sprite = s; r.sortingOrder = IsoView.SortOrder(near.x, near.y); r.enabled = true;
            // same painter's slot as the tile's tree or building; a hair nearer the camera so the walker draws after it (JS draws crews last per tile)
            r.transform.localPosition = new Vector3(sx / IsoView.PPU, -sy / IsoView.PPU, -0.01f);
        }

        // ---------- Sprites (pixels from src/ui.js) ----------
        static Color32 C(string hex, float a = 1)
        {
            ColorUtility.TryParseHtmlString(hex, out var c); c.a = a; return c;
        }

        void MakeSprites()
        {
            // one small atlas: 3 kinds x 2 worker frames (6x10) on the first row, 6 tunics x 2 folk frames (3x5) below
            const int W = 36, H = 16;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "agents" };
            var px = new Color32[W * H];
            // canvas coordinates are y-down; the texture is y-up
            void Fill(int ox, int oy, int h, int x, int y, int w, int hh, Color32 c)
            {
                for (int j = y; j < y + hh; j++) for (int i = x; i < x + w; i++) px[(oy + h - 1 - j) * W + ox + i] = c;
            }
            _worker = new Sprite[3, 2];
            var kinds = new[] { ("#5f8a3c", "#8a5a2b"), ("#8a8d98", "#b4b8c4"), ("#a8622a", "#e0873a") };
            for (int k = 0; k < 3; k++)
                for (int carry = 0; carry < 2; carry++)
                {
                    int ox = (k * 2 + carry) * 6, oy = H - 10;
                    var (tunic, load) = kinds[k];
                    void F(int x, int y, int w, int h, Color32 c) => Fill(ox, oy, 10, x, y, w, h, c);
                    F(0, 9, 6, 1, C("#000000", 0.28f));
                    F(1, 7, 1, 2, C("#3a2a1a")); F(4, 7, 1, 2, C("#3a2a1a"));
                    F(1, 3, 4, 5, C("#20180f")); F(2, 4, 2, 3, C(tunic));
                    F(2, 1, 2, 2, C("#e0b890")); F(1, 0, 4, 1, C("#4a3220"));
                    if (carry == 1) { F(4, 2, 2, 3, C("#20180f")); F(4, 3, 2, 1, C(load)); }
                }
            _folk = new Sprite[World.FolkVariants, 2];
            var tunics = new[] { "#8a5a34", "#6b7f3a", "#8d93a5", "#a3412f", "#c9a24a", "#4d6d92" };
            for (int t = 0; t < tunics.Length; t++)
                for (int fr = 0; fr < 2; fr++)
                {
                    int ox = (t * 2 + fr) * 3, oy = 0;
                    void F(int x, int y, int w, int h, Color32 c) => Fill(ox, oy, 5, x, y, w, h, c);
                    F(1, 0, 1, 1, C("#e0b890"));
                    F(0, 1, 3, 2, C(tunics[t]));
                    var leg = C("#2e2218");
                    if (fr == 1) { F(0, 4, 1, 1, leg); F(2, 3, 1, 1, leg); F(1, 3, 1, 1, leg); }
                    else { F(0, 3, 1, 1, leg); F(2, 4, 1, 1, leg); F(1, 3, 1, 1, leg); }
                }
            tex.SetPixels32(px); tex.Apply(false, true);
            // pivot: bottom-centre (whole pixels), so the feet stand on the point Put computes
            for (int k = 0; k < 3; k++) for (int c = 0; c < 2; c++)
                _worker[k, c] = Sprite.Create(tex, new Rect((k * 2 + c) * 6, H - 10, 6, 10), new Vector2(3f / 6f, 0), IsoView.PPU, 0, SpriteMeshType.FullRect);
            for (int t = 0; t < tunics.Length; t++) for (int fr = 0; fr < 2; fr++)
                _folk[t, fr] = Sprite.Create(tex, new Rect((t * 2 + fr) * 3, 0, 3, 5), new Vector2(1f / 3f, 0), IsoView.PPU, 0, SpriteMeshType.FullRect);
        }
    }
}
