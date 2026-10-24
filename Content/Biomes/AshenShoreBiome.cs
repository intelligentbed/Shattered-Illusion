using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using ShatteredIllusion.Content.Tiles.BoilingOcean;
using ShatteredIllusion.Content.World.WorldGeneration;
using System;

namespace ShatteredIllusion.Content.Biomes
{
    /// <summary>
    /// The shore is only the surface sea. The deep part underneath is a separate biome, the Ashen Core
    /// (see AshenCoreBiome). Rough layout, in tile coordinates (x grows to the right, y grows DOWN):
    /// </summary>
    public class AshenShoreBiome : ShatteredBiome
    {
        public override SceneEffectPriority Priority => SceneEffectPriority.BiomeHigh;

        // Blocks that count toward the shore: Ashen Sand (beach) and Ashen Dirt (seafloor).
        protected override int[] GetBiomeTiles()
        {
            return
            [
                ModContent.TileType<AshenSand>(),
                ModContent.TileType<AshenDirt>()
            ];
        }
        public override ModWaterStyle WaterStyle => ModContent.GetInstance<AshenWaterStyle>();

        // to be in the shore you eithr have to be in the shore's area or place 300+ ashen dirt/sand
        // Either way, the shore is never active inside the Ashen Core, so the two never overlap.
        public override bool IsBiomeActive(Player player)
        {
            // player.Center is in pixels; ToTileCoordinates divides by 16 to get the tile the player is in.
            Point tile = player.Center.ToTileCoordinates();
            return (HasEnoughTiles || IsInShoreArea(tile)) && !AshenCoreBiome.IsInCoreArea(tile);
        }

        /// <summary>
        /// Right edge of the Ashen area, in tiles from the left edge of the world.
        /// Matches the generator: it builds BiomeWidth (700) columns, or fewer on a world too small for that.
        /// </summary>
        public static int AreaWidth => Math.Min(AshenShore.BiomeWidth, Main.maxTilesX - 5);

        /// <summary>
        /// Depth (tile y) where the shore ends and the Ashen Core begins.
        /// For worlds that generated the shore, this is the real bottom of the generated shore (the deepest
        /// seafloor plus its dirt layer), saved with the world, so the biome covers all of the terrain the
        /// generator made. Older worlds fall back to a depth derived from the cavern layer:
        /// Main.rockLayer is where the cavern layer starts; maxTilesY / 13 moves the line up a bit.
        /// </summary>
        public static int AreaBottom => AshenShore.GeneratedShoreBottomY > 0
            ? AshenShore.GeneratedShoreBottomY
            : (int)(Main.rockLayer - Main.maxTilesY / 13);

        /// <summary>
        /// True if the tile is inside the shore's area: left of AreaWidth and above AreaBottom.
        /// Note: this doesn't check the shore actually generated in this world (e.g. a world made
        /// before it existed), so in such worlds the left edge still counts as Ashen Shore.
        /// </summary>
        public static bool IsInShoreArea(Point tile) => tile.X < AreaWidth && tile.Y < AreaBottom;
    }
}