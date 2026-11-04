using System;
using Terraria.ModLoader;
using Terraria;
using Terraria.ID;
using Terraria.Audio;
using ShatteredIllusion.Content.Items.Accessories;

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

