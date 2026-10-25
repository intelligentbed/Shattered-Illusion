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
        private const float NinjaVanishAppearDuration = 8f;
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
                    SmokePuff(npc, 2);

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
            }

            Texture2D ninjaTexture = (ninjaWalkAsset ??= ModContent.Request<Texture2D>(
                "ShatteredIllusion/Content/NPCs/BossAI/VanillaBosses/KingSlime/NinjaWalk"
            )).Value;

            int frameCount = Math.Max(1, NinjaWalkFrameCount);
            int frameHeight = Math.Max(1, ninjaTexture.Height / frameCount);

            float animTimer = npc.ai[Slot_Timer];
            int frameIndex = (int)(animTimer / NinjaWalkFrameDuration) % frameCount;

            var frame = new Rectangle(0, frameIndex * frameHeight, ninjaTexture.Width, frameHeight);

            SpriteEffects spriteEffects = npc.spriteDirection == 1
                ? SpriteEffects.None
                : SpriteEffects.FlipHorizontally;

            Vector2 origin = frame.Size() * new Vector2(0.5f, 1f);
            Vector2 drawPos = npc.Bottom - screenPos;

            var draw = new DrawData(
                ninjaTexture,
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

        private void DrawNinjaStab(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            Texture2D stabTexture = (ninjaStabAsset ??= ModContent.Request<Texture2D>(
                "ShatteredIllusion/Content/NPCs/BossAI/VanillaBosses/KingSlime/NinjaStab"
            )).Value;

            int step = CurrentStep(npc);
            float timer = npc.ai[Slot_Timer];
            KingSlimeInstanceData data = npc.GetGlobalNPC<KingSlimeInstanceData>();

            int frameIndex = step switch
            {
                NinjaStabWindupStep => timer >= StabTelegraph(data) - NinjaStabSwingAnticipation ? 1 : 0,
                NinjaStabStrikeStep => 2,
                _ => 3 + (int)MathHelper.Clamp(timer / (StabRecovery(data) / 3f), 0f, 2f),
            };

            frameIndex = Math.Clamp(frameIndex, 0, NinjaStabFrameCount - 1);

            var frame = stabTexture.Frame(1, NinjaStabFrameCount, 0, frameIndex);

            SpriteEffects spriteEffects = npc.spriteDirection == 1
                ? SpriteEffects.None
                : SpriteEffects.FlipHorizontally;

            Vector2 origin = frame.Size() * new Vector2(0.5f, 1f);
            Vector2 drawPos = npc.Bottom - screenPos;

            var draw = new DrawData(
                stabTexture,
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

        private void DrawNinjaSlash(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            Texture2D slashTexture = (ninjaSlashAsset ??= ModContent.Request<Texture2D>(
                "ShatteredIllusion/Content/NPCs/BossAI/VanillaBosses/KingSlime/NinjaSlash"
            )).Value;

            int step = CurrentStep(npc);
            float timer = npc.ai[Slot_Timer];

            bool counter = CurrentPhase(npc) == AttackPhase.NinjaCounter;

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
            Vector2 drawPos = npc.Bottom - screenPos - new Vector2(npc.direction * widened / 2f, 0f);

            Color color = npc.GetAlpha(drawColor);

            if (counter && step == NinjaCounterStanceStep)
                color = Color.Lerp(color, new Color(255, 90, 90), 0.3f + 0.25f * (float)Math.Sin(timer * 0.35f));

            var draw = new DrawData(
                slashTexture,
                drawPos,
                frame,
                color,
                npc.rotation,
                origin,
                npc.scale,
                spriteEffects,
                0f
            );

            draw.Draw(spriteBatch);
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

            SpriteEffects spriteEffects = npc.spriteDirection == 1
                ? SpriteEffects.None
                : SpriteEffects.FlipHorizontally;

            Vector2 origin = frame.Size() * new Vector2(0.5f, 1f);
            Vector2 drawPos = npc.Bottom - screenPos;

            var draw = new DrawData(
                parryTexture,
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