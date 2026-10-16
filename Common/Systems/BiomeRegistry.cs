using System;
using System.Collections.Generic;
using Terraria.ModLoader;
using ShatteredIllusion.Content.Biomes;

namespace ShatteredIllusion.Common.Systems
{
    /// <summary>
    /// Keeps the list of every ShatteredBiome and how many of each biome's blocks are near the player.
    ///
    /// How the counting works: every scene update the game scans the tiles around the local player's
    /// screen and hands us the totals in TileCountsAvailable (one number per tile type). We add up the
    /// numbers for each biome's blocks and store the sum, which biomes read through ShatteredBiome.TileCount.
    /// Biomes don't need to be added here by hand; any class that extends ShatteredBiome is picked up.
    /// </summary>
    public class BiomeRegistry : ModSystem
    {
        // Every ShatteredBiome in the mod, in registration order.
        private static readonly List<ShatteredBiome> biomes = new();
        // Each biome's position in the lists, so a lookup doesn't have to search.
        private static readonly Dictionary<ShatteredBiome, int> indices = new();
        // counts[i] = how many of biomes[i]'s blocks were in the last scan.
        private static int[] counts = [];

        public static IReadOnlyList<ShatteredBiome> Biomes => biomes;

        // Runs once after all mods have loaded their content, so tile types are known.
        public override void PostSetupContent()
        {
            biomes.Clear();
            indices.Clear();

            foreach (ShatteredBiome biome in ModContent.GetContent<ShatteredBiome>())
            {
                indices[biome] = biomes.Count;
                biomes.Add(biome);
                biome.BiomeTiles = biome.RequestBiomeTiles();
            }

            counts = new int[biomes.Count];
        }

        // tileCounts[type] is how many tiles of that type are near the player. Runs often, so no allocations here.
        public override void TileCountsAvailable(ReadOnlySpan<int> tileCounts)
        {
            for (int i = 0; i < biomes.Count; i++)
            {
                int total = 0;
                int[] tiles = biomes[i].BiomeTiles;

                for (int j = 0; j < tiles.Length; j++)
                {
                    int type = tiles[j];
                    if ((uint)type < (uint)tileCounts.Length)
                        total += tileCounts[type];
                }

                counts[i] = total;
            }
        }

        /// <summary>How many of the biome's blocks were near the player in the last scan (0 if unknown).</summary>
        public static int GetTileCount(ShatteredBiome biome)
        {
            if (biome is null || !indices.TryGetValue(biome, out int index) || index >= counts.Length)
                return 0;

            return counts[index];
        }

        // Leaving a world: forget the old counts so they don't leak into the next world.
        public override void ClearWorld()
        {
            Array.Clear(counts);
        }

        public override void Unload()
        {
            biomes.Clear();
            indices.Clear();
            counts = [];
        }
    }
}
