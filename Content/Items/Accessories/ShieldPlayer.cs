using System;
using Terraria.ModLoader;
using Terraria;
using Terraria.ID;
using Terraria.Audio;
using ShatteredIllusion.Content.Items.Accessories;

namespace ShatteredIllusion.Content.Items.Accessories
{
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

