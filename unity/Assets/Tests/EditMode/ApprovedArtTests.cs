using NUnit.Framework;
using UnityEngine;
using CrownAndHearth.Game;
using CrownAndHearth.Sim;

namespace CrownAndHearth.Tests
{
    public class ApprovedArtTests
    {
        static void SpriteReady(string name, int width, int height)
        {
            var sprite = Resources.Load<Sprite>("Art/" + name);
            Assert.IsNotNull(sprite, name + " imports as a Sprite");
            Assert.AreEqual(width, sprite.rect.width, name);
            Assert.AreEqual(height, sprite.rect.height, name);
            Assert.AreEqual(FilterMode.Point, sprite.texture.filterMode, name);
            Assert.AreEqual(TextureWrapMode.Clamp, sprite.texture.wrapMode, name);
            Assert.AreEqual(32, sprite.pixelsPerUnit, name);
            Assert.AreEqual(0, sprite.pivot.y, name + " anchors to the ground");
        }

        [TestCase("manor_1", 64, 88)]
        [TestCase("manor_2", 64, 88)]
        [TestCase("house_2", 32, 48)]
        [TestCase("storehouse", 64, 88)]
        [TestCase("smokehouse", 64, 88)]
        public void BuildingRotationsAreImportedAtNativeSize(string slot, int w, int h)
        {
            var library = new ArtLibrary();
            for (int view = 0; view < 4; view++)
            {
                string name = slot + "_" + (view + 1);
                SpriteReady(name, w, h);
                Assert.AreSame(Resources.Load<Sprite>("Art/" + name), library.Building(slot, view));
            }
        }

        [Test]
        public void EveryPalisadeConnectionUsesTheApprovedSprite()
        {
            var walls = new WallArt();
            try
            {
                for (int mask = 0; mask < 16; mask++)
                {
                    foreach (string prefix in new[] { "palisade_", "palisade_damaged_", "palisade2_" })
                        SpriteReady(prefix + mask, 32, 48);
                    Assert.AreSame(Resources.Load<Sprite>("Art/palisade_" + mask), walls.Get(mask));
                    Assert.AreSame(Resources.Load<Sprite>("Art/palisade2_" + mask), walls.Get(mask, 2));
                }
            }
            finally { walls.Dispose(); }
        }

        [Test]
        public void GateStatesAndAccessStairsHaveEveryRotation()
        {
            for (int view = 1; view <= 4; view++)
            {
                foreach (string state in new[] { "open", "closed", "broken" })
                    SpriteReady("gate_" + state + "_" + view, 32, 48);
                SpriteReady("wall_stairs_" + view, 32, 48);
            }
        }

        [Test]
        public void SmokehousePlacementMatchesItsTwoByTwoArt()
        {
            Assert.AreEqual(2, Defs.DefaultSize("smokehouse"));
            Assert.AreEqual(2, Defs.PlacementSize("smokehouse"));
            Assert.AreEqual("smokehouse", ObjectRenderer.SlotName(new Building { T = "smokehouse", Sz = 2 }));
            Assert.AreEqual(14, Defs.HouseCap2[1]);
            Assert.AreEqual(20, Defs.HouseCap2[2]);
        }
    }
}
