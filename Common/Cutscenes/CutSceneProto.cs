using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
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

namespace ShatteredIllusion.Common.Cutscenes //REFACTORED THE WHOLE SHIBANG 
{
    public class BossCutsceneSystem : ModSystem
    {
        private const int CUTSCENE_DURATION = 200; //duration of cutscene in ticks (60 = 1 sec). Yes bro i know how ticks work im not DUMB

        // title card layout all tuned from the BOTTOM of the screen now instead of the top
        private const float TITLE_BOTTOM_MARGIN = 130f; // how far up from the bottom edge the subtitle line sits
        private const float TITLE_LINE_GAP = 26f;       // vertical gap between the big title and the small subtitle
        private const float BACKDROP_PADDING = 22f;      // extra room the dark backing bar gives around the text
        private const float ORNAMENT_GAP = 16f;          // gap between subtitle text and the little accent lines/diamonds
        private const float ORNAMENT_LENGTH = 46f;       // how long the flanking accent lines are
        private static readonly Color AccentColor = new Color(255, 205, 120); // used for the subtitle + ornaments

        public override void PostUpdateEverything()
        {
            if (Main.dedServ) return;

            CutscenePlayer cutscenePlayer = Main.LocalPlayer.GetModPlayer<CutscenePlayer>();
            if (!cutscenePlayer.IsCutsceneActive) return;

            if (!cutscenePlayer.TryGetTrackedNPC(out NPC tracked))
            {
                cutscenePlayer.EndCutscene();
                return;
            }

            cutscenePlayer.TargetPosition = tracked.Center;
            cutscenePlayer.CutsceneTimer++;

            if (!cutscenePlayer.ScreenshakeTriggered &&
                cutscenePlayer.ScreenshakeStartTick >= 0 &&
                cutscenePlayer.CutsceneTimer >= cutscenePlayer.ScreenshakeStartTick)
            {
                ScreenshakePlayer shakePlayer = Main.LocalPlayer.GetModPlayer<ScreenshakePlayer>();
                shakePlayer.StartShake(cutscenePlayer.ScreenshakeDuration, cutscenePlayer.ScreenshakeMagnitude);
                cutscenePlayer.ScreenshakeTriggered = true;
            }

            if (!cutscenePlayer.AudioTriggered &&
                cutscenePlayer.AudioStartTick >= 0 &&
                cutscenePlayer.CutsceneTimer >= cutscenePlayer.AudioStartTick)
            {
                cutscenePlayer.PlayAudio();
            }

            if (cutscenePlayer.AudioTriggered &&
                cutscenePlayer.AudioDuration > 0 &&
                cutscenePlayer.CutsceneTimer >= cutscenePlayer.AudioStartTick + cutscenePlayer.AudioDuration)
            {
                cutscenePlayer.StopAudio();
            }

            if (cutscenePlayer.CutsceneTimer >= CUTSCENE_DURATION)
            {
                cutscenePlayer.EndCutscene();
            }
        }

