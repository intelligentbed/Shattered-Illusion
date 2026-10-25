<<<<<<< Updated upstream
using Microsoft.Xna.Framework;
using ParticleLibrary.Core.V3.Particles;
using ParticleLibrary.Utilities;
using ShatteredIllusion.Common.Visuals;
using ShatteredIllusion.Content.Particles;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using SystemVector2 = System.Numerics.Vector2;

namespace ShatteredIllusion.Content.NPCs.BossAI.VanillaBosses.KingSlime
{
    public static class KingSlimeCinema
    {
        public const string TransformTitle = "";
        public const string TransformSubtitle = "";

        public static readonly ImpactStyle Look = new ImpactStyle
        {
            Flash = new Color(150, 225, 255),
            InnerRing = new Color(200, 245, 255),
            OuterRing = new Color(0, 150, 255),
            Spark = new Color(150, 225, 255),
            DebrisDust = DustID.t_Slime,
            DebrisColor = Color.Cyan,
            SkirtDust = DustID.Smoke,
            SkirtColor = new Color(170, 220, 255),
            Sound = null
        };

        public static readonly TitleStyle TitleLook = new TitleStyle
        {
            Accent = new Color(170, 235, 255),
            AccentDeep = new Color(40, 150, 220)
        };

        public static bool TelegraphEnabled => !Main.dedServ && ModContent.GetInstance<ShatteredIllusionConfig>().SlamTelegraph;
    }

    public class KingSlimeCinemaSystem : ModSystem
    {
        public override void PostSetupContent() => Cinema.Watch(NPCID.KingSlime);
    }

    // CinematicTick reads the fight's phase/step each tick and fires effects on TRANSITIONS,
    // so none of the attack code has to change.
    public partial class KingSlimeOverride
    {
        private static void SlimeImpact(Vector2 pos, float power, bool ground = true) => Cinema.Impact(pos, power, KingSlimeCinema.Look, ground);

        private static void RequestHitStop(KingSlimeInstanceData data, int frames)
        {
            if (Cinema.HitStopEnabled)
                data.HitStopFrames = Math.Max(data.HitStopFrames, frames);
        }

        // Call from PreAI, just before npc.ai[Slot_Timer]++.
        private static bool CinematicHitStop(NPC npc, KingSlimeInstanceData data)
        {
            if (data.HitStopFrames <= 0)
                return false;

            if (!data.HitStopActive)
            {
                data.HitStopActive = true;
                data.HitStopVelocity = npc.velocity;
            }

            data.HitStopFrames--;
            npc.velocity = Vector2.Zero;

            if (data.HitStopFrames <= 0)
            {
                npc.velocity = data.HitStopVelocity;
                data.HitStopActive = false;
            }

            return true;
        }

        // Call from PreAI, after the attack switch.
        private void CinematicTick(NPC npc, KingSlimeInstanceData data)
        {
            if (Main.dedServ)
                return;

            KingSlimeInstanceData introData = data;
            if ((introData.IntroBeats & IntroBeat_Setup) != 0 && (introData.IntroBeats & IntroBeat_Release) == 0)
            {
                introData.IntroBeats |= IntroBeat_Release;
                Cinema.Letterbox(false);
                Cinema.Vignette(0f);
                Cinema.ReleaseCameraOffset();
            }

            AttackPhase phase = CurrentPhase(npc);
            int step = CurrentStep(npc);
            float timer = npc.ai[Slot_Timer];

            int prevPhaseRaw = data.CinePhase;
            int prevStep = data.CineStep;
            data.CinePhase = (int)phase;
            data.CineStep = step;

            if (prevPhaseRaw < 0)
                return;

            AttackPhase prevPhase = (AttackPhase)prevPhaseRaw;
            bool phaseChanged = prevPhase != phase;
            bool stepChanged = prevStep != step;

            if (phaseChanged || stepChanged)
                CinematicBeats(npc, data, prevPhase, prevStep, phase, step);

            CinematicContinuous(npc, data, phase, step, timer);
        }

