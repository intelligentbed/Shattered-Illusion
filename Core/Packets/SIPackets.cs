using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ShatteredIllusion.Core.Packets
{
    public enum MessageType : byte
    {
        SyncDownedGreatAntlionCharger = 1,
        TutorialProcUpdate = 2,
        RequestParry = 3,
        RequestActivateSteeled = 4,
        SyncParryState = 5,
        ParryVisualEffect = 6,
        StartBossCutscene = 7,
        SyncAntlionDiveState = 8,
    }

    internal static class SIPackets
    {
        public static void SendBossCutscene(NPC npc, int toClient)
        {
            if (Main.netMode != NetmodeID.Server)
                return;
            ModPacket packet = ModContent.GetInstance<ShatteredIllusion>().GetPacket();
            packet.Write((byte)MessageType.StartBossCutscene);
            packet.Write(npc.whoAmI); packet.Write(npc.type);
            packet.Send(toClient);
        }

        public static void SendAntlionDiveState(NPC npc, float landingX, float groundY)
        {
            if (Main.netMode != NetmodeID.Server)
                return;

            ModPacket packet = ModContent.GetInstance<ShatteredIllusion>().GetPacket();
            packet.Write((byte)MessageType.SyncAntlionDiveState);
            packet.Write(npc.whoAmI);
            packet.Write(landingX);
            packet.Write(groundY);
            packet.Send();
        }
    }
}