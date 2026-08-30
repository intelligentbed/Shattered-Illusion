using ShatteredIllusion.Common.Players.ParrySystem;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;


namespace ShatteredIllusion.Content.Armor.MycelialArmor
{
    [AutoloadEquip(EquipType.Head)]
    public class MycelialHelmet : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 20;
            Item.height = 20;
            Item.maxStack = 1;
            Item.rare = ItemRarityID.Blue;
            Item.defense = 2;
        }

        public override void UpdateEquip(Player player)
        {
            player.GetCritChance(DamageClass.Melee) += 4;
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ItemID.GlowingMushroom, 15)
                .AddIngredient(ItemID.SilverBar, 3)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }


    [AutoloadEquip(EquipType.Body)]
    public class MycelialChestplate : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 20;
            Item.height = 20;
            Item.maxStack = 1;
            Item.rare = ItemRarityID.Blue;
            Item.defense = 4;
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ItemID.GlowingMushroom, 20)
                .AddIngredient(ItemID.SilverBar, 5)
                .AddTile(TileID.Anvils)
                .Register();
        }

        public override void UpdateEquip(Player player)
        {
            player.GetDamage(DamageClass.Melee) += 0.03f;
        }

        public override bool IsArmorSet(Item head, Item body, Item legs)
        {
            return head.type == ModContent.ItemType<MycelialHelmet>()
                && body.type == ModContent.ItemType<MycelialChestplate>()
                && legs.type == ModContent.ItemType<MycelialLeggings>();
        }

        public override void UpdateArmorSet(Player player)
        {
            player.GetDamage(DamageClass.Melee) += 0.05f;
            player.setBonus =
                "Increase melee damage by 5%\n" +
                "Parrying releases a burst of spores that damages and poisons nearby enemies";

            player.GetModPlayer<ParryPlayer>().MycelialSetActive = true;
        }
    }


    [AutoloadEquip(EquipType.Legs)]
    public class MycelialLeggings : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 20;
            Item.height = 20;
            Item.maxStack = 1;
            Item.rare = ItemRarityID.Blue;
            Item.defense = 2;
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ItemID.GlowingMushroom, 15)
                .AddIngredient(ItemID.SilverBar, 3)
                .AddTile(TileID.Anvils)
                .Register();
        }

        public override void UpdateEquip(Player player)
        {
            player.GetAttackSpeed(DamageClass.Melee) += 0.02f;
        }
    }

}