        private void CinematicBeats(NPC npc, KingSlimeInstanceData data, AttackPhase prevPhase, int prevStep, AttackPhase phase, int step)
        {
            if ((prevPhase == AttackPhase.Jump || prevPhase == AttackPhase.BigJump) && phase == AttackPhase.Cooldown)
                SlimeImpact(npc.Bottom, prevPhase == AttackPhase.BigJump ? 0.45f : 0.28f);

            if (phase == AttackPhase.Teleport && step == TeleportResurfacingStep)
            {
                Cinema.Flash(new Color(0, 170, 255), 0.18f);
                Cinema.ZoomPunch(0.02f);
            }

            if (phase == AttackPhase.HugeJump && step == HugeJumpHoveringStep)
            {
                Cinema.HoldZoom(0.07f);
                Cinema.Vignette(0.4f);
            }

            if (phase == AttackPhase.HugeJump && step == HugeJumpSlammingStep)
            {
                Cinema.HoldZoom(0f);
                Cinema.Vignette(0.15f);
                if (Cinema.Enabled)
                    SoundEngine.PlaySound(SoundID.Item1 with { Pitch = -0.8f, Volume = 0.8f }, npc.Center);
            }

            if (prevPhase == AttackPhase.HugeJump && prevStep == HugeJumpSlammingStep && phase == AttackPhase.Cooldown)
            {
                SlimeImpact(npc.Bottom, 1.15f);
                Cinema.Vignette(0f);
                RequestHitStop(data, 4);
            }

            if (phase == AttackPhase.SplitAttack && step == SplitAttackWindupStep)
            {
                Cinema.HoldZoom(0.09f);
                Cinema.Vignette(0.35f);
            }

            if (phase == AttackPhase.SplitAttack && step == SplitAttackDivergingStep)
            {
                Cinema.HoldZoom(0f);
                Cinema.ReleaseFocus();
                Cinema.Flash(new Color(0, 200, 255), 0.3f);
            }

            if (phase == AttackPhase.SplitAttack && step == SplitAttackSettlingStep)
            {
                SlimeImpact(npc.Center, 1.5f, false);
                Cinema.Vignette(0f);
                RequestHitStop(data, 5);
            }

            if (phase == AttackPhase.TransformToNinja && step == TransformShrinkStep)
            {
                Cinema.Letterbox(true);
                Cinema.HoldZoom(0.2f);
                Cinema.Vignette(0.55f);
                Cinema.Focus(npc.Center, 0.55f);
            }

            if (phase == AttackPhase.TransformToNinja && step == TransformRiseStep)
            {
                Cinema.Flash(new Color(220, 250, 255), 0.95f);
                Cinema.HoldZoom(0.1f);
                SlimeImpact(npc.Center, 1.8f, false);
                Cinema.Title(KingSlimeCinema.TransformTitle, KingSlimeCinema.TransformSubtitle, 150, KingSlimeCinema.TitleLook);

                Vector2 crownPos = npc.Top + new Vector2(0f, -10f);
                for (int i = 0; i < (Cinema.ParticlesEnabled ? 18 : 0); i++)
                {
                    Dust d = Dust.NewDustPerfect(
                        crownPos,
                        DustID.GoldFlame,
                        new Vector2(Main.rand.NextFloat(-5f, 5f), -Main.rand.NextFloat(2f, 8f)),
                        0,
                        default,
                        Main.rand.NextFloat(1.2f, 1.9f));
                    d.noGravity = false;
                }

                RequestHitStop(data, 6);
            }

            if (prevPhase == AttackPhase.TransformToNinja && phase == AttackPhase.NinjaWalk)
            {
                Cinema.Letterbox(false);
                Cinema.HoldZoom(0f);
                Cinema.ReleaseFocus();
                Cinema.Vignette(0f);
            }

            if (phase == AttackPhase.NinjaStab && step == NinjaStabWindupStep)
            {
                Cinema.HoldZoom(0.06f);
                Cinema.Vignette(0.3f);
            }

            if (phase == AttackPhase.NinjaStab && step == NinjaStabStrikeStep)
            {
                Cinema.HoldZoom(0f);
                Cinema.Vignette(0f);
                Cinema.ZoomPunch(0.06f);
                Cinema.Flash(new Color(180, 255, 235), 0.25f);
                Cinema.Shake(npc.Center, new Vector2(npc.direction, 0f), 7f, 10f, 12);

                for (int i = 0; i < (Cinema.ParticlesEnabled ? 6 : 0); i++)
                {
                    Vector2 p = npc.Center + new Vector2(npc.direction * (60f + i * 55f), Main.rand.NextFloat(-6f, 6f));

                    BossParticleSystem.SandStreaks.Create(new ParticleInfo(
                        position: p.ToNumerics(),
                        velocity: new SystemVector2(npc.direction * 3f, 0f),
                        rotation: 0f,
                        scale: new SystemVector2(70f, 6f),
                        color: new Color(150, 255, 230, 255),
                        duration: 10
                    ));
                }
            }

            if (prevPhase == AttackPhase.NinjaStab && phase == AttackPhase.NinjaStunned)
            {
                Cinema.Flash(new Color(255, 240, 190), 0.75f);
                Cinema.ZoomPunch(0.12f);
                Cinema.Letterbox(true, 55);
                Cinema.Vignette(0.35f);
                Cinema.Focus(npc.Center, 0.45f);
                RequestHitStop(data, 8);

                Cinema.Shake(npc.Center, Main.rand.NextVector2Unit(), 12f, 18f, 14);

                if (Cinema.Enabled)
                    SoundEngine.PlaySound(SoundID.Item37 with { Pitch = 0.1f, Volume = 0.9f }, npc.Center);

                if (Cinema.ParticlesEnabled)
                    BossParticleSystem.Shockwaves.Create(new ParticleInfo(
                        position: npc.Center.ToNumerics(),
                        velocity: SystemVector2.Zero,
                        rotation: 0f,
                        scale: new SystemVector2(240f, 240f),
                        color: new Color(255, 225, 140, 255),
                        duration: 20
                    ));

                for (int i = 0; i < (Cinema.ParticlesEnabled ? 16 : 0); i++)
                {
                    Vector2 v = Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(4f, 9f);

                    BossParticleSystem.EmberBursts.Create(new ParticleInfo(
                        position: npc.Center.ToNumerics(),
                        velocity: v.ToNumerics(),
                        rotation: 0f,
                        scale: new SystemVector2(11f, 11f),
                        color: new Color(255, 215, 120, 255),
                        duration: Main.rand.Next(18, 30)
                    ));
                }
            }

            if (prevPhase == AttackPhase.NinjaStunned && phase == AttackPhase.NinjaWalk)
            {
                Cinema.Vignette(0f);
                Cinema.ReleaseFocus();
            }

            if (phase == AttackPhase.Despawn)
                Cinema.ReleaseAll();
        }

