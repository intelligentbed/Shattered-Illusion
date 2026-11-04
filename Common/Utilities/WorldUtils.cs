using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace ShatteredIllusion.Common.Utilities
{
    // Tile and world queries. Everything takes WORLD positions (pixels) unless the name says Tile.
    public static class WorldUtils
    {
        public static bool InWorld(Vector2 world, int fluff = 0) => WorldGen.InWorld((int)(world.X / 16f), (int)(world.Y / 16f), fluff);

        public static Vector2 ClampToWorld(Vector2 world, float margin = 32f) => new Vector2(
            MathHelper.Clamp(world.X, margin, Main.maxTilesX * 16f - margin),
            MathHelper.Clamp(world.Y, margin, Main.maxTilesY * 16f - margin));

        // Solid for movement: real tile, not actuated, not a platform top unless includePlatforms.
        public static bool IsSolidTile(int x, int y, bool includePlatforms = false)
        {
            if (!WorldGen.InWorld(x, y, 1))
                return true; // treat out of world as a wall so searches stop

            Tile t = Main.tile[x, y];
            if (!t.HasUnactuatedTile)
                return false;
            if (Main.tileSolid[t.TileType] && !Main.tileSolidTop[t.TileType])
                return true;
            return includePlatforms && Main.tileSolidTop[t.TileType];
        }

        public static bool IsSolidAt(Vector2 world, bool includePlatforms = false) =>
            IsSolidTile((int)(world.X / 16f), (int)(world.Y / 16f), includePlatforms);

        // Any solid tile overlapping a box. Wraps Collision.SolidCollision with a center-based API.
        public static bool BoxHitsSolid(Vector2 center, int width, int height) =>
            Collision.SolidCollision(center - new Vector2(width, height) * 0.5f, width, height);

        public static bool HasLineOfSight(Vector2 a, Vector2 b) => Collision.CanHitLine(a, 1, 1, b, 1, 1);

        // Walk down from `from` and return the world Y of the top of the first solid tile.
        // Returns null if nothing is found within maxDistance (pixels).
        public static float? GroundYBelow(Vector2 from, float maxDistance = 1600f, bool includePlatforms = true)
        {
            int x = (int)(from.X / 16f);
            int startY = (int)(from.Y / 16f);
            int steps = (int)(maxDistance / 16f);
            for (int i = 0; i <= steps; i++)
            {
                int y = startY + i;
                if (!WorldGen.InWorld(x, y, 1))
                    return null;
                if (IsSolidTile(x, y, includePlatforms))
                    return y * 16f;
            }
            return null;
        }

        // Walk up and return the world Y of the BOTTOM of the first solid tile above.
        public static float? CeilingYAbove(Vector2 from, float maxDistance = 1600f)
        {
            int x = (int)(from.X / 16f);
            int startY = (int)(from.Y / 16f);
            int steps = (int)(maxDistance / 16f);
            for (int i = 0; i <= steps; i++)
            {
                int y = startY - i;
                if (!WorldGen.InWorld(x, y, 1))
                    return null;
                if (IsSolidTile(x, y))
                    return (y + 1) * 16f;
            }
            return null;
        }

        // Distance until the first solid tile in a horizontal direction (dirX = -1 or 1). maxDistance if clear.
        public static float WallDistance(Vector2 from, int dirX, float maxDistance = 1600f)
        {
            int y = (int)(from.Y / 16f);
            int startX = (int)(from.X / 16f);
            int steps = (int)(maxDistance / 16f);
            for (int i = 1; i <= steps; i++)
            {
                if (IsSolidTile(startX + dirX * i, y))
                    return i * 16f - 8f;
            }
            return maxDistance;
        }

        // Step along a ray and return the first point that is inside a solid tile. `hit` is that point (or the end if clear).
        public static bool Raycast(Vector2 start, Vector2 dir, float maxDistance, out Vector2 hit, float step = 8f, bool includePlatforms = false)
        {
            dir = dir.SafeNormalize(Vector2.UnitX);
            for (float d = 0f; d <= maxDistance; d += step)
            {
                Vector2 p = start + dir * d;
                if (IsSolidAt(p, includePlatforms))
                {
                    hit = p;
                    return true;
                }
            }
            hit = start + dir * maxDistance;
            return false;
        }

        // Length a beam can travel before it hits a tile. Use for lasers that stop at walls.
        public static float BeamLength(Vector2 start, Vector2 dir, float maxLength, float step = 8f) =>
            Raycast(start, dir, maxLength, out Vector2 hit, step) ? Vector2.Distance(start, hit) : maxLength;

        // Pull a point out of solid tiles by sliding it up (useful before spawning something at a "ground" position).
        public static Vector2 PushOutOfSolidUp(Vector2 world, int maxTiles = 20)
        {
            int x = (int)(world.X / 16f), y = (int)(world.Y / 16f);
            for (int i = 0; i < maxTiles && IsSolidTile(x, y); i++)
                y--;
            return new Vector2(world.X, y * 16f + 8f);
        }

        // Count solid tiles in a tile-space rectangle. Handy for "is this area open enough" checks before spawning/placing.
        public static int CountSolid(Point topLeftTile, int w, int h)
        {
            int n = 0;
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    if (IsSolidTile(topLeftTile.X + x, topLeftTile.Y + y))
                        n++;
            return n;
        }

        public static bool IsInLiquid(Vector2 world, int liquidType = -1)
        {
            int x = (int)(world.X / 16f), y = (int)(world.Y / 16f);
            if (!WorldGen.InWorld(x, y, 1))
                return false;
            Tile t = Main.tile[x, y];
            return t.LiquidAmount > 0 && (liquidType < 0 || t.LiquidType == liquidType);
        }

        // On-screen test with padding, for skipping draw work.
        public static bool OnScreen(Vector2 world, float pad = 64f)
        {
            Vector2 s = world - Main.screenPosition;
            return s.X > -pad && s.Y > -pad && s.X < Main.screenWidth + pad && s.Y < Main.screenHeight + pad;
        }

        public static bool OnScreen(Rectangle worldRect, float pad = 64f)
        {
            Rectangle view = new Rectangle((int)(Main.screenPosition.X - pad), (int)(Main.screenPosition.Y - pad),
                (int)(Main.screenWidth + pad * 2f), (int)(Main.screenHeight + pad * 2f));
            return view.Intersects(worldRect);
        }

        public static Vector2 WorldToScreen(Vector2 world) => world - Main.screenPosition;
        public static Vector2 ScreenToWorld(Vector2 screen) => screen + Main.screenPosition;
    }
}
