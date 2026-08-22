using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ShatteredIllusion.Content.Items.Placeables.Relics
{
    public abstract class BaseRelicItem : ModItem
    {
        protected abstract int TileType { get; }

        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 1;
        }

        public override void SetDefaults()
        {
            Item.DefaultToPlaceableTile(TileType);
            Item.width = 30;
            Item.height = 40;
            Item.maxStack = Item.CommonMaxStack;
            Item.rare = ItemRarityID.Master;
            Item.master = true;
        }
    }
}