        private void CinematicContinuous(NPC npc, KingSlimeInstanceData data, AttackPhase phase, int step, float timer)
        {
            // Landing zone: a fixed outer ellipse plus an inner one closing in on it. Respawned every
            // tick (duration 2) so nothing stacks or flickers.
            if (KingSlimeCinema.TelegraphEnabled && phase == AttackPhase.HugeJump && step == HugeJumpHoveringStep)
            {
                Vector2 target = SlamTargetPos(npc);

                if (target != Vector2.Zero)
                {
                    const float zoneSize = 170f;
                    const float flat = 0.28f;

                    float t = MathHelper.Clamp(timer / 65f, 0f, 1f);
                    Vector2 pos = target - new Vector2(0f, 3f);
                    Color c = Color.Lerp(new Color(0, 190, 255), Color.White, t * t);

                    BossParticleSystem.GroundMarkers.Create(new ParticleInfo(
                        position: pos.ToNumerics(),
                        velocity: SystemVector2.Zero,
                        rotation: 0f,
                        scale: new SystemVector2(zoneSize, zoneSize * flat),
                        color: new Color(0, 190, 255) * (0.25f + 0.35f * t),
                        duration: 2
                    ));

                    float inner = MathHelper.Lerp(zoneSize * 0.2f, zoneSize, t);

                    BossParticleSystem.GroundMarkers.Create(new ParticleInfo(
                        position: pos.ToNumerics(),
                        velocity: SystemVector2.Zero,
                        rotation: 0f,
                        scale: new SystemVector2(inner, inner * flat),
                        color: c * (0.35f + 0.5f * t),
                        duration: 2
                    ));
                }
            }

            bool flair = Cinema.ParticlesEnabled;

            if (flair && phase == AttackPhase.HugeJump && step == HugeJumpSlammingStep)
            {
                for (int i = 0; i < 2; i++)
                {
                    Vector2 p = npc.Center + new Vector2(
                        Main.rand.NextFloat(-npc.width * 0.5f, npc.width * 0.5f),
                        -npc.height * 0.5f - Main.rand.NextFloat(0f, 70f));

                    BossParticleSystem.SandStreaks.Create(new ParticleInfo(
                        position: p.ToNumerics(),
                        velocity: SystemVector2.Zero,
                        rotation: MathHelper.PiOver2,
                        scale: new SystemVector2(46f, 5f),
                        color: new Color(150, 225, 255, 255),
                        duration: 6
                    ));
                }
            }

            if (phase == AttackPhase.TransformToNinja && step == TransformShrinkStep)
                Cinema.Focus(npc.Center, 0.55f);

            if (flair && phase == AttackPhase.TransformToNinja && step == TransformShrinkStep && timer % 2f == 0f)
            {
                float t = MathHelper.Clamp(timer / TransformShrinkDuration, 0f, 1f);
                int streaks = 1 + (int)(t * 2f);

                for (int i = 0; i < streaks; i++)
                {
                    Vector2 offset = Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(80f, 130f);

                    BossParticleSystem.SandStreaks.Create(new ParticleInfo(
                        position: (npc.Center + offset).ToNumerics(),
                        velocity: (-offset * 0.09f).ToNumerics(),
                        rotation: 0f,
                        scale: new SystemVector2(24f, 4f),
                        color: new Color(150, 235, 255, 255) * 0.7f,
                        duration: 10
                    ));
                }
            }

            if (phase == AttackPhase.TransformToNinja && step == TransformRiseStep)
                Cinema.Focus(npc.Center, 0.45f);

            if (flair && phase == AttackPhase.NinjaStab && step == NinjaStabStrikeStep)
            {
                BossParticleSystem.EmberBursts.Create(new ParticleInfo(
                    position: npc.Center.ToNumerics(),
                    velocity: new SystemVector2(-npc.direction * 1.5f, 0f),
                    rotation: 0f,
                    scale: new SystemVector2(16f, 16f),
                    color: new Color(120, 255, 220, 200),
                    duration: 12
                ));
            }

            if (flair && phase == AttackPhase.NinjaStunned && Main.rand.NextBool(3))
            {
                Dust d = Dust.NewDustPerfect(
                    npc.Center + Main.rand.NextVector2Circular(npc.width * 0.5f, npc.height * 0.5f),
                    DustID.Electric,
                    Main.rand.NextVector2Circular(2f, 2f),
                    100,
                    Color.Gold,
                    1.0f);
                d.noGravity = true;
            }
        }
    }
=======
using Microsoft.Xna.Framework;
using ParticleLibrary.Core.V3.Particles;
using ParticleLibrary.Utilities;
using ShatteredIllusion.Common.Visuals;
using ShatteredIllusion.Content.Particles;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using SystemVector2 = System.Numerics.Vector2;

namespace ShatteredIllusion.Content.NPCs.BossAI.VanillaBosses.KingSlime
{
    public static class KingSlimeCinema
    {
        public const string TransformTitle = "";
        public const string TransformSubtitle = "";

