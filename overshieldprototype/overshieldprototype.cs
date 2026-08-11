using System;
using Terraria.ModLoader;
using Terraria;
using Terraria.ID;
using Terraria.Audio;

namespace overshieldprototype
{
    // The item that grants the shield. Give it whatever internal item name you're using.
    public class AncientShieldItem : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 24;
            Item.height = 24;
            Item.value = Item.buyPrice(gold: 1);
            Item.rare = ItemRarityID.Blue;
            Item.accessory = true; // This is what makes it equippable in an accessory slot
            Item.defense = 5; // Flat defense bonus while equipped — tModLoader applies this automatically
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            // Runs every frame while equipped.
            // This is where you flip the ModPlayer's HasShield flag on while it's worn.
            player.GetModPlayer<ShieldPlayer>().HasShield = true;

            // Movement speed penalty for wearing a heavy shield.
            // player.moveSpeed is a multiplier (1.0 = normal), so -0.1f = 10% slower.
            // Adjust this value to taste.
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

        // PreHurt was removed from tModLoader in the 1.4.4 damage hooks rework.
        // Damage-before-it-happens now goes through ModifyHurt(ref Player.HurtModifiers),
        // and reacting-after-it-happens goes through OnHurt(Player.HurtInfo).
        //
        // Here we let the hit apply normally, then refund the portion of health
        // the shield absorbed. This sidesteps needing to know the exact
        // StatModifier/Flat-damage API and is easy to reason about.
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

namespace overshieldprototype.Buffs
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

