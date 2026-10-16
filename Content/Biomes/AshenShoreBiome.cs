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

        // Volcanic Stone is left out: it fills the depths, which belong to the planned abyss biome.
        protected override int[] GetBiomeTiles()
        {
            return
            [
                ModContent.TileType<AshenSand>(),
                ModContent.TileType<AshenDirt>()
            ];
        }

        // Like Calamity's Sulphurous Sea: enough Ashen blocks nearby, or anywhere in the shore's surface band,
        // so open water far from the seafloor still counts.
        public override bool IsBiomeActive(Player player) => HasEnoughTiles || IsInShoreArea(player.Center.ToTileCoordinates());

        public static bool IsInShoreArea(Point tile)
        {
            int width = Math.Min(AshenShore.BiomeWidth, Main.maxTilesX - 5);
            return tile.X < width && tile.Y < Main.rockLayer - Main.maxTilesY / 13;
        }
    }
}
