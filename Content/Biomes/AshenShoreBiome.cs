using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using ShatteredIllusion.Content.Tiles.BoilingOcean;
using ShatteredIllusion.Content.World.WorldGeneration;
using System;

namespace ShatteredIllusion.Content.Biomes
{
    public class AshenShoreBiome : ShatteredBiome
    {
        public override SceneEffectPriority Priority => SceneEffectPriority.BiomeHigh;

        // Volcanic Stone is left out: it fills the depths, which belong to the Ashen Core.
        protected override int[] GetBiomeTiles()
        {
            return
            [
                ModContent.TileType<AshenSand>(),
                ModContent.TileType<AshenDirt>()
            ];
        }

        // Like Calamity's Sulphurous Sea: enough Ashen blocks nearby, or anywhere in the shore's surface band,
        // so open water far from the seafloor still counts. Never active inside the Ashen Core.
        public override bool IsBiomeActive(Player player)
        {
            Point tile = player.Center.ToTileCoordinates();
            return (HasEnoughTiles || IsInShoreArea(tile)) && !AshenCoreBiome.IsInCoreArea(tile);
        }

        public static int AreaWidth => Math.Min(AshenShore.BiomeWidth, Main.maxTilesX - 5);

        // The shore band ends here and the Ashen Core begins.
        public static int AreaBottom => (int)(Main.rockLayer - Main.maxTilesY / 13);

        public static bool IsInShoreArea(Point tile) => tile.X < AreaWidth && tile.Y < AreaBottom;
    }
}
