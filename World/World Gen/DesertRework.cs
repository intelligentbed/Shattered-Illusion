using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.Generation;
using Terraria.ID;
using Terraria.IO;
using Terraria.ModLoader;
using Terraria.WorldBuilding;

namespace ShatteredIllusion.World.World_Gen
{
    public class DesertRework : ModSystem
    {
        private const string VanillaPassName = "Full Desert";
        private const string ReworkPassName = "Shattered Illusion: Rebuild Underground Desert";

        private const int BorderMargin = 12;
        private const int TopSectionRatio = 5;

        public override void ModifyWorldGenTasks(List<GenPass> tasks, ref double totalWeight)
        {
            int index = tasks.FindIndex(pass => pass.Name.Equals(VanillaPassName));

            if (index == -1)
            {
                Mod.Logger.Warn(
                    $"[DesertRework] Could not find '{VanillaPassName}' gen pass; underground desert rework was skipped.");
                return;
            }

            tasks.Insert(index + 1, new PassLegacy(ReworkPassName, RebuildUndergroundDesert));
        }

        private static void RebuildUndergroundDesert(
            GenerationProgress progress,
            GameConfiguration configuration)
        {
            Rectangle vanillaArea = GenVars.UndergroundDesertLocation;

            if (!IsValidArea(vanillaArea))
            {
                ModContent.GetInstance<ShatteredIllusion>().Logger.Warn(
                    "[DesertRework] Underground Desert bounds were invalid; rework was skipped.");
                return;
            }

            Rectangle area = GetUndergroundArea(vanillaArea);

            progress.Message = "Mapping underground desert layout...";
            bool[,] mask = BuildDesertMask(area);

            progress.Message = "Preserving vanilla desert structure...";
            PreserveVanillaInterior(area, mask);

            progress.Message = "Sculpting sandstone formations...";
            PlaceSandstoneFormations(area, mask);

            progress.Message = "Adding layered desert strata...";
            ApplySandstormStriations(area, mask);

            progress.Message = "Carving fractured canyon systems...";
            CarveCanyonSystem(area);

            progress.Message = "Excavating underground caves...";
            CarveOrganicPockets(area, mask);

            progress.Message = "Connecting underground passages...";
            AddGroundingConnections(area);
        }

        private static bool[,] BuildDesertMask(Rectangle area)
        {
            bool[,] mask = new bool[area.Width, area.Height];

            for (int x = area.Left; x < area.Right; x++)
            {
                for (int y = area.Top; y < area.Bottom; y++)
                {
                    if (!WorldGen.InWorld(x, y, 20))
                        continue;

                    Tile tile = Main.tile[x, y];

                    if (tile.HasTile && IsDesertTile(tile.TileType))
                        mask[x - area.Left, y - area.Top] = true;
                }
            }

            return mask;
        }

        private static void PreserveVanillaInterior(Rectangle area, bool[,] mask)
        {
            for (int x = area.Left; x < area.Right; x++)
            {
                for (int y = area.Top; y < area.Bottom; y++)
                {
                    if (!IsMasked(area, mask, x, y))
                        continue;

                    Tile tile = Main.tile[x, y];

                    if (!tile.HasTile)
                        WorldGen.PlaceTile(x, y, TileID.Sand, mute: true, forced: true);
                }
            }
        }

        private static void PlaceSandstoneFormations(Rectangle area, bool[,] mask)
        {
            const int majorCount = 20;
            const int minorCount = 30;

            int topBoundary = area.Height / TopSectionRatio;

            for (int i = 0; i < majorCount; i++)
            {
                Point center = RandomMaskedPoint(area, mask, 35);

                if (center.Y < area.Top + topBoundary + 20)
                    continue;

                ApplyBlobFormation(
                    area,
                    mask,
                    center,
                    WorldGen.genRand.Next(45, 95),
                    WorldGen.genRand.Next(20, 50),
                    WorldGen.genRand.NextFloat(0.04f, 0.1f),
                    TileID.Sandstone);
            }

            for (int i = 0; i < minorCount; i++)
            {
                Point center = RandomMaskedPoint(area, mask, 25);

                if (center.Y < area.Top + topBoundary + 15)
                    continue;

                ushort tileType = WorldGen.genRand.NextBool()
                    ? TileID.Sandstone
                    : TileID.HardenedSand;

                ApplyBlobFormation(
                    area,
                    mask,
                    center,
                    WorldGen.genRand.Next(18, 42),
                    WorldGen.genRand.Next(12, 28),
                    WorldGen.genRand.NextFloat(0.1f, 0.22f),
                    tileType);
            }
        }

