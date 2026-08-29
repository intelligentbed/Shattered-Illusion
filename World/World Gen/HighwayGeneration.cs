using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.Generation;
using Terraria.ID;
using Terraria.IO;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.WorldBuilding;

namespace ShatteredIllusion.World.World_Gen
{
    public class HighwayGeneration : ModSystem //OM GHTIS CODE IS SO ASS WHOEVER MADE IT I HATE YOU AND IT FUCKING CRASHES AND IVE BEEN HERE FOR HOURS FIXING 
    {
        public static Rectangle HighwayArea;

        private const int StructureWidth = 329;
        private const int StructureHeight = 46;

        public override void ModifyWorldGenTasks(List<GenPass> tasks, ref double totalWeight)
        {
            // Run AFTER all decoration passes (chests, pots, houses, statues, life crystals, etc.)
            // but BEFORE liquids settle, so nothing gets placed inside the structure afterward.
            int passIndex = tasks.FindIndex(genpass => genpass.Name.Equals("Settle Liquids"));

            if (passIndex == -1)
            {
                for (int i = 0; i < tasks.Count; i++)
                {
                    ModContent.GetInstance<ShatteredIllusion>().Logger.Info($"[GenPass {i}] {tasks[i].Name}");
                }

                passIndex = tasks.Count - 1; 
            }

            tasks.Insert(
                passIndex,
                new PassLegacy("ShatteredIllusion Highway Structure", (progress, config) =>
                {
                    GenerateHighwayStructure(progress, config);
                })
            );
        }

        private void GenerateHighwayStructure(GenerationProgress progress, GameConfiguration config)
        {
            progress.Message = "Generating Highway Structure...";

            int targetX = Main.maxTilesX / 2 - StructureWidth / 2;
            int targetY = Main.maxTilesY / 3;

            Rectangle desertBounds = GenVars.UndergroundDesertLocation;

            if (desertBounds.Width > 0 && desertBounds.Height > 0)
            {
                int desertCenterX = Utils.Clamp(desertBounds.X + desertBounds.Width / 2, 50, Main.maxTilesX - StructureWidth - 50);
                int startY = Utils.Clamp(desertBounds.Y, 50, Main.maxTilesY - 50);
                int endY = Utils.Clamp(desertBounds.Y + desertBounds.Height - 1, 50, Main.maxTilesY - 50);

                int deepestSolidY = -1;

                for (int y = startY; y <= endY; y++)
                {
                    Tile tile = Framing.GetTileSafely(desertCenterX, y);

                    if (tile.HasTile && (tile.TileType == TileID.Sandstone || tile.TileType == TileID.HardenedSand))
                    {
                        deepestSolidY = y;
                    }
                }

                if (deepestSolidY != -1)
                {
                    targetX = desertCenterX - StructureWidth / 2;
                    targetY = Utils.Clamp(deepestSolidY - StructureHeight - 5, 50, Main.maxTilesY - StructureHeight - 50);
                }
                else
                {
                    targetY = Utils.Clamp(desertBounds.Y + desertBounds.Height / 2 - StructureHeight / 2, 50, Main.maxTilesY - StructureHeight - 50);
                }
            }

            targetX = Utils.Clamp(targetX, 50, Main.maxTilesX - StructureWidth - 50);
            targetY = Utils.Clamp(targetY, 50, Main.maxTilesY - StructureHeight - 50);

            Point16 position = new Point16(targetX, targetY);

            try
            {
                StructureHelper.API.Generator.GenerateStructure(
                    "World/Structures/AntlionBossArena.shstruct",
                    position,
                    ModContent.GetInstance<ShatteredIllusion>()
                );
            }
            catch (System.Exception ex)
            {
                ModContent.GetInstance<ShatteredIllusion>().Logger.Error(
                    $"Highway structure placement FAILED at ({targetX},{targetY}) size {StructureWidth}x{StructureHeight}: {ex}"
                );
            }

            HighwayArea = new Rectangle(targetX, targetY, StructureWidth, StructureHeight);
        }

        public override void SaveWorldData(TagCompound tag)
        {
            tag["HighwayAreaX"] = HighwayArea.X;
            tag["HighwayAreaY"] = HighwayArea.Y;
            tag["HighwayAreaWidth"] = HighwayArea.Width;
            tag["HighwayAreaHeight"] = HighwayArea.Height;
        }

        public override void LoadWorldData(TagCompound tag)
        {
            int x = tag.GetInt("HighwayAreaX");
            int y = tag.GetInt("HighwayAreaY");
            int w = tag.GetInt("HighwayAreaWidth");
            int h = tag.GetInt("HighwayAreaHeight");

            HighwayArea = new Rectangle(x, y, w, h);
        }

        public override void ClearWorld()
        {
            HighwayArea = Rectangle.Empty;
        }
    }

    #region Highway Protection System

    public class HighwayProtectionTile : GlobalTile
    {
        public override bool CanKillTile(int i, int j, int type, ref bool blockDamaged)
        {
            if (!HighwayGeneration.HighwayArea.IsEmpty && HighwayGeneration.HighwayArea.Contains(i, j))
            {
                Player player = Main.LocalPlayer;
                int pickPower = player?.HeldItem?.pick ?? 0;

                if (pickPower < 100)
                {
                    return false;
                }
            }

            return base.CanKillTile(i, j, type, ref blockDamaged);
        }

        public override bool CanExplode(int i, int j, int type)
        {
            if (!HighwayGeneration.HighwayArea.IsEmpty && HighwayGeneration.HighwayArea.Contains(i, j))
            {
                return false;
            }

            return base.CanExplode(i, j, type);
        }
    }

    public class HighwayProtectionWall : GlobalWall
    {
        public override void KillWall(int i, int j, int type, ref bool fail)
        {
            if (!HighwayGeneration.HighwayArea.IsEmpty && HighwayGeneration.HighwayArea.Contains(i, j))
            {
                Player player = Main.LocalPlayer;
                int hammerPower = player?.HeldItem?.hammer ?? 0;

                if (hammerPower < 100)
                {
                    fail = true;
                }
            }
        }

        public override bool CanExplode(int i, int j, int type)
        {
            if (!HighwayGeneration.HighwayArea.IsEmpty && HighwayGeneration.HighwayArea.Contains(i, j))
            {
                return false;
            }

            return base.CanExplode(i, j, type);
        }
    }

    #endregion
}