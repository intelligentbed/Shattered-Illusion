using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;
using ShatteredIllusion.Common.Cutscenes;

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
            HugeJump
        }

        // sorry guys but i dont have an attack order joke this time
        private static readonly AIState[] NormalAttackOrder =
        {
            AIState.Jump,
            AIState.BigJump,
            AIState.BigJump,
            AIState.Teleport,
            AIState.HugeJump,
            AIState.BigJump
        };

        public int AttackIndex = 0;
        public int StateTimer = 0;
        public int SubState = 0;

        // Stores the position the Huge Jump slam is targeting so the landing point can be telegraphed.
        private Vector2 slamTargetPos = Vector2.Zero;

        //OMG THE NINJA NOT BEING VISIBLE WAS DIVING ME CRAZY 
        private const int SlimeAlpha = 25;

        public override bool AppliesToEntity(NPC entity, bool lateRequest)
        {
            return entity.type == NPCID.KingSlime;
        }

        public override bool PreAI(NPC npc)
        {
            if (npc.type != NPCID.KingSlime)
                return true;

            // Disable vanilla King Slime AI.
            npc.aiStyle = -1;

            npc.ai[0] = 0f;
            npc.ai[1] = 0f;
            npc.ai[2] = 1f;
            npc.localAI[1] = 0f;
            npc.scale = 1.15f;

            if (!hasTriggeredCutscene)
            {
                hasTriggeredCutscene = true;

                BossCutsceneSystem.StartBossCutscene(
                    npc,
                    "      The Crowned Aberration      \n         --King Slime--"
                );
            }

            if (BossCutsceneSystem.IsCutsceneActive)
            {
                npc.velocity = Vector2.Zero;
                return false;
            }

            npc.TargetClosest(true);
            Player player = Main.player[npc.target];

            if (!player.active || player.dead)
            {
                npc.velocity = Vector2.Zero;
                return false;
            }

            StateTimer++;

            // Keep the sprite facing the direction King Slime is moving.
            if (npc.velocity.X != 0f)
            {
                npc.direction = npc.velocity.X > 0 ? 1 : -1;
                npc.spriteDirection = npc.direction;
            }

            AIState currentAttack =
                NormalAttackOrder[AttackIndex % NormalAttackOrder.Length];

            switch (currentAttack)
            {
                case AIState.Jump:
                    ExecuteJump(npc, player, isBigJump: false);
                    break;

                case AIState.BigJump:
                    ExecuteJump(npc, player, isBigJump: true);
                    break;

                case AIState.Teleport:
                    ExecuteTeleport(npc, player);
                    break;

                case AIState.HugeJump:
                    ExecuteHugeJump(npc, player);
                    break;
            }

            return false;
        }

        // Advances to the next attack and resets all state belonging to the previous attack.
        private void NextAttack()
        {
            AttackIndex++;
            StateTimer = 0;
            SubState = 0;
            slamTargetPos = Vector2.Zero;
        }

        private static bool IsAuthority =>
            Main.netMode != NetmodeID.MultiplayerClient;

        private void ExecuteJump(NPC npc, Player player, bool isBigJump)
        {
            npc.noGravity = false;
            npc.noTileCollide = false;
            npc.alpha = SlimeAlpha; // Maintain subtle transparency for ninja visibility

            if (SubState == 0)
            {
                npc.velocity.X *= 0.8f;

                // Give King Slime a short pause before committing to the jump.
                if (StateTimer >= 30)
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

                        for (int i = 0; i < (isBigJump ? 10 : 5); i++)
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
                                1f
                            );
                        }
                    }

                    SoundEngine.PlaySound(
                        SoundID.NPCHit1 with { Pitch = -0.4f },
                        npc.Center
                    );

                    SubState = 1;
                    StateTimer = 0;
                }
            }
            else if (SubState == 1)
            {
                // Wait until the jump has landed before moving to the next attack.
                if (StateTimer > 10 && HasLanded(npc))
                {
                    // Using SoundID.Item1 (slimy splash/thud) for landing
                    SoundEngine.PlaySound(
                        SoundID.Item1 with
                        {
                            Pitch = -0.3f,
                            Volume = 0.8f
                        },
                        npc.Center
                    );

                    NextAttack();
                }
            }
        }

        private void ExecuteTeleport(NPC npc, Player player)
        {
            npc.velocity *= 0.7f;

            if (SubState == 0)
            {
                npc.noTileCollide = true;

                // Fade completely out smoothly.
                npc.alpha += 15;

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
                    StateTimer = 0;
                }
            }
            else if (SubState == 1)
            {
                // Fade back in down to our preferred translucent alpha instead of 0
                npc.alpha -= 20;

                if (npc.alpha <= SlimeAlpha)
                {
                    npc.alpha = SlimeAlpha;
                    npc.noTileCollide = false;

                    if (IsAuthority)
                        npc.netUpdate = true;

                    // Standard magic re-appearance sound
                    SoundEngine.PlaySound(
                        SoundID.Item8 with { Pitch = -0.2f },
                        npc.Center
                    );

                    NextAttack();
                }
            }
        }

        private void ExecuteHugeJump(NPC npc, Player player)
        {
            npc.alpha = SlimeAlpha; // Maintain subtle transparency for ninja visibility

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
                StateTimer = 0;
            }
            else if (SubState == 1)
            {
                if (StateTimer % 6 == 0)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        Dust d = Dust.NewDustPerfect(
                            npc.Center +
                            Main.rand.NextVector2Circular(40f, 40f),
                            DustID.BlueCrystalShard,
                            Velocity: -npc.velocity * 0.1f,
                            Scale: 1.3f
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

                // Hold overhead long enough for the player to react to the incoming slam.
                if (StateTimer >= 65)
                {
                    if (IsAuthority)
                        npc.netUpdate = true;

                    SubState = 2;
                    StateTimer = 0;
                }
            }
            else if (SubState == 2)
            {
                npc.noTileCollide = false;
                npc.noGravity = false;

                // Check landing using the velocity the engine already resolved from LAST
                if (StateTimer > 5 && HasLanded(npc))
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

                    // Camera screen shake on hard impact.
                    if (Main.netMode != NetmodeID.Server)
                    {
                        Main.instance.CameraModifiers.Add(
                            new PunchCameraModifier(
                                npc.Center,
                                Main.rand.NextVector2Unit(),
                                10f,
                                16f,
                                20,
                                1000f
                            )
                        );
                    }

                    // The slam releases a small spread of Spiked Slime projectiles on impact.
                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        for (int i = -2; i <= 2; i++)
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

                    NextAttack();
                    return;
                }

                npc.velocity.X = 0f;
                npc.velocity.Y = 26f;
            }
        }

        private static bool HasLanded(NPC npc)
        {
            return npc.oldVelocity.Y >= 0f &&
                   Math.Abs(npc.velocity.Y) < 0.1f;
        }

        public override bool PreDraw(
            NPC npc,
            SpriteBatch spriteBatch,
            Vector2 screenPos,
            Color drawColor)
        {
            if (npc.type != NPCID.KingSlime)
                return true;

            AIState currentAttack =
                NormalAttackOrder[AttackIndex % NormalAttackOrder.Length];

            // Draw the Huge Jump landing indicator while King Slime is hovering overhead.
            if (currentAttack == AIState.HugeJump &&
                SubState == 1 &&
                slamTargetPos != Vector2.Zero)
            {
                Texture2D telegraphTex = ModContent.Request<Texture2D>(
                    "ShatteredIllusion/Content/NPCs/BossAI/VanilliaBosses/KingSlime/HugeJumpTelegraph"
                ).Value;

                Vector2 drawPos = npc.Bottom - screenPos;
                float beamLength =
                    slamTargetPos.Y - npc.Bottom.Y;

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

            return true;
        }
    }
}