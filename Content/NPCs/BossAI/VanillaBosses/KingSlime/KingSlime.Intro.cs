using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ShatteredIllusion.Common.Cutscenes;
using ShatteredIllusion.Common.Visuals;
using ShatteredIllusion.Content.Particles;
using ParticleLibrary.Core.V3.Particles;
using ParticleLibrary.Utilities;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using SystemVector2 = System.Numerics.Vector2;

namespace ShatteredIllusion.Content.NPCs.BossAI.VanillaBosses.KingSlime
{
    public partial class KingSlimeOverride
    {
        // ---- intro timeline (cutscene ticks, cutscene lasts 200) ------------
        private const int IntroFallStart = 30;
        private const int IntroLand = 120;          // matches the cutscene's screenshake tick
        private const int IntroCrownStart = 128;
        private const int IntroCrownDuration = 28;
        private const int IntroReleaseTick = 175;
        private const float IntroFallHeight = 900f;
        private const float IntroCrownFallHeight = 800f;
        private const float IntroCameraLift = 200f; // camera starts looking up at the sky
        private const float IntroSpawnDistance = 320f; // horizontal distance from the player where he lands

        private const int IntroBeat_Setup = 1;
        private const int IntroBeat_Whoosh = 2;
        private const int IntroBeat_Land = 4;
        private const int IntroBeat_Crown = 8;
        private const int IntroBeat_Release = 16;

        // ---- death timeline (ticks since the killing blow) -------------------
        private const int DeathFreezeFrames = 10;       // singleplayer hit-stop on the final blow
        private const int DeathDissolveStart = 6;
        private const int DeathDissolveFrameTicks = 12; // 6 frames * 12 = 72 ticks of slow melt
        private const int DeathCrownStart = 22;
        private const int DeathCrownDuration = 30;
        private const int DeathShatterTick = 112;
        private const int DeathEndTick = 150;
        private const float DeathCrownFallHeight = 800f;

        private const int DeathBeat_Start = 1;
        private const int DeathBeat_Contact = 2;
        private const int DeathBeat_Shatter = 4;

        private static readonly Color CrownGold = new Color(255, 225, 120);

        private static Texture2D GetCrownTexture() =>
            (crownAsset ??= ModContent.Request<Texture2D>("ShatteredIllusion/Assets/ExtraTextures/Resprites/Extra_39")).Value;

        private static float Clamp01(float v) => MathHelper.Clamp(v, 0f, 1f);

        private static void GoldBurst(Vector2 pos, int count, float speed, float scaleMin, float scaleMax, bool gravity)
        {
            for (int i = 0; i < count; i++)
            {
                Vector2 v = Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(speed * 0.35f, speed);
                v.Y -= 2f;

                Dust d = Dust.NewDustPerfect(
                    pos + Main.rand.NextVector2Circular(8f, 8f),
                    DustID.GoldFlame,
                    v,
                    0,
                    default,
                    Main.rand.NextFloat(scaleMin, scaleMax));

                d.noGravity = !gravity;
            }
        }

        private static void GoldRing(Vector2 pos, float size, int duration)
        {
            BossParticleSystem.Shockwaves.Create(new ParticleInfo(
                position: pos.ToNumerics(),
                velocity: SystemVector2.Zero,
                rotation: 0f,
                scale: new SystemVector2(size, size),
                color: new Color(255, 225, 140, 255),
                duration: duration
            ));
        }

        // =================================================================
        //  INTRO
        // =================================================================
        private struct IntroVisual
        {
            public bool Active;
            public bool BodyVisible;
            public bool CrownVisible;
            public float BodyOffsetY;
            public float CrownOffsetY;
            public float CrownRotation;
        }

