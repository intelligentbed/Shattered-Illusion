using Microsoft.Xna.Framework;
using System.IO;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using ShatteredIllusion.Common.Cutscenes;
using ShatteredIllusionKeybinds;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace ShatteredIllusion.Content.NPCs.BossAI.GreatAntlionCharger
{
    [AutoloadBossHead] // ID LIKE TO THANK MERLIN FOR HELPING ME OUT ALSO FOR CLEANING MY DIRTY NASTY CODE A LITTLE
    public class GreatAntlionCharger : ModNPC, IParryable
    {
        private const int MainFrameCount = 5;
        private const int BurrowFrameCount = 8;

        private const int BurrowDigTime = 60;
        private const int BurrowPursuitEnd = 180;
        private const int BurrowTelegraphEnd = 210;
        private const int BurrowEnd = 245;
        private const int SpitWindupFrame = 1;
        private const int SpitFireFrame = 2;
        private const int SpitRecoverFrame = 0;
        const float MouthForwardOffset = 8f;
        const float MouthSidewaysOffset = 6f;
        const float MouthVerticalOffset = 8f;
        float upwardAimOffset = -160f;

        private float Phase2DiveLandingX;
        private float Phase2DiveGroundY;
        private float Phase2DiveLaunchX;
        private float Phase2DiveLaunchY;

        private bool Phase2;
        private bool Phase2Transitioning;



        private int DashStuckTimer;

        private const string BurrowTexturePath =
            "ShatteredIllusion/Content/NPCs/BossAI/GreatAntlionCharger/GreatAntlionChargerBurrow";


        public enum AIState
        {
            WaitingForCutscene,
            Launch,
            Dash,
            Phase2BurrowDive,
            Burrow,
            Spit,
            Cooldown
        }
        public AIState CurrentState
        {
            get => (AIState)NPC.ai[0];
            set => NPC.ai[0] = (float)value;
        }

        public ref float Timer => ref NPC.ai[1];

        // we are in the attack loop SO ARE YOU 
        public ref float AttackSequenceIndex => ref NPC.ai[3];

        public bool HasSeenCutsceneStart
        {
            get => NPC.ai[2] == 1f;
            set => NPC.ai[2] = value ? 1f : 0f;
        }

        public bool IsParryable { get; private set; }

        private bool IsHidden =>
            CurrentState == AIState.WaitingForCutscene ||
            (CurrentState == AIState.Burrow && Timer > 30f && Timer < 150f);

        private Asset<Texture2D> burrowTexture;
        private float direction;
        private static readonly AIState[] Phase1AttackOrder =
        {
        AIState.Launch,
        AIState.Dash,
        AIState.Spit,
        AIState.Burrow,
        AIState.Dash,
        AIState.Burrow,
        AIState.Spit,
    };

        private static readonly AIState[] Phase2AttackOrder =
        {
        AIState.Phase2BurrowDive,
        AIState.Phase2BurrowDive,
        AIState.Burrow,
        };


        public override void SetStaticDefaults()
        {
            // 5 frames bro. 5 frames.
            Main.npcFrameCount[NPC.type] = MainFrameCount;
        }

        public override void SetDefaults()
        {
            NPC.width = 160;
            NPC.height = 70;
            NPC.damage = 45;
            NPC.defense = 10;
            NPC.lifeMax = 5000;
            NPC.HitSound = SoundID.NPCHit1;
            NPC.DeathSound = SoundID.NPCDeath1;
            NPC.value = 100f;

            NPC.boss = true;
            NPC.knockBackResist = 0f;

            NPC.aiStyle = -1;
            NPC.noGravity = false;
            NPC.noTileCollide = false;
            Main.npcFrameCount[NPC.type] = MainFrameCount;
        }

        public override void Load()
        {
            burrowTexture = ModContent.Request<Texture2D>(BurrowTexturePath);
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {

            if (CurrentState == AIState.WaitingForCutscene || NPC.alpha >= 255)
            {
                return false;
            }

            if (burrowTexture == null)
            {
                burrowTexture = ModContent.Request<Texture2D>(BurrowTexturePath);
            }

            // Only show the burrow sprite while digging
            if (CurrentState == AIState.Burrow && Timer <= BurrowDigTime &&
                burrowTexture != null && burrowTexture.IsLoaded)
            {
                Texture2D texture = burrowTexture.Value;
                SpriteEffects effects = NPC.spriteDirection == -1
                ? SpriteEffects.None
                : SpriteEffects.FlipHorizontally;

                int burrowFrameHeight = texture.Height / BurrowFrameCount;

                Rectangle sourceRect = new Rectangle(
                    0,
                    NPC.frame.Y,
                    texture.Width,
                    burrowFrameHeight
                );

                Vector2 origin = new Vector2(
                    texture.Width / 2f,
                    burrowFrameHeight / 2f
                );

                Vector2 drawPos = NPC.Center - screenPos + new Vector2(0f, NPC.gfxOffY);

                spriteBatch.Draw(
                    texture,
                    drawPos,
                    sourceRect,
                    NPC.GetAlpha(drawColor),
                    NPC.rotation,
                    origin,
                    NPC.scale,
                    effects,
                    0f
                );

                return false;
            }

            Texture2D mainTexture = TextureAssets.Npc[NPC.type].Value;
            SpriteEffects mainEffects = NPC.spriteDirection == -1
                ? SpriteEffects.None
                : SpriteEffects.FlipHorizontally;

            Vector2 mainOrigin = new Vector2(
                mainTexture.Width / 2f,
                (mainTexture.Height / MainFrameCount) / 2f
            );

            Vector2 mainDrawPos = NPC.Center - screenPos + new Vector2(0f, NPC.gfxOffY);

            spriteBatch.Draw(
                mainTexture,
                mainDrawPos,
                NPC.frame,
                NPC.GetAlpha(drawColor),
                NPC.rotation,
                mainOrigin,
                NPC.scale,
                mainEffects,
                0f
            );

            return false;
        }

        public override bool CanHitPlayer(Player target, ref int cooldownSlot)
        {
            if (IsHidden)
            {
                return false;
            }

            return base.CanHitPlayer(target, ref cooldownSlot);
        }

        public override bool? CanBeHitByItem(Player player, Item item)
        {
            if (IsHidden)
            {
                return false;
            }

            return base.CanBeHitByItem(player, item);
        }

        public override bool? CanBeHitByProjectile(Projectile projectile)
        {
            if (IsHidden)
            {
                return false;
            }

            return base.CanBeHitByProjectile(projectile);
        }

        public override void FindFrame(int frameHeight)
        {
            bool phase2DiveAnimation =
                CurrentState == AIState.Phase2BurrowDive && Timer <= 45f;

            bool normalBurrowAnimation =
                CurrentState == AIState.Burrow && Timer <= BurrowDigTime;

            if (phase2DiveAnimation || normalBurrowAnimation)
            {
                if (burrowTexture == null)
                {
                    burrowTexture = ModContent.Request<Texture2D>(BurrowTexturePath);
                }

                if (burrowTexture != null && burrowTexture.IsLoaded)
                {
                    int burrowFrameHeight =
                        burrowTexture.Height() / BurrowFrameCount;

                    float animationTime =
                        phase2DiveAnimation ? 45f : BurrowDigTime;

                    float animationProgress =
                        MathHelper.Clamp(Timer / animationTime, 0f, 1f);

                    int frame =
                        (int)(animationProgress * BurrowFrameCount);

                    frame = Math.Min(frame, BurrowFrameCount - 1);

                    NPC.frame.Width = burrowTexture.Width();
                    NPC.frame.Height = burrowFrameHeight;
                    NPC.frame.X = 0;
                    NPC.frame.Y = frame * burrowFrameHeight;

                    return;
                }
            }

            Texture2D mainTexture = TextureAssets.Npc[NPC.type].Value;
            NPC.frame.Width = mainTexture.Width;
            NPC.frame.Height = frameHeight;

            if (CurrentState == AIState.Spit)
            {
                int spitFrame;

                if (Timer < 30f)
                {
                    spitFrame = SpitWindupFrame;
                }
                else if (Timer < 40f)
                {
                    spitFrame = SpitFireFrame;
                }
                else
                {
                    spitFrame = SpitRecoverFrame;
                }

                NPC.frame.Y = spitFrame * frameHeight;
                return;
            }

            if (NPC.velocity.X == 0f ||
                CurrentState == AIState.WaitingForCutscene)
            {
                NPC.frame.Y = 0;
                return;
            }

            NPC.frameCounter += Math.Abs(NPC.velocity.X) * 0.15f;

            if (NPC.frameCounter >= MainFrameCount)
            {
                NPC.frameCounter = 0f;
                NPC.frame.Y += frameHeight;

                if (NPC.frame.Y >= frameHeight * MainFrameCount)
                {
                    NPC.frame.Y = 0;
                }


        NPC.frame.Width = TextureAssets.Npc[NPC.type].Value.Width;
                NPC.frame.Height = frameHeight;

                // Freeze on frame 1 while idle
                if (NPC.velocity.X == 0f || CurrentState == AIState.WaitingForCutscene)
                {
                    NPC.frame.Y = 0;
                    return;
                }

                NPC.frameCounter += System.Math.Abs(NPC.velocity.X) * 0.15f;

                if (NPC.frameCounter >= MainFrameCount)
                {
                    NPC.frameCounter = 0f;
                    NPC.frame.Y += frameHeight;

                    if (NPC.frame.Y >= frameHeight * MainFrameCount)
                    {
                        NPC.frame.Y = 0;
                    }
                }
            }
        }

        // Scans downward returns the Y of the nearest solid tile.
        private float GetGroundY(Vector2 checkPosition)
        {
            int startTileX = (int)(checkPosition.X / 16f);
            int startTileY = (int)(checkPosition.Y / 16f);

            for (int y = startTileY; y < startTileY + 120; y++)
            {
                Tile tile = Main.tile[startTileX, y];

                if (tile != null && tile.HasUnactuatedTile && Main.tileSolid[tile.TileType])
                {
                    return y * 16f;
                }
            }

            return checkPosition.Y;
        }

        public override void AI()
        {

            NPC.TargetClosest(true);
            Player target = Main.player[NPC.target];

            NPC.color = Color.White;
            IsParryable = false;

            // Phase 2 begins at 55% HP.
            if (!Phase2 && !Phase2Transitioning && NPC.life <= NPC.lifeMax * 0.55f)
            {
                StartPhase2(target);
            }

            if (!target.active || target.dead)
            {
                NPC.velocity.Y += 0.2f;
                return;
            }

            // Lets him step up small ledges while charging
            Collision.StepUp(
                ref NPC.position,
                ref NPC.velocity,
                NPC.width,
                NPC.height,
                ref NPC.stepSpeed,
                ref NPC.gfxOffY
            );



            switch (CurrentState)
            {
                case AIState.WaitingForCutscene:

                    NPC.TargetClosest(false);

                    if (BossCutsceneSystem.IsCutsceneActive)
                    {
                        HasSeenCutsceneStart = true;
                        NPC.velocity = Vector2.Zero;
                        NPC.alpha = 255;

                        SpawnSandDustWall();
                    }
                    else if (HasSeenCutsceneStart)
                    {
                        AttackSequenceIndex = 0;
                        CurrentState = Phase1AttackOrder[(int)AttackSequenceIndex];
                        Timer = 0;

                        NPC.alpha = 0;

                        float distance = Vector2.Distance(NPC.Center, target.Center);

                        // Clamped 32-64f so launch speed scales with distance but doesn't go absurd
                        float launchSpeed = MathHelper.Clamp(distance * 0.10f, 32f, 64f);
                        float directionX = target.Center.X > NPC.Center.X ? 1f : -1f;

                        NPC.velocity.X = directionX * launchSpeed;
                        NPC.velocity.Y = 0f; // Grounded launch, no upward burst

                        NPC.direction = (int)directionX;
                        NPC.spriteDirection = NPC.direction;
                        NPC.netUpdate = true;

                        for (int i = 0; i < 35; i++)
                        {
                            Vector2 speed =
                                Main.rand.NextVector2Circular(8f, 8f) +
                                new Vector2(NPC.velocity.X * 0.2f, -2f);

                            Dust tornadoDust = Dust.NewDustPerfect(
                                NPC.Center,
                                DustID.SandstormInABottle,
                                speed
                            );

                            tornadoDust.scale = Main.rand.NextFloat(2.5f, 4.2f);
                            tornadoDust.noGravity = true;
                        }
                    }
                    else
                    {
                        NPC.velocity = Vector2.Zero;
                        NPC.alpha = 255;
                    }

                    break;

                case AIState.Launch:
                    NPC.alpha = 0;
                    Timer++;
                    NPC.spriteDirection = NPC.direction;
                    NPC.noTileCollide = false;

                    // Hop over small ledges when velocity suddenly zeroes out
                    if (NPC.velocity.X == 0f && NPC.oldVelocity.X != 0f)
                    {
                        NPC.velocity.Y = -5.5f;
                    }

                    if (Timer <= 25f && NPC.velocity.Y == 0f)
                    {
                        Vector2 groundPos = new Vector2(
                            NPC.Center.X,
                            NPC.position.Y + NPC.height
                        );

                        Dust groundDust = Dust.NewDustPerfect(
                            groundPos + new Vector2(
                                Main.rand.NextFloat(-NPC.width / 2f, NPC.width / 2f),
                                0f
                            ),
                            DustID.SandstormInABottle
                        );

                        groundDust.scale = Main.rand.NextFloat(1.5f, 2.5f);
                        groundDust.velocity = new Vector2(
                            -NPC.direction * Main.rand.NextFloat(2f, 5f),
                            -Main.rand.NextFloat(1f, 3f)
                        );
                    }

                    if (Timer <= 15f)
                    {
                        for (int i = 0; i < 3; i++)
                        {
                            Dust trailDust = Dust.NewDustPerfect(
                                NPC.Center + Main.rand.NextVector2Circular(
                                    NPC.width / 2f,
                                    NPC.height / 2f
                                ),
                                DustID.SandstormInABottle
                            );

                            trailDust.scale = Main.rand.NextFloat(2f, 3.2f);
                            trailDust.noGravity = true;
                            trailDust.velocity =
                                -NPC.velocity * 0.15f +
                                Main.rand.NextVector2Circular(1f, 1f);
                        }
                    }

                    if (Timer <= 8f)
                    {
                        SoundEngine.PlaySound(SoundID.Roar);
                    }

                    // Don't let this idiot launch himself into the depths of Terraria.
                    NPC.velocity.X *= 0.96f;

                    if (Timer >= 60f)
                    {
                        Timer = 0;
                        CurrentState = AIState.Cooldown;
                        NPC.noTileCollide = false;
                        NPC.netUpdate = true;
                    }

                    break;

                case AIState.Dash:
                    NPC.alpha = 0;
                    Timer++;
                    NPC.spriteDirection = NPC.direction;

                    // Red tint tells the player this attack can be parried.
                    IsParryable = true;
                    NPC.color = new Color(255, 190, 190);


                    // Reset the escape timer when the dash starts.
                    if (Timer == 1)
                    {
                        DashStuckTimer = 0;
                        NPC.noTileCollide = false;

                        float directionX = target.Center.X > NPC.Center.X ? 1f : -1f;
                        float dashSpeed = Main.masterMode
                            ? 23f
                            : (Main.expertMode ? 21f : 19f);

                        NPC.velocity.X = directionX * dashSpeed;
                        NPC.velocity.Y = 0f;

                        NPC.direction = (int)directionX;
                        NPC.netUpdate = true;

                        SoundEngine.PlaySound(SoundID.Roar);
                    }

                    if (Math.Abs(NPC.velocity.X) < 1f && Math.Abs(NPC.oldVelocity.X) > 3f)
                    {
                        DashStuckTimer++;

                        if (DashStuckTimer == 1)
                        {
                            NPC.velocity.Y = -8f;
                        }

                        if (DashStuckTimer >= 4)
                        {
                            NPC.noTileCollide = true;
                            NPC.velocity.X = NPC.direction * 12f;
                            NPC.velocity.Y = -3f;

                            DashStuckTimer = 0;
                            NPC.netUpdate = true;
                        }
                    }
                    else
                    {
                        DashStuckTimer = 0;
                    }

                    // Once we're moving through the obstruction, turn collision back on.
                    if (NPC.noTileCollide && Math.Abs(NPC.velocity.X) > 4f)
                    {
                        NPC.noTileCollide = false;
                    }

                    if (Timer <= 30f)
                    {
                        Dust dust = Dust.NewDustPerfect(
                            NPC.Center,
                            DustID.Sand,
                            -NPC.velocity * 0.2f
                        );

                        dust.scale = 1.8f;
                        dust.noGravity = true;
                    }

                    // Deceleration.
                    NPC.velocity.X *= 0.98f;

                    if (Timer >= 45f)
                    {
                        DashStuckTimer = 0;
                        NPC.noTileCollide = false;

                        Timer = 0;
                        CurrentState = AIState.Cooldown;
                        NPC.netUpdate = true;
                    }

                    break;

                case AIState.Burrow:
                    Timer++;

                    // Digging in on the surface
                    if (Timer <= BurrowDigTime)
                    {
                        float progress = Timer / BurrowDigTime;
                        float smoothProgress = progress * progress * (3f - 2f * progress);

                        NPC.alpha = (int)MathHelper.Lerp(0f, 190f, smoothProgress);

                        NPC.noTileCollide = true;
                        NPC.velocity.X *= 0.92f;
                        NPC.velocity.Y = MathHelper.Lerp(0f, 2f, smoothProgress);

                        int dustCount = progress < 0.5f ? 3 : 5;

                        for (int i = 0; i < dustCount; i++)
                        {
                            Dust d = Dust.NewDustPerfect(
                                NPC.Bottom + new Vector2(
                                    Main.rand.NextFloat(-NPC.width / 2f, NPC.width / 2f),
                                    0f
                                ),
                                DustID.Sand
                            );

                            d.velocity = new Vector2(
                                Main.rand.NextFloat(-3f, 3f),
                                -Main.rand.NextFloat(2f, 5f)
                            );

                            d.scale = Main.rand.NextFloat(1.3f, 2.1f);
                            d.noGravity = false;
                        }

                        if (Main.rand.NextBool(3))
                        {
                            Dust tungstenDust = Dust.NewDustPerfect(
                                NPC.Bottom + new Vector2(
                                    Main.rand.NextFloat(-NPC.width / 2f, NPC.width / 2f),
                                    0f
                                ),
                                DustID.Tungsten
                            );

                            tungstenDust.velocity = new Vector2(
                                Main.rand.NextFloat(-2f, 2f),
                                -Main.rand.NextFloat(1f, 3f)
                            );

                            tungstenDust.scale = Main.rand.NextFloat(0.8f, 1.3f);
                            tungstenDust.noGravity = true;
                        }

                        if (Timer == 1f)
                        {
                            SoundEngine.PlaySound(SoundID.Item14, NPC.Center);
                        }
                    }
                    // Underground pursuit
                    else if (Timer < BurrowPursuitEnd)
                    {
                        NPC.alpha = 255;
                        NPC.noTileCollide = true;

                        float actualGroundY = GetGroundY(target.Center) + 64f;

                        float deltaX = target.Center.X - NPC.Center.X;
                        float speedX = MathHelper.Clamp(
                            deltaX * 0.08f,
                            -16f,
                            16f
                        );

                        NPC.velocity.X = speedX;
                        NPC.velocity.Y = (actualGroundY - NPC.Center.Y) * 0.2f;

                        Vector2 groundPos = new Vector2(
                            NPC.Center.X,
                            actualGroundY - 64f
                        );

                        Dust d = Dust.NewDustPerfect(
                            groundPos + new Vector2(
                                Main.rand.NextFloat(-20f, 20f),
                                0f
                            ),
                            DustID.SandstormInABottle
                        );

                        Dust tungstenDust = Dust.NewDustPerfect(
                            groundPos + new Vector2(
                                Main.rand.NextFloat(-20f, 20f),
                                0f
                            ),
                            DustID.Tungsten
                        );

                        d.velocity = new Vector2(
                            0f,
                            -Main.rand.NextFloat(2f, 4f)
                        );

                        d.scale = Main.rand.NextFloat(1.5f, 2.8f);
                    }
                    // Stop & telegraph burst location on ground
                    else if (Timer < BurrowTelegraphEnd)
                    {
                        NPC.velocity = Vector2.Zero;
                        NPC.alpha = 255;

                        float actualGroundY = GetGroundY(NPC.Center);
                        Vector2 telegraphPos = new Vector2(
                            NPC.Center.X,
                            actualGroundY
                        );

                        for (int i = 0; i < 3; i++)
                        {
                            Dust d = Dust.NewDustPerfect(
                                telegraphPos + new Vector2(
                                    Main.rand.NextFloat(-NPC.width / 2f, NPC.width / 2f),
                                    0f
                                ),
                                DustID.SandstormInABottle
                            );

                            d.velocity = new Vector2(
                                0f,
                                -Main.rand.NextFloat(4f, 8f)
                            );

                            d.scale = Main.rand.NextFloat(2f, 3.5f);
                            d.noGravity = true;
                        }
                    }
                    // Erupt upward out of the ground
                    else
                    {
                        if (Timer == BurrowTelegraphEnd)
                        {
                            // Snap to ground level right before emerging
                            float actualGroundY = GetGroundY(NPC.Center);

                            NPC.Center = new Vector2(
                                NPC.Center.X,
                                actualGroundY - 30f
                            );

                            NPC.velocity = new Vector2(0f, -18f);
                            NPC.alpha = 0;

                            // Keep collision disabled for the entire launch.
                            NPC.noTileCollide = true;

                            NPC.netUpdate = true;

                            SoundEngine.PlaySound(SoundID.Roar, NPC.Center);

                            for (int i = 0; i < 35; i++)
                            {
                                Vector2 dustVel =
                                    Main.rand.NextVector2Circular(9f, 9f) +
                                    new Vector2(0f, -5f);

                                Dust d = Dust.NewDustPerfect(
                                    NPC.Center,
                                    DustID.Sand,
                                    dustVel
                                );

                                d.scale = Main.rand.NextFloat(2f, 3.8f);
                            }

                            // amount of rubble based of difficulty
                            int rubbleCount = 6;

                            if (Main.masterMode)
                            {
                                rubbleCount = 12;
                            }
                            else if (Main.expertMode)
                            {
                                rubbleCount = 8;
                            }

                            for (int r = 0; r < rubbleCount; r++)
                            {
                                float offsetX = Main.rand.NextFloat(-500f, 500f); // the spread range of rubble

                                Vector2 spawnPos = new Vector2(
                                    NPC.Center.X + offsetX,
                                    NPC.Center.Y - 500f
                                );

                                Vector2 velocity = new Vector2(
                                    Main.rand.NextFloat(-2f, 2f),
                                    Main.rand.NextFloat(2f, 5f)
                                );

                                Projectile.NewProjectile(
                                    NPC.GetSource_FromAI(),
                                    spawnPos,
                                    velocity,
                                    ModContent.ProjectileType<FallingRubble>(),
                                    50,
                                    2f,
                                    Main.myPlayer
                                );
                            }
                        }

                        // Gravity's back on for the actual jump out
                        NPC.velocity.Y += 0.35f;
                        if (Timer >= BurrowEnd)
                        {
                            NPC.noTileCollide = false;
                            Timer = 0;
                            CurrentState = AIState.Cooldown;
                            NPC.netUpdate = true;
                        }
                    }
                    break;


                case AIState.Spit:
                    NPC.alpha = 0;
                    Timer++;

                    // Slow down and face the player.
                    NPC.velocity.X *= 0.85f;

                    if (Timer == 1f)
                    {
                        NPC.velocity = Vector2.Zero;
                    }

                    {
                        float spitDeltaX = target.Center.X - NPC.Center.X;

                        if (Math.Abs(spitDeltaX) > 10f)
                        {
                            int newDirection = spitDeltaX > 0f ? 1 : -1;

                            if (newDirection != NPC.spriteDirection)
                            {
                                NPC.direction = newDirection;
                                NPC.spriteDirection = newDirection;
                                NPC.netUpdate = true;
                            }
                        }
                    }

                    //Sand gathers around his mouth before spitting.
                    if (Timer <= 30f)
                    {
                        Vector2 mouthPosition = NPC.Center + new Vector2(
                            NPC.spriteDirection * (NPC.width / 2f + MouthForwardOffset + MouthSidewaysOffset),
                            MouthVerticalOffset
                        );

                        if (Main.rand.NextBool(2))
                        {
                            Dust dust = Dust.NewDustPerfect(
                                mouthPosition + Main.rand.NextVector2Circular(8f, 8f),
                                DustID.SandstormInABottle,
                                Main.rand.NextVector2Circular(1f, 1f)
                            );

                            dust.scale = Main.rand.NextFloat(1.2f, 2f);
                            dust.noGravity = true;
                        }
                    }

                    // SPIT
                    if (Timer == 30f)
                    {
                        Vector2 mouthPosition = NPC.Center + new Vector2(
                            NPC.direction * (NPC.width / 2f + MouthForwardOffset + MouthSidewaysOffset),
                            MouthVerticalOffset
                        );

                        Vector2 predictedPosition =
                            target.Center + target.velocity * 10f;

                        Vector2 direction = predictedPosition - mouthPosition;
                        direction.Normalize();
                        direction = direction.RotatedBy(-MathHelper.ToRadians(15f));

                        int shotCount = Main.masterMode
                            ? 4
                            : Main.expertMode
                                ? 3
                                : 1;

                        float spitSpeed = Main.masterMode
                            ? 14f
                            : Main.expertMode
                                ? 13f
                                : 12f;

                        float spread = Main.masterMode
                            ? 0.22f
                            : Main.expertMode
                                ? 0.18f
                                : 0f;

                        for (int i = 0; i < shotCount; i++)
                        {
                            Vector2 shotDirection = direction;

                            if (shotCount > 1)
                            {
                                float offset = (i - (shotCount - 1) / 2f) * spread;
                                shotDirection = direction.RotatedBy(offset);
                            }

                            Projectile.NewProjectile(
                                NPC.GetSource_FromAI(),
                                mouthPosition,
                                shotDirection * spitSpeed,
                                ModContent.ProjectileType<SandBall>(),
                                35,
                                0f,
                                Main.myPlayer
                            );
                        }

                        SoundEngine.PlaySound(SoundID.DD2_OgreSpit, mouthPosition);

                        for (int i = 0; i < 15; i++)
                        {
                            Dust dust = Dust.NewDustPerfect(
                                mouthPosition,
                                DustID.Sand,
                                direction * Main.rand.NextFloat(2f, 5f) +
                                Main.rand.NextVector2Circular(2f, 2f)
                            );

                            dust.scale = Main.rand.NextFloat(1.5f, 2.5f);
                            dust.noGravity = true;
                        }
                    }

                    // Recovery
                    if (Timer >= 55f)
                    {
                        Timer = 0;
                        CurrentState = AIState.Cooldown;
                        NPC.netUpdate = true;
                    }

                    break;


                case AIState.Phase2BurrowDive:
                    Timer++;

                    // BURROW DOWN FOR PHASE 2
                    if (Timer <= 45f)
                    {
                        float progress = Timer / 45f;
                        float smoothProgress = progress * progress * (3f - 2f * progress);

                        NPC.alpha = (int)MathHelper.Lerp(0f, 255f, smoothProgress);
                        NPC.noTileCollide = true;

                        NPC.velocity.X *= 0.90f;
                        NPC.velocity.Y = MathHelper.Lerp(0f, 3f, smoothProgress);

                        for (int i = 0; i < 4; i++)
                        {
                            Dust dust = Dust.NewDustPerfect(
                                NPC.Bottom + new Vector2(
                                    Main.rand.NextFloat(-NPC.width / 2f, NPC.width / 2f),
                                    0f
                                ),
                                DustID.Sand
                            );

                            dust.velocity = new Vector2(
                                Main.rand.NextFloat(-3f, 3f),
                                -Main.rand.NextFloat(2f, 5f)
                            );

                            dust.scale = Main.rand.NextFloat(1.3f, 2.3f);
                        }

                        if (Timer == 1f)
                        {
                            SoundEngine.PlaySound(SoundID.Item14, NPC.Center);
                        }
                    }

                    // REPOSITIONING FARTHER WAY FOR THE LAUNCH (lunch im writing this kinda hungry btw)
                    else if (Timer <= 75f)
                    {
                        NPC.alpha = 255;
                        NPC.noTileCollide = true;
                        NPC.velocity = Vector2.Zero;

                        if (Timer == 46f)
                        {
                            float direction = target.Center.X >= NPC.Center.X
                                ? 1f
                                : -1f;

                            float launchX = target.Center.X - direction * 800f;

                            launchX = MathHelper.Clamp(
                                launchX,
                                200f,
                                Main.maxTilesX * 16f - 200f
                            );

                            // Land BEHIND the player.
                            Phase2DiveLandingX =
                                target.Center.X - direction * 180f;

                            Phase2DiveLandingX = MathHelper.Clamp(
                                Phase2DiveLandingX,
                                200f,
                                Main.maxTilesX * 16f - 200f
                            );

                            float launchGroundY = GetGroundY(
                                new Vector2(launchX, target.Center.Y)
                            );

                            Phase2DiveGroundY = GetGroundY(
                                new Vector2(
                                    Phase2DiveLandingX,
                                    target.Center.Y
                                )
                            );

                            NPC.Center = new Vector2(
                                launchX,
                                launchGroundY - NPC.height / 2f
                            );

                            NPC.direction = (int)direction;
                            NPC.spriteDirection = NPC.direction;

                            NPC.netUpdate = true;
                        }

                        if (Main.rand.NextBool(2))
                        {
                            Dust dust = Dust.NewDustPerfect(
                                NPC.Center + Main.rand.NextVector2Circular(30f, 15f),
                                DustID.SandstormInABottle,
                                Main.rand.NextVector2Circular(1f, 1f)
                            );

                            dust.scale = Main.rand.NextFloat(1.5f, 2.5f);
                            dust.noGravity = true;
                        }
                    }

                    // TELEGRAPH FROM THE LAUNCH
                    else if (Timer <= 90f)
                    {
                        NPC.alpha = 0;
                        NPC.noTileCollide = true;
                        NPC.velocity = Vector2.Zero;

                        for (int i = 0; i < 5; i++)
                        {
                            Dust dust = Dust.NewDustPerfect(
                                NPC.Bottom + new Vector2(
                                    Main.rand.NextFloat(-NPC.width / 2f, NPC.width / 2f),
                                    0f
                                ),
                                DustID.Sand
                            );

                            dust.velocity = new Vector2(
                                Main.rand.NextFloat(-4f, 4f),
                                -Main.rand.NextFloat(4f, 8f)
                            );

                            dust.scale = Main.rand.NextFloat(1.5f, 3f);
                            dust.noGravity = true;
                        }

                        if (Timer == 76f)
                        {
                            SoundEngine.PlaySound(
                                SoundID.Roar,
                                NPC.Center
                            );
                        }
                    }
            
                   
                    // ACTUAL PHASE 2 LAUNCH
                    else
                    {
                        NPC.alpha = 0;
                        NPC.noTileCollide = true;

                        const float airTime = 50f; // total frames the leap takes
                        const float arcHeight = 500f;

                        if (Timer == 91f)
                        {
                            // Snapshot start position once, at launch.
                            Phase2DiveLaunchX = NPC.Center.X;
                            Phase2DiveLaunchY = NPC.Center.Y;

                            NPC.direction =
                                Phase2DiveLandingX > Phase2DiveLaunchX ? 1 : -1;

                            NPC.spriteDirection = NPC.direction;

                            NPC.netUpdate = true;

                            // Launch burst.
                            for (int i = 0; i < 30; i++)
                            {
                                Dust dust = Dust.NewDustPerfect(
                                    NPC.Center,
                                    DustID.Sand,
                                    Main.rand.NextVector2Circular(8f, 8f)
                                );

                                dust.scale = Main.rand.NextFloat(2f, 3.5f);
                                dust.noGravity = true;
                            }

                            // RUBBLE
                            int rubbleCount = Main.masterMode
                                ? 12
                                : Main.expertMode
                                    ? 8
                                    : 6;

                            for (int i = 0; i < rubbleCount; i++)
                            {
                                float rubbleX =
                                    target.Center.X +
                                    Main.rand.NextFloat(-500f, 500f);

                                Vector2 rubbleSpawn = new Vector2(
                                    rubbleX,
                                    target.Center.Y - 450f
                                );

                                Vector2 rubbleVelocity = new Vector2(
                                    Main.rand.NextFloat(-2f, 2f),
                                    Main.rand.NextFloat(2f, 5f)
                                );

                                Projectile.NewProjectile(
                                    NPC.GetSource_FromAI(),
                                    rubbleSpawn,
                                    rubbleVelocity,
                                    ModContent.ProjectileType<FallingRubble>(),
                                    50,
                                    2f,
                                    Main.myPlayer
                                );
                            }
                        }

                        if (Timer >= 91f)
                        {
    
                            float progress =
                                MathHelper.Clamp((Timer - 91f) / airTime, 0f, 1f);

                            float landingY =
                                Phase2DiveGroundY - NPC.height / 2f;

                            float newX =
                                MathHelper.Lerp(Phase2DiveLaunchX, Phase2DiveLandingX, progress);

                            float newY =
                                MathHelper.Lerp(Phase2DiveLaunchY, landingY, progress)
                                - arcHeight * 4f * progress * (1f - progress);

                            NPC.Center = new Vector2(newX, newY);

                            // Sand trail while flying.
                            if (Main.rand.NextBool(2))
                            {
                                Dust dust = Dust.NewDustPerfect(
                                    NPC.Center,
                                    DustID.Sand,
                                    Main.rand.NextVector2Circular(1.5f, 1.5f)
                                );

                                dust.scale = Main.rand.NextFloat(1.5f, 2.5f);
                                dust.noGravity = true;
                            }

                            // CRASH 
                            if (progress >= 1f)
                            {
                                NPC.Center = new Vector2(
                                    Phase2DiveLandingX,
                                    landingY
                                );

                                NPC.velocity = Vector2.Zero;
                                NPC.noTileCollide = false;

                                SoundEngine.PlaySound(
                                    SoundID.Item14,
                                    NPC.Center
                                );

                                for (int i = 0; i < 50; i++)
                                {
                                    Vector2 dustVelocity =
                                        Main.rand.NextVector2Circular(10f, 7f);

                                    dustVelocity.Y -= 4f;

                                    Dust dust = Dust.NewDustPerfect(
                                        NPC.Bottom,
                                        DustID.Sand,
                                        dustVelocity
                                    );

                                    dust.scale = Main.rand.NextFloat(2f, 4f);
                                    dust.noGravity = true;
                                }

                                // Start the actual burrow animation.
                                Timer = 0;
                                CurrentState = AIState.Burrow;

                                NPC.alpha = 0;
                                NPC.noTileCollide = true;

                                NPC.netUpdate = true;
                            }
                        }
                    }

                    break;

                case AIState.Cooldown:
                    NPC.alpha = 0;
                    Timer++;
                    NPC.noTileCollide = false;
                    NPC.velocity.X *= 0.88f;

                    float cooldownTime = Main.masterMode
                        ? 45f
                        : (Main.expertMode ? 55f : 64f);

                    if (Timer >= cooldownTime)
                    {
                        Timer = 0;

                        AIState[] attackOrder = Phase2
                            ? Phase2AttackOrder
                            : Phase1AttackOrder;

                        AttackSequenceIndex =
                            (AttackSequenceIndex + 1) % attackOrder.Length;

                        CurrentState = attackOrder[(int)AttackSequenceIndex];
                        NPC.netUpdate = true;
                    }

                    break;
            }
        }

        private void StartPhase2(Player target)
        {
            Phase2 = true;
            Phase2Transitioning = true;

            Timer = 0;
            AttackSequenceIndex = 0;

            NPC.velocity = Vector2.Zero;
            NPC.noTileCollide = false;
            NPC.alpha = 0;
            NPC.color = Color.White;

            IsParryable = false;

            NPC.netUpdate = true;

            SoundEngine.PlaySound(SoundID.Roar, NPC.Center);

            for (int i = 0; i < 40; i++)
            {
                Vector2 velocity = Main.rand.NextVector2Circular(8f, 6f);
                velocity.Y -= 3f;

                Dust dust = Dust.NewDustPerfect(
                    NPC.Center,
                    DustID.Sand,
                    velocity
                );

                dust.scale = Main.rand.NextFloat(2f, 4f);
                dust.noGravity = true;
            }
        }

        private void HandlePhase2Transition()
        {
            Timer++;

            NPC.velocity.X *= 0.85f;
            NPC.velocity.Y = 0f;

            NPC.noTileCollide = false;
            NPC.alpha = 0;
            NPC.color = Color.White;

            IsParryable = false;


            NPC.position.X += Main.rand.NextFloat(-1.5f, 1.5f);

            int dustCount = Timer < 30f ? 4 : 7;

            for (int i = 0; i < dustCount; i++)
            {
                Vector2 dustPosition = NPC.Bottom + new Vector2(
                    Main.rand.NextFloat(-NPC.width / 2f, NPC.width / 2f),
                    0f
                );

                Dust dust = Dust.NewDustPerfect(
                    dustPosition,
                    DustID.Sand
                );

                dust.velocity = new Vector2(
                    Main.rand.NextFloat(-4f, 4f),
                    -Main.rand.NextFloat(2f, 6f)
                );

                dust.scale = Main.rand.NextFloat(1.5f, 3f);
                dust.noGravity = true;
            }

            if (Timer == 30f)
            {
                SoundEngine.PlaySound(SoundID.Item14, NPC.Center);

                for (int i = 0; i < 30; i++)
                {
                    Dust dust = Dust.NewDustPerfect(
                        NPC.Center,
                        DustID.Sand,
                        Main.rand.NextVector2Circular(7f, 7f)
                    );

                    dust.scale = Main.rand.NextFloat(2f, 3.5f);
                    dust.noGravity = true;
                }
            }

            if (Timer >= 60f)
            {
                Timer = 0;
                AttackSequenceIndex = 0;
                CurrentState = AIState.Phase2BurrowDive;
                Phase2Transitioning = false;
                NPC.netUpdate = true;
            }
        }

        public void OnParried(Player player)
        {
            float knockbackDir = NPC.Center.X < player.Center.X ? -1f : 1f;
            NPC.velocity = new Vector2(knockbackDir * 10f, -4f);

            CurrentState = AIState.Cooldown;

            // Master mode gets a 5 tick stun instead of the normal 15 because I HATE YOU.
            Timer = Main.masterMode ? -1f : -15f;

            IsParryable = false;
            NPC.netUpdate = true;

            for (int i = 0; i < 20; i++)
            {
                Dust d = Dust.NewDustPerfect(
                    NPC.Center,
                    DustID.Gold,
                    Main.rand.NextVector2Circular(6f, 6f)
                );

                d.noGravity = true;
            }
        }

        private void SpawnSandDustWall()
        {
            float xOffset = (NPC.width / 2f + 1f) * NPC.direction; // 1 pixel offset lol
            Vector2 spawnCenter = NPC.Center + new Vector2(xOffset, 0f);

            float wallHeightInPixels = 7f * 16f; // wall height in blocks
            float halfWallHeight = wallHeightInPixels / 2f;

            int dustCount = 4;

            for (int i = 0; i < dustCount; i++)
            {
                float yOffset = Main.rand.NextFloat(-halfWallHeight, halfWallHeight);
                Vector2 dustPos = spawnCenter + new Vector2(
                    Main.rand.NextFloat(-4f, 4f),
                    yOffset
                );

                Dust dust = Dust.NewDustPerfect(dustPos, DustID.Sand);
                dust.velocity = new Vector2(
                    NPC.direction * Main.rand.NextFloat(0.5f, 2f),
                    Main.rand.NextFloat(-1f, 1f)
                );

                dust.noGravity = false;
                dust.scale = Main.rand.NextFloat(1f, 1.4f);
            }
        }
    }
}