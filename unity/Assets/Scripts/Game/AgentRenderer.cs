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
            int dx = x - px, dy = y - py;
            int screenStep = rot switch { 1 => -dy - dx, 2 => dy - dx, 3 => dy + dx, _ => dx - dy };
            r.flipX = screenStep < 0;
            r.sprite = s; r.sortingOrder = IsoView.SortOrder(near.x, near.y); r.enabled = true;
            // same painter's slot as the tile's tree or building; a hair nearer the camera so the walker draws after it (JS draws crews last per tile)
            r.transform.localPosition = new Vector3(sx / IsoView.PPU, -sy / IsoView.PPU, -0.01f);
        }

        void MakeSprites()
        {
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

        void OnDestroy()
        {
            if (_worker == null) return;
            foreach (var sprite in _worker) Destroy(sprite);
            foreach (var sprite in _folk) Destroy(sprite);
        }
    }
}