        // Read by PreDraw. Everything the draw code needs for the entrance.
        private static IntroVisual GetIntroVisual(NPC npc)
        {
            IntroVisual v = default;

            if (Main.dedServ || !Cinema.IntroEnabled)
                return v;

            CutscenePlayer cp = Main.LocalPlayer.GetModPlayer<CutscenePlayer>();

            if (!cp.Active || !cp.TryGetNpc(out NPC trackedNpc) || trackedNpc.whoAmI != npc.whoAmI)
                return v;

            int t = cp.Timer;
            v.Active = true;

            v.BodyVisible = t >= IntroFallStart;
            if (v.BodyVisible)
            {
                // Accelerating fall: slow at first, fastest right before impact.
                float u = Clamp01((t - IntroFallStart) / (float)(IntroLand - IntroFallStart));
                v.BodyOffsetY = -IntroFallHeight * (1f - u * u);
            }

            v.CrownVisible = t >= IntroCrownStart;
            if (v.CrownVisible)
            {
                float d = Clamp01((t - IntroCrownStart) / (float)IntroCrownDuration);
                v.CrownOffsetY = -IntroCrownFallHeight * (1f - EaseOutBounce(d));

                float dir = npc.spriteDirection >= 0 ? 1f : -1f;
                v.CrownRotation = (1f - d) * MathHelper.TwoPi * 1.25f * dir + 0.14f * EaseOutBounce(d) * dir;
            }

            return v;
        }

        // Called from PreAI while the boss cutscene is running.
        private void CinematicIntro(NPC npc)
        {
            if (Main.dedServ || !Cinema.IntroEnabled)
                return;

            CutscenePlayer cp = Main.LocalPlayer.GetModPlayer<CutscenePlayer>();

            if (!cp.Active || !cp.TryGetNpc(out NPC trackedNpc) || trackedNpc.whoAmI != npc.whoAmI)
                return;

            KingSlimeInstanceData data = npc.GetGlobalNPC<KingSlimeInstanceData>();
            int t = cp.Timer;

            // Keeps the idle wobble + landing squish spring alive during the cutscene.
            UpdateJellyWobble(npc, data);

            if ((data.IntroBeats & IntroBeat_Setup) == 0)
            {
                data.IntroBeats |= IntroBeat_Setup;

                // Pull him in next to the player (on whichever side vanilla put him), then stand him on the ground.
                Vector2 playerPos = Main.LocalPlayer.Center;
                float side = npc.Center.X >= playerPos.X ? 1f : -1f;
                Vector2 spawnSpot = new Vector2(playerPos.X + side * IntroSpawnDistance, playerPos.Y);

                if (TryFindGroundedTeleportPosition(spawnSpot, npc.height, out Vector2 grounded))
                {
                    npc.Center = grounded;
                    npc.velocity = Vector2.Zero;
                }

                Cinema.Letterbox(true);
                Cinema.Vignette(0.55f);
            }

            // Camera: starts tilted up at the empty sky, then pans down to meet him.
            float pan = MathHelper.SmoothStep(0f, 1f, Clamp01((t - 70f) / 60f));
            Cinema.CameraOffset(new Vector2(0f, -IntroCameraLift * (1f - pan)));

            // Falling: speed lines and slime flecks trailing above him.
            if (t >= IntroFallStart && t < IntroLand)
            {
                IntroVisual v = GetIntroVisual(npc);
                Vector2 bodyCenter = npc.Center + new Vector2(0f, v.BodyOffsetY);

                if ((data.IntroBeats & IntroBeat_Whoosh) == 0 && t >= IntroFallStart + 40)
                {
                    data.IntroBeats |= IntroBeat_Whoosh;
                    SoundEngine.PlaySound(SoundID.Item1 with { Pitch = -0.9f, Volume = 0.9f }, bodyCenter);
                }

                if (Cinema.ParticlesEnabled)
                {
                    for (int i = 0; i < 2; i++)
                    {
                        Vector2 p = bodyCenter + new Vector2(
                            Main.rand.NextFloat(-npc.width * 0.5f, npc.width * 0.5f),
                            -npc.height * 0.5f - Main.rand.NextFloat(0f, 90f));

                        BossParticleSystem.SandStreaks.Create(new ParticleInfo(
                            position: p.ToNumerics(),
                            velocity: SystemVector2.Zero,
                            rotation: MathHelper.PiOver2,
                            scale: new SystemVector2(60f, 5f),
                            color: new Color(150, 225, 255, 255),
                            duration: 7
                        ));
                    }
                }

                if (Main.rand.NextBool(2))
                {
                    Dust d = Dust.NewDustPerfect(
                        bodyCenter + Main.rand.NextVector2Circular(npc.width * 0.5f, npc.height * 0.5f),
                        DustID.t_Slime,
                        new Vector2(Main.rand.NextFloat(-1f, 1f), -Main.rand.NextFloat(3f, 8f)),
                        120,
                        Color.Cyan,
                        Main.rand.NextFloat(1.1f, 1.7f));
                    d.noGravity = true;
                }
            }

            // Impact.
            if ((data.IntroBeats & IntroBeat_Land) == 0 && t >= IntroLand)
            {
                data.IntroBeats |= IntroBeat_Land;

                SlimeImpact(npc.Bottom, 1.7f);
                Cinema.Vignette(0.3f);

                data.SquishOffset = -0.32f;
                data.SquishSpringVel = 0f;
            }

            // The crown clangs onto his head.
            int contactTick = IntroCrownStart + (int)(0.3636f * IntroCrownDuration);

            if ((data.IntroBeats & IntroBeat_Crown) == 0 && t >= contactTick)
            {
                data.IntroBeats |= IntroBeat_Crown;

                Vector2 head = npc.Top + new Vector2(0f, 6f);

                GoldBurst(head, 14, 6f, 1.1f, 1.7f, gravity: false);
                SoundEngine.PlaySound(SoundID.Item37 with { Pitch = -0.2f, Volume = 0.9f }, head);

                Cinema.Flash(new Color(255, 235, 170), 0.35f);
                Cinema.ZoomPunch(0.04f);

                // He squashes a little under the weight.
                data.SquishOffset = Math.Min(data.SquishOffset, 0f) - 0.1f;
            }

            if ((data.IntroBeats & IntroBeat_Release) == 0 && t >= IntroReleaseTick)
            {
                data.IntroBeats |= IntroBeat_Release;
                Cinema.Letterbox(false);
                Cinema.Vignette(0f);
                Cinema.ReleaseCameraOffset();
            }
        }

