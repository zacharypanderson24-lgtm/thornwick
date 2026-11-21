using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace CrownAndHearth.Tests
{
    public class TerrainArtTests
    {
        [Test]
        public void GroundAtlasKeepsPointSamplingAndUsesTheThreeAuthoredStoneSurfaces()
        {
            var atlas = Resources.Load<Texture2D>("Art/ground_atlas");
            Assert.That(atlas, Is.Not.Null);
            Assert.That(atlas.width, Is.EqualTo(352));
            Assert.That(atlas.height, Is.EqualTo(16));
            Assert.That(atlas.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(atlas.mipmapCount, Is.EqualTo(1));

            var png = new Texture2D(2, 2);
            var surface = new Texture2D(2, 2);
            try
            {
                var assets = Path.Combine(Application.dataPath, "..", "..", "assets");
                Assert.That(png.LoadImage(File.ReadAllBytes(Path.Combine(Application.dataPath, "Resources", "Art", "ground_atlas.png"))), Is.True);
                var cells = png.GetPixels32();
                foreach (var (name, slot) in new[] { ("mountain_rock", 6), ("mountain_snow", 7), ("worked_stone", 10) })
                {
                    Assert.That(surface.LoadImage(File.ReadAllBytes(Path.Combine(assets, name + ".png"))), Is.True, name);
                    Assert.That((surface.width, surface.height), Is.EqualTo((32, 16)), name);
                    var pixels = surface.GetPixels32();
                    for (int y = 0; y < 16; y++)
                        for (int x = 0; x < 32; x++)
                            Assert.That(cells[y * png.width + slot * 32 + x], Is.EqualTo(pixels[y * 32 + x]), name);
                }
            }
            finally
            {
                Object.DestroyImmediate(png);
                Object.DestroyImmediate(surface);
            }
        }

        [Test]
        public void NatureSpritesKeepTheirSlotsAndGroundPivot()
        {
            var species = new[] { "oak", "old_oak", "pine", "spruce", "fir", "birch", "aspen", "willow", "maple" };
            foreach (var name in species)
                for (int count = 2; count <= 4; count++) CheckSprite($"tree_{name}_{count}");
            foreach (int id in new[] { 0, 2, 3, 4, 5, 6, 7, 8, 9 }) CheckSprite($"tree_{id}");
            foreach (string name in new[] { "rock_0", "rock_1", "ore_0", "ore_1" }) CheckSprite(name);
        }

        static void CheckSprite(string name)
        {
            var sprites = Resources.LoadAll<Sprite>("Art/" + name);
            Assert.That(sprites, Has.Length.EqualTo(1), name);
            var sprite = sprites[0];
            Assert.That(sprite.name, Is.EqualTo(name));
            Assert.That((sprite.rect.width, sprite.rect.height), Is.EqualTo((32f, 48f)), name);
            Assert.That(sprite.pivot, Is.EqualTo(new Vector2(16f, 0f)), name);
        }
    }
}
