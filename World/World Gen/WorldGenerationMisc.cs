using ShatteredIllusion.Content.Items.Accessories;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent.Generation;
using Terraria.ID;
using Terraria.IO;
using Terraria.ModLoader;
using Terraria.WorldBuilding;

namespace ShatteredIllusion.World.World_Gen
{
    public class WorldGenerationMisc : ModSystem
    {
        private const string InsertAfterPassName = "Buried Chests";
        private const string CustomPassName = "Shattered Illusion: Populate Desert Chests";

        private const float DuneCarapaceChestChance = 0.30f;
        private const int ChestFrameWidth = 36;
        private const int BiomeCheckRadius = 40; 

        public override void ModifyWorldGenTasks(List<GenPass> tasks, ref double totalWeight)
        {
            int insertAfterIndex = tasks.FindIndex(genPass => genPass.Name.Equals(InsertAfterPassName));

            if (insertAfterIndex == -1)
            {
                Mod.Logger.Warn($"[DuneCarapace] Could not find '{InsertAfterPassName}' gen pass; desert chest injection was skipped.");
                return;
            }

            tasks.Insert(insertAfterIndex + 1, new PassLegacy(CustomPassName, InjectDuneCarapace));
        }

        private static void InjectDuneCarapace(GenerationProgress progress, GameConfiguration configuration)
        {
            progress.Message = "Burying secrets in the sand...";

            int chestsScanned = 0;
            int undergroundChests = 0;
            int desertChests = 0;
            int itemsPlaced = 0;

            for (int i = 0; i < Main.maxChests; i++)
            {
                Chest chest = Main.chest[i];
                if (chest == null)
                    continue;

                chestsScanned++;

                if (!IsUnderground(chest.y))
                    continue;

                undergroundChests++;

                if (!IsInDesertBiome(chest.x, chest.y))
                    continue;

                desertChests++;

                if (!WorldGen.genRand.NextBool((int)Math.Round(1f / DuneCarapaceChestChance)))
                    continue;

                if (TryAddItemToChestFirstSlot(chest, ModContent.ItemType<DuneCarapace>(), stack: 1))
                    itemsPlaced++;
            }
        }

        private static bool IsUnderground(int tileY)
        {
            return tileY > Main.worldSurface;
        }

        private static bool IsInDesertBiome(int tileX, int tileY)
        {
            if (!WorldGen.InWorld(tileX, tileY))
                return false;

            int sandCount = 0;
            int checkedTiles = 0;

            for (int x = tileX - BiomeCheckRadius; x <= tileX + BiomeCheckRadius; x += 2)
            {
                for (int y = tileY - BiomeCheckRadius; y <= tileY + BiomeCheckRadius; y += 2)
                {
                    if (!WorldGen.InWorld(x, y))
                        continue;

                    Tile tile = Main.tile[x, y];
                    if (!tile.HasTile)
                        continue;

                    checkedTiles++;

                    if (tile.TileType == TileID.Sand
                        || tile.TileType == TileID.HardenedSand
                        || tile.TileType == TileID.Sandstone
                        || tile.TileType == TileID.CorruptSandstone
                        || tile.TileType == TileID.CrimsonSandstone)
                    {
                        sandCount++;
                    }
                }
            }

            if (checkedTiles == 0)
                return false;

            return sandCount >= 60; 
        }

        private static bool TryAddItemToChestFirstSlot(Chest chest, int itemType, int stack)
        {
            const int targetSlot = 0;

            if (chest.item[targetSlot] != null && !chest.item[targetSlot].IsAir)
            {
                int relocateSlot = -1;
                for (int i = 0; i < chest.item.Length; i++)
                {
                    if (i == targetSlot)
                        continue;

                    if (chest.item[i] == null || chest.item[i].IsAir)
                    {
                        relocateSlot = i;
                        break;
                    }
                }

                if (relocateSlot == -1)
                    return false; 

                chest.item[relocateSlot] = chest.item[targetSlot];
            }

            chest.item[targetSlot] = new Item();
            chest.item[targetSlot].SetDefaults(itemType);
            chest.item[targetSlot].stack = stack;
            return true;
        }
    }
}