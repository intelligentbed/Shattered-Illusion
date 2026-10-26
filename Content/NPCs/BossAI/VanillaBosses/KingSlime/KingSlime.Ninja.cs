using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ShatteredIllusion.Common.Players.ParrySystem;
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
        private const float NinjaVanishAppearDuration = 12f;
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
                    SoundEngine.PlaySound(SoundID.Item1 with { Pitch = 0.1f, Volume = 0.8f }, npc.Center);

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
                    SoundEngine.PlaySound(SoundID.Item1 with { Pitch = 0.3f, Volume = 0.9f }, npc.Center);
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

            SoundEngine.PlaySound(SoundID.Item8 with { Pitch = 0.3f }, npc.Center);

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

                    SoundEngine.PlaySound(SoundID.Item8 with { Pitch = 0.6f }, npc.Center);

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
                npc.alpha = (int)MathHelper.Lerp(255, SlimeAlpha, timer / NinjaVanishAppearDuration);

                if (timer >= NinjaVanishAppearDuration)
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

                    SoundEngine.PlaySound(SoundID.Item1 with { Pitch = 0.4f, Volume = 0.7f }, npc.Center);

                    SetStep(npc, NinjaStabStrikeStep);
                    timer = 0f;
                }
            }
            else if (step == NinjaStabStrikeStep)
            {
                // The dash itself, and the parryable window. noGravity noTileCollide and stuff like that
                npc.noGravity = true;
                npc.noTileCollide = true;
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
            int frameIndex = moving ? (int)data.WalkClock % frameCount : 0;

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
            float duration = sinking ? (quick ? 6f : NinjaVanishFadeDuration) : NinjaVanishAppearDuration;
            float t = MathHelper.Clamp(npc.ai[Slot_Timer] / duration, 0f, 1f);

            int stage = (int)MathHelper.Clamp(t * NinjaPopupFrameCount, 0f, NinjaPopupFrameCount - 1);
            Rectangle frame = NinjaPopupFrames[sinking ? stage : NinjaPopupFrameCount - 1 - stage];

            SpriteEffects spriteEffects = npc.spriteDirection == 1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            Vector2 origin = frame.Size() * new Vector2(0.5f, 1f);

            DrawNinjaSprite(spriteBatch, popupTexture, frame, origin, npc.Bottom, screenPos, drawColor,
                new Vector2(npc.scale), npc.rotation, spriteEffects);
        }

        private void DrawNinjaPopup(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor, bool sinking)
        {
            Texture2D popupTexture = (ninjaPopupAsset ??= ModContent.Request<Texture2D>(
                "ShatteredIllusion/Content/NPCs/BossAI/VanillaBosses/KingSlime/NinjaPopup"
            )).Value;

            float timer = npc.ai[Slot_Timer];
            int stageIndex = (int)MathHelper.Clamp(timer / NinjaPopupFrameDuration, 0f, NinjaPopupFrameCount - 1);

            // Read the sheet back to front - see the comment on NinjaPopupFrames above.
            Rectangle frame = NinjaPopupFrames[sinking ? stageIndex : NinjaPopupFrameCount - 1 - stageIndex];

            SpriteEffects spriteEffects = npc.spriteDirection == 1
                ? SpriteEffects.None
                : SpriteEffects.FlipHorizontally;

            Vector2 origin = frame.Size() * new Vector2(0.5f, 1f);
            Vector2 drawPos = npc.Bottom - screenPos;

            var draw = new DrawData(
                popupTexture,
                drawPos,
                frame,
                npc.GetAlpha(drawColor),
                npc.rotation,
                origin,
                npc.scale,
                spriteEffects,
                0f
            );

            draw.Draw(spriteBatch);
        }
    }
}