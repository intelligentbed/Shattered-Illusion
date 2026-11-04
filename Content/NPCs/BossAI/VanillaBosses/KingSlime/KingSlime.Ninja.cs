using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ShatteredIllusion.Common.Players.ParrySystem;
using ShatteredIllusion.Common.Visuals;
using ShatteredIllusion.Content.Particles;
using ParticleLibrary.Core.V3.Particles;
using ParticleLibrary.Utilities;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using SystemVector2 = System.Numerics.Vector2;

namespace ShatteredIllusion.Content.NPCs.BossAI.VanillaBosses.KingSlime
{
    public partial class KingSlimeOverride
    {
        private static Asset<Texture2D>? ninjaWalkAsset;
        private static Asset<Texture2D>? ninjaStabAsset;
        private static Asset<Texture2D>? ninjaStunAsset;
        private static Asset<Texture2D>? ninjaSlashAsset;
        private static Asset<Texture2D>? ninjaPopupAsset;

        private const int NinjaWalkFrameCount = 4;
        private const int NinjaWalkFrameDuration = 6;

        private const int NinjaHitboxWidth = 34;
        private const int NinjaHitboxHeight = 44;

        private const float NinjaChaseAcceleration = 0.04f;
        private static float NinjaMaxSpeed => 6f * NinjaTempo;
        private const float NinjaVelocityLerp = 0.15f;

        //This is because a certain SOMEONE was too lazy and didnt want to make a re-emerge of the ninja 
        private static readonly Rectangle[] NinjaPopupFrames =
        {
            new Rectangle(0, 0,   34, 44),
            new Rectangle(0, 46,  34, 44),
            new Rectangle(0, 92,  34, 44),
            new Rectangle(0, 144, 34, 34),
            new Rectangle(0, 202, 34, 26),
            new Rectangle(0, 258, 34, 16),
        };

        private const int NinjaPopupFrameCount = 6;
        private const float NinjaPopupFrameDuration = TransformRiseDuration / NinjaPopupFrameCount;

        private const int NinjaStabWindupStep = 0;
        private const int NinjaStabStrikeStep = 1;
        private const int NinjaStabRecoveryStep = 2;

        private const int NinjaStabFrameCount = 6;

        private static float NinjaStabTelegraphDuration => 50f / NinjaTempo;
        private const float NinjaStabDashDuration = 10f;
        private static float NinjaStabRecoveryDuration => 24f / NinjaTempo;

        private const float NinjaStabDashDistance = 420f;
        private const float NinjaStabRange = 460f;
        private static float NinjaWalkAttackCooldown => 70f / NinjaTempo;
        private const float NinjaStabSwingAnticipation = 8f;

        private const float NinjaStabTrackAcceleration = 0.09f;
        private const float NinjaStabTrackMaxSpeed = 4.5f;
        private const float NinjaStabTrackLerp = 0.12f;
        private const float NinjaStabTrackDeadzone = 10f;
        private const float NinjaStabTrackLockLerp = 0.4f;

        private const int NinjaSlashWindupStep = 0;
        private const int NinjaSlashSwingStep = 1;
        private const int NinjaSlashRecoveryStep = 2;

        private const int NinjaVanishFadeStep = 0;
        private const int NinjaVanishHiddenStep = 1;
        private const int NinjaVanishAppearStep = 2;

        private const float NinjaOutOfRangePatience = 90f;
        private const float NinjaCounterRange = 400f;
        private const float NinjaCounterRingRadius = 60f;
        private const float NinjaCounterRecoveryDuration = 30f;
        private const int NinjaCounterStanceStep = 0;
        private const int NinjaCounterRecoveryStep = 1;

        private static float NinjaCounterDuration => 60f / NinjaTempo;
        private static int NinjaChainDashes => Main.masterMode ? 4 : 3;
        private static int NinjaFlickerCount => Main.masterMode ? 4 : 3;

        private static float NinjaTempo => Main.masterMode ? 1.45f : Main.expertMode ? 1.2f : 1f;
        private static int NinjaSlashHits => Main.masterMode ? 3 : 2;

        private const int Local_SlashHit = Local_SplitVertical;

        private const float NinjaSlashRange = 240f;
        private static float NinjaSlashTelegraphDuration => 40f / NinjaTempo;
        private static float NinjaSlashFollowUpDuration => 16f / NinjaTempo;
        private static float NinjaSlashAmbushDuration => 18f / NinjaTempo;
        private const float NinjaSlashLockTime = 6f;
        private const float NinjaSlashSwingDuration = 12f;
        private static float NinjaSlashRecoveryDuration => 26f / NinjaTempo;
        private const float NinjaSlashLungeSpeed = 7f;
        private const float NinjaSlashArcRadius = 95f;
        private const float NinjaSlashReach = 75f;
        private const float NinjaSlashHitboxHeight = 100f;
        private const float NinjaSlashOriginX = 30f;

        private static readonly int[] NinjaSlashFrameBottoms = { 72, 146, 220, 294, 368, 442 };
        private const int NinjaSlashSheetWidth = 100;
        private const int NinjaSlashFrameHeight = 72;

        private const float NinjaVanishFadeDuration = 16f;
        private static float NinjaVanishHiddenDuration => 48f / NinjaTempo;
        private static float NinjaVanishLockTime => 14f / NinjaTempo;
        // Re-emerge is long enough to actually read: burst out of the goo, hop up, land. Flicker combos keep it snappy.
        private const float NinjaVanishAppearDurationFull = 24f;
        private const float NinjaVanishAppearDurationQuick = 12f;
        private static float NinjaVanishAppearDuration(bool quick) => quick ? NinjaVanishAppearDurationQuick : NinjaVanishAppearDurationFull;
        private const float NinjaVanishBehindDistance = 110f;

        private const int NinjaStunnedFrameCount = 5;
        private const float NinjaStunnedMaterializeDuration = 40f;
        private const float NinjaStunnedDuration = 80f;
        private static readonly Rectangle[] NinjaStunFrames =
        {
            new Rectangle(0, 0,   38, 45),
            new Rectangle(0, 46,  38, 45),
            new Rectangle(0, 94,  38, 43),
            new Rectangle(0, 138, 38, 45),
            new Rectangle(0, 184, 38, 44),
        };

        public bool IsParryable => FindParryableNinja(null) != null;

        public void OnParried(Player player)
        {
            NPC npc = FindParryableNinja(player);
            if (npc == null)
                return;

            if (IsWeaponFormPhase(CurrentPhase(npc)))
                EnterWeaponStun(npc);
            else
                EnterStunned(npc);
        }

        private static NPC FindParryableNinja(Player player)
        {
            NPC closest = null;
            float closestDistSq = float.MaxValue;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];

                if (!npc.active || npc.type != NPCID.KingSlime || !IsInStabParryWindow(npc))
                    continue;

                if (player == null)
                    return npc; // just checking existence - first match is enough

                float distSq = Vector2.DistanceSquared(npc.Center, player.Center);
                if (distSq < closestDistSq)
                {
                    closestDistSq = distSq;
                    closest = npc;
                }
            }

