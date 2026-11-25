// Work crews and townsfolk on the map (JS draw(): the crewAt buckets, SPR.worker and SPR.folk).
// Civilians and combatants share the same small, directional sprite proportions.
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
        static readonly string[] FolkSprites = { "civilian_0", "civilian_1", "civilian_2", "civilian_3", "civilian_4", "civilian_5" };
        BattleArt _battle;
        readonly List<SpriteRenderer> _pool = new List<SpriteRenderer>();
        int _used;
        readonly BattleSpread _spread = new BattleSpread();   // moves fighters apart inside their tile so a fight is a crowd

        public void Init(Material mat) { _mat = mat; _battle = new BattleArt(); }

        /// <summary>Place every crew worker and villager who is out. frac = how far through the current day (0..1).</summary>
        public void Draw(World w, int rot, float frac, bool lod)
        {
            _used = 0;
            if (!lod && _battle != null)
            {
                DrawPeople(w, rot, frac);
                if (w.Raid != null) DrawBattle(w, w.Raid, rot, frac);
            }
            for (int i = _used; i < _pool.Count; i++) if (_pool[i].enabled) _pool[i].enabled = false;
        }

        /// <summary>A world's work crews who are out and its townsfolk walking about: your town, or an NPC settlement's town.</summary>
        void DrawPeople(World w, int rot, float frac)
        {
            {
                foreach (var b in w.Blds)
                {
                    if (b.Crew.Count == 0) continue;
                    string actor = TradeSprite(b.T);
                    if (actor == null) continue;
                    for (int i = 0; i < b.Crew.Count; i++)
                    {
                        var a = b.Crew[i]; if (a.St == CrewState.Home || a.St == CrewState.Down) continue;   // underground miners are not drawn
                        if (a.St == CrewState.Shelter && a.X == a.Px && a.Y == a.Py) continue;   // hiding in the Town Hall (the day it runs in, it is still drawn)
                        int dx = a.X - a.Px, dy = a.Y - a.Py;
                        bool moving = dx != 0 || dy != 0;
                        bool working = a.St == CrewState.Work;
                        int facing = Facing(rot, moving || !working ? dx : a.Tx - a.X, moving || !working ? dy : a.Ty - a.Y);
                        string pose = a.St == CrewState.Back && a.Carry > 0 ? "carry" : moving ? "walk" : working ? "attack" : "idle";
                        Put(w, rot, _battle.Pose(actor, pose, facing, w.Day, frac, i), a.Px, a.Py, a.X, a.Y, frac, Jitter(i), 0);
                    }
                }
                foreach (var bt in w.Boats)   // fishing boats on the water (World.Boats.cs)
                {
                    int bdx = bt.X - bt.Px, bdy = bt.Y - bt.Py;
                    if (bdx == 0 && bdy == 0 && bt.Path.Count > 1) { int ni = Mathf.Min(bt.Path.Count - 1, bt.I + 1); bdx = bt.Path[ni] % N - bt.X; bdy = bt.Path[ni] / N - bt.Y; }
                    int bf = Facing(rot, bdx == 0 && bdy == 0 ? 1 : bdx, bdx == 0 && bdy == 0 ? 0 : bdy);
                    Put(w, rot, BoatArt.Get(bf, (w.Day + bt.Id) % 2), bt.Px, bt.Py, bt.X, bt.Y, frac, 0, 1);
                }
                foreach (var v in w.Folk)
                {
                    if (v.St == FolkState.Home || (v.St == FolkState.Shelter && v.X == v.Px && v.Y == v.Py)) continue;   // at home, or hiding in the Town Hall
                    int dx = v.X - v.Px, dy = v.Y - v.Py;
                    string pose = dx != 0 || dy != 0 ? "walk" : "idle";
                    Put(w, rot, _battle.Pose(FolkSprites[v.V], pose, Facing(rot, dx, dy), w.Day, frac, v.Id),
                        v.Px, v.Py, v.X, v.Y, frac, Jitter(v.Id), (v.Id * 7) % 3 - 1);
                }
            }
        }

        /// <summary>The soldiers and raiders of an invasion, drawn on the camp world with the same sprites and effects as a raid. Replaces the
        /// settlement agents. An NPC settlement's own workers and townsfolk go about their day around it (townFrac: how far through its day).</summary>
        public void DrawCamp(World cw, CampBattle battle, int rot, float frac, float townFrac = 0f)
        {
            _used = 0; _spread.Reset();
            if (_battle != null && battle != null && battle.Camp.IsTown) DrawPeople(cw, rot, townFrac);
            if (_battle != null && battle != null)
            {
                int ox = cw.CampX0, oy = cw.CampY0;
                foreach (var u in battle.Units)
                {
                    bool dead = !u.Alive;
                    if (u.Left || u.InBarracks) continue;   // walked off the map, or still inside its barracks
                    if (dead && battle.Tick - u.DeadTick > CampBattle.BodyTicks) continue;
                    int x = ox + u.X, y = oy + u.Y, px = ox + u.Px, py = oy + u.Py;
                    bool moving = x != px || y != py;
                    int tx = u.Shooting ? ox + u.ShotX : u.Target != null ? ox + u.Target.X : x, ty = u.Shooting ? oy + u.ShotY : u.Target != null ? oy + u.Target.Y : y;
                    int facing = Facing(rot, moving ? x - px : tx - x, moving ? y - py : ty - y);
                    // a settlement's defenders are soldiers like ours (archers, and infantry for the rest, the lord and his guard included)
                    // in their realm's colours; a raider camp's are raiders
                    bool townSoldier = battle.Camp.IsTown && !u.Attacker;
                    string actor = townSoldier ? (u.Kind == BKind.RaiderArcher ? "archer" : "infantry") : u.Kind switch
                    {
                        BKind.Infantry => "infantry",
                        BKind.Archer => "archer",
                        BKind.Brute => "raider_brute",
                        BKind.Chief => "raider_chief",
                        _ => "raider",
                    };
                    bool recoiling = u.Hurt && !u.Shooting && frac < 0.30f;
                    string pose = dead ? "fall" : recoiling ? "hurt" : u.Attacked ? "attack" : moving ? "run" : "idle";
                    float phase = dead ? battle.Tick - u.DeadTick - 1 + frac : HitPhase(u.Hurt && !u.Shooting, frac);
                    var sprite = townSoldier
                        ? _battle.PoseTinted(actor, WorldMapRenderer.RealmColor(battle.Camp.Town), pose, facing, battle.Tick, phase, u.Id)
                        : _battle.Pose(actor, pose, facing, battle.Tick, phase, u.Id);
                    var off = _spread.Place(u.Id, x, y, dead);
                    Put(cw, rot, sprite, dead ? x : px, dead ? y : py, x, y, frac, off.dx, off.dy);
                    if (u.Shooting && !dead) DrawArrow(cw, rot, x, y, ox + u.ShotX, oy + u.ShotY, frac);
                    if (!dead) DrawBattleEffects(cw, rot, px, py, x, y, frac, u.Hurt, pose == "run");
                }
            }
            for (int i = _used; i < _pool.Count; i++) if (_pool[i].enabled) _pool[i].enabled = false;
        }

        void DrawBattle(World w, RaidReport raid, int rot, float frac)
        {
            _spread.Reset();
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
                string pose = dead ? "fall" : recoiling ? "hurt" : a.Fighting ? "attack" : a.St == RaiderState.Loot ? (a.Rebel ? "attack" : "loot") :
                    a.St == RaiderState.Flee ? (a.Carry > 0 ? "carry" : "run") : moving ? "walk" : "idle";
                float phase = dead ? w.Day - a.DeadDay - 1 + frac : HitPhase(a.Hurt, frac);
                var off = _spread.Place(a.Id, x, y, dead);
                Put(w, rot, _battle.Pose(a.Rebel ? TradeSprite(a.Trade) ?? "raider" : "raider", pose, facing, w.Day, phase, a.Id),
                    dead ? x : px, dead ? y : py, x, y, frac, off.dx, off.dy);
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
                var off = _spread.Place(f.Id + 100000, f.X, f.Y, dead);   // soldiers and raiders count ids separately: keep their wobbles apart
                Put(w, rot, _battle.Pose(actor, pose, facing, w.Day, phase, f.Id),
                    dead ? f.X : f.Px, dead ? f.Y : f.Py, f.X, f.Y, frac, off.dx, off.dy);
                if (f.Shooting && !dead) DrawArrow(w, rot, f.X, f.Y, f.ShotX, f.ShotY, frac);
                if (!dead) DrawBattleEffects(w, rot, f.Px, f.Py, f.X, f.Y, frac, f.Hurt, pose == "run");
            }

            if (!w.MilitiaFights) return;
            foreach (var (trade, x, y, target) in raid.MilitiaActions)
            {
                string actor = TradeSprite(trade);
                if (actor == null) continue;
                int facing = Facing(rot, target.X - x, target.Y - y);
                Put(w, rot, _battle.Pose(actor, "attack", facing, w.Day, frac, x + y), x, y, x, y, 0, 0, 0);
                if (trade == "hunter") DrawArrow(w, rot, x, y, target.X, target.Y, frac);
            }
        }

        static string TradeSprite(string trade) => trade switch
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
            Put(w, rot, _battle.Fx("arrow", direction), x, y, targetX, targetY, flight, 0, -5 - arc);
        }

        void DrawBattleEffects(World w, int rot, int x, int y, int targetX, int targetY, float frac, bool hurt, bool running)
        {
            if (hurt && frac < 0.4f)
                Put(w, rot, _battle.Fx("hit", (int)(frac * 10)), x, y, targetX, targetY, frac, 2, -6);
            else if (running && (x != targetX || y != targetY) && frac < 0.35f)
                Put(w, rot, _battle.Fx("dust", (int)(frac * 14)), x, y, x, y, 0, 0, 0);
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
            r.sprite = s; r.color = Color.white; r.sortingOrder = IsoView.SortOrder(near.x, near.y); r.enabled = true;
            // same painter's slot as the tile's tree or building; a hair nearer the camera so the walker draws after it (JS draws crews last per tile)
            r.transform.localPosition = new Vector3(sx / IsoView.PPU, -sy / IsoView.PPU, -0.01f);
        }

        void OnDestroy() => _battle?.Dispose();
    }
}
