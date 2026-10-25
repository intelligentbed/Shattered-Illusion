using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.Generation;
using Terraria.ID;
using Terraria.IO;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.WorldBuilding;

namespace ShatteredIllusion.World.WorldGeneration
{
    public class HighwayGeneration : ModSystem
    {
        public static Rectangle HighwayArea { get; private set; }

        private const int StructureWidth = 329;
        private const int StructureHeight = 46;

        private const int MarkerHalfWidth = 6;

        // Extra tiles around the highway that vanilla structures must also stay out of.
        private const int ReservePadding = 12;

        private const string StructurePath =
            "Content/World/Structures/AntlionBossArena.shstruct";

        // Area chosen during the "reserve" pass. Used by the "place" pass at the end of worldgen.
        private static Rectangle reservedArea = Rectangle.Empty;

        public override void ModifyWorldGenTasks(
            List<GenPass> tasks,
            ref double totalWeight)
        {
            // PASS 1: reserve the area as early as the desert exists, so vanilla
            // structures (underground houses, cabins, chests, micro biomes...) avoid it.
            int reserveIndex = tasks.FindIndex(
                genpass => genpass.Name.Equals("Full Desert"));

            if (reserveIndex != -1)
            {
                reserveIndex++; // after the desert is generated
            }
            else
            {
                ModContent.GetInstance<ShatteredIllusion>().Logger.Warn(
                    "[HighwayGeneration] Could not find 'Full Desert' gen pass. " +
                    "Reserving the highway area before 'Settle Liquids' instead.");

                reserveIndex = tasks.FindIndex(
                    genpass => genpass.Name.Equals("Settle Liquids"));

                if (reserveIndex == -1)
                    reserveIndex = tasks.Count;
            }

            tasks.Insert(
                reserveIndex,
                new PassLegacy(
                    "ShatteredIllusion Highway Reserve",
                    ReserveHighwayArea));

            // PASS 2: place the structure at the very end, so nothing generated afterwards
            // can end up inside it, and anything that slipped in gets cleared out.
            int placeIndex = tasks.FindIndex(
                genpass => genpass.Name.Equals("Final Cleanup"));

            if (placeIndex == -1)
            {
                ModContent.GetInstance<ShatteredIllusion>().Logger.Warn(
                    "[HighwayGeneration] Could not find 'Final Cleanup' gen pass. " +
                    "Placing the highway at the end of world generation.");

                for (int i = 0; i < tasks.Count; i++)
                {
                    ModContent.GetInstance<ShatteredIllusion>().Logger.Info(
                        $"[GenPass {i}] {tasks[i].Name}");
                }

                placeIndex = tasks.Count;
            }

            tasks.Insert(
                placeIndex,
                new PassLegacy(
                    "ShatteredIllusion Highway Structure",
                    GenerateHighwayStructure));
        }

        /// <summary>
        /// Works out where the highway goes (same logic as before).
        /// </summary>
        private static Point16 ComputePlacement()
        {
            int targetX = Main.maxTilesX / 2 - StructureWidth / 2;
            int targetY = Main.maxTilesY / 3;

            Rectangle desertBounds = GenVars.UndergroundDesertLocation;

            if (desertBounds.Width > 0 && desertBounds.Height > 0)
            {
                int desertCenterX = Utils.Clamp(
                    desertBounds.X + desertBounds.Width / 2,
                    50,
                    Main.maxTilesX - StructureWidth - 50);

                int startY = Utils.Clamp(
                    desertBounds.Y,
                    50,
                    Main.maxTilesY - 50);

                int endY = Utils.Clamp(
                    desertBounds.Y + desertBounds.Height - 1,
                    50,
                    Main.maxTilesY - 50);

                int deepestSolidY = -1;

                for (int y = startY; y <= endY; y++)
                {
                    Tile tile = Framing.GetTileSafely(desertCenterX, y);

                    if (tile.HasTile &&
                        (tile.TileType == TileID.Sandstone ||
                         tile.TileType == TileID.HardenedSand))
                    {
                        deepestSolidY = y;
                    }
                }

                if (deepestSolidY != -1)
                {
                    targetX = desertCenterX - StructureWidth / 2;

                    targetY = Utils.Clamp(
                        deepestSolidY - StructureHeight - 5,
                        50,
                        Main.maxTilesY - StructureHeight - 50);
                }
                else
                {
                    targetY = Utils.Clamp(
                        desertBounds.Y +
                        desertBounds.Height / 2 -
                        StructureHeight / 2,
                        50,
                        Main.maxTilesY - StructureHeight - 50);
                }
            }

            targetX = Utils.Clamp(
                targetX,
                50,
                Main.maxTilesX - StructureWidth - 50);

            targetY = Utils.Clamp(
                targetY,
                50,
                Main.maxTilesY - StructureHeight - 50);

            return new Point16(targetX, targetY);
        }

        private static void ReserveHighwayArea(
            GenerationProgress progress,
            GameConfiguration config)
        {
            progress.Message = "Reserving space for the highway...";

            Point16 position = ComputePlacement();

            reservedArea = new Rectangle(
                position.X,
                position.Y,
                StructureWidth,
                StructureHeight);

            // Vanilla structure generators check this map before placing anything.
            GenVars.structures.AddProtectedStructure(reservedArea, ReservePadding);
        }