        // =================================================================
        //  DEATH
        // =================================================================

        // Called by KingSlimeInstanceData.CheckDead. Returns true to CANCEL the death
        // (he stays alive while the scene plays), false to let it through.
        internal static bool InterceptDeath(NPC npc, KingSlimeInstanceData data)
        {
            if (data.AllowDeath)
                return false;

            AttackPhase phase = CurrentPhase(npc);

            if (phase == AttackPhase.Death)
            {
                npc.life = Math.Max(npc.life, 1);
                return true;
            }

            // Leaving the fight anyway; no scene needed.
            if (phase == AttackPhase.Despawn)
                return false;

            bool ninja = phase == AttackPhase.NinjaWalk
                || phase == AttackPhase.NinjaStab
                || phase == AttackPhase.NinjaSlash
                || phase == AttackPhase.NinjaVanish
                || phase == AttackPhase.NinjaCounter
                || phase == AttackPhase.NinjaStunned
                || (phase == AttackPhase.TransformToNinja && CurrentStep(npc) == TransformRiseStep);

            data.ArenaActive = false;
            data.CrownT = 0f;

            if (IsWeaponFormPhase(phase))
            {
                data.WeaponMorph = 0f;
                ResizeKeepingCenter(npc, data.OriginalWidth, data.OriginalHeight);
            }

            if (ninja)
            {
                SetSlashHitbox(npc, false);
                npc.alpha = SlimeAlpha;
            }

            SetPhase(npc, AttackPhase.Death);
            SetStep(npc, ninja ? 1 : 0); // step doubles as "which form" for the whole scene
            npc.ai[Slot_Timer] = 0f;

            npc.life = 1;
            npc.dontTakeDamage = true;
            npc.damage = 0;
            npc.velocity.X *= 0.3f;
            npc.timeLeft = 3600;

            if (IsAuthority)
            {
                RecallAllMinions(npc);
                npc.netUpdate = true;
            }

            return true;
        }

