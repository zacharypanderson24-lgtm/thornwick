using System;
using System.Collections.Generic;
using UnityEngine;

namespace CrownAndHearth.Game
{
    // The approved sheets are framed at runtime to keep import settings and anchors consistent across the roster.
    sealed class BattleArt : IDisposable
    {
        const string Folder = "Art/Battle/";

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
                    _cache[slot] = Sprite.Create(_texture, rect, _pivot, IsoView.PPU, 0, SpriteMeshType.FullRect);
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
        public Sprite Fx(string name, int frame)
        {
            var effect = _effects[name];
            return _effectSheet.Frame(effect.firstColumn + Mathf.Clamp(frame, 0, effect.frames - 1), 0);
        }
        public void Dispose()
        {
            foreach (var actor in _actors.Values) actor.sheet.Release();
            _effectSheet.Release();
        }
    }
}
