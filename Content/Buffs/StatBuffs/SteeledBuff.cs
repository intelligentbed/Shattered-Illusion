using Terraria;
using Terraria.ModLoader;

namespace ShatteredIllusion.Content.Buffs.StatBuffs
{
    public class SteeledBuff : ModBuff
    {
        // the parry heal stuff is in ParrySystem
        public const float DamageMultiplier = 1.1f;

        public override void SetStaticDefaults()
        {
            Main.debuff[Type] = false;
            Main.buffNoSave[Type] = false;
            Main.buffNoTimeDisplay[Type] = false;
        }

        public override void Update(Player player, ref int buffIndex)
        {
            player.GetDamage(DamageClass.Generic) *= DamageMultiplier;
        }
    }
}