        private static bool DeathIsNinja(NPC npc) => CurrentStep(npc) == 1;

        private static Vector2 DeathCrownRest(NPC npc, Texture2D crown) =>
            new Vector2(npc.Center.X, npc.Bottom.Y - crown.Height * 0.5f * npc.scale);

        private void DoDeathScene(NPC npc, KingSlimeInstanceData data)
        {
            // Final-blow freeze (singleplayer). Freezes the scene clock too.
            if (CinematicHitStop(npc, data))
                return;

            bool ninja = DeathIsNinja(npc);

            npc.dontTakeDamage = true;
            npc.damage = 0;
            npc.life = Math.Max(npc.life, 1);
            npc.timeLeft = 3600;
            npc.noTileCollide = false;
            npc.noGravity = false;
            npc.velocity.X *= 0.85f;
            npc.velocity.Y = Math.Min(npc.velocity.Y + 0.5f, 14f);

            ref float timer = ref npc.ai[Slot_Timer];
            int t = (int)timer;
            timer++;

            if (!ninja)
            {
                // Slime form: flatten into a puddle, then fade after the shatter.
                npc.scale = MathHelper.Lerp(npc.scale, BaseScale, 0.1f);

                float melt = Clamp01((t - 4f) / 55f);
                melt = 1f - (1f - melt) * (1f - melt);
                data.SquishOffset = MathHelper.Lerp(0f, -0.55f, melt);
                data.WobbleClock += 1f;

                float fade = Clamp01((t - DeathShatterTick) / 35f);
                npc.alpha = (int)MathHelper.Lerp(SlimeAlpha, 255, fade);

                if (t % 3 == 0 && t < DeathShatterTick && !Main.dedServ)
                {
                    Dust d = Dust.NewDustPerfect(
                        npc.Bottom + new Vector2(Main.rand.NextFloat(-npc.width * 0.5f, npc.width * 0.5f), -4f),
                        DustID.t_Slime,
                        new Vector2(Main.rand.NextFloat(-2f, 2f), -Main.rand.NextFloat(1f, 3f)),
                        120,
                        Color.Cyan,
                        Main.rand.NextFloat(1.2f, 1.8f));
                    d.noGravity = true;
                }
            }

            if (!Main.dedServ)
                DeathClientFx(npc, data, t, ninja);

            // Scene over: let the real death through (loot, downed flag, vanilla death burst).
            if (t >= DeathEndTick && IsAuthority)
            {
                data.AllowDeath = true;
                npc.dontTakeDamage = false;
                npc.life = 0;
                npc.HitEffect(0, 10.0);
                npc.checkDead();
            }
        }

        private void DeathClientFx(NPC npc, KingSlimeInstanceData data, int t, bool ninja)
        {
            bool cam = Cinema.DeathCameraEnabled;
            Texture2D crown = GetCrownTexture();
            Vector2 rest = DeathCrownRest(npc, crown);

            // ---- the killing blow ----
            if ((data.DeathBeats & DeathBeat_Start) == 0)
            {
                data.DeathBeats |= DeathBeat_Start;

                SoundEngine.PlaySound(SoundID.NPCHit4 with { Pitch = -0.4f, Volume = 1.1f }, npc.Center);
                SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.7f, Volume = 0.8f }, npc.Center);

