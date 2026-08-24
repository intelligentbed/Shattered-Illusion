using ShatteredIllusion.Content.Items.Materials;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

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
            Item.rare = ItemRarityID.Blue;
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
                .AddIngredient(ModContent.ItemType<EvergreenCrystal>(), 5)
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
            Item.rare = ItemRarityID.Blue;
            Item.defense = 3;
        }
        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ItemID.Wood, 15)
                .AddIngredient(ModContent.ItemType<EvergreenCrystal>(), 10)
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
            player.setBonus = "Increase Max Summons by 1\n" +
                              "Increase Life Regeneration by 1 HP/s";
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
            Item.rare = ItemRarityID.Blue   ;
            Item.defense = 2;
        }
        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ItemID.Wood, 10)
                .AddIngredient(ModContent.ItemType<EvergreenCrystal>(), 3)
                .AddTile(TileID.Anvils)
                .Register();
        }

        public override void UpdateEquip(Player player)
        {
            player.GetDamage(DamageClass.Summon) += 0.06f;
        }
    }

}