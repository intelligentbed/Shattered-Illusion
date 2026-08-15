using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.GameContent;
using Terraria.Audio;
using ReLogic.Graphics;
using ReLogic.Utilities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ShatteredIllusion.Content.BossAI;

namespace ShatteredIllusion.Content.BossAI.CutSceneProto //IM ACTUALLY COMMENTING HERE TO EXPLAIN STUFF IT SHIT BREAKS WHILE IM GONE//
{
    public class BossCutsceneSystem : ModSystem
    {
        public static bool IsCutsceneActive { get; private set; } = false;
        public static Vector2 TargetPosition { get; private set; } = Vector2.Zero;
        private static int trackedNpcIndex = -1;
        public static Vector2 SmoothedCameraPos { get; set; } = Vector2.Zero;

        private static int cutsceneTimer = 0;
        private const int CUTSCENE_DURATION = 180; //duration of cutscene in ticks (60 = 1 sec)//
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

        public override void AI(NPC npc)
        {
            if (npc.type != NPCID.KingSlime) return; //THIS LINE IS SUPER IMPORTANT: NPCID is which entity the cutscene will track. Just change NPC ID from EOC to whatver antlion boss ID will be//

            if (!hasTriggeredCutscene)
            {
                BossCutsceneSystem.StartBossCutscene(
                    npc,
                    "     The Isolated Beast     \n--Great Antlion Charger--",
                    screenshakeStartTick: 120,   //screenshake start timestamp//
                    screenshakeDuration: 60,     //shakes for 60 ticks once triggered
                    screenshakeMagnitude: 4,     //severity of screenshake
                    audioSound: new SoundStyle("ShatteredIllusion/Sounds/BarkFart"), //Audio file that plays (prolly gonna wanna make a custom roar sfx)//
                    audioStartTick: 60,         // another time stamp for start - audio//
                    audioDuration: 120);         //plays/loops for 120 ticks (only matters if the sound loops)
                hasTriggeredCutscene = true;
            }

            if (BossCutsceneSystem.IsCutsceneActive)
            {
                npc.dontTakeDamage = true;
            }
            else
            {
                npc.dontTakeDamage = false;
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