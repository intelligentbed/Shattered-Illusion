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
            // Runs every frame while equipped.
            player.GetModPlayer<ShieldPlayer>().HasShield = true;

            // Movement speed penalty for wearing a heavy shield.
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

    public class ShieldPlayer : ModPlayer
    {
        public bool HasShield = false;

        public int shieldHP = 0;
        public const int MaxShieldHP = 100;
        private int regenTimer = 0;
        private int outOfCombatTimer = 0;

        public override void ResetEffects()
        {
            HasShield = false;
        }

        public override void PostUpdateMiscEffects()
        {
            if (!HasShield)
            {
                shieldHP = 0;
                return;
            }

            if (outOfCombatTimer > 0)
            {
                outOfCombatTimer--;
            }
            else if (shieldHP < MaxShieldHP)
            {
                regenTimer++;
                if (regenTimer >= 15)
                {
                    shieldHP++;
                    regenTimer = 0;
                }
            }
        }
        public override void OnHurt(Player.HurtInfo info)
        {
            if (!HasShield || shieldHP <= 0) return;

            outOfCombatTimer = 180;

            int absorbed = Math.Min(shieldHP, info.Damage);
            shieldHP -= absorbed;
            Player.statLife += absorbed;

            if (absorbed > 0)
            {
                SoundEngine.PlaySound(shieldHP <= 0 ? SoundID.NPCDeath37 : SoundID.NPCHit34, Player.position);
            }
        }

    }

}

namespace ShatteredIllusion.Content.Buffs
{
    public class ShieldBuff : ModBuff
    {
        public override void Update(Player player, ref int buffIndex)
        {
            var modPlayer = player.GetModPlayer<ShieldPlayer>();

            if (!modPlayer.HasShield && modPlayer.shieldHP == 0)
            {
                modPlayer.shieldHP = ShieldPlayer.MaxShieldHP;
            }

            modPlayer.HasShield = true;
        }
    }
}

