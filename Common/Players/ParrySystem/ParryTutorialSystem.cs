using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ShatteredIllusion.Content.NPCs.BossAI.GreatAntlionCharger;
using ShatteredIllusion.Core.OverrideSystem;
using ShatteredIllusionKeybinds;
using System.IO;
using System.Linq;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.UI.Chat;

namespace ShatteredIllusion.Common.Players.ParrySystem
{
    /// <summary>
    /// Draws a fading parry hint the first few times a parryable attack opens up.
    /// Capped per-world across all tutorial bosses combined.
    /// </summary>
    public class ParryTutorialSystem : ModSystem
    {
        public const int MaxTutorialProcs = 5;

        private const int DisplayDuration = 240; // ~4 seconds
        private const int FadeFrames = 30;

        public static int ProcCount { get; private set; }

        private static int displayTimer = 0;

        public override void SaveWorldData(TagCompound tag)
        {
            tag["parryTutorialProcCount"] = ProcCount;
        }

        public override void LoadWorldData(TagCompound tag)
        {
            ProcCount = tag.GetInt("parryTutorialProcCount");
        }

        // Keeps a newly-joining client in sync with the world's current count.
        public override void NetSend(BinaryWriter writer)
        {
            writer.Write(ProcCount);
        }

        public override void NetReceive(BinaryReader reader)
        {
            ProcCount = reader.ReadInt32();
        }

        /// <summary>
        /// Call once per rising edge of a boss becoming parryable. No ops past the world cap.
        /// In multiplayer, the server decides and broadcasts; clients just wait for that packet.
        /// </summary>
        public static void TryShowTutorial(NPC source)
        {
            if (ProcCount >= MaxTutorialProcs)
                return;

            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            ProcCount++;
            displayTimer = DisplayDuration;

            if (Main.netMode == NetmodeID.Server)
            {
                // Swap YourMod for your actual Mod class type.
                ModPacket packet = ModContent.GetInstance<ShatteredIllusion>().GetPacket();
                packet.Write((byte)ParryPacketType.TutorialProcUpdate);
                packet.Write(ProcCount);
                packet.Send();
            }
        }

        /// <summary>Call from your Mod.HandlePacket when reading a TutorialProcUpdate packet.</summary>
        public static void ReceiveProcUpdate(int newCount)
        {
            ProcCount = newCount;
            displayTimer = DisplayDuration;
        }

        public override void PostUpdateEverything()
        {
            if (displayTimer > 0)
                displayTimer--;
        }

        public override void PostDrawInterface(SpriteBatch spriteBatch)
        {
            if (displayTimer <= 0)
                return;

            float alpha = 1f;
            if (displayTimer < FadeFrames)
                alpha = displayTimer / (float)FadeFrames;
            else if (displayTimer > DisplayDuration - FadeFrames)
                alpha = (DisplayDuration - displayTimer) / (float)FadeFrames;

            string assignedKey = KeybindSystem.ParryKeybind.GetAssignedKeys().FirstOrDefault() ?? "?";

            string line1 = "This attack can be PARRIED!";
            string line2 = $"Press [{assignedKey}] when the enemy flashes red.";

            var font = FontAssets.MouseText.Value;
            Vector2 anchor = new Vector2(Main.screenWidth / 2f, 90f);

            Vector2 size1 = font.MeasureString(line1);
            Vector2 size2 = font.MeasureString(line2);

            float panelWidth = System.Math.Max(size1.X, size2.X) + 40f;
            float panelHeight = size1.Y + size2.Y + 20f;
            Vector2 panelPos = anchor - new Vector2(panelWidth / 2f, panelHeight / 2f);

            Texture2D pixel = TextureAssets.MagicPixel.Value;
            spriteBatch.Draw(
                pixel,
                new Rectangle((int)panelPos.X, (int)panelPos.Y, (int)panelWidth, (int)panelHeight),
                Color.Black * (0.55f * alpha)
            );

            ChatManager.DrawColorCodedStringWithShadow(
                spriteBatch, font, line1,
                anchor - new Vector2(size1.X / 2f, size1.Y + 4f),
                Color.Gold * alpha, 0f, Vector2.Zero, Vector2.One
            );

            ChatManager.DrawColorCodedStringWithShadow(
                spriteBatch, font, line2,
                anchor - new Vector2(size2.X / 2f, -4f),
                Color.White * alpha, 0f, Vector2.Zero, Vector2.One
            );
        }
    }

    public enum ParryPacketType : byte
    {
        TutorialProcUpdate
    }

    /// <summary>
    /// Watches the tutorial eligible bosses and fires the hint on the rising edge
    /// of IsParryable. 
    /// </summary>
    public class ParryTutorialWatcherNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        private bool wasParryableLastTick;

        public override bool AppliesToEntity(NPC entity, bool lateRequest)
        {
            return entity.type == NPCID.KingSlime
                || entity.type == NPCID.EyeofCthulhu
                || entity.type == ModContent.NPCType<GreatAntlionCharger>();
        }

        public override void PostAI(NPC npc)
        {
            bool isParryableNow = GetIsParryable(npc);

            if (isParryableNow && !wasParryableLastTick)
                ParryTutorialSystem.TryShowTutorial(npc);

            wasParryableLastTick = isParryableNow;
        }

        private static bool GetIsParryable(NPC npc)
        {
            if (npc.ModNPC is IParryable modNpcBoss)
                return modNpcBoss.IsParryable;

            if (NPCBehaviorOverrideLoader.TryGet(npc.type, out var container) &&
                container.BehaviorOverride is IParryable overrideBoss)
            {
                return overrideBoss.IsParryable;
            }

            return false;
        }
    }
}