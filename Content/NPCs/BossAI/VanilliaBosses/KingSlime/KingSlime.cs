using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ShatteredIllusion.Common.Cutscenes;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace ShatteredIllusion.Content.NPCs.BossAI.VanilliaBosses.KingSlime
{
    public class KingSlimeOverride : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        private bool hasTriggeredCutscene = false;

        public enum AIState
        {
            Jump,
            BigJump,
            Teleport,
            HugeJump,
            SplitAttack,
            Cooldown
        }

        //Attack order as you know
        private static readonly AIState[] NormalAttackOrder =
        {
            AIState.Jump,
            AIState.BigJump,
            AIState.BigJump,
            AIState.Teleport,
            AIState.HugeJump,
            AIState.SplitAttack,
            AIState.BigJump,
            AIState.HugeJump,
        };


        public AIState CurrentState = AIState.Jump;
        public float Timer;
        public int SubState;
        public int AttackSequenceIndex;

        // Stores the position the Huge Jump slam is targeting so the landing point can be telegraphed.
        private Vector2 slamTargetPos = Vector2.Zero;

        // OMG THE NINJA NOT BEING VISIBLE WAS DRIVING ME CRAZY 
        private const int SlimeAlpha = 25;

        // Overall King Slime size everything below scales off of it
        public const float BaseScale = 1.5f;
        private const float SplitMinScale = BaseScale * 0.55f;
        private const float SplitPopScale = BaseScale * 1.35f;

        private const float SplitAnchorYOffset = 16f;

        private bool splitVertical;

        private const float ShockwaveTelegraphWindow = 15f;

        private static bool IsAuthority =>
            Main.netMode != NetmodeID.MultiplayerClient;

        public override bool AppliesToEntity(NPC entity, bool lateRequest)
        {
            return entity.type == NPCID.KingSlime;
        }

        public override void SendExtraAI(NPC npc, BitWriter bitWriter, BinaryWriter binaryWriter)
        {
            binaryWriter.Write((byte)CurrentState);
            binaryWriter.Write(Timer);
            binaryWriter.Write((byte)SubState);
            binaryWriter.Write((byte)AttackSequenceIndex);
            binaryWriter.Write(slamTargetPos.X);
            binaryWriter.Write(slamTargetPos.Y);
            binaryWriter.Write(splitVertical);
        }

        public override void ReceiveExtraAI(NPC npc, BitReader bitReader, BinaryReader binaryReader)
        {
            CurrentState = (AIState)binaryReader.ReadByte();
            Timer = binaryReader.ReadSingle();
            SubState = binaryReader.ReadByte();
            AttackSequenceIndex = binaryReader.ReadByte();
            slamTargetPos = new Vector2(binaryReader.ReadSingle(), binaryReader.ReadSingle());
            splitVertical = binaryReader.ReadBoolean();
        }

        public override bool PreAI(NPC npc)
        {
            if (npc.type != NPCID.KingSlime)
                return true;

            npc.aiStyle = -1;

            npc.ai[0] = 0f;
            npc.ai[1] = 0f;
            npc.ai[2] = 1f;
            npc.localAI[1] = 0f;

            if (!hasTriggeredCutscene)
            {
                hasTriggeredCutscene = true;

                BossCutsceneSystem.StartBossCutscene(
                    npc,
                    "     The Crowned Aberration     \n         --King Slime--"
                );
            }

            if (BossCutsceneSystem.IsCutsceneActive)
            {
                npc.velocity = Vector2.Zero;
                return false;
            }

            npc.TargetClosest(true);
            Player target = Main.player[npc.target];

            if (!target.active || target.dead)
            {
                npc.velocity = Vector2.Zero;
                return false;
            }

            Timer++;

            // Keep the sprite facing the direction King Slime is moving.
            if (npc.velocity.X != 0f)
            {
                npc.direction = npc.velocity.X > 0 ? 1 : -1;
                npc.spriteDirection = npc.direction;
            }

            switch (CurrentState) //testing out a new way to make this since the antlion wasnt the best 
            {
                case AIState.Jump:
                    ExecuteJump(npc, target, isBigJump: false);
                    break;

                case AIState.BigJump:
                    ExecuteJump(npc, target, isBigJump: true);
                    break;

                case AIState.Teleport:
                    ExecuteTeleport(npc, target);
                    break;

                case AIState.HugeJump:
                    ExecuteHugeJump(npc, target);
                    break;

                case AIState.SplitAttack:
                    ExecuteSplitAttack(npc, target);
                    break;

                case AIState.Cooldown:
                    ExecuteCooldown(npc);
                    break;
            }

            return false;
        }

        private void EnterCooldown()
        {
            CurrentState = AIState.Cooldown;
            Timer = 0f;
            SubState = 0;
            slamTargetPos = Vector2.Zero;
        }

        private void ExecuteCooldown(NPC npc)
        {
            npc.noGravity = false;
            npc.noTileCollide = false;
            npc.alpha = SlimeAlpha;
            npc.scale = BaseScale;
            npc.velocity.X *= 0.9f;

            const float cooldownTime = 20f;

            if (Timer >= cooldownTime)
            {
                AttackSequenceIndex = (AttackSequenceIndex + 1) % NormalAttackOrder.Length;
                CurrentState = NormalAttackOrder[AttackSequenceIndex];

                // Decide this split's orientation up front (50/50) so the windup telegraph
                // and the clones that spawn later both agree on it.
                if (CurrentState == AIState.SplitAttack && IsAuthority)
                    splitVertical = Main.rand.NextBool(2);

                Timer = 0f;
                SubState = 0;
                npc.netUpdate = true;
            }
        }

        private void ExecuteJump(NPC npc, Player player, bool isBigJump)
        {
            npc.noGravity = false;
            npc.noTileCollide = false;
            npc.alpha = SlimeAlpha;
            npc.scale = BaseScale;

            if (SubState == 0)
            {
                npc.velocity.X *= 0.8f;

                if (Main.rand.NextBool(3))
                {
                    Dust.NewDust(npc.position, npc.width, npc.height, DustID.t_Slime, 0f, 2f, 100, Color.Cyan, 1.2f);
                }

                // Give King Slime a short pause before committing to the jump
                if (Timer >= 30f)
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

                    SubState = 1;
                    Timer = 0f;
                }
            }
            else if (SubState == 1)
            {
                if (isBigJump && Main.rand.NextBool(2))
                {
                    Dust.NewDust(npc.position, npc.width, npc.height, DustID.t_Slime, npc.velocity.X * 0.3f, npc.velocity.Y * 0.3f, 150, Color.DeepSkyBlue, 1.1f);
                }

                // Wait until the jump has landed before moving to the next attack.
                if (Timer > 10f && HasLanded(npc))
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

                    EnterCooldown();
                }
            }
        }

        private void ExecuteTeleport(NPC npc, Player player)
        {
            npc.velocity *= 0.7f;

            if (SubState == 0)
            {
                npc.noTileCollide = true;
                npc.alpha += 15;

                // Shrink in on the teleport vanish
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

                    SubState = 1;
                    Timer = 0f;
                }
            }
            else if (SubState == 1)
            {
                npc.alpha -= 20;

                // Extend back out to normal size (and slightly overshoot TO MAKE HIM LOOK TUFF) as he reappears
                float targetMaterializeScale = BaseScale;
                npc.scale = MathHelper.Lerp(npc.scale, targetMaterializeScale, 0.15f);

                if (Timer <= 12f)
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

                    EnterCooldown();
                }
            }
        }

        private void ExecuteHugeJump(NPC npc, Player player)
        {
            npc.alpha = SlimeAlpha;
            npc.scale = BaseScale;

            if (SubState == 0)
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

                SubState = 1;
                Timer = 0f;
            }
            else if (SubState == 1)
            {
                if (Timer % 4f == 0f)
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
                slamTargetPos = new Vector2(
                    npc.Center.X,
                    player.Bottom.Y
                );

                // Hold overhead long enough for the player to react to the incoming slam (hopefully)
                if (Timer >= 65f)
                {
                    if (IsAuthority)
                        npc.netUpdate = true;

                    SubState = 2;
                    Timer = 0f;
                }
            }
            else if (SubState == 2)
            {
                npc.noTileCollide = false;
                npc.noGravity = false;

                if (Timer > 5f && HasLanded(npc))
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

                    // The slam releases a spread of Spiked Slime projectiles on landing
                    // Classic: 2, Expert: 4, Master: 6.
                    if (Main.netMode != NetmodeID.MultiplayerClient)
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

                    EnterCooldown();
                    return;
                }

                npc.velocity.X = 0f;
                npc.velocity.Y = 26f;
            }
        }

        private void ExecuteSplitAttack(NPC npc, Player player)
        {
            npc.noGravity = true;
            npc.noTileCollide = true;
            npc.velocity = Vector2.Zero;
            npc.alpha = SlimeAlpha;

            if (SubState == 0)
            {
                npc.scale = MathHelper.Max(SplitMinScale, npc.scale - 0.02f);

                // roar right as the windup starts 
                {
                    SoundEngine.PlaySound(
                        SoundID.Roar with { Pitch = -0.3f, Volume = 1.1f },
                        npc.Center
                    );
                }

                float windupT = MathHelper.Clamp(Timer / 60f, 0f, 1f);
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

                if (Timer >= 60f)
                {
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

                    SubState = 1;
                    Timer = 0f;
                    npc.netUpdate = true;
                }
            }
            //Clones traveling out and back 
            else if (SubState == 1)
            {
                npc.scale = SplitMinScale;

                if (Main.rand.NextBool(2))
                {
                    Dust.NewDust(npc.position, npc.width, npc.height, DustID.t_Slime, Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-2f, 2f), 150, Color.Cyan, 1.3f);
                }

                // Telegraph the incoming shockwave
                float framesUntilFire = 70f - Timer;
                if (framesUntilFire <= ShockwaveTelegraphWindow && framesUntilFire >= 0f)
                {
                    float warnT = 1f - (framesUntilFire / ShockwaveTelegraphWindow); // 0 -> 1 as fire approaches
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

                if (Timer >= 70f)
                {
                    SubState = 2;
                    Timer = 0f;

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

                        // shockwave projectiles
                        float speed = 9.5f;
                        Vector2[] xDirections = {
                            new Vector2(1, 1),   // Down-Right
                            new Vector2(-1, 1),  // Down-Left
                            new Vector2(1, -1),  // Up-Right
                            new Vector2(-1, -1)  // Up-Left
                        };

                        foreach (var dir in xDirections)
                        {
                            Projectile.NewProjectile(
                                npc.GetSource_FromAI(),
                                npc.Center,
                                dir * speed,
                                ModContent.ProjectileType<SlimyShockwave>(),
                                npc.damage,
                                2f,
                                Main.myPlayer
                            );
                        }
                        npc.netUpdate = true;
                    }
                }
            }
            else if (SubState == 2)
            {
                npc.scale = MathHelper.Lerp(npc.scale, BaseScale, 0.1f);

                if (Timer >= 35f)
                {
                    npc.scale = BaseScale;
                    EnterCooldown();
                }
            }
        }

        private static bool HasLanded(NPC npc)
        {
            return npc.oldVelocity.Y >= 0f &&
                   Math.Abs(npc.velocity.Y) < 0.1f;
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
            if (npc.type != NPCID.KingSlime)
                return true;

            // Draw the custom King Slime sprite sheet from Assets/ExtraTextures.
            Texture2D kingSlimeTexture = ModContent.Request<Texture2D>(
                "ShatteredIllusion/Assets/ExtraTextures/Resprites/NPC_50"
            ).Value;

            // Fargo-style frame handling.
            var frameCount = Main.npcFrameCount[npc.type];
            var frameVertical = npc.frame.Y / npc.frame.Height;
            var frame = kingSlimeTexture.Frame(1, frameCount, 0, frameVertical);
            frame.Inflate(0, -2);

            SpriteEffects spriteEffects =
                npc.spriteDirection == 1
                    ? SpriteEffects.None
                    : SpriteEffects.FlipHorizontally;

            // Draw the body from the bottom like Fargo does.
            var bodyDraw = new DrawData(
                kingSlimeTexture,
                npc.Bottom - screenPos + new Vector2(0f, 2f),
                frame,
                npc.GetAlpha(drawColor),
                npc.rotation,
                frame.Size() * new Vector2(0.5f, 1f),
                npc.scale,
                spriteEffects,
                0f
            );

            bodyDraw.Draw(spriteBatch);

            // Draw the Huge Jump landing indicator while King Slime is hovering overhead.
            if (CurrentState == AIState.HugeJump &&
                SubState == 1 &&
                slamTargetPos != Vector2.Zero)
            {
                Texture2D telegraphTex = ModContent.Request<Texture2D>(
                    "ShatteredIllusion/Content/NPCs/BossAI/VanilliaBosses/KingSlime/HugeJumpTelegraph"
                ).Value;

                float startYOffset = 40f; // how much higher to start the beam
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
            if (CurrentState == AIState.SplitAttack && SubState == 0)
            {
                Texture2D telegraphTex = ModContent.Request<Texture2D>(
                    "ShatteredIllusion/Content/NPCs/BossAI/VanilliaBosses/KingSlime/HugeJumpTelegraph"
                ).Value;

                Vector2 drawPos = npc.Center - screenPos;
                float maxSpanLength = 240f;

                float scaleX = 20f / telegraphTex.Height;
                float scaleY = maxSpanLength / telegraphTex.Width;
                Vector2 origin = new Vector2(0, telegraphTex.Height / 2f);

                Color warningTint = new Color(0, 180, 255, 200) * (Timer / 60f);

                // Rotations for a +/- pair along whichever axis this split is using.
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

            // Render the crown normally without the shader.
            Texture2D crownTexture = ModContent.Request<Texture2D>(
                "ShatteredIllusion/Assets/ExtraTextures/Resprites/Extra_39"
            ).Value;
            var center = npc.Center;

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

            center.Y += npc.gfxOffY - (70f - yOffset) * npc.scale;
            spriteBatch.Draw(crownTexture, center - screenPos, null, Color.White, 0f, crownTexture.Size() / 2f, npc.scale, spriteEffects, 0f);

            return false;
        }
    }
}