using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace ShatteredIllusion.Content.Biomes
{
    /// <summary>
    /// The Ashen Core: the abyss under the Ashen Shore (like Calamity's Abyss under the Sulphurous Sea).
    /// Planned layout: caves with ruins of the geothermal plants, then a big volcano arena at the bottom.
    /// It runs all the way to the bottom of the world, replacing the underworld (hell) in its columns.
    ///
    /// Detection only for now. Nothing generates the Core yet and it has no blocks of its own, so it is
    /// detected purely by position. See AshenShoreBiome for a diagram of how the two areas stack.
    /// </summary>
    public class AshenCoreBiome : ShatteredBiome
    {
        // BiomeHigh, same as the shore, so it beats vanilla underground, cavern and underworld biomes here.
        public override SceneEffectPriority Priority => SceneEffectPriority.BiomeHigh;

        // No Core blocks yet. When the Core gets its own tiles, list them here and add HasEnoughTiles
        // to IsBiomeActive. Volcanic Stone isn't used because the generator also places it near the
        // shore's surface (the lava fringe), which would make the Core turn on up there.
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