        public override void PostDrawInterface(SpriteBatch spriteBatch)
        {
            if (Main.dedServ) return;

            CutscenePlayer cutscenePlayer = Main.LocalPlayer.GetModPlayer<CutscenePlayer>();
            if (!cutscenePlayer.IsCutsceneActive) return;

            if (cutscenePlayer.CutsceneTimer < cutscenePlayer.TitleFadeInStart ||
                cutscenePlayer.CutsceneTimer > cutscenePlayer.TitleFadeOutStart + cutscenePlayer.TitleFadeOutTicks)
            {
                return;
            }

            float alpha = 1f;
            if (cutscenePlayer.CutsceneTimer < cutscenePlayer.TitleFadeInStart + cutscenePlayer.TitleFadeInTicks)
            {
                alpha = (cutscenePlayer.CutsceneTimer - cutscenePlayer.TitleFadeInStart) /
                        (float)cutscenePlayer.TitleFadeInTicks;
            }
            else if (cutscenePlayer.CutsceneTimer > cutscenePlayer.TitleFadeOutStart)
            {
                alpha = 1f - (cutscenePlayer.CutsceneTimer - cutscenePlayer.TitleFadeOutStart) /
                        (float)cutscenePlayer.TitleFadeOutTicks;
            }
            alpha = MathHelper.Clamp(alpha, 0f, 1f);

            float introProgress = MathHelper.Clamp(
                (cutscenePlayer.CutsceneTimer - cutscenePlayer.TitleFadeInStart) / (float)cutscenePlayer.TitleFadeInTicks,
                0f, 1f);
            float scale = MathHelper.Lerp(0.85f, 1f, introProgress);
            float yDrift = MathHelper.Lerp(14f, 0f, introProgress);

            string[] lines = cutscenePlayer.TitleText.Split('\n');
            string titleLine = lines.Length > 0 ? lines[0].Trim() : "";
            string subLine = lines.Length > 1 ? lines[1].Trim() : "";
            string cleanedSub = subLine.Trim('-', ' ');

            var titleFont = FontAssets.DeathText.Value;
            var subFont = FontAssets.MouseText.Value;

            Vector2 titleTextSize = titleFont.MeasureString(titleLine);
            Vector2 subTextSize = subFont.MeasureString(cleanedSub);

            // anchor everything off the BOTTOM of the screen
            float centerX = Main.screenWidth / 2f;
            float subCenterY = Main.screenHeight - TITLE_BOTTOM_MARGIN + yDrift;
            float titleCenterY = subCenterY - (subTextSize.Y / 2f) - TITLE_LINE_GAP - (titleTextSize.Y * scale / 2f);

            // dark backdrop
            float backdropTop = titleCenterY - (titleTextSize.Y * scale / 2f) - BACKDROP_PADDING;
            float backdropBottom = subCenterY + (subTextSize.Y / 2f) + BACKDROP_PADDING;
            DrawFeatheredBackdrop(spriteBatch, backdropTop, backdropBottom, alpha);

            // expanding gold rule between the title and the subtitle, grows in with the intro
            float ruleWidth = MathHelper.Lerp(0f, 220f, introProgress);
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            float ruleY = titleCenterY + (titleTextSize.Y * scale / 2f) + (TITLE_LINE_GAP / 2f);

            spriteBatch.Draw(
                pixel,
                new Rectangle((int)(centerX - ruleWidth / 2f), (int)ruleY, (int)ruleWidth, 2),
                AccentColor * alpha * 0.9f);

            DrawSubtitleOrnaments(spriteBatch, pixel, centerX, subCenterY, subTextSize.X, alpha);

            // main title, drawn bigger with a soft multi-directional outline instead of one flat shadow
            DrawOutlinedString(
                spriteBatch, titleFont, titleLine,
                new Vector2(centerX, titleCenterY), Color.White * alpha, Color.Black * alpha * 0.75f, scale);

            // subtitle line, smaller and in the gold accent color so it reads as a "label" under the title
            DrawOutlinedString(
                spriteBatch, subFont, cleanedSub,
                new Vector2(centerX, subCenterY), AccentColor * alpha, Color.Black * alpha * 0.75f, 1f);
        }

        internal static void ReceiveStart(int npcIndex, int npcType)
        {
            if (Main.dedServ) return;
            if (npcIndex < 0 || npcIndex >= Main.maxNPCs) return;

            NPC npc = Main.npc[npcIndex];
            if (!npc.active || npc.type != npcType) return;

            Main.LocalPlayer.GetModPlayer<CutscenePlayer>().StartCutscene(npc);
        }

        private static void DrawFeatheredBackdrop(SpriteBatch spriteBatch, float top, float bottom, float alpha)
        {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            const int featherBands = 8;
            float totalHeight = bottom - top;
            float bandHeight = totalHeight / featherBands;

            for (int i = 0; i < featherBands; i++)
            {
                float bandCenter = (i + 0.5f) / featherBands;
                float edgeDistance = MathHelper.Min(bandCenter, 1f - bandCenter) * 2f;
                float bandAlpha = MathHelper.Clamp(edgeDistance, 0f, 1f) * 0.5f * alpha;
                float y = top + bandHeight * i;

                spriteBatch.Draw(
                    pixel,
                    new Rectangle(0, (int)y, Main.screenWidth, (int)bandHeight + 1),
                    Color.Black * bandAlpha);
            }
        }

