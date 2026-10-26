using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.Drawing;

namespace ShatteredIllusion.Common.TileMerging
{
    /// <summary>
    /// Draws the jagged seam between two merging tile types, on top of the tile the game already drew.
    ///
    /// How it works: the line between two tiles gets a wobble that varies along its length. The wobble is measured
    /// in 2px steps (Terraria's pixel grid) in a range of -3 to +3 steps. A positive value means the material on the
    /// far side (right or below) pokes into the near side (left or above) by that many steps; a negative value means
    /// the near material pokes into the far side. Each tile draws only the material poking INTO itself, using the
    /// partner's own texture, so a seam is drawn once from each side without double-covering anything. Pokes are at
    /// most 6px, so the pokes from opposite edges of a 16px tile can never touch.
    ///
    /// The wobble is a pure function of the seam's position in the world (which line, which row), so the two tiles on
    /// either side of a seam always agree on its shape. It is made by hashing a number for every 8px along the seam
    /// and blending between neighboring numbers.
    ///
    /// Taper: where a seam ends (the next tile along it is not a continuation of the boundary), the last 4px are
    /// pulled toward the straight line so the end doesn't leave a notch. The boundary is treated as continuing past
    /// a corner when the diagonal neighbor on that side is a partner and the straight neighbor on that side is the
    /// same type as this tile. Contact that is only diagonal draws nothing; the diagonals are used for the taper only.
    ///
    /// Texture: the poking material is sampled from a fixed interior frame of the partner's texture sheet (the frame
    /// variant is picked by hashing the partner's position, not read from the partner's actual frame), at the same
    /// position within the tile as where it lands, so the poking material looks like the partner's own interior
    /// texture. This assumes the standard layout where frames sit on an 18px grid (16px frame, 2px gap) and the fully
    /// surrounded frames start at (18, 18). Tile sheets that don't follow that layout would sample the wrong pixels.
    ///
    /// Light: each strip uses one flat color, Lighting.GetColor for the tile it is drawn in (white if the partner
    /// has a glow coating), rather than vanilla's smooth per-corner light blending. Tiles that end up unlit/black
    /// are skipped, since PostDraw also runs for tiles something else chose not to draw. Tiles hidden by echo coating
    /// are skipped too.
    ///
    /// Limitations:
    /// - Only full blocks take part (see TileMergeCache.IsEligible); other shapes keep the vanilla look.
    /// - Each strip switches texture away from the tile batch's current texture, so long seams cost a few extra
    ///   SpriteBatch flushes.
    /// - Where seams meet at a concave corner (Left+Up, etc.), strips can overlap in a small area; the fixed draw
    ///   order keeps the result deterministic.
    /// - Spelunker/danger-sense glow isn't applied to strips.
    ///
    /// Everything here draws into the batch that is already open for tile drawing and allocates nothing per frame
    /// (the neighbor cache allocates once per 16x16 chunk the first time it is used).
    /// </summary>
    public static class TileMergeRenderer
    {
        // Largest wobble in 2px steps (3 steps = 6px).
        private const int MaxStep = 3;

        // Draw the strips poking into the tile at (i, j) in the fixed order left, right, up, down.
        public static void Draw(int i, int j, int type, SpriteBatch spriteBatch)
        {
            Tile self = Main.tile[i, j];
            if (!TileDrawing.IsVisible(self))
                return;

            // PostDraw also runs for tiles that something else chose not to draw. A fully dark tile is treated as
            // not drawn (checked first because it is cheaper than the neighbor lookup).
            Color color = Lighting.GetColor(i, j);
            if (!self.IsTileFullbright && (color.R | color.G | color.B) == 0)
                return;

            ushort mask = TileMergeCache.GetMask(i, j, type);
            if ((mask & 0xFF) == 0)
                return;

            // Same position vanilla uses for tile drawing (see Main.drawToScreen / Main.offScreenRange).
            // The camera position is cut to whole pixels exactly like vanilla does, so strips line up with the tile underneath.
            // (Camera.UnscaledPosition just returns Main.screenPosition.)
            Vector2 origin = new Vector2(i * 16 - (int)Main.screenPosition.X, j * 16 - (int)Main.screenPosition.Y)
                + (Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange));

            if ((mask & TileMergeCache.Left) != 0)
            {
                // Vertical seam on this tile's left edge. This tile is on the far (+x) side, so it receives where the wobble is negative.
                bool up = Continues(mask, TileMergeCache.UpLeft, TileMergeCache.SameUp);
                bool down = Continues(mask, TileMergeCache.DownLeft, TileMergeCache.SameDown);
                DrawEdge(spriteBatch, origin, color, i - 1, j, i, j, false, -1, up, down, false);
            }

            if ((mask & TileMergeCache.Right) != 0)
            {
                bool up = Continues(mask, TileMergeCache.UpRight, TileMergeCache.SameUp);
                bool down = Continues(mask, TileMergeCache.DownRight, TileMergeCache.SameDown);
                DrawEdge(spriteBatch, origin, color, i + 1, j, i + 1, j, false, 1, up, down, true);
            }

            if ((mask & TileMergeCache.Up) != 0)
            {
                // Horizontal seam on the top edge; "start" and "end" run left to right.
                bool left = Continues(mask, TileMergeCache.UpLeft, TileMergeCache.SameLeft);
                bool right = Continues(mask, TileMergeCache.UpRight, TileMergeCache.SameRight);
                DrawEdge(spriteBatch, origin, color, i, j - 1, i, j, true, -1, left, right, false);
            }