        public static readonly ImpactStyle Look = new ImpactStyle
        {
            Flash = new Color(150, 225, 255),
            InnerRing = new Color(200, 245, 255),
            OuterRing = new Color(0, 150, 255),
            Spark = new Color(150, 225, 255),
            DebrisDust = DustID.t_Slime,
            DebrisColor = Color.Cyan,
            SkirtDust = DustID.Smoke,
            SkirtColor = new Color(170, 220, 255),
            Sound = null
        };

        public static readonly TitleStyle TitleLook = new TitleStyle
        {
            Accent = new Color(170, 235, 255),
            AccentDeep = new Color(40, 150, 220)
        };

        public static bool TelegraphEnabled => !Main.dedServ && ModContent.GetInstance<ShatteredIllusionConfig>().SlamTelegraph;
    }

    public class KingSlimeCinemaSystem : ModSystem
    {
        public override void PostSetupContent() => Cinema.Watch(NPCID.KingSlime);
    }

    // CinematicTick reads the fight's phase/step each tick and fires effects on TRANSITIONS,
    // so none of the attack code has to change.
    public partial class KingSlimeOverride
    {
        private static void SlimeImpact(Vector2 pos, float power, bool ground = true) => Cinema.Impact(pos, power, KingSlimeCinema.Look, ground);