        private static void GenerateHighwayStructure(
            GenerationProgress progress,
            GameConfiguration config)
        {
            progress.Message = "Generating Highway Structure...";

            HighwayArea = Rectangle.Empty;

            // If the reserve pass didn't run or came out empty, compute it now.
            if (reservedArea.IsEmpty)
            {
                Point16 fallback = ComputePlacement();

                reservedArea = new Rectangle(
                    fallback.X,
                    fallback.Y,
                    StructureWidth,
                    StructureHeight);
            }

            int targetX = reservedArea.X;
            int targetY = reservedArea.Y;
            Point16 position = new Point16(targetX, targetY);

            try
            {
                // Remove anything that generated here anyway (mod structures, chests, caves,
                // liquids, etc.) so the highway is always clean.
                ClearArea(reservedArea);

                StructureHelper.API.Generator.GenerateStructure(
                    StructurePath,
                    position,
                    ModContent.GetInstance<ShatteredIllusion>());
            }
            catch (Exception ex)
            {
                ModContent.GetInstance<ShatteredIllusion>().Logger.Error(
                    $"[HighwayGeneration] CRITICAL: Highway structure placement FAILED " +
                    $"at ({targetX}, {targetY}) with size " +
                    $"{StructureWidth}x{StructureHeight}.\n{ex}");

                HighwayArea = Rectangle.Empty;

                // Do not generate the surface marker.
                return;
            }

            HighwayArea = new Rectangle(
                targetX,
                targetY,
                StructureWidth,
                StructureHeight);

            progress.Message = "Marking the surface above the highway...";

            int highwayCenterX = targetX + StructureWidth / 2;

            try
            {
                GenerateSurfaceMarker(highwayCenterX);
            }
            catch (Exception ex)
            {
                ModContent.GetInstance<ShatteredIllusion>().Logger.Error(
                    $"[HighwayGeneration] Highway surface marker FAILED " +
                    $"above centerX {highwayCenterX}.\n{ex}");
            }
        }

        /// <summary>
        /// Wipes tiles, walls, liquids, wires, chests and signs inside the area.
        /// </summary>
        private static void ClearArea(Rectangle area)
        {
            for (int i = 0; i < Main.maxChests; i++)
            {
                Chest chest = Main.chest[i];

                if (chest != null && area.Contains(chest.x, chest.y))
                    Main.chest[i] = null;
            }

            for (int i = 0; i < Main.sign.Length; i++)
            {
                Sign sign = Main.sign[i];

                if (sign != null && area.Contains(sign.x, sign.y))
                    Main.sign[i] = null;
            }

            for (int x = area.Left; x < area.Right; x++)
            {
                for (int y = area.Top; y < area.Bottom; y++)
                {
                    if (!WorldGen.InWorld(x, y, 5))
                        continue;

                    Tile tile = Main.tile[x, y];
                    tile.ClearEverything();
                }
            }
        }

        private static void GenerateSurfaceMarker(int centerX)
        {
            for (int dx = -MarkerHalfWidth; dx <= MarkerHalfWidth; dx++)
            {
                int x = centerX + dx;

                if (!WorldGen.InWorld(x, 0, 20))
                    continue;

                // Occasionally leave a gap so the marker looks broken.
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

                    WorldGen.PlaceTile(
                        x,
                        y,
                        TileID.GrayBrick,
                        mute: true,
                        forced: true);
                }
            }
        }

        private static int FindSurfaceY(int x)
        {
            int startY = Math.Max(
                (int)Main.worldSurface - 60,
                10);

            for (int y = startY; y < Main.maxTilesY - 50; y++)
            {
                Tile tile = Framing.GetTileSafely(x, y);

                if (tile.HasTile &&
                    Main.tileSolid[tile.TileType])
                {
                    return y;
                }
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
            int width = tag.GetInt("HighwayAreaWidth");
            int height = tag.GetInt("HighwayAreaHeight");

            HighwayArea = new Rectangle(
                x,
                y,
                width,
                height);
        }

        public override void ClearWorld()
        {
            HighwayArea = Rectangle.Empty;
            reservedArea = Rectangle.Empty;
        }

        public override void NetSend(BinaryWriter writer)
        {
            writer.Write(HighwayArea.X);
            writer.Write(HighwayArea.Y);
            writer.Write(HighwayArea.Width);
            writer.Write(HighwayArea.Height);
        }

        public override void NetReceive(BinaryReader reader)
        {
            int x = reader.ReadInt32();
            int y = reader.ReadInt32();
            int width = reader.ReadInt32();
            int height = reader.ReadInt32();

            HighwayArea = new Rectangle(x, y, width, height);
        }
    }

    #region Highway Protection System

    public class HighwayProtectionTile : GlobalTile
    {
        public override bool CanKillTile(
            int i,
            int j,
            int type,
            ref bool blockDamaged)
        {
            if (!HighwayGeneration.HighwayArea.IsEmpty &&
                HighwayGeneration.HighwayArea.Contains(i, j))
            {
                return false;
            }

            return base.CanKillTile(
                i,
                j,
                type,
                ref blockDamaged);
        }

        public override bool CanExplode(
            int i,
            int j,
            int type)
        {
            if (!HighwayGeneration.HighwayArea.IsEmpty &&
                HighwayGeneration.HighwayArea.Contains(i, j))
            {
                return false;
            }

            return base.CanExplode(i, j, type);
        }
    }

    public class HighwayProtectionWall : GlobalWall
    {
        public override void KillWall(
            int i,
            int j,
            int type,
            ref bool fail)
        {
            if (!HighwayGeneration.HighwayArea.IsEmpty &&
                HighwayGeneration.HighwayArea.Contains(i, j))
            {
                fail = true;
            }
        }

        public override bool CanExplode(
            int i,
            int j,
            int type)
        {
            if (!HighwayGeneration.HighwayArea.IsEmpty &&
                HighwayGeneration.HighwayArea.Contains(i, j))
            {
                return false;
            }

            return base.CanExplode(i, j, type);
        }
    }

    #endregion
}