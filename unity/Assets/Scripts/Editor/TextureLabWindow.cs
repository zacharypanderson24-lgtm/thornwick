using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CrownAndHearth.Sim;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace CrownAndHearth.EditorTools
{
    public class TextureLabWindow : EditorWindow
    {
        static readonly string[] Buildings = Defs.Names.Keys.Where(name => name != "road" && name != "house")
            .Concat(new[] { "house_1", "house_2", "manor_1", "manor_2", "farm2", "market2" })
            .Concat(Enumerable.Range(2, 7).Select(i => "house_1_v" + i)).OrderBy(name => name).ToArray();

        DropdownField _slot, _rotation;
        Label _status;
        VisualElement _existing;
        Image _preview;
        Button _install;
        Texture2D _candidate;
        byte[] _sourceBytes;

        [MenuItem("Crown & Hearth/Texture Lab")]
        public static void Open() => GetWindow<TextureLabWindow>("Texture Lab");

        public void CreateGUI()
        {
            var root = rootVisualElement;
            root.Clear();
            var scroll = new ScrollView();
            scroll.style.paddingLeft = 10;
            scroll.style.paddingRight = 10;
            scroll.style.paddingTop = 8;
            root.Add(scroll);

            var slots = new List<string>(Buildings);
            slots.AddRange(PrototypeArtImporter.AtlasCells.Take(6));
            slots.AddRange(new[] { "rock_0", "rock_1", "ore_0", "ore_1", "stump", "sapling" });
            if (Directory.Exists(PrototypeArtImporter.SourceDir))
                slots.AddRange(Directory.GetFiles(PrototypeArtImporter.SourceDir, "tree_*.png")
                    .Select(Path.GetFileNameWithoutExtension).OrderBy(name => name));
            _slot = new DropdownField("Texture slot", slots, 0);
            _rotation = new DropdownField("Camera view", new List<string> { "1", "2", "3", "4" }, 0);
            scroll.Add(_slot);
            scroll.Add(_rotation);

            _existing = new VisualElement();
            _existing.style.flexDirection = FlexDirection.Row;
            _existing.style.flexWrap = Wrap.Wrap;
            scroll.Add(new Label("Current textures"));
            scroll.Add(_existing);

            scroll.Add(new Button(ChooseFile) { text = "Preview PNG…" });
            _preview = MakeImage();
            scroll.Add(_preview);
            _status = new Label();
            _status.style.whiteSpace = WhiteSpace.Normal;
            scroll.Add(_status);
            _install = new Button(Install) { text = "Apply to slot and reimport" };
            scroll.Add(_install);

            _slot.RegisterValueChangedCallback(_ => RefreshSlot());
            _rotation.RegisterValueChangedCallback(_ => ClearPreview());
            RefreshSlot();
        }

        bool FourViews => Array.IndexOf(Buildings, _slot.value) >= 0;
        bool Ground => _slot.value.StartsWith("grass", StringComparison.Ordinal);
        string Destination => FourViews ? _slot.value + "_" + _rotation.value : _slot.value;

        void RefreshSlot()
        {
            ClearPreview();
            _rotation.style.display = FourViews ? DisplayStyle.Flex : DisplayStyle.None;
            _existing.Clear();
            if (Ground)
            {
                var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(PrototypeArtImporter.OutDir + "/ground_atlas.png");
                var image = MakeImage();
                image.image = atlas;
                int cell = Array.IndexOf(PrototypeArtImporter.AtlasCells, _slot.value);
                image.uv = new Rect(cell / (float)PrototypeArtImporter.AtlasCells.Length, 0,
                    1f / PrototypeArtImporter.AtlasCells.Length, 1);
                _existing.Add(image);
                return;
            }

            for (int view = 1; view <= (FourViews ? 4 : 1); view++)
            {
                string name = FourViews ? _slot.value + "_" + view : _slot.value;
                var texture = FindTexture(name) ?? FindTexture(_slot.value) ?? FindTexture("ph_" + _slot.value);
                var column = new VisualElement();
                if (FourViews) column.Add(new Label("View " + view));
                var image = MakeImage();
                image.image = texture;
                column.Add(image);
                _existing.Add(column);
            }
        }

        static Texture2D FindTexture(string name) =>
            AssetDatabase.LoadAssetAtPath<Texture2D>(PrototypeArtImporter.OutDir + "/" + name + ".png");

        static Image MakeImage()
        {
            var image = new Image { scaleMode = ScaleMode.ScaleToFit };
            image.style.width = 144;
            image.style.height = 128;
            image.style.backgroundColor = new Color(0.16f, 0.20f, 0.16f);
            image.style.marginRight = 6;
            image.style.marginBottom = 8;
            return image;
        }

        void ChooseFile()
        {
            string file = EditorUtility.OpenFilePanel("Preview texture", PrototypeArtImporter.SourceDir, "png");
            if (string.IsNullOrEmpty(file)) return;
            ClearPreview();
            try
            {
                var bytes = File.ReadAllBytes(file);
                _candidate = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    hideFlags = HideFlags.HideAndDontSave,
                };
                if (!_candidate.LoadImage(bytes)) throw new InvalidDataException("This file is not a readable PNG.");
                int width = _candidate.width, height = _candidate.height;
                var pixels = _candidate.GetPixels32();
                PrototypeArtImporter.KeyOut(pixels, width, height);
                if (Ground)
                {
                    pixels = PrototypeArtImporter.FitGround(pixels, width, height);
                    if (pixels == null) throw new InvalidDataException("The ground texture needs a solid 2:1 diamond.");
                    _candidate.Reinitialize(32, 16);
                }
                else
                {
                    int expectedWidth = _slot.value == "hall" ? 96 :
                        _slot.value == "barracks" || _slot.value.StartsWith("manor_", StringComparison.Ordinal) ||
                        _slot.value == "farm2" || _slot.value == "market2" ? 64 : 32;
                    if (width != expectedWidth)
                        throw new InvalidDataException("This slot needs a " + expectedWidth + "-pixel-wide game sprite.");
                    if (!pixels.Any(pixel => pixel.a != 0))
                        throw new InvalidDataException("The texture is empty after cleaning the background.");
                }
                _candidate.SetPixels32(pixels);
                _candidate.Apply();
                _preview.image = _candidate;
                _sourceBytes = bytes;
                _status.text = $"Ready to import {Destination}.png";
                _install.SetEnabled(true);
            }
            catch (Exception error)
            {
                ClearPreview();
                _status.text = error.Message;
            }
        }

        void Install()
        {
            if (_sourceBytes == null) return;
            string path = Path.Combine(PrototypeArtImporter.SourceDir, Destination + ".png");
            if (File.Exists(path) && !EditorUtility.DisplayDialog("Replace texture?",
                    $"Replace {Destination}.png in the shared prototype art folder and reimport it into Unity?",
                    "Replace", "Cancel")) return;
            string temporary = path + ".tmp";
            try
            {
                Directory.CreateDirectory(PrototypeArtImporter.SourceDir);
                File.WriteAllBytes(temporary, _sourceBytes);
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
                PrototypeArtImporter.ImportAll();
                RefreshSlot();
                _status.text = $"Imported {Path.GetFileName(path)}";
            }
            catch (Exception error)
            {
                _status.text = error.Message;
            }
        }

        void ClearPreview()
        {
            if (_preview != null) _preview.image = null;
            if (_candidate != null) DestroyImmediate(_candidate);
            _candidate = null;
            _sourceBytes = null;
            _status.text = "";
            _install.SetEnabled(false);
        }

        void OnDisable()
        {
            if (_candidate != null) DestroyImmediate(_candidate);
        }
    }
}
