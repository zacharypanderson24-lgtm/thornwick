// The map lenses beyond the survival overlays: which tiles to colour, and how, for Workers, Food sources, Trade, Danger and Military reach.
// Pure C# (no UnityEngine): the game turns each tone into a colour (GameController.DrawChosenOverlay). Tested directly.
using System.Collections.Generic;
using System.Linq;
using CrownAndHearth.Sim;

namespace CrownAndHearth.Game.UI
{
    public enum LensTone : byte { Good, Warn, Bad, Info, Soft }

    public static class LensData
    {
        /// <summary>The tiles to paint for a lens, with the tone of each (empty for an overlay that is not one of these lenses).</summary>
        public static List<(int x, int y, LensTone tone)> Tiles(World w, OverlayId id)
        {
            var list = new List<(int, int, LensTone)>();
            switch (id)
            {
                case OverlayId.Workers:
                    foreach (var b in w.Blds.Where(b => b.Jobs > 0))
                    {
                        var tone = !b.Served ? LensTone.Bad : b.Workers >= b.Jobs ? LensTone.Good : b.Workers > 0 ? LensTone.Warn : LensTone.Bad;
                        foreach (var (x, y) in b.Tiles()) list.Add((x, y, tone));
                    }
                    break;
                case OverlayId.Food:
                    for (int y = 0; y < Defs.N; y++) for (int x = 0; x < Defs.N; x++)
                        if (w.Grid[y, x] == null && w.ZoneMap[y, x] == Zone.Farm) list.Add((x, y, LensTone.Soft));
                    foreach (var b in w.Blds.Where(b => b.T == "farm" || b.T == "hunter" || b.T == "fisher" || b.T == "dock" || b.T == "silo" || b.T == "smokehouse"))
                        foreach (var (x, y) in b.Tiles()) list.Add((x, y, b.Served ? LensTone.Good : LensTone.Bad));
                    break;
                case OverlayId.Trade:
                    foreach (var b in w.Blds)
                    {
                        if (b.T == "market" || b.T == "port" || b.T == "storehouse") foreach (var (x, y) in b.Tiles()) list.Add((x, y, b.Served ? LensTone.Good : LensTone.Bad));
                        else if (b.T == "road" && b.Served && w.Markets > 0) list.Add((b.X, b.Y, LensTone.Info));
                    }
                    break;
                case OverlayId.Military:
                    foreach (var b in w.Blds.Where(b => b.T == "watchtower" || b.T == "barracks" || b.T == "muster"))
                    {
                        foreach (var (x, y) in b.Tiles()) list.Add((x, y, LensTone.Good));
                        if (b.T != "watchtower") continue;
                        int cx = b.X, cy = b.Y, r = World.WatchtowerRange;
                        for (int dy = -r; dy <= r; dy++) for (int dx = -r; dx <= r; dx++)
                        {
                            int d2 = dx * dx + dy * dy;
                            if (d2 > r * r || d2 < (r - 1) * (r - 1)) continue;   // the ring only
                            if (World.Inb(cx + dx, cy + dy)) list.Add((cx + dx, cy + dy, LensTone.Soft));
                        }
                    }
                    break;
                case OverlayId.Danger:
                    {
                        int x0 = Defs.N, y0 = Defs.N, x1 = 0, y1 = 0;
                        for (int y = 0; y < Defs.N; y++) for (int x = 0; x < Defs.N; x++)
                            if (w.Claimed(x, y)) { if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y; }
                        if (x1 < x0) break;
                        var sides = new HashSet<string>(w.LiveCamps.Select(c => w.CampSideName(c)));
                        const int band = 3;
                        for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++)
                        {
                            bool hit = (sides.Contains("north") && y < y0 + band) || (sides.Contains("south") && y > y1 - band)
                                    || (sides.Contains("west") && x < x0 + band) || (sides.Contains("east") && x > x1 - band);
                            if (hit && w.Grid[y, x] == null) list.Add((x, y, LensTone.Bad));
                        }
                        foreach (var b in w.Blds.Where(b => b.T == "wall" || b.T == "watchtower")) foreach (var (x, y) in b.Tiles()) list.Add((x, y, LensTone.Good));
                        break;
                    }
            }
            return list;
        }

        /// <summary>What the colours mean, in one line.</summary>
        public static string Legend(OverlayId id) => id switch
        {
            OverlayId.Workers => "Workers: green = fully staffed, amber = short of workers, red = nobody or no road to the Hall.",
            OverlayId.Food => "Food sources: green = working food building, red = no road, yellow = Farmland zone waiting for a farmhouse.",
            OverlayId.Trade => "Trade: green = market, port or storehouse on a road, red = not connected, blue = roads caravans can use.",
            OverlayId.Military => "Military reach: green = barracks, muster ground and towers, pale ring = how far a watchtower sees.",
            OverlayId.Danger => "Danger: red band = the edge raiders come from (their camps lie that way), green = your walls and towers.",
            _ => "",
        };

        public static bool IsLens(OverlayId id) => id == OverlayId.Workers || id == OverlayId.Food || id == OverlayId.Trade || id == OverlayId.Military || id == OverlayId.Danger;
    }
}
