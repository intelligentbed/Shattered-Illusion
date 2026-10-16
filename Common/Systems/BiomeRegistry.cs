using System;
using System.Collections.Generic;
using Terraria.ModLoader;
using ShatteredIllusion.Content.Biomes;

namespace ShatteredIllusion.Common.Systems
{
    public class BiomeRegistry : ModSystem
    {
        private static readonly List<ShatteredBiome> biomes = new();
        private static readonly Dictionary<ShatteredBiome, int> indices = new();
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
