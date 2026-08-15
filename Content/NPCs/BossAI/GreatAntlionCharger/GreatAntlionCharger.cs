using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ShatteredIllusion.Content.NPCs.BossAI.GreatAntlionCharger
{
    public class GreatAntlionCharger : ModNPC //ok so this is my first REAL attempt as boss ai so imma have to comment ever which where to know where shit is 
    {
        public enum AIState //this handels the attack order
        {
            WaitingForCutscene,
            Launch,
            Cooldown
        }

        public AIState CurrentState
        {
            get => (AIState)NPC.ai[0];
            set => NPC.ai[0] = (float)value;
        }

        public ref float LaunchTimer => ref NPC.ai[1];

        public bool HasSeenCutsceneStart
        {
            get => NPC.ai[2] == 1f;
            set => NPC.ai[2] = value ? 1f : 0f;
        }

        public override void SetDefaults()
        {
            NPC.width = 100;
            NPC.height = 30;
            NPC.damage = 30;
            NPC.defense = 10;
            NPC.lifeMax = 200;
            NPC.HitSound = SoundID.NPCHit1;
            NPC.DeathSound = SoundID.NPCDeath1;
            NPC.value = 100f;
            NPC.aiStyle = -1;
            NPC.knockBackResist = 0.5f;
            NPC.noGravity = false;
            NPC.noTileCollide = false;
        }

        public override void AI()
        {
            NPC.TargetClosest(true);
            Player target = Main.player[NPC.target];

            if (!target.active || target.dead)
            {
                NPC.velocity.Y += 0.2f;
                return;
            }

            switch (CurrentState)
            {
                case AIState.WaitingForCutscene:
                    // while cutscene running keeps boss invisible 
                    if (BossCutsceneSystem.IsCutsceneActive)
                    {
                        HasSeenCutsceneStart = true;
                        NPC.velocity = Vector2.Zero;
                        NPC.Opacity = 0f; 
                    }
                    //ok this should be self explaniatory 
                    else if (HasSeenCutsceneStart)
                    {
                        CurrentState = AIState.Launch;
                        LaunchTimer = 0;

                        NPC.Opacity = 1f;

                        float distance = Vector2.Distance(NPC.Center, target.Center);

                        //12f base speed, plus alittle bit extra speed for distance (clamped so it doesn't zoom forever) 
                        float launchSpeed = MathHelper.Clamp(distance * 0.10f, 32f, 64f);

                        float directionX = target.Center.X > NPC.Center.X ? 1f : -1f;

                        NPC.velocity.X = directionX * launchSpeed;

                        NPC.velocity.X = directionX * launchSpeed;
                        NPC.velocity.Y = -3f; // Upward burst

                        NPC.direction = (int)directionX;
                        NPC.spriteDirection = NPC.direction;
                        NPC.netUpdate = true;
                    }
                    else
                    {
                        NPC.velocity = Vector2.Zero;
                        NPC.Opacity = 0f;
                    }
                    break;

                case AIState.Launch:
                    LaunchTimer++;
                    NPC.spriteDirection = NPC.direction;
                    NPC.Opacity = 1f;

                    // disables the tile collision for the first 15 ticks 
                    if (LaunchTimer <= 15f)
                    {
                        NPC.noTileCollide = true;
                    }
                    else
                    {
                        NPC.noTileCollide = false; // reenable
                    }

                    // Apply momentum friction so he doesnt fly away to the farthests depths of terraria caves
                    {
                        NPC.velocity.X *= 0.97f;
                    }

                    if (LaunchTimer >= 60f)
                    {
                        LaunchTimer = 0;
                        CurrentState = AIState.Cooldown;
                        NPC.noTileCollide = false; 
                        NPC.netUpdate = true;
                    }
                    break;

                case AIState.Cooldown:
                    NPC.Opacity = 1f;
                    NPC.noTileCollide = false;
                    NPC.velocity.X *= 0.88f;
                    break;
            }
        }
    }
}