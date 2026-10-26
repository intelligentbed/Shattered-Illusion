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
    public class CutsceneData
    {
        public string TitleKey = "Mods.ShatteredIllusion.Cutscenes.Unknown.Title";
        public int TotalTicks = 220;
        public int IntroTicks = 45;
        public int OutroTicks = 40;
        public float Zoom = 1.8f;
        public bool FreezeNpc = true;

        public int ShakeStart = -1;
        public int ShakeDuration;
        public int ShakeMagnitude;

        public SoundStyle? Sound;
        public int SoundStart = -1;
        public int SoundDuration;
    }

    internal static class CutsceneDefinition
    {
        public static CutsceneData Get(int npcType)
        {
            if (npcType == ModContent.NPCType<GreatAntlionCharger>())
            {
                return new CutsceneData
                {
                    TitleKey = "Mods.ShatteredIllusion.Cutscenes.GreatAntlionCharger.Title",
                    Zoom = 1.9f,
                    ShakeStart = 0,
                    ShakeDuration = 200,
                    ShakeMagnitude = 4,
                    Sound = new SoundStyle("ShatteredIllusion/Sounds/Silence"),
                    SoundStart = 0,
                    SoundDuration = 120
                };
            }

            if (npcType == NPCID.KingSlime)
            {
                return new CutsceneData
                {
                    TitleKey = "Mods.ShatteredIllusion.Cutscenes.KingSlime.Title",
                    FreezeNpc = false,
                    ShakeStart = 120,
                    ShakeDuration = 60,
                    ShakeMagnitude = 4,
                    Sound = new SoundStyle("ShatteredIllusion/Sounds/Silence"), //im sorry barkfart but im showcasing right now so bye bye
                    SoundStart = 60,
                    SoundDuration = 120
                };
            }

            return null;
        }
    }

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

    public class CutscenePlayer : ModPlayer
    {
        public bool Active { get; private set; }
        public CutsceneData Data { get; private set; }
        public int Timer { get; set; }
        public bool ShakeFired { get; set; }
        public bool SoundFired { get; private set; }

        private int npcIndex = -1;
        private int npcType = -1;
        private float savedZoom = 1f;
        private SlotId activeAudio;

        public static float Ease(float t)
        {
            t = MathHelper.Clamp(t, 0f, 1f);
            return t * t * t * (t * (t * 6f - 15f) + 10f);
        }

        public float Blend
        {
            get
            {
                if (!Active) return 0f;
                if (Timer < Data.IntroTicks)
                    return Ease(Timer / (float)Data.IntroTicks);

                int outroStart = Data.TotalTicks - Data.OutroTicks;
                if (Timer > outroStart)
                    return 1f - Ease((Timer - outroStart) / (float)Data.OutroTicks);

                return 1f;
            }
        }

        private float HoldProgress
        {
            get
            {
                float length = Math.Max(1f, Data.TotalTicks - Data.OutroTicks - Data.IntroTicks);
                return MathHelper.Clamp((Timer - Data.IntroTicks) / length, 0f, 1f);
            }
        }

        public override void OnEnterWorld()
        {
            if (Player.whoAmI == Main.myPlayer)
                EndCutscene();
        }

        public override void OnRespawn()
        {
            if (Player.whoAmI == Main.myPlayer)
                EndCutscene();
        }

        public override void PreUpdateMovement()
        {
            if (Player.whoAmI != Main.myPlayer || !Active) return;

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
            if (Player.whoAmI != Main.myPlayer || !Active) return;
            if (!TryGetNpc(out NPC npc)) return;

            float blend = Blend;
            Vector2 focus = Vector2.Lerp(Player.Center, npc.Center, blend);

            float zoom = MathHelper.Lerp(savedZoom, Data.Zoom * (1f + 0.06f * HoldProgress), blend);
            Main.GameZoomTarget = zoom;

            Main.screenPosition = focus - new Vector2(Main.screenWidth / 2f, Main.screenHeight / 2f);
        }

        public void StartCutscene(NPC npc)
        {
            if (Main.dedServ) return;

            CutsceneData data = CutsceneDefinition.Get(npc.type);
            if (data == null) return;

            if (Active)
                EndCutscene();

            Data = data;
            Active = true;
            npcIndex = npc.whoAmI;
            npcType = npc.type;
            savedZoom = Main.GameZoomTarget;
            Timer = 0;
            ShakeFired = false;
            SoundFired = false;
            activeAudio = default;
        }

        public bool TryGetNpc(out NPC npc)
        {
            npc = null;
            if (npcIndex < 0 || npcIndex >= Main.maxNPCs) return false;

            NPC tracked = Main.npc[npcIndex];
            if (!tracked.active || tracked.type != npcType) return false;

            npc = tracked;
            return true;
        }

        public void PlayAudio()
        {
            if (SoundFired || !Data.Sound.HasValue) return;

            activeAudio = SoundEngine.PlaySound(Data.Sound.Value);
            SoundFired = true;
        }

        public void StopAudio()
        {
            if (SoundEngine.TryGetActiveSound(activeAudio, out var sound))
                sound.Stop();
        }

        public void EndCutscene()
        {
            if (!Active) return;

            StopAudio();

            Active = false;
            Data = null;
            npcIndex = -1;
            npcType = -1;
            Timer = 0;
            ShakeFired = false;
            SoundFired = false;

            if (Player.whoAmI == Main.myPlayer)
                Main.GameZoomTarget = savedZoom;
        }
    }

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