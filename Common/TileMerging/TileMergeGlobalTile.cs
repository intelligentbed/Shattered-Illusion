using Microsoft.Xna.Framework.Graphics;
using Terraria.ModLoader;

namespace ShatteredIllusion.Common.TileMerging
{
    /// <summary>Connects the merge system to the game: draws the seams and keeps the neighbor cache fresh.</summary>
    public class TileMergeGlobalTile : GlobalTile
    {
        // Runs for every drawn tile, so the first check is a single array read.
        public override void PostDraw(int i, int j, int type, SpriteBatch spriteBatch)
        {
            if (!TileMergeRegistry.HasPartners(type))
                return;

            TileMergeRenderer.Draw(i, j, type, spriteBatch);
        }

        // WorldGen.TileFrame calls this for every active tile whenever it is placed, mined, hammered, converted,
        // actuated or received over the network (all of those go through WorldGen.SquareTileFrame / TileFrame).
        // It isn't called for tiles that just became air, but their neighbors get reframed, and each neighbor's
        // call clears the block around itself, which includes the emptied spot.
        public override bool TileFrame(int i, int j, int type, ref bool resetFrame, ref bool noBreak)
        {
            TileMergeCache.Invalidate(i, j);
            return true;
        }
    }
}