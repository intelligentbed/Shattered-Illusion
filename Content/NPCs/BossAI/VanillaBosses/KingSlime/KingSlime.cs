using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ShatteredIllusion.Common.Cutscenes;
using ShatteredIllusion.Core.OverrideSystem;
using ShatteredIllusion.Content.Particles;
using ParticleLibrary.Core.V3.Particles;
using ParticleLibrary.Utilities;
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
    public class KingSlimeOverride : NPCBehaviorOverride
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
            Despawn
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
        };

        private const float ForceDespawnRange = 3000f;
        private const int ForceDespawnDelay = 60;
        private const float DespawnFadeDuration = 40f;

        private int spawnGraceTimer = 0;
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

        private const float ShockwaveTelegraphWindow = 15f;

        private float squishOffset = 0f;
        private float squishSpringVel = 0f;
        private float lastVelocityY = 0f;
        private float wobbleClock = 0f;

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

        // Named sub-steps within each attack phase, replacing the old raw 0/1/2 SubState values.
        private const int JumpWindupStep = 0;
        private const int JumpAirborneStep = 1;

        private const int TeleportSubmergingStep = 0;
        private const int TeleportResurfacingStep = 1;

        private const int HugeJumpLiftoffStep = 0;
        private const int HugeJumpHoveringStep = 1;
        private const int HugeJumpSlammingStep = 2;

        private const int SplitAttackWindupStep = 0;
        private const int SplitAttackDivergingStep = 1;
        private const int SplitAttackSettlingStep = 2;

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

            if (cutscenePlayer.IsCutsceneActive)
            {
                npc.velocity = Vector2.Zero;
                return false;
            }

            npc.TargetClosest(true);
            Player target = Main.player[npc.target];

            ref float forceDespawnTimer = ref npc.localAI[Local_ForceDespawnTimer];

            if (spawnGraceTimer < SpawnGracePeriod)
            {
                spawnGraceTimer++;
            }
            else if (!npc.HasValidTarget || npc.Distance(target.Center) > ForceDespawnRange)
            {
                if (++forceDespawnTimer > ForceDespawnDelay && CurrentPhase(npc) != AttackPhase.Despawn && IsAuthority)
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

            npc.ai[Slot_Timer]++;
            UpdateJellyWobble(npc);

            // Keep the sprite facing the direction King Slime is moving.
            if (npc.velocity.X != 0f)
            {
                npc.direction = npc.velocity.X > 0 ? 1 : -1;
                npc.spriteDirection = npc.direction;
            }

            switch (CurrentPhase(npc)) //testing out a new way to make this since the antlion wasnt the best 
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
            }

            return false;
        }

        private void UpdateJellyWobble(NPC npc)
        {
            wobbleClock += 1f;

            float velocityDeltaY = npc.velocity.Y - lastVelocityY;
            lastVelocityY = npc.velocity.Y;

            if (Math.Abs(velocityDeltaY) > 2f)
            {
                squishSpringVel -= velocityDeltaY * 0.025f;
            }

            squishSpringVel += -squishOffset * 0.35f;
            squishSpringVel *= 0.72f;
            squishOffset += squishSpringVel;
            squishOffset = MathHelper.Clamp(squishOffset, -0.35f, 0.35f);
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
            npc.scale = BaseScale;
            npc.velocity.X *= 0.9f;

            const float cooldownTime = 20f;

            ref float timer = ref npc.ai[Slot_Timer];

            if (timer >= cooldownTime)
            {
                int nextSequenceIndex = (SequenceIndex(npc) + 1) % AttackSequence.Length;
                npc.ai[Slot_SequenceIndex] = nextSequenceIndex;

                AttackPhase nextPhase = AttackSequence[nextSequenceIndex];
                SetPhase(npc, nextPhase);

                // Decide this split's orientation
                if (nextPhase == AttackPhase.SplitAttack && IsAuthority)
                    SetSplitVertical(npc, Main.rand.NextBool(2));

                timer = 0f;
                SetStep(npc, 0);
                npc.netUpdate = true;
            }
        }

        private void EnterDespawn(NPC npc)
        {
            SetPhase(npc, AttackPhase.Despawn);
            SetStep(npc, 0);
            npc.ai[Slot_Timer] = 0f;
            SetSlamTargetPos(npc, Vector2.Zero);
            npc.netUpdate = true;
        }

        private void DoDespawn(NPC npc)
        {
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
            npc.scale = BaseScale;

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

                    SoundEngine.PlaySound(
                        SoundID.NPCHit1 with { Pitch = -0.4f },
                        npc.Center
                    );

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

                    SoundEngine.PlaySound(
                        SoundID.Item1 with
                        {
                            Pitch = -0.3f,
                            Volume = 0.8f
                        },
                        npc.Center
                    );

                    for (int i = 0; i < 10; i++)
                    {
                        Dust.NewDust(npc.position, npc.width, npc.height, DustID.BlueCrystalShard, Main.rand.NextFloat(-4f, 4f), Main.rand.NextFloat(-3f, 0f), 100, Color.Cyan, 1.4f);
                    }

                    // Sharper embers on top of the dust to punch up the landing impact.
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
                                color: new Color(120, 210, 255, 0),
                                duration: Main.rand.Next(20, 32)
                            ));
                        }
                    }

                    EnterCooldown(npc);
                }
            }
        }

        private void DoTeleport(NPC npc, Player player)
        {
            npc.velocity *= 0.7f;

            ref float timer = ref npc.ai[Slot_Timer];
            int step = CurrentStep(npc);

            if (step == TeleportSubmergingStep)
            {
                npc.noTileCollide = true;
                npc.alpha += 15;

                npc.scale = MathHelper.Max(BaseScale * 0.2f, npc.scale - 0.05f);

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

                        npc.Center =
                            player.Center + new Vector2(offsetX, offsetY);

                        npc.netUpdate = true;
                    }

                    SetStep(npc, TeleportResurfacingStep);
                    timer = 0f;
                }
            }
            else if (step == TeleportResurfacingStep)
            {
                npc.alpha -= 20;

                // Extend back out to normal size (and slightly overshoot TO MAKE HIM LOOK TUFF) as he reappears
                float targetMaterializeScale = BaseScale;
                npc.scale = MathHelper.Lerp(npc.scale, targetMaterializeScale, 0.15f);

                if (timer <= 12f)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        Dust d = Dust.NewDustPerfect(
                            npc.Bottom + Main.rand.NextVector2Circular(npc.width * 0.5f, 25f),
                            DustID.BlueCrystalShard,
                            new Vector2(Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-5f, -1f)),
                            100,
                            Color.DeepSkyBlue,
                            Main.rand.NextFloat(1.4f, 2.0f)
                        );
                        d.noGravity = true;
                    }
                }

                if (npc.alpha <= SlimeAlpha)
                {
                    npc.alpha = SlimeAlpha;
                    npc.scale = BaseScale;
                    npc.noTileCollide = false;

                    if (IsAuthority)
                        npc.netUpdate = true;

                    SoundEngine.PlaySound(
                        SoundID.Item8 with { Pitch = -0.2f },
                        npc.Center
                    );

                    // A burst of embers to sell the "snapping back into existence" moment.
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
                                color: new Color(0, 170, 255, 0),
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
            npc.scale = BaseScale;

            ref float timer = ref npc.ai[Slot_Timer];
            int step = CurrentStep(npc);

            if (step == HugeJumpLiftoffStep)
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

                SoundEngine.PlaySound(
                    SoundID.NPCHit19 with { Pitch = -0.5f },
                    npc.Center
                );

                SetStep(npc, HugeJumpHoveringStep);
                timer = 0f;
            }
            else if (step == HugeJumpHoveringStep)
            {
                if (timer % 4f == 0f)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        Dust d = Dust.NewDustPerfect(
                            npc.Center +
                            Main.rand.NextVector2Circular(50f, 50f),
                            DustID.BlueCrystalShard,
                            Velocity: -npc.velocity * 0.15f + Main.rand.NextVector2Circular(2f, 2f),
                            Scale: 1.5f
                        );

                        d.noGravity = true;
                    }
                }

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

                // Lock in the landing position used by the visual telegraph.
                SetSlamTargetPos(npc, new Vector2(
                    npc.Center.X,
                    player.Bottom.Y
                ));

                if (!Main.dedServ && timer % 6f == 0f)
                {
                    Vector2 slamTargetPos = SlamTargetPos(npc);
                    Vector2 beamStart = npc.Bottom - new Vector2(0f, 35f);
                    float beamLength = slamTargetPos.Y - beamStart.Y;

                    if (beamLength > 0f)
                    {
                        for (int i = 0; i < 2; i++)
                        {
                            Vector2 segmentPos = beamStart + new Vector2(0f, beamLength * Main.rand.NextFloat());

                            BossParticleSystem.TelegraphSegments.Create(new ParticleInfo(
                                position: segmentPos.ToNumerics(),
                                velocity: SystemVector2.Zero,
                                rotation: 0f,
                                scale: new SystemVector2(24f, 8f),
                                color: new Color(0, 170, 255, 0),
                                duration: 18
                            ));
                        }
                    }
                }

                // Hold overhead long enough for the player to react to the incoming slam (hopefully)
                if (timer >= 65f)
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
                    // yo this sound sounds so yunky i love it
                    SoundEngine.PlaySound(
                        SoundID.NPCDeath1 with
                        {
                            Pitch = -0.7f,
                            Volume = 1.2f
                        },
                        npc.Center
                    );

                    ScreenShake(npc, 10f, 20, 16f);

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
                            color: new Color(0, 170, 255, 0),
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

                // roar right as the windup starts 
                {
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
                    int streakCount = (int)MathHelper.Lerp(1f, 3f, windupT);

                    for (int i = 0; i < streakCount; i++)
                    {
                        Vector2 spawnOffset = Main.rand.NextVector2Circular(npc.width * 0.9f, npc.height * 0.9f);
                        Vector2 inwardVelocity = -spawnOffset * Main.rand.NextFloat(0.05f, 0.09f);

                        BossParticleSystem.SandStreaks.Create(new ParticleInfo(
                            position: (npc.Center + spawnOffset).ToNumerics(),
                            velocity: inwardVelocity.ToNumerics(),
                            rotation: 0f,
                            scale: new SystemVector2(24f, 5f),
                            color: new Color(0, 170, 255, 0),
                            duration: 20
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

                        SoundEngine.PlaySound(SoundID.Item8 with { Pitch = -0.6f, Volume = 1.3f }, npc.Center);
                    }

                    SetStep(npc, SplitAttackDivergingStep);
                    timer = 0f;
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

                // Telegraph the incoming shockwave
                float framesUntilFire = 70f - timer;
                if (framesUntilFire <= ShockwaveTelegraphWindow && framesUntilFire >= 0f)
                {
                    float warnT = 1f - (framesUntilFire / ShockwaveTelegraphWindow);
                    Vector2[] telegraphDirections =
                    {
                        new Vector2(1, 1),
                        new Vector2(-1, 1),
                        new Vector2(1, -1),
                        new Vector2(-1, -1)
                    };

                    foreach (var dir in telegraphDirections)
                    {
                        Vector2 unitDir = Vector2.Normalize(dir);
                        float reach = MathHelper.Lerp(20f, 70f, warnT);

                        Dust d = Dust.NewDustPerfect(
                            npc.Center + unitDir * reach,
                            DustID.BlueCrystalShard,
                            unitDir * 2f,
                            100,
                            Color.Cyan,
                            MathHelper.Lerp(0.7f, 1.3f, warnT)
                        );
                        d.noGravity = true;
                    }
                }

                if (timer >= 70f)
                {
                    SetStep(npc, SplitAttackSettlingStep);
                    timer = 0f;

                    // Re-merge and fire shockwave
                    if (IsAuthority)
                    {
                        npc.scale = SplitPopScale;

                        SoundEngine.PlaySound(SoundID.NPCDeath19 with { Pitch = -0.4f, Volume = 1.6f }, npc.Center);
                        ScreenShake(npc, 18f, 28, 22f);

                        for (int i = 0; i < 40; i++)
                        {
                            Dust.NewDust(npc.position, npc.width, npc.height, DustID.TintableDust, Main.rand.NextFloat(-8f, 8f), Main.rand.NextFloat(-8f, 8f), 100, Color.Cyan, 2.0f);
                        }

                        // One big expanding ring to sell the re-merge before the shockwave fires.
                        if (!Main.dedServ)
                        {
                            BossParticleSystem.Shockwaves.Create(new ParticleInfo(
                                position: npc.Center.ToNumerics(),
                                velocity: SystemVector2.Zero,
                                rotation: 0f,
                                scale: new SystemVector2(220f, 220f),
                                color: new Color(0, 170, 255, 0),
                                duration: 22
                            ));
                        }


                        float speed = 9.5f;
                        Vector2[] xDirections = {
                            new Vector2(1, 1),   // Down-Right
                            new Vector2(-1, 1),  // Down-Left
                            new Vector2(1, -1),  // Up-Right
                            new Vector2(-1, -1)  // Up-Left
                        };

                        const float secondPulseDelay = 16f; // frames before the counter spin pulse launches
                        const float secondPulseRotation = MathHelper.PiOver4 * 0.5f; // 22.5 degrees

                        foreach (var dir in xDirections)
                        {
                            Vector2 unitDir = Vector2.Normalize(dir);

                            // First pulse - spins clockwise
                            Projectile.NewProjectile(
                                npc.GetSource_FromAI(),
                                npc.Center,
                                unitDir * speed,
                                ModContent.ProjectileType<SlimyShockwave>(),
                                npc.damage,
                                2f,
                                Main.myPlayer,
                                1f
                            );

                            // Second pulse 
                            Vector2 secondDir = unitDir.RotatedBy(secondPulseRotation);
                            Projectile.NewProjectile(
                                npc.GetSource_FromAI(),
                                npc.Center,
                                secondDir * speed,
                                ModContent.ProjectileType<SlimyShockwave>(),
                                npc.damage,
                                2f,
                                Main.myPlayer,
                                -1f,
                                secondPulseDelay
                            );
                        }
                        npc.netUpdate = true;
                    }
                }
            }
            else if (step == SplitAttackSettlingStep)
            {
                npc.scale = MathHelper.Lerp(npc.scale, BaseScale, 0.1f);

                if (timer >= 35f)
                {
                    npc.scale = BaseScale;
                    EnterCooldown(npc);
                }
            }
        }

        private static bool HasLanded(NPC npc)
        {
            return npc.oldVelocity.Y >= 0f &&
                   Math.Abs(npc.velocity.Y) < 0.1f;
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
            Texture2D kingSlimeTexture = ModContent.Request<Texture2D>(
                "ShatteredIllusion/Assets/ExtraTextures/Resprites/NPC_50"
            ).Value;

            var frameCount = Main.npcFrameCount[npc.type];
            var frameVertical = npc.frame.Y / npc.frame.Height;
            var frame = kingSlimeTexture.Frame(1, frameCount, 0, frameVertical);
            frame.Inflate(0, -2);

            SpriteEffects spriteEffects =
                npc.spriteDirection == 1
                    ? SpriteEffects.None
                    : SpriteEffects.FlipHorizontally;

            float idleWobble = (float)Math.Sin(wobbleClock * 0.1f) * 0.02f;
            float totalSquish = squishOffset + idleWobble;

            float squishY = 1f + totalSquish;
            float squishX = 1f - totalSquish * 0.6f;

            Vector2 jellyScale = new Vector2(npc.scale * squishX, npc.scale * squishY);

            // A little sway that grows with how hard he's currently jiggling (i laugh writing this)
            float wobbleRotation = (float)Math.Sin(wobbleClock * 0.08f) * 0.025f * (1f + Math.Abs(squishOffset) * 3f);

            var bodyDraw = new DrawData(
                kingSlimeTexture,
                npc.Bottom - screenPos + new Vector2(0f, 2f),
                frame,
                npc.GetAlpha(drawColor),
                npc.rotation + wobbleRotation,
                frame.Size() * new Vector2(0.5f, 1f),
                jellyScale,
                spriteEffects,
                0f
            );

            bodyDraw.Draw(spriteBatch);

            AttackPhase currentPhase = CurrentPhase(npc);
            int currentStep = CurrentStep(npc);
            Vector2 slamTargetPos = SlamTargetPos(npc);
            float timer = npc.ai[Slot_Timer];

            // Draw the Huge Jump landing indicator while King Slime is hovering overhead.
            if (currentPhase == AttackPhase.HugeJump &&
                currentStep == HugeJumpHoveringStep &&
                slamTargetPos != Vector2.Zero)
            {
                Texture2D telegraphTex = ModContent.Request<Texture2D>(
                    "ShatteredIllusion/Content/NPCs/BossAI/VanillaBosses/KingSlime/HugeJumpTelegraph"
                ).Value;

                float startYOffset = 35f; // how much higher to start the beam
                Vector2 beamStart = npc.Bottom - new Vector2(0f, startYOffset);

                Vector2 drawPos = beamStart - screenPos;
                float beamLength =
                    slamTargetPos.Y - beamStart.Y;

                if (beamLength > 0)
                {
                    float scaleX =
                        24f / telegraphTex.Height;

                    float scaleY =
                        beamLength / telegraphTex.Width;

                    Vector2 origin =
                        new Vector2(
                            0,
                            telegraphTex.Height / 2f
                        );

                    float rotation =
                        MathHelper.PiOver2;

                    Color blueTint =
                        new Color(
                            0,
                            150,
                            255,
                            200
                        );

                    spriteBatch.Draw(
                        telegraphTex,
                        drawPos,
                        null,
                        blueTint,
                        rotation,
                        origin,
                        new Vector2(scaleY, scaleX),
                        SpriteEffects.None,
                        0f
                    );
                }
            }

            // the split telegraph 
            if (currentPhase == AttackPhase.SplitAttack && currentStep == SplitAttackWindupStep)
            {
                Texture2D telegraphTex = ModContent.Request<Texture2D>(
                    "ShatteredIllusion/Content/NPCs/BossAI/VanillaBosses/KingSlime/HugeJumpTelegraph"
                ).Value;

                Vector2 drawPos = npc.Center - screenPos;

                float maxSpanLength = Main.masterMode ? 280f : (Main.expertMode ? 240f : 200f);

                float scaleX = 20f / telegraphTex.Height;
                float scaleY = maxSpanLength / telegraphTex.Width;
                Vector2 origin = new Vector2(0, telegraphTex.Height / 2f);

                Color warningTint = new Color(0, 180, 255, 200) * (timer / 60f);

                // Rotations for a +/- pair along whichever axis this split is using.
                bool splitVertical = SplitVertical(npc);
                float rotationA = splitVertical ? -MathHelper.PiOver2 : 0f;
                float rotationB = splitVertical ? MathHelper.PiOver2 : MathHelper.Pi;

                // First beam (right / up)
                spriteBatch.Draw(
                    telegraphTex,
                    drawPos,
                    null,
                    warningTint,
                    rotationA,
                    origin,
                    new Vector2(scaleY, scaleX),
                    SpriteEffects.None,
                    0f
                );

                // Second beam (left / down)
                spriteBatch.Draw(
                    telegraphTex,
                    drawPos,
                    null,
                    warningTint,
                    rotationB,
                    origin,
                    new Vector2(scaleY, scaleX),
                    SpriteEffects.None,
                    0f
                );
            }

            Texture2D crownTexture = ModContent.Request<Texture2D>(
                "ShatteredIllusion/Assets/ExtraTextures/Resprites/Extra_39"
            ).Value;

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

            center.Y += npc.gfxOffY + yOffset * npc.scale * squishY;

            if (currentPhase == AttackPhase.SplitAttack && currentStep == SplitAttackSettlingStep)
            {
                float dropT = MathHelper.Clamp(timer / CrownDropDuration, 0f, 1f);
                center.Y -= CrownDropHeight * (1f - EaseOutBounce(dropT));
            }

            spriteBatch.Draw(crownTexture, center - screenPos, null, Color.White, 0f, crownTexture.Size() / 2f, npc.scale, spriteEffects, 0f);

            return false;
        }
    }
}