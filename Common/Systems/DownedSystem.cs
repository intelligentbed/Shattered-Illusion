using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using ShatteredIllusion.Core.Packets;

namespace ShatteredIllusion.Common.Systems
{
    public class DownedSystem : ModSystem
    {
        public static bool downedGreatAntlionCharger;

        public static void SetDownedGreatAntlionCharger()
        {
            // Clients never decide world progression
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