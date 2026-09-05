using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace ShatteredIllusion
{
    public enum MessageType : byte
    {
        DownedGreatAntlionCharger
    }

    public class ShatteredIllusion : Mod
    {
        public override void Unload()
        {
            Core.OverrideSystem.NPCBehaviorOverrideLoader.Unload();
        }

        public override void PostSetupContent()
        {
            Core.OverrideSystem.NPCBehaviorOverrideLoader.Load();

            if (ModLoader.TryGetMod("BossChecklist", out Mod bossChecklist))
            {
                int attractorType = ModContent.ItemType<Content.Items.SummonItems.AntlionAttractor>();

                bossChecklist.Call(
                    "LogBoss",
                    this,
                    "GreatAntlionCharger",
                    1.5f,
                    (Func<bool>)(() => DownedSystem.downedGreatAntlionCharger),
                    ModContent.NPCType<Content.NPCs.BossAI.GreatAntlionCharger.GreatAntlionCharger>(),
                    new Dictionary<string, object>
                    {
                        ["spawnItems"] = attractorType,
                        ["spawnInfo"] = Language.GetOrRegister(
                            "Mods.ShatteredIllusion.Bosses.GreatAntlionCharger.SpawnInfo",
                            () => $"Use an [i:{attractorType}] while in the Underground Railway in the deepest part of the Underground Desert."
                        )
                    }
                );
            }
        }

        public override void HandlePacket(BinaryReader reader, int whoAmI)
        {
            var messageType = (MessageType)reader.ReadByte();

            switch (messageType)
            {
                case MessageType.DownedGreatAntlionCharger:
                    DownedSystem.downedGreatAntlionCharger = true;

                    if (Main.netMode == NetmodeID.Server)
                    {
                        ModPacket packet = GetPacket();
                        packet.Write((byte)MessageType.DownedGreatAntlionCharger);
                        packet.Send(-1, whoAmI);
                    }
                    break;
            }
        }
    }

    public class DownedSystem : ModSystem
    {
        public static bool downedGreatAntlionCharger;
        public static void SetDownedGreatAntlionCharger()
        {
            downedGreatAntlionCharger = true;

            if (Main.netMode != NetmodeID.SinglePlayer)
            {
                ModPacket packet = ModContent.GetInstance<ShatteredIllusion>().GetPacket();
                packet.Write((byte)MessageType.DownedGreatAntlionCharger);
                packet.Send();
            }
        }

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