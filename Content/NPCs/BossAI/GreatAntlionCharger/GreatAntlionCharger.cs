using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ShatteredIllusion.Common.Cutscenes;
using ShatteredIllusionKeybinds;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace ShatteredIllusion.Content.NPCs.BossAI.GreatAntlionCharger
{
    [AutoloadBossHead]
    public class GreatAntlionCharger : ModNPC, IParryable
    {
        public enum AIState
        {
            WaitingForCutscene,
            Launch,
            Dash,
            Cooldown
        }

        public AIState CurrentState
        {
            get => (AIState)NPC.ai[0];
            set => NPC.ai[0] = (float)value;
        }

        public ref float Timer => ref NPC.ai[1];

        // Keeps track of where boss is attack loop 
        public ref float AttackSequenceIndex => ref NPC.ai[3];

        public bool HasSeenCutsceneStart
        {
            get => NPC.ai[2] == 1f;
            set => NPC.ai[2] = value ? 1f : 0f;
        }

        //parryplayer checks if can be parried 
        public bool IsParryable { get; private set; }

        // attack order here 
        private static readonly AIState[] AttackOrder = new AIState[]
        {
            AIState.Launch,
            AIState.Dash
        };

        public override void SetStaticDefaults()
        {
            Main.npcFrameCount[NPC.type] = 5; // 5 frames bro 5 frames
        }

        public override void SetDefaults()
        {
            NPC.width = 160;
            NPC.height = 70;
            NPC.scale = 1.2f;
            NPC.damage = 30;
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
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            // Forces the boss sprite to not draw at all while invisible or waiting for cutscene
            if (CurrentState == AIState.WaitingForCutscene || NPC.alpha >= 255)
            {
                return false;
            }

            return true;
        }

        public override bool CanHitPlayer(Player target, ref int cooldownSlot)
        {
            // Disable contact damage while waiting in cutscene
            if (CurrentState == AIState.WaitingForCutscene)
                return false;

            return base.CanHitPlayer(target, ref cooldownSlot);
        }

        public override bool? CanBeHitByItem(Player player, Item item)
        {
            if (CurrentState == AIState.WaitingForCutscene)
                return false;

            return base.CanBeHitByItem(player, item);
        }

        public override bool? CanBeHitByProjectile(Projectile projectile)
        {
            if (CurrentState == AIState.WaitingForCutscene)
                return false;

            return base.CanBeHitByProjectile(projectile);
        }

        public override void FindFrame(int frameHeight)
        {
            // Lock to idle frame (Frame 0) when stationary, invisible, or waiting
            if (NPC.velocity.X == 0f || CurrentState == AIState.WaitingForCutscene)
            {
                NPC.frame.Y = 0;
                return;
            }

            // Animate faster when moving faster
            NPC.frameCounter += System.Math.Abs(NPC.velocity.X) * 0.15f;

            if (NPC.frameCounter >= 5.0)
            {
                NPC.frameCounter = 0.0;
                NPC.frame.Y += frameHeight;

                // Loop through all 5 frames (0 to 4)
                if (NPC.frame.Y >= frameHeight * 5)
                {
                    NPC.frame.Y = 0;
                }
            }
        }

        public override void AI()
        {
            NPC.TargetClosest(true);
            Player target = Main.player[NPC.target];

            NPC.color = Color.White;
            IsParryable = false;

            if (!target.active || target.dead)
            {
                NPC.velocity.Y += 0.2f;
                return;
            }

            //Handles climbing charging 
            Collision.StepUp(ref NPC.position, ref NPC.velocity, NPC.width, NPC.height, ref NPC.stepSpeed, ref NPC.gfxOffY);

            switch (CurrentState)
            {
                case AIState.WaitingForCutscene:

                    //Ensure the boss targets the player 
                    NPC.TargetClosest(false);

                    // while cutscene running keeps boss invisible 
                    if (BossCutsceneSystem.IsCutsceneActive)
                    {
                        HasSeenCutsceneStart = true;
                        NPC.velocity = Vector2.Zero;
                        NPC.alpha = 255; // Fully invisible

                        SpawnSandDustWall();
                    }
                    else if (HasSeenCutsceneStart)
                    {
                        //Start the first attack in the loop
                        AttackSequenceIndex = 0;
                        CurrentState = AttackOrder[(int)AttackSequenceIndex];
                        Timer = 0;

                        NPC.alpha = 0; // Make visible again when state transitions

                        float distance = Vector2.Distance(NPC.Center, target.Center);

                        //32f base speed so the middle one
                        float launchSpeed = MathHelper.Clamp(distance * 0.10f, 32f, 64f);

                        float directionX = target.Center.X > NPC.Center.X ? 1f : -1f;

                        NPC.velocity.X = directionX * launchSpeed;
                        NPC.velocity.Y = 0f; // Grounded launch without upward burst

                        NPC.direction = (int)directionX;
                        NPC.spriteDirection = NPC.direction;
                        NPC.netUpdate = true;

                        // dust launch burst
                        for (int i = 0; i < 35; i++)
                        {
                            Vector2 speed = Main.rand.NextVector2Circular(8f, 8f) + new Vector2(NPC.velocity.X * 0.2f, -2f);
                            Dust tornadoDust = Dust.NewDustPerfect(NPC.Center, DustID.SandstormInABottle, speed);
                            tornadoDust.scale = Main.rand.NextFloat(2.5f, 4.2f);
                            tornadoDust.noGravity = true;
                        }
                    }
                    else
                    {
                        NPC.velocity = Vector2.Zero;
                        NPC.alpha = 255; // Keep invisible if cutscene hasn't triggered yet
                    }
                    break;

                case AIState.Launch:
                    NPC.alpha = 0; // Ensure visible during active AI states
                    Timer++;
                    NPC.spriteDirection = NPC.direction;

                    // Keep tile collision active so it stays glued to floor
                    NPC.noTileCollide = false;

                    // Small hop if running directly a wall
                    if (NPC.velocity.X == 0f && NPC.oldVelocity.X != 0f)
                    {
                        NPC.velocity.Y = -5.5f;
                    }

                    // Ground dust kicking up along the bottom of the hit box
                    if (Timer <= 25f && NPC.velocity.Y == 0f)
                    {
                        Vector2 groundPos = new Vector2(NPC.Center.X, NPC.position.Y + NPC.height);
                        Dust groundDust = Dust.NewDustPerfect(groundPos + new Vector2(Main.rand.NextFloat(-NPC.width / 2f, NPC.width / 2f), 0f), DustID.SandstormInABottle);
                        groundDust.scale = Main.rand.NextFloat(1.5f, 2.5f);
                        groundDust.velocity = new Vector2(-NPC.direction * Main.rand.NextFloat(2f, 5f), -Main.rand.NextFloat(1f, 3f));
                    }

                    // Large trailing particles while zooming
                    if (Timer <= 15f)
                    {
                        for (int i = 0; i < 3; i++)
                        {
                            Dust trailDust = Dust.NewDustPerfect(NPC.Center + Main.rand.NextVector2Circular(NPC.width / 2f, NPC.height / 2f), DustID.SandstormInABottle);
                            trailDust.scale = Main.rand.NextFloat(2f, 3.2f);
                            trailDust.noGravity = true;
                            trailDust.velocity = -NPC.velocity * 0.15f + Main.rand.NextVector2Circular(1f, 1f);
                        }
                    }

                    if (Timer <= 8f)
                    {
                        SoundEngine.PlaySound(SoundID.Roar);
                    }
                    // Apply friction so he doesnt fly away to the farthests depths of terraria caves
                    {
                        NPC.velocity.X *= 0.96f;
                    }

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

                    // Mark boss as parryable and tint sprite very slightly red
                    IsParryable = true;
                    NPC.color = new Color(255, 190, 190);

                    // Start of dash direction toward target
                    if (Timer == 1)
                    {
                        float directionX = target.Center.X > NPC.Center.X ? 1f : -1f;
                        NPC.velocity.X = directionX * 22f; //dash speed
                        NPC.direction = (int)directionX;
                        NPC.netUpdate = true;
                        SoundEngine.PlaySound(SoundID.Roar);
                    }

                    if (NPC.velocity.X == 0f && NPC.oldVelocity.X != 0f)
                    {
                        NPC.velocity.Y = -5.5f;
                    }

                    // Trail effect during dash
                    if (Timer <= 30)
                    {
                        Dust dust = Dust.NewDustPerfect(NPC.Center, DustID.Sand, -NPC.velocity * 0.2f);
                        dust.scale = 1.8f;
                        dust.noGravity = true;
                    }

                    NPC.velocity.X *= 0.98f; // deceleration

                    if (Timer >= 45f)
                    {
                        Timer = 0;
                        CurrentState = AIState.Cooldown;
                        NPC.netUpdate = true;
                    }
                    break;

                case AIState.Cooldown:
                    NPC.alpha = 0;
                    Timer++;
                    NPC.noTileCollide = false;
                    NPC.velocity.X *= 0.88f;

                    // Pause for ___ number of ticks before reading the next attack 
                    if (Timer >= 40f)
                    {
                        Timer = 0;

                        // Increment sequence index and loop back 
                        AttackSequenceIndex = (AttackSequenceIndex + 1) % AttackOrder.Length;
                        CurrentState = AttackOrder[(int)AttackSequenceIndex];

                        NPC.netUpdate = true;
                    }
                    break;
            }
        }

        public void OnParried(Player player)
        {
            float knockbackDir = NPC.Center.X < player.Center.X ? -1f : 1f;
            NPC.velocity = new Vector2(knockbackDir * 8f, -4f);

            CurrentState = AIState.Cooldown;

            //master mode gets a 5 tick stun stead of the normal 15 tick because I HATE YOU
            if (Main.masterMode)
            {
                Timer = -5f;
            }
            else
            {
                Timer = -15f;
            }

            IsParryable = false;
            NPC.netUpdate = true;

            for (int i = 0; i < 20; i++)
            {
                Dust d = Dust.NewDustPerfect(NPC.Center, DustID.Gold, Main.rand.NextVector2Circular(6f, 6f));
                d.noGravity = true;
            }
        }

        private void SpawnSandDustWall()
        {
            // 1 pixel offset
            float xOffset = (NPC.width / 2f + 1f) * NPC.direction;
            Vector2 spawnCenter = NPC.Center + new Vector2(xOffset, 0f);

            //first number changes how high is the wall in blocks
            float wallHeightInPixels = 7f * 16f;
            float halfWallHeight = wallHeightInPixels / 2f;

            int dustCount = 4;
            for (int i = 0; i < dustCount; i++)
            {
                // Pick a random height bounded by the height
                float yOffset = Main.rand.NextFloat(-halfWallHeight, halfWallHeight);
                Vector2 dustPos = spawnCenter + new Vector2(Main.rand.NextFloat(-4f, 4f), yOffset);

                Dust dust = Dust.NewDustPerfect(dustPos, DustID.Sand);
                dust.velocity = new Vector2(NPC.direction * Main.rand.NextFloat(0.5f, 2f), Main.rand.NextFloat(-1f, 1f));
                dust.noGravity = false;
                dust.scale = Main.rand.NextFloat(1f, 1.4f);
            }
        }
    }
}