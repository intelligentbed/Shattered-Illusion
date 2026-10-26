using System;
using System.Collections.Generic;
using Terraria.ModLoader;

namespace ShatteredIllusion.Content.Items.Placeables.Blocks.BoilingOcean
{
    public class AshenDirtItem : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 16;
            Item.height = 16;
            Item.useTime = 10;
            Item.useAnimation = 15;
            Item.maxStack = 9999;
            Item.useStyle = Terraria.ID.ItemUseStyleID.Swing;
            Item.consumable = true;
            Item.value = 0;
            Item.createTile = ModContent.TileType<Content.Tiles.BoilingOcean.AshenDirt>();
        }   
    }
}
