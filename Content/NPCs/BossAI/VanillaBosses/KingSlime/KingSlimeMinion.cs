using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ParticleLibrary.Core.V3.Particles;
using ParticleLibrary.Utilities;
using ShatteredIllusion.Content.Particles;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using SystemVector2 = System.Numerics.Vector2;

namespace ShatteredIllusion.Content.NPCs.BossAI.VanillaBosses.KingSlime
{
//TODO: GET A SPRITE
    public class KingSlimeMinion : ModNPC
    {
        public const int Slot_Owner = 0;
        public const int Slot_Phase = 1;
        public const int Slot_ReturnCountdown = 2;
        public const int Slot_AttackVariant = 3;

        private enum MinionPhase : byte
        {
            Attacking = 0,
            Returning = 1,
        }

        private enum AttackSubPhase : byte
        {
            Orbit = 0,
            Brace = 1,
            Strike = 2,
            Reposition = 3,
        }

        private const float OrbitRadius = 160f;
        private const int OrbitDuration = 75;
        private const int BraceDuration = 28;
        private const int StrikeDuration = 26;
        private const int RepositionDuration = 18;

        private const float ArrivalDistance = 60f;

        // Base speeds are tuned for Master Mode with a very slight trim off based on the other difficulties so should be good
        private const float BaseOrbitAngularSpeed = 0.155f;
        private const float BaseStrikeSpeed = 15.5f;
        private const float BaseReturnSpeed = 14f;

        private static float DifficultySpeedMultiplier =>
            Main.masterMode ? 1f : (Main.expertMode ? 0.82f : 0.65f);

        private static float OrbitAngularSpeed => BaseOrbitAngularSpeed * DifficultySpeedMultiplier;
        private static float StrikeSpeed => BaseStrikeSpeed * DifficultySpeedMultiplier;
        private static float ReturnSpeed => BaseReturnSpeed * DifficultySpeedMultiplier;

        private AttackSubPhase SubPhase
        {
            get => (AttackSubPhase)NPC.localAI[0];
            set => NPC.localAI[0] = (float)value;
        }

        private float SubPhaseTimer
        {
            get => NPC.localAI[1];
            set => NPC.localAI[1] = value;
        }

        private float OrbitAngle
        {
            get => NPC.localAI[2];
            set => NPC.localAI[2] = value;
        }

        private MinionPhase Phase
        {
            get => (MinionPhase)NPC.ai[Slot_Phase];
            set => NPC.ai[Slot_Phase] = (float)value;
        }

        private NPC Owner =>
            Main.npc.IndexInRange((int)NPC.ai[Slot_Owner]) ? Main.npc[(int)NPC.ai[Slot_Owner]] : null;

        //YUMMY YUMMY JELLY
        private float _jellyStretch = 1f;
        private float _jellyStretchVelocity;
        private float _jellyFacing;

        private Vector2 _trailAnchor;
        private bool _trailAnchorSet;

        public override void SetDefaults()
        {
            NPC.width = 22;
            NPC.height = 22;

            NPC.damage = 28;
            NPC.defense = 0;
            NPC.lifeMax = 5;

            NPC.dontTakeDamage = true;
            NPC.HitSound = null;
            NPC.DeathSound = null;

            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.knockBackResist = 0f;
            NPC.aiStyle = -1;

            NPC.color = new Color(0, 160, 255, 255);
        }

        public void Initialize(int ownerWhoAmI, int returnDelay)
        {
            NPC.ai[Slot_Owner] = ownerWhoAmI;
            NPC.ai[Slot_Phase] = (float)MinionPhase.Attacking;
            NPC.ai[Slot_ReturnCountdown] = returnDelay;
            NPC.ai[Slot_AttackVariant] = Main.rand.Next(2);

            SubPhase = AttackSubPhase.Orbit;
            SubPhaseTimer = 0f;
            OrbitAngle = Main.rand.NextFloat(MathHelper.TwoPi);

            NPC.netUpdate = true;
        }

