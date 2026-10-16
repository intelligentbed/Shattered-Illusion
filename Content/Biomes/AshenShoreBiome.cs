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

        protected override int[] GetBiomeTiles()
        {
            return
            [
                ModContent.TileType<AshenSand>(),
                ModContent.TileType<AshenDirt>(),
                ModContent.TileType<VolcanicStone>()
            ];
        }

        // Like Calamity's Sulphurous Sea: enough Ashen blocks nearby, or anywhere in the generated shore area,
        // so open water far from the seafloor still counts.
        public override bool IsBiomeActive(Player player) => HasEnoughTiles || IsInShoreArea(player.Center.ToTileCoordinates());

        public static bool IsInShoreArea(Point tile)
        {
            int width = Math.Min(AshenShore.BiomeWidth, Main.maxTilesX - 5);
            int bottom = (int)Main.worldSurface + AshenShore.SeafloorDepth + AshenShore.SeafloorSolidBuffer;
            return tile.X < width && tile.Y < Math.Min(bottom, Main.UnderworldLayer);
        }
    }
}
