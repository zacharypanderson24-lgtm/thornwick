// A complete portrait recipe: ids only, never image references, so replacing artwork or adding assets never breaks a saved character.
// The field names are the Python generator's JSON keys, so recipes from the supplied tools load unchanged (JsonUtility.FromJson).
using System;
using UnityEngine;

namespace CrownAndHearth.Portraits
{
    [Serializable]
    public class PortraitAppearance
    {
        public int version = 19;
        public int seed;
        public string climate = "temperate";       // regional wardrobe pool the character was generated from
        public string archetype = "";              // "" = none
        public string face = "standard";
        public string skin = "peach";
        public string hair = "short";
        public string hair_color = "brown";
        public string brows = "straight";
        public string eyes = "standard";
        public string beard = "none";
        public string clothing = "civil";
        public string headwear = "none";
        public string clothing_color = "original";
        public string headwear_color = "original";
        public string age = "young";

        public PortraitAppearance Clone() => (PortraitAppearance)MemberwiseClone();
        public string ToJson(bool pretty = false) => JsonUtility.ToJson(this, pretty);
        public static PortraitAppearance FromJson(string json) => JsonUtility.FromJson<PortraitAppearance>(json);

        // Everything that changes the picture (not the seed or the generation bookkeeping): the cache key.
        public string Key() => string.Join("|", face, skin, age, clothing, clothing_color, hair, hair_color, brows, beard, eyes, headwear, headwear_color);
    }
}
