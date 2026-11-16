// Trees, rocks, ore, stumps, saplings and buildings as pooled SpriteRenderers, sorted back-to-front by view diagonal
// (the browser's painter's order). Rebuilt when the world changes or the camera rotates.
using System.Collections.Generic;
using CrownAndHearth.Sim;
using UnityEngine;
using static CrownAndHearth.Sim.Defs;

namespace CrownAndHearth.Game
{
    public class ObjectRenderer : MonoBehaviour
    {
        ArtLibrary _art;
        Material _mat;
        readonly List<SpriteRenderer> _pool = new List<SpriteRenderer>();
        int _used;

        public void Init(ArtLibrary art, Material mat) { _art = art; _mat = mat; }

        public void Build(World w, int rot)
        {
            _used = 0;
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    var b = w.Grid[y, x];
                    if (b != null)
                    {
                        if (b.T != "road" && b.X == x && b.Y == y) PlaceBuilding(w, b, rot);
                        continue;
                    }
                    var d = w.DecoMap[y, x];
                    if (d == Deco.None) continue;
                    var sprite = DecoSprite(w, x, y, d);
                    if (sprite == null) continue;
                    var v = IsoView.ToView(x, y, rot);
                    float lvl = Mathf.Round((float)w.TileLevel(x, y) * LvlPx) / LvlPx;
                    Put(sprite, IsoView.VertexPos(v.x + 1, v.y + 1, lvl), IsoView.SortOrder(v.x, v.y));
                }
            for (int i = _used; i < _pool.Count; i++) if (_pool[i].enabled) _pool[i].enabled = false;
        }

        Sprite DecoSprite(World w, int x, int y, Deco d)
        {
            switch (d)
            {
                case Deco.Tree:
                    var (n, kind) = w.TreeGrove(x, y);
                    return (n > 1 && kind != null ? _art.Find($"tree_{kind}_{n}") : null) ?? _art.Find("tree_" + w.TreeVariant(x, y));
                case Deco.Rock:
                    if (w.IsMountain(x, y)) return null;            // mountains are part of the ground mesh
                    int lv = w.RockLevel(x, y);
                    return lv >= 2 ? _art.Find("ph_rock_1") : lv == 1 ? _art.Find("ph_rock_0") : null;
                case Deco.Ore: return _art.Find(Noise.Hash2(x, y, 11) < 0.5 ? "ph_ore_0" : "ph_ore_1");
                case Deco.Stump: return _art.Find("ph_stump");
                case Deco.Sapling: return _art.Find("ph_sapling");
            }
            return null;
        }

        void PlaceBuilding(World w, Building b, int rot)
        {
            // the footprint's front tile in this view: its bottom corner is the sprite's anchor
            Vector2Int front = new Vector2Int(-1, -1); int best = -1;
            foreach (var (tx, ty) in b.Tiles()) { var v = IsoView.ToView(tx, ty, rot); if (v.x + v.y > best) { best = v.x + v.y; front = v; } }
            int view = ((rot + Mathf.Max(0, b.Face)) % 4 + 4) % 4;
            var sprite = _art.Building(SlotName(b), view);
            if (sprite == null) return;
            int lvl = IsoView.VhView(w, front.x + 1, front.y + 1, rot);
            Put(sprite, IsoView.VertexPos(front.x + 1, front.y + 1, lvl), IsoView.SortOrder(front.x, front.y));
        }

        /// <summary>The art slot for a building (JS slotName + the hut-variant rule).</summary>
        public static string SlotName(Building b)
        {
            if (b.T == "house")
            {
                if (b.Lvl == 2) return "house_2";
                return b.Var > 1 ? "house_1_v" + b.Var : "house_1";
            }
            return b.T;
        }

        void Put(Sprite s, Vector3 pos, int order)
        {
            SpriteRenderer r;
            if (_used < _pool.Count) r = _pool[_used];
            else
            {
                var go = new GameObject("obj");
                go.transform.SetParent(transform, false);
                r = go.AddComponent<SpriteRenderer>();
                r.sharedMaterial = _mat;
                _pool.Add(r);
            }
            _used++;
            r.sprite = s; r.sortingOrder = order; r.enabled = true;
            r.transform.localPosition = pos;
        }
    }
}
