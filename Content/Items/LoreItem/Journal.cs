using Terraria.ID;
using Terraria.ModLoader;

namespace ShatteredIllusion.Content.Items.LoreItem
{
    public class Journal : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 24;
            Item.height = 24;
            Item.maxStack = 1;
            Item.rare = ItemRarityID.Blue;
            Item.value = 0;
        }
    }
}