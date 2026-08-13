using System.Collections.Generic;
using Terraria;
using Terraria.GameContent.Generation;
using Terraria.ID;
using Terraria.IO;
using Terraria.ModLoader;
using Terraria.WorldBuilding;
using ShatteredIllusion.Placeables.Blocks;

namespace ShatteredIllusion.World
{
    public class MossWorldGen : ModSystem
    {
        public override void ModifyWorldGenTasks(
            List<GenPass> tasks,
            ref double totalWeight)
        {
            int genIndex = tasks.FindIndex(
                genpass => genpass.Name.Equals("Shinies")
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
            int cavernStart = (int)Main.rockLayer;

            int veinCount = (Main.maxTilesX * Main.maxTilesY) / 12000;

            for (int vein = 0; vein < veinCount; vein++)
            {
                // PICK A RANDOM CENTER
                int centerX = WorldGen.genRand.Next(20, Main.maxTilesX - 20);
                int centerY = WorldGen.genRand.Next(
                    cavernStart,
                    Main.maxTilesY - 100
                );

                // START VEIN IN STONE
                if (!Main.tile[centerX, centerY].HasTile ||
                    Main.tile[centerX, centerY].TileType != TileID.Stone)
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
                        if (i < 0 || i >= Main.maxTilesX ||
                            j < cavernStart || j >= Main.maxTilesY)
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
                            Main.tile[i, j].TileType == TileID.Stone)
                        {
                            Main.tile[i, j].TileType =
                                (ushort)ModContent.TileType<MossBlock>();
                        }
                    }
                }
            }
        }
    }
}
