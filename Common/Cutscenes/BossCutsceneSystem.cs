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
    public class BossCutsceneSystem : ModSystem
    {
        private const float BAR_FRACTION = 0.12f;
        private const float TITLE_MAX_SCALE = 1.1f;
        private const float TITLE_MAX_WIDTH_FRACTION = 0.6f;
        private const float SUBTITLE_SCALE = 1.1f;
        private const float SUBTITLE_TRACKING = 5f;
        private const int LETTER_STAGGER = 2;
        private static readonly Color Accent = new Color(255, 205, 120);

        public override void PostUpdateEverything()
        {
            if (Main.dedServ) return;

            CutscenePlayer cp = Main.LocalPlayer.GetModPlayer<CutscenePlayer>();
            if (!cp.Active) return;

            if (!cp.TryGetNpc(out _))
            {
                cp.EndCutscene();
                return;
            }

            cp.Timer++;
            CutsceneData data = cp.Data;

            if (!cp.ShakeFired && data.ShakeStart >= 0 && cp.Timer >= data.ShakeStart)
            {
                Main.LocalPlayer.GetModPlayer<ScreenshakePlayer>().StartShake(data.ShakeDuration, data.ShakeMagnitude);
                cp.ShakeFired = true;
            }

            if (!cp.SoundFired && data.Sound.HasValue && data.SoundStart >= 0 && cp.Timer >= data.SoundStart)
                cp.PlayAudio();

            if (cp.SoundFired && data.SoundDuration > 0 && cp.Timer >= data.SoundStart + data.SoundDuration)
                cp.StopAudio();

            if (cp.Timer >= data.TotalTicks)
                cp.EndCutscene();
        }

        public override void PostDrawInterface(SpriteBatch spriteBatch)
        {
            if (Main.dedServ) return;

            CutscenePlayer cp = Main.LocalPlayer.GetModPlayer<CutscenePlayer>();
            if (!cp.Active) return;

            float blend = cp.Blend;
            int w = Main.screenWidth;
            int h = Main.screenHeight;
            Texture2D pixel = TextureAssets.MagicPixel.Value;

            spriteBatch.Draw(pixel, new Rectangle(0, 0, w, h), Color.Black * (0.25f * blend));

            int bar = (int)(h * BAR_FRACTION * blend);
            spriteBatch.Draw(pixel, new Rectangle(0, 0, w, bar), Color.Black);
            spriteBatch.Draw(pixel, new Rectangle(0, h - bar, w, bar), Color.Black);

            DrawTitle(spriteBatch, pixel, cp);
        }

        private static void DrawTitle(SpriteBatch sb, Texture2D pixel, CutscenePlayer cp)
        {
            CutsceneData data = cp.Data;
            string[] lines = Language.GetTextValue(data.TitleKey).Split('\n');
            string title = lines.Length > 0 ? lines[0].Trim() : "";
            string subtitle = lines.Length > 1 ? lines[1].Trim('-', ' ').ToUpperInvariant() : "";

            CutsceneTitleRenderer.Draw(
                sb,
                pixel,
                title,
                subtitle,
                cp.Timer,
                data.IntroTicks + 10,
                data.TotalTicks - data.OutroTicks - 20,
                20f,
                Main.screenWidth,
                Main.screenHeight,
                Accent);
        }

        internal static void ReceiveStart(int npcIndex, int npcType)
        {
            if (Main.dedServ) return;
            if (npcIndex < 0 || npcIndex >= Main.maxNPCs) return;

            NPC npc = Main.npc[npcIndex];
            if (!npc.active || npc.type != npcType) return;

            Main.LocalPlayer.GetModPlayer<CutscenePlayer>().StartCutscene(npc);
        }
    }
}