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
}