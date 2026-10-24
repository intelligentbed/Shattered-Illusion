using System.Collections.Generic;
using Terraria;
using Terraria.GameContent.Generation;
using Terraria.ID;
using Terraria.IO;
using Terraria.ModLoader;
using Terraria.WorldBuilding;
using ShatteredIllusion.Content.Items.Placeables.Blocks;

namespace ShatteredIllusion.World.WorldGeneration
{
    public class MossWorldGen : ModSystem
    {
        // 1 in N dirt tiles inside the cavern layer starts a moss vein. Lower = more moss.
        private const int CavernVeinChance = 200;

        // Chance (1 in N) for each tile inside a vein's ellipse to be skipped, for a ragged edge.
        private const int SkipChance = 4;

        // Minimum number of columns between two trees that get moss under them.
        private const int TreeColumnSpacing = 12;

        // How far below a trunk to look for the ground.
        private const int TreeGroundSearchDepth = 80;

        public override void ModifyWorldGenTasks(
            List<GenPass> tasks,
            ref double totalWeight)
        {
            int insertIndex;

            int genIndex = tasks.FindIndex(
                genpass => genpass.Name.Equals("Living Trees"));

            if (genIndex != -1)
            {
                insertIndex = genIndex + 1;
            }
            else
            {
                Mod.Logger.Warn(
                    "[MossGen] Could not find 'Living Trees' gen pass. " +
                    "Inserting moss generation before 'Settle Liquids' instead.");

                insertIndex = tasks.FindIndex(
                    genpass => genpass.Name.Equals("Settle Liquids"));

                if (insertIndex == -1)
                    insertIndex = tasks.Count;
            }

            tasks.Insert(
                insertIndex,
                new PassLegacy(
                    "Generating Moss",
                    GenerateMossPass));
        }

        private void GenerateMossPass(
            GenerationProgress progress,
            GameConfiguration configuration)
        {
            progress.Message = "Generating Moss";

            ushort mossType = (ushort)ModContent.TileType<MossBlock>();

            int cavernTiles = GenerateCavernMoss(progress, mossType);
            int treeTiles = GenerateTreeMoss(progress, mossType);

            Mod.Logger.Info(
                $"[MossGen] Placed {cavernTiles} moss tiles in the caverns " +
                $"and {treeTiles} under living trees.");
        }

        /// <summary>
        /// Seeds moss veins on dirt in the cavern layer.
        /// </summary>
        private int GenerateCavernMoss(GenerationProgress progress, ushort mossType)
        {
            int placed = 0;

            int minX = 20;
            int maxX = Main.maxTilesX - 20;
            int minY = (int)Main.rockLayer;

            // The underworld starts roughly 200 tiles above the bottom of the world.
            int maxY = Main.maxTilesY - 200;

            for (int x = minX; x < maxX; x++)
            {
                progress.Set((x - minX) / (float)(maxX - minX) * 0.5f);

                for (int y = minY; y < maxY; y++)
                {
                    Tile tile = Main.tile[x, y];

                    if (!tile.HasTile || tile.TileType != TileID.Dirt)
                        continue;

                    if (!WorldGen.genRand.NextBool(CavernVeinChance))
                        continue;

                    placed += PlaceMossVein(
                        x,
                        y,
                        WorldGen.genRand.Next(2, 5),
                        WorldGen.genRand.Next(2, 5),
                        minY,
                        maxY,
                        mossType);
                }
            }

            return placed;
        }

        /// <summary>
        /// Puts moss veins in the dirt underneath living trees.
        /// </summary>
        private int GenerateTreeMoss(GenerationProgress progress, ushort mossType)
        {
            int placed = 0;
            int lastTreeX = -1000;
            int surfaceY = (int)Main.worldSurface;

            for (int i = 20; i < Main.maxTilesX - 20; i++)
            {
                progress.Set(0.5f + i / (float)Main.maxTilesX * 0.5f);

                // Only one pass per tree, not one per trunk column.
                if (i - lastTreeX < TreeColumnSpacing)
                    continue;

                // Topmost living wood in this column.
                int trunkY = -1;

                for (int j = 20; j < surfaceY; j++)
                {
                    Tile tile = Main.tile[i, j];

                    if (tile.HasTile && tile.TileType == TileID.LivingWood)
                    {
                        trunkY = j;
                        break;
                    }
                }

                if (trunkY == -1)
                    continue;

                // Find the ground below the tree. The trunk can be hollow, so skip
                // over air, living wood and leaves until we hit real solid ground.
                int groundY = -1;
                int searchEnd = System.Math.Min(
                    trunkY + TreeGroundSearchDepth,
                    Main.maxTilesY - 20);

                for (int j = trunkY; j < searchEnd; j++)
                {
                    Tile tile = Main.tile[i, j];

                    if (!tile.HasTile ||
                        tile.TileType == TileID.LivingWood ||
                        tile.TileType == TileID.LeafBlock)
                    {
                        continue;
                    }

                    if (Main.tileSolid[tile.TileType])
                    {
                        groundY = j;
                        break;
                    }
                }

                if (groundY == -1)
                    continue;

                lastTreeX = i;

                int treeVeinCount = WorldGen.genRand.Next(3, 7);

                for (int vein = 0; vein < treeVeinCount; vein++)
                {
                    int centerX = i + WorldGen.genRand.Next(-8, 9);
                    int centerY = groundY + WorldGen.genRand.Next(0, 12);

                    placed += PlaceMossVein(
                        centerX,
                        centerY,
                        WorldGen.genRand.Next(2, 5),
                        WorldGen.genRand.Next(2, 5),
                        0,
                        Main.maxTilesY - 20,
                        mossType);
                }
            }

            return placed;
        }

        /// <summary>
        /// Replaces dirt inside an ellipse with moss. Returns how many tiles were changed.
        /// </summary>
        private static int PlaceMossVein(
            int centerX,
            int centerY,
            int radiusX,
            int radiusY,
            int minY,
            int maxY,
            ushort mossType)
        {
            int placed = 0;

            for (int i = centerX - radiusX; i <= centerX + radiusX; i++)
            {
                for (int j = centerY - radiusY; j <= centerY + radiusY; j++)
                {
                    if (!WorldGen.InWorld(i, j, 20) || j < minY || j >= maxY)
                        continue;

                    float dx = (i - centerX) / (float)radiusX;
                    float dy = (j - centerY) / (float)radiusY;

                    if (dx * dx + dy * dy > 1f)
                        continue;

                    // Randomly skip some tiles for a rough edge.
                    if (WorldGen.genRand.NextBool(SkipChance))
                        continue;

                    Tile tile = Main.tile[i, j];

                    if (!tile.HasTile || tile.TileType != TileID.Dirt)
                        continue;

                    // Swap the tile type directly. PlaceTile on an already-occupied tile
                    // is unreliable, and this keeps walls and other data intact.
                    tile.TileType = mossType;
                    WorldGen.SquareTileFrame(i, j, true);
                    placed++;
                }
            }

            return placed;
        }
    }
}