using Terraria.ModLoader;
using ShatteredIllusion.Common.Systems;

namespace ShatteredIllusion.Content.Biomes
{
    public abstract class ShatteredBiome : ModBiome
    {
        internal int[] BiomeTiles = [];

        protected abstract int[] GetBiomeTiles();

        public virtual int MinimumTileCount => 300;

        public int TileCount => BiomeRegistry.GetTileCount(this);

        public bool HasEnoughTiles => TileCount >= MinimumTileCount;

        internal int[] RequestBiomeTiles() => GetBiomeTiles();
    }
}
