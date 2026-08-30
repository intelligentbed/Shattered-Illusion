using Terraria.ID;
using Terraria.ModLoader;

namespace ShatteredIllusion.Content.Items.Other.RustyKey
{
    internal class RustyKeys : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 24;
            Item.height = 24;
            Item.maxStack = 999;
            Item.value = 0;
            Item.rare = ItemRarityID.White;
            Item.useStyle = ItemUseStyleID.None;
        }
    }

    //See RustyLockSystem
    internal class Rustylocker : ModItem
    {

        public override void SetDefaults()
        {
            Item.width = 24;
            Item.height = 24;
            Item.maxStack = 1;
            Item.value = 0;
            Item.rare = ItemRarityID.White;


            Item.useStyle = ItemUseStyleID.None;
        }
    }
}
