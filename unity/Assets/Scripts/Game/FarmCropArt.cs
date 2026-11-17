using UnityEngine;

namespace CrownAndHearth.Game
{
    internal static class FarmCropArt
    {
        public const int CropCount = 3;
        public const int StageCount = 4;
        public const int DaysPerStage = 4;

        // Each palette replaces the same four crop marks in all views. The barn and soil are untouched.
        static readonly uint[] Marks = { 0xC7953F, 0xD9AF52, 0x929454, 0xE2B863 };
        static readonly uint[] Soil = { 0xA77536, 0xA77536, 0xA77536, 0xA77536 };
        static readonly uint[,] Sprouts =
        {
            { 0xA77536, 0x7C8E45, 0xA77536, 0x687B39 },
            { 0xA77536, 0x819950, 0xA77536, 0x627D42 },
            { 0xA77536, 0x78895C, 0xA77536, 0x5B7548 },
        };
        static readonly uint[,] Growing =
        {
            { 0x73813C, 0x99A454, 0x778143, 0xADB05A },
            { 0x6C8A42, 0xA6AA5E, 0x799049, 0xB4BC6F },
            { 0x6F8051, 0x919C68, 0x6C794C, 0xA5AF7B },
        };
        static readonly uint[,] Ripe =
        {
            { 0xC7953F, 0xD9AF52, 0x929454, 0xE2B863 },
            { 0xBC913E, 0xD4BD71, 0x9D925D, 0xE9CE86 },
            { 0x967D4B, 0xBAA374, 0x85845D, 0xD0B987 },
        };

        public static void Apply(Color32[] pixels, int crop, int stage)
        {
            for (int i = 0; i < pixels.Length; i++)
            {
                var pixel = pixels[i];
                if (pixel.a == 0) continue;
                uint rgb = ((uint)pixel.r << 16) | ((uint)pixel.g << 8) | pixel.b;
                int mark = System.Array.IndexOf(Marks, rgb);
                if (mark < 0) continue;
                uint color = stage switch
                {
                    0 => Soil[mark],
                    1 => Sprouts[crop, mark],
                    2 => Growing[crop, mark],
                    _ => Ripe[crop, mark],
                };
                pixels[i] = new Color32((byte)(color >> 16), (byte)(color >> 8), (byte)color, pixel.a);
            }
        }
    }
}
