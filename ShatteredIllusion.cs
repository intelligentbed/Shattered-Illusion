using System;
using System.Collections.Generic;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace ShatteredIllusion
{
    public class ShatteredIllusion : Mod
    {
        public override void PostSetupContent()
        {
            if (ModLoader.TryGetMod("BossChecklist", out Mod bossChecklist))
            {
                bossChecklist.Call(
                    "LogBoss",
                    this,
                    "GreatAntlionCharger",
                    1.5f,
                    (Func<bool>)(() => DownedSystem.downedGreatAntlionCharger),
                    ModContent.NPCType<Content.NPCs.BossAI.GreatAntlionCharger.GreatAntlionCharger>(),
                    new Dictionary<string, object>
                    {
                        ["spawnItems"] = ModContent.ItemType<Content.Items.SummonItems.AntlionAttractor>()
                    }
                );
            }
        }
    }

    public class DownedSystem : ModSystem
    {
        public static bool downedGreatAntlionCharger;

        public override void OnWorldLoad()
        {
            downedGreatAntlionCharger = false;
        }

        public override void OnWorldUnload()
        {
            downedGreatAntlionCharger = false;
        }

        public override void SaveWorldData(TagCompound tag)
        {
            if (downedGreatAntlionCharger)
            {
                tag["downedGreatAntlionCharger"] = true;
            }
        }

        public override void LoadWorldData(TagCompound tag)
        {
            downedGreatAntlionCharger = tag.ContainsKey("downedGreatAntlionCharger");
        }
    }
}