        private static void ApplyBlobFormation(
            Rectangle area,
            bool[,] mask,
            Point center,
            int radiusX,
            int radiusY,
            double waveModifier,
            ushort tileType)
        {
            for (int dx = -radiusX; dx <= radiusX; dx++)
            {
                for (int dy = -radiusY; dy <= radiusY; dy++)
                {
                    double normalizedX = dx / (double)radiusX;
                    double normalizedY = dy / (double)radiusY;

                    double noise =
                        1.0 +
                        Math.Sin(dx * waveModifier * 1.5 + dy * waveModifier) * 0.18 +
                        Math.Cos(dy * waveModifier * 1.2 - dx * waveModifier) * 0.15;

                    if (normalizedX * normalizedX + normalizedY * normalizedY > noise)
                        continue;

                    int x = center.X + dx;
                    int y = center.Y + dy;

                    if (!IsInside(area, x, y, 2) || !IsMasked(area, mask, x, y))
                        continue;

                    Tile tile = Main.tile[x, y];

                    if (tile.HasTile && IsDesertTile(tile.TileType))
                        tile.TileType = tileType;
                }
            }
        }

        private static void ApplySandstormStriations(Rectangle area, bool[,] mask)
        {
            const int baseStreakCount = 20;
            const int maxStreakCount = 75;
            const int verticalSpacing = 15;

            int topBoundary = area.Height / TopSectionRatio;
            int usableHeight = area.Height - topBoundary;

            for (int y = area.Top + topBoundary; y < area.Bottom; y += verticalSpacing)
            {
                float depthRatio = MathHelper.Clamp(
                    (float)(y - (area.Top + topBoundary)) / usableHeight,
                    0f,
                    1f);

                int streakCount = (int)MathHelper.Lerp(
                    baseStreakCount,
                    maxStreakCount,
                    depthRatio * depthRatio);

                for (int i = 0; i < streakCount / 4; i++)
                    CreateStriation(area, mask, y, depthRatio);
            }
        }

        private static void CreateStriation(
            Rectangle area,
            bool[,] mask,
            int y,
            float depthRatio)
        {
            Point start = new Point(
                WorldGen.genRand.Next(area.Left + 15, area.Right - 15),
                y + WorldGen.genRand.Next(-10, 10));

            if (!IsMasked(area, mask, start.X, start.Y))
                return;

            int length = WorldGen.genRand.Next(30, 90);
            float angle = MathHelper.PiOver4 + WorldGen.genRand.NextFloat(-0.35f, 0.35f);

            ushort tileType = depthRatio > 0.5f
                ? (WorldGen.genRand.NextBool() ? TileID.Sandstone : TileID.HardenedSand)
                : TileID.HardenedSand;

            Vector2 position = new Vector2(start.X, start.Y);
            Vector2 direction = new Vector2(
                (float)Math.Cos(angle),
                (float)Math.Sin(angle));

            for (int step = 0; step < length; step++)
            {
                position += direction;

                float wave = (float)Math.Sin(step * 0.15f) * 1.5f;
                int x = (int)(position.X + wave);
                int targetY = (int)position.Y;

                if (!IsInside(area, x, targetY, 2))
                    continue;

                int thickness = depthRatio > 0.6f
                    ? WorldGen.genRand.Next(1, 4)
                    : 1;

                PaintDesertArea(
                    area,
                    x,
                    targetY,
                    thickness,
                    tileType);
            }
        }

