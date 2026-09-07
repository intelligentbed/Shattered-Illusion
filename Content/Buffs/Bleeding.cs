using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ShatteredIllusion.Content.Buffs
{
    internal class Bleeding : ModBuff
    {
        public override void SetStaticDefaults()
        {
            Main.debuff[Type] = true;
            Main.pvpBuff[Type] = true;
            Main.buffNoSave[Type] = true;
        }

        public override void Update(NPC npc, ref int buffIndex)
        {
            if (Main.GameUpdateCount % 30 == 0 && !npc.friendly)
            {
                int bleedDamage = 6;
                npc.StrikeNPC(new NPC.HitInfo
                {
                    Damage = bleedDamage,
                    HitDirection = 0,
                    Knockback = 0f,
                    Crit = false
                });
            }
        }
    }
}