using CrownAndHearth.Game;
using NUnit.Framework;
using UnityEngine;

namespace CrownAndHearth.Tests
{
    public class PortraitDisplayTests
    {
        [Test] public void UpscaleKeepsEverySourcePixelAsAHardSquare()
        {
            var src = new Texture2D(48, 48, TextureFormat.RGBA32, false);
            for (int y = 0; y < 48; y++) for (int x = 0; x < 48; x++) src.SetPixel(x, y, new Color32((byte)(x * 5), (byte)(y * 5), 7, 255));
            src.Apply();
            var up = CharacterPortraits.Upscale(src);
            Assert.AreEqual(192, up.width); Assert.AreEqual(192, up.height);
            Assert.AreEqual(FilterMode.Bilinear, up.filterMode); Assert.Greater(up.mipmapCount, 1);
            var a = up.GetPixels32(0); var b = src.GetPixels32();
            for (int y = 0; y < 192; y += 7) for (int x = 0; x < 192; x += 5) Assert.AreEqual(b[(y / 4) * 48 + x / 4], a[y * 192 + x], $"({x},{y})");
            Object.DestroyImmediate(src); Object.DestroyImmediate(up);
        }
    }
}
