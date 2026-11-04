using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ShatteredIllusion.Common.Cutscenes;
using ShatteredIllusion.Common.Players.ParrySystem;
using ShatteredIllusion.Common.Visuals;
using ShatteredIllusion.Core.OverrideSystem;
using ShatteredIllusion.Content.Particles;
using ParticleLibrary.Core.V3.Particles;
using ParticleLibrary.Utilities;
using ReLogic.Content;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;
using SystemVector2 = System.Numerics.Vector2;

namespace ShatteredIllusion.Content.NPCs.BossAI.VanillaBosses.KingSlime
{
    public partial class KingSlimeOverride : NPCBehaviorOverride, IParryable
    {
        public override int NPCOverrideType => NPCID.KingSlime;

        public enum AttackPhase
        {
            Jump,
            BigJump,
            Teleport,
            HugeJump,
            SplitAttack,
            Cooldown,

            TransformToNinja,
            NinjaWalk,
            NinjaStab,
            NinjaSlash,
            NinjaVanish,
            NinjaCounter,
            NinjaStunned,
            NinjaLeap,
            NinjaGeyser,

            TransformToSlime,
            EchoDive,
            CrownSummon,
            WeaponSword,
            WeaponSpear,
            WeaponHammer,
            WeaponBow,
            WeaponStunned,

            Despawn,

            Death
        }

        //Attack order as you know
        private static readonly AttackPhase[] AttackSequence =
        {
            AttackPhase.Jump,
            AttackPhase.BigJump,
            AttackPhase.BigJump,
            AttackPhase.Teleport,
            AttackPhase.HugeJump,
            AttackPhase.SplitAttack,
            AttackPhase.BigJump,
            AttackPhase.HugeJump,
            AttackPhase.Jump,
            AttackPhase.BigJump,
            AttackPhase.BigJump,
            AttackPhase.Teleport,
            AttackPhase.HugeJump,
            AttackPhase.SplitAttack,
            AttackPhase.BigJump,
            AttackPhase.HugeJump,
        };
        //Why is it so long? because this should last around 30 secs for the ninja phase to start 

        #region Ninja phase

        private enum NinjaAttack
        {
            Stab,
            ChainDash,
            Slash,
            Vanish,
            Flicker,
            Counter,
            Leap,
            Geyser,
        }

        private static readonly NinjaAttack[] NinjaAttackSequence =
        {
            NinjaAttack.Stab,
            NinjaAttack.Geyser,
            NinjaAttack.Slash,
            NinjaAttack.Leap,
            NinjaAttack.Counter,
            NinjaAttack.ChainDash,
            NinjaAttack.Vanish,
            NinjaAttack.Geyser,
            NinjaAttack.Flicker,
            NinjaAttack.Leap,
        };

        #endregion

        #region Arena phase

        private static readonly AttackPhase[] ArenaSequence =
        {
            AttackPhase.Jump,
            AttackPhase.WeaponSpear,
            AttackPhase.EchoDive,
            AttackPhase.WeaponBow,
            AttackPhase.BigJump,
            AttackPhase.WeaponSword,
            AttackPhase.CrownSummon,
            AttackPhase.HugeJump,
            AttackPhase.WeaponHammer,
            AttackPhase.SplitAttack,
            AttackPhase.Teleport,
            AttackPhase.WeaponSpear,
            AttackPhase.WeaponBow,
            AttackPhase.WeaponHammer,
        };

        #endregion

        private const float ForceDespawnRange = 3000f;
        private const int ForceDespawnDelay = 60;
        private const float DespawnFadeDuration = 40f;

        private const int SpawnGracePeriod = 120;

        // OMG THE NINJA NOT BEING VISIBLE WAS DRIVING ME CRAZY NEVER LET ME CODE AGAIN 
        private const int SlimeAlpha = 25;

        // Overall King Slime size everything below scales off of it (hopefully) 
        public const float BaseScale = 1.5f;
        private const float SplitMinScale = BaseScale * 0.55f;
        private const float SplitPopScale = BaseScale * 1.35f;

        private const float SplitAnchorYOffset = 16f;

        private const float CrownDropHeight = 46f;
        private const float CrownDropDuration = 24f;

        // i was told to comment things out :(. So this is the amount of mass lost per minion spawned, and the minimum effective scale king slime can shrink to.
        private const float MassLostPerMinion = 0.15f;
        private const float MinEffectiveScale = BaseScale * 0.6f;
        private const int MinionReturnDelayMin = 360;
        private const int MinionReturnDelayMax = 420;

        private static readonly float[] SplitTelegraphSigns = { -1f, 1f };

        private static Asset<Texture2D>? kingSlimeAsset;
        private static Asset<Texture2D>? crownAsset;

        private static bool IsAuthority =>
            Main.netMode != NetmodeID.MultiplayerClient;

        private const int Slot_Phase = 0;
        private const int Slot_Timer = 1;
        private const int Slot_Step = 2;
        private const int Slot_SequenceIndex = 3;

        private const int Local_SlamTargetX = 0;
        private const int Local_SlamTargetY = 1;
        private const int Local_SplitVertical = 2;
        private const int Local_ForceDespawnTimer = 3;


        private const int JumpWindupStep = 0;
        private const int JumpAirborneStep = 1;

        private const int TeleportSubmergingStep = 0;
        private const int TeleportResurfacingStep = 1;
        private const int TeleportHiddenStep = 2; // underground; the landing spot is marked on the floor

        private const float TeleportHiddenDuration = 32f;
        private const float TeleportHiddenLockTime = 12f;
        private const float TeleportResurfaceDuration = 30f;
        private const float TeleportLandPoint = 0.78f; // fraction of the resurface where he comes back down
        private const float TeleportHopHeight = 90f;

        private const int HugeJumpLiftoffStep = 0;
        private const int HugeJumpHoveringStep = 1;
        private const int HugeJumpSlammingStep = 2;

        private const int SplitAttackWindupStep = 0;
        private const int SplitAttackDivergingStep = 1;
        private const int SplitAttackSettlingStep = 2;

        private const int TransformShrinkStep = 0;
        private const int TransformRiseStep = 1;

        private const float TransformShrinkDuration = 24f;
        // Longer than it used to be (30) so the pop-out and hop are readable. Also drives the slime re-forming on revert.
        private const float TransformRiseDuration = 44f;


        //This covers his base size and the size lost to minions, but not the extra size added by SplitAttack 
        private static float EffectiveBaseScale(NPC npc)
        {
            int activeMinions = CountActiveMinions(npc);
            float reduced = BaseScale - activeMinions * MassLostPerMinion;
            return MathHelper.Max(MinEffectiveScale, reduced);
        }

        private static int CountActiveMinions(NPC npc)
        {
            KingSlimeInstanceData data = npc.GetGlobalNPC<KingSlimeInstanceData>();

            if (data.ActiveMinionCountTick == Main.GameUpdateCount)
                return data.ActiveMinionCount;

            int minionType = ModContent.NPCType<KingSlimeMinion>();
            int count = 0;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC other = Main.npc[i];

                if (other.active &&
                    other.type == minionType &&
                    (int)other.ai[KingSlimeMinion.Slot_Owner] == npc.whoAmI)
                {
                    count++;
                }
            }

            data.ActiveMinionCount = count;
            data.ActiveMinionCountTick = Main.GameUpdateCount;
            return count;
        }

        // Changes the hitbox size while keeping the NPC's visual center fixed -
        // npc.position is the top-left corner, so resizing without this shifts
        // the hitbox off-center from what's actually drawn.
        private static void ResizeKeepingCenter(NPC npc, int newWidth, int newHeight)
        {
            Vector2 center = npc.Center;
            npc.width = newWidth;
            npc.height = newHeight;
            npc.position = center - new Vector2(npc.width, npc.height) * 0.5f;
        }

