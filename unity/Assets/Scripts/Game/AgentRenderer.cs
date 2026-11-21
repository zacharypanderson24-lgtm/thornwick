// Work crews and townsfolk on the map (JS draw(): the crewAt buckets, SPR.worker and SPR.folk).
// Point-sampled PNG atlas with trade outfits, carried materials, and two walking poses.
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
        Sprite[,,] _worker;  // [trade, carrying, step]
        Sprite[,] _folk;     // [tunic, frame]
        Sprite[,] _raider;   // [carrying, step], drawn in code
        Texture2D _raiderTex;
        Sprite[,] _fighter;   // [infantry or archer, walk 0 / walk 1 / attack]
        Sprite[] _fallen;     // [raider, soldier]
        Sprite _arrow;
        Texture2D _fighterTex;
        readonly List<SpriteRenderer> _pool = new List<SpriteRenderer>();
        int _used;

        public void Init(Material mat) { _mat = mat; MakeSprites(); }

        /// <summary>Place every crew worker and villager who is out. frac = how far through the current day (0..1).</summary>
        public void Draw(World w, int rot, float frac, bool lod)
        {
            _used = 0;
            if (!lod && _worker != null)
            {
                foreach (var b in w.Blds)
                {
                    var kind = World.CrewKind(b.T); if (kind == Deco.None) continue;
                    int k = kind == Deco.Tree ? 0 : kind == Deco.Rock ? 1 : 2;
                    for (int i = 0; i < b.Crew.Count; i++)
                    {
                        var a = b.Crew[i]; if (a.St == CrewState.Home || a.St == CrewState.Down) continue;   // underground miners are not drawn
                        int jit = (i * 3) % 5 - 2;
                        int step = (a.Px != a.X || a.Py != a.Y) && frac >= 0.5f ? 1 : 0;
                        Put(w, rot, _worker[k, a.St == CrewState.Back && a.Carry > 0 ? 1 : 0, step], a.Px, a.Py, a.X, a.Y, frac, jit, 0);
                    }
                }
                if (w.Raid != null && _raider != null)
                    foreach (var a in w.Raid.Raiders)
                    {
                        if (a.St == RaiderState.Waiting || a.St == RaiderState.Gone) continue;
                        if (a.St == RaiderState.Dead) { if (w.Day - a.DeadDay <= World.BodyDays) Put(w, rot, _fallen[0], a.X, a.Y, a.X, a.Y, 1f, (a.Id * 3) % 5 - 2, 0); continue; }
                        int step = (a.Px != a.X || a.Py != a.Y) && frac >= 0.5f ? 1 : 0;
                        Put(w, rot, _raider[a.Carry > 0 ? 1 : 0, step], a.Px, a.Py, a.X, a.Y, frac, (a.Id * 3) % 5 - 2, (a.Id * 7) % 3 - 1, a.Hurt && frac > 0.3f);
                    }
                if (w.Raid != null && _fighter != null)
                    foreach (var f in w.Raid.Fighters)
                    {
                        int kind = f.Kind == Troop.Archer ? 1 : 0, jit = (f.Id * 3) % 5 - 2, jy = (f.Id * 7) % 3 - 1;
                        if (f.St == FighterState.Dead) { if (w.Day - f.DeadDay <= World.BodyDays) Put(w, rot, _fallen[1], f.X, f.Y, f.X, f.Y, 1f, jit, 0); continue; }
                        if (f.St == FighterState.Idle) continue;   // inside the barracks
                        int step = (f.Px != f.X || f.Py != f.Y) && frac >= 0.5f ? 1 : 0;
                        int frame = f.Attacked && frac >= 0.5f ? 2 : step;
                        Put(w, rot, _fighter[kind, frame], f.Px, f.Py, f.X, f.Y, frac, jit, jy, f.Hurt && frac > 0.3f);
                        if (f.Shooting) Put(w, rot, _arrow, f.X, f.Y, f.ShotX, f.ShotY, frac, 0, -7);
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

        void Put(World w, int rot, Sprite s, int px, int py, int x, int y, float frac, int jit, int jy, bool hurt = false)
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
            int dx = x - px, dy = y - py;
            int screenStep = rot switch { 1 => -dy - dx, 2 => dy - dx, 3 => dy + dx, _ => dx - dy };
            r.flipX = screenStep < 0;
            r.sprite = s; r.color = hurt ? new Color(1f, 0.45f, 0.4f) : Color.white; r.sortingOrder = IsoView.SortOrder(near.x, near.y); r.enabled = true;
            // same painter's slot as the tile's tree or building; a hair nearer the camera so the walker draws after it (JS draws crews last per tile)
            r.transform.localPosition = new Vector3(sx / IsoView.PPU, -sy / IsoView.PPU, -0.01f);
        }

        void MakeSprites()
        {
            MakeRaiderSprites();
            MakeFighterSprites();
            const int width = 8, height = 12, columns = 12;
            var atlas = Resources.Load<Texture2D>("Art/agents");
            if (atlas == null || atlas.width != width * columns || atlas.height != height * 2)
            {
                Debug.LogError("Expected a 96×24 people atlas at Resources/Art/agents.");
                return;
            }

            Sprite Frame(int column, int row) => Sprite.Create(atlas,
                new Rect(column * width, row * height, width, height), new Vector2(0.5f, 0),
                IsoView.PPU, 0, SpriteMeshType.FullRect);

            _worker = new Sprite[3, 2, 2];
            for (int trade = 0; trade < 3; trade++)
                for (int carrying = 0; carrying < 2; carrying++)
                    for (int step = 0; step < 2; step++)
                        _worker[trade, carrying, step] = Frame(trade * 4 + carrying * 2 + step, 1);

            _folk = new Sprite[World.FolkVariants, 2];
            for (int tunic = 0; tunic < World.FolkVariants; tunic++)
                for (int step = 0; step < 2; step++)
                    _folk[tunic, step] = Frame(tunic * 2 + step, 0);
        }

        /// <summary>Raiders: a hooded figure in a red cloak with a spear; with a sack on the back once they carry loot. 8 x 12, two walking frames.</summary>
        void MakeRaiderSprites()
        {
            const int W = 8, H = 12;
            _raiderTex = new Texture2D(W * 4, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "raiders" };
            var clear = new Color32(0, 0, 0, 0);
            var px = new Color32[W * 4 * H];
            for (int i = 0; i < px.Length; i++) px[i] = clear;
            Color32 cloak = new Color32(155, 47, 42, 255), edge = new Color32(98, 28, 28, 255), skin = new Color32(224, 184, 138, 255), hood = new Color32(59, 42, 42, 255), steel = new Color32(190, 190, 196, 255), sack = new Color32(201, 160, 90, 255);
            void P(int ox, int x, int y, Color32 c) { if (x >= 0 && x < W && y >= 0 && y < H) px[y * W * 4 + ox + x] = c; }
            for (int carrying = 0; carrying < 2; carrying++)
                for (int step = 0; step < 2; step++)
                {
                    int ox = (carrying * 2 + step) * W;
                    for (int y = 3; y <= 8; y++) for (int x = 2; x <= 5; x++) P(ox, x, y, x == 2 || x == 5 ? edge : cloak);
                    P(ox, 3, 9, skin); P(ox, 4, 9, skin); P(ox, 3, 10, skin); P(ox, 4, 10, skin);
                    for (int x = 2; x <= 5; x++) P(ox, x, 11, hood);
                    P(ox, 2, 10, hood); P(ox, 5, 10, hood);
                    if (step == 0) { P(ox, 2, 2, hood); P(ox, 2, 1, hood); P(ox, 5, 2, hood); P(ox, 5, 1, hood); P(ox, 2, 0, hood); P(ox, 5, 0, hood); }
                    else { P(ox, 3, 2, hood); P(ox, 3, 1, hood); P(ox, 4, 2, hood); P(ox, 4, 1, hood); P(ox, 3, 0, hood); P(ox, 4, 0, hood); }
                    for (int y = 2; y <= 10; y++) P(ox, 6, y, y >= 9 ? steel : hood);
                    P(ox, 7, 10, steel);
                    if (carrying == 1) for (int y = 4; y <= 7; y++) for (int x = 0; x <= 1; x++) P(ox, x, y, sack);
                }
            _raiderTex.SetPixels32(px); _raiderTex.Apply();
            _raider = new Sprite[2, 2];
            for (int carrying = 0; carrying < 2; carrying++)
                for (int step = 0; step < 2; step++)
                    _raider[carrying, step] = Sprite.Create(_raiderTex, new Rect((carrying * 2 + step) * W, 0, W, H), new Vector2(0.5f, 0), IsoView.PPU, 0, SpriteMeshType.FullRect);
        }

        /// <summary>Soldiers drawn in code: infantry in a blue tabard with helmet, sword and shield; archers in green with a bow. Walk 0, walk 1 and an
        /// attack pose each, plus a fallen raider and a fallen soldier lying down, and a little arrow. All 8 x 12.</summary>
        void MakeFighterSprites()
        {
            const int W = 8, H = 12, Cells = 8;
            _fighterTex = new Texture2D(W * Cells, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "fighters" };
            var px = new Color32[W * Cells * H];
            var clear = new Color32(0, 0, 0, 0);
            for (int i = 0; i < px.Length; i++) px[i] = clear;
            Color32 C(char c) => c switch
            {
                'h' => new Color32(176, 180, 190, 255), 's' => new Color32(224, 184, 138, 255), 'b' => new Color32(58, 90, 168, 255), 'B' => new Color32(36, 56, 112, 255),
                'w' => new Color32(215, 215, 225, 255), 'o' => new Color32(120, 80, 40, 255), 'l' => new Color32(50, 40, 40, 255),
                'g' => new Color32(63, 122, 63, 255), 'G' => new Color32(40, 80, 40, 255), 't' => new Color32(130, 90, 45, 255), 'S' => new Color32(225, 225, 200, 255),
                'r' => new Color32(155, 47, 42, 255), 'R' => new Color32(98, 28, 28, 255), _ => clear,
            };
            void Cell(int cell, string[] rows, int fromRow = 0)
            {
                for (int i = 0; i < rows.Length; i++)
                    for (int x = 0; x < W; x++)
                    {
                        char c = rows[i][x]; if (c == '.') continue;
                        int y = H - 1 - (fromRow + i);
                        px[y * W * Cells + cell * W + x] = C(c);
                    }
            }
            var infLegs0 = new[] { "..l..l..", "..l..l..", "..l..l..", "..l..l.." };
            var legs1 = new[] { "...ll...", "...ll...", "...ll...", "...ll..." };
            var inf = new[] { "..hhhh..", "..hssh..", "..hhhh..", "obbbbbw.", "obBBBbw.", "obbbbb..", "..bBBb..", "..bbbb.." };
            var infAtk = new[] { "..hhhh.w", "..hssh.w", "..hhhh.w", "obbbbbbw", "obBBBb..", "obbbbb..", "..bBBb..", "..bbbb.." };
            var arc = new[] { "..gggg..", "..gssg.t", "..gggg.t", ".gGGGg.t", ".gGGGgSt", ".gGGGg.t", "..gGGg.t", "..gggg.." };
            var arcAtk = new[] { "..gggg..", "..gssg.t", "..gggg.t", ".gGGGgSt", "SSSGGgSt", ".gGGGgSt", "..gGGg.t", "..gggg.." };
            Cell(0, inf); Cell(0, infLegs0, 8);
            Cell(1, inf); Cell(1, legs1, 8);
            Cell(2, infAtk); Cell(2, infLegs0, 8);
            Cell(3, arc); Cell(3, infLegs0, 8);
            Cell(4, arc); Cell(4, legs1, 8);
            Cell(5, arcAtk); Cell(5, infLegs0, 8);
            Cell(6, new[] { "........", ".RrrrRh.", "RrrrrRss" }, 9);   // a fallen raider
            Cell(7, new[] { "........", ".BbbbBh.", "BbbbbBss" }, 9);   // a fallen soldier
            _fighterTex.SetPixels32(px); _fighterTex.Apply();
            _fighter = new Sprite[2, 3];
            for (int kind = 0; kind < 2; kind++)
                for (int frame = 0; frame < 3; frame++)
                    _fighter[kind, frame] = Sprite.Create(_fighterTex, new Rect((kind * 3 + frame) * W, 0, W, H), new Vector2(0.5f, 0), IsoView.PPU, 0, SpriteMeshType.FullRect);
            _fallen = new Sprite[2];
            for (int i = 0; i < 2; i++)
                _fallen[i] = Sprite.Create(_fighterTex, new Rect((6 + i) * W, 0, W, H), new Vector2(0.5f, 0), IsoView.PPU, 0, SpriteMeshType.FullRect);
            var arrowTex = new Texture2D(4, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "arrow" };
            arrowTex.SetPixels32(new[] { new Color32(130, 90, 45, 255), new Color32(130, 90, 45, 255), new Color32(130, 90, 45, 255), new Color32(215, 215, 225, 255) });
            arrowTex.Apply();
            _arrow = Sprite.Create(arrowTex, new Rect(0, 0, 4, 1), new Vector2(0.5f, 0), IsoView.PPU, 0, SpriteMeshType.FullRect);
        }

        void OnDestroy()
        {
            if (_fighter != null) { foreach (var sprite in _fighter) Destroy(sprite); foreach (var sprite in _fallen) Destroy(sprite); Destroy(_arrow.texture); Destroy(_arrow); Destroy(_fighterTex); }
            if (_raider != null) { foreach (var sprite in _raider) Destroy(sprite); Destroy(_raiderTex); }
            if (_worker == null) return;
            foreach (var sprite in _worker) Destroy(sprite);
            foreach (var sprite in _folk) Destroy(sprite);
        }
    }
}
