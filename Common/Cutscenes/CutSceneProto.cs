using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using ReLogic.Utilities;
using ShatteredIllusion.Content.NPCs.BossAI.GreatAntlionCharger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace ShatteredIllusion.Common.Cutscenes //IM ACTUALLY COMMENTING HERE TO EXPLAIN STUFF IT SHIT BREAKS WHILE IM GONE//
{
    public class BossCutsceneSystem : ModSystem
    {
        public static bool IsCutsceneActive { get; private set; } = false;
        public static Vector2 TargetPosition { get; private set; } = Vector2.Zero;
        private static int trackedNpcIndex = -1;
        public static Vector2 SmoothedCameraPos { get; set; } = Vector2.Zero;

        private static int cutsceneTimer = 0;
        private const int CUTSCENE_DURATION = 200; //duration of cutscene in ticks (60 = 1 sec). Yes bro i know how ticks work im not DUMB//
        private const int TITLE_FADE_IN_START = 40;
        private const int TITLE_FADE_IN_TICKS = 20;
        private const int TITLE_FADE_OUT_START = 140;
        private const int TITLE_FADE_OUT_TICKS = 20; //each number is a timestamp (if you want title to spawn at 2 sec, title fade in would be at 120//

        private static string bossTitleText = "";
        private static float savedPlayerZoom = 1f;

        private static int screenshakeStartTick = -1;
        private static int screenshakeDuration = 0;
        private static int screenshakeMagnitude = 0;
        private static bool screenshakeTriggered = false;
        private static SoundStyle audioSound;
        private static int audioStartTick = -1;
        private static int audioDuration = 0;
        private static bool audioTriggered = false;
        private static SlotId activeAudio;

        public static void StartBossCutscene(NPC npc, string titleText = null,
            int screenshakeStartTick = -1, int screenshakeDuration = 0, int screenshakeMagnitude = 0,
            SoundStyle? audioSound = null, int audioStartTick = -1, int audioDuration = 0)
        {
            IsCutsceneActive = true;
            trackedNpcIndex = npc.whoAmI;
            TargetPosition = npc.Center;
            cutsceneTimer = 0;
            savedPlayerZoom = Main.GameZoomTarget;
            SmoothedCameraPos = Main.screenPosition;
            bossTitleText = titleText ?? npc.GivenOrTypeName;

            BossCutsceneSystem.screenshakeStartTick = screenshakeStartTick;
            BossCutsceneSystem.screenshakeDuration = screenshakeDuration;
            BossCutsceneSystem.screenshakeMagnitude = screenshakeMagnitude;
            screenshakeTriggered = false;

            BossCutsceneSystem.audioSound = audioSound ?? default;
            BossCutsceneSystem.audioStartTick = audioStartTick;
            BossCutsceneSystem.audioDuration = audioDuration;
            audioTriggered = false;
            activeAudio = default;
        }

        public override void PostUpdateEverything()
        {
            if (!IsCutsceneActive) return;
            NPC tracked = (trackedNpcIndex >= 0 && trackedNpcIndex < Main.maxNPCs) ? Main.npc[trackedNpcIndex] : null;
            if (tracked == null || !tracked.active)
            {
                EndCutscene();
                return;
            }
            TargetPosition = tracked.Center;

            cutsceneTimer++;

            if (!screenshakeTriggered && screenshakeStartTick >= 0 && cutsceneTimer >= screenshakeStartTick)
            {
                var shakePlayer = Main.LocalPlayer.GetModPlayer<ScreenshakePlayer>();
                shakePlayer.screenshakeTimer = screenshakeDuration;
                shakePlayer.screenshakeMagnitude = screenshakeMagnitude;
                screenshakeTriggered = true;
            }

            if (!audioTriggered && audioStartTick >= 0 && cutsceneTimer >= audioStartTick)
            {
                activeAudio = SoundEngine.PlaySound(audioSound);
                audioTriggered = true;
            }

            if (audioTriggered && audioDuration > 0 && cutsceneTimer >= audioStartTick + audioDuration)
            {
                if (SoundEngine.TryGetActiveSound(activeAudio, out var sound))
                {
                    sound.Stop();
                }
                audioDuration = 0; 
            }

            if (cutsceneTimer >= CUTSCENE_DURATION)
            {
                EndCutscene();
            }
        }

        private static void EndCutscene()
        {
            IsCutsceneActive = false;
            trackedNpcIndex = -1;
            screenshakeStartTick = -1;
            if (SoundEngine.TryGetActiveSound(activeAudio, out var sound))
            {
                sound.Stop();
            }
            Main.GameZoomTarget = savedPlayerZoom;
        }

        public override void PostDrawInterface(SpriteBatch spriteBatch)
        {
            if (!IsCutsceneActive) return;
            if (cutsceneTimer < TITLE_FADE_IN_START || cutsceneTimer > TITLE_FADE_OUT_START + TITLE_FADE_OUT_TICKS) return;

            float alpha = 1f;
            if (cutsceneTimer < TITLE_FADE_IN_START + TITLE_FADE_IN_TICKS)
            {
                alpha = (cutsceneTimer - TITLE_FADE_IN_START) / (float)TITLE_FADE_IN_TICKS;
            }
            else if (cutsceneTimer > TITLE_FADE_OUT_START)
            {
                alpha = 1f - (cutsceneTimer - TITLE_FADE_OUT_START) / (float)TITLE_FADE_OUT_TICKS;
            }
            alpha = MathHelper.Clamp(alpha, 0f, 1f);

            var font = FontAssets.DeathText.Value;
            Vector2 size = font.MeasureString(bossTitleText);
            Vector2 pos = new Vector2(Main.screenWidth / 2f, Main.screenHeight * 0.25f) - size / 2f;

            spriteBatch.DrawString(font, bossTitleText, pos + new Vector2(2f, 2f), Color.Black * alpha * 0.6f);
            spriteBatch.DrawString(font, bossTitleText, pos, Color.White * alpha);
        }

    }

    public class CutscenePlayer : ModPlayer
    {
        public override void PreUpdateMovement()
        {
            if (BossCutsceneSystem.IsCutsceneActive)
            {
                Player.velocity = Vector2.Zero;
                Player.controlLeft = false;
                Player.controlRight = false;
                Player.controlUp = false;
                Player.controlDown = false;
                Player.controlJump = false;
                Player.controlUseItem = false;
            }
        }

        public override void ModifyScreenPosition()
        {
            if (Player.whoAmI != Main.myPlayer) return;
            if (!BossCutsceneSystem.IsCutsceneActive) return;

            Main.GameZoomTarget = 2.0f;
            Vector2 viewportCenter = new Vector2(Main.screenWidth / 2f, Main.screenHeight / 2f);
            Vector2 desiredScreenPos = BossCutsceneSystem.TargetPosition - viewportCenter;
            BossCutsceneSystem.SmoothedCameraPos = Vector2.Lerp(BossCutsceneSystem.SmoothedCameraPos, desiredScreenPos, 0.05f);
            Main.screenPosition = BossCutsceneSystem.SmoothedCameraPos;
        }
    }

    public class UniversalBossCutsceneNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        private bool hasTriggeredCutscene = false;

        // Use OnSpawn so it triggers BEFORE any PreAI returns false
        public override void OnSpawn(NPC npc, IEntitySource source)
        {
            if (!IsRegisteredBoss(npc.type)) return;

            if (!hasTriggeredCutscene)
            {
                TriggerBossCutscene(npc);
                hasTriggeredCutscene = true;
            }
        }

        public override bool PreAI(NPC npc)
        {
            if (!IsRegisteredBoss(npc.type)) return true;

            // Apply invulnerability while cutscene is playing
            npc.dontTakeDamage = BossCutsceneSystem.IsCutsceneActive;

            return true;
        }

        private bool IsRegisteredBoss(int type)
        {
            return type == ModContent.NPCType<GreatAntlionCharger>() || type == NPCID.KingSlime;
        }

        private void TriggerBossCutscene(NPC npc)
        {
            switch (npc.type)
            {
                case int type when type == ModContent.NPCType<GreatAntlionCharger>():
                    BossCutsceneSystem.StartBossCutscene(
                        npc,
                        "      The Isolated Beast      \n--Great Antlion Charger--",
                        screenshakeStartTick: 0,
                        screenshakeDuration: 200,
                        screenshakeMagnitude: 4,
                        audioSound: new SoundStyle("ShatteredIllusion/Sounds/Silence"),
                        audioStartTick: 0,
                        audioDuration: 120);
                    break;

                case NPCID.KingSlime:
                    BossCutsceneSystem.StartBossCutscene(
                        npc,
                        "      The Crowned Aberration      \n           --King Slime--",
                        screenshakeStartTick: 120,
                        screenshakeDuration: 60,
                        screenshakeMagnitude: 4,
                        audioSound: new SoundStyle("ShatteredIllusion/Sounds/BarkFart"),
                        audioStartTick: 60,
                        audioDuration: 120);
                    break;
            }
        }
    }

    public class ScreenshakePlayer : ModPlayer
    {
        public int screenshakeTimer;
        public int screenshakeMagnitude;

        public override void ModifyScreenPosition()
        {
            if (screenshakeTimer > 0)
            {
                screenshakeTimer--;
                Main.screenPosition += new Vector2(
                    Main.rand.Next(-screenshakeMagnitude, screenshakeMagnitude + 1),
                    Main.rand.Next(-screenshakeMagnitude, screenshakeMagnitude + 1));
            }
        }
    }
}