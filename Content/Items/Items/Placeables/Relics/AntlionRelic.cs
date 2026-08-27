using Terraria.ModLoader;

namespace ShatteredIllusion.Content.Items.Placeables.Relics
{
    public class AntlionRelic : BaseRelicItem
    {
        // Fully qualified to point directly to your tile class in Tiles/Relics/
        protected override int TileType => ModContent.TileType<Tiles.Relics.AntlionRelic>();
    }
}