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

            // Use the ACTUAL final center of the placed structure, not the earlier guess,
            // so the surface marker always lines up with where the highway really ended up.
            progress.Message = "Marking the surface above the highway...";

            try
            {
                GenerateSurfaceMarker(targetX + StructureWidth / 2);
            }
            catch (System.Exception ex)
            {
                ModContent.GetInstance<ShatteredIllusion>().Logger.Error(
                    $"Highway surface marker FAILED above centerX {targetX + StructureWidth / 2}: {ex}"
                );
            }
        }

        // A jagged strip of broken ancient roadway breaching the dunes directly above
        // the buried highway. Deliberately uneven (random height + occasional gaps) so
        // it reads as ruins that eroded/cracked over time, not a placed marker.
        private const int MarkerHalfWidth = 6;

        private void GenerateSurfaceMarker(int centerX)
        {
            for (int dx = -MarkerHalfWidth; dx <= MarkerHalfWidth; dx++)
            {
                int x = centerX + dx;

                if (!WorldGen.InWorld(x, 0, 20))
                    continue;

                // Occasional missing slab breaks up the line so it looks weathered/broken
                // rather than a solid, obviously man-made bar.
                if (WorldGen.genRand.NextFloat() < 0.15f)
                    continue;

                int groundY = FindSurfaceY(x);

                if (groundY == -1)
                    continue;

                int slabHeight = WorldGen.genRand.Next(2, 5);

                for (int dy = 0; dy < slabHeight; dy++)
                {
                    int y = groundY - dy;

                    if (!WorldGen.InWorld(x, y, 20))
                        continue;

                    WorldGen.PlaceTile(x, y, TileID.GrayBrick, mute: true, forced: true);
                }
            }
        }

        // Scans down from just above the world's surface line to find the first solid
        // tile at a given x - i.e. the actual ground level a player would walk on.
        private static int FindSurfaceY(int x)
        {
            int startY = System.Math.Max((int)Main.worldSurface - 60, 10);

            for (int y = startY; y < Main.maxTilesY - 50; y++)
            {
                Tile tile = Framing.GetTileSafely(x, y);

                if (tile.HasTile && Main.tileSolid[tile.TileType])
                    return y;
            }

            return -1;
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