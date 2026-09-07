using Terraria;
using Terraria.ModLoader;

namespace ShatteredIllusion.Content.Buffs.StatBuffs
{
    public class SproutedBuff : ModBuff
    {
        public const float SummonDamageMultiplier = 1.15f;

        public override void SetStaticDefaults()
        {
            Main.debuff[Type] = false;
            Main.buffNoSave[Type] = false;
            Main.buffNoTimeDisplay[Type] = false;
        }

        public override void Update(Player player, ref int buffIndex)
        {
            player.GetDamage(DamageClass.Summon) *= SummonDamageMultiplier;
        }
    }
}