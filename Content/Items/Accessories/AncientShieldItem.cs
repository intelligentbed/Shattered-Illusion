using System;
using Terraria.ModLoader;
using Terraria;
using Terraria.ID;
using Terraria.Audio;
using ShatteredIllusion.Content.Items.Accessories;

namespace ShatteredIllusion.Content.Items.Accessories
{
    public class AncientShieldItem : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 24;
            Item.height = 24;
            Item.value = Item.buyPrice(gold: 1);
            Item.rare = ItemRarityID.Blue;
            Item.accessory = true; 
            Item.defense = 5; 
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            // Runs every frame while equipped might cause lag in the long run but im lazy
            player.GetModPlayer<ShieldPlayer>().HasShield = true;

            // Movement speed penalty
            player.moveSpeed -= 0.1f;
        }

        public override void AddRecipes()
        {
            Recipe recipe = CreateRecipe();
            recipe.AddIngredient(ItemID.StoneBlock, 25);
            recipe.AddIngredient(ItemID.Wood, 5);
            recipe.AddTile(TileID.Anvils);
            recipe.Register();
        }
    }
}

