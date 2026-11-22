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
        readonly List<(SpriteRenderer renderer, bool flax, int offset, int planted)> _fields = new();
        readonly HashSet<LivestockPen> _visiblePens = new();
        readonly List<(SpriteRenderer renderer, LivestockPen pen, int index, int level, Sprite standing, Sprite stepping)> _livestock = new();

        WallArt _walls;
        CampArt _campArt;

        void OnDestroy() { _crops?.Dispose(); _walls?.Dispose(); _campArt?.Dispose(); }

        public void Init(ArtLibrary art, Material mat) { _art = art; _mat = mat; _crops = new FarmCropArt(); }

        /// <summary>Build the sprites for the block of view tiles `win` only (the map is 256 x 256: the part near the camera is enough).</summary>
        public void Build(World w, int rot, RectInt win)
        {
            _used = 0;
            _fields.Clear();
            _visiblePens.Clear();
            _livestock.Clear();
            _cropDay = w.Day;
            var fields = w.FarmFields();
            for (int vy = win.yMin; vy < win.yMax; vy++)
                for (int vx = win.xMin; vx < win.xMax; vx++)
                {
                    var wt = IsoView.FromView(vx, vy, rot); int x = wt.x, y = wt.y;
                    if (!w.InPlay(x, y)) continue;
                    var b = w.Grid[y, x];
                    if (b != null)
                    {
                        if (b.T != "road" && b.X == x && b.Y == y) PlaceBuilding(w, b, rot);
                        continue;
                    }
                    if (w.PenAt(x, y) is LivestockPen pen)
                    {
                        PlacePenTile(w, pen, x, y, vx, vy, rot);
                        _visiblePens.Add(pen);
                        continue;
                    }
                    if (fields.ContainsKey(World.Tk(x, y)))
                    {
                        int key = World.Tk(x, y);
                        int planted = w.SownOn(key);
                        if (w.SeasonalFarming && (planted == 0 || planted < w.Day - w.YearDay + 1 || w.YearDay > 180)) continue;
                        int offset = FarmCropArt.GrowthOffset(x, y, w.Seed);
                        bool flax = w.IsFlaxField(x, y);
                        if (w.SeasonalFarming) flax = w.PlantedFlax(key);
                        var plant = w.SeasonalFarming ? _crops.SeasonalTile(flax, w.Day - planted) : _crops.Tile(flax, w.Day, offset);
                        var tile = IsoView.ToView(x, y, rot);
                        if (plant != null)
                        {
                            int baseHeight = IsoView.VhView(w, tile.x + 1, tile.y + 1, rot);
                            var renderer = Put(plant, IsoView.VertexPos(tile.x + 1, tile.y + 1, baseHeight),
                                IsoView.SortOrder(tile.x, tile.y));
                            _fields.Add((renderer, flax, offset, w.SeasonalFarming ? planted : 0));
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
            foreach (var pen in _visiblePens)
            {
                if (pen.Count == 0) continue;
                int level = w.Vh(pen.Left, pen.Top);
                var standing = _art.SettlementWork.Animal(pen.Species);
                var stepping = _art.SettlementWork.Animal(pen.Species, true);
                for (int i = 0; i < pen.Count; i++)
                    _livestock.Add((Put(standing, Vector3.zero, 0), pen, i, level, standing, stepping));
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

        void PlacePenTile(World w, LivestockPen pen, int x, int y, int vx, int vy, int rotation)
        {
            var anchor = IsoView.VertexPos(vx + 1, vy + 1, w.Vh(x, y));
            int sort = IsoView.SortOrder(vx, vy);
            int edges = 0;
            for (int edge = 0; edge < 4; edge++)
            {
                var other = IsoView.FromView(vx + (edge == 1 ? 1 : edge == 3 ? -1 : 0),
                    vy + (edge == 2 ? 1 : edge == 0 ? -1 : 0), rotation);
                if (w.PenAt(other.x, other.y) != pen) edges |= 1 << edge;
            }
            if ((edges & 9) != 0) Put(_art.SettlementWork.Fence(edges & 9), anchor, sort - 1);
            if ((edges & 6) != 0) Put(_art.SettlementWork.Fence(edges & 6), anchor, sort + 1);
        }

        public void AnimateLivestock(int day, float fraction, int rotation)
        {
            foreach (var animal in _livestock)
            {
                var (pos, order, facesLeft, step) = LivestockMotion.Pose(animal.pen, animal.index, animal.level, day, fraction, rotation);
                var renderer = animal.renderer;
                renderer.transform.localPosition = pos;
                renderer.sortingOrder = order;
                renderer.flipX = facesLeft;
                renderer.sprite = step ? animal.stepping : animal.standing;
            }
        }

        void PlaceBuilding(World w, Building b, int rot)
        {
            if (b.T == "wall") { PlaceWall(w, b, rot); return; }
            if (b.T == "tent" || b.T == "supplies" || b.T == "chieftent" || b.T == "campfire" || b.T == "rack") { PlaceCamp(w, b, rot); return; }
            // the footprint's front tile in this view: its bottom corner is the sprite's anchor
            Vector2Int front = new Vector2Int(-1, -1); int best = -1;
            foreach (var (tx, ty) in b.Tiles()) { var v = IsoView.ToView(tx, ty, rot); if (v.x + v.y > best) { best = v.x + v.y; front = v; } }
            int view = ((rot + Mathf.Max(0, b.Face)) % 4 + 4) % 4;
            string slot = SlotName(b);
            // level 2 huts (watered, World.Water.cs) keep their own hut look until real house_2 art exists, rather than the placeholder block
            if (slot == "house_2" && _art.Find("house_2_1") == null && _art.Find("house_2") == null)
                slot = b.Var > 1 ? "house_1_v" + b.Var : "house_1";
            var sprite = _art.Building(slot, view);
            if (sprite == null) return;
            int lvl = IsoView.VhView(w, front.x + 1, front.y + 1, rot);
            Put(sprite, IsoView.VertexPos(front.x + 1, front.y + 1, lvl), IsoView.SortOrder(front.x, front.y));
        }

        /// <summary>A one-tile camp prop, anchored at the front corner of its tile.</summary>
        void PlaceCamp(World w, Building b, int rot)
        {
            _campArt ??= new CampArt();
            var here = IsoView.ToView(b.X, b.Y, rot);
            float lvl = Mathf.Round((float)w.TileLevel(b.X, b.Y) * LvlPx) / LvlPx;
            var sprite = b.T switch
            {
                "tent" => _campArt.Tent(rot),
                "chieftent" => _campArt.ChiefTent(rot),
                "campfire" => _campArt.Fire(),
                "rack" => _campArt.Rack(rot),
                _ => _campArt.Supplies((Res)b.Var),
            };
            Put(sprite, IsoView.VertexPos(here.x + 1, here.y + 1, lvl), IsoView.SortOrder(here.x, here.y));
        }

        /// <summary>A palisade tile: a thin line of stakes joined to the palisade tiles beside it.</summary>
        void PlaceWall(World w, Building b, int rot)
        {
            _walls ??= new WallArt();
            var here = IsoView.ToView(b.X, b.Y, rot);
            int mask = 0;
            for (int k = 0; k < 4; k++)
            {
                int nx = b.X + Defs.Nb[k, 0], ny = b.Y + Defs.Nb[k, 1];
                if (!World.Inb(nx, ny) || w.Grid[ny, nx]?.T != "wall") continue;
                var there = IsoView.ToView(nx, ny, rot);
                int dx = there.x - here.x, dy = there.y - here.y;
                mask |= dx == 1 ? WallArt.E : dx == -1 ? WallArt.W : dy == 1 ? WallArt.S : WallArt.N;
            }
            float lvl = Mathf.Round((float)w.TileLevel(b.X, b.Y) * LvlPx) / LvlPx;
            Put(_walls.Get(mask), IsoView.VertexPos(here.x + 1, here.y + 1, lvl), IsoView.SortOrder(here.x, here.y));
        }

        /// <summary>The art slot for a building (JS slotName + the hut-variant rule).</summary>
        public static string SlotName(Building b)
        {
            if (b.T == "hunter" && b.Sz == 1) return "wood";   // pre-release hunting saves
            if (b.Sz == 2)
            {
                if (b.T == "house") return "manor_" + b.Lvl;
                if (b.T == "wood" || b.T == "mine" || b.T == "quarry" || b.T == "forester" || b.T == "sawmill") return b.T + "2";
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
                field.renderer.sprite = field.planted > 0
                    ? (day - 1) % World.YearLength < 2 * World.SeasonLength
                        ? _crops.SeasonalTile(field.flax, day - field.planted)
                        : null
                    : _crops.Tile(field.flax, day, field.offset);
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
            r.flipX = false;
            r.transform.localPosition = pos;
            return r;
        }
    }
}
