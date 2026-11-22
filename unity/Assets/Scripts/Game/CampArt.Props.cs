using UnityEngine;

namespace CrownAndHearth.Game
{
    public sealed partial class CampArt
    {
        static readonly Color32 Timber = new Color32(86, 57, 37, 255);
        static readonly Color32 TimberLight = new Color32(150, 106, 64, 255);
        static readonly Color32 Brass = new Color32(190, 139, 65, 255);
        static readonly Color32 BrassLight = new Color32(228, 183, 98, 255);

        static void DrawChiefTent(Color32[] px, int rotation)
        {
            bool mirror = (rotation & 1) != 0;
            bool entrance = rotation == 0 || rotation == 3;
            (int x, int y) P(int x, int y) => (CX + (mirror ? -x : x), y);
            var red = new Color32(149, 65, 52, 255);
            var shade = new Color32(103, 42, 40, 255);
            var fold = new Color32(177, 86, 64, 255);
            var seam = new Color32(72, 34, 33, 255);
            var opening = new Color32(42, 26, 23, 255);
            var gable = mirror ? shade : red;
            var roof = mirror ? red : shade;

            // Raised eaves make the command tent taller while its feet fit the same diamond.
            Quad(px, P(-14, 9), P(0, 2), P(0, 12), P(-14, 19), gable);
            Quad(px, P(0, 2), P(14, 9), P(14, 19), P(0, 12), roof);
            Triangle(px, P(-14, 19), P(0, 12), P(-7, 32), gable);
            Quad(px, P(-7, 32), P(7, 39), P(14, 19), P(0, 12), roof);
            Quad(px, P(-6, 30), P(7, 37), P(9, 31), P(-4, 24), mirror ? fold : red);
            Line(px, P(-3, 21), P(10, 27), mirror ? shade : gable);
            Line(px, P(-1, 16), P(12, 22), seam);
            Line(px, P(-14, 18), P(0, 11), seam);
            Line(px, P(0, 11), P(14, 18), seam);
            Line(px, P(-14, 19), P(0, 12), mirror ? Brass : BrassLight);
            Line(px, P(0, 12), P(14, 19), mirror ? BrassLight : Brass);
            Line(px, P(-13, 10), P(0, 3), seam);
            Line(px, P(0, 3), P(13, 10), seam);

            if (entrance)
            {
                Quad(px, P(-10, 8), P(-4, 5), P(-4, 16), P(-10, 19), opening);
                Triangle(px, P(-10, 19), P(-4, 16), P(-7, 24), opening);
                Line(px, P(-11, 9), P(-9, 19), Brass);
                Line(px, P(-7, 24), P(-4, 15), mirror ? Brass : BrassLight);
                Line(px, P(-10, 8), P(-4, 5), TimberLight);
            }
            else Line(px, P(-7, 6), P(-7, 25), seam);

            Line(px, P(-14, 9), P(-14, 17), Timber);
            Line(px, P(0, 2), P(0, 11), Timber);
            Line(px, P(14, 9), P(14, 17), Timber);
            Line(px, P(-7, 32), P(7, 39), TimberLight);
            var pole = entrance ? P(-7, 32) : P(7, 39);
            Rect(px, pole.x, pole.y, 1, 8, TimberLight);
            Quad(px, (pole.x + 1, pole.y + 7), (pole.x + 8, pole.y + 6),
                (pole.x + 7, pole.y + 2), (pole.x + 1, pole.y + 3), shade);
            Line(px, (pole.x + 1, pole.y + 7), (pole.x + 8, pole.y + 6), Brass);
            Rect(px, pole.x + 3, pole.y + 4, 2, 2, BrassLight);
        }