        private static void CarveCanyonSystem(Rectangle area)
        {
            CarvePrimaryRift(area);

            CarveJaggedBranch(
                area,
                new Vector2(area.Center.X - 15, area.Top + area.Height / 3),
                -0.7f,
                65);

            CarveJaggedBranch(
                area,
                new Vector2(area.Center.X + 15, area.Top + area.Height / 2),
                0.65f,
                55);
        }

        private static void CarvePrimaryRift(Rectangle area)
        {
            Vector2 position = new Vector2(
                area.Center.X,
                area.Top + area.Height / TopSectionRatio + 2);

            int steps = area.Height - area.Height / TopSectionRatio - 20;
            float direction = 0f;

            for (int step = 0; step < steps; step++)
            {
                if (step % 12 == 0)
                    direction = WorldGen.genRand.NextFloat(-1.2f, 1.2f);

                position.X += direction + WorldGen.genRand.NextFloat(-0.8f, 0.8f);
                position.Y += 1f;

                if (!IsInside(area, (int)position.X, (int)position.Y, 4))
                    continue;

                CarveFracture(
                    area,
                    (int)position.X,
                    (int)position.Y,
                    WorldGen.genRand.Next(8, 24),
                    WorldGen.genRand.Next(12, 30));
            }
        }

        private static void CarveJaggedBranch(
            Rectangle area,
            Vector2 startPosition,
            float horizontalDirection,
            int length)
        {
            Vector2 position = startPosition;

            for (int step = 0; step < length; step++)
            {
                if (step % 8 == 0)
                    horizontalDirection *= -1f;

                position.X += horizontalDirection + WorldGen.genRand.NextFloat(-0.5f, 0.5f);
                position.Y += 1f;

                if (!IsInside(area, (int)position.X, (int)position.Y, 4))
                    break;

                CarveFracture(
                    area,
                    (int)position.X,
                    (int)position.Y,
                    WorldGen.genRand.Next(6, 16),
                    WorldGen.genRand.Next(8, 20));
            }
        }

        private static void CarveFracture(
            Rectangle area,
            int centerX,
            int centerY,
            int radiusX,
            int radiusY)
        {
            for (int dx = -radiusX; dx <= radiusX; dx++)
            {
                for (int dy = -radiusY; dy <= radiusY; dy++)
                {
                    if (Math.Abs(dx) +
                        Math.Abs(dy) +
                        WorldGen.genRand.Next(-3, 3) >
                        radiusX + radiusY * 0.5)
                    {
                        continue;
                    }

                    int x = centerX + dx;
                    int y = centerY + dy;

                    if (!IsInside(area, x, y, 1))
                        continue;

                    Tile tile = Main.tile[x, y];

                    if (tile.HasTile)
                        WorldGen.KillTile(x, y, false, false, true);
                }
            }
        }

        private static void CarveOrganicPockets(Rectangle area, bool[,] mask)
        {
            const int pocketCount = 14;

            int minimumY = area.Top + area.Height / TopSectionRatio + 10;

            for (int i = 0; i < pocketCount; i++)
            {
                Point start = RandomMaskedPoint(area, mask, 20);

                if (start.Y < minimumY)
                    continue;

                double angle = WorldGen.genRand.NextDouble() * MathHelper.TwoPi;
                int steps = WorldGen.genRand.Next(35, 70);
                int radius = WorldGen.genRand.Next(6, 13);

                Vector2 position = new Vector2(start.X, start.Y);

                for (int step = 0; step < steps; step++)
                {
                    if (!IsInside(area, (int)position.X, (int)position.Y, 6))
                        break;

                    CarveFracture(
                        area,
                        (int)position.X,
                        (int)position.Y,
                        radius,
                        radius);

                    angle += WorldGen.genRand.NextFloat(-0.35f, 0.35f);

                    position.X += (float)Math.Cos(angle) * 1.8f;
                    position.Y += (float)Math.Sin(angle) * 1.8f;
                }
            }
        }

