using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CrownAndHearth.Game
{
    // The approved sheets are framed at runtime to keep import settings and anchors consistent across the roster.
    sealed class BattleArt : IDisposable
    {
        const string Folder = "Art/Battle/";
        const float PixelsPerUnit = IsoView.PPU * 2f;

        [Serializable] sealed class Catalogue
        {
            public int width, height, foot;
            public ActorInfo[] actors;
            public Effect[] effects;
        }
        [Serializable] sealed class ActorInfo { public string name; public Clip[] clips; }
        [Serializable] sealed class Effect { public string name; public int firstColumn, frames; }
        [Serializable] sealed class Clip
        {
            public string name;
            public int firstColumn;
            public int[] frameMs;
            public bool loop;
            public int Duration { get; private set; }

            public void Prepare()
            {
                foreach (int ms in frameMs) Duration += ms;
            }
            public int Column(float dayFraction, float clock)
            {
                float ms = loop ? Mathf.Repeat(clock, Duration) : Mathf.Clamp01(dayFraction) * Duration;
                for (int i = 0; i < frameMs.Length - 1; i++)
                {
                    if (ms < frameMs[i]) return firstColumn + i;
                    ms -= frameMs[i];
                }
                return firstColumn + frameMs.Length - 1;
            }
        }

        sealed class Sheet
        {
            readonly Texture2D _texture;
            readonly int _width, _height, _columns;
            readonly Vector2 _pivot;
            readonly Sprite[] _cache;
            public Sheet(Texture2D texture, int width, int height, Vector2 pivot)
            {
                _texture = texture;
                _width = width;
                _height = height;
                _columns = texture.width / width;
                _pivot = pivot;
                _cache = new Sprite[_columns * (texture.height / height)];
            }
            public Sprite Frame(int column, int row)
            {
                int slot = row * _columns + column;
                if (_cache[slot] == null)
                {
                    var rect = new Rect(column * _width, _texture.height - (row + 1) * _height, _width, _height);
                    _cache[slot] = Sprite.Create(_texture, rect, _pivot, PixelsPerUnit, 0, SpriteMeshType.FullRect);
                    _cache[slot].name = _texture.name;
                }
                return _cache[slot];
            }
            public void Release()
            {
                foreach (var frame in _cache)
                    if (frame != null) UnityEngine.Object.Destroy(frame);
            }
        }

        readonly Dictionary<string, (Sheet sheet, Dictionary<string, Clip> clips)> _actors = new();
        readonly Dictionary<string, Effect> _effects = new();
        readonly Sheet _effectSheet;

        public BattleArt()
        {
            var source = Resources.Load<TextAsset>(Folder + "battle_catalogue");
            if (source == null) throw new InvalidOperationException("Missing battle art catalogue.");
            var catalogue = JsonUtility.FromJson<Catalogue>(source.text);
            Vector2 pivot = new Vector2(0.5f, 1f - catalogue.foot / (float)catalogue.height);
            _pivot = pivot; _frameW = catalogue.width; _frameH = catalogue.height;
            foreach (var actor in catalogue.actors)
            {
                var clips = new Dictionary<string, Clip>();
                foreach (var clip in actor.clips)
                {
                    clip.Prepare();
                    clips.Add(clip.name, clip);
                }
                _actors.Add(actor.name, (LoadSheet(actor.name, catalogue, 4, pivot), clips));
            }
            _effectSheet = LoadSheet("battle_effects", catalogue, 1, new Vector2(0.5f, 0.5f));
            foreach (var effect in catalogue.effects) _effects.Add(effect.name, effect);
        }

        static Sheet LoadSheet(string name, Catalogue catalogue, int rows, Vector2 pivot)
        {
            var texture = Resources.Load<Texture2D>(Folder + name);
            if (texture == null || texture.height != rows * catalogue.height || texture.width % catalogue.width != 0)
                throw new InvalidOperationException("Missing or malformed battle sheet: " + name);
            return new Sheet(texture, catalogue.width, catalogue.height, pivot);
        }

        public Sprite Pose(string actor, string clip, int facing, int day, float fraction, int id)
        {
            var data = _actors[actor];
            int column = data.clips[clip].Column(fraction, (day % 1000 + fraction) * 1000f + id * 71);
            return data.sheet.Frame(column, facing);
        }
        /// <summary>The tunic colours of our soldiers' sheets (the infantry's brown body, the archers' green): only these are dyed, so
        /// helmets, shields, skin and weapons stay as they are. Each is dyed in the new colour at its own brightness, keeping the shading.</summary>
        public static readonly Dictionary<string, string[]> ClothColours = new()
        {
            { "infantry", new[] { "635B4A", "8A806B", "AFA28B" } },
            { "archer", new[] { "4D5748", "687461" } },
        };

        static float Luma(Color32 c) => (0.299f * c.r + 0.587f * c.g + 0.114f * c.b) / 255f;

        static void DyeCloth(string actor, Color32[] pixels, Color32 dye)
        {
            if (!ClothColours.TryGetValue(actor, out var hexes)) return;
            var cloth = new Dictionary<int, float>();   // packed rgb -> brightness relative to the cloth's middle tone
            var tones = new List<Color32>();
            foreach (var hex in hexes)
            {
                int v = Convert.ToInt32(hex, 16);
                tones.Add(new Color32((byte)(v >> 16), (byte)(v >> 8), (byte)v, 255));
            }
            float mid = tones.Average(Luma);
            foreach (var t in tones) cloth[t.r << 16 | t.g << 8 | t.b] = Luma(t) / mid;
            for (int i = 0; i < pixels.Length; i++)
            {
                var p = pixels[i];
                if (p.a < 200 || !cloth.TryGetValue(p.r << 16 | p.g << 8 | p.b, out float shade)) continue;
                float k = Mathf.Clamp(shade * 0.85f, 0.35f, 1.25f);
                pixels[i] = new Color32((byte)Mathf.Clamp(dye.r * k, 0, 255), (byte)Mathf.Clamp(dye.g * k, 0, 255), (byte)Mathf.Clamp(dye.b * k, 0, 255), p.a);
            }
        }

        /// <summary>The same pose with the soldier's tunic dyed (DyeCloth): a settlement's soldiers are ours in their realm's colours. The
        /// red "hurt" flash frames are left alone. Each colour's sheet is made once.</summary>
        public Sprite PoseTinted(string actor, Color32 tint, string clip, int facing, int day, float fraction, int id)
        {
            var data = _actors[actor];
            int column = data.clips[clip].Column(fraction, (day % 1000 + fraction) * 1000f + id * 71);
            var key = (actor, tint.r << 16 | tint.g << 8 | tint.b);
            if (!_tinted.TryGetValue(key, out var sheet)) _tinted[key] = sheet = MakeTinted(actor, data.clips, tint);
            return (sheet ?? data.sheet).Frame(column, facing);
        }

        readonly Dictionary<(string, int), Sheet> _tinted = new();
        readonly List<Texture2D> _tintedTextures = new();
        Vector2 _pivot;
        int _frameW, _frameH;

        Sheet MakeTinted(string actor, Dictionary<string, Clip> clips, Color32 tint)
        {
            var source = Resources.Load<Texture2D>(Folder + actor);
            if (source == null || !source.isReadable) return null;   // not readable: fall back to the untinted sheet
            int w = source.width, h = source.height;
            var pixels = source.GetPixels32();
            var original = (Color32[])pixels.Clone();
            DyeCloth(actor, pixels, tint);
            if (clips.TryGetValue("hurt", out var hurt))   // keep the red hurt flash
                for (int c = hurt.firstColumn; c < hurt.firstColumn + hurt.frameMs.Length; c++)
                    for (int y = 0; y < h; y++)
                        for (int x = c * _frameW; x < (c + 1) * _frameW && x < w; x++) pixels[y * w + x] = original[y * w + x];
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = source.name };
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            _tintedTextures.Add(tex);
            return new Sheet(tex, _frameW, _frameH, _pivot);
        }

        public Sprite Fx(string name, int frame)
        {
            var effect = _effects[name];
            return _effectSheet.Frame(effect.firstColumn + Mathf.Clamp(frame, 0, effect.frames - 1), 0);
        }
        public void Dispose()
        {
            foreach (var actor in _actors.Values) actor.sheet.Release();
            _effectSheet.Release();
            foreach (var sheet in _tinted.Values) sheet?.Release();
            foreach (var tex in _tintedTextures) if (tex != null) UnityEngine.Object.Destroy(tex);
        }
    }
}
