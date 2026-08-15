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
    public class HighwayGeneration : ModSystem
    {
        public static Rectangle HighwayArea;

        public override void ModifyWorldGenTasks(List<GenPass> tasks, ref double totalWeight)
        {
            int passIndex = tasks.FindIndex(genpass => genpass.Name.Equals("Micro Biomes"));
            if (passIndex != -1)
            {
                tasks.Insert(passIndex + 1, new PassLegacy("ShatteredIllusion Highway Structure", (progress, config) =>
                {
                    GenerateHighwayStructure(progress, config);
                }));
            }
        }

        private void GenerateHighwayStructure(GenerationProgress progress, GameConfiguration config)
        {
            progress.Message = "Generating Highway Structure...";

            Rectangle desertBounds = GenVars.UndergroundDesertLocation;

            if (desertBounds.Width == 0 || desertBounds.Height == 0)
            {
                return;
            }

            int desertCenterX = desertBounds.X + (desertBounds.Width / 2);
            int structureWidth = 328;
            int structureHeight = 60;

            int targetX = desertCenterX - (structureWidth / 2) - 1; // so i dont forget this is x offset

            int startY = desertBounds.Y;
            int endY = desertBounds.Y + desertBounds.Height;
            int targetY = endY;

            for (int y = endY; y > startY; y--)
            {
                Tile tile = Main.tile[desertCenterX, y];
                if (tile.HasTile && (tile.TileType == TileID.Sandstone || tile.TileType == TileID.HardenedSand))
                {
                    targetY = y - 40; //this is Y 
                    break;
                }
            }

            string structurePath = "World/Structures/Highway.shstruct";
            Point16 position = new Point16(targetX, targetY);

            StructureHelper.API.Generator.GenerateStructure(structurePath, position, ModContent.GetInstance<ShatteredIllusion>());

            HighwayArea = new Rectangle(targetX, targetY, structureWidth, structureHeight);
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
        public override void KillTile(int i, int j, int type, ref bool fail, ref bool effectOnly, ref bool noItem)
        {
            if (!HighwayGeneration.HighwayArea.IsEmpty && HighwayGeneration.HighwayArea.Contains(i, j))
            {
                Player player = Main.LocalPlayer;
                Item item = player.HeldItem;

                int pickPower = (item != null && item.pick > 0) ? item.pick : 0;

                if (pickPower < 100)
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

    public class HighwayProtectionWall : GlobalWall
    {
        public override void KillWall(int i, int j, int type, ref bool fail)
        {
            if (!HighwayGeneration.HighwayArea.IsEmpty && HighwayGeneration.HighwayArea.Contains(i, j))
            {
                Player player = Main.LocalPlayer;
                Item item = player.HeldItem;

                int pickPower = (item != null && item.pick > 0) ? item.pick : 0;

                if (pickPower < 100)
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