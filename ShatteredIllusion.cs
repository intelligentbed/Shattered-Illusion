using System;
using System.Collections.Generic;
using Terraria.ModLoader;

namespace ShatteredIllusion
{
    public class ShatteredIllusion : Mod
    {
        public override void PostSetupContent()
        {
            if (ModLoader.TryGetMod("BossChecklist", out Mod bossChecklist))
            {
                // Register Great Antlion Charger
                bossChecklist.Call(
                    "LogBoss",
                    this,                                                              
                    "GreatAntlionCharger",
                    1.5f,
                    (Func<bool>)(() => DownedSystem.downedGreatAntlionCharger),        
                    ModContent.NPCType<Content.NPCs.BossAI.GreatAntlionCharger.GreatAntlionCharger>(),
                    new Dictionary<string, object>
                    {
                        ["spawnItems"] = ModContent.ItemType<Content.Items.Other.AntlionAttractor>()
                    }
                );
            }
        }
    }
    public class DownedSystem : ModSystem
    {
        public static bool downedGreatAntlionCharger;
    }
}