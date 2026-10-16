using System;
using System.Collections.Generic;
using Terraria.ModLoader;
using ShatteredIllusion.Content.Biomes;

namespace ShatteredIllusion.Common.Systems
{
    public class BiomeRegistry : ModSystem
    {
        // Every ShatteredBiome in the mod, in registration order.
        private static readonly List<ShatteredBiome> biomes = new();
        // Each biome's position in the lists, so a lookup doesn't have to search.
        private static readonly Dictionary<ShatteredBiome, int> indices = new();
        // counts[i] = how many of biomes[i]'s blocks were in the last scan.
        private static int[] counts = [];

        public static IReadOnlyList<ShatteredBiome> Biomes => biomes;

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

        public static int GetTileCount(ShatteredBiome biome)
        {
            if (biome is null || !indices.TryGetValue(biome, out int index) || index >= counts.Length)
                return 0;

            return counts[index];
        }

        //forget the old counts so they don't leak into the next world.
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
