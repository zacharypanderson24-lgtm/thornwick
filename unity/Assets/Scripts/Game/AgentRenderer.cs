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
        BattleArt _battle;
        readonly List<SpriteRenderer> _pool = new List<SpriteRenderer>();
        int _used;

        public void Init(Material mat) { _mat = mat; MakeSprites(); _battle = new BattleArt(); }

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
                if (w.Raid != null) DrawBattle(w, w.Raid, rot, frac);
                foreach (var v in w.Folk)
                {
                    if (v.St == FolkState.Home) continue;
                    int frame = v.St == FolkState.Walk || v.St == FolkState.Back ? (v.I + v.Id) & 1 : 0;
                    Put(w, rot, _folk[v.V, frame], v.Px, v.Py, v.X, v.Y, frac, (v.Id * 3) % 5 - 2, (v.Id * 7) % 3 - 1);
                }
            }
            for (int i = _used; i < _pool.Count; i++) if (_pool[i].enabled) _pool[i].enabled = false;
        }

        void DrawBattle(World w, RaidReport raid, int rot, float frac)
        {
            foreach (var a in raid.Raiders)
            {
                if (a.St == RaiderState.Waiting || a.St == RaiderState.Gone) continue;
                int x = a.X, y = a.Y, px = a.Px, py = a.Py;
                bool moving = x != px || y != py;
                int tx = a.Opponent?.X ?? a.Target?.X ?? x, ty = a.Opponent?.Y ?? a.Target?.Y ?? y;
                int facing = Facing(rot, moving ? x - px : tx - x, moving ? y - py : ty - y);
                bool dead = a.St == RaiderState.Dead;
                if (dead && w.Day - a.DeadDay > World.BodyDays) continue;
                bool recoiling = a.Hurt && frac < 0.30f;
                string pose = dead ? "fall" : recoiling ? "hurt" : a.Fighting ? "attack" : a.St == RaiderState.Loot ? "loot" :
                    a.St == RaiderState.Flee ? (a.Carry > 0 ? "carry" : "run") : moving ? "walk" : "idle";
                float phase = dead ? w.Day - a.DeadDay - 1 + frac : HitPhase(a.Hurt, frac);
                Put(w, rot, _battle.Pose("raider", pose, facing, w.Day, phase, a.Id),
                    dead ? x : px, dead ? y : py, x, y, frac, Jitter(a.Id), (a.Id * 7) % 3 - 1, true);
                if (!dead) DrawBattleEffects(w, rot, px, py, x, y, frac, a.Hurt, pose == "run" || pose == "carry");
            }

            foreach (var f in raid.Fighters)
            {
                if (f.St == FighterState.Idle) continue;
                bool dead = f.St == FighterState.Dead;
                if (dead && w.Day - f.DeadDay > World.BodyDays) continue;
                bool moving = f.X != f.Px || f.Y != f.Py;
                int tx = f.Shooting ? f.ShotX : f.Target?.X ?? f.X, ty = f.Shooting ? f.ShotY : f.Target?.Y ?? f.Y;
                int facing = Facing(rot, moving ? f.X - f.Px : tx - f.X, moving ? f.Y - f.Py : ty - f.Y);
                string actor = f.Kind == Troop.Archer ? "archer" : "infantry";
                bool recoiling = f.Hurt && !f.Shooting && frac < 0.30f;
                string pose = dead ? "fall" : f.Shooting ? "attack" : recoiling ? "hurt" : f.Attacked ? "attack" :
                    moving ? (f.St == FighterState.Engage ? "run" : "walk") : "idle";
                float phase = dead ? w.Day - f.DeadDay - 1 + frac : HitPhase(f.Hurt && !f.Shooting, frac);
                Put(w, rot, _battle.Pose(actor, pose, facing, w.Day, phase, f.Id),
                    dead ? f.X : f.Px, dead ? f.Y : f.Py, f.X, f.Y, frac, Jitter(f.Id), (f.Id * 7) % 3 - 1, true);
                if (f.Shooting && !dead) DrawArrow(w, rot, f.X, f.Y, f.ShotX, f.ShotY, frac);
                if (!dead) DrawBattleEffects(w, rot, f.Px, f.Py, f.X, f.Y, frac, f.Hurt, pose == "run");
            }

            if (!w.MilitiaFights) return;
            foreach (var (trade, x, y, target) in raid.MilitiaActions)
            {
                string actor = trade switch
                {
                    "farm" => "militia_farmer",
                    "hunter" => "militia_hunter",
                    "wood" => "militia_woodcutter",
                    "forester" => "militia_forester",
                    "sawmill" => "militia_sawyer",
                    "quarry" => "militia_quarry",
                    "mine" => "militia_miner",
                    _ => null
                };
                if (actor == null) continue;
                int facing = Facing(rot, target.X - x, target.Y - y);
                Put(w, rot, _battle.Pose(actor, "attack", facing, w.Day, frac, x + y), x, y, x, y, 0, 0, 0, true);
                if (trade == "hunter") DrawArrow(w, rot, x, y, target.X, target.Y, frac);
            }
        }

        static float HitPhase(bool hurt, float fraction) => !hurt ? fraction :
            fraction < 0.30f ? fraction / 0.30f : (fraction - 0.30f) / 0.70f;

        static int Jitter(int id) => (id * 3) % 5 - 2;

        static (int x, int y) ScreenDelta(int rot, int x, int y)
        {
            var (u, v) = rot switch { 1 => (-y, x), 2 => (-x, -y), 3 => (y, -x), _ => (x, y) };
            return ((u - v) * 16, (u + v) * 8);
        }

        static int Facing(int rot, int x, int y)
        {
            var (sx, sy) = ScreenDelta(rot, x, y);
            return (sy < 0 ? 2 : 0) + (sx < 0 ? 1 : 0);
        }

        void DrawArrow(World w, int rot, int x, int y, int targetX, int targetY, float frac)
        {
            const float release = 0.54f, impact = 0.89f;
            if (frac < release || frac >= impact) return;
            float flight = (frac - release) / (impact - release);
            var (sx, sy) = ScreenDelta(rot, targetX - x, targetY - y);
            sy -= Mathf.RoundToInt(((float)w.TileLevel(targetX, targetY) - (float)w.TileLevel(x, y)) * LvlPx);
            int direction = (Mathf.RoundToInt(Mathf.Atan2(sy, sx) / (Mathf.PI / 4f)) + 8) % 8;
            int arc = Mathf.RoundToInt(4f * flight * (1f - flight));
            Put(w, rot, _battle.Fx("arrow", direction), x, y, targetX, targetY, flight, 0, -9 - arc, true);
        }

        void DrawBattleEffects(World w, int rot, int x, int y, int targetX, int targetY, float frac, bool hurt, bool running)
        {
            if (hurt && frac < 0.4f)
                Put(w, rot, _battle.Fx("hit", (int)(frac * 10)), x, y, targetX, targetY, frac, 4, -12, true);
            else if (running && (x != targetX || y != targetY) && frac < 0.35f)
                Put(w, rot, _battle.Fx("dust", (int)(frac * 14)), x, y, x, y, 0, 0, 0, true);
        }

        void Put(World w, int rot, Sprite s, int px, int py, int x, int y, float frac, int jit, int jy, bool authoredFacing = false)
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
            r.flipX = !authoredFacing && screenStep < 0;
            r.sprite = s; r.color = Color.white; r.sortingOrder = IsoView.SortOrder(near.x, near.y); r.enabled = true;
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
            _battle?.Dispose();
            if (_worker == null) return;
            foreach (var sprite in _worker) Destroy(sprite);
            foreach (var sprite in _folk) Destroy(sprite);
        }
    }
}