        public override void AI()
        {
            NPC owner = Owner;

            if (owner == null || !owner.active || owner.type != NPCID.KingSlime)
            {
                //fade out if the owner is gone in this situation itll be the GOAT kingslime
                FizzleOut();
                return;
            }

            NPC.TargetClosest(false);
            Player target = Main.player.IndexInRange(NPC.target) ? Main.player[NPC.target] : null;

            UpdateJellySquashAndStretch();

            if (Phase == MinionPhase.Attacking)
            {
                ref float countdown = ref NPC.ai[Slot_ReturnCountdown];
                countdown--;

                if (countdown <= 0f || target == null || !target.active || target.dead)
                {
                    Phase = MinionPhase.Returning;
                    NPC.netUpdate = true;
                }
                else
                {
                    RunAttackLoop(target);
                }
            }
            else
            {
                RunReturn(owner);
            }
        }

        private void RunAttackLoop(Player target)
        {
            SubPhaseTimer++;

            switch (SubPhase)
            {
                case AttackSubPhase.Orbit:
                    OrbitTick(target);
                    break;
                case AttackSubPhase.Brace:
                    BraceTick(target);
                    break;
                case AttackSubPhase.Strike:
                    StrikeTick();
                    break;
                case AttackSubPhase.Reposition:
                    RepositionTick(target);
                    break;
            }
        }

        private void OrbitTick(Player target)
        {
            OrbitAngle += OrbitAngularSpeed;
            Vector2 desired = target.Center + OrbitAngle.ToRotationVector2() * OrbitRadius;
            NPC.velocity = (desired - NPC.Center) * 0.2f;
            NPC.rotation += 0.2f;

            if (!Main.dedServ && SubPhaseTimer % 3f == 0f)
            {
                BossParticleSystem.SandStreaks.Create(new ParticleInfo(
                    position: NPC.Center.ToNumerics(),
                    velocity: (NPC.velocity * 0.3f).ToNumerics(),
                    rotation: 0f,
                    scale: new SystemVector2(16f, 5f),
                    color: new Color(0, 170, 255, 255),
                    duration: 14
                ));
            }

            if (SubPhaseTimer >= OrbitDuration)
            {
                SubPhase = AttackSubPhase.Brace;
                SubPhaseTimer = 0f;
            }
        }

        private void BraceTick(Player target)
        {
            NPC.velocity *= 0.8f;
            NPC.rotation += 0.5f;

            if (!Main.dedServ && SubPhaseTimer % 2f == 0f)
            {
                BossParticleSystem.GroundWarnings.Create(new ParticleInfo(
                    position: NPC.Center.ToNumerics(),
                    velocity: SystemVector2.Zero,
                    rotation: 0f,
                    scale: new SystemVector2(30f, 30f),
                    color: new Color(0, 200, 255, 255),
                    duration: 6
                ));
            }

            if (SubPhaseTimer >= BraceDuration)
            {
                SubPhase = AttackSubPhase.Strike;
                SubPhaseTimer = 0f;

                NPC.velocity = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitX) * StrikeSpeed;
                ResetTrailAnchor();

                _jellyStretch = 0.6f;
                _jellyStretchVelocity = 0.85f;

                NPC.netUpdate = true;
            }
        }

        private void StrikeTick()
        {
            NPC.rotation += 0.4f;

            SpawnTrailSegment(new Color(0, 170, 255, 255), 6f, 14);

            if (!Main.dedServ && Main.rand.NextBool(2))
            {
                BossParticleSystem.EmberBursts.Create(new ParticleInfo(
                    position: NPC.Center.ToNumerics(),
                    velocity: (-NPC.velocity * 0.15f).ToNumerics(),
                    rotation: 0f,
                    scale: new SystemVector2(9f, 9f),
                    color: new Color(120, 210, 255, 255),
                    duration: 14
                ));
            }

            if (SubPhaseTimer >= StrikeDuration)
            {
                NPC.velocity *= 0.4f;
                SubPhase = AttackSubPhase.Reposition;
                SubPhaseTimer = 0f;
            }
        }

        private void RepositionTick(Player target)
        {
            NPC.velocity *= 0.9f;
            NPC.rotation *= 0.9f;

            if (SubPhaseTimer >= RepositionDuration)
            {
                OrbitAngle = (NPC.Center - target.Center).ToRotation();
                SubPhase = AttackSubPhase.Orbit;
                SubPhaseTimer = 0f;
            }
        }

