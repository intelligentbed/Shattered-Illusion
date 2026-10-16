using Terraria;
using Terraria.ModLoader;
using ShatteredIllusion.Content.Tiles.BoilingOcean;

namespace ShatteredIllusion.Content.Biomes
{
    public class AshenShoreBiome : ShatteredBiome
    {
        public override SceneEffectPriority Priority => SceneEffectPriority.BiomeLow;

        protected override int[] GetBiomeTiles()
        {
            return
            [
                ModContent.TileType<AshenSand>(),
                ModContent.TileType<AshenDirt>(),
                ModContent.TileType<VolcanicStone>()
            ];
        }

        public override bool IsBiomeActive(Player player) => HasEnoughTiles && !player.ZoneUnderworldHeight;
    }
}