        private static void RequestHitStop(KingSlimeInstanceData data, int frames)
        {
            if (Cinema.HitStopEnabled)
                data.HitStopFrames = Math.Max(data.HitStopFrames, frames);
        }

        // Call from PreAI, just before npc.ai[Slot_Timer]++.
        private static bool CinematicHitStop(NPC npc, KingSlimeInstanceData data)
        {
            if (data.HitStopFrames <= 0)
                return false;

            if (!data.HitStopActive)
            {
                data.HitStopActive = true;
                data.HitStopVelocity = npc.velocity;
            }

            data.HitStopFrames--;
            npc.velocity = Vector2.Zero;

            if (data.HitStopFrames <= 0)
            {
                npc.velocity = data.HitStopVelocity;
                data.HitStopActive = false;
            }

            return true;
        }

        // Call from PreAI, after the attack switch.
        private void CinematicTick(NPC npc, KingSlimeInstanceData data)
        {
            if (Main.dedServ)
                return;

            KingSlimeInstanceData introData = data;
            if ((introData.IntroBeats & IntroBeat_Setup) != 0 && (introData.IntroBeats & IntroBeat_Release) == 0)
            {
                introData.IntroBeats |= IntroBeat_Release;
                Cinema.Letterbox(false);
                Cinema.Vignette(0f);
                Cinema.ReleaseCameraOffset();
            }

            AttackPhase phase = CurrentPhase(npc);
            int step = CurrentStep(npc);
            float timer = npc.ai[Slot_Timer];

            int prevPhaseRaw = data.CinePhase;
            int prevStep = data.CineStep;
            data.CinePhase = (int)phase;
            data.CineStep = step;

            if (prevPhaseRaw < 0)
                return;

            AttackPhase prevPhase = (AttackPhase)prevPhaseRaw;
            bool phaseChanged = prevPhase != phase;
            bool stepChanged = prevStep != step;

            if (phaseChanged || stepChanged)
                CinematicBeats(npc, data, prevPhase, prevStep, phase, step);

            CinematicContinuous(npc, data, phase, step, timer);
        }