        // short gold line + little rotated-square "diamond" on each side of the subtitle text, idk how i feel about it so might scrap
        private static void DrawSubtitleOrnaments(
            SpriteBatch spriteBatch, Texture2D pixel, float centerX, float centerY, float subTextWidth, float alpha)
        {
            float lineStart = subTextWidth / 2f + ORNAMENT_GAP;
            float lineEnd = lineStart + ORNAMENT_LENGTH;

            spriteBatch.Draw(
                pixel,
                new Rectangle((int)(centerX + lineStart), (int)centerY, (int)ORNAMENT_LENGTH, 1),
                AccentColor * alpha);

            DrawDiamond(spriteBatch, pixel, new Vector2(centerX + lineEnd + 8f, centerY), alpha);

            spriteBatch.Draw(
                pixel,
                new Rectangle((int)(centerX - lineEnd), (int)centerY, (int)ORNAMENT_LENGTH, 1),
                AccentColor * alpha);

            DrawDiamond(spriteBatch, pixel, new Vector2(centerX - lineEnd - 8f, centerY), alpha);
        }

        private static void DrawDiamond(SpriteBatch spriteBatch, Texture2D pixel, Vector2 center, float alpha)
        {
            const float diamondSize = 6f;
            Vector2 origin = new Vector2(0.5f, 0.5f);

            spriteBatch.Draw(
                pixel,
                center,
                null,
                AccentColor * alpha,
                MathHelper.PiOver4,
                origin,
                diamondSize,
                SpriteEffects.None,
                0f);
        }

        private static void DrawOutlinedString(
            SpriteBatch spriteBatch,
            DynamicSpriteFont font,
            string text,
            Vector2 center,
            Color color,
            Color outlineColor,
            float scale)
        {
            if (string.IsNullOrEmpty(text)) return;

            Vector2 size = font.MeasureString(text);
            Vector2 origin = size / 2f;

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;

                    Vector2 offset = new Vector2(dx, dy);

                    spriteBatch.DrawString(
                        font,
                        text,
                        center + offset,
                        outlineColor,
                        0f,
                        origin,
                        scale,
                        SpriteEffects.None,
                        0f);
                }
            }

