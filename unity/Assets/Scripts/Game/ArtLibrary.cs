// Finds sprites by the prototype's slot names (assets/<name>_<view>.png), with placeholders (ph_<name>) where no art exists yet.
using System.Collections.Generic;
using UnityEngine;

namespace CrownAndHearth.Game
{
    public class ArtLibrary
    {
        readonly Dictionary<string, Sprite> _sprites = new Dictionary<string, Sprite>();
        public Texture2D GroundAtlas { get; }
        /// <summary>Atlas cells, in the order PrototypeArtImporter writes them.</summary>
        public const int CellGrass = 0, CellFert = 4, CellRock = 6, CellSnow = 7, CellRoad = 8, CellWhite = 9, CellCount = 10;

        public ArtLibrary()
        {
            foreach (var s in Resources.LoadAll<Sprite>("Art")) _sprites[s.name] = s;
            GroundAtlas = Resources.Load<Texture2D>("Art/ground_atlas");
            if (GroundAtlas == null) Debug.LogError("Art/ground_atlas is missing. Run Crown & Hearth > Import Prototype Art.");
        }

        public Sprite Find(string name) => _sprites.TryGetValue(name, out var s) ? s : null;

        /// <summary>A building's sprite for camera-relative view 0..3 (file suffix _1.._4), else its single image, else its placeholder.</summary>
        public Sprite Building(string slot, int view)
        {
            return Find(slot + "_" + (view + 1)) ?? Find(slot) ?? Find("ph_" + slot);
        }
    }
}
