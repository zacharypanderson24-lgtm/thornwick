// Draws a raider camp (Sim/CampBattle.cs) into a texture: a top-down view of the 32 x 32 camp map, 14 pixels a tile, with tents, supply piles,
// trees and rocks, the planned landings, and every soldier and raider as a small figure (lerped between ticks, flashing red when hit,
// arrows drawn as lines, the fallen lying down for a few ticks). The planning screen and the battle share it. Also the little component that
// turns a click or a hover over that picture into a tile.
using System;
using CrownAndHearth.Sim;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CrownAndHearth.Game
{
    public static class CampPainter
    {
        public const int Tile = 14, Size = CampMap.Size * Tile;

        static readonly Color32 Clear = new Color32(0, 0, 0, 0);

        public static Texture2D NewTexture() => new Texture2D(Size, Size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "camp" };

        public static Color32 ResourceColour(Res r) => r switch
        {
            Res.Food => new Color32(226, 196, 84, 255), Res.Wood => new Color32(190, 140, 80, 255), Res.Logs => new Color32(120, 84, 48, 255),
            Res.Stone => new Color32(160, 160, 166, 255), Res.Ore => new Color32(214, 120, 60, 255), Res.Gold => new Color32(255, 214, 64, 255),
            _ => new Color32(120, 150, 230, 255),
        };

        public static string TileName(int x, int y) => y == 0 ? "north edge" : y == CampMap.Size - 1 ? "south edge" : x == 0 ? "west edge" : "east edge";

        /// <summary>The tile under a pointer, from the picture's rectangle (tile y = 0 at the top).</summary>
        public static bool TileAt(RectTransform picture, Vector2 screen, out int x, out int y)
        {
            x = y = 0;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(picture, screen, null, out var local)) return false;
            var r = picture.rect;
            float u = (local.x - r.xMin) / r.width, v = (local.y - r.yMin) / r.height;
            if (u < 0 || u >= 1 || v < 0 || v >= 1) return false;
            x = Mathf.Clamp(Mathf.FloorToInt(u * CampMap.Size), 0, CampMap.Size - 1);
            y = Mathf.Clamp(CampMap.Size - 1 - Mathf.FloorToInt(v * CampMap.Size), 0, CampMap.Size - 1);
            return true;
        }

        /// <summary>Paint the whole camp. frac is how far through the current tick (0..1); plan lists the landings being planned; selected and hover outline a tile.</summary>
        public static void Paint(Texture2D tex, Color32[] px, CampBattle b, float frac, System.Collections.Generic.IList<BattleGroup> plan, (int x, int y)? selected, (int x, int y)? hover)
        {
            var map = b.Map;
            for (int ty = 0; ty < CampMap.Size; ty++)
                for (int tx = 0; tx < CampMap.Size; tx++)
                {
                    double h = Noise.Hash2(tx, ty, 41);
                    byte g = (byte)(96 + h * 14), r = (byte)(86 + h * 10), bl = (byte)(58 + h * 8);
                    if (CampMap.IsEdge(tx, ty)) { r = (byte)(r + 18); g = (byte)(g + 14); bl = (byte)(bl + 26); }   // the landing edge is a little lighter
                    FillTile(px, tx, ty, new Color32(r, g, bl, 255));
                    if (h > 0.8) Dot(px, tx * Tile + (int)(h * 1000) % 10 + 2, ty * Tile + (int)(h * 100000) % 10 + 2, new Color32((byte)(r - 10), (byte)(g + 12), (byte)(bl - 4), 255));
                    switch (map.Ground[ty, tx])
                    {
                        case CampTile.Tree: Tree(px, tx, ty); break;
                        case CampTile.Rock: Rock(px, tx, ty); break;
                    }
                }
            foreach (var t in map.Tents) Tent(px, t.x, t.y);
            foreach (var p in map.Piles) Pile(px, p);
            if (plan != null)
                foreach (var g in plan) Marker(px, g.X, g.Y, new Color32(80, 140, 255, 255), g.Total > 0 ? 3 : 2);
            if (selected.HasValue) Outline(px, selected.Value.x, selected.Value.y, new Color32(255, 230, 90, 255));
            if (hover.HasValue) Outline(px, hover.Value.x, hover.Value.y, new Color32(255, 255, 255, 200));

            // bodies first, then the living from the back of the picture to the front
            foreach (var u in b.Units) if (!u.Alive && b.Tick - u.DeadTick <= CampBattle.BodyTicks) Body(px, u);
            var alive = new System.Collections.Generic.List<BUnit>();
            foreach (var u in b.Units) if (u.Alive) alive.Add(u);
            alive.Sort((a, c) => (a.Py + (a.Y - a.Py) * frac).CompareTo(c.Py + (c.Y - c.Py) * frac));
            foreach (var u in alive) if (u.Shooting && frac > 0.15f) Line(px, u, new Color32(255, 240, 160, 255));
            foreach (var u in alive) Figure(px, u, frac);
            tex.SetPixels32(px); tex.Apply();
        }

        // ---------- pixels ----------
        static void Set(Color32[] px, int x, int y, Color32 c)
        {
            if (x < 0 || y < 0 || x >= Size || y >= Size) return;
            px[(Size - 1 - y) * Size + x] = c;   // y = 0 is the top of the picture
        }

        static void Rect(Color32[] px, int x0, int y0, int w, int h, Color32 c) { for (int y = y0; y < y0 + h; y++) for (int x = x0; x < x0 + w; x++) Set(px, x, y, c); }
        static void Dot(Color32[] px, int x, int y, Color32 c) => Set(px, x, y, c);
        static void FillTile(Color32[] px, int tx, int ty, Color32 c) => Rect(px, tx * Tile, ty * Tile, Tile, Tile, c);

        static void Outline(Color32[] px, int tx, int ty, Color32 c)
        {
            int x0 = tx * Tile, y0 = ty * Tile;
            for (int i = 0; i < Tile; i++) { Set(px, x0 + i, y0, c); Set(px, x0 + i, y0 + Tile - 1, c); Set(px, x0, y0 + i, c); Set(px, x0 + Tile - 1, y0 + i, c); }
        }

        static void Marker(Color32[] px, int tx, int ty, Color32 c, int inset)
        {
            Rect(px, tx * Tile + inset, ty * Tile + inset, Tile - 2 * inset, Tile - 2 * inset, c);
            Outline(px, tx, ty, new Color32(255, 255, 255, 230));
        }

        static void Tree(Color32[] px, int tx, int ty)
        {
            int cx = tx * Tile + Tile / 2, cy = ty * Tile + Tile / 2 - 1;
            Rect(px, cx - 1, cy + 3, 3, 4, new Color32(88, 60, 36, 255));
            for (int dy = -5; dy <= 4; dy++) for (int dx = -5; dx <= 5; dx++)
                if (dx * dx + dy * dy * 1.2f <= 26) Set(px, cx + dx, cy + dy, (dx + dy) % 3 == 0 ? new Color32(46, 110, 52, 255) : new Color32(34, 88, 42, 255));
        }

        static void Rock(Color32[] px, int tx, int ty)
        {
            int cx = tx * Tile + Tile / 2, cy = ty * Tile + Tile / 2 + 1;
            for (int dy = -3; dy <= 3; dy++) for (int dx = -5; dx <= 5; dx++)
                if (dx * dx / 1.7f + dy * dy * 1.6f <= 14) Set(px, cx + dx, cy + dy, dy < 0 ? new Color32(150, 150, 154, 255) : new Color32(112, 112, 118, 255));
        }

        static void Tent(Color32[] px, int tx, int ty)
        {
            int x0 = tx * Tile, y0 = ty * Tile;
            for (int row = 0; row < 11; row++)
            {
                int half = 1 + row / 2 + (row > 4 ? 1 : 0);
                for (int dx = -half; dx <= half; dx++)
                    Set(px, x0 + Tile / 2 + dx, y0 + 2 + row, dx < 0 ? new Color32(190, 58, 48, 255) : new Color32(142, 40, 36, 255));
            }
            Rect(px, x0 + Tile / 2 - 1, y0 + 8, 3, 5, new Color32(40, 20, 20, 255));   // the doorway
            Set(px, x0 + Tile / 2, y0 + 1, new Color32(60, 40, 30, 255));
        }

        static void Pile(Color32[] px, SupplyPile p)
        {
            if (p.Amount <= 0) return;
            int x0 = p.X * Tile, y0 = p.Y * Tile;
            for (int dy = 0; dy < 7; dy++) for (int dx = -5; dx <= 5; dx++)
                if (dx * dx / 3.3f + (dy - 6) * (dy - 6) * 0.9f <= 7) Set(px, x0 + Tile / 2 + dx, y0 + 5 + dy, new Color32(130, 100, 62, 255));
            var c = ResourceColour(p.Res);
            Rect(px, x0 + Tile / 2 - 2, y0 + 5, 5, 4, c);
            Rect(px, x0 + Tile / 2 - 1, y0 + 4, 3, 1, c);
        }

        static (Color32 body, Color32 head, Color32 accent) Palette(BKind k) => k switch
        {
            BKind.Infantry => (new Color32(58, 100, 190, 255), new Color32(190, 194, 204, 255), new Color32(215, 215, 225, 255)),
            BKind.Archer => (new Color32(60, 140, 70, 255), new Color32(224, 184, 138, 255), new Color32(130, 90, 45, 255)),
            BKind.Raider => (new Color32(170, 50, 44, 255), new Color32(224, 184, 138, 255), new Color32(190, 190, 196, 255)),
            _ => (new Color32(120, 36, 36, 255), new Color32(224, 184, 138, 255), new Color32(230, 150, 60, 255)),
        };

        static void Figure(Color32[] px, BUnit u, float frac)
        {
            float wx = u.Px + (u.X - u.Px) * frac, wy = u.Py + (u.Y - u.Py) * frac;
            int cx = Mathf.RoundToInt(wx * Tile + Tile / 2f), by = Mathf.RoundToInt(wy * Tile + Tile - 2);   // feet near the bottom of the tile
            var (body, head, accent) = Palette(u.Kind);
            if (u.Hurt && frac > 0.2f) { body = new Color32(255, 110, 100, 255); head = new Color32(255, 200, 190, 255); }
            Rect(px, cx - 2, by - 5, 5, 5, body);                 // body
            Rect(px, cx - 1, by - 8, 3, 3, head);                 // head
            Rect(px, cx - 2, by, 2, 2, new Color32(40, 32, 32, 255)); Rect(px, cx + 1, by, 2, 2, new Color32(40, 32, 32, 255));   // legs
            if (u.Ranged) { Rect(px, cx + 3, by - 8, 1, 7, accent); }      // a bow
            else { Rect(px, cx + 3, by - 9, 1, 6, accent); if (u.Attacked && frac > 0.4f) Rect(px, cx + 4, by - 10, 2, 2, new Color32(255, 240, 120, 255)); }   // a blade, a spark when it strikes
        }

        static void Body(Color32[] px, BUnit u)
        {
            int cx = u.X * Tile + Tile / 2, by = u.Y * Tile + Tile - 2;
            var (body, head, _) = Palette(u.Kind);
            body = new Color32((byte)(body.r / 2), (byte)(body.g / 2), (byte)(body.b / 2), 255);
            Rect(px, cx - 4, by - 2, 7, 3, body); Rect(px, cx + 3, by - 2, 2, 3, head);
        }

        static void Line(Color32[] px, BUnit u, Color32 c)
        {
            int x0 = u.X * Tile + Tile / 2, y0 = u.Y * Tile + Tile / 2 - 3, x1 = u.ShotX * Tile + Tile / 2, y1 = u.ShotY * Tile + Tile / 2 - 3;
            int dx = Math.Abs(x1 - x0), dy = -Math.Abs(y1 - y0), sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1, err = dx + dy;
            for (int guard = 0; guard < 2000; guard++)
            {
                if ((guard / 3) % 2 == 0) Set(px, x0, y0, c);
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }
    }

    /// <summary>Put on the camp picture: reports the tile under a click or the pointer.</summary>
    public sealed class CampGrid : MonoBehaviour, IPointerClickHandler, IPointerMoveHandler, IPointerExitHandler
    {
        public Action<int, int> Clicked;
        public Action<int, int> Hovered;
        public Action HoverEnded;
        RectTransform Rt => (RectTransform)transform;

        public void OnPointerClick(PointerEventData e) { if (CampPainter.TileAt(Rt, e.position, out int x, out int y)) Clicked?.Invoke(x, y); }
        public void OnPointerMove(PointerEventData e) { if (CampPainter.TileAt(Rt, e.position, out int x, out int y)) Hovered?.Invoke(x, y); else HoverEnded?.Invoke(); }
        public void OnPointerExit(PointerEventData e) => HoverEnded?.Invoke();
    }
}