        private void CinematicBeats(NPC npc, KingSlimeInstanceData data, AttackPhase prevPhase, int prevStep, AttackPhase phase, int step)
        {
            if ((prevPhase == AttackPhase.Jump || prevPhase == AttackPhase.BigJump) && phase == AttackPhase.Cooldown)
                SlimeImpact(npc.Bottom, prevPhase == AttackPhase.BigJump ? 0.45f : 0.28f);

            if (phase == AttackPhase.Teleport && step == TeleportResurfacingStep)
            {
                Cinema.Flash(new Color(0, 170, 255), 0.18f);
                Cinema.ZoomPunch(0.02f);
            }

            if (phase == AttackPhase.HugeJump && step == HugeJumpHoveringStep)
            {
                Cinema.HoldZoom(0.07f);
                Cinema.Vignette(0.4f);
            }

            if (phase == AttackPhase.HugeJump && step == HugeJumpSlammingStep)
            {
                Cinema.HoldZoom(0f);
                Cinema.Vignette(0.15f);
                if (Cinema.Enabled)
                    SoundEngine.PlaySound(SoundID.Item1 with { Pitch = -0.8f, Volume = 0.8f }, npc.Center);
            }

            if (prevPhase == AttackPhase.HugeJump && prevStep == HugeJumpSlammingStep && phase == AttackPhase.Cooldown)
            {
                SlimeImpact(npc.Bottom, 1.15f);
                Cinema.Vignette(0f);
                RequestHitStop(data, 4);
            }

            if (phase == AttackPhase.SplitAttack && step == SplitAttackWindupStep)
            {
                Cinema.HoldZoom(0.09f);
                Cinema.Vignette(0.35f);
            }

            if (phase == AttackPhase.SplitAttack && step == SplitAttackDivergingStep)
            {
                Cinema.HoldZoom(0f);
                Cinema.ReleaseFocus();
                Cinema.Flash(new Color(0, 200, 255), 0.3f);
            }

            if (phase == AttackPhase.SplitAttack && step == SplitAttackSettlingStep)
            {
                SlimeImpact(npc.Center, 1.5f, false);
                Cinema.Vignette(0f);
                RequestHitStop(data, 5);
            }

            if (phase == AttackPhase.TransformToNinja && step == TransformShrinkStep)
            {
                Cinema.Letterbox(true);
                Cinema.HoldZoom(0.2f);
                Cinema.Vignette(0.55f);
                Cinema.Focus(npc.Center, 0.55f);
            }

            if (phase == AttackPhase.TransformToNinja && step == TransformRiseStep)
            {
                Cinema.Flash(new Color(220, 250, 255), 0.95f);
                Cinema.HoldZoom(0.1f);
                SlimeImpact(npc.Center, 1.8f, false);
                Cinema.Title(KingSlimeCinema.TransformTitle, KingSlimeCinema.TransformSubtitle, 150, KingSlimeCinema.TitleLook);

                Vector2 crownPos = npc.Top + new Vector2(0f, -10f);
                for (int i = 0; i < (Cinema.ParticlesEnabled ? 18 : 0); i++)
                {
                    Dust d = Dust.NewDustPerfect(
                        crownPos,
                        DustID.GoldFlame,
                        new Vector2(Main.rand.NextFloat(-5f, 5f), -Main.rand.NextFloat(2f, 8f)),
                        0,
                        default,
                        Main.rand.NextFloat(1.2f, 1.9f));
                    d.noGravity = false;
                }

                RequestHitStop(data, 6);
            }

            if (prevPhase == AttackPhase.TransformToNinja && phase == AttackPhase.NinjaWalk)
            {
                Cinema.Letterbox(false);
                Cinema.HoldZoom(0f);
                Cinema.ReleaseFocus();
                Cinema.Vignette(0f);
            }

            if (phase == AttackPhase.NinjaStab && step == NinjaStabWindupStep)
            {
                Cinema.HoldZoom(0.06f);
                Cinema.Vignette(0.3f);
            }

            if (phase == AttackPhase.NinjaStab && step == NinjaStabStrikeStep)
            {
                Cinema.HoldZoom(0f);
                Cinema.Vignette(0f);
                Cinema.ZoomPunch(0.06f);
                Cinema.Flash(new Color(180, 255, 235), 0.25f);
                Cinema.Shake(npc.Center, new Vector2(npc.direction, 0f), 7f, 10f, 12);

                for (int i = 0; i < (Cinema.ParticlesEnabled ? 6 : 0); i++)
                {
                    Vector2 p = npc.Center + new Vector2(npc.direction * (60f + i * 55f), Main.rand.NextFloat(-6f, 6f));

                    BossParticleSystem.SandStreaks.Create(new ParticleInfo(
                        position: p.ToNumerics(),
                        velocity: new SystemVector2(npc.direction * 3f, 0f),
                        rotation: 0f,
                        scale: new SystemVector2(70f, 6f),
                        color: new Color(150, 255, 230, 255),
                        duration: 10
                    ));
                }
            }

            if (prevPhase == AttackPhase.NinjaStab && phase == AttackPhase.NinjaStunned)
            {
                Cinema.Flash(new Color(255, 240, 190), 0.75f);
                Cinema.ZoomPunch(0.12f);
                Cinema.Letterbox(true, 55);
                Cinema.Vignette(0.35f);
                Cinema.Focus(npc.Center, 0.45f);
                RequestHitStop(data, 8);

                Cinema.Shake(npc.Center, Main.rand.NextVector2Unit(), 12f, 18f, 14);

                if (Cinema.Enabled)
                    SoundEngine.PlaySound(SoundID.Item37 with { Pitch = 0.1f, Volume = 0.9f }, npc.Center);

                if (Cinema.ParticlesEnabled)
                    BossParticleSystem.Shockwaves.Create(new ParticleInfo(
                        position: npc.Center.ToNumerics(),
                        velocity: SystemVector2.Zero,
                        rotation: 0f,
                        scale: new SystemVector2(240f, 240f),
                        color: new Color(255, 225, 140, 255),
                        duration: 20
                    ));

                for (int i = 0; i < (Cinema.ParticlesEnabled ? 16 : 0); i++)
                {
                    Vector2 v = Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(4f, 9f);

                    BossParticleSystem.EmberBursts.Create(new ParticleInfo(
                        position: npc.Center.ToNumerics(),
                        velocity: v.ToNumerics(),
                        rotation: 0f,
                        scale: new SystemVector2(11f, 11f),
                        color: new Color(255, 215, 120, 255),
                        duration: Main.rand.Next(18, 30)
                    ));
                }
            }

            if (prevPhase == AttackPhase.NinjaStunned && phase == AttackPhase.NinjaWalk)
            {
                Cinema.Vignette(0f);
                Cinema.ReleaseFocus();
            }

            if (phase == AttackPhase.NinjaSlash && step == NinjaSlashSwingStep)
            {
                Cinema.ZoomPunch(0.04f);
                Cinema.Flash(new Color(255, 230, 180), 0.18f);
            }

            if (phase == AttackPhase.NinjaVanish && step == NinjaVanishAppearStep)
                Cinema.Flash(new Color(180, 255, 235), 0.15f);

            if (phase == AttackPhase.TransformToSlime && step == RevertRiseStep)
            {
                Cinema.Flash(new Color(255, 230, 160), 0.6f);
                Cinema.ZoomPunch(0.08f);
                SlimeImpact(npc.Center, 1.4f, false);
            }

            if (phase == AttackPhase.Despawn)
                Cinema.ReleaseAll();
        }

