using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using Terraria.ID;
using Terraria.ObjectData;
using ShatteredIllusion.Content.Tiles.BoilingOcean;

namespace ShatteredIllusion.Content.Items.Placeables.Blocks.BoilingOcean
{
    public class AshenSandItem : ModItem
    {
        public override void SetDefaults() 
        {
            Item.width = 16;
            Item.height = 16;
            Item.useTime = 10;
            Item.useAnimation = 15;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.consumable = true;
            Item.maxStack = 9999;
            Item.value = 0;
            Item.createTile = ModContent.TileType<AshenSand>();
        }
    }
}
