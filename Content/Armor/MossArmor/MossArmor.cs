using System;
using Microsoft.Xna.Framework;
using ShatteredIllusion.Content.Items.Materials;
using ShatteredIllusion.Content.Items.Placeables.Blocks;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ShatteredIllusion.Content.Armor.MossArmor
{
    [AutoloadEquip(EquipType.Head)]
    public class MossHelmet : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 20;
            Item.height = 20;
            Item.maxStack = 1;
            Item.rare = ItemRarityID.Green;
            Item.defense = 4;
        }

        public override void UpdateEquip(Player player)
        {
            player.statManaMax2 += 20;
            player.GetDamage(DamageClass.Magic) += 0.05f;
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<MossItem>(), 10)
                .AddIngredient(ItemID.StoneBlock, 15)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }

    [AutoloadEquip(EquipType.Body)]
    public class MossChestplate : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 20;
            Item.height = 20;
            Item.maxStack = 1;
            Item.rare = ItemRarityID.Green;
            Item.defense = 5;
        }

        public override void UpdateEquip(Player player)
        {
            player.statManaMax2 += 20;
            player.GetDamage(DamageClass.Magic) += 0.05f;
            player.GetCritChance(DamageClass.Magic) += 2;
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<MossItem>(), 15)
                .AddIngredient(ItemID.StoneBlock, 20)
                .AddTile(TileID.Anvils)
                .Register();
        }

        public override bool IsArmorSet(Item head, Item body, Item legs)
        {
            return head.type == ModContent.ItemType<MossHelmet>()
                && body.type == ModContent.ItemType<MossChestplate>()
                && legs.type == ModContent.ItemType<MossLeggings>();
        }


        public override void UpdateArmorSet(Player player)
        {
            player.setBonus =
                "Maximum mana is halved\n" +
                "Unused mana regeneration builds into increased movement and cast speed";

            player.GetModPlayer<MossArmorPlayer>().UpdateVerdantConduit(player);
        }
    }

    [AutoloadEquip(EquipType.Legs)]
    public class MossLeggings : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 20;
            Item.height = 20;
            Item.maxStack = 1;
            Item.rare = ItemRarityID.Green;
            Item.defense = 4;
        }

        public override void UpdateEquip(Player player)
        {
            player.statManaMax2 += 20;
            player.GetDamage(DamageClass.Magic) += 0.05f;
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<MossItem>(), 10)
                .AddIngredient(ItemID.StoneBlock, 12)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }

    public class MossArmorPlayer : ModPlayer
    {
        public bool mossArmorSet;

        public float verdantSpeedBonus;

        private const float MaxSpeedBonus = 0.10f;
        private const int RampTimeTicks = 8 * 60; 
        private const int DecayTimeTicks = 2 * 60;  
        private const float GainPerTick = MaxSpeedBonus / RampTimeTicks;
        private const float DecayPerTick = MaxSpeedBonus / DecayTimeTicks;

        public override void ResetEffects()
        {
            mossArmorSet = false;
        }

        public void UpdateVerdantConduit(Player player)
        {
            mossArmorSet = true;
            player.statManaMax2 /= 2;

            bool atFullMana = player.statManaMax2 > 0 && player.statMana >= player.statManaMax2;

            verdantSpeedBonus = atFullMana
                ? Math.Min(MaxSpeedBonus, verdantSpeedBonus + GainPerTick)
                : Math.Max(0f, verdantSpeedBonus - DecayPerTick);

            player.moveSpeed += verdantSpeedBonus;
        }

        public override void PostUpdateEquips()
        {
            if (!mossArmorSet && verdantSpeedBonus > 0f)
            {
                verdantSpeedBonus = Math.Max(0f, verdantSpeedBonus - DecayPerTick);
                Player.moveSpeed += verdantSpeedBonus;
            }
        }

        public override float UseSpeedMultiplier(Item item)
        {
            if (verdantSpeedBonus > 0f && item.DamageType == DamageClass.Magic)
            {
                return 1f + verdantSpeedBonus;
            }

            return 1f;
        }
    }
}