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
        public override void ModifyWorldGenTasks(
            List<GenPass> tasks,
            ref double totalWeight)
        {
            int genIndex = tasks.FindIndex(
                genpass => genpass.Name.Equals("Living Trees")
            );

            if (genIndex == -1)
                return;

            tasks.Insert(
                genIndex + 1,
                new PassLegacy(
                    "Generating Moss",
                    delegate (
                        GenerationProgress progress,
                        GameConfiguration configuration)
                    {
                        progress.Message = "Generating Moss";

                        GenerateMoss(
                            0,
                            0,
                            Main.maxTilesX,
                            Main.maxTilesY
                        );
                    }
                )
            );
        }

        private static void GenerateMoss(int x, int y, int width, int height)
        {
            // The layer 
            int cavernStart = (int)Main.rockLayer;

            int veinCount = (width * height) / 12000;

            for (int vein = 0; vein < veinCount; vein++)
            {
                // PICKS A RANDOM CENTER
                int centerX = WorldGen.genRand.Next(x + 20, x + width - 20);
                int centerY = WorldGen.genRand.Next(
                    cavernStart,
                    y + height - 100
                );

                // Skip if center is out of bounds
                if (centerX < x || centerX >= x + width || centerY < cavernStart || centerY >= y + height)
                    continue;

                // START VEIN IN dirt
                if (!Main.tile[centerX, centerY].HasTile ||
                    Main.tile[centerX, centerY].TileType != TileID.Dirt)
                {
                    continue;
                }

                // VEIN SHAPE
                int radiusX = WorldGen.genRand.Next(2, 5);
                int radiusY = WorldGen.genRand.Next(2, 5);

                for (int i = centerX - radiusX; i <= centerX + radiusX; i++)
                {
                    for (int j = centerY - radiusY; j <= centerY + radiusY; j++)
                    {
                        if (i < x || i >= x + width || j < cavernStart || j >= y + height)
                        {
                            continue;
                        }

                        float dx = (i - centerX) / (float)radiusX;
                        float dy = (j - centerY) / (float)radiusY;

                        if (dx * dx + dy * dy > 1f)
                            continue;

                        // RANDOMLY SKIP SOME TILES
                        if (WorldGen.genRand.NextBool(4))
                            continue;

                        if (Main.tile[i, j].HasTile &&
                            Main.tile[i, j].TileType == TileID.Dirt)
                        {
                            Main.tile[i, j].TileType =
                                (ushort)ModContent.TileType<MossBlock>();
                        }
                    }
                }
            }

            // GENERATE MOSS UNDER LIVING WOOD TREES
            for (int i = x + 20; i < x + width - 20; i++)
            {
                for (int j = y + 20; j < Main.worldSurface; j++)
                {
                    if (!Main.tile[i, j].HasTile ||
                        Main.tile[i, j].TileType != TileID.LivingWood)
                    {
                        continue;
                    }

                    int groundY = j;
                    // Find the ground below the tree
                    while (groundY < y + height - 20 &&
                           Main.tile[i, groundY].HasTile &&
                           Main.tile[i, groundY].TileType == TileID.LivingWood)
                    {
                        groundY++;
                    }

                    if (groundY >= y + height - 20)
                        continue;

                    if (!Main.tile[i, groundY].HasTile ||
                        Main.tile[i, groundY].TileType != TileID.Dirt)
                    {
                        continue;
                    }
                    // Generate moss veins under the tree
                    int treeVeinCount = WorldGen.genRand.Next(3, 7);

                    for (int vein = 0; vein < treeVeinCount; vein++)
                    {
                        int centerX = i + WorldGen.genRand.Next(-8, 9);
                        int centerY = groundY + WorldGen.genRand.Next(0, 12);

                        int radiusX = WorldGen.genRand.Next(2, 5);
                        int radiusY = WorldGen.genRand.Next(2, 5);

                        for (int mossX = centerX - radiusX; mossX <= centerX + radiusX; mossX++)
                        {
                            for (int mossY = centerY - radiusY; mossY <= centerY + radiusY; mossY++)
                            {
                                if (mossX < x || mossX >= x + width || mossY < y || mossY >= y + height)
                                {
                                    continue;
                                }

                                float dx = (mossX - centerX) / (float)radiusX;
                                float dy = (mossY - centerY) / (float)radiusY;

                                if (dx * dx + dy * dy > 1f)
                                    continue;

                                if (WorldGen.genRand.NextBool(4))
                                    continue;
                                // Replace dirt with moss
                                if (Main.tile[mossX, mossY].HasTile &&
                                    Main.tile[mossX, mossY].TileType == TileID.Dirt)
                                {
                                    Main.tile[mossX, mossY].TileType =
                                        (ushort)ModContent.TileType<MossBlock>();
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}