            return closest;
        }

        public static bool IsInStabParryWindow(NPC npc) =>
            (CurrentPhase(npc) == AttackPhase.NinjaStab && CurrentStep(npc) == NinjaStabStrikeStep)
            || (CurrentPhase(npc) == AttackPhase.NinjaSlash && CurrentStep(npc) == NinjaSlashSwingStep)
            || (CurrentPhase(npc) == AttackPhase.NinjaLeap && CurrentStep(npc) == NinjaLeapPlungeStep)
            || ((CurrentPhase(npc) == AttackPhase.WeaponSword || CurrentPhase(npc) == AttackPhase.WeaponSpear
                || CurrentPhase(npc) == AttackPhase.WeaponHammer) && CurrentStep(npc) == WeaponStrikeStep);

        private void DoNinjaWalk(NPC npc, Player target)
        {
            if (npc.GetGlobalNPC<KingSlimeInstanceData>().NinjaClock >= NinjaPhaseDuration)
            {
                EnterRevert(npc);
                return;
            }

            npc.noGravity = false;
            npc.noTileCollide = false;

            // Safety net: if anything left him embedded in blocks, pop him out before he can wedge.
            NinjaUnstick(npc);

            float dirX = target.Center.X - npc.Center.X;
            npc.velocity.X = MathHelper.Lerp(
                npc.velocity.X,
                MathHelper.Clamp(dirX * NinjaChaseAcceleration, -NinjaMaxSpeed, NinjaMaxSpeed),
                NinjaVelocityLerp
            );

            if (npc.velocity.X != 0f)
            {
                npc.direction = npc.velocity.X > 0 ? 1 : -1;
                npc.spriteDirection = npc.direction;
            }

            // Walk cycle advances with ground speed (feet match movement; he idles when he stops).
            KingSlimeInstanceData walkData = npc.GetGlobalNPC<KingSlimeInstanceData>();
            float prevClock = walkData.WalkClock;
            walkData.WalkClock += Math.Abs(npc.velocity.X) * 0.028f;

            if (!Main.dedServ && npc.velocity.Y == 0f && Math.Abs(npc.velocity.X) > 1.2f
                && (int)prevClock != (int)walkData.WalkClock && (int)walkData.WalkClock % 2 == 0)
            {
                Dust.NewDust(new Vector2(npc.Center.X - 8f, npc.Bottom.Y - 4f), 16, 4, DustID.Smoke,
                    -npc.direction * 0.6f, -0.3f, 150, Color.White, 0.6f);
            }

            // Obstacles: hop over blocks in his way, and onto ledges the player is standing on.
            bool grounded = npc.velocity.Y == 0f;
            int moveDir = Math.Sign(dirX);

            if (walkData.NinjaJumpCooldown > 0)
                walkData.NinjaJumpCooldown--;

            if (grounded && moveDir != 0 && Math.Abs(dirX) > 24f && walkData.NinjaJumpCooldown <= 0)
            {
                Vector2 probe = new Vector2(npc.position.X + moveDir * 10f, npc.position.Y);
                float rise = 0f;

                if (Collision.SolidCollision(probe, npc.width, npc.height))
                {
                    for (float h = 16f; h <= 112f; h += 16f)
                    {
                        if (!Collision.SolidCollision(probe - new Vector2(0f, h), npc.width, npc.height))
                        {
                            rise = h;
                            break;
                        }
                    }
                }
                else if (target.Center.Y < npc.Center.Y - 90f && Math.Abs(dirX) < 220f)
                {
                    rise = 100f;
                }

                if (rise > 0f)
                {
                    npc.velocity.Y = -Math.Min((float)Math.Sqrt(2f * 0.3f * (rise + 20f)) + 0.6f, 10.5f);
                    walkData.NinjaJumpCooldown = 30;

                    if (!Main.dedServ)
                        SmokePuff(npc, 4);

                    if (IsAuthority)
                        npc.netUpdate = true;
                }
            }

            // Watchdog: wedged against something he can't hop (tall wall) -> shadow-step behind the player instead.
            if (grounded && Math.Abs(dirX) > 90f && Math.Abs(npc.position.X - npc.oldPosition.X) < 0.4f)
                walkData.NinjaStuck++;
            else
                walkData.NinjaStuck = Math.Max(0, walkData.NinjaStuck - 2);

            if (walkData.NinjaStuck > 50)
            {
                walkData.NinjaStuck = 0;
                EnterNinjaVanish(npc);
                return;
            }

            float timer = npc.ai[Slot_Timer];
            float distanceToTarget = Vector2.Distance(npc.Center, target.Center);


            if (timer >= NinjaWalkAttackCooldown)
                ChooseNinjaAttack(npc, distanceToTarget);
        }

        private void ChooseNinjaAttack(NPC npc, float distance)
        {
            KingSlimeInstanceData data = npc.GetGlobalNPC<KingSlimeInstanceData>();
            int index = SequenceIndex(npc) % NinjaAttackSequence.Length;
            NinjaAttack next = NinjaAttackSequence[index];

            float range = next switch
            {
                NinjaAttack.Stab or NinjaAttack.ChainDash => NinjaStabRange,
                NinjaAttack.Slash => NinjaSlashRange,
                NinjaAttack.Counter => NinjaCounterRange,
                NinjaAttack.Leap => 650f,
                NinjaAttack.Geyser => 520f,
                _ => float.MaxValue,
            };

            data.NinjaFlicker = false;

            if (distance > range)
            {
                // Kited for too long: ambush instead, without spending the queued attack.
                if (npc.ai[Slot_Timer] >= NinjaWalkAttackCooldown + NinjaOutOfRangePatience)
                    EnterNinjaVanish(npc);

                return;
            }

            npc.ai[Slot_SequenceIndex] = (index + 1) % NinjaAttackSequence.Length;

            switch (next)
            {
                case NinjaAttack.Stab:
                    EnterNinjaStab(npc);
                    break;
                case NinjaAttack.ChainDash:
                    EnterNinjaStab(npc, NinjaChainDashes);
                    break;
                case NinjaAttack.Slash:
                    EnterNinjaSlash(npc, 0f);
                    break;
                case NinjaAttack.Counter:
                    EnterNinjaCounter(npc);
                    break;
                case NinjaAttack.Leap:
                    EnterNinjaLeap(npc);
                    break;
                case NinjaAttack.Geyser:
                    EnterNinjaGeyser(npc);
                    break;
                case NinjaAttack.Flicker:
                    data.NinjaFlicker = true;
                    data.NinjaComboDone = 0;
                    data.NinjaComboTotal = NinjaFlickerCount;
                    EnterNinjaVanish(npc);
                    break;
                default:
                    EnterNinjaVanish(npc);
                    break;
            }
        }

        private void EnterNinjaSlash(NPC npc, float startTimer, bool single = false)
        {
            npc.GetGlobalNPC<KingSlimeInstanceData>().NinjaSingleSlash = single;
            SetPhase(npc, AttackPhase.NinjaSlash);
            SetStep(npc, NinjaSlashWindupStep);
            npc.ai[Slot_Timer] = startTimer;
            npc.localAI[Local_SlashHit] = 0f;
            npc.velocity.X = 0f;
            npc.damage = 0;

            if (IsAuthority)
                npc.netUpdate = true;
        }

        private void DoNinjaSlash(NPC npc, Player target)
        {
            ref float timer = ref npc.ai[Slot_Timer];
            int step = CurrentStep(npc);
            bool followUp = npc.localAI[Local_SlashHit] > 0f;
            KingSlimeInstanceData data = npc.GetGlobalNPC<KingSlimeInstanceData>();

            if (step == NinjaSlashWindupStep)
            {
                npc.noGravity = false;
                npc.noTileCollide = false;
                npc.damage = 0;

                float duration = followUp ? NinjaSlashFollowUpDuration : NinjaSlashTelegraphDuration;

                if (timer >= duration - NinjaSlashLockTime)
                {
                    npc.velocity.X = 0f;
                }
                else
                {
                    npc.velocity.X *= 0.7f;
                    npc.direction = target.Center.X > npc.Center.X ? 1 : -1;
                    npc.spriteDirection = npc.direction;
                }

                if (!Main.dedServ)
                    DrawSlashTelegraph(npc, MathHelper.Clamp(timer / duration, 0f, 1f));

                if (timer >= duration)
                {
                    npc.velocity.X = NinjaSlashLungeSpeed * npc.direction;
                    KingSlimeSfx.Swing(npc.Center, 0.1f, 0.8f);

                    SetStep(npc, NinjaSlashSwingStep);
                    timer = 0f;

                    if (IsAuthority)
                        npc.netUpdate = true;
                }
            }
            else if (step == NinjaSlashSwingStep)
            {
                npc.noGravity = true;
                npc.noTileCollide = true;
                npc.damage = npc.defDamage;
                npc.velocity.X *= 0.85f;
                npc.velocity.Y = 0f;
                SetSlashHitbox(npc, true);

                if (timer <= 1f && !Main.dedServ)
                {
                    BossParticleSystem.EmberBursts.Create(new ParticleInfo(
                        position: (npc.Center + new Vector2(npc.direction * 40f, 0f)).ToNumerics(),
                        velocity: new Vector2(npc.direction * 2f, 0f).ToNumerics(),
                        rotation: 0f,
                        scale: new SystemVector2(18f, 18f),
                        color: new Color(255, 200, 120, 255),
                        duration: 14
                    ));
                }

                if (timer >= NinjaSlashSwingDuration)
                {
                    SetSlashHitbox(npc, false);
                    NinjaUnstick(npc);
                    npc.noGravity = false;
                    npc.noTileCollide = false;
                    npc.damage = 0;
                    timer = 0f;

                    npc.localAI[Local_SlashHit]++;

                    if (npc.localAI[Local_SlashHit] < (data.NinjaSingleSlash ? 1 : NinjaSlashHits))
                    {
                        SetStep(npc, NinjaSlashWindupStep);
                    }
                    else if (data.NinjaFlicker && ++data.NinjaComboDone < data.NinjaComboTotal)
                    {
                        EnterNinjaVanish(npc);
                    }
                    else
                    {
                        data.NinjaFlicker = false;
                        npc.localAI[Local_SlashHit] = 0f;
                        SetStep(npc, NinjaSlashRecoveryStep);
                    }

                    if (IsAuthority)
                        npc.netUpdate = true;
                }
            }
            else
            {
                npc.noGravity = false;
                npc.noTileCollide = false;
                npc.damage = 0;
                npc.velocity.X *= 0.85f;

                if (timer >= NinjaSlashRecoveryDuration)
                    ReturnToNinjaWalk(npc);
            }
        }

        private void EnterNinjaCounter(NPC npc)
        {
            SetPhase(npc, AttackPhase.NinjaCounter);
            SetStep(npc, NinjaCounterStanceStep);
            npc.ai[Slot_Timer] = 0f;
            npc.velocity.X = 0f;
            npc.damage = 0;
            npc.GetGlobalNPC<KingSlimeInstanceData>().NinjaCounterLife = npc.life;

            SoundEngine.PlaySound(SoundID.Item29 with { Pitch = -0.2f }, npc.Center);

            if (IsAuthority)
                npc.netUpdate = true;
        }

        private void DoNinjaCounter(NPC npc, Player target)
        {
            ref float timer = ref npc.ai[Slot_Timer];
            KingSlimeInstanceData data = npc.GetGlobalNPC<KingSlimeInstanceData>();

            npc.noGravity = false;
            npc.noTileCollide = false;
            npc.damage = 0;
            npc.velocity.X *= 0.7f;

            if (CurrentStep(npc) == NinjaCounterStanceStep)
            {
                npc.direction = target.Center.X > npc.Center.X ? 1 : -1;
                npc.spriteDirection = npc.direction;

                if (npc.life < data.NinjaCounterLife)
                {
                    KingSlimeSfx.Swing(npc.Center, 0.3f, 0.9f);
                    EnterNinjaSlash(npc, NinjaSlashTelegraphDuration - NinjaSlashLockTime, true);
                    return;
                }

                if (!Main.dedServ)
                {
                    const int dashes = 10;
                    float spin = timer * 0.12f;
                    float pulse = 0.65f + 0.35f * (float)Math.Sin(timer * 0.35f);

                    for (int i = 0; i < dashes; i++)
                    {
                        float angle = spin + MathHelper.TwoPi * i / dashes;
                        Vector2 offset = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * NinjaCounterRingRadius;

                        BossParticleSystem.TelegraphSegments.Create(new ParticleInfo(
                            position: (npc.Center + offset).ToNumerics(),
                            velocity: SystemVector2.Zero,
                            rotation: angle + MathHelper.PiOver2,
                            scale: new SystemVector2(18f, 8f),
                            color: new Color(255, 90, 90, 255) * pulse,
                            duration: 2
                        ));
                    }
                }

                if (timer >= NinjaCounterDuration)
                {
                    SetStep(npc, NinjaCounterRecoveryStep);
                    timer = 0f;
                }
            }
            else if (timer >= NinjaCounterRecoveryDuration)
            {
                ReturnToNinjaWalk(npc);
            }
        }

        private void ReturnToNinjaWalk(NPC npc)
        {
            npc.GetGlobalNPC<KingSlimeInstanceData>().NinjaStuck = 0;
            SetPhase(npc, AttackPhase.NinjaWalk);
            npc.ai[Slot_Timer] = 0f;
            SetStep(npc, 0);

            if (IsAuthority)
                npc.netUpdate = true;
        }

        private static void DrawSlashTelegraph(NPC npc, float windupT)
        {
            const int segments = 9;
            int shown = (int)MathHelper.Lerp(3f, segments, windupT);
            float center = npc.Center.Y - 6f;

            for (int i = 0; i < shown; i++)
            {
                float angle = MathHelper.Lerp(-1.05f, 1.05f, i / (segments - 1f));
                Vector2 offset = new Vector2(npc.direction * (float)Math.Cos(angle), (float)Math.Sin(angle)) * NinjaSlashArcRadius;
                float tangent = (float)Math.Atan2(offset.Y, offset.X) + MathHelper.PiOver2;

                BossParticleSystem.TelegraphSegments.Create(new ParticleInfo(
                    position: new Vector2(npc.Center.X + offset.X, center + offset.Y).ToNumerics(),
                    velocity: SystemVector2.Zero,
                    rotation: tangent,
                    scale: new SystemVector2(22f, 9f),
                    color: new Color(255, 200, 120, 255) * (0.35f + 0.65f * windupT),
                    duration: 2
                ));
            }
        }

        // The widened box reaches forward only; the body stays put so the draw code can offset for it.
        private static void SetSlashHitbox(NPC npc, bool active)
        {
            int bodyW = (int)(NinjaHitboxWidth * npc.scale);
            int bodyH = (int)(NinjaHitboxHeight * npc.scale);
            bool isActive = npc.width > bodyW;

            if (isActive == active)
                return;

            int w = active ? bodyW + (int)(NinjaSlashReach * npc.scale) : bodyW;
            int h = active ? (int)(NinjaSlashHitboxHeight * npc.scale) : bodyH;
            float bodyCenterX = isActive ? npc.Center.X - npc.direction * (npc.width - bodyW) / 2f : npc.Center.X;
            float newCenterX = active ? bodyCenterX + npc.direction * (w - bodyW) / 2f : bodyCenterX;
            float bottom = npc.Bottom.Y;

            npc.width = w;
            npc.height = h;
            npc.position = new Vector2(newCenterX - w / 2f, bottom - h);
        }

        private void EnterNinjaVanish(NPC npc)
        {
            SetPhase(npc, AttackPhase.NinjaVanish);
            SetStep(npc, NinjaVanishFadeStep);
            npc.ai[Slot_Timer] = 0f;
            npc.velocity.X = 0f;
            npc.damage = 0;

            {
                KingSlimeInstanceData vanishData = npc.GetGlobalNPC<KingSlimeInstanceData>();
                KingSlimeSfx.Submerge(npc.Center, vanishData.NinjaFlicker && vanishData.NinjaComboDone > 0);
            }

            if (IsAuthority)
                npc.netUpdate = true;
        }

        private void DoNinjaVanish(NPC npc, Player target)
        {
            ref float timer = ref npc.ai[Slot_Timer];
            int step = CurrentStep(npc);

            KingSlimeInstanceData data = npc.GetGlobalNPC<KingSlimeInstanceData>();
            bool quick = data.NinjaFlicker && data.NinjaComboDone > 0;
            float fade = quick ? 6f : NinjaVanishFadeDuration;
            float hidden = quick ? 26f / NinjaTempo : NinjaVanishHiddenDuration;
            float lockTime = quick ? 10f / NinjaTempo : NinjaVanishLockTime;
            int side = quick && data.NinjaComboDone % 2 == 1 ? -1 : 1;

            float appearDuration = NinjaVanishAppearDuration(quick);

            npc.damage = 0;
            npc.velocity.X *= 0.8f;

            if (step == NinjaVanishFadeStep)
            {
                npc.noGravity = false;
                npc.noTileCollide = false;
                npc.alpha = (int)MathHelper.Lerp(SlimeAlpha, 255, timer / fade);

                if (!Main.dedServ)
                {
                    SmokePuff(npc, 1);

                    if (Main.rand.NextBool(2))
                    {
                        Dust.NewDust(new Vector2(npc.position.X, npc.Bottom.Y - 6f), npc.width, 6, DustID.t_Slime,
                            Main.rand.NextFloat(-1.5f, 1.5f), -Main.rand.NextFloat(0.5f, 2.5f), 100, Color.Cyan, 1.2f);
                    }
                }

                if (timer >= fade)
                {
                    SetStep(npc, NinjaVanishHiddenStep);
                    timer = 0f;
                    SetSlamTargetPos(npc, FindAmbushSpot(npc, target, side));
                }
            }
            else if (step == NinjaVanishHiddenStep)
            {
                npc.noGravity = true;
                npc.noTileCollide = true;
                npc.dontTakeDamage = true;
                npc.velocity = Vector2.Zero;
                npc.alpha = 255;

                if (timer < hidden - lockTime)
                    SetSlamTargetPos(npc, FindAmbushSpot(npc, target, side));

                Vector2 spot = SlamTargetPos(npc);
                float t = MathHelper.Clamp(timer / hidden, 0f, 1f);
                bool locked = timer >= hidden - lockTime;

                // The spot just locked: bubble a warning there so you can hear where he's coming up.
                if (locked && timer - 1f < hidden - lockTime)
                    KingSlimeSfx.AmbushWarning(spot);

                if (!Main.dedServ)
                {
                    float size = 70f;
                    Color c = locked ? Color.White : new Color(255, 200, 120);

                    BossParticleSystem.GroundMarkers.Create(new ParticleInfo(
                        position: (spot - new Vector2(0f, 3f)).ToNumerics(),
                        velocity: SystemVector2.Zero,
                        rotation: 0f,
                        scale: new SystemVector2(size, size * 0.3f),
                        color: c * (0.3f + 0.5f * t),
                        duration: 2
                    ));

                    float inner = MathHelper.Lerp(size * 0.2f, size, t);

                    BossParticleSystem.GroundMarkers.Create(new ParticleInfo(
                        position: (spot - new Vector2(0f, 3f)).ToNumerics(),
                        velocity: SystemVector2.Zero,
                        rotation: 0f,
                        scale: new SystemVector2(inner, inner * 0.3f),
                        color: c * (0.4f + 0.5f * t),
                        duration: 2
                    ));
                }

                if (timer >= hidden)
                {
                    npc.Bottom = spot;
                    npc.direction = target.Center.X > npc.Center.X ? 1 : -1;
                    npc.spriteDirection = npc.direction;

                    KingSlimeSfx.Emerge(npc.Center, false);

                    if (!Main.dedServ)
                        SmokePuff(npc, 14);

                    SetStep(npc, NinjaVanishAppearStep);
                    timer = 0f;

                    if (IsAuthority)
                        npc.netUpdate = true;
                }
            }
            else
            {
                npc.noGravity = true;
                npc.noTileCollide = false;
                npc.velocity = Vector2.Zero;
                npc.alpha = (int)MathHelper.Lerp(255, SlimeAlpha, MathHelper.Clamp(timer / (appearDuration * 0.5f), 0f, 1f));

                if (!Main.dedServ)
                {
                    float u = MathHelper.Clamp(timer / appearDuration, 0f, 1f);
                    EmergePuddle(npc.Bottom, u, 110f);

                    if (timer <= 1f)
                        GelFountain(npc, 12);
                }

                if (timer >= appearDuration)
                {
                    npc.alpha = SlimeAlpha;
                    SetSlamTargetPos(npc, Vector2.Zero);
                    EnterNinjaSlash(npc, NinjaSlashTelegraphDuration - (quick ? 15f / NinjaTempo : NinjaSlashAmbushDuration), data.NinjaFlicker);
                }
            }
        }

        private static void SmokePuff(NPC npc, int count)
        {
            for (int i = 0; i < count; i++)
            {
                Dust.NewDust(npc.position, npc.width, npc.height, DustID.Smoke,
                    Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-2f, 0.5f), 150, Color.White, 1.2f);
            }
        }

        // Gel puddle that splashes out of the floor under an emerging ninja. u: 0 -> 1 over the emerge.
        private static void EmergePuddle(Vector2 bottom, float u, float size)
        {
            if (Main.dedServ)
                return;

            float eased = 1f - (1f - u) * (1f - u);
            float s = MathHelper.Lerp(size * 0.35f, size, eased);

            BossParticleSystem.GroundMarkers.Create(new ParticleInfo(
                position: (bottom - new Vector2(0f, 3f)).ToNumerics(),
                velocity: SystemVector2.Zero,
                rotation: 0f,
                scale: new SystemVector2(s, s * 0.3f),
                color: new Color(120, 235, 255) * (0.1f + 0.8f * (1f - u)),
                duration: 2
            ));
        }

        // Gel spraying up out of the floor as the ninja bursts out.
        private static void GelFountain(NPC npc, int count)
        {
            if (Main.dedServ)
                return;

            for (int i = 0; i < count; i++)
            {
                Dust d = Dust.NewDustPerfect(
                    npc.Bottom + new Vector2(Main.rand.NextFloat(-14f, 14f), -2f),
                    DustID.t_Slime,
                    new Vector2(Main.rand.NextFloat(-2.5f, 2.5f), -Main.rand.NextFloat(3f, 8f)),
                    100,
                    Color.Cyan,
                    Main.rand.NextFloat(1.3f, 1.9f));
                d.noGravity = false;
            }
        }

        // Pose for the pop-out: the goo sheet played back-to-front, then a hop with stretch on the way up and a squash on landing.
        // t: 0 -> 1. hop: pixels of visual lift (the hitbox stays put, damage is off during the emerge).
        private static void EmergePose(float t, float hop, out int sheetStage, out float lift, out Vector2 stretch)
        {
            t = MathHelper.Clamp(t, 0f, 1f);

            // First ~55%: the goo climbs from a puddle to the full ninja. The rest is the hop.
            float riseT = MathHelper.Clamp(t / 0.55f, 0f, 1f);
            sheetStage = (int)MathHelper.Clamp(riseT * NinjaPopupFrameCount, 0f, NinjaPopupFrameCount - 1);

            float h = MathHelper.Clamp((t - 0.3f) / 0.7f, 0f, 1f);
            float arc = (float)Math.Sin(Math.PI * h);
            lift = arc * hop;

            float land = h > 0.82f ? (float)Math.Sin((h - 0.82f) / 0.18f * Math.PI) : 0f;
            stretch = new Vector2(1f - 0.12f * arc + 0.16f * land, 1f + 0.2f * arc - 0.16f * land);
        }

        // Prefers standing behind the player, then in front; stays put if both spots are inside tiles.
        private static Vector2 FindAmbushSpot(NPC npc, Player target, int firstSide)
        {
            int bodyW = (int)(NinjaHitboxWidth * npc.scale);
            int bodyH = (int)(NinjaHitboxHeight * npc.scale);

            for (int n = 0; n < 2; n++)
            {
                int side = n == 0 ? firstSide : -firstSide;
                float x = target.Center.X + side * -target.direction * NinjaVanishBehindDistance;
                int tx = (int)(x / 16f);
                int ty = (int)(target.Bottom.Y / 16f);

                for (int i = 0; i < 60 && ty < Main.maxTilesY - 2; i++, ty++)
                {
                    Tile tile = Main.tile[tx, ty];

                    if (tile.HasUnactuatedTile && (Main.tileSolid[tile.TileType] || Main.tileSolidTop[tile.TileType]))
                        break;
                }

                Vector2 spot = new Vector2(x, ty * 16f);

                if (!Collision.SolidCollision(spot - new Vector2(bodyW / 2f, bodyH), bodyW, bodyH))
                    return spot;
            }

            return npc.Bottom;
        }

        private void EnterNinjaStab(NPC npc, int dashes = 1)
        {
            KingSlimeInstanceData data = npc.GetGlobalNPC<KingSlimeInstanceData>();
            data.NinjaComboDone = 0;
            data.NinjaComboTotal = dashes;
            SetPhase(npc, AttackPhase.NinjaStab);
            SetStep(npc, NinjaStabWindupStep);
            npc.ai[Slot_Timer] = 0f;
            npc.velocity.X = 0f;
            npc.damage = 0;

            if (IsAuthority)
                npc.netUpdate = true;
        }

        private void DoNinjaStab(NPC npc, Player target)
        {
            ref float timer = ref npc.ai[Slot_Timer];
            int step = CurrentStep(npc);
            KingSlimeInstanceData data = npc.GetGlobalNPC<KingSlimeInstanceData>();
            float telegraph = StabTelegraph(data);
            float recovery = StabRecovery(data);

            if (step == NinjaStabWindupStep)
            {
                // He hovers for the whole startup so he can line the dash up vertically.
                npc.noGravity = true;
                npc.noTileCollide = false;
                npc.damage = 0;
                npc.velocity.X *= 0.7f;

                bool lockedIn = timer >= telegraph - NinjaStabSwingAnticipation;

                if (lockedIn)
                {
                    // Anticipation window: stop tracking, freeze the height, wind the swing.
                    npc.velocity.Y = MathHelper.Lerp(npc.velocity.Y, 0f, NinjaStabTrackLockLerp);

                    if (Math.Abs(npc.velocity.Y) < 0.05f)
                        npc.velocity.Y = 0f;
                }
                else
                {
                    float yDiff = target.Center.Y - npc.Center.Y;
                    float desiredYVel = Math.Abs(yDiff) <= NinjaStabTrackDeadzone
                        ? 0f
                        : MathHelper.Clamp(yDiff * NinjaStabTrackAcceleration, -NinjaStabTrackMaxSpeed, NinjaStabTrackMaxSpeed);

                    npc.velocity.Y = MathHelper.Lerp(npc.velocity.Y, desiredYVel, NinjaStabTrackLerp);
                }

                // Lock in facing (HOLY MEGA MOG LOL, gosh i hate myself)
                npc.direction = target.Center.X > npc.Center.X ? 1 : -1;
                npc.spriteDirection = npc.direction;

                if (timer <= 1f && data.NinjaComboDone == 0)
                {
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.2f, Volume = 0.9f }, npc.Center);
                }

                if (Main.rand.NextBool(2))
                {
                    Dust.NewDust(npc.position, npc.width, npc.height, DustID.Smoke,
                        Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-1f, 0f), 150, Color.White, 0.9f);
                }

                // The stab telegraph 
                if (!Main.dedServ && timer % 3f == 0f)
                {
                    float windupT = MathHelper.Clamp(timer / telegraph, 0f, 1f);
                    int segments = (int)MathHelper.Lerp(3f, 10f, windupT);
                    Vector2 dashDir = new Vector2(npc.direction, 0f);

                    for (int i = 0; i < segments; i++)
                    {
                        float t = (i + Main.rand.NextFloat()) / segments;
                        Vector2 segmentPos = npc.Center + dashDir * (NinjaStabDashDistance * t);

                        BossParticleSystem.TelegraphSegments.Create(new ParticleInfo(
                            position: segmentPos.ToNumerics(),
                            velocity: SystemVector2.Zero,
                            rotation: 0f,
                            scale: new SystemVector2(20f, 9f),
                            color: new Color(120, 255, 220, 255),
                            duration: 14
                        ));
                    }
                }

                if (timer >= telegraph)
                {
                    npc.velocity.Y = 0f;

                    if (IsAuthority)
                    {
                        npc.velocity.X = (NinjaStabDashDistance / NinjaStabDashDuration) * npc.direction;
                        npc.netUpdate = true;
                    }

                    KingSlimeSfx.Swing(npc.Center, 0.4f, 0.7f);

                    SetStep(npc, NinjaStabStrikeStep);
                    timer = 0f;
                }
            }
            else if (step == NinjaStabStrikeStep)
            {
                // The dash itself, and the parryable window. He now stops at walls instead of tunnelling into them
                // (that was the main way he ended up wedged inside blocks).
                npc.noGravity = true;
                npc.noTileCollide = false;
                npc.damage = npc.defDamage;
                npc.velocity.Y = 0f;

                if (timer <= 1f && !Main.dedServ)
                {
                    Vector2 trailVelocity = new Vector2(npc.direction * 2f, 0f);

                    BossParticleSystem.EmberBursts.Create(new ParticleInfo(
                        position: npc.Center.ToNumerics(),
                        velocity: trailVelocity.ToNumerics(),
                        rotation: 0f,
                        scale: new SystemVector2(18f, 18f),
                        color: new Color(120, 255, 220, 255),
                        duration: 14
                    ));
                }

                // Afterimage smear along the dash line every tick
                if (!Main.dedServ)
                {
                    Dust.NewDust(npc.position, npc.width, npc.height, DustID.Smoke,
                        -npc.velocity.X * 0.15f, 0f, 100, new Color(120, 255, 220), 1.1f);
                }

                if (timer >= NinjaStabDashDuration)
                {
                    npc.noGravity = false;
                    npc.noTileCollide = false;
                    npc.velocity.X *= 0.2f;
                    NinjaUnstick(npc);

                    data.NinjaComboDone++;
                    SetStep(npc, NinjaStabRecoveryStep);
                    timer = 0f;

                    if (IsAuthority)
                        npc.netUpdate = true;
                }
            }
            else if (step == NinjaStabRecoveryStep)
            {
                npc.noGravity = false;
                npc.noTileCollide = false;
                npc.damage = 0;
                npc.velocity.X *= 0.85f;

                if (timer >= recovery)
                {
                    if (data.NinjaComboDone < data.NinjaComboTotal)
                    {
                        SetStep(npc, NinjaStabWindupStep);
                        timer = 0f;
                    }
                    else
                    {
                        ReturnToNinjaWalk(npc);
                    }

                    if (IsAuthority)
                        npc.netUpdate = true;
                }
            }
        }

        private static float StabTelegraph(KingSlimeInstanceData data) =>
            data.NinjaComboDone > 0 ? 22f / NinjaTempo : NinjaStabTelegraphDuration;

        private static float StabRecovery(KingSlimeInstanceData data) =>
            data.NinjaComboDone < data.NinjaComboTotal ? 8f / NinjaTempo : NinjaStabRecoveryDuration;

        // ---------------------------------------------------------------------------------------------
        // Safety helpers (keep him from ever ending up embedded in blocks)
        // ---------------------------------------------------------------------------------------------

        private static void NinjaUnstick(NPC npc)
        {
            if (!Collision.SolidCollision(npc.position, npc.width, npc.height))
                return;

            Vector2 origin = npc.position;

            for (int i = 1; i <= 40; i++)
            {
                Vector2 p = origin - new Vector2(0f, i * 4f);

                if (!Collision.SolidCollision(p, npc.width, npc.height))
                {
                    npc.position = p;
                    return;
                }
            }

            for (int i = 1; i <= 24; i++)
            {
                foreach (int sign in new[] { 1, -1 })
                {
                    Vector2 p = origin + new Vector2(sign * i * 4f, 0f);

                    if (!Collision.SolidCollision(p, npc.width, npc.height))
                    {
                        npc.position = p;
                        return;
                    }
                }
            }
        }

        private static bool NinjaSolidTile(int tx, int ty)
        {
            if (tx < 0 || tx >= Main.maxTilesX || ty < 0 || ty >= Main.maxTilesY)
                return false;

            Tile tile = Main.tile[tx, ty];

            return tile.HasUnactuatedTile && (Main.tileSolid[tile.TileType] || Main.tileSolidTop[tile.TileType]);
        }

        // World Y of the top of the first solid tile at or below startY in this column.
        private static float FindGroundY(float x, float startY)
        {
            int tx = (int)(x / 16f);
            int ty = (int)(startY / 16f);

            for (int i = 0; i < 80 && ty < Main.maxTilesY - 2; i++, ty++)
            {
                if (NinjaSolidTile(tx, ty))
                    break;
            }

            return ty * 16f;
        }

        private static float FindCeilingY(NPC npc)
        {
            int tx = (int)(npc.Center.X / 16f);
            int ty = (int)(npc.Top.Y / 16f);

            for (int i = 0; i < 40 && ty > 1; i++, ty--)
            {
                if (NinjaSolidTile(tx, ty))
                    return (ty + 1) * 16f;
            }

            return npc.Top.Y - 40f * 16f;
        }

        private static void SpawnGeyser(NPC npc, float x, float startY, float delay)
        {
            float groundY = FindGroundY(x, startY);

            // Don't erupt out of a wall.
            if (Collision.SolidCollision(new Vector2(x - 20f, groundY - 48f), 40, 40))
                return;

            NPC.NewNPC(npc.GetSource_FromAI(), (int)x, (int)groundY, ModContent.NPCType<KingSlimeClone>(),
                ai0: npc.whoAmI, ai1: 0f, ai2: 4f, ai3: delay);
        }

        // ---------------------------------------------------------------------------------------------
        // Shadow Dive: crouch, leap, hover above you while the dive line locks on, plunge, gel geysers on landing.
        // The plunge is parryable; the landing is a free punish window.
        // ---------------------------------------------------------------------------------------------

        private const int NinjaLeapCrouchStep = 0;
        private const int NinjaLeapRiseStep = 1;
        private const int NinjaLeapHoverStep = 2;
        private const int NinjaLeapPlungeStep = 3;
        private const int NinjaLeapLandStep = 4;

        private const float NinjaLeapHeight = 240f;
        private const float NinjaLeapLockTime = 12f;

        private static float NinjaLeapCrouchDuration => 22f / NinjaTempo;
        private static float NinjaLeapHoverDuration => 36f / NinjaTempo;
        private static float NinjaLeapLandDuration => 40f / NinjaTempo;
        private static float NinjaLeapPlungeSpeed => Main.masterMode ? 24f : Main.expertMode ? 21f : 18f;
        private static int NinjaLeapGeysers => Main.masterMode ? 3 : 2;

        private void EnterNinjaLeap(NPC npc)
        {
            SetPhase(npc, AttackPhase.NinjaLeap);
            SetStep(npc, NinjaLeapCrouchStep);
            npc.ai[Slot_Timer] = 0f;
            npc.velocity.X = 0f;
            npc.damage = 0;
            SetSlamTargetPos(npc, Vector2.Zero);

            if (IsAuthority)
                npc.netUpdate = true;
        }

        // The plunge always heads downward, even if the player is above him.
        private static Vector2 LeapDirection(Vector2 from, Vector2 aim)
        {
            Vector2 dir = aim - from;

            if (dir.LengthSquared() < 1f)
                return new Vector2(0f, 1f);

            dir.Normalize();

            if (dir.Y < 0.35f)
            {
                dir.Y = 0.35f;
                dir.Normalize();
            }

            return dir;
        }

        private void DoNinjaLeap(NPC npc, Player target)
        {
            ref float timer = ref npc.ai[Slot_Timer];
            int step = CurrentStep(npc);
            KingSlimeInstanceData data = npc.GetGlobalNPC<KingSlimeInstanceData>();

            if (step == NinjaLeapCrouchStep)
            {
                npc.noGravity = false;
                npc.noTileCollide = false;
                npc.damage = 0;
                npc.velocity.X *= 0.7f;
                npc.direction = target.Center.X > npc.Center.X ? 1 : -1;
                npc.spriteDirection = npc.direction;

                if (timer <= 1f)
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.3f, Volume = 0.8f }, npc.Center);

                if (!Main.dedServ && Main.rand.NextBool(2))
                    SmokePuff(npc, 1);

                if (timer >= NinjaLeapCrouchDuration)
                {
                    float apexY = Math.Max(target.Center.Y - NinjaLeapHeight, FindCeilingY(npc) + npc.height * 0.5f + 8f);
                    apexY = Math.Min(apexY, npc.Center.Y - 40f);
                    data.NinjaLeapPoint = new Vector2(npc.Center.X, apexY);

                    KingSlimeSfx.Swing(npc.Center, -0.3f, 0.9f);

                    if (!Main.dedServ)
                        SmokePuff(npc, 10);

                    SetStep(npc, NinjaLeapRiseStep);
                    timer = 0f;

                    if (IsAuthority)
                        npc.netUpdate = true;
                }
            }
            else if (step == NinjaLeapRiseStep)
            {
                npc.noGravity = true;
                npc.noTileCollide = false;
                npc.damage = 0;

                Vector2 goal = new Vector2(npc.Center.X + (target.Center.X - npc.Center.X) * 0.08f, data.NinjaLeapPoint.Y);
                Vector2 toGoal = goal - npc.Center;
                Vector2 v = toGoal * 0.16f;

                if (v.Length() > 16f)
                    v = Vector2.Normalize(v) * 16f;

                npc.velocity = v;
                npc.direction = target.Center.X > npc.Center.X ? 1 : -1;
                npc.spriteDirection = npc.direction;

                if (!Main.dedServ)
                {
                    Dust d = Dust.NewDustPerfect(npc.Bottom + Main.rand.NextVector2Circular(8f, 4f), DustID.Smoke,
                        new Vector2(0f, 1.5f), 150, Color.White, 1f);
                    d.noGravity = true;
                }

                if (toGoal.Length() < 14f || timer >= 26f)
                {
                    npc.velocity *= 0.3f;
                    SetStep(npc, NinjaLeapHoverStep);
                    timer = 0f;
                }
            }
            else if (step == NinjaLeapHoverStep)
            {
                npc.noGravity = true;
                npc.noTileCollide = false;
                npc.damage = 0;
                npc.direction = target.Center.X > npc.Center.X ? 1 : -1;
                npc.spriteDirection = npc.direction;

                float duration = NinjaLeapHoverDuration;
                bool locked = timer >= duration - NinjaLeapLockTime;

                // Energy streaks pull in and flash exactly as he launches.
                if ((int)timer == (int)(duration - 18f))
                    VisualFX.PlayOn(KingSlimeFx.DiveCharge, npc);

                if (!locked)
                {
                    float vx = MathHelper.Clamp((target.Center.X - npc.Center.X) * 0.09f, -7f, 7f);
                    float vy = (data.NinjaLeapPoint.Y - npc.Center.Y) * 0.1f;
                    npc.velocity = Vector2.Lerp(npc.velocity, new Vector2(vx, vy), 0.2f);
                }
                else
                {
                    npc.velocity *= 0.6f;

                    if (SlamTargetPos(npc) == Vector2.Zero)
                        SetSlamTargetPos(npc, target.Center);
                }

                if (!Main.dedServ)
                {
                    Vector2 aim = locked ? SlamTargetPos(npc) : target.Center;
                    Vector2 dir = LeapDirection(npc.Center, aim);
                    float length = Vector2.Distance(npc.Center, aim);
                    float t = MathHelper.Clamp(timer / duration, 0f, 1f);
                    Color col = (locked ? Color.White : new Color(120, 255, 220)) * (0.25f + 0.6f * t);

                    DashLine(npc.Center, npc.Center + dir * (length + 160f), col, 12f);

                    float groundY = FindGroundY(aim.X, aim.Y - 8f);
                    Vector2 marker = new Vector2(aim.X, groundY - 3f);
                    float inner = MathHelper.Lerp(20f, 90f, t);

                    BossParticleSystem.GroundMarkers.Create(new ParticleInfo(
                        position: marker.ToNumerics(),
                        velocity: SystemVector2.Zero,
                        rotation: 0f,
                        scale: new SystemVector2(90f, 27f),
                        color: col,
                        duration: 2
                    ));

                    BossParticleSystem.GroundMarkers.Create(new ParticleInfo(
                        position: marker.ToNumerics(),
                        velocity: SystemVector2.Zero,
                        rotation: 0f,
                        scale: new SystemVector2(inner, inner * 0.3f),
                        color: col,
                        duration: 2
                    ));
                }

                if (timer >= duration)
                {
                    Vector2 dir = LeapDirection(npc.Center, SlamTargetPos(npc));
                    data.NinjaLeapPoint = dir;
                    npc.velocity = dir * NinjaLeapPlungeSpeed;

                    if (Math.Abs(dir.X) > 0.1f)
                    {
                        npc.direction = dir.X > 0f ? 1 : -1;
                        npc.spriteDirection = npc.direction;
                    }

                    KingSlimeSfx.Swing(npc.Center, -0.1f, 1f);
                    VisualFX.Sparks(npc.Center, -dir, 0.6f, 8, 3f, 7f, new Color(120, 255, 220), 8f, 16);
                    SetStep(npc, NinjaLeapPlungeStep);
                    timer = 0f;

                    if (IsAuthority)
                        npc.netUpdate = true;
                }
            }
            else if (step == NinjaLeapPlungeStep)
            {
                npc.noGravity = true;
                npc.noTileCollide = false;
                npc.damage = npc.defDamage;
                npc.velocity = data.NinjaLeapPoint * NinjaLeapPlungeSpeed;

                if (!Main.dedServ)
                {
                    Dust.NewDust(npc.position, npc.width, npc.height, DustID.Smoke,
                        -npc.velocity.X * 0.1f, -npc.velocity.Y * 0.1f, 100, new Color(120, 255, 220), 1.1f);
                }

                bool landed = timer >= 3f && (npc.collideY || npc.collideX || (npc.position - npc.oldPosition).LengthSquared() < 4f);

                if (landed || timer >= 50f)
                {
                    npc.velocity = Vector2.Zero;
                    npc.noGravity = false;
                    npc.damage = 0;
                    NinjaUnstick(npc);
                    SetSlamTargetPos(npc, Vector2.Zero);

                    SlimeImpact(npc.Bottom, 0.7f);
                    ScreenShake(npc, 9f, 14, 12f);
                    KingSlimeSfx.DiveImpact(npc.Center);

                    if (!Main.dedServ)
                    {
                        for (int i = 0; i < 16; i++)
                        {
                            Dust.NewDust(npc.position, npc.width, npc.height, DustID.t_Slime,
                                Main.rand.NextFloat(-4f, 4f), -Main.rand.NextFloat(1f, 4f), 100, Color.Cyan, 1.5f);
                        }
                    }

                    // Gel geysers ripple outward from the crater.
                    if (IsAuthority)
                    {
                        int count = NinjaLeapGeysers;

                        for (int side = -1; side <= 1; side += 2)
                        {
                            for (int i = 0; i < count; i++)
                                SpawnGeyser(npc, npc.Center.X + side * 80f * (i + 1), npc.Bottom.Y - 24f, 18f + i * 10f);
                        }
                    }

                    SetStep(npc, NinjaLeapLandStep);
                    timer = 0f;

                    if (IsAuthority)
                        npc.netUpdate = true;
                }
            }
            else
            {
                npc.noGravity = false;
                npc.noTileCollide = false;
                npc.damage = 0;
                npc.velocity.X *= 0.8f;

                if (timer >= NinjaLeapLandDuration)
                    ReturnToNinjaWalk(npc);
            }
        }

        // ---------------------------------------------------------------------------------------------
        // Gel Geysers: he stomps and gel erupts where you were standing. Rounds: single -> pincer (safe in the middle) -> triple.
        // ---------------------------------------------------------------------------------------------

        private static int NinjaGeyserRounds => Main.masterMode ? 5 : Main.expertMode ? 4 : 3;
        private static float NinjaGeyserDelay => 36f / NinjaTempo;
        private static float NinjaGeyserFirst => 40f / NinjaTempo;
        private static float NinjaGeyserGap => 46f / NinjaTempo;

        private void EnterNinjaGeyser(NPC npc)
        {
            SetPhase(npc, AttackPhase.NinjaGeyser);
            SetStep(npc, 0);
            npc.ai[Slot_Timer] = 0f;
            npc.velocity.X = 0f;
            npc.damage = 0;

            if (IsAuthority)
                npc.netUpdate = true;
        }

        private void DoNinjaGeyser(NPC npc, Player target)
        {
            ref float timer = ref npc.ai[Slot_Timer];

            npc.noGravity = false;
            npc.noTileCollide = false;
            npc.damage = 0;
            npc.velocity.X *= 0.8f;
            npc.direction = target.Center.X > npc.Center.X ? 1 : -1;
            npc.spriteDirection = npc.direction;

            float first = NinjaGeyserFirst;
            float gap = NinjaGeyserGap;
            int rounds = NinjaGeyserRounds;

            if (timer <= 1f)
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.2f, Volume = 0.9f }, npc.Center);

            for (int w = 0; w < rounds; w++)
            {
                float spawnAt = first + w * gap;

                if (IsAuthority && (int)timer == (int)spawnAt)
                {
                    float[] offsets = (w % 3) switch
                    {
                        0 => new[] { 0f },
                        1 => new[] { -130f, 130f },
                        _ => new[] { 0f, -210f, 210f },
                    };

                    foreach (float offset in offsets)
                        SpawnGeyser(npc, target.Center.X + offset, target.Bottom.Y - 24f, NinjaGeyserDelay);

                    KingSlimeSfx.GeyserCall(npc.Center);
                }

                if (!Main.dedServ && (int)timer == (int)spawnAt)
                {
                    SmokePuff(npc, 5);
                    VisualFX.Ring(npc.Bottom - new Vector2(0f, 3f), 110f, new Color(255, 215, 90), 14, 0.3f);
                }
            }

            if (timer >= first + (rounds - 1) * gap + NinjaGeyserDelay + 28f + 18f)
                ReturnToNinjaWalk(npc);
        }

        private void EnterStunned(NPC npc)
        {
            SetSlashHitbox(npc, false);
            npc.GetGlobalNPC<KingSlimeInstanceData>().NinjaFlicker = false;
            SetPhase(npc, AttackPhase.NinjaStunned);
            SetStep(npc, 0);
            npc.ai[Slot_Timer] = 0f;
            npc.velocity.X = 0f;
            npc.damage = 0;

            SoundEngine.PlaySound(SoundID.NPCHit4 with { Pitch = 0.6f }, npc.Center);

            if (!Main.dedServ)
            {
                for (int i = 0; i < 10; i++)
                {
                    Dust.NewDust(npc.position, npc.width, npc.height, DustID.Electric,
                        Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-2f, 2f), 100, Color.Gold, 1.2f);
                }
            }

            if (IsAuthority)
                npc.netUpdate = true;
        }

        private void DoNinjaStunned(NPC npc)
        {
            npc.noGravity = false;
            npc.noTileCollide = false;
            npc.velocity.X *= 0.8f;
            npc.damage = 0;

            ref float timer = ref npc.ai[Slot_Timer];

            if (timer >= NinjaStunnedDuration)
            {
                npc.damage = npc.defDamage;
                SetPhase(npc, AttackPhase.NinjaWalk);
                npc.ai[Slot_Timer] = 0f;
                SetStep(npc, 0);

                if (IsAuthority)
                    npc.netUpdate = true;
            }
        }

        private void DrawNinja(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            switch (CurrentPhase(npc))
            {
                case AttackPhase.TransformToNinja:
                    DrawNinjaPopup(npc, spriteBatch, screenPos, drawColor, false);
                    return;

                case AttackPhase.TransformToSlime:
                    DrawNinjaPopup(npc, spriteBatch, screenPos, drawColor, true);
                    return;

                case AttackPhase.NinjaStab:
                    DrawNinjaStab(npc, spriteBatch, screenPos, drawColor);
                    return;

                case AttackPhase.NinjaSlash:
                case AttackPhase.NinjaCounter:
                    DrawNinjaSlash(npc, spriteBatch, screenPos, drawColor);
                    return;

                case AttackPhase.NinjaStunned:
                    DrawNinjaStunned(npc, spriteBatch, screenPos, drawColor);
                    return;

                case AttackPhase.NinjaVanish:
                    DrawNinjaVanish(npc, spriteBatch, screenPos, drawColor);
                    return;

                case AttackPhase.NinjaLeap:
                    DrawNinjaLeap(npc, spriteBatch, screenPos, drawColor);
                    return;

                case AttackPhase.NinjaGeyser:
                    DrawNinjaGeyser(npc, spriteBatch, screenPos, drawColor);
                    return;
            }

            DrawNinjaWalkCycle(npc, spriteBatch, screenPos, drawColor);
        }

        // Damped spring, 1 -> 0 with a little overshoot. Drives the squash/stretch settle after each move.
        private static float NinjaSpring(float u) => (float)(Math.Exp(-4.2 * u) * Math.Cos(Math.Clamp(u, 0f, 1f) * 9.0));

        private static void DrawNinjaSprite(SpriteBatch sb, Texture2D tex, Rectangle frame, Vector2 origin, Vector2 anchor,
            Vector2 screenPos, Color color, Vector2 scale, float rotation, SpriteEffects fx)
        {
            sb.Draw(tex, anchor - screenPos, frame, color, rotation, origin, scale, fx, 0f);
        }

        private static void DrawNinjaTrail(SpriteBatch sb, Texture2D tex, Rectangle frame, Vector2 origin, Vector2 anchor,
            Vector2 screenPos, Vector2 velocity, Color tint, Vector2 scale, float rotation, SpriteEffects fx,
            int count, float spacing, float alpha)
        {
            for (int i = count; i >= 1; i--)
            {
                float fade = 1f - i / (float)(count + 1);
                DrawNinjaSprite(sb, tex, frame, origin, anchor - velocity * (i * spacing), screenPos,
                    tint * (alpha * fade * fade), scale, rotation, fx);
            }
        }

        private void DrawNinjaWalkCycle(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            Texture2D ninjaTexture = (ninjaWalkAsset ??= ModContent.Request<Texture2D>(
                "ShatteredIllusion/Content/NPCs/BossAI/VanillaBosses/KingSlime/NinjaWalk"
            )).Value;

            KingSlimeInstanceData data = npc.GetGlobalNPC<KingSlimeInstanceData>();

            int frameCount = Math.Max(1, NinjaWalkFrameCount);
            int frameHeight = Math.Max(1, ninjaTexture.Height / frameCount);

            float speed = Math.Abs(npc.velocity.X);
            bool moving = speed > 0.45f;
            bool airborne = Math.Abs(npc.velocity.Y) > 0.5f;
            int frameIndex = airborne ? 1 : moving ? (int)data.WalkClock % frameCount : 0;

            var frame = new Rectangle(0, frameIndex * frameHeight, ninjaTexture.Width, frameHeight);

            SpriteEffects spriteEffects = npc.spriteDirection == 1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            Vector2 origin = frame.Size() * new Vector2(0.5f, 1f);

            float sx;
            float sy;

            if (moving)
            {
                // Each footfall compresses him slightly; he leans into the run.
                float step = (float)Math.Abs(Math.Sin(data.WalkClock * MathHelper.Pi));
                sy = 1f - 0.035f * step;
                sx = 1f + 0.02f * step;
            }
            else
            {
                float breath = (float)Math.Sin(data.NinjaClock * 0.11f);
                sy = 1f + 0.018f * breath;
                sx = 1f - 0.01f * breath;
            }

            if (airborne)
            {
                // Stretch going up, squash slightly coming down.
                float air = MathHelper.Clamp(-npc.velocity.Y / 8f, -1f, 1f);
                sy = 1f + 0.07f * air;
                sx = 1f - 0.04f * air;
            }

            float lean = MathHelper.Clamp(npc.velocity.X * 0.014f, -0.09f, 0.09f);

            DrawNinjaSprite(spriteBatch, ninjaTexture, frame, origin, npc.Bottom, screenPos, npc.GetAlpha(drawColor),
                new Vector2(npc.scale * sx, npc.scale * sy), npc.rotation + lean, spriteEffects);
        }

        private void DrawNinjaStab(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            Texture2D stabTexture = (ninjaStabAsset ??= ModContent.Request<Texture2D>(
                "ShatteredIllusion/Content/NPCs/BossAI/VanillaBosses/KingSlime/NinjaStab"
            )).Value;

            int step = CurrentStep(npc);
            float timer = npc.ai[Slot_Timer];
            KingSlimeInstanceData data = npc.GetGlobalNPC<KingSlimeInstanceData>();
            float telegraph = StabTelegraph(data);
            float recovery = Math.Max(1f, StabRecovery(data));
            int dir = npc.direction;

            int frameIndex = step switch
            {
                NinjaStabWindupStep => timer >= telegraph - NinjaStabSwingAnticipation ? 1 : 0,
                NinjaStabStrikeStep => 2,
                _ => 3 + (int)MathHelper.Clamp(timer / (recovery / 3f), 0f, 2f),
            };

            frameIndex = Math.Clamp(frameIndex, 0, NinjaStabFrameCount - 1);

            var frame = stabTexture.Frame(1, NinjaStabFrameCount, 0, frameIndex);
            SpriteEffects spriteEffects = npc.spriteDirection == 1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            Vector2 origin = frame.Size() * new Vector2(0.5f, 1f);

            float sx = 1f;
            float sy = 1f;
            float rot = 0f;
            Vector2 shake = Vector2.Zero;

            if (step == NinjaStabWindupStep)
            {
                // Coil back: crouch, lean away from the dash, then tremble during the lock-in.
                float coilSpan = Math.Max(1f, telegraph - NinjaStabSwingAnticipation);
                float coil = Smooth01(timer / coilSpan);

                sx = 1f + 0.06f * coil;
                sy = 1f - 0.08f * coil;
                rot = -dir * 0.10f * coil;

                if (timer >= coilSpan)
                {
                    shake.X = (float)Math.Sin(timer * 4.1f) * 1.5f;
                    sy -= 0.03f;
                }
            }
            else if (step == NinjaStabStrikeStep)
            {
                sx = 1.18f;
                sy = 0.9f;
                rot = dir * 0.1f;
            }
            else
            {
                float spring = NinjaSpring(timer / recovery);

                sx = 1f + 0.16f * spring;
                sy = 1f - 0.12f * spring;
                rot = dir * 0.06f * spring;
            }

            Vector2 scale = new Vector2(npc.scale * sx, npc.scale * sy);
            Vector2 anchor = npc.Bottom + shake;

            if (step == NinjaStabStrikeStep)
            {
                Color cyan = Color.Lerp(npc.GetAlpha(drawColor), new Color(120, 255, 220), 0.65f);
                DrawNinjaTrail(spriteBatch, stabTexture, frame, origin, anchor, screenPos, npc.velocity, cyan, scale, npc.rotation + rot,
                    spriteEffects, 5, 0.55f, 0.55f);
            }

            DrawNinjaSprite(spriteBatch, stabTexture, frame, origin, anchor, screenPos, npc.GetAlpha(drawColor), scale, npc.rotation + rot, spriteEffects);
        }

        private void DrawNinjaSlash(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            Texture2D slashTexture = (ninjaSlashAsset ??= ModContent.Request<Texture2D>(
                "ShatteredIllusion/Content/NPCs/BossAI/VanillaBosses/KingSlime/NinjaSlash"
            )).Value;

            int step = CurrentStep(npc);
            float timer = npc.ai[Slot_Timer];
            int dir = npc.direction;

            bool counter = CurrentPhase(npc) == AttackPhase.NinjaCounter;
            bool followUp = npc.localAI[Local_SlashHit] > 0f;

            int frameIndex = counter ? (step == NinjaCounterStanceStep ? 0 : 5) : step switch
            {
                NinjaSlashWindupStep => 0,
                NinjaSlashSwingStep => timer < 5f ? 1 : 2,
                _ => 3 + (int)MathHelper.Clamp(timer / (NinjaSlashRecoveryDuration / 3f), 0f, 2f),
            };

            int bottom = NinjaSlashFrameBottoms[frameIndex];
            var frame = new Rectangle(0, bottom - NinjaSlashFrameHeight, NinjaSlashSheetWidth, NinjaSlashFrameHeight);

            bool facingRight = npc.spriteDirection == 1;
            SpriteEffects spriteEffects = facingRight ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            Vector2 origin = new Vector2(facingRight ? NinjaSlashOriginX : NinjaSlashSheetWidth - NinjaSlashOriginX, frame.Height);

            float widened = Math.Max(0, npc.width - (int)(NinjaHitboxWidth * npc.scale));
            Vector2 anchor = npc.Bottom - new Vector2(dir * widened / 2f, 0f);

            float sx = 1f;
            float sy = 1f;
            float rot = 0f;
            Vector2 shake = Vector2.Zero;

            if (counter)
            {
                if (step == NinjaCounterStanceStep)
                {
                    float pulse = 0.5f + 0.5f * (float)Math.Sin(timer * 0.35f);
                    sx = 1f + 0.03f * pulse;
                    sy = 1f - 0.03f * pulse;
                    shake.X = (float)Math.Sin(timer * 2.3f) * 0.6f;
                }
                else
                {
                    float spring = NinjaSpring(timer / NinjaCounterRecoveryDuration);
                    sx = 1f + 0.1f * spring;
                    sy = 1f - 0.08f * spring;
                }
            }
            else if (step == NinjaSlashWindupStep)
            {
                float duration = Math.Max(1f, followUp ? NinjaSlashFollowUpDuration : NinjaSlashTelegraphDuration);
                float coil = Smooth01(timer / duration);

                sx = 1f + 0.05f * coil;
                sy = 1f - 0.07f * coil;
                rot = -dir * 0.09f * coil;

                if (timer >= duration - NinjaSlashLockTime)
                    shake.X = (float)Math.Sin(timer * 4.1f) * 1.2f;
            }
            else if (step == NinjaSlashSwingStep)
            {
                float u = MathHelper.Clamp(timer / NinjaSlashSwingDuration, 0f, 1f);

                sx = MathHelper.Lerp(1.16f, 1.04f, u);
                sy = MathHelper.Lerp(0.92f, 0.98f, u);
                rot = dir * MathHelper.Lerp(0.14f, 0.04f, u);
            }
            else
            {
                float spring = NinjaSpring(timer / Math.Max(1f, NinjaSlashRecoveryDuration));
                sx = 1f + 0.1f * spring;
                sy = 1f - 0.08f * spring;
                rot = dir * 0.05f * spring;
            }

            Vector2 scale = new Vector2(npc.scale * sx, npc.scale * sy);
            anchor += shake;
            Color color = npc.GetAlpha(drawColor);

            if (counter && step == NinjaCounterStanceStep)
            {
                float pulse = 0.5f + 0.5f * (float)Math.Sin(timer * 0.35f);

                // Pixel-art style red outline glow: four offset copies behind the sprite.
                foreach (Vector2 o in new[] { new Vector2(3f, 0f), new Vector2(-3f, 0f), new Vector2(0f, 3f), new Vector2(0f, -3f) })
                {
                    DrawNinjaSprite(spriteBatch, slashTexture, frame, origin, anchor + o, screenPos,
                        new Color(255, 70, 70) * (0.2f + 0.3f * pulse), scale, npc.rotation + rot, spriteEffects);
                }

                color = Color.Lerp(color, new Color(255, 90, 90), 0.3f + 0.25f * (float)Math.Sin(timer * 0.35f));
            }

            if (!counter && step == NinjaSlashSwingStep && timer < 8f)
            {
                // Smear behind the lunge.
                Color gold = Color.Lerp(color, new Color(255, 210, 140), 0.6f);
                DrawNinjaTrail(spriteBatch, slashTexture, frame, origin, anchor, screenPos, new Vector2(dir * 9f, 0f), gold, scale,
                    npc.rotation + rot, spriteEffects, 3, 1.1f, 0.5f * (1f - timer / 8f));
            }

            DrawNinjaSprite(spriteBatch, slashTexture, frame, origin, anchor, screenPos, color, scale, npc.rotation + rot, spriteEffects);
        }

        private void DrawNinjaStunned(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            Texture2D parryTexture = (ninjaStunAsset ??= ModContent.Request<Texture2D>(
                "ShatteredIllusion/Content/NPCs/BossAI/VanillaBosses/KingSlime/NinjaStun"
            )).Value;

            float timer = npc.ai[Slot_Timer];

            int frameIndex = (int)MathHelper.Clamp(
                timer / (NinjaStunnedMaterializeDuration / NinjaStunnedFrameCount),
                0f,
                NinjaStunnedFrameCount - 1
            );

            Rectangle frame = NinjaStunFrames[frameIndex];
            SpriteEffects spriteEffects = npc.spriteDirection == 1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            Vector2 origin = frame.Size() * new Vector2(0.5f, 1f);

            // Jitters while he re-materializes, then sways dizzily once he's solid again.
            float materialize = MathHelper.Clamp(timer / NinjaStunnedMaterializeDuration, 0f, 1f);
            Vector2 jitter = new Vector2((float)Math.Sin(timer * 2.3f) * 1.4f * (1f - materialize), 0f);
            float sway = materialize >= 1f ? (float)Math.Sin(timer * 0.22f) * 0.06f : 0f;

            DrawNinjaSprite(spriteBatch, parryTexture, frame, origin, npc.Bottom + jitter, screenPos, npc.GetAlpha(drawColor),
                new Vector2(npc.scale), npc.rotation + sway, spriteEffects);
        }

        private void DrawNinjaLeap(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            Texture2D stabTexture = (ninjaStabAsset ??= ModContent.Request<Texture2D>(
                "ShatteredIllusion/Content/NPCs/BossAI/VanillaBosses/KingSlime/NinjaStab"
            )).Value;

            int step = CurrentStep(npc);
            float timer = npc.ai[Slot_Timer];
            KingSlimeInstanceData data = npc.GetGlobalNPC<KingSlimeInstanceData>();
            Color color = npc.GetAlpha(drawColor);
            SpriteEffects fx = npc.spriteDirection == 1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            int dir = npc.direction;

            if (step == NinjaLeapCrouchStep)
            {
                Texture2D walk = (ninjaWalkAsset ??= ModContent.Request<Texture2D>(
                    "ShatteredIllusion/Content/NPCs/BossAI/VanillaBosses/KingSlime/NinjaWalk"
                )).Value;

                int frameHeight = Math.Max(1, walk.Height / Math.Max(1, NinjaWalkFrameCount));
                var wf = new Rectangle(0, 0, walk.Width, frameHeight);
                float coil = Smooth01(timer / Math.Max(1f, NinjaLeapCrouchDuration));

                DrawNinjaSprite(spriteBatch, walk, wf, wf.Size() * new Vector2(0.5f, 1f), npc.Bottom, screenPos, color,
                    new Vector2(npc.scale * (1f + 0.14f * coil), npc.scale * (1f - 0.22f * coil)), npc.rotation, fx);
                return;
            }

            var frame = stabTexture.Frame(1, NinjaStabFrameCount, 0, 1);
            Vector2 bottomOrigin = frame.Size() * new Vector2(0.5f, 1f);

            if (step == NinjaLeapRiseStep)
            {
                DrawNinjaSprite(spriteBatch, stabTexture, frame, bottomOrigin, npc.Bottom, screenPos, color,
                    new Vector2(npc.scale * 0.92f, npc.scale * 1.14f), npc.rotation, fx);
            }
            else if (step == NinjaLeapHoverStep)
            {
                float pulse = 0.5f + 0.5f * (float)Math.Sin(timer * 0.4f);
                Vector2 shake = new Vector2((float)Math.Sin(timer * 3.7f) * 1.2f, 0f);
                Vector2 scale = new Vector2(npc.scale * (1f + 0.03f * pulse), npc.scale * (1f - 0.03f * pulse));

                // Cyan pixel-outline glow while he lines up the dive.
                foreach (Vector2 o in new[] { new Vector2(3f, 0f), new Vector2(-3f, 0f), new Vector2(0f, 3f), new Vector2(0f, -3f) })
                {
                    DrawNinjaSprite(spriteBatch, stabTexture, frame, bottomOrigin, npc.Bottom + shake + o, screenPos,
                        new Color(120, 255, 220) * (0.2f + 0.3f * pulse), scale, npc.rotation - dir * 0.08f, fx);
                }

                DrawNinjaSprite(spriteBatch, stabTexture, frame, bottomOrigin, npc.Bottom + shake, screenPos, color, scale, npc.rotation - dir * 0.08f, fx);
            }
            else if (step == NinjaLeapPlungeStep)
            {
                // Rotated to point along the dive; pivots around his middle so the body doesn't swing.
                Vector2 d = data.NinjaLeapPoint;
                int side = npc.spriteDirection == 1 ? 1 : -1;
                float rot = side * (float)Math.Atan2(d.Y, Math.Abs(d.X));

                var strike = stabTexture.Frame(1, NinjaStabFrameCount, 0, 2);
                Vector2 center = strike.Size() * 0.5f;
                Vector2 scale = new Vector2(npc.scale * 1.1f, npc.scale * 0.95f);
                Color cyan = Color.Lerp(color, new Color(120, 255, 220), 0.65f);

                DrawNinjaTrail(spriteBatch, stabTexture, strike, center, npc.Center, screenPos, npc.velocity, cyan, scale, rot, fx, 5, 0.55f, 0.55f);
                DrawNinjaSprite(spriteBatch, stabTexture, strike, center, npc.Center, screenPos, color, scale, rot, fx);
            }
            else
            {
                float u = MathHelper.Clamp(timer / Math.Max(1f, NinjaLeapLandDuration), 0f, 1f);
                int landFrame = 3 + (int)MathHelper.Clamp(timer / (NinjaLeapLandDuration / 3f), 0f, 2f);
                var landRect = stabTexture.Frame(1, NinjaStabFrameCount, 0, Math.Clamp(landFrame, 0, NinjaStabFrameCount - 1));
                float spring = NinjaSpring(u);

                DrawNinjaSprite(spriteBatch, stabTexture, landRect, landRect.Size() * new Vector2(0.5f, 1f), npc.Bottom, screenPos, color,
                    new Vector2(npc.scale * (1f + 0.2f * spring), npc.scale * (1f - 0.18f * spring)), npc.rotation, fx);
            }
        }

        private void DrawNinjaGeyser(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            Texture2D stabTexture = (ninjaStabAsset ??= ModContent.Request<Texture2D>(
                "ShatteredIllusion/Content/NPCs/BossAI/VanillaBosses/KingSlime/NinjaStab"
            )).Value;

            float timer = npc.ai[Slot_Timer];
            float first = NinjaGeyserFirst;
            float gap = Math.Max(1f, NinjaGeyserGap);

            // Each round he stomps: a sharp squash that springs back.
            float stomp = 0f;

            if (timer >= first)
                stomp = NinjaSpring(((timer - first) % gap) / 22f);

            var frame = stabTexture.Frame(1, NinjaStabFrameCount, 0, timer >= first - 8f ? 1 : 0);
            SpriteEffects fx = npc.spriteDirection == 1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

            DrawNinjaSprite(spriteBatch, stabTexture, frame, frame.Size() * new Vector2(0.5f, 1f), npc.Bottom, screenPos, npc.GetAlpha(drawColor),
                new Vector2(npc.scale * (1f + 0.1f * stomp), npc.scale * (1f - 0.1f * stomp)), npc.rotation, fx);
        }

        // Vanish / ambush: he melts into the goo (popup sheet forwards) and re-forms from it (sheet backwards)
        // rather than just fading out. Fully opaque on purpose - npc.alpha is driven to 255 for the hidden step.
        private void DrawNinjaVanish(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            int step = CurrentStep(npc);

            if (step == NinjaVanishHiddenStep)
                return;

            Texture2D popupTexture = (ninjaPopupAsset ??= ModContent.Request<Texture2D>(
                "ShatteredIllusion/Content/NPCs/BossAI/VanillaBosses/KingSlime/NinjaPopup"
            )).Value;

            KingSlimeInstanceData data = npc.GetGlobalNPC<KingSlimeInstanceData>();
            bool quick = data.NinjaFlicker && data.NinjaComboDone > 0;
            bool sinking = step == NinjaVanishFadeStep;
            float duration = sinking ? (quick ? 6f : NinjaVanishFadeDuration) : NinjaVanishAppearDuration(quick);
            float t = MathHelper.Clamp(npc.ai[Slot_Timer] / duration, 0f, 1f);

            SpriteEffects spriteEffects = npc.spriteDirection == 1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

            if (sinking)
            {
                int stage = (int)MathHelper.Clamp(t * NinjaPopupFrameCount, 0f, NinjaPopupFrameCount - 1);
                Rectangle sinkFrame = NinjaPopupFrames[stage];

                DrawNinjaSprite(spriteBatch, popupTexture, sinkFrame, sinkFrame.Size() * new Vector2(0.5f, 1f), npc.Bottom, screenPos, drawColor,
                    new Vector2(npc.scale), npc.rotation, spriteEffects);
                return;
            }

            EmergePose(t, quick ? 14f : 26f, out int riseStage, out float lift, out Vector2 stretch);
            Rectangle frame = NinjaPopupFrames[NinjaPopupFrameCount - 1 - riseStage];

            DrawNinjaSprite(spriteBatch, popupTexture, frame, frame.Size() * new Vector2(0.5f, 1f), npc.Bottom - new Vector2(0f, lift), screenPos, drawColor,
                new Vector2(npc.scale) * stretch, npc.rotation, spriteEffects);
        }

        private void DrawNinjaPopup(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor, bool sinking)
        {
            Texture2D popupTexture = (ninjaPopupAsset ??= ModContent.Request<Texture2D>(
                "ShatteredIllusion/Content/NPCs/BossAI/VanillaBosses/KingSlime/NinjaPopup"
            )).Value;

            float timer = npc.ai[Slot_Timer];
            Rectangle frame;
            float lift = 0f;
            Vector2 stretch = Vector2.One;

            if (sinking)
            {
                int stageIndex = (int)MathHelper.Clamp(timer / NinjaPopupFrameDuration, 0f, NinjaPopupFrameCount - 1);
                frame = NinjaPopupFrames[stageIndex];
            }
            else
            {
                // Read the sheet back to front - see the comment on NinjaPopupFrames above - then hop out of the goo.
                EmergePose(timer / TransformRiseDuration, 40f, out int riseStage, out lift, out stretch);
                frame = NinjaPopupFrames[NinjaPopupFrameCount - 1 - riseStage];
            }

            SpriteEffects spriteEffects = npc.spriteDirection == 1
                ? SpriteEffects.None
                : SpriteEffects.FlipHorizontally;

            Vector2 origin = frame.Size() * new Vector2(0.5f, 1f);
            Vector2 drawPos = npc.Bottom - new Vector2(0f, lift) - screenPos;

            if (!sinking && !Main.dedServ)
                EmergePuddle(npc.Bottom, MathHelper.Clamp(timer / TransformRiseDuration, 0f, 1f), 150f);

            var draw = new DrawData(
                popupTexture,
                drawPos,
                frame,
                npc.GetAlpha(drawColor),
                npc.rotation,
                origin,
                npc.scale * stretch,
                spriteEffects,
                0f
            );

            draw.Draw(spriteBatch);
        }
    }
}