            spriteBatch.DrawString(
                font,
                text,
                center,
                color,
                0f,
                origin,
                scale,
                SpriteEffects.None,
                0f);
        }
    }

    public class CutscenePlayer : ModPlayer
    {
        public bool IsCutsceneActive { get; private set; }
        public int TrackedNpcIndex { get; private set; } = -1;
        public int TrackedNpcType { get; private set; } = -1;
        public int CutsceneTimer { get; set; }
        public Vector2 TargetPosition { get; set; }
        public Vector2 SmoothedCameraPos { get; private set; }

        public string TitleText { get; private set; } = "";
        public int TitleFadeInStart { get; private set; } = 40;
        public int TitleFadeInTicks { get; private set; } = 20;
        public int TitleFadeOutStart { get; private set; } = 140;
        public int TitleFadeOutTicks { get; private set; } = 20;

        public int ScreenshakeStartTick { get; private set; } = -1;
        public int ScreenshakeDuration { get; private set; }
        public int ScreenshakeMagnitude { get; private set; }
        public bool ScreenshakeTriggered { get; set; }

        public SoundStyle AudioSound { get; private set; }
        public int AudioStartTick { get; private set; } = -1;
        public int AudioDuration { get; private set; }
        public bool AudioTriggered { get; private set; }

        private SlotId activeAudio;
        private float savedPlayerZoom = 1f;

        private static void SetGlobalGameZoomTarget(float value)
        {
            Main.GameZoomTarget = value;
        }

        public override void OnEnterWorld()
        {
            if (Main.dedServ) return;
            if (Player.whoAmI == Main.myPlayer)
                ResetCutscene();
        }

        public override void PlayerDisconnect()
        {
            if (Main.dedServ) return;
            // Reset cutscene when this player disconnects.
            ResetCutscene();
        }

        public override void OnRespawn()
        {
            if (Main.dedServ) return;
            if (Player.whoAmI == Main.myPlayer)
                ResetCutscene();
        }

        public override void PreUpdateMovement()
        {
            if (Main.dedServ) return;
            if (Player.whoAmI != Main.myPlayer) return;
            if (!IsCutsceneActive) return;

            Player.velocity = Vector2.Zero;
            Player.controlLeft = false;
            Player.controlRight = false;
            Player.controlUp = false;
            Player.controlDown = false;
            Player.controlJump = false;
            Player.controlUseItem = false;
        }

        public override void ModifyScreenPosition()
        {
            if (Main.dedServ) return;
            if (Player.whoAmI != Main.myPlayer) return;
            if (!IsCutsceneActive) return;

            SetGlobalGameZoomTarget(2.0f);

            Vector2 viewportCenter = new Vector2(
                Main.screenWidth / 2f,
                Main.screenHeight / 2f);

            Vector2 desiredScreenPos = TargetPosition - viewportCenter;

            SmoothedCameraPos = Vector2.Lerp(
                SmoothedCameraPos,
                desiredScreenPos,
                0.05f);

            Main.screenPosition = SmoothedCameraPos;
        }

        public void StartCutscene(NPC npc)
        {
            if (Main.dedServ) return;
            if (IsCutsceneActive)
                EndCutscene();

            IsCutsceneActive = true;
            TrackedNpcIndex = npc.whoAmI;
            TrackedNpcType = npc.type;
            TargetPosition = npc.Center;
            SmoothedCameraPos = Main.screenPosition;
            savedPlayerZoom = Main.GameZoomTarget;

            CutsceneDefinition.Apply(npc.type, this);

            CutsceneTimer = 0;
            ScreenshakeTriggered = false;
            AudioTriggered = false;
            activeAudio = default;
        }

        public bool TryGetTrackedNPC(out NPC npc)
        {
            npc = null;

            if (TrackedNpcIndex < 0 || TrackedNpcIndex >= Main.maxNPCs)
                return false;

            NPC tracked = Main.npc[TrackedNpcIndex];

            if (!tracked.active)
                return false;

            if (tracked.type != TrackedNpcType)
                return false;

            npc = tracked;
            return true;
        }

        public void PlayAudio()
        {
            if (Main.dedServ) return;
            if (AudioTriggered) return;
            if (AudioStartTick < 0) return;

            activeAudio = SoundEngine.PlaySound(AudioSound);
            AudioTriggered = true;
        }

        public void StopAudio()
        {
            if (Main.dedServ) return;

            if (SoundEngine.TryGetActiveSound(activeAudio, out var sound))
            {
                sound.Stop();
            }

            AudioDuration = 0;
        }

        public void EndCutscene()
        {
            if (Main.dedServ) return;

            StopAudio();

            IsCutsceneActive = false;
            TrackedNpcIndex = -1;
            TrackedNpcType = -1;
            CutsceneTimer = 0;
            TargetPosition = Vector2.Zero;
            SmoothedCameraPos = Vector2.Zero;

            ScreenshakeStartTick = -1;
            ScreenshakeDuration = 0;
            ScreenshakeMagnitude = 0;
            ScreenshakeTriggered = false;

            AudioSound = default;
            AudioStartTick = -1;
            AudioDuration = 0;
            AudioTriggered = false;

            SetGlobalGameZoomTarget(savedPlayerZoom);
        }

        public void ResetCutscene()
        {
            if (Main.dedServ) return;
            EndCutscene();
        }

        internal void SetDefinition(
            string titleText,
            int titleFadeInStart,
            int titleFadeInTicks,
            int titleFadeOutStart,
            int titleFadeOutTicks,
            int screenshakeStartTick,
            int screenshakeDuration,
            int screenshakeMagnitude,
            SoundStyle audioSound,
            int audioStartTick,
            int audioDuration)
        {
            TitleText = titleText;
            TitleFadeInStart = titleFadeInStart;
            TitleFadeInTicks = titleFadeInTicks;
            TitleFadeOutStart = titleFadeOutStart;
            TitleFadeOutTicks = titleFadeOutTicks;

            ScreenshakeStartTick = screenshakeStartTick;
            ScreenshakeDuration = screenshakeDuration;
            ScreenshakeMagnitude = screenshakeMagnitude;

            AudioSound = audioSound;
            AudioStartTick = audioStartTick;
            AudioDuration = audioDuration;
        }
    }

    internal static class CutsceneDefinition
    {
        public static void Apply(int npcType, CutscenePlayer player)
        {
            if (npcType == ModContent.NPCType<GreatAntlionCharger>())
            {
                GreatAntlionChargerCutscene.Apply(player);
                return;
            }

            if (npcType == NPCID.KingSlime)
            {
                KingSlimeCutscene.Apply(player);
                return;
            }

            player.SetDefinition(
                Language.GetTextValue("Mods.ShatteredIllusion.Cutscenes.Unknown.Title"),
                40, 20, 140, 20,
                -1, 0, 0,
                default, -1, 0);
        }
    }

    internal static class GreatAntlionChargerCutscene
    {
        public static void Apply(CutscenePlayer player)
        {
            player.SetDefinition(
                Language.GetTextValue("The Isolated Beast      \nGreat Antlion Charger"),
                40, 20, 140, 20,
                0, 200, 4,
                new SoundStyle("ShatteredIllusion/Sounds/Silence"), 0, 120);
        }
    }

    internal static class KingSlimeCutscene
    {
        public static void Apply(CutscenePlayer player)
        {
            player.SetDefinition(
                Language.GetTextValue("The Viscious Monarch      \n           --King Slime--"),
                60, 20, 150, 20,
                120, 60, 4,
                new SoundStyle("ShatteredIllusion/Sounds/BarkFart"), 60, 120);
        }
    }

    public class UniversalBossCutsceneNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        private int cutsceneTimer;
        private bool cutsceneWasActive;

        public override void OnSpawn(NPC npc, IEntitySource source)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            if (!IsRegisteredBoss(npc.type))
                return;

            StartServerCutscene(npc);
        }

        public override bool PreAI(NPC npc)
        {
            if (!IsRegisteredBoss(npc.type))
                return true;

            if (cutsceneTimer > 0)
            {
                npc.velocity = Vector2.Zero;
                npc.dontTakeDamage = true;

                cutsceneWasActive = true;
                cutsceneTimer--;

                return false;
            }

            if (cutsceneWasActive)
            {
                cutsceneWasActive = false;
                npc.dontTakeDamage = false;

                if (Main.netMode != NetmodeID.MultiplayerClient &&
                    npc.ModNPC is GreatAntlionCharger antlion)
                {
                    antlion.FinishIntro();
                }
            }

            return true;
        }

        private void StartServerCutscene(NPC npc)
        {
            cutsceneTimer = 200;

            if (Main.netMode == NetmodeID.Server)
            {
                const float maxDistance = 3000f;
                float maxDistanceSquared = maxDistance * maxDistance;

                for (int i = 0; i < Main.maxPlayers; i++)
                {
                    Player player = Main.player[i];

                    if (!player.active)
                        continue;

                    if (Vector2.DistanceSquared(player.Center, npc.Center) > maxDistanceSquared)
                        continue;

                    // Tell this specific client to start the cutscene.
                    SIPackets.SendBossCutscene(npc, i);
                }
            }
            else
            {
                // Singleplayer starts the cutscene directly.
                if (Main.myPlayer >= 0 &&
                    Main.myPlayer < Main.maxPlayers)
                {
                    Main.LocalPlayer
                        .GetModPlayer<CutscenePlayer>()
                        .StartCutscene(npc);
                }
            }
        }

        private static bool IsRegisteredBoss(int type)
        {
            return type == ModContent.NPCType<GreatAntlionCharger>() ||
                   type == NPCID.KingSlime;
        }
    }

    public class ScreenshakePlayer : ModPlayer
    {
        public int screenshakeTimer;
        public int screenshakeMagnitude;

        public void StartShake(int duration, int magnitude)
        {
            if (Main.dedServ) return;

            screenshakeTimer = Math.Max(screenshakeTimer, duration);
            screenshakeMagnitude = Math.Max(screenshakeMagnitude, magnitude);
        }

        public override void ModifyScreenPosition()
        {
            if (Main.dedServ) return;
            if (Player.whoAmI != Main.myPlayer) return;
            if (screenshakeTimer <= 0) return;

            screenshakeTimer--;

            Main.screenPosition += new Vector2(
                Main.rand.Next(-screenshakeMagnitude, screenshakeMagnitude + 1),
                Main.rand.Next(-screenshakeMagnitude, screenshakeMagnitude + 1));

            if (screenshakeTimer <= 0)
                screenshakeMagnitude = 0;
        }
    }
}
