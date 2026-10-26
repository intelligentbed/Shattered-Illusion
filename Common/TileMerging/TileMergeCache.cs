using Terraria;
using Terraria.ID;

namespace ShatteredIllusion.Common.TileMerging
{
    /// <summary>
    /// Remembers, for tiles that have merge partners, which of their 8 neighbors are partners.
    ///
    /// The answer is a ushort. The low 8 bits have one bit per neighbor (see the Left..DownRight constants). Bits 8
    /// to 11 (SameLeft..SameDown) say whether each straight neighbor is a full block of the SAME type as this tile;
    /// the renderer uses them to tell whether a seam carries on past a corner. Test only the low 8 bits (mask &amp; 0xFF)
    /// to ask whether any partner is touching. It is worked out the first time a registered tile is drawn and then
    /// reused, so the draw loop doesn't re-read 8 neighbors for every tile on every frame. Tiles with no partner
    /// neighbors are stored too (as an empty mask), so they aren't recomputed either.
    ///
    /// Storage is a grid of 16x16-tile chunks. The grid itself is one reference per chunk (about 600KB on a large
    /// world) and is created on first use. A chunk's 256 entries (512 bytes) are only allocated when a registered
    /// tile in that chunk is first drawn. Each entry is 0 for "not worked out yet", or the mask plus 0x8000 once it
    /// is known (so an empty mask is stored too and isn't recomputed). Lookups are plain array indexing, with no
    /// hashing, and the memory stays small even when many interior tiles are stored.
    ///
    /// A neighbor counts only if it is a plain full block: it has a tile, is not actuated, is not sloped and is not a
    /// half block. The tile itself must be a plain full block too. Any other shape gets 0, which leaves the vanilla
    /// rendering untouched.
    ///
    /// Entries go stale when a tile changes, so TileMergeGlobalTile.TileFrame calls Invalidate. Placing, mining,
    /// hammering, converting, actuating and network section updates all end up in WorldGen.TileFrame. Drawing runs
    /// on the main thread, but WorldGen.StartHardmode frames tiles from a worker thread, so a mask can very rarely be
    /// stored stale until the tile is next reframed. WorldGen.TileFrame skips tiles within 5 tiles of the world edge;
    /// that is harmless because players can't reach that area.
    /// </summary>
    public static class TileMergeCache
    {
        public const byte Left = 1 << 0;
        public const byte Right = 1 << 1;
        public const byte Up = 1 << 2;
        public const byte Down = 1 << 3;
        public const byte UpLeft = 1 << 4;
        public const byte UpRight = 1 << 5;
        public const byte DownLeft = 1 << 6;
        public const byte DownRight = 1 << 7;

        // Straight neighbor is a full block of the same type as this tile.
        public const ushort SameLeft = 1 << 8;
        public const ushort SameRight = 1 << 9;
        public const ushort SameUp = 1 << 10;
        public const ushort SameDown = 1 << 11;

        private const int ChunkShift = 4;
        private const int ChunkSize = 1 << ChunkShift;
        private const int ChunkMask = ChunkSize - 1;
        private const ushort Computed = 0x8000;

        // One slot per 16x16 chunk of the world; a slot is null until a tile in it is stored.
        private static ushort[]?[]? _chunks;
        // World size the grid was built for, so a world of a different size gets a new grid.
        private static int _width;
        private static int _height;
        private static int _chunksWide;

        /// <summary>The neighbor mask for the tile at (i, j), computed and stored on first request.</summary>
        public static ushort GetMask(int i, int j, int type)
        {
            if (_chunks is null || _width != Main.maxTilesX || _height != Main.maxTilesY)
                Allocate();

            ushort[]?[] chunks = _chunks!;
            int chunkIndex = (j >> ChunkShift) * _chunksWide + (i >> ChunkShift);
            ushort[] chunk = chunks[chunkIndex] ??= new ushort[ChunkSize * ChunkSize];

            int slot = ((j & ChunkMask) << ChunkShift) | (i & ChunkMask);
            ushort stored = chunk[slot];
            if (stored != 0)
                return (ushort)(stored & ~Computed);

            ushort mask = Compute(i, j, type);
            chunk[slot] = (ushort)(mask | Computed);
            return mask;
        }

        /// <summary>Drops the stored masks of the 3x3 block around (i, j).</summary>
        public static void Invalidate(int i, int j)
        {
            ushort[]?[]? chunks = _chunks;
            if (chunks is null)
                return;

            for (int x = i - 1; x <= i + 1; x++)
            {
                for (int y = j - 1; y <= j + 1; y++)
                {
                    if (x < 0 || y < 0 || x >= _width || y >= _height)
                        continue;

                    ushort[]? chunk = chunks[(y >> ChunkShift) * _chunksWide + (x >> ChunkShift)];
                    if (chunk is not null)
                        chunk[((y & ChunkMask) << ChunkShift) | (x & ChunkMask)] = 0;
                }
            }
        }

        public static void Clear()
        {
            _chunks = null;
        }

        private static void Allocate()
        {
            _width = Main.maxTilesX;
            _height = Main.maxTilesY;
            _chunksWide = (_width + ChunkMask) >> ChunkShift;
            int chunksHigh = (_height + ChunkMask) >> ChunkShift;
            _chunks = new ushort[chunksHigh * _chunksWide][];
        }

        /// <summary>True for a tile that has a block, isn't actuated, and is a full square (no slope, no half block).</summary>
        public static bool IsEligible(Tile tile)
        {
            return tile.HasTile && !tile.IsActuated && tile.Slope == SlopeType.Solid && !tile.IsHalfBlock;
        }

        private static ushort Compute(int i, int j, int type)
        {
            // Tiles at the very edge of the world have no full set of neighbors; leave them to vanilla.
            if (i < 1 || j < 1 || i >= Main.maxTilesX - 1 || j >= Main.maxTilesY - 1)
                return 0;

            if (!IsEligible(Main.tile[i, j]))
                return 0;

            int mask = 0;
            mask |= Check(i - 1, j, type, Left);
            mask |= Check(i + 1, j, type, Right);
            mask |= Check(i, j - 1, type, Up);
            mask |= Check(i, j + 1, type, Down);
            mask |= Check(i - 1, j - 1, type, UpLeft);
            mask |= Check(i + 1, j - 1, type, UpRight);
            mask |= Check(i - 1, j + 1, type, DownLeft);
            mask |= Check(i + 1, j + 1, type, DownRight);
            mask |= Same(i - 1, j, type, SameLeft);
            mask |= Same(i + 1, j, type, SameRight);
            mask |= Same(i, j - 1, type, SameUp);
            mask |= Same(i, j + 1, type, SameDown);
            return (ushort)mask;
        }

        private static int Same(int x, int y, int type, ushort bit)
        {
            Tile neighbor = Main.tile[x, y];
            return IsEligible(neighbor) && neighbor.TileType == type ? bit : 0;
        }

        private static int Check(int x, int y, int type, byte bit)
        {
            Tile neighbor = Main.tile[x, y];
            return IsEligible(neighbor) && TileMergeRegistry.ArePartners(type, neighbor.TileType) ? bit : 0;
        }
    }
}
