using ShatteredIllusion.Content.Placeables.Blocks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ShatteredIllusion.Content.Buffs
{
    public class MossSoup : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 16;
            Item.height = 16;
            Item.maxStack = 9999; 
            Item.value = Item.buyPrice(copper: 1);
            Item.rare = ItemRarityID.White;

            Item.useStyle = ItemUseStyleID.EatFood;
            Item.useAnimation = 17;
            Item.useTime = 17;
            Item.UseSound = SoundID.Item2; // basic eating sound
            Item.consumable = true;

            // Buff Setup
            Item.buffType = ModContent.BuffType<MossBuff>(); // Buff applied on consumption
            Item.buffTime = 3600 * 5;      // the first number is the ticks and its multiplied by the second number which is the seconds So this is 5 minutes. 
        }

        public override void AddRecipes()
        {
            Recipe recipe = CreateRecipe();
            recipe.AddIngredient(ItemID.Bowl, 1);
            recipe.AddIngredient(ItemID.BottledWater, 1);
            recipe.AddIngredient(ModContent.ItemType<MossItem>(), 5);
            recipe.AddTile(TileID.WorkBenches);
            recipe.Register();
        }
    }
}