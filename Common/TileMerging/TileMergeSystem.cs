using ShatteredIllusion.Content.Tiles.BoilingOcean;
using Terraria.ModLoader;

namespace ShatteredIllusion.Common.TileMerging
{
    /// <summary>Registers the merging tile pairs and clears the merge data when worlds or the mod unload.</summary>
    public class TileMergeSystem : ModSystem
    {
        // Runs once after all mods have loaded their content, so tile types are known.
        public override void PostSetupContent()
        {
            int sand = ModContent.TileType<AshenSand>();
            int dirt = ModContent.TileType<AshenDirt>();
            int stone = ModContent.TileType<VolcanicStone>();

            TileMergeRegistry.RegisterMerge(sand, dirt);
            TileMergeRegistry.RegisterMerge(dirt, stone);
        }

        // Leaving or switching worlds: stored neighbor masks belong to the old world's tiles.
        public override void ClearWorld()
        {
            TileMergeCache.Clear();
        }

        public override void OnWorldUnload()
        {
            TileMergeCache.Clear();
        }

        public override void Unload()
        {
            TileMergeCache.Clear();
            TileMergeRegistry.Clear();
        }
    }
}