        private void CinematicContinuous(NPC npc, KingSlimeInstanceData data, AttackPhase phase, int step, float timer)
        {
            // Landing zone: a fixed outer ellipse plus an inner one closing in on it. Respawned every
            // tick (duration 2) so nothing stacks or flickers.
            if (KingSlimeCinema.TelegraphEnabled && phase == AttackPhase.HugeJump && step == HugeJumpHoveringStep)
            {
                Vector2 target = SlamTargetPos(npc);

                if (target != Vector2.Zero)
                {
                    const float zoneSize = 170f;
                    const float flat = 0.28f;

                    float t = MathHelper.Clamp(timer / 65f, 0f, 1f);
                    Vector2 pos = target - new Vector2(0f, 3f);
                    Color c = Color.Lerp(new Color(0, 190, 255), Color.White, t * t);

                    BossParticleSystem.GroundMarkers.Create(new ParticleInfo(
                        position: pos.ToNumerics(),
                        velocity: SystemVector2.Zero,
                        rotation: 0f,
                        scale: new SystemVector2(zoneSize, zoneSize * flat),
                        color: new Color(0, 190, 255) * (0.25f + 0.35f * t),
                        duration: 2
                    ));

                    float inner = MathHelper.Lerp(zoneSize * 0.2f, zoneSize, t);

                    BossParticleSystem.GroundMarkers.Create(new ParticleInfo(
                        position: pos.ToNumerics(),
                        velocity: SystemVector2.Zero,
                        rotation: 0f,
                        scale: new SystemVector2(inner, inner * flat),
                        color: c * (0.35f + 0.5f * t),
                        duration: 2
                    ));
                }
            }

            bool flair = Cinema.ParticlesEnabled;

            if (flair && phase == AttackPhase.HugeJump && step == HugeJumpSlammingStep)
            {
                for (int i = 0; i < 2; i++)
                {
                    Vector2 p = npc.Center + new Vector2(
                        Main.rand.NextFloat(-npc.width * 0.5f, npc.width * 0.5f),
                        -npc.height * 0.5f - Main.rand.NextFloat(0f, 70f));

                    BossParticleSystem.SandStreaks.Create(new ParticleInfo(
                        position: p.ToNumerics(),
                        velocity: SystemVector2.Zero,
                        rotation: MathHelper.PiOver2,
                        scale: new SystemVector2(46f, 5f),
                        color: new Color(150, 225, 255, 255),
                        duration: 6
                    ));
                }
            }

            if (phase == AttackPhase.TransformToNinja && step == TransformShrinkStep)
                Cinema.Focus(npc.Center, 0.55f);

            if (flair && phase == AttackPhase.TransformToNinja && step == TransformShrinkStep && timer % 2f == 0f)
            {
                float t = MathHelper.Clamp(timer / TransformShrinkDuration, 0f, 1f);
                int streaks = 1 + (int)(t * 2f);

                for (int i = 0; i < streaks; i++)
                {
                    Vector2 offset = Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(80f, 130f);

                    BossParticleSystem.SandStreaks.Create(new ParticleInfo(
                        position: (npc.Center + offset).ToNumerics(),
                        velocity: (-offset * 0.09f).ToNumerics(),
                        rotation: 0f,
                        scale: new SystemVector2(24f, 4f),
                        color: new Color(150, 235, 255, 255) * 0.7f,
                        duration: 10
                    ));
                }
            }

            if (phase == AttackPhase.TransformToNinja && step == TransformRiseStep)
                Cinema.Focus(npc.Center, 0.45f);

            if (flair && phase == AttackPhase.NinjaStab && step == NinjaStabStrikeStep)
            {
                BossParticleSystem.EmberBursts.Create(new ParticleInfo(
                    position: npc.Center.ToNumerics(),
                    velocity: new SystemVector2(-npc.direction * 1.5f, 0f),
                    rotation: 0f,
                    scale: new SystemVector2(16f, 16f),
                    color: new Color(120, 255, 220, 200),
                    duration: 12
                ));
            }

            if (flair && phase == AttackPhase.NinjaStunned && Main.rand.NextBool(3))
            {
                Dust d = Dust.NewDustPerfect(
                    npc.Center + Main.rand.NextVector2Circular(npc.width * 0.5f, npc.height * 0.5f),
                    DustID.Electric,
                    Main.rand.NextVector2Circular(2f, 2f),
                    100,
                    Color.Gold,
                    1.0f);
                d.noGravity = true;
            }
        }
    }
>>>>>>> Stashed changes
}