        private static void AddGroundingConnections(Rectangle area)
        {
            int centerX = area.Center.X;

            CarveTunnel(
                area,
                new Vector2(centerX, area.Top + 2),
                new Vector2(centerX, area.Top + 35),
                7);

            CarveTunnel(
                area,
                new Vector2(centerX, area.Bottom - 30),
                new Vector2(centerX, area.Bottom - 2),
                9);
        }

        private static void CarveTunnel(
            Rectangle area,
            Vector2 start,
            Vector2 target,
            int radius)
        {
            Vector2 position = start;
            int safety = 0;

            while (Vector2.Distance(position, target) > 2f && safety++ < 1000)
            {
                Vector2 direction = target - position;

                if (direction != Vector2.Zero)
                    direction.Normalize();

                position += direction * 1.5f;

                CarveFracture(
                    area,
                    (int)position.X,
                    (int)position.Y,
                    radius,
                    radius);
            }
        }

        private static void PaintDesertArea(
            Rectangle area,
            int centerX,
            int centerY,
            int radius,
            ushort tileType)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    int x = centerX + dx;
                    int y = centerY + dy;

                    if (!IsInside(area, x, y, 2))
                        continue;

                    Tile tile = Main.tile[x, y];

                    if (tile.HasTile && IsDesertTile(tile.TileType))
                        tile.TileType = tileType;
                }
            }
        }

        private static bool IsDesertTile(int tileType)
        {
            return tileType == TileID.Sand ||
                   tileType == TileID.HardenedSand ||
                   tileType == TileID.Sandstone ||
                   tileType == TileID.CorruptSandstone ||
                   tileType == TileID.CrimsonSandstone;
        }

        private static Point RandomMaskedPoint(
            Rectangle area,
            bool[,] mask,
            int margin)
        {
            for (int attempt = 0; attempt < 50; attempt++)
            {
                Point candidate = RandomPoint(area, margin);

                if (IsMasked(area, mask, candidate.X, candidate.Y))
                    return candidate;
            }

            return RandomPoint(area, margin);
        }

        private static Point RandomPoint(Rectangle area, int margin)
        {
            int minX = area.Left + margin;
            int maxX = area.Right - margin;
            int minY = area.Top + margin;
            int maxY = area.Bottom - margin;

            if (maxX <= minX)
                minX = maxX = area.Center.X;

            if (maxY <= minY)
                minY = maxY = area.Center.Y;

            return new Point(
                WorldGen.genRand.Next(minX, maxX + 1),
                WorldGen.genRand.Next(minY, maxY + 1));
        }

        private static Rectangle GetUndergroundArea(Rectangle vanillaArea)
        {
            int top = vanillaArea.Y + vanillaArea.Height / TopSectionRatio;
            int bottom = vanillaArea.Bottom - BorderMargin;

            if (bottom <= top)
                return vanillaArea;

            return new Rectangle(
                vanillaArea.X + BorderMargin,
                top,
                Math.Max(1, vanillaArea.Width - BorderMargin * 2),
                Math.Max(1, bottom - top));
        }

        private static bool IsValidArea(Rectangle area)
        {
            return area.Width > BorderMargin * 2 &&
                   area.Height > BorderMargin * 2 &&
                   WorldGen.InWorld(area.Left + BorderMargin, area.Top + BorderMargin, 20) &&
                   WorldGen.InWorld(area.Right - BorderMargin, area.Bottom - BorderMargin, 20);
        }

        private static bool IsMasked(
            Rectangle area,
            bool[,] mask,
            int x,
            int y)
        {
            int maskX = x - area.Left;
            int maskY = y - area.Top;

            if (maskX < 0 ||
                maskY < 0 ||
                maskX >= mask.GetLength(0) ||
                maskY >= mask.GetLength(1))
            {
                return false;
            }

            return mask[maskX, maskY];
        }

        private static bool IsInside(
            Rectangle area,
            int x,
            int y,
            int margin)
        {
            return x >= area.Left + margin &&
                   x < area.Right - margin &&
                   y >= area.Top + margin &&
                   y < area.Bottom - margin &&
                   WorldGen.InWorld(x, y, 20);
        }
    }
}

