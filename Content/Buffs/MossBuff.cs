using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria;
using Terraria.ModLoader;

namespace ShatteredIllusion.Content.Buffs
{
    public class MossBuff : ModBuff
    {
        public override void SetStaticDefaults()
        {
            Main.pvpBuff[Type] = true;    
            Main.buffNoSave[Type] = false; 
        }
        // is it just me or i feel like this set static defaults is unnecessary? 
        public override void Update(Player player, ref int buffIndex)
        { 
            player.lifeRegen += 1;              
        }
    }
}