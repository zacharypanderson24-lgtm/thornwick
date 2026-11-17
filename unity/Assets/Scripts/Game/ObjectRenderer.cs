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
        int _used, _cropDay;
        FarmCropArt _crops;
        readonly List<(SpriteRenderer renderer, int crop, int offset)> _fields = new();

        void OnDestroy() => _crops?.Dispose();

        public void Init(ArtLibrary art, Material mat) { _art = art; _mat = mat; _crops = new FarmCropArt(); }

        public void Build(World w, int rot)
        {
            _used = 0;
            _fields.Clear();
            _cropDay = w.Day;
            var fields = w.FarmFields();
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    var b = w.Grid[y, x];
                    if (b != null)
                    {
                        if (b.T != "road" && b.X == x && b.Y == y) PlaceBuilding(w, b, rot);
                        continue;
                    }
                    if (fields.ContainsKey(World.Tk(x, y)))
                    {
                        var (crop, offset) = FarmCropArt.Look(x, y, w.Seed);
                        var plant = _crops.Tile(crop, w.Day, offset);
                        var tile = IsoView.ToView(x, y, rot);
                        if (plant != null)
                        {
                            int baseHeight = IsoView.VhView(w, tile.x + 1, tile.y + 1, rot);
                            var renderer = Put(plant, IsoView.VertexPos(tile.x + 1, tile.y + 1, baseHeight),
                                IsoView.SortOrder(tile.x, tile.y));
                            _fields.Add((renderer, crop, offset));
                        }
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
                    return lv >= 2 ? NatureSprite("rock_1") : lv == 1 ? NatureSprite("rock_0") : null;
                case Deco.Ore: return NatureSprite(Noise.Hash2(x, y, 11) < 0.5 ? "ore_0" : "ore_1");
                case Deco.Stump: return NatureSprite("stump");
                case Deco.Sapling: return NatureSprite("sapling");
            }
            return null;
        }

        Sprite NatureSprite(string slot) => _art.Find(slot) ?? _art.Find("ph_" + slot);

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
            if (b.Sz == 2)
            {
                if (b.T == "house") return "manor_" + b.Lvl;
                if (b.T == "farm") return "farm2";
                if (b.T == "market") return "market2";
            }
            if (b.T == "house")
            {
                if (b.Lvl == 2) return "house_2";
                return b.Var > 1 ? "house_1_v" + b.Var : "house_1";
            }
            return b.T;
        }

        public void RefreshCropStages(int day)
        {
            if (day == _cropDay) return;
            foreach (var field in _fields)
                field.renderer.sprite = _crops.Tile(field.crop, day, field.offset);
            _cropDay = day;
        }

        SpriteRenderer Put(Sprite s, Vector3 pos, int order)
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
            return r;
        }
    }
}
