using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Utilities;
using ShatteredIllusion.Content.NPCs.BossAI.GreatAntlionCharger;
using ShatteredIllusion.Core.Packets;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace ShatteredIllusion.Common.Cutscenes
{
    public class UniversalBossCutsceneNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        private int holdTimer;
        private bool wasActive;
        private CutsceneData data;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
        {
            return CutsceneDefinition.Get(entity.type) != null;
        }

        public override void OnSpawn(NPC npc, IEntitySource source)
        {
            data = CutsceneDefinition.Get(npc.type);
            if (data == null) return;

            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            holdTimer = data.TotalTicks;
            npc.netUpdate = true;

            if (Main.netMode == NetmodeID.SinglePlayer)
            {
                Main.LocalPlayer.GetModPlayer<CutscenePlayer>().StartCutscene(npc);
                return;
            }

            // clients drop the cutscene packet if they haven't been told about the NPC yet
            NetMessage.SendData(MessageID.SyncNPC, number: npc.whoAmI);

            const float maxDistance = 3000f;
            float maxDistanceSquared = maxDistance * maxDistance;

            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player player = Main.player[i];
                if (!player.active) continue;
                if (Vector2.DistanceSquared(player.Center, npc.Center) > maxDistanceSquared) continue;

                SIPackets.SendBossCutscene(npc, i);
            }
        }

        public override bool PreAI(NPC npc)
        {
            if (holdTimer > 0)
            {
                if (data.FreezeNpc)
                    npc.velocity = Vector2.Zero;

                npc.dontTakeDamage = true;
                wasActive = true;
                holdTimer--;

                return !data.FreezeNpc;
            }

            if (wasActive)
            {
                wasActive = false;
                npc.dontTakeDamage = false;

                if (npc.ModNPC is GreatAntlionCharger antlion)
                    antlion.FinishIntro();
            }

            return true;
        }
    }
}