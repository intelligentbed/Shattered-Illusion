using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using ShatteredIllusion.Common.Players.ParrySystem;
using ShatteredIllusion.Common.Cutscenes;
using ShatteredIllusion.Core.Packets;

namespace ShatteredIllusion
{
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
            if (reader.BaseStream.Position >= reader.BaseStream.Length)
                return;

            var messageType = (MessageType)reader.ReadByte();

            switch (messageType)
            {
                case MessageType.SyncDownedGreatAntlionCharger:

                    if (Main.netMode == NetmodeID.MultiplayerClient)
                        DownedSystem.ReceiveDownedState(reader.ReadBoolean());
                    break;

                case MessageType.TutorialProcUpdate:
                    if (Main.netMode == NetmodeID.MultiplayerClient)
                        ParryTutorialSystem.ReceiveProcUpdate(reader.ReadInt32());
                    break;

                case MessageType.RequestParry:
                    if (Main.netMode == NetmodeID.Server)
                        ParryPlayer.HandleParryRequest(whoAmI);
                    break;

                case MessageType.RequestActivateSteeled:
                    if (Main.netMode == NetmodeID.Server)
                        ParryPlayer.HandleActivateSteeledRequest(whoAmI);
                    break;

                case MessageType.SyncParryState:
                    if (Main.netMode == NetmodeID.MultiplayerClient)
                        ParryPlayer.ReceiveSyncedState(reader);
                    break;

                case MessageType.ParryVisualEffect:
                    if (Main.netMode == NetmodeID.MultiplayerClient)
                        ParryPlayer.ReceiveVisualEffect(reader);
                    break;

                case MessageType.StartBossCutscene:
                    if (Main.netMode == NetmodeID.MultiplayerClient)
                    {
                        int npcIndex = reader.ReadInt32();
                        int npcType = reader.ReadInt32();

                        BossCutsceneSystem.ReceiveStart(npcIndex, npcType);
                    }
                    break;

                default:
                    Logger.Warn($"Ignoring unknown packet type {(byte)messageType} from player {whoAmI}.");
                    break;
            }
        }
    }

    public class DownedSystem : ModSystem
    {
        public static bool downedGreatAntlionCharger;

        public static void SetDownedGreatAntlionCharger()
        {
            // Clients never decide world progression. This method is called by the
            // boss's server-side OnKill hook.
            if (Main.netMode == NetmodeID.MultiplayerClient || downedGreatAntlionCharger)
                return;

            downedGreatAntlionCharger = true;

            if (Main.netMode == NetmodeID.Server)
            {
                ModPacket packet = ModContent.GetInstance<ShatteredIllusion>().GetPacket();

                packet.Write((byte)MessageType.SyncDownedGreatAntlionCharger);
                packet.Write(downedGreatAntlionCharger);

                packet.Send();
            }
        }

        internal static void ReceiveDownedState(bool isDowned)
        {
            downedGreatAntlionCharger = isDowned;
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
            downedGreatAntlionCharger =
                tag.ContainsKey("downedGreatAntlionCharger");
        }

        public override void NetSend(BinaryWriter writer)
        {
            writer.Write(downedGreatAntlionCharger);
        }

        public override void NetReceive(BinaryReader reader)
        {
            downedGreatAntlionCharger = reader.ReadBoolean();
        }
    }
}