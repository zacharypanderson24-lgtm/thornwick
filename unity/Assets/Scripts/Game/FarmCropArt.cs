using UnityEngine;

namespace CrownAndHearth.Game
{
    internal sealed class FarmCropArt
    {
        const int StageCount = 4;
        const int DaysPerStage = 4;
        readonly Sprite[] _sprites = new Sprite[StageCount];

        public FarmCropArt()
        {
            const int size = 32;
            var atlas = Resources.Load<Texture2D>("Art/farm-crops");
            if (atlas == null || atlas.width != size * StageCount || atlas.height != size)
                return;
            for (int stage = 0; stage < StageCount; stage++)
                _sprites[stage] = Sprite.Create(atlas, new Rect(stage * size, 0, size, size),
                    new Vector2(0.5f, 0), IsoView.PPU, 0, SpriteMeshType.FullRect);
        }

        public Sprite Tile(int day, int offset) =>
            _sprites[((day - 1 + offset) / DaysPerStage) % StageCount];

        public static int GrowthOffset(int x, int y, int seed) =>
            (int)(Sim.Noise.Hash2(x, y, seed + 17) * StageCount * DaysPerStage);

        public void Dispose()
        {
            foreach (var sprite in _sprites)
                if (sprite != null) Object.Destroy(sprite);
        }
    }
}
