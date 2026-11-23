using System;
using System.IO;
using CrownAndHearth.Game;
using CrownAndHearth.Sim;
using UnityEditor;
using UnityEngine;

namespace CrownAndHearth.EditorTools
{
    public static class ApprovedArtVerification
    {
        [InitializeOnLoadMethod]
        static void Schedule() { EditorApplication.update += CheckWhenReady; }
        static void CheckWhenReady()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            EditorApplication.update -= CheckWhenReady;
            Run();
        }
        [MenuItem("Crown & Hearth/Verify Approved Art")]
        public static void Run()
        {
            int count = 0;
            void Verify(string name, int width, int height)
            {
                var sprite = Resources.Load<Sprite>("Art/" + name);
                if (sprite == null || sprite.rect.width != width || sprite.rect.height != height ||
                    sprite.texture.filterMode != FilterMode.Point || sprite.pivot.y != 0)
                    throw new InvalidOperationException("Approved art import failed: " + name);
                count++;
            }
            foreach (string slot in new[] { "manor_1", "manor_2", "house_2", "storehouse", "smokehouse" })
                for (int view = 1; view <= 4; view++) Verify(slot + "_" + view, slot == "house_2" ? 32 : 64, slot == "house_2" ? 48 : 88);
            var walls = new WallArt();
            try
            {
                for (int mask = 0; mask < 16; mask++)
                {
                    foreach (string prefix in new[] { "palisade_", "palisade_damaged_", "palisade2_" }) Verify(prefix + mask, 32, 48);
                    foreach (int x in new[] { -1, 1 }) foreach (int y in new[] { -1, 1 })
                    {
                        string name = "palisade2_" + mask + "_" + x + "_" + y;
                        Verify(name, 32, 48);
                        if (walls.Get(mask, 2, x, y).name != name) throw new InvalidOperationException("Walkway orientation mismatch");
                    }
                    if (walls.Get(mask).name != "palisade_" + mask || walls.Get(mask, 2).name != "palisade2_" + mask)
                        throw new InvalidOperationException("Wall renderer did not select the approved sprite");
                }
            }
            finally { walls.Dispose(); }
            for (int view = 1; view <= 4; view++)
            {
                foreach (string state in new[] { "open", "closed", "broken" }) Verify("gate_" + state + "_" + view, 32, 48);
                Verify("wall_stairs_" + view, 32, 48);
            }
            if (Defs.PlacementSize("smokehouse") != 2 || Defs.HouseCap2[1] != 14) throw new InvalidOperationException("Footprint/capacity mismatch");
            var destination = Path.GetFullPath(Path.Combine(Application.dataPath, "../..", "approved-art-verification.json"));
            File.WriteAllText(destination, "{\"status\":\"passed\",\"sprites\":" + count + ",\"wallRenderer\":\"passed\",\"smokehouseFootprint\":2}");
            Debug.Log("[Approved Art] PASS: " + count + " imported sprites, correct native sizes/filtering/pivots, wall renderer and smokehouse footprint.");
        }
    }
}
