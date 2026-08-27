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
        private const string InsertAfterPassName = "Full Desert";
        private const string CustomPassName = "Shattered Illusion: Open Up Underground Desert";

        private const int TunnelCount = 35;
        private const int TunnelMinSteps = 120;
        private const int TunnelMaxSteps = 260;

        private const double TunnelMinStrength = 2.5;
        private const double TunnelMaxStrength = 5.5;
        private const double TurnAmount = 0.35;

        private const int StepsBetweenBranches = 40;
        private const float BranchChance = 0.5f;

        private const int EdgeMargin = 15;

        public override void ModifyWorldGenTasks(List<GenPass> tasks, ref double totalWeight)
        {
            int insertAfterIndex = tasks.FindIndex(
                genPass => genPass.Name.Equals(InsertAfterPassName)
            );

            if (insertAfterIndex == -1)
            {
                Mod.Logger.Warn(
                    $"[DesertRework] Could not find '{InsertAfterPassName}' gen pass; underground desert rework was skipped."
                );

                return;
            }

            tasks.Insert(
                insertAfterIndex + 1,
                new PassLegacy(CustomPassName, CarveSpaghettiNetwork)
            );
        }

        private static void CarveSpaghettiNetwork(
            GenerationProgress progress,
            GameConfiguration configuration)
        {
            progress.Message = "Winding tunnels through the sand...";

            Rectangle area = GenVars.UndergroundDesertLocation;

            if (area.Width <= 0 || area.Height <= 0)
            {
                ModContent.GetInstance<ShatteredIllusion>().Logger.Warn(
                    "[DesertRework] Underground Desert bounds not found, skipping cave rework."
                );

                return;
            }

            int tunnelsCarved = 0;
            int branchesCarved = 0;

            for (int i = 0; i < TunnelCount; i++)
            {
                Point start = RandomPointInArea(area);
                double angle = WorldGen.genRand.NextDouble() * MathHelper.TwoPi;
                int steps = WorldGen.genRand.Next(TunnelMinSteps, TunnelMaxSteps + 1);

                CarveWorm(
                    area,
                    start.X,
                    start.Y,
                    angle,
                    steps,
                    ref branchesCarved
                );

                tunnelsCarved++;
            }
        }

        private static Point RandomPointInArea(Rectangle area)
        {
            int x = area.X + WorldGen.genRand.Next(area.Width);
            int y = area.Y + WorldGen.genRand.Next(area.Height);

            return new Point(x, y);
        }

        private static void CarveWorm(
            Rectangle area,
            int startX,
            int startY,
            double startAngle,
            int steps,
            ref int branchesCarved)
        {
            double x = startX;
            double y = startY;
            double angle = startAngle;

            double strength = TunnelMinStrength +
                WorldGen.genRand.NextDouble() *
                (TunnelMaxStrength - TunnelMinStrength);

            for (int step = 0; step < steps; step++)
            {
                if (!IsWellInsideArea(area, (int)x, (int)y))
                    break;

                CarveCircle((int)x, (int)y, strength);

                angle += (WorldGen.genRand.NextDouble() * 2 - 1) * TurnAmount;

                x += Math.Cos(angle);
                y += Math.Sin(angle);

                if (step > 0 &&
                    step % StepsBetweenBranches == 0 &&
                    WorldGen.genRand.NextFloat() < BranchChance)
                {
                    double branchTurn =
                        -0.3 + WorldGen.genRand.NextDouble() * 0.6;

                    double branchAngle =
                        angle +
                        (WorldGen.genRand.NextBool() ? 1 : -1) *
                        (MathHelper.PiOver2 + branchTurn);

                    int branchSteps = WorldGen.genRand.Next(
                        TunnelMinSteps / 2,
                        TunnelMaxSteps / 2
                    );

                    // Branches cannot create their own branches preventing exponential growth (trust me its happends before)
                    CarveWormNoBranching(
                        area,
                        (int)x,
                        (int)y,
                        branchAngle,
                        branchSteps
                    );

                    branchesCarved++;
                }
            }
        }

        private static void CarveWormNoBranching(
            Rectangle area,
            int startX,
            int startY,
            double startAngle,
            int steps)
        {
            double x = startX;
            double y = startY;
            double angle = startAngle;

            double minStrength = TunnelMinStrength * 0.7;
            double maxStrength = TunnelMaxStrength * 0.7;

            double strength = minStrength +
                WorldGen.genRand.NextDouble() *
                (maxStrength - minStrength);

            for (int step = 0; step < steps; step++)
            {
                if (!IsWellInsideArea(area, (int)x, (int)y))
                    break;

                CarveCircle((int)x, (int)y, strength);

                angle += (WorldGen.genRand.NextDouble() * 2 - 1) * TurnAmount;

                x += Math.Cos(angle);
                y += Math.Sin(angle);
            }
        }

        private static bool IsWellInsideArea(Rectangle area, int x, int y)
        {
            return x > area.X + EdgeMargin
                && x < area.X + area.Width - EdgeMargin
                && y > area.Y + EdgeMargin
                && y < area.Y + area.Height - EdgeMargin
                && WorldGen.InWorld(x, y, 10);
        }

        // Only desert-related blocks are removed; existing structures and other tiles are preserved.
        private static readonly HashSet<int> CarvableTileTypes = new HashSet<int>
        {
            TileID.Sand,
            TileID.HardenedSand,
            TileID.Sandstone,
            TileID.CorruptSandstone,
            TileID.CrimsonSandstone
        };

        private static void CarveCircle(
            int centerX,
            int centerY,
            double radius)
        {
            int radiusTiles = (int)Math.Ceiling(radius);

            for (int dx = -radiusTiles; dx <= radiusTiles; dx++)
            {
                for (int dy = -radiusTiles; dy <= radiusTiles; dy++)
                {
                    if (dx * dx + dy * dy > radius * radius)
                        continue;

                    int worldX = centerX + dx;
                    int worldY = centerY + dy;

                    if (!WorldGen.InWorld(worldX, worldY, 10))
                        continue;

                    Tile tile = Main.tile[worldX, worldY];

                    if (!tile.HasTile ||
                        !CarvableTileTypes.Contains(tile.TileType))
                    {
                        continue;
                    }

                    WorldGen.KillTile(
                        worldX,
                        worldY,
                        false,
                        false,
                        true
                    );
                }
            }
        }
    }
}