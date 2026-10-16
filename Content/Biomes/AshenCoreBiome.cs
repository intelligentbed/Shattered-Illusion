using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace ShatteredIllusion.Content.Biomes
{
    /// <summary>
    /// lwk just a pplanning right nowe sicne we havne tstarted on the biome yet sooo yeah 
    /// </summary>
    public class AshenCoreBiome : ShatteredBiome
    {
        //the biome takes over the the vanillia biomes so there isnt over lap
        public override SceneEffectPriority Priority => SceneEffectPriority.BiomeHigh;

        //ADD BLOCKS HERE (when we get some) AND ADD HasEnoughTiles to the
        // IsBiomeActive. 
        protected override int[] GetBiomeTiles() => [];

        public override bool IsBiomeActive(Player player) => IsInCoreArea(player.Center.ToTileCoordinates());

        /// <summary>
        /// True if the tile is in the Core's area: the same columns as the shore (left of
        /// AshenShoreBiome.AreaWidth), from where the shore ends (AreaBottom) all the way to the bottom
        /// of the world, including the underworld layer. Remember y grows downward, so "below" means a bigger y.
        /// Vanilla's player.ZoneUnderworldHeight is still true at that depth because it only checks height;
        /// code that should treat this area as the Core instead of hell has to check the Core first.
        /// </summary>
        public static bool IsInCoreArea(Point tile)
        {
            return tile.X < AshenShoreBiome.AreaWidth
                && tile.Y >= AshenShoreBiome.AreaBottom
                && tile.Y < Main.maxTilesY;
        }
    }
}