        private void RunReturn(NPC owner)
        {
            Vector2 toOwner = owner.Center - NPC.Center;

            if (toOwner.Length() <= ArrivalDistance)
            {
                Absorb(owner);
                return;
            }

            NPC.velocity = toOwner.SafeNormalize(Vector2.UnitY) * ReturnSpeed;
            NPC.rotation += 0.25f;

            SpawnTrailSegment(new Color(0, 170, 255, 255), 5f, 12);
        }

        private void Absorb(NPC owner)
        {
            if (!Main.dedServ)
            {
                for (int i = 0; i < 8; i++)
                {
                    BossParticleSystem.EmberBursts.Create(new ParticleInfo(
                        position: owner.Center.ToNumerics(),
                        velocity: Main.rand.NextVector2Circular(2f, 2f).ToNumerics(),
                        rotation: 0f,
                        scale: new SystemVector2(10f, 10f),
                        color: new Color(0, 170, 255, 255),
                        duration: 16
                    ));
                }
            }

            NPC.active = false;
        }

        private void FizzleOut()
        {
            NPC.velocity *= 0.9f;
            NPC.alpha += 15;

            if (NPC.alpha >= 255)
                NPC.active = false;
        }

        private void UpdateJellySquashAndStretch()
        {
            float speed = NPC.velocity.Length();

            if (speed > 0.5f)
                _jellyFacing = NPC.velocity.ToRotation();

            float targetStretch = 1f + MathHelper.Clamp(speed * 0.02f, 0f, 0.55f);

            if (Phase == MinionPhase.Attacking && SubPhase == AttackSubPhase.Brace)
                targetStretch = 0.65f;

            float springStrength = 0.35f;
            float damping = 0.72f;

            float delta = targetStretch - _jellyStretch;
            _jellyStretchVelocity += delta * springStrength;
            _jellyStretchVelocity *= damping;
            _jellyStretch += _jellyStretchVelocity;
        }


        private void SpawnTrailSegment(Color color, float width, int duration = 16)
        {
            if (Main.dedServ)
                return;

            Vector2 currentCenter = NPC.Center;

            if (!_trailAnchorSet)
            {
                ResetTrailAnchor();
                return;
            }

            Vector2 delta = currentCenter - _trailAnchor;
            float distance = delta.Length();

            if (distance > 1f && distance < 200f)
            {
                Vector2 midpoint = _trailAnchor + delta * 0.5f;
                Vector2 direction = delta / distance;

                BossParticleSystem.SandStreaks.Create(new ParticleInfo(
                    position: midpoint.ToNumerics(),
                    velocity: (direction * 0.5f).ToNumerics(),
                    rotation: 0f,
                    scale: new SystemVector2(distance + 6f, width),
                    color: color,
                    duration: duration
                ));
            }

            _trailAnchor = currentCenter;
        }

        private void ResetTrailAnchor()
        {
            _trailAnchor = NPC.Center;
            _trailAnchorSet = true;
        }

        // Only actually able to land a hit while it's mid dash 
        public override bool CanHitPlayer(Player target, ref int cooldownSlot)
        {
            return Phase == MinionPhase.Attacking && SubPhase == AttackSubPhase.Strike;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo hurtInfo)
        {
            if (Main.dedServ)
                return;

            for (int i = 0; i < 6; i++)
            {
                BossParticleSystem.EmberBursts.Create(new ParticleInfo(
                    position: NPC.Center.ToNumerics(),
                    velocity: Main.rand.NextVector2Circular(3f, 3f).ToNumerics(),
                    rotation: 0f,
                    scale: new SystemVector2(8f, 8f),
                    color: Color.Cyan,
                    duration: 14
                ));
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            Texture2D texture = TextureAssets.Npc[NPC.type].Value;

            float stretch = MathHelper.Clamp(_jellyStretch, 0.55f, 1.9f);
            float squish = MathHelper.Clamp(1f / stretch, 0.55f, 1.9f);

            Vector2 origin = texture.Size() * 0.5f;
            Vector2 drawPos = NPC.Center - screenPos;

            spriteBatch.Draw(
                texture,
                drawPos,
                null,
                NPC.GetAlpha(drawColor),
                _jellyFacing,
                origin,
                new Vector2(stretch, squish),
                SpriteEffects.None,
                0f
            );

            return false;
        }
    }
}