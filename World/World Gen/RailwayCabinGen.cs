using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.Generation;
using Terraria.ModLoader;
using Terraria.WorldBuilding;
using ShatteredIllusion.World.World_Gen; 

namespace ShatteredIllusion.World.World_Gen
{
    public class RailwayCabinGen : ModSystem
    {
        private static readonly string[] CabinNames =
        {
            "railcabin1", "railcabin2", "railcabin3", "railcabin4",
            "railcabin5", "railcabin6", "railcabin7"
        };

        private const string StructurePath = "World/Structures/RailwayCabins/";

        private const int YJitter = 12;
        private const int SlotPadding = 40;

        public override void ModifyWorldGenTasks(List<GenPass> tasks, ref double totalWeight)
        {
            int passIndex = tasks.FindIndex(genpass => genpass.Name.Equals("ShatteredIllusion Highway Structure"));

            if (passIndex == -1)
            {
                passIndex = tasks.FindIndex(genpass => genpass.Name.Equals("Settle Liquids"));
                if (passIndex == -1)
                {
                    passIndex = tasks.Count - 1;
                }
            }
            else
            {
                passIndex += 1; 
            }

            tasks.Insert(
                passIndex,
                new PassLegacy("ShatteredIllusion Railway Cabins", (progress, config) =>
                {
                    GenerateCabins(progress, config);
                })
            );
        }

        private void GenerateCabins(GenerationProgress progress, Terraria.IO.GameConfiguration config)
        {
            progress.Message = "Generating Railway Cabins...";

            // matches the y level of the highway
            int baseY = !HighwayGeneration.HighwayArea.IsEmpty
                ? HighwayGeneration.HighwayArea.Y
                : Main.maxTilesY / 3;

            // little offset of for the ocean changes 
            int leftBound = GenVars.leftBeachEnd > 0 ? GenVars.leftBeachEnd : 400;
            int rightBound = GenVars.rightBeachStart > 0 ? GenVars.rightBeachStart : Main.maxTilesX - 400;

            leftBound += 100;
            rightBound -= 100;

            if (rightBound <= leftBound)
            {
                leftBound = 400;
                rightBound = Main.maxTilesX - 400;
            }

            int usableWidth = rightBound - leftBound;
            int slotWidth = usableWidth / CabinNames.Length;

            for (int i = 0; i < CabinNames.Length; i++)
            {
                int slotStart = leftBound + i * slotWidth;
                int slotEnd = slotStart + slotWidth;

                int safeSlotStart = slotStart + SlotPadding;
                int safeSlotEnd = slotEnd - SlotPadding;

                if (safeSlotEnd <= safeSlotStart)
                {
                    safeSlotStart = slotStart;
                    safeSlotEnd = slotEnd;
                }

                int targetX = WorldGen.genRand.Next(safeSlotStart, safeSlotEnd);
                int targetY = baseY + WorldGen.genRand.Next(-YJitter, YJitter + 1);

                targetX = Utils.Clamp(targetX, 50, Main.maxTilesX - 50);
                targetY = Utils.Clamp(targetY, 50, Main.maxTilesY - 50);

                Point16 position = new Point16(targetX, targetY);
                string structurePath = StructurePath + CabinNames[i] + ".shstruct";

                try
                {
                    StructureHelper.API.Generator.GenerateStructure(
                        structurePath,
                        position,
                        ModContent.GetInstance<ShatteredIllusion>()
                    );
                }
                catch (Exception ex)
                {
                    ModContent.GetInstance<ShatteredIllusion>().Logger.Error(
                        $"Cabin '{CabinNames[i]}' placement FAILED at ({targetX},{targetY}): {ex}"
                    );
                }
            }
        }
    }
}