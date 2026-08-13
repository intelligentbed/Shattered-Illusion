using ShatteredIllusion.Placeables.Blocks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ShatteredIllusion.Content.Items.Materials
{
    public class EvergreenCrystal : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 16;
            Item.height = 16;
            Item.maxStack = 999;
            Item.value = Item.buyPrice(silver: 5);
            Item.rare = ItemRarityID.Green;
        }
         public override void AddRecipes()
        {
            Recipe recipe = CreateRecipe();
            recipe.AddIngredient(ItemID.StoneBlock, 10);
            recipe.AddIngredient(ItemID.Emerald, 1);
            recipe.AddIngredient(ModContent.ItemType<MossItem>(), 5);
            recipe.AddTile(TileID.Anvils);
            recipe.Register();
        }
    }
}
