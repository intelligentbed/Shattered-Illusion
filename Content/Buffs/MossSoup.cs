using ShatteredIllusion.Content.Items.Placeables.Blocks;
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
            Item.rare = ItemRarityID.Green;

            Item.useStyle = ItemUseStyleID.EatFood;
            Item.useAnimation = 17;
            Item.useTime = 17;
            Item.UseSound = SoundID.Item2; 
            Item.consumable = true;

            Item.buffType = ModContent.BuffType<MossBuff>();
            Item.buffTime = 3600 * 3;      // 3600 = 1 min, 1 min x 3 = 3 min
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