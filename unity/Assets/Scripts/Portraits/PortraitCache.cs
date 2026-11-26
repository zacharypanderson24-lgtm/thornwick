// On-demand portrait textures. A portrait is drawn the first time somebody asks for it and kept in a small least-recently-used cache keyed by what
// changes the picture, so it is only redrawn when the appearance changes, and thousands of characters never cost anything until one is shown.
using System.Collections.Generic;
using UnityEngine;

namespace CrownAndHearth.Portraits
{
    public class PortraitCache
    {
        class Item { public string key; public Texture2D tex; }
        readonly int _capacity;
        readonly Dictionary<string, LinkedListNode<Item>> _map = new Dictionary<string, LinkedListNode<Item>>();
        readonly LinkedList<Item> _lru = new LinkedList<Item>();

        public int Count => _map.Count;
        public int Renders { get; private set; }      // how many portraits were actually drawn (tests and diagnostics)

        public PortraitCache(int capacity = 256) { _capacity = Mathf.Max(1, capacity); }

        public static readonly PortraitCache Shared = new PortraitCache(256);

        public Texture2D Get(PortraitLibrary lib, PortraitAppearance a)
        {
            string key = a.Key();
            if (_map.TryGetValue(key, out var node))
            {
                _lru.Remove(node); _lru.AddFirst(node);
                if (node.Value.tex != null) return node.Value.tex;      // (a destroyed texture is redrawn below)
                _lru.Remove(node); _map.Remove(key);
            }
            var tex = PortraitRenderer.ToTexture(PortraitRenderer.Render(lib, a));
            tex.name = "portrait";
            Renders++;
            _map[key] = _lru.AddFirst(new Item { key = key, tex = tex });
            while (_map.Count > _capacity)
            {
                var last = _lru.Last; _lru.RemoveLast(); _map.Remove(last.Value.key);
                if (last.Value.tex != null) Release(last.Value.tex);
            }
            return tex;
        }

        static void Release(Texture2D t) { if (Application.isPlaying) Object.Destroy(t); else Object.DestroyImmediate(t); }

        // Call after the artwork, palettes or rules change.
        public void Clear()
        {
            foreach (var i in _lru) if (i.tex != null) Release(i.tex);
            _lru.Clear(); _map.Clear();
            PortraitRenderer.ClearLayerCache();
        }
    }
}
