using System;
using CrownAndHearth.Sim;
using UnityEngine;

namespace CrownAndHearth.Game
{
    static class LivestockMotion
    {
        static readonly Vector3[] SmallLoop =
        {
            new(0, 0.075f), new(0.14f, 0), new(0, -0.075f), new(-0.14f, 0),
        };

        public static (Vector3 position, int order, bool facesLeft, bool step) Pose(
            LivestockPen pen, int animal, int level, int day, float fraction, int rotation)
        {
            // Divide a continuous serpentine path into disjoint stretches. At full capacity,
            // chickens still take a short loop inside their own tile.
            int start = (int)((long)animal * pen.Area / pen.Count);
            int length = (int)((long)(animal + 1) * pen.Area / pen.Count) - start;
            int period = length == 1 ? SmallLoop.Length : 2 * (length - 1);
            double pace = pen.Species == Zone.Chickens ? 1.15 : 0.8;
            double elapsed = ((day - 1d + fraction) * pace + animal * 0.61 +
                Noise.Hash2(pen.Left, pen.Top, 937) * period) % period;
            int leg = (int)elapsed;
            float phase = (float)(elapsed - leg);
            float travel = Mathf.Clamp01((phase - 0.14f) / 0.72f);
            bool step = travel > 0 && travel < 1 && ((int)(travel * 4) & 1) != 0;

            if (length == 1)
            {
                var tile = ViewTile(pen, start, rotation);
                var from = SmallLoop[(leg + rotation) % period];
                var to = SmallLoop[(leg + rotation + 1) % period];
                return (TileCenter(tile, level) + Vector3.Lerp(from, to, travel), IsoView.SortOrder(tile.x, tile.y), to.x < from.x, step);
            }

            int Offset(int i) => i < length ? i : period - i;
            var a = ViewTile(pen, start + Offset(leg), rotation);
            var b = ViewTile(pen, start + Offset((leg + 1) % period), rotation);
            var posA = TileCenter(a, level);
            var posB = TileCenter(b, level);
            var tileAtFeet = travel < 0.5f ? a : b;
            return (Vector3.Lerp(posA, posB, travel), IsoView.SortOrder(tileAtFeet.x, tileAtFeet.y), posB.x < posA.x, step);
        }

        static Vector2Int ViewTile(LivestockPen pen, int offset, int rotation)
        {
            int width = pen.Right - pen.Left + 1;
            int row = offset / width, col = offset % width;
            return IsoView.ToView(pen.Left + ((row & 1) == 0 ? col : width - 1 - col), pen.Top + row, rotation);
        }

        static Vector3 TileCenter(Vector2Int tile, int level) =>
            IsoView.VertexPos(tile.x, tile.y, level) + Vector3.down * (8f / IsoView.PPU);
    }
}
