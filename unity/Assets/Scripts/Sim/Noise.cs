// Deterministic hash and value noise, ported bit-for-bit from the JavaScript prototype (src/sim.js: hash2, vnoise).
// Every world feature (heights, forests, ore, hut looks) is derived from these, so they must match the browser game exactly.
// SimParityTests checks them against values produced by the JS code.
using System;

namespace CrownAndHearth.Sim
{
    public static class Noise
    {
        /// <summary>JS: hash2(x, y, s). Integer hash of a lattice point, returned in [0, 1).</summary>
        public static double Hash2(int x, int y, int s)
        {
            unchecked
            {
                // Math.imul(x, a) + Math.imul(y, b) + Math.imul(s, c) | 0  ==  wrapping int32 arithmetic
                int h = x * 374761393 + y * 668265263 + s * 2147483647;
                h = (h ^ (int)((uint)h >> 13)) * 1274126177;
                return (uint)(h ^ (int)((uint)h >> 16)) / 4294967296.0;
            }
        }

        /// <summary>JS: vnoise(x, y, s). Smooth value noise.</summary>
        public static double VNoise(double x, double y, int s)
        {
            int xi = (int)Math.Floor(x), yi = (int)Math.Floor(y);
            double xf = x - xi, yf = y - yi;
            double a = Hash2(xi, yi, s), b = Hash2(xi + 1, yi, s), c = Hash2(xi, yi + 1, s), d = Hash2(xi + 1, yi + 1, s);
            double u = xf * xf * (3 - 2 * xf), v = yf * yf * (3 - 2 * yf);
            return a + (b - a) * u + (c - a) * v + (a - b - c + d) * u * v;
        }

        /// <summary>JS Math.round: halves round towards +infinity (C#'s Math.Round would round them to even).</summary>
        public static int JsRound(double v) => (int)Math.Floor(v + 0.5);

        /// <summary>JS Math.hypot for two arguments.</summary>
        public static double Hypot(double a, double b) => Math.Sqrt(a * a + b * b);
    }
}