                if (cam)
                {
                    Cinema.Flash(Color.White, 1f);
                    Cinema.ZoomPunch(0.1f);
                    Cinema.Letterbox(true);
                    Cinema.Vignette(0.55f);
                    SlimeImpact(npc.Center, 0.9f, false);
                    RequestHitStop(data, DeathFreezeFrames);
                }
            }

            // ---- slow push-in while the form comes apart ----
            if (cam)
            {
                if (t < DeathShatterTick)
                    Cinema.HoldZoom(0.08f + 0.14f * Clamp01(t / (float)DeathShatterTick));
                else
                    Cinema.HoldZoom(0.06f);

                Cinema.Focus(npc.Center, 0.5f);
            }

            // ---- ninja melts: a splash each time the dissolve advances a frame ----
            if (ninja && t >= DeathDissolveStart && (t - DeathDissolveStart) % DeathDissolveFrameTicks == 0 && t < DeathShatterTick)
            {
                for (int i = 0; i < 8; i++)
                {
                    Dust d = Dust.NewDustPerfect(
                        npc.Bottom + new Vector2(Main.rand.NextFloat(-npc.width * 0.5f, npc.width * 0.5f), -6f),
                        DustID.t_Slime,
                        new Vector2(Main.rand.NextFloat(-3f, 3f), -Main.rand.NextFloat(1f, 4f)),
                        100,
                        Color.Cyan,
                        Main.rand.NextFloat(1.2f, 1.9f));
                    d.noGravity = false;
                }
            }

            // ---- crown first hits the ground ----
            int contactTick = DeathCrownStart + (int)(0.3636f * DeathCrownDuration);

            if ((data.DeathBeats & DeathBeat_Contact) == 0 && t >= contactTick)
            {
                data.DeathBeats |= DeathBeat_Contact;

                GoldBurst(rest, 10, 5f, 1.0f, 1.5f, gravity: true);
                SoundEngine.PlaySound(SoundID.Item37 with { Pitch = -0.3f, Volume = 0.9f }, rest);

                if (cam)
                    SlimeImpact(rest, 0.45f);
            }