            if ((mask & TileMergeCache.Down) != 0)
            {
                bool left = Continues(mask, TileMergeCache.DownLeft, TileMergeCache.SameLeft);
                bool right = Continues(mask, TileMergeCache.DownRight, TileMergeCache.SameRight);
                DrawEdge(spriteBatch, origin, color, i, j + 1, i, j + 1, true, 1, left, right, true);
            }
        }

        // The seam keeps going past this tile's corner if the diagonal neighbor is a partner and the straight
        // neighbor is the same type as this tile (so the seam really runs on between those two).
        private static bool Continues(ushort mask, byte diagonal, ushort straightSame)
        {
            return (mask & diagonal) != 0 && (mask & straightSame) != 0;
        }

        /// <summary>
        /// Draws what pokes into the tile at origin from one neighbor.
        /// </summary>
        /// <param name="ni">Neighbor tile X.</param>
        /// <param name="nj">Neighbor tile Y.</param>
        /// <param name="lineX">Tile X of the seam line's far-side tile (the line sits at its left or top edge); with lineY it identifies the seam.</param>
        /// <param name="lineY">Tile Y, as above.</param>
        /// <param name="horizontal">True for a seam running left to right (top or bottom edge).</param>
        /// <param name="side">-1 if this tile is on the far (+x / +y) side of the seam, +1 if on the near side.</param>
        /// <param name="continuesStart">Seam continues toward lower x / y past this tile.</param>
        /// <param name="continuesEnd">Seam continues toward higher x / y past this tile.</param>
        /// <param name="farEdge">True if the seam is on the right / bottom edge of this tile (strips are drawn against that edge).</param>
        private static void DrawEdge(SpriteBatch spriteBatch, Vector2 origin, Color color, int ni, int nj,
            int lineX, int lineY, bool horizontal, int side, bool continuesStart, bool continuesEnd, bool farEdge)
        {
            Tile neighbor = Main.tile[ni, nj];
            if (!TileDrawing.IsVisible(neighbor))
                return;

            // Seam position: the tile coordinate of the line, and where along it this tile starts.
            int line = horizontal ? lineY : lineX;
            int along = horizontal ? lineX : lineY;

            // Three lattice values cover this tile's 8 rows: at the start, middle and end.
            int a = Lattice(line, along * 2, horizontal);
            int b = Lattice(line, along * 2 + 1, horizontal);
            int c = Lattice(line, along * 2 + 2, horizontal);

            Span<int> depth = stackalloc int[8];
            for (int k = 0; k < 8; k++)
            {
                int offset = k < 4 ? Blend(a, b, k) : Blend(b, c, k - 4);
                // Positive offset: the far side pokes in. 'side' tells which side this tile is on.
                depth[k] = Math.Max(0, offset * (side < 0 ? -1 : 1));
            }

            // Taper the ends of a seam that stops here.
            if (!continuesStart)
            {
                depth[0] = 0;
                depth[1] /= 2;
            }

            if (!continuesEnd)
            {
                depth[7] = 0;
                depth[6] /= 2;
            }

            // The strip shows the neighbor's material, so the neighbor's glow coating decides if it ignores lighting.
            if (neighbor.IsTileFullbright)
                color = Color.White;

            int nType = neighbor.TileType;
            if (!TextureAssets.Tile[nType].IsLoaded)
                Main.instance.LoadTiles(nType);

            // Painted texture, so the partner's paint is respected.
            Texture2D texture = Main.instance.TilesRenderer.GetTileDrawTexture(neighbor, ni, nj);
            int variant = (int)(Hash(ni, nj, 7u) % 3u);
            int frameX = 18 * (1 + variant);
            int frameY = 18;

            // Draw each run of equal depth as one rectangle.
            int k0 = 0;
            while (k0 < 8)
            {
                int d = depth[k0];
                int k1 = k0 + 1;
                while (k1 < 8 && depth[k1] == d)
                    k1++;

                if (d > 0)
                {
                    int alongPos = k0 * 2;
                    int alongLen = (k1 - k0) * 2;
                    int thick = d * 2;
                    int inset = farEdge ? 16 - thick : 0;

                    // Position inside the 16x16 tile; the same numbers pick the matching pixels in the source frame.
                    int x = horizontal ? alongPos : inset;
                    int y = horizontal ? inset : alongPos;
                    int w = horizontal ? alongLen : thick;
                    int h = horizontal ? thick : alongLen;

                    spriteBatch.Draw(texture, new Vector2(origin.X + x, origin.Y + y),
                        new Rectangle(frameX + x, frameY + y, w, h), color);
                }

                k0 = k1;
            }
        }

        // Linear blend of two lattice values at step f/4 along the way, rounded to a whole step.
        private static int Blend(int from, int to, int f)
        {
            int n = from * (4 - f) + to * f;
            return n >= 0 ? (n + 2) >> 2 : -((-n + 2) >> 2);
        }

        // Random-looking whole number in [-3, 3] for one 8px point along a seam.
        private static int Lattice(int line, int index, bool horizontal)
        {
            return (int)(Hash(line, index, horizontal ? 1u : 0u) % (2 * MaxStep + 1)) - MaxStep;
        }

        // Small integer hash; no Random so the result is the same on every client and every frame.
        private static uint Hash(int a, int b, uint salt)
        {
            unchecked
            {
                uint h = (uint)a * 0x9E3779B1u ^ (uint)b * 0x85EBCA6Bu ^ (salt + 1u) * 0xC2B2AE35u;
                h ^= h >> 15;
                h *= 0x2C1B3C6Du;
                h ^= h >> 12;
                h *= 0x297A2D39u;
                h ^= h >> 15;
                return h;
            }
        }
    }
}
