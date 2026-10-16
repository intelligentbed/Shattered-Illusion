using Terraria.ModLoader;
using ShatteredIllusion.Common.Systems;

namespace ShatteredIllusion.Content.Biomes
{
    /// <summary>
    /// Base class for every Shattered Illusion biome.
    ///
    /// A plain ModBiome is registered with tModLoader but never turns on by itself: the game only
    /// puts a player "in" it when IsBiomeActive returns true. This class adds the common way of
    /// deciding that, which is "are enough of this biome's blocks near the player?".
    ///
    /// To make a new biome:
    ///   1. Extend ShatteredBiome instead of ModBiome.
    ///   2. Return the biome's block (tile) types from GetBiomeTiles.
    ///   3. In IsBiomeActive, use HasEnoughTiles, a position check, or both.
    /// BiomeRegistry finds the new biome automatically and keeps its block count up to date.
    /// </summary>
    public abstract class ShatteredBiome : ModBiome
    {
        // Filled in once by BiomeRegistry after all content has loaded.
        internal int[] BiomeTiles = [];

        /// <summary>
        /// The tile types that count toward this biome, e.g. ModContent.TileType&lt;AshenSand&gt;().
        /// Called once after mod content has loaded, so ModContent lookups are safe here.
        /// Return an empty array if the biome is detected by position only.
        /// </summary>
        protected abstract int[] GetBiomeTiles();

        /// <summary>
        /// How many of this biome's blocks must be near the player for HasEnoughTiles to be true.
        /// "Near" is the area the game scans around the player's screen each scene update.
        /// For scale, vanilla needs about 300 blocks for Corruption and 1500 sand for the Desert.
        /// </summary>
        public virtual int MinimumTileCount => 300;

        /// <summary>How many of this biome's blocks were found near the local player in the last scan.</summary>
        public int TileCount => BiomeRegistry.GetTileCount(this);

        /// <summary>True when TileCount has reached MinimumTileCount.</summary>
        public bool HasEnoughTiles => TileCount >= MinimumTileCount;

        // Lets BiomeRegistry call the protected GetBiomeTiles.
        internal int[] RequestBiomeTiles() => GetBiomeTiles();
    }
}