        private static void RecallAllMinions(NPC npc)
        {
            npc.GetGlobalNPC<KingSlimeInstanceData>().ActiveMinionCountTick = uint.MaxValue;

            int minionType = ModContent.NPCType<KingSlimeMinion>();

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC other = Main.npc[i];

                if (other.active &&
                    other.type == minionType &&
                    (int)other.ai[KingSlimeMinion.Slot_Owner] == npc.whoAmI)
                {
                    other.active = false;

                    if (Main.netMode == NetmodeID.Server)
                        NetMessage.SendData(MessageID.SyncNPC, number: other.whoAmI);
                }
            }
        }

        private static AttackPhase CurrentPhase(NPC npc) => (AttackPhase)(int)npc.ai[Slot_Phase];
        private static void SetPhase(NPC npc, AttackPhase phase) => npc.ai[Slot_Phase] = (int)phase;

        private static int CurrentStep(NPC npc) => (int)npc.ai[Slot_Step];
        private static void SetStep(NPC npc, int step) => npc.ai[Slot_Step] = step;

        private static int SequenceIndex(NPC npc) => (int)npc.ai[Slot_SequenceIndex];

        private static Vector2 SlamTargetPos(NPC npc) =>
            new(npc.localAI[Local_SlamTargetX], npc.localAI[Local_SlamTargetY]);

        private static void SetSlamTargetPos(NPC npc, Vector2 pos)
        {
            npc.localAI[Local_SlamTargetX] = pos.X;
            npc.localAI[Local_SlamTargetY] = pos.Y;
        }

        private static bool SplitVertical(NPC npc) => npc.localAI[Local_SplitVertical] == 1f;
        private static void SetSplitVertical(NPC npc, bool value) => npc.localAI[Local_SplitVertical] = value ? 1f : 0f;

        public override void SendExtraData(NPC npc, BinaryWriter binaryWriter)
        {
            binaryWriter.Write(npc.localAI[Local_SlamTargetX]);
            binaryWriter.Write(npc.localAI[Local_SlamTargetY]);
            binaryWriter.Write(npc.localAI[Local_SplitVertical]);
            binaryWriter.Write(npc.localAI[Local_ForceDespawnTimer]);
        }

        public override void ReceiveExtraData(NPC npc, BinaryReader binaryReader)
        {
            npc.localAI[Local_SlamTargetX] = binaryReader.ReadSingle();
            npc.localAI[Local_SlamTargetY] = binaryReader.ReadSingle();
            npc.localAI[Local_SplitVertical] = binaryReader.ReadSingle();
            npc.localAI[Local_ForceDespawnTimer] = binaryReader.ReadSingle();
        }

        public override bool PreAI(NPC npc)
        {
            CutscenePlayer cutscenePlayer =
                Main.LocalPlayer.GetModPlayer<CutscenePlayer>();

            if (cutscenePlayer.Active &&
                cutscenePlayer.TryGetNpc(out NPC trackedNpc) &&
                trackedNpc.whoAmI == npc.whoAmI)
            {
                npc.velocity = Vector2.Zero;
                CinematicIntro(npc);
                return false;
            }


            if (!npc.HasValidTarget)
                npc.TargetClosest(true);

            Player target = Main.player[npc.target];

            KingSlimeInstanceData data = npc.GetGlobalNPC<KingSlimeInstanceData>();

            if (CurrentPhase(npc) != AttackPhase.SplitAttack)
                data.SplitRoarPlayed = false;

            // Cache the vanilla King Slime hitbox once so it can be restored if he
            // reverts from ninja form back to slime form (e.g. despawning mid-fight).
            if (data.OriginalWidth == 0)
            {
                data.OriginalWidth = npc.width;
                data.OriginalHeight = npc.height;
            }

            if (CurrentPhase(npc) == AttackPhase.Death)
            {
                DoDeathScene(npc, data);
                return false;
            }

            npc.dontTakeDamage = false;
            npc.damage = npc.defDamage;

            ref float forceDespawnTimer = ref npc.localAI[Local_ForceDespawnTimer];

            if (data.SpawnGraceTimer < SpawnGracePeriod)
            {
                data.SpawnGraceTimer++;
            }
            else if (!npc.HasValidTarget || npc.Distance(target.Center) > ForceDespawnRange)
            {
                if (++forceDespawnTimer > ForceDespawnDelay &&
                    CurrentPhase(npc) != AttackPhase.Despawn &&
                    IsAuthority)
                {
                    EnterDespawn(npc);
                }
            }
            else
            {
                forceDespawnTimer = 0;
                npc.timeLeft = 3600;
            }

            if (CurrentPhase(npc) == AttackPhase.Despawn)
            {
                DoDespawn(npc);
                return false;
            }

            if (!npc.HasValidTarget)
            {
                npc.velocity.X *= 0.9f;
                return false;
            }

            if (CinematicHitStop(npc, data))
                return false;

            npc.ai[Slot_Timer]++;
            UpdateJellyWobble(npc, data);

            if (npc.velocity.X != 0f)
            {
                npc.direction = npc.velocity.X > 0 ? 1 : -1;
                npc.spriteDirection = npc.direction;
            }

            switch (CurrentPhase(npc))
            {
                case AttackPhase.Jump:
                    DoJump(npc, target, isBigJump: false);
                    break;

                case AttackPhase.BigJump:
                    DoJump(npc, target, isBigJump: true);
                    break;

                case AttackPhase.Teleport:
                    DoTeleport(npc, target);
                    break;

                case AttackPhase.HugeJump:
                    DoHugeJump(npc, target);
                    break;

                case AttackPhase.SplitAttack:
                    DoSplitAttack(npc, target);
                    break;

                case AttackPhase.Cooldown:
                    DoCooldown(npc);
                    break;

                case AttackPhase.TransformToNinja:
                    DoTransform(npc);
                    break;

                case AttackPhase.NinjaWalk:
                    DoNinjaWalk(npc, target);
                    break;

                case AttackPhase.NinjaStab:
                    DoNinjaStab(npc, target);
                    break;

                case AttackPhase.NinjaSlash:
                    DoNinjaSlash(npc, target);
                    break;

                case AttackPhase.NinjaVanish:
                    DoNinjaVanish(npc, target);
                    break;

                case AttackPhase.NinjaCounter:
                    DoNinjaCounter(npc, target);
                    break;

                case AttackPhase.TransformToSlime:
                    DoRevert(npc, target);
                    break;

                case AttackPhase.EchoDive:
                    DoEchoDive(npc, target);
                    break;

                case AttackPhase.CrownSummon:
                    DoCrownSummon(npc);
                    break;

                case AttackPhase.WeaponSword:
                    DoWeaponSword(npc, target);
                    break;

                case AttackPhase.WeaponSpear:
                    DoWeaponSpear(npc, target);
                    break;

                case AttackPhase.WeaponHammer:
                    DoWeaponHammer(npc, target);
                    break;

                case AttackPhase.WeaponBow:
                    DoWeaponBow(npc, target);
                    break;

                case AttackPhase.WeaponStunned:
                    DoWeaponStunned(npc);
                    break;

                case AttackPhase.NinjaStunned:
                    DoNinjaStunned(npc);
                    break;

                case AttackPhase.NinjaLeap:
                    DoNinjaLeap(npc, target);
                    break;

                case AttackPhase.NinjaGeyser:
                    DoNinjaGeyser(npc, target);
                    break;
            }

            ArenaTick(npc, data, target);
            CinematicTick(npc, data);

            return false;
        }

        private void UpdateJellyWobble(NPC npc, KingSlimeInstanceData data)
        {
            data.WobbleClock += 1f;

            float velocityDeltaY = npc.velocity.Y - data.LastVelocityY;
            data.LastVelocityY = npc.velocity.Y;

            if (Math.Abs(velocityDeltaY) > 2f)
            {
                data.SquishSpringVel -= velocityDeltaY * 0.025f;
            }

            data.SquishSpringVel += -data.SquishOffset * 0.35f;
            data.SquishSpringVel *= 0.72f;
            data.SquishOffset += data.SquishSpringVel;
            data.SquishOffset = MathHelper.Clamp(data.SquishOffset, -0.35f, 0.35f);
        }

        private void EnterCooldown(NPC npc)
        {
            SetPhase(npc, AttackPhase.Cooldown);
            npc.ai[Slot_Timer] = 0f;
            SetStep(npc, 0);
            SetSlamTargetPos(npc, Vector2.Zero);

            if (IsAuthority)
                npc.netUpdate = true;
        }

        private void DoCooldown(NPC npc)
        {
            npc.noGravity = false;
            npc.noTileCollide = false;
            npc.alpha = SlimeAlpha;
            npc.scale = EffectiveBaseScale(npc);
            npc.velocity.X *= 0.9f;

            const float cooldownTime = 20f;

            ref float timer = ref npc.ai[Slot_Timer];

            if (timer >= cooldownTime)
            {
                bool arena = IsArena(npc);
                AttackPhase[] sequence = arena ? ArenaSequence : AttackSequence;
                bool finishedFullLoop = SequenceIndex(npc) == sequence.Length - 1;

                if (finishedFullLoop)
                {
                    if (arena)
                        ExitArena(npc);
                    else
                        EnterTransform(npc);

                    return;
                }

                int nextSequenceIndex = (SequenceIndex(npc) + 1) % sequence.Length;
                npc.ai[Slot_SequenceIndex] = nextSequenceIndex;

                AttackPhase nextPhase = sequence[nextSequenceIndex];
                SetPhase(npc, nextPhase);

                // Decide this split's orientation
                if (nextPhase == AttackPhase.SplitAttack && IsAuthority)
                    SetSplitVertical(npc, Main.rand.NextBool(2));

                if (arena)
                    SetArenaTarget(npc, nextPhase);

                timer = 0f;
                SetStep(npc, 0);

                if (IsAuthority)
                    npc.netUpdate = true;
            }
        }

        private void EnterTransform(NPC npc)
        {
            SetPhase(npc, AttackPhase.TransformToNinja);
            SetStep(npc, TransformShrinkStep);
            npc.ai[Slot_Timer] = 0f;
            npc.velocity = Vector2.Zero;
            npc.dontTakeDamage = true;
            npc.damage = 0;
            SetSlamTargetPos(npc, Vector2.Zero);

            if (IsAuthority)
            {
                RecallAllMinions(npc);
                npc.netUpdate = true;
            }
        }

        private void DoTransform(NPC npc)
        {
            npc.noGravity = false;
            npc.noTileCollide = false;
            npc.velocity.X *= 0.8f;
            npc.dontTakeDamage = true;
            npc.damage = 0;

            ref float timer = ref npc.ai[Slot_Timer];
            int step = CurrentStep(npc);

            if (step == TransformShrinkStep)
            {
                if (Main.rand.NextBool(2))
                    Dust.NewDust(npc.position, npc.width, npc.height, DustID.t_Slime, 0f, 1f, 100, Color.Cyan, 1.3f);

                if (timer >= TransformShrinkDuration)
                {
                    for (int i = 0; i < 20; i++)
                    {
                        Dust.NewDust(npc.position, npc.width, npc.height, DustID.Smoke,
                            Main.rand.NextFloat(-2f, 2f), -Main.rand.NextFloat(1f, 3f), 150, Color.White, 1.5f);
                    }

                    KingSlimeSfx.TransformSquash(npc.Center);

                    timer = 0f;
                    SetStep(npc, TransformRiseStep);

                    ResizeKeepingCenter(npc, (int)(NinjaHitboxWidth * npc.scale), (int)(NinjaHitboxHeight * npc.scale));

                    if (IsAuthority)
                        npc.netUpdate = true;
                }
            }
            else if (step == TransformRiseStep)
            {
                // The burst: lands exactly as the ninja leaves the goo and starts to hop.
                float burstAt = TransformRiseDuration * 0.3f;

                if (timer >= burstAt && timer - 1f < burstAt)
                {
                    KingSlimeSfx.Emerge(npc.Center, true);

                    if (!Main.dedServ)
                        VisualFX.Play(KingSlimeFx.GroundEmerge, npc.Bottom);
                }

                if (timer >= TransformRiseDuration)
                {
                    npc.dontTakeDamage = false;
                    npc.damage = npc.defDamage;

                    SetPhase(npc, AttackPhase.NinjaWalk);
                    npc.ai[Slot_Timer] = 0f;
                    npc.ai[Slot_SequenceIndex] = 0f;
                    npc.GetGlobalNPC<KingSlimeInstanceData>().NinjaClock = 0;
                    SetStep(npc, 0);

                    if (IsAuthority)
                        npc.netUpdate = true;
                }
            }
        }

        private void EnterDespawn(NPC npc)
        {
            SetSlashHitbox(npc, false);
            npc.GetGlobalNPC<KingSlimeInstanceData>().ArenaActive = false;
            npc.GetGlobalNPC<KingSlimeInstanceData>().CrownT = 0f;
            SetPhase(npc, AttackPhase.Despawn);
            SetStep(npc, 0);
            npc.ai[Slot_Timer] = 0f;
            SetSlamTargetPos(npc, Vector2.Zero);

            if (IsAuthority)
            {
                npc.netUpdate = true;
                RecallAllMinions(npc);
            }
        }

        private void DoDespawn(NPC npc)
        {
            KingSlimeInstanceData data = npc.GetGlobalNPC<KingSlimeInstanceData>();
            if (data.OriginalWidth != 0 && npc.width != data.OriginalWidth)
                ResizeKeepingCenter(npc, data.OriginalWidth, data.OriginalHeight);

            npc.dontTakeDamage = true;
            npc.damage = 0;

            ref float timer = ref npc.ai[Slot_Timer];
            timer++;
            npc.noGravity = false;
            npc.noTileCollide = false;
            npc.scale = MathHelper.Lerp(npc.scale, BaseScale, 0.1f);

            npc.velocity.X *= 0.95f;
            npc.velocity.Y += 0.75f;

            // Fade out as he drops.
            float fadeT = MathHelper.Clamp(timer / DespawnFadeDuration, 0f, 1f);
            npc.alpha = (int)MathHelper.Lerp(SlimeAlpha, 255, fadeT);

            if (timer >= DespawnFadeDuration && IsAuthority)
            {
                npc.active = false;

                if (Main.netMode == NetmodeID.Server)
                {
                    NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, npc.whoAmI);
                }
            }
        }

        private void DoJump(NPC npc, Player player, bool isBigJump)
        {
            npc.noGravity = false;
            npc.noTileCollide = false;
            npc.alpha = SlimeAlpha;
            npc.scale = EffectiveBaseScale(npc);

            ref float timer = ref npc.ai[Slot_Timer];
            int step = CurrentStep(npc);

            if (step == JumpWindupStep)
            {
                npc.velocity.X *= 0.8f;

                if (Main.rand.NextBool(3))
                {
                    Dust.NewDust(npc.position, npc.width, npc.height, DustID.t_Slime, 0f, 2f, 100, Color.Cyan, 1.2f);
                }

                // Give King Slime a short pause before committing to the jump 
                // funny story I actually almost spilled my water because I was dricking it mid attempt
                if (timer >= 30f)
                {
                    if (IsAuthority)
                    {
                        int dir = player.Center.X > npc.Center.X ? 1 : -1;
                        float distanceX =
                            Math.Abs(player.Center.X - npc.Center.X);

                        float xVel = isBigJump
                            ? MathHelper.Clamp(distanceX * 0.02f, 5f, 9f) * dir
                            : MathHelper.Clamp(distanceX * 0.015f, 3f, 6f) * dir;

                        float yVel = isBigJump ? -8.5f : -5.5f;

                        npc.velocity = new Vector2(xVel, yVel);
                        npc.netUpdate = true;

                        for (int i = 0; i < (isBigJump ? 14 : 7); i++)
                        {
                            Dust.NewDust(
                                npc.position,
                                npc.width,
                                npc.height,
                                DustID.TintableDust,
                                xVel * 0.2f,
                                yVel * 0.2f,
                                100,
                                new Color(0, 100, 255, 120),
                                1.2f
                            );
                        }
                    }

                    KingSlimeSfx.JumpLaunch(npc.Center, isBigJump);

                    SetStep(npc, JumpAirborneStep);
                    timer = 0f;
                }
            }
            else if (step == JumpAirborneStep)
            {
                if (isBigJump && Main.rand.NextBool(2))
                {
                    Dust.NewDust(npc.position, npc.width, npc.height, DustID.t_Slime, npc.velocity.X * 0.3f, npc.velocity.Y * 0.3f, 150, Color.DeepSkyBlue, 1.1f);
                }

                // Wait until the jump has landed before moving to the next attack.
                if (timer > 10f && HasLanded(npc))
                {

                    KingSlimeSfx.JumpLand(npc.Center, isBigJump);

                    for (int i = 0; i < 10; i++)
                    {
                        Dust.NewDust(npc.position, npc.width, npc.height, DustID.BlueCrystalShard, Main.rand.NextFloat(-4f, 4f), Main.rand.NextFloat(-3f, 0f), 100, Color.Cyan, 1.4f);
                    }

                    if (!Main.dedServ)
                    {
                        for (int i = 0; i < (isBigJump ? 10 : 6); i++)
                        {
                            Vector2 sparkVelocity = Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(3f, 7f);

                            BossParticleSystem.EmberBursts.Create(new ParticleInfo(
                                position: npc.Center.ToNumerics(),
                                velocity: sparkVelocity.ToNumerics(),
                                rotation: 0f,
                                scale: new SystemVector2(12f, 12f),
                                color: new Color(120, 210, 255, 255),
                                duration: Main.rand.Next(20, 32)
                            ));
                        }
                    }

                    EnterCooldown(npc);
                }
            }
        }

        // Where the hop lifts him above the ground while he pops back out (visual only; the hitbox stays on the floor).
        private static float TeleportHopLift(NPC npc)
        {
            if (CurrentPhase(npc) != AttackPhase.Teleport || CurrentStep(npc) != TeleportResurfacingStep)
                return 0f;

            float h = MathHelper.Clamp(npc.ai[Slot_Timer] / (TeleportResurfaceDuration * TeleportLandPoint), 0f, 1f);
            return (float)Math.Sin(Math.PI * h) * TeleportHopHeight;
        }

        // Teleport: he sinks into the floor (squashes flat, splashes), a ring on the floor marks where he will come
        // up, then he bursts out of it with a hop, stretches on the way up and squashes when he lands.
        private void DoTeleport(NPC npc, Player player)
        {
            KingSlimeInstanceData data = npc.GetGlobalNPC<KingSlimeInstanceData>();

            npc.velocity *= 0.7f;

            ref float timer = ref npc.ai[Slot_Timer];
            int step = CurrentStep(npc);

            npc.dontTakeDamage = true;
            npc.damage = 0;

            if (step == TeleportSubmergingStep)
            {
                npc.noTileCollide = true;
                npc.alpha += 15;

                npc.scale = MathHelper.Max(BaseScale * 0.2f, npc.scale - 0.05f);

                // Flatten into the floor.
                data.SquishOffset = MathHelper.Lerp(data.SquishOffset, -0.32f, 0.3f);
                data.SquishSpringVel = 0f;

                if (timer <= 1f)
                    KingSlimeSfx.Submerge(npc.Center, false);

                for (int i = 0; i < 2; i++)
                {
                    Dust d = Dust.NewDustPerfect(
                        npc.Center + Main.rand.NextVector2Circular(npc.width * 0.5f, npc.height * 0.5f),
                        DustID.t_Slime,
                        Main.rand.NextVector2Circular(4f, 4f),
                        150,
                        Color.Cyan,
                        Main.rand.NextFloat(1.2f, 1.8f)
                    );
                    d.noGravity = true;
                }

                // Gel pooling under him as he goes down.
                EmergePuddle(npc.Bottom, MathHelper.Clamp(timer / 17f, 0f, 1f), 150f);

                if (npc.alpha >= 255)
                {
                    npc.alpha = 255;

                    if (IsAuthority)
                    {
                        float offsetX = Main.rand.NextBool()
                            ? Main.rand.NextFloat(160f, 260f)
                            : Main.rand.NextFloat(-260f, -160f);

                        float offsetY =
                            Main.rand.NextFloat(-100f, 40f);

                        Vector2 desiredCenter = player.Center + new Vector2(offsetX, offsetY);

                        // Make sure he actually land on solid ground 
                        Vector2 spot = TryFindGroundedTeleportPosition(desiredCenter, npc.height, out Vector2 groundedCenter)
                            ? groundedCenter
                            : desiredCenter;

                        // He stays hidden where he sank; the spot is just marked until he comes up.
                        SetSlamTargetPos(npc, spot);
                        npc.netUpdate = true;
                    }

                    SetStep(npc, TeleportHiddenStep);
                    timer = 0f;
                }
            }
            else if (step == TeleportHiddenStep)
            {
                npc.noGravity = true;
                npc.noTileCollide = true;
                npc.velocity = Vector2.Zero;
                npc.alpha = 255;

                Vector2 spot = SlamTargetPos(npc);
                float t = MathHelper.Clamp(timer / TeleportHiddenDuration, 0f, 1f);
                bool locked = timer >= TeleportHiddenDuration - TeleportHiddenLockTime;

                if (spot != Vector2.Zero)
                {
                    Vector2 floor = spot + new Vector2(0f, npc.height * 0.5f);

                    // The moment the spot locks, bubble a warning there so it can be heard as well as seen.
                    if (locked && timer - 1f < TeleportHiddenDuration - TeleportHiddenLockTime)
                        KingSlimeSfx.AmbushWarning(floor);

                    if (!Main.dedServ)
                    {
                        const float size = 130f;
                        Color c = locked ? Color.White : new Color(0, 190, 255);

                        BossParticleSystem.GroundMarkers.Create(new ParticleInfo(
                            position: (floor - new Vector2(0f, 3f)).ToNumerics(),
                            velocity: SystemVector2.Zero,
                            rotation: 0f,
                            scale: new SystemVector2(size, size * 0.3f),
                            color: c * (0.3f + 0.4f * t),
                            duration: 2
                        ));

                        float inner = MathHelper.Lerp(size * 0.2f, size, t);

                        BossParticleSystem.GroundMarkers.Create(new ParticleInfo(
                            position: (floor - new Vector2(0f, 3f)).ToNumerics(),
                            velocity: SystemVector2.Zero,
                            rotation: 0f,
                            scale: new SystemVector2(inner, inner * 0.3f),
                            color: c * (0.4f + 0.5f * t),
                            duration: 2
                        ));

                        // Gel bubbling up from the spot.
                        if (Main.rand.NextBool(locked ? 1 : 3))
                        {
                            Dust d = Dust.NewDustPerfect(
                                floor + new Vector2(Main.rand.NextFloat(-size * 0.4f, size * 0.4f), -2f),
                                DustID.t_Slime,
                                new Vector2(0f, -Main.rand.NextFloat(0.8f, 2.5f)),
                                120,
                                Color.Cyan,
                                Main.rand.NextFloat(1.0f, 1.5f));
                            d.noGravity = true;
                        }
                    }
                }

                if (timer >= TeleportHiddenDuration)
                {
                    if (IsAuthority && spot != Vector2.Zero)
                    {
                        npc.Center = spot;
                        npc.netUpdate = true;
                    }

                    SetSlamTargetPos(npc, Vector2.Zero);
                    npc.scale = EffectiveBaseScale(npc);

                    // Start flat, so the jump out of the floor reads as a spring.
                    data.SquishOffset = -0.35f;
                    data.SquishSpringVel = 0f;

                    KingSlimeSfx.Emerge(npc.Center, false);

                    SetStep(npc, TeleportResurfacingStep);
                    timer = 0f;
                }
            }
            else if (step == TeleportResurfacingStep)
            {
                npc.noGravity = true;
                npc.velocity = Vector2.Zero;
                npc.alpha = (int)MathHelper.Lerp(255, SlimeAlpha, MathHelper.Clamp(timer / 6f, 0f, 1f));
                npc.scale = EffectiveBaseScale(npc);

                float landAt = TeleportResurfaceDuration * TeleportLandPoint;

                if (timer < landAt)
                {
                    // Coil, then stretch tall as he leaves the floor.
                    float u = MathHelper.Clamp(timer / 6f, 0f, 1f);
                    data.SquishOffset = MathHelper.Lerp(-0.35f, 0.33f, u * u);
                    data.SquishSpringVel = 0f;
                }

                if (timer <= 8f)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        Dust d = Dust.NewDustPerfect(
                            npc.Bottom + Main.rand.NextVector2Circular(npc.width * 0.5f, 10f),
                            DustID.t_Slime,
                            new Vector2(Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-6f, -2f)),
                            100,
                            Color.Cyan,
                            Main.rand.NextFloat(1.4f, 2.0f)
                        );
                        d.noGravity = false;
                    }
                }

                EmergePuddle(npc.Bottom, MathHelper.Clamp(timer / TeleportResurfaceDuration, 0f, 1f), 170f);

                // Back down: land with a squash and let the jelly spring settle.
                if (timer >= landAt && timer - 1f < landAt)
                {
                    data.SquishOffset = -0.35f;
                    data.SquishSpringVel = 0f;
                    KingSlimeSfx.TeleportLand(npc.Center);

                    if (!Main.dedServ)
                    {
                        VisualFX.Ring(npc.Bottom - new Vector2(0f, 3f), 190f, new Color(120, 235, 255, 255), 16, 0.3f);

                        for (int i = 0; i < 10; i++)
                        {
                            Dust.NewDust(npc.position, npc.width, npc.height, DustID.t_Slime,
                                Main.rand.NextFloat(-5f, 5f), -Main.rand.NextFloat(1f, 4f), 100, Color.Cyan, 1.5f);
                        }
                    }
                }

                if (timer >= TeleportResurfaceDuration)
                {
                    npc.alpha = SlimeAlpha;
                    npc.scale = EffectiveBaseScale(npc);
                    npc.noTileCollide = false;
                    npc.noGravity = false;

                    if (IsAuthority)
                        npc.netUpdate = true;

                    if (!Main.dedServ)
                    {
                        for (int i = 0; i < 8; i++)
                        {
                            Vector2 sparkVelocity = Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(2f, 6f);

                            BossParticleSystem.EmberBursts.Create(new ParticleInfo(
                                position: npc.Center.ToNumerics(),
                                velocity: sparkVelocity.ToNumerics(),
                                rotation: 0f,
                                scale: new SystemVector2(11f, 11f),
                                color: new Color(0, 170, 255, 255),
                                duration: Main.rand.Next(18, 28)
                            ));
                        }
                    }

                    EnterCooldown(npc);
                }
            }
        }

        private void DoHugeJump(NPC npc, Player player)
        {
            npc.alpha = SlimeAlpha;
            npc.scale = EffectiveBaseScale(npc);

            KingSlimeInstanceData data = npc.GetGlobalNPC<KingSlimeInstanceData>();

            ref float timer = ref npc.ai[Slot_Timer];
            int step = CurrentStep(npc);

            if (step == HugeJumpLiftoffStep)
            {
                // Anticipation: he crouches (squashes flat, gel gathering at his base) before the launch,
                // instead of just shooting upward. Then the launch stretches him tall.
                const float crouchDuration = 22f;

                npc.noGravity = false;
                npc.noTileCollide = false;
                npc.velocity.X *= 0.6f;

                float c = MathHelper.Clamp(timer / crouchDuration, 0f, 1f);
                data.SquishOffset = MathHelper.Lerp(data.SquishOffset, -0.1f - 0.25f * c, 0.35f);
                data.SquishSpringVel = 0f;

                if (timer <= 1f)
                    KingSlimeSfx.HugeWindup(npc.Center);

                if (!Main.dedServ && Main.rand.NextBool(2))
                {
                    Dust d = Dust.NewDustPerfect(
                        npc.Bottom + new Vector2(Main.rand.NextFloat(-npc.width * 0.5f, npc.width * 0.5f), -2f),
                        DustID.t_Slime,
                        new Vector2(Main.rand.NextFloat(-1f, 1f) * (1f + c), -Main.rand.NextFloat(0.5f, 2f)),
                        120,
                        Color.Cyan,
                        Main.rand.NextFloat(1.1f, 1.6f));
                    d.noGravity = true;
                }

                if (timer >= crouchDuration)
                {
                    npc.noGravity = true;
                    npc.noTileCollide = true;

                    if (IsAuthority)
                    {
                        float xDir =
                            Math.Sign(player.Center.X - npc.Center.X);

                        if (xDir == 0)
                            xDir = 1;

                        npc.velocity =
                            new Vector2(xDir * 4f, -16f);

                        npc.netUpdate = true;
                    }

                    // Spring tall and blast gel out of the floor he just left.
                    data.SquishOffset = 0.35f;
                    data.SquishSpringVel = 0f;

                    KingSlimeSfx.HugeLiftoff(npc.Center);

                    if (!Main.dedServ)
                    {
                        VisualFX.Play(KingSlimeFx.HugeLaunch, npc.Bottom);

                        for (int i = 0; i < 18; i++)
                        {
                            Dust.NewDust(npc.position, npc.width, npc.height, DustID.t_Slime,
                                Main.rand.NextFloat(-6f, 6f), Main.rand.NextFloat(-1f, 3f), 100, Color.Cyan, 1.6f);
                        }
                    }

                    ScreenShake(npc, 5f, 10, 10f);

                    SetStep(npc, HugeJumpHoveringStep);
                    timer = 0f;
                }
            }
            else if (step == HugeJumpHoveringStep)
            {
                const float hoverDuration = 65f;

                // GO toward hovering above the player instead of snapping position coordinates.
                float targetX = player.Center.X;
                float targetY = player.Center.Y - 350f;

                npc.velocity.X = MathHelper.Lerp(
                    npc.velocity.X,
                    (targetX - npc.Center.X) * 0.1f,
                    0.2f
                );

                npc.velocity.Y = MathHelper.Lerp(
                    npc.velocity.Y,
                    (targetY - npc.Center.Y) * 0.1f,
                    0.2f
                );

                // Last stretch of the hover: gel streaks pull into him and he compresses like a spring.
                if ((int)timer == (int)(hoverDuration - 24f) && !Main.dedServ)
                    VisualFX.PlayOn(KingSlimeFx.HugeCharge, npc);

                if (timer >= hoverDuration - 24f)
                {
                    float charge = MathHelper.Clamp((timer - (hoverDuration - 24f)) / 24f, 0f, 1f);
                    data.SquishOffset = MathHelper.Lerp(data.SquishOffset, -0.05f - 0.2f * charge, 0.25f);
                    data.SquishSpringVel = 0f;
                }

                // Lock in the landing position used by the visual telegraph.
                SetSlamTargetPos(npc, new Vector2(
                    npc.Center.X,
                    player.Bottom.Y
                ));

                // Hold overhead long enough for the player to react to the incoming slam (hopefully)
                if (timer >= hoverDuration)
                {
                    if (IsAuthority)
                        npc.netUpdate = true;

                    SetStep(npc, HugeJumpSlammingStep);
                    timer = 0f;
                }
            }
            else if (step == HugeJumpSlammingStep)
            {
                npc.noTileCollide = false;
                npc.noGravity = false;

                if (timer > 5f && HasLanded(npc))
                {
                    // (the original NPCDeath1 "yunky" sound is kept as the core of this - it's layered with a low thud and a splash now)
                    KingSlimeSfx.HugeSlam(npc.Center);

                    ScreenShake(npc, 10f, 20, 16f);

                    // Hard squash on impact; the jelly spring then wobbles him back to shape.
                    data.SquishOffset = -0.35f;
                    data.SquishSpringVel = 0.04f;

                    if (!Main.dedServ)
                        VisualFX.Play(KingSlimeFx.HugeImpact, npc.Bottom);

                    for (int i = 0; i < 30; i++)
                    {
                        Dust.NewDust(npc.position, npc.width, npc.height, DustID.t_Slime, Main.rand.NextFloat(-8f, 8f), Main.rand.NextFloat(-6f, 2f), 100, Color.Cyan, 1.8f);
                    }

                    // One big expanding ring to sell the slam's impact radius.
                    if (!Main.dedServ)
                    {
                        BossParticleSystem.Shockwaves.Create(new ParticleInfo(
                            position: npc.Bottom.ToNumerics(),
                            velocity: SystemVector2.Zero,
                            rotation: 0f,
                            scale: new SystemVector2(260f, 260f),
                            color: new Color(0, 170, 255, 255),
                            duration: 24
                        ));
                    }

                    // The slam releases a spread of Spiked Slime projectiles on landing
                    // Classic: 2, Expert: 4, Master: 6.
                    if (IsAuthority)
                    {
                        int halfCount = Main.masterMode ? 3 : (Main.expertMode ? 2 : 1);

                        for (int i = -halfCount; i <= halfCount; i++)
                        {
                            if (i == 0)
                                continue;

                            Vector2 blobVelocity =
                                new Vector2(i * 3.5f, -5f);

                            Projectile.NewProjectile(
                                npc.GetSource_FromAI(),
                                npc.Bottom,
                                blobVelocity,
                                ProjectileID.SpikedSlimeSpike,
                                18,
                                1f
                            );
                        }
                    }

                    EnterCooldown(npc);
                    return;
                }

                // Stretched long while he drops so the dive reads as speed.
                data.SquishOffset = MathHelper.Lerp(data.SquishOffset, 0.35f, 0.4f);
                data.SquishSpringVel = 0f;

                npc.velocity.X = 0f;
                npc.velocity.Y = 26f;
            }
        }

        private void DoSplitAttack(NPC npc, Player player)
        {
            npc.noGravity = true;
            npc.noTileCollide = true;
            npc.velocity = Vector2.Zero;
            npc.alpha = SlimeAlpha;

            ref float timer = ref npc.ai[Slot_Timer];
            int step = CurrentStep(npc);

            if (step == SplitAttackWindupStep)
            {
                npc.scale = MathHelper.Max(SplitMinScale, npc.scale - 0.02f);

                if (timer == 0f && !Main.dedServ)
                {
                    BossParticleSystem.ChargeCores.Create(new ParticleInfo(
                        position: npc.Center.ToNumerics(),
                        velocity: SystemVector2.Zero,
                        rotation: 0f,
                        scale: new SystemVector2(46f, 46f),
                        color: new Color(0, 200, 255, 255),
                        duration: 60
                    ));
                }

                // roar right as the windup starts 
                KingSlimeInstanceData data = npc.GetGlobalNPC<KingSlimeInstanceData>();

                if (!data.SplitRoarPlayed)
                {
                    data.SplitRoarPlayed = true;

                    SoundEngine.PlaySound(
                        SoundID.Roar with { Pitch = -0.3f, Volume = 1.1f },
                        npc.Center
                    );
                }

                float windupT = MathHelper.Clamp(timer / 60f, 0f, 1f);
                int dustCount = (int)MathHelper.Lerp(2f, 14f, windupT);

                for (int i = 0; i < dustCount; i++)
                {
                    Dust d = Dust.NewDustPerfect(
                        npc.Bottom + Main.rand.NextVector2Circular(npc.width * 0.6f, 20f),
                        DustID.BlueCrystalShard,
                        new Vector2(Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-4f, -1f)),
                        100,
                        Color.Cyan,
                        Main.rand.NextFloat(1.3f, 2.1f)
                    );
                    d.noGravity = true;
                }


                if (!Main.dedServ)
                {
                    int streakCount = (int)MathHelper.Lerp(1f, 2f, windupT);

                    for (int i = 0; i < streakCount; i++)
                    {
                        Vector2 spawnOffset = Main.rand.NextVector2Circular(npc.width * 1.6f, npc.height * 1.6f);
                        Vector2 inwardVelocity = -spawnOffset * Main.rand.NextFloat(0.05f, 0.09f);

                        BossParticleSystem.SandStreaks.Create(new ParticleInfo(
                            position: (npc.Center + spawnOffset).ToNumerics(),
                            velocity: inwardVelocity.ToNumerics(),
                            rotation: 0f,
                            scale: new SystemVector2(24f, 5f),
                            color: new Color(0, 170, 255, 255),
                            duration: 12
                        ));
                    }

                    // Split-axis telegraph 
                    for (int pass = 0; pass < (IsArena(npc) ? 2 : 1); pass++)
                    {
                        bool splitVerticalTelegraph = SplitVertical(npc) ^ (pass == 1);
                        Vector2 axis = splitVerticalTelegraph ? Vector2.UnitY : Vector2.UnitX;
                        float maxReach = Main.masterMode ? 280f : (Main.expertMode ? 240f : 200f);
                        float reach = MathHelper.Lerp(40f, maxReach, windupT);
                        const float segmentSpacing = 26f;

                        // Evenly spaced dashes that extend outward as the windup builds. Respawned every tick
                        // (duration 2) so the line is steady instead of flickering at random spots.
                        foreach (float sign in SplitTelegraphSigns)
                        {
                            for (float d = segmentSpacing; d <= reach; d += segmentSpacing)
                            {
                                float fade = MathHelper.Clamp(1.2f - d / maxReach, 0.35f, 1f);

                                BossParticleSystem.TelegraphSegments.Create(new ParticleInfo(
                                    position: (npc.Center + axis * (sign * d)).ToNumerics(),
                                    velocity: SystemVector2.Zero,
                                    rotation: splitVerticalTelegraph ? MathHelper.PiOver2 : 0f,
                                    scale: new SystemVector2(24f, 8f),
                                    color: new Color(0, 180, 255, 255) * (fade * (0.4f + 0.5f * windupT)),
                                    duration: 2
                                ));
                            }
                        }
                    }

                    if (timer >= 45f)
                    {
                        float ringSize = MathHelper.Lerp(50f, 90f, (timer - 45f) / 15f);

                        BossParticleSystem.GroundMarkers.Create(new ParticleInfo(
                            position: npc.Center.ToNumerics(),
                            velocity: SystemVector2.Zero,
                            rotation: 0f,
                            scale: new SystemVector2(ringSize, ringSize),
                            color: new Color(0, 200, 255, 255) * 0.6f,
                            duration: 2
                        ));
                    }
                }

                if (timer >= 60f)
                {
                    bool splitVertical = SplitVertical(npc);

                    if (IsAuthority)
                    {
                        Vector2 splitAnchor = npc.Center + new Vector2(0f, SplitAnchorYOffset);

                        // Spawn Clone 1 (left / up)
                        NPC.NewNPC(
                            npc.GetSource_FromAI(),
                            (int)splitAnchor.X,
                            (int)splitAnchor.Y,
                            ModContent.NPCType<KingSlimeClone>(),
                            ai0: npc.whoAmI,
                            ai1: 0f,
                            ai2: splitVertical ? 1f : 0f,
                            ai3: -1f
                        );

                        // Spawn Clone 2 (right / down)
                        NPC.NewNPC(
                            npc.GetSource_FromAI(),
                            (int)splitAnchor.X,
                            (int)splitAnchor.Y,
                            ModContent.NPCType<KingSlimeClone>(),
                            ai0: npc.whoAmI,
                            ai1: 0f,
                            ai2: splitVertical ? 1f : 0f,
                            ai3: 1f
                        );

                        if (IsArena(npc))
                        {
                            foreach (float extraSide in SplitTelegraphSigns)
                            {
                                NPC.NewNPC(
                                    npc.GetSource_FromAI(),
                                    (int)splitAnchor.X,
                                    (int)splitAnchor.Y,
                                    ModContent.NPCType<KingSlimeClone>(),
                                    ai0: npc.whoAmI,
                                    ai1: 0f,
                                    ai2: splitVertical ? 0f : 1f,
                                    ai3: extraSide
                                );
                            }
                        }

                        SoundEngine.PlaySound(SoundID.Item8 with { Pitch = -0.6f, Volume = 1.3f }, npc.Center);
                    }

                    SetStep(npc, SplitAttackDivergingStep);
                    timer = 0f;

                    if (IsAuthority)
                        npc.netUpdate = true;
                }
            }
            //Clones traveling out and back 
            else if (step == SplitAttackDivergingStep)
            {
                npc.scale = SplitMinScale;

                if (Main.rand.NextBool(2))
                {
                    Dust.NewDust(npc.position, npc.width, npc.height, DustID.t_Slime, Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-2f, 2f), 150, Color.Cyan, 1.3f);
                }

                if (timer >= 70f)
                {
                    SetStep(npc, SplitAttackSettlingStep);
                    timer = 0f;

                    // Re-merge
                    if (IsAuthority)
                    {
                        npc.scale = SplitPopScale;

                        SoundEngine.PlaySound(SoundID.NPCDeath19 with { Pitch = -0.4f, Volume = 1.6f }, npc.Center);
                        ScreenShake(npc, 18f, 28, 22f);

                        for (int i = 0; i < 40; i++)
                        {
                            Dust.NewDust(npc.position, npc.width, npc.height, DustID.TintableDust, Main.rand.NextFloat(-8f, 8f), Main.rand.NextFloat(-8f, 8f), 100, Color.Cyan, 2.0f);
                        }

                        if (!Main.dedServ)
                        {
                            BossParticleSystem.Shockwaves.Create(new ParticleInfo(
                                position: npc.Center.ToNumerics(),
                                velocity: SystemVector2.Zero,
                                rotation: 0f,
                                scale: new SystemVector2(220f, 220f),
                                color: new Color(0, 170, 255, 255),
                                duration: 22
                            ));
                        }

                        SpawnMassMinions(npc, IsArena(npc) ? 2 : 0);

                        npc.netUpdate = true;
                    }
                }
            }
            else if (step == SplitAttackSettlingStep)
            {
                float settleTarget = EffectiveBaseScale(npc);
                npc.scale = MathHelper.Lerp(npc.scale, settleTarget, 0.1f);

                if (timer >= 35f)
                {
                    npc.scale = settleTarget;
                    EnterCooldown(npc);
                }
            }
        }

        private static bool HasLanded(NPC npc)
        {
            bool velocitySettled = npc.oldVelocity.Y >= 0f && Math.Abs(npc.velocity.Y) < 0.1f;

            bool groundedBelow = Collision.SolidCollision(npc.BottomLeft - Vector2.UnitY * 4f, npc.width, 8);

            return velocitySettled && groundedBelow;
        }

        private static bool TryFindGroundedTeleportPosition(Vector2 desiredCenter, int npcHeight, out Vector2 groundedCenter, int maxSearchTiles = 60)
        {
            Point tileCoords = desiredCenter.ToTileCoordinates();

            for (int y = tileCoords.Y; y < tileCoords.Y + maxSearchTiles; y++)
            {
                if (!WorldGen.InWorld(tileCoords.X, y, 5))
                    continue;

                Tile tile = Framing.GetTileSafely(tileCoords.X, y);

                bool isSolidGround = tile.HasUnactuatedTile
                    && Main.tileSolid[tile.TileType]
                    && !Main.tileSolidTop[tile.TileType];

                if (isSolidGround)
                {
                    groundedCenter = new Vector2(desiredCenter.X, y * 16f - npcHeight * 0.5f);
                    return true;
                }
            }

            groundedCenter = desiredCenter;
            return false;
        }

        private static float EaseOutBounce(float t)
        {
            const float n1 = 7.5625f;
            const float d1 = 2.75f;

            if (t < 1f / d1)
                return n1 * t * t;
            else if (t < 2f / d1)
            {
                t -= 1.5f / d1;
                return n1 * t * t + 0.75f;
            }
            else if (t < 2.5f / d1)
            {
                t -= 2.25f / d1;
                return n1 * t * t + 0.9375f;
            }
            else
            {
                t -= 2.625f / d1;
                return n1 * t * t + 0.984375f;
            }
        }

        private void SpawnMassMinions(NPC npc, int bonus = 0, Vector2? origin = null)
        {
            npc.GetGlobalNPC<KingSlimeInstanceData>().ActiveMinionCountTick = uint.MaxValue;

            int minionCount = (Main.masterMode ? 3 : (Main.expertMode ? 2 : 1)) + bonus;

            for (int i = 0; i < minionCount; i++)
            {
                Vector2 spawnOffset = Main.rand.NextVector2CircularEdge(1f, 1f) * (npc.width * 0.4f);
                Vector2 spawnPosition = (origin ?? npc.Center) + spawnOffset;

                int index = NPC.NewNPC(
                    npc.GetSource_FromAI(),
                    (int)spawnPosition.X,
                    (int)spawnPosition.Y,
                    ModContent.NPCType<KingSlimeMinion>()
                );

                if (Main.npc.IndexInRange(index) && Main.npc[index].ModNPC is KingSlimeMinion minion)
                {
                    int returnDelay = Main.rand.Next(MinionReturnDelayMin, MinionReturnDelayMax);
                    minion.Initialize(npc.whoAmI, returnDelay);
                }
            }

            for (int i = 0; i < 16; i++)
            {
                Dust d = Dust.NewDustPerfect(
                    npc.Center,
                    DustID.t_Slime,
                    Main.rand.NextVector2Circular(5f, 5f),
                    100,
                    Color.Cyan,
                    Main.rand.NextFloat(1.2f, 2f)
                );

                d.noGravity = true;
            }
        }

        private void ScreenShake(NPC npc, float strength, int frames, float vibration = 5f)
        {
            if (Main.netMode == NetmodeID.Server)
                return;

            Main.instance.CameraModifiers.Add(
                new PunchCameraModifier(
                    npc.Center,
                    Main.rand.NextVector2Unit(),
                    strength,
                    vibration,
                    frames,
                    1000f
                )
            );
        }

        public override bool PreDraw(
                    NPC npc,
                    SpriteBatch spriteBatch,
                    Vector2 screenPos,
                    Color drawColor)
        {
            Texture2D kingSlimeTexture = (kingSlimeAsset ??= ModContent.Request<Texture2D>(
                "ShatteredIllusion/Assets/ExtraTextures/Resprites/NPC_50"
            )).Value;

            var frameCount = Main.npcFrameCount[npc.type];
            var frameVertical = npc.frame.Y / npc.frame.Height;
            var frame = kingSlimeTexture.Frame(1, frameCount, 0, frameVertical);
            frame.Inflate(0, -2);

            SpriteEffects spriteEffects =
                npc.spriteDirection == 1
                    ? SpriteEffects.None
                    : SpriteEffects.FlipHorizontally;

            KingSlimeInstanceData data = npc.GetGlobalNPC<KingSlimeInstanceData>();

            if (data.CrownT > 0f && CurrentPhase(npc) != AttackPhase.Death)
                DrawArenaBorder(spriteBatch, screenPos, data);

            AttackPhase currentPhase = CurrentPhase(npc);
            int currentStep = CurrentStep(npc);
            float timer = npc.ai[Slot_Timer];

            bool isNinjaForm = currentPhase == AttackPhase.NinjaWalk
                || currentPhase == AttackPhase.NinjaStab
                || currentPhase == AttackPhase.NinjaSlash
                || currentPhase == AttackPhase.NinjaVanish
                || currentPhase == AttackPhase.NinjaCounter
                || currentPhase == AttackPhase.NinjaLeap
                || currentPhase == AttackPhase.NinjaGeyser
                || currentPhase == AttackPhase.NinjaStunned;
            bool isRising = currentPhase == AttackPhase.TransformToNinja && currentStep == TransformRiseStep;
            bool isShrinking = currentPhase == AttackPhase.TransformToNinja && currentStep == TransformShrinkStep;
            bool isSinking = currentPhase == AttackPhase.TransformToSlime && currentStep == RevertSinkStep;
            bool isGrowing = currentPhase == AttackPhase.TransformToSlime && currentStep == RevertRiseStep;

            if (currentPhase == AttackPhase.Death && DeathIsNinja(npc))
            {
                DrawNinjaDeath(npc, spriteBatch, screenPos, drawColor);
                return false;
            }

            IntroVisual intro = GetIntroVisual(npc);

            if (intro.Active && !intro.BodyVisible)
                return false;

            if (isNinjaForm || isRising || isSinking)
            {
                DrawNinja(npc, spriteBatch, screenPos, drawColor);
                return false;
            }

            float idleWobble = (float)Math.Sin(data.WobbleClock * 0.1f) * 0.02f;
            float totalSquish = data.SquishOffset + idleWobble;

            float shrinkSquish = isShrinking
                ? MathHelper.Clamp(timer / TransformShrinkDuration, 0f, 1f)
                : isGrowing ? 1f - MathHelper.Clamp(timer / TransformRiseDuration, 0f, 1f) : 0f;

            float squishY = 1f + totalSquish - shrinkSquish;
            float squishX = 1f - totalSquish * 0.6f + shrinkSquish * 0.9f;

            Vector2 jellyScale = new Vector2(npc.scale * squishX, npc.scale * squishY);

            // Teleport: the hop out of the floor is drawn as a lift (the hitbox stays on the ground).
            float teleportLift = TeleportHopLift(npc);

            // A little sway that grows with how hard he's currently jiggling (i laugh writing this)
            float wobbleRotation = (float)Math.Sin(data.WobbleClock * 0.08f) * 0.025f * (1f + Math.Abs(data.SquishOffset) * 3f);

            var bodyDraw = new DrawData(
                kingSlimeTexture,
                npc.Bottom - screenPos + new Vector2(0f, 2f + intro.BodyOffsetY - teleportLift),
                frame,
                npc.GetAlpha(drawColor),
                npc.rotation + wobbleRotation,
                frame.Size() * new Vector2(0.5f, 1f),
                jellyScale,
                spriteEffects,
                0f
            );

            if (IsWeaponFormPhase(currentPhase))
                DrawWeaponForm(npc, spriteBatch, kingSlimeTexture, frame, screenPos, drawColor, data);
            else
                bodyDraw.Draw(spriteBatch);

            Texture2D crownTexture = (crownAsset ??= ModContent.Request<Texture2D>(
                "ShatteredIllusion/Assets/ExtraTextures/Resprites/Extra_39"
            )).Value;

            var center = npc.Bottom;
            center.Y -= frame.Height * jellyScale.Y;

            var yOffset = (npc.frame.Y / npc.frame.Height) switch
            {
                0 => 2f,
                1 => -6f,
                2 => 2f,
                3 => 10f,
                4 => 2f,
                5 => 0f,
                _ => 0f,
            };

            center.Y += npc.gfxOffY + yOffset * npc.scale * squishY - teleportLift;

            if (currentPhase == AttackPhase.SplitAttack && currentStep == SplitAttackSettlingStep)
            {
                float dropT = MathHelper.Clamp(timer / CrownDropDuration, 0f, 1f);
                center.Y -= CrownDropHeight * (1f - EaseOutBounce(dropT));
            }

            float crownRotation = 0f;

            if (currentPhase != AttackPhase.Death && data.CrownT > 0f)
            {
                float ease = data.CrownT * data.CrownT * (3f - 2f * data.CrownT);
                Vector2 roofSeat = new Vector2((data.ArenaMin.X + data.ArenaMax.X) * 0.5f, data.ArenaMin.Y - 8f - crownTexture.Height * npc.scale * 0.5f);
                center = Vector2.Lerp(center, roofSeat, ease);
                crownRotation = MathHelper.TwoPi * ease;
            }

            if (currentPhase == AttackPhase.Death)
            {
                DrawDeathCrown(npc, spriteBatch, screenPos, center, spriteEffects);
                return false;
            }

            if (intro.Active)
            {
                if (!intro.CrownVisible)
                    return false;

                center.Y += intro.CrownOffsetY;
                crownRotation = intro.CrownRotation;
            }

            // The crown follows the body's fade while he teleports (otherwise it floats around with nothing under it).
            Color crownColor = currentPhase == AttackPhase.Teleport ? Color.White * npc.Opacity : Color.White;

            spriteBatch.Draw(crownTexture, center - screenPos, null, crownColor, crownRotation, crownTexture.Size() / 2f, npc.scale, spriteEffects, 0f);

            return false;
        }
    }

    // Every King Slime sound lives here so the whole fight can be mixed from one file.
    //
    // Why this exists: the old calls were single vanilla sounds at one fixed pitch, so every move sounded the same,
    // repeated hits stacked into mush, and a heavy slam sounded about as big as a tiny hop. Now:
    //   * each moment is 2-3 layers (a wet "body", a low "weight", and a "snap" on top for the important ones)
    //   * every layer has PitchVariance so repeats don't machine-gun
    //   * MaxInstances caps stacking (the Roar and Item1 swings were the worst offenders)
    //   * the telegraph for the ambush re-emerge is audible, so you can hear WHERE he is coming up
    //
    // Tuning: Volume is the main knob. Heavier = lower pitch + more "weight" layer.
    public static class KingSlimeSfx
    {
        // ---- layer helpers ----
        private static SoundStyle Wet(float pitch, float volume) =>
            SoundID.Splash with { Pitch = pitch, Volume = volume, PitchVariance = 0.12f, MaxInstances = 3, SoundLimitBehavior = SoundLimitBehavior.IgnoreNew };

        private static SoundStyle WetLight(float pitch, float volume) =>
            SoundID.SplashWeak with { Pitch = pitch, Volume = volume, PitchVariance = 0.15f, MaxInstances = 3, SoundLimitBehavior = SoundLimitBehavior.IgnoreNew };

        private static SoundStyle Squelch(float pitch, float volume) =>
            SoundID.NPCHit1 with { Pitch = pitch, Volume = volume, PitchVariance = 0.1f, MaxInstances = 3, SoundLimitBehavior = SoundLimitBehavior.IgnoreNew };

        private static SoundStyle Weight(float pitch, float volume) =>
            SoundID.Item14 with { Pitch = pitch, Volume = volume, PitchVariance = 0.05f, MaxInstances = 2, SoundLimitBehavior = SoundLimitBehavior.ReplaceOldest };

        private static SoundStyle Whoosh(float pitch, float volume) =>
            SoundID.Item1 with { Pitch = pitch, Volume = volume, PitchVariance = 0.1f, MaxInstances = 3, SoundLimitBehavior = SoundLimitBehavior.IgnoreNew };

        private static SoundStyle Shimmer(float pitch, float volume) =>
            SoundID.Item8 with { Pitch = pitch, Volume = volume, PitchVariance = 0.05f, MaxInstances = 2, SoundLimitBehavior = SoundLimitBehavior.ReplaceOldest };

        private static void Play(SoundStyle style, Vector2 pos) => SoundEngine.PlaySound(style, pos);

        // ---- slime form ----

        // Crouch before a hop / launch off the floor.
        public static void JumpLaunch(Vector2 pos, bool big)
        {
            Play(Squelch(big ? -0.5f : -0.3f, 0.8f), pos);
            Play(WetLight(big ? -0.1f : 0.2f, 0.7f), pos);
        }

        // Landing from a normal / big jump.
        public static void JumpLand(Vector2 pos, bool big)
        {
            Play(Wet(big ? -0.35f : -0.1f, big ? 0.95f : 0.75f), pos);
            Play(Weight(big ? -0.85f : -0.95f, big ? 0.45f : 0.25f), pos);
        }

        // Huge jump crouch: he squashes down and winds up before launching.
        public static void HugeWindup(Vector2 pos)
        {
            Play(Squelch(-0.7f, 0.9f), pos);
            Play(WetLight(-0.4f, 0.7f), pos);
        }

        // Huge jump takeoff.
        public static void HugeLiftoff(Vector2 pos)
        {
            Play(Squelch(-0.6f, 1f), pos);
            Play(Wet(-0.2f, 0.8f), pos);
            Play(Whoosh(-0.5f, 0.6f), pos);
        }

        // Huge jump impact: the big one.
        public static void HugeSlam(Vector2 pos)
        {
            Play(SoundID.NPCDeath1 with { Pitch = -0.7f, Volume = 1.1f, MaxInstances = 2, SoundLimitBehavior = SoundLimitBehavior.ReplaceOldest }, pos);
            Play(Weight(-0.7f, 0.9f), pos);
            Play(Wet(-0.5f, 1f), pos);
        }

        // ---- ninja: transform / revert ----

        // Slime has shrunk down to ninja size and is about to burst out.
        public static void TransformSquash(Vector2 pos)
        {
            Play(Wet(-0.3f, 0.9f), pos);
            Play(SoundID.NPCDeath39 with { Pitch = -0.2f, Volume = 0.8f, MaxInstances = 2 }, pos);
        }

        // Ninja bursts out of the goo (transform rise and the vanish re-appear). `big` = the transform, which is the showpiece.
        public static void Emerge(Vector2 pos, bool big)
        {
            // wet burst + sharp upward "pop" + a bright snap so the reveal reads even off-screen
            Play(Wet(big ? 0.0f : 0.15f, big ? 1f : 0.85f), pos);
            Play(Squelch(big ? 0.1f : 0.3f, 0.6f), pos);
            Play(Whoosh(big ? 0.3f : 0.55f, 0.55f), pos);
            Play(Shimmer(big ? 0.5f : 0.7f, big ? 0.7f : 0.45f), pos);
        }

        // Teleport: he lands back on the ground after popping out of the floor.
        public static void TeleportLand(Vector2 pos)
        {
            Play(Wet(0.0f, 0.8f), pos);
            Play(Weight(-0.9f, 0.35f), pos);
            Play(Shimmer(0.2f, 0.5f), pos);
        }

        // Ninja melts into the goo.
        public static void Submerge(Vector2 pos, bool quick)
        {
            Play(Wet(-0.25f, quick ? 0.6f : 0.8f), pos);
            Play(Shimmer(0.3f, quick ? 0.35f : 0.5f), pos);
        }

        // Ambush spot has locked: a bubbling warning at the spot he's about to come up. Audible, not just visual.
        public static void AmbushWarning(Vector2 spot)
        {
            Play(WetLight(0.45f, 0.8f), spot);
            Play(Shimmer(0.9f, 0.3f), spot);
        }

        // Slime form re-forms from the ninja (revert).
        public static void RevertGrow(Vector2 pos)
        {
            Play(Wet(-0.4f, 1f), pos);
            Play(SoundID.NPCDeath39 with { Pitch = -0.4f, Volume = 0.8f, MaxInstances = 2 }, pos);
        }

        // ---- ninja: attacks ----

        // Blade swing / dash. pitch up = faster, lighter move.
        public static void Swing(Vector2 pos, float pitch, float volume) =>
            Play(Whoosh(pitch, volume), pos);

        // Shadow dive impact.
        public static void DiveImpact(Vector2 pos)
        {
            Play(Wet(-0.4f, 0.95f), pos);
            Play(Weight(-0.75f, 0.7f), pos);
            Play(SoundID.NPCDeath39 with { Pitch = -0.3f, Volume = 0.7f, MaxInstances = 2 }, pos);
        }

        // Gel geysers erupt where you were standing.
        public static void GeyserCall(Vector2 pos)
        {
            Play(Shimmer(-0.2f, 0.8f), pos);
            Play(WetLight(-0.2f, 0.7f), pos);
        }
    }
}
