using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.Generation;
using Terraria.IO;
using Terraria.ModLoader;
using Terraria.WorldBuilding;

namespace ShatteredIllusion.World.WorldGeneration
{
    public class StarHouseGen : ModSystem
    {
        private const string StructurePath = "World/Structures/starhouse.shstruct";

        private const int MinCount = 2;
        private const int MaxCount = 6;

        private const float SkyBandTopFraction = 0.10f;
        private const float SkyBandBottomFraction = 0.35f;

        private const int MinHorizontalGap = 300;

        public override void ModifyWorldGenTasks(List<GenPass> tasks, ref double totalWeight)
        {
            int passIndex = tasks.FindIndex(genpass => genpass.Name.Equals("Settle Liquids"));

            if (passIndex == -1)
            {
                passIndex = tasks.Count - 1;
            }

            tasks.Insert(
                passIndex,
                new PassLegacy("ShatteredIllusion Star Houses", (progress, config) =>
                {
                    GenerateStarHouses(progress, config);
                })
            );
        }

        private void GenerateStarHouses(GenerationProgress progress, GameConfiguration config)
        {
            progress.Message = "Generating Star Houses...";

            int leftBound = GenVars.leftBeachEnd > 0 ? GenVars.leftBeachEnd : 400;
            int rightBound = GenVars.rightBeachStart > 0 ? GenVars.rightBeachStart : Main.maxTilesX - 400;

            leftBound += 100;
            rightBound -= 100;

            if (rightBound <= leftBound)
            {
                leftBound = 400;
                rightBound = Main.maxTilesX - 400;
            }

            int skyTop = (int)(Main.worldSurface * SkyBandTopFraction);
            int skyBottom = (int)(Main.worldSurface * SkyBandBottomFraction);

            int count = WorldGen.genRand.Next(MinCount, MaxCount + 1);
            List<int> usedX = new List<int>();

            for (int i = 0; i < count; i++)
            {
                int targetX = -1;
                int attempts = 0;

                // Try to find an X that's far enough from any previously placed star house
                while (attempts < 30)
                {
                    int candidate = WorldGen.genRand.Next(leftBound, rightBound);
                    bool tooClose = false;

                    foreach (int existingX in usedX)
                    {
                        if (Math.Abs(candidate - existingX) < MinHorizontalGap)
                        {
                            tooClose = true;
                            break;
                        }
                    }

                    if (!tooClose)
                    {
                        targetX = candidate;
                        break;
                    }

                    attempts++;
                }

                if (targetX == -1)
                {
                    // Couldn't find a spaced-out spot after 30 tries, just place it anyway
                    targetX = WorldGen.genRand.Next(leftBound, rightBound);
                }

                usedX.Add(targetX);

                int targetY = WorldGen.genRand.Next(skyTop, skyBottom);

                targetX = Utils.Clamp(targetX, 50, Main.maxTilesX - 50);
                targetY = Utils.Clamp(targetY, 50, Main.maxTilesY - 50);

                Point16 position = new Point16(targetX, targetY);

                try
                {
                    StructureHelper.API.Generator.GenerateStructure(
                        StructurePath,
                        position,
                        ModContent.GetInstance<ShatteredIllusion>()
                    );
                }
                catch (Exception ex)
                {
                    ModContent.GetInstance<ShatteredIllusion>().Logger.Error(
                        $"Star House placement FAILED at ({targetX},{targetY}): {ex}"
                    );
                }
            }
        }
    }
}