        static void DrawFire(Color32[] px)
        {
            var ash = new Color32(58, 44, 35, 255);
            var rock = new Color32(116, 104, 89, 255);
            var rockShade = new Color32(82, 75, 65, 255);
            var rockLight = new Color32(154, 139, 114, 255);
            var ember = new Color32(161, 59, 33, 255);
            var flame = new Color32(218, 96, 41, 255);
            var hot = new Color32(248, 162, 65, 255);
            var core = new Color32(255, 216, 116, 255);

            Quad(px, (6, 8), (15, 13), (26, 8), (16, 3), ash);
            foreach (var stone in new[] { (10, 10), (15, 12), (20, 11), (24, 9) })
                Cube(px, stone.Item1, stone.Item2, 2, 2, rockLight, rock, rockShade);
            Line(px, (10, 7), (21, 11), Timber);
            Line(px, (10, 8), (21, 12), TimberLight);
            Line(px, (11, 11), (21, 6), Timber);
            Line(px, (11, 12), (21, 7), ember);
            Triangle(px, (11, 8), (21, 9), (14, 21), flame);
            Triangle(px, (14, 8), (22, 11), (20, 17), ember);
            Triangle(px, (13, 9), (20, 10), (15, 18), hot);
            Triangle(px, (14, 9), (18, 10), (16, 15), core);
            Set(px, 18, 21, hot);
            Set(px, 12, 19, flame);
            foreach (var stone in new[] { (8, 7), (12, 5), (16, 3), (20, 5), (24, 7) })
                Cube(px, stone.Item1, stone.Item2, 2, 2, rock, rockShade, ash);
        }

        static void DrawRack(Color32[] px, bool mirror)
        {
            (int x, int y) P(int x, int y) => (CX + (mirror ? -x : x), y);
            var shadow = new Color32(56, 42, 32, 255);
            var iron = new Color32(128, 132, 129, 255);
            var edge = new Color32(180, 182, 169, 255);
            Line(px, P(-10, 5), P(-7, 7), shadow);
            Line(px, P(7, 10), P(10, 11), shadow);
            Line(px, P(-9, 5), P(-9, 20), Timber);
            Line(px, P(-8, 5), P(-8, 20), TimberLight);
            Line(px, P(8, 10), P(8, 28), Timber);
            Line(px, P(9, 11), P(9, 28), TimberLight);
            Line(px, P(-8, 9), P(8, 17), shadow);
            Line(px, P(-8, 17), P(8, 25), TimberLight);
            Line(px, P(-8, 16), P(8, 24), Timber);
            foreach (int x in new[] { -5, -1, 4 })
            {
                int y = 10 + x / 2;
                Line(px, P(x, y), P(x + 1, y + 17), TimberLight);
                Triangle(px, P(x, y + 16), P(x + 2, y + 17), P(x + 1, y + 21), iron);
                Line(px, P(x, y + 16), P(x + 1, y + 21), edge);
            }
            var shield = new Color32(128, 64, 48, 255);
            Quad(px, P(2, 19), P(7, 21), P(7, 14), P(4, 12), shadow);
            Quad(px, P(3, 19), P(7, 20), P(6, 15), P(4, 13), shield);
            var boss = P(5, 17);
            Set(px, boss.x, boss.y, edge);
        }

        static void DrawGold(Color32[] px)
        {
            var dark = new Color32(62, 43, 29, 255);
            var sack = new Color32(119, 92, 60, 255);
            var sackLight = new Color32(158, 129, 87, 255);
            Cube(px, 20, 9, 7, 7, TimberLight, Timber, dark);
            Line(px, (13, 16), (20, 12), Brass);
            Line(px, (20, 12), (27, 16), Brass);
            Rect(px, 19, 11, 2, 3, BrassLight);
            Line(px, (22, 13), (22, 8), TimberLight);
            Quad(px, (6, 8), (10, 13), (16, 9), (13, 4), sack);
            Triangle(px, (6, 8), (10, 13), (13, 10), sackLight);
            Line(px, (9, 12), (12, 13), dark);
            foreach (var pile in new[] { (12, 6), (16, 4), (19, 6) })
            {
                Cube(px, pile.Item1, pile.Item2, 3, 2, BrassLight, Brass, TimberLight);
                Set(px, pile.Item1 + 1, pile.Item2 + 2, new Color32(247, 207, 126, 255));
            }
        }
    }
}
