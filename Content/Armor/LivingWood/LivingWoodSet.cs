using Terraria.ModLoader;
using Terraria;
using Terraria.ID;
using ShatteredIllusion.Placeables.Blocks;

namespace ShatteredIllusion.Content.Armor.LivingWood
{
    [AutoloadEquip(EquipType.Head)]
    public class LivingWoodHelmet : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 20;
            Item.height = 20;
            Item.maxStack = 1;
            Item.rare = ItemRarityID.White;
            Item.defense = 1;
        }

        public override void UpdateEquip(Player player)
        {
            player.GetDamage(DamageClass.Summon) += 0.06f;
        }
        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ItemID.Wood, 10)
                .AddIngredient(ItemID.Emerald, 1)
                .AddIngredient(ModContent.ItemType<MossItem>(), 10)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }

    [AutoloadEquip(EquipType.Body)]
    public class LivingWoodChestplate : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 20;
            Item.height = 20;
            Item.maxStack = 1;
            Item.rare = ItemRarityID.White;
            Item.defense = 3;
        }
        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ItemID.Wood, 15)
                .AddIngredient(ModContent.ItemType<MossItem>(), 5)
                .AddIngredient(ItemID.Emerald, 2)
                .AddTile(TileID.Anvils)
                .Register();
        }

        public override void UpdateEquip(Player player)
        {
            player.maxMinions += 1;
        }

        public override bool IsArmorSet(Item head, Item body, Item legs)
        {
            return head.type == ModContent.ItemType<LivingWoodHelmet>()
                && body.type == ModContent.ItemType<LivingWoodChestplate>()
                && legs.type == ModContent.ItemType<LivingWoodLeggings>();
        }

        public override void UpdateArmorSet(Player player)
        {
            player.maxMinions += 1;
            player.lifeRegen += 3;
        }
    }

    [AutoloadEquip(EquipType.Legs)]
    public class LivingWoodLeggings : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 20;
            Item.height = 20;
            Item.maxStack = 1;
            Item.rare = ItemRarityID.White;
            Item.defense = 2;
        }
        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ItemID.Wood, 10)
                .AddIngredient(ModContent.ItemType<MossItem>(), 5)
                .AddIngredient(ItemID.Emerald, 1)
                .AddTile(TileID.Anvils)
                .Register();
        }

        public override void UpdateEquip(Player player)
        {
            player.GetDamage(DamageClass.Summon) += 0.06f;
        }
    }

}