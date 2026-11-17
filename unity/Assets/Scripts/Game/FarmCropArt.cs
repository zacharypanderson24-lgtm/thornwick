using System.Collections.Generic;
using UnityEngine;

namespace CrownAndHearth.Game
{
    internal sealed class FarmCropArt
    {
        const int CropCount = 3;
        const int StageCount = 4;
        const int DaysPerStage = 4;
        readonly Sprite[,] _sprites = new Sprite[CropCount, StageCount];

        public FarmCropArt()
        {
            const int size = 32;
            var atlas = Resources.Load<Texture2D>("Art/farm-crops");
            if (atlas == null || atlas.width != size * StageCount || atlas.height != size * CropCount)
                return;
            for (int crop = 0; crop < CropCount; crop++)
                for (int stage = 0; stage < StageCount; stage++)
                    _sprites[crop, stage] = Sprite.Create(atlas,
                        new Rect(stage * size, (CropCount - 1 - crop) * size, size, size),
                        new Vector2(0.5f, 0), IsoView.PPU, 0, SpriteMeshType.FullRect);
        }

        public Sprite Tile(int crop, int day, int offset) =>
            _sprites[crop, ((day - 1 + offset) / DaysPerStage) % StageCount];

        public static (int crop, int offset) Look(int x, int y, int seed) =>
            ((int)(Sim.Noise.Hash2(x, y, seed) * CropCount),
             (int)(Sim.Noise.Hash2(x, y, seed + 17) * StageCount * DaysPerStage));

        public void Dispose()
        {
            foreach (var sprite in _sprites)
                if (sprite != null) Object.Destroy(sprite);
        }
    }
}
