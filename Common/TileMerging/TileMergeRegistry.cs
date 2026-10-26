using System;
using Terraria;
using Terraria.ModLoader;

namespace ShatteredIllusion.Common.TileMerging
{
    /// <summary>
    /// Remembers which pairs of tile types should blend into each other with a jagged, organic seam.
    ///
    /// Pairs are symmetric (registering A with B also registers B with A). Each tile type has a tiny array of its
    /// partners (null when it has none), so the draw code can ask "does this type have any partners?" with a single
    /// array read and "are these two partners?" with a scan of a few numbers - no dictionaries in hot paths.
    ///
    /// This registry is the source of truth, not Main.tileMerge: other mods set that for unrelated pairs. We do still
    /// set Main.tileMerge for our pairs. Vanilla framing (WorldGen.TileFrame) then treats a partner neighbor as the
    /// same tile, picks the fully-surrounded frame and draws no outline along the seam. Our renderer then draws the
    /// jagged seam on top of that.
    /// </summary>
    public static class TileMergeRegistry
    {
        // _partners[type] = the types registered as partners of 'type', or null if it has none.
        private static ushort[]?[] _partners = [];

        /// <summary>Registers two tile types as merge partners. Ignores self-pairs, invalid types and repeats.</summary>
        public static void RegisterMerge(int tileA, int tileB)
        {
            int count = TileLoader.TileCount;
            if (tileA == tileB || (uint)tileA >= (uint)count || (uint)tileB >= (uint)count)
                return;

            if (_partners.Length != count)
                Array.Resize(ref _partners, count);

            Add(tileA, tileB);
            Add(tileB, tileA);

            Main.tileMerge[tileA][tileB] = true;
            Main.tileMerge[tileB][tileA] = true;
        }

        private static void Add(int type, int partner)
        {
            ushort[]? existing = _partners[type];

            if (existing is null)
            {
                _partners[type] = [(ushort)partner];
                return;
            }

            if (Array.IndexOf(existing, (ushort)partner) >= 0)
                return;

            Array.Resize(ref existing, existing.Length + 1);
            existing[^1] = (ushort)partner;
            _partners[type] = existing;
        }

        /// <summary>True if the tile type has at least one registered partner. One array read; safe for the draw loop.</summary>
        public static bool HasPartners(int type)
        {
            ushort[]?[] partners = _partners;
            return (uint)type < (uint)partners.Length && partners[type] is not null;
        }

        /// <summary>True if the two tile types were registered as merge partners.</summary>
        public static bool ArePartners(int a, int b)
        {
            ushort[]?[] partners = _partners;
            if ((uint)a >= (uint)partners.Length)
                return false;

            ushort[]? list = partners[a];
            if (list is null)
                return false;

            for (int i = 0; i < list.Length; i++)
            {
                if (list[i] == b)
                    return true;
            }

            return false;
        }

        // Forget every pair. Main.tileMerge is rebuilt by tML on reload, so it needs no cleanup here.
        public static void Clear()
        {
            _partners = [];
        }
    }
}