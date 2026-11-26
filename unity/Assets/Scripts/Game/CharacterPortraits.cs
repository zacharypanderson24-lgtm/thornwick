// Turns a character's saved appearance definition (a seed, a climate, an archetype and hair-ageing tendencies) into a portrait recipe and a cached texture.
// The face comes only from Look.Seed, so changing job or clothes (Look.Outfit, Look.Archetype) never changes who the person is; age picks the portrait's
// age stage, and the saved tendencies decide when the hair greys, recedes or goes.
using CrownAndHearth.Portraits;
using CrownAndHearth.Sim;
using UnityEngine;

namespace CrownAndHearth.Game
{
    public static class CharacterPortraits
    {
        public static PortraitAppearance Appearance(PortraitLibrary lib, Character c, int day)
        {
            // the physical identity: generated from the seed alone, whatever the archetype (so the outfit may change, the face does not)
            var face = PortraitGenerator.Generate(lib, c.Look.Seed, c.Look.Climate, null);
            var a = PortraitGenerator.Generate(lib, c.Look.Seed, c.Look.Climate, c.Look.Archetype);
            a.skin = face.skin; a.hair = face.hair; a.hair_color = face.hair_color; a.brows = face.brows; a.beard = face.beard; a.face = face.face; a.eyes = face.eyes;
            if (!string.IsNullOrEmpty(c.Look.Outfit) && lib.Find(PortraitLayer.Clothing, c.Look.Outfit) != null) a.clothing = c.Look.Outfit;
            int years = c.AgeYears(day);
            a.age = CharacterBalance.StageIds[(int)c.StageAt(day)];
            if (c.Look.GreyAge > 0 && years >= c.Look.GreyAge && lib.FindEntry(lib.hairSetId, "grey") != null) a.hair_color = "grey";
            if (c.Look.BaldAge > 0 && years >= c.Look.BaldAge && lib.Find(PortraitLayer.Hair, "bald") != null) a.hair = "bald";
            else if (c.Look.RecedeAge > 0 && years >= c.Look.RecedeAge && lib.Find(PortraitLayer.Hair, "receding") != null) a.hair = "receding";
            return a;
        }

        public static Texture2D Texture(Character c, int day)
        {
            var lib = PortraitLibrary.Shared;
            return lib == null ? null : PortraitCache.Shared.Get(lib, Appearance(lib, c, day));
        }
    }
}
