// Finds sprites by the prototype's slot names (assets/<name>_<view>.png), with placeholders (ph_<name>) where no art exists yet.
using System.Collections.Generic;
using UnityEngine;

namespace CrownAndHearth.Game
{
    public class ArtLibrary
    {
        readonly Dictionary<string, Sprite> _sprites = new Dictionary<string, Sprite>();
        readonly Dictionary<Sprite, Sprite> _banners = new Dictionary<Sprite, Sprite>();
        Color32? _bannerColor;

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
            var source = Find(slot + "_" + (view + 1)) ?? Find(slot) ?? Find("ph_" + slot);
            if (source == null || !_bannerColor.HasValue || (slot != "hall" && slot != "barracks") || !source.texture.isReadable)
                return source;
            if (_banners.TryGetValue(source, out var tinted)) return tinted;

            var rect = source.rect;
            int width = (int)rect.width, height = (int)rect.height;
            var original = source.texture.GetPixels((int)rect.x, (int)rect.y, width, height);
            var pixels = System.Array.ConvertAll(original, pixel => (Color32)pixel);
            BannerTint.Apply(pixels, width, _bannerColor.Value);
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            var pivot = new Vector2(source.pivot.x / width, source.pivot.y / height);
            tinted = Sprite.Create(texture, new Rect(0, 0, width, height), pivot,
                source.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            _banners[source] = tinted;
            return tinted;
        }

        public void SetBanner(string hex)
        {
            Color32? next = ColorUtility.TryParseHtmlString(hex ?? "", out var color) ? (Color32)color : (Color32?)null;
            if (System.Nullable.Equals(_bannerColor, next)) return;
            foreach (var sprite in _banners.Values)
            {
                Object.Destroy(sprite.texture);
                Object.Destroy(sprite);
            }
            _banners.Clear();
            _bannerColor = next;
        }
    }
}