            // ---- the crown cracks and shatters ----
            if ((data.DeathBeats & DeathBeat_Shatter) == 0 && t >= DeathShatterTick)
            {
                data.DeathBeats |= DeathBeat_Shatter;

                SoundEngine.PlaySound(SoundID.Shatter with { Pitch = -0.2f, Volume = 1.1f }, rest);
                SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.4f, Volume = 0.8f }, rest);

                GoldBurst(rest, 46, 15f, 1.5f, 2.5f, gravity: true);
                GoldBurst(rest, 18, 6f, 1.2f, 1.8f, gravity: false);

                if (Cinema.ParticlesEnabled)
                {
                    GoldRing(rest, 240f, 18);
                    GoldRing(rest, 420f, 30);

                    for (int i = 0; i < 24; i++)
                    {
                        Vector2 v = Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(4f, 12f);

                        BossParticleSystem.EmberBursts.Create(new ParticleInfo(
                            position: rest.ToNumerics(),
                            velocity: v.ToNumerics(),
                            rotation: 0f,
                            scale: new SystemVector2(11f, 11f),
                            color: new Color(255, 215, 120, 255),
                            duration: Main.rand.Next(20, 34)
                        ));
                    }
                }

                if (cam)
                {
                    Cinema.Flash(new Color(255, 240, 190), 1f);
                    Cinema.ZoomPunch(0.14f);
                    Cinema.Shake(rest, Main.rand.NextVector2Unit(), 16f, 20f, 26);
                    RequestHitStop(data, 6);
                }
            }
        }

        // ---- drawing -------------------------------------------------------

        private void DrawNinjaDeath(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            int t = (int)npc.ai[Slot_Timer];

            SpriteEffects spriteEffects = npc.spriteDirection == 1
                ? SpriteEffects.None
                : SpriteEffects.FlipHorizontally;

            Texture2D texture;
            Rectangle frame;
            Color color;

            if (t < DeathDissolveStart)
            {
                // Final-blow freeze: the white silhouette frame.
                texture = (ninjaStunAsset ??= ModContent.Request<Texture2D>(
                    "ShatteredIllusion/Content/NPCs/BossAI/VanillaBosses/KingSlime/NinjaStun")).Value;
                frame = NinjaStunFrames[0];
                color = Color.White;
            }
            else
            {
                // NinjaPopup read FORWARD is the ninja dissolving back into slime.
                texture = (ninjaPopupAsset ??= ModContent.Request<Texture2D>(
                    "ShatteredIllusion/Content/NPCs/BossAI/VanillaBosses/KingSlime/NinjaPopup")).Value;

                int idx = (int)MathHelper.Clamp((t - DeathDissolveStart) / (float)DeathDissolveFrameTicks, 0f, NinjaPopupFrameCount - 1);
                frame = NinjaPopupFrames[idx];

                float fade = 1f - Clamp01((t - DeathShatterTick) / 30f);
                color = npc.GetAlpha(drawColor) * fade;
            }

            // Tremor while he is coming apart.
            Vector2 jitter = t < DeathDissolveStart + 30
                ? Main.rand.NextVector2Circular(1.5f, 1.5f)
                : Vector2.Zero;

            spriteBatch.Draw(
                texture,
                npc.Bottom - screenPos + jitter,
                frame,
                color,
                0f,
                frame.Size() * new Vector2(0.5f, 1f),
                npc.scale,
                spriteEffects,
                0f);

            DrawDeathCrown(npc, spriteBatch, screenPos, npc.Top, spriteEffects);
        }

        // headWorld = where the crown sits on his head (world position).
        private void DrawDeathCrown(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Vector2 headWorld, SpriteEffects spriteEffects)
        {
            int t = (int)npc.ai[Slot_Timer];

            if (t >= DeathShatterTick)
                return;

            bool ninja = DeathIsNinja(npc);
            Texture2D crown = GetCrownTexture();
            Vector2 rest = DeathCrownRest(npc, crown);

            Vector2 pos;
            float rotation;

            if (t < DeathCrownStart)
            {
                // Ninja form has no crown yet; slime form still wears it.
                if (ninja)
                    return;

                pos = headWorld;
                rotation = 0f;
            }
            else
            {
                float d = Clamp01((t - DeathCrownStart) / (float)DeathCrownDuration);
                float e = EaseOutBounce(d);
                float dir = npc.spriteDirection >= 0 ? 1f : -1f;

                if (ninja)
                {
                    // Dropped from the sky, like the intro.
                    pos = new Vector2(rest.X, MathHelper.Lerp(rest.Y - DeathCrownFallHeight, rest.Y, e));
                }
                else
                {
                    // Pops off his head in an arc, then bounces to the ground.
                    pos = new Vector2(
                        MathHelper.Lerp(headWorld.X, rest.X, e),
                        MathHelper.Lerp(headWorld.Y, rest.Y, e));

                    pos.Y -= 80f * (float)Math.Sin(MathHelper.Pi * Clamp01(d * 1.6f));
                }

                rotation = (1f - d) * MathHelper.TwoPi * 1.5f * dir + 0.18f * e * dir;
            }

            // Trembles and heats up right before it breaks.
            float shake = Clamp01((t - (DeathShatterTick - 26)) / 26f);
            Vector2 jitter = shake > 0f
                ? Main.rand.NextVector2Circular(1f, 1f) * (0.6f + 3f * shake)
                : Vector2.Zero;

            Color color = Color.Lerp(Color.White, new Color(255, 236, 160), shake);

            spriteBatch.Draw(
                crown,
                pos + jitter - screenPos,
                null,
                color,
                rotation,
                crown.Size() / 2f,
                npc.scale,
                spriteEffects,
                0f);
        }
    }
}