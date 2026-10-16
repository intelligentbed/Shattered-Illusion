using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace ShatteredIllusion.Content.Biomes
{
    // The abyss under the Ashen Shore: caves with ruins, and the volcano arena at the bottom.
    // Detection only for now; its generation and Core-specific tiles don't exist yet.
    public class AshenCoreBiome : ShatteredBiome
    {
        public override SceneEffectPriority Priority => SceneEffectPriority.BiomeHigh;

        // Add Core tiles here once they exist (Volcanic Stone also appears near the shore's surface, so it isn't used).
        protected override int[] GetBiomeTiles() => [];

        public override bool IsBiomeActive(Player player) => IsInCoreArea(player.Center.ToTileCoordinates());

        public static bool IsInCoreArea(Point tile)
        {
            return tile.X < AshenShoreBiome.AreaWidth
                && tile.Y >= AshenShoreBiome.AreaBottom
                && tile.Y < Main.UnderworldLayer;
        }
    }
}
