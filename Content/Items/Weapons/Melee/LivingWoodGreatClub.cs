using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace ShatteredIllusion.Content.Items.Weapons.Melee
{
    public class LivingWoodGreatClub : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 64;
            Item.height = 64;

            Item.damage = 40;
            Item.DamageType = DamageClass.Melee;
            Item.knockBack = 10f;

            Item.useTime = 20;
            Item.useAnimation = 20;
            Item.reuseDelay = 20;
            Item.useStyle = ItemUseStyleID.Shoot;

            Item.shoot = ModContent.ProjectileType<LivingWoodGreatClubHoldout>();
            Item.shootSpeed = 1f;

            Item.noUseGraphic = true;
            Item.noMelee = true;
            Item.channel = true;
            Item.useTurn = false;

            Item.autoReuse = false;

            Item.UseSound = SoundID.Item1;

            Item.value = Item.buyPrice(silver: 50);
            Item.rare = ItemRarityID.Green;
        }
    }

    public class LivingWoodGreatClubHoldout : ModProjectile
    {
        private enum Phase
        {
            Charging,
            Swinging
        }

        private int baseDamage = -1;
        private float baseKnockback = -1f;

        private bool hasSlammed;

        private const int MaxChargeTime = 50;
        private const int SwingTime = 30;

        private const float ClubHeadReach = 78f;
        private const float ClubHeadCheckSize = 10f;

        private const float MinDamageMultiplier = 0.6f;
        private const float MaxDamageMultiplier = 1.8f;
        private const float MinKnockbackMultiplier = 0.5f;
        private const float MaxKnockbackMultiplier = 1.6f;

        private const float DamageStartProgress = 0.4f;
        private const float HandReachDistance = 13f;

        private const int PostSwingCooldown = 20;

        public override string Texture =>
            "ShatteredIllusion/Content/Items/Weapons/Melee/LivingWoodGreatClub";

        private const float SpriteVerticalNudge = 6f;

        private static readonly Vector2 GripPixel = new(5f, 64f);

        private const float SpriteNeutralAngle = -MathHelper.PiOver4; // -45°
        private const float SpriteRotationOffset = -SpriteNeutralAngle; // +45°

        public override void SetDefaults()
        {
            Projectile.width = 46;
            Projectile.height = 89;

            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ownerHitCheck = true;

            Projectile.DamageType = DamageClass.Melee;
            Projectile.timeLeft = 5;
        }

        public override bool? CanDamage()
        {
            if ((Phase)Projectile.localAI[0] != Phase.Swinging)
                return false;

            float progress = Projectile.ai[0] / SwingTime;
            return progress >= DamageStartProgress;
        }

        public override void AI()
        {
            Player player = Main.player[Projectile.owner];

            if (!player.active || player.dead)
            {
                Projectile.Kill();
                return;
            }

            // base stats on the first tick
            if (baseDamage < 0)
            {
                baseDamage = Projectile.damage;
                baseKnockback = Projectile.knockBack;
            }

            player.heldProj = Projectile.whoAmI;
            player.itemTime = 2;
            player.itemAnimation = 2;

            if (Projectile.ai[1] == 0)
            {
                int initialDir = Main.MouseWorld.X >= player.Center.X ? 1 : -1;
                Projectile.ai[1] = initialDir;
            }

            int direction = (int)Projectile.ai[1];
            player.ChangeDir(direction);

            var phase = (Phase)Projectile.localAI[0];
            float rotation;

            if (phase == Phase.Charging)
            {
                rotation = DoCharging(player, direction);
            }
            else
            {
                rotation = DoSwinging(player, direction, out bool justFinishedSwing);

                if (!hasSlammed && CanDamage() == true && TryGetTileSlamPosition(player, rotation, out Vector2 slamPosition))
                {
                    hasSlammed = true;

                    player.itemAnimation = player.itemTime = PostSwingCooldown;

                    CreateImpact(player, direction, slamPosition);
                    Projectile.Kill();
                    return;
                }

                if (justFinishedSwing)
                {
                    player.itemAnimation = player.itemTime = PostSwingCooldown;
                    Projectile.Kill();
                    return;
                }
            }

            Vector2 gripPosition = player.MountedCenter + rotation.ToRotationVector2() * HandReachDistance;

            Projectile.Center = gripPosition;

            Projectile.rotation = rotation + Mirror(SpriteRotationOffset, direction);
            Projectile.spriteDirection = direction;

            SyncPlayerArm(player, direction);
        }

        /// <summary>
        /// Holds the club coiled further back the longer the button is held,
        /// capping at MaxChargeTime. Releasing the button (or hitting the cap
        /// and then releasing) transitions into the swing.
        /// </summary> im new to this :)
        private float DoCharging(Player player, int direction)
        {
            Projectile.timeLeft = 5;

            Projectile.ai[0] = System.Math.Min(Projectile.ai[0] + 1, MaxChargeTime);
            float chargeRatio = Projectile.ai[0] / MaxChargeTime;

            float startAngle = GetRestAngle(direction);
            float fullChargeAngle = GetFullChargeAngle(direction);
            float rotation = MathHelper.Lerp(startAngle, fullChargeAngle, MathHelper.SmoothStep(0f, 1f, chargeRatio));


            if (chargeRatio >= 1f)
                rotation += (float)System.Math.Sin(Main.GameUpdateCount * 0.3f) * 0.04f;

            if (!player.channel)
            {
                Projectile.localAI[0] = (float)Phase.Swinging;
                Projectile.localAI[1] = chargeRatio;
                Projectile.ai[0] = 0;

                Projectile.damage = (int)(baseDamage * MathHelper.Lerp(MinDamageMultiplier, MaxDamageMultiplier, chargeRatio));
                Projectile.knockBack = baseKnockback * MathHelper.Lerp(MinKnockbackMultiplier, MaxKnockbackMultiplier, chargeRatio);
            }

            return rotation;
        }

        private float DoSwinging(Player player, int direction, out bool justFinishedSwing)
        {
            justFinishedSwing = false;

            Projectile.timeLeft = 5;
            Projectile.ai[0]++;

            float chargeRatio = Projectile.localAI[1];

            float holdAngle = MathHelper.Lerp(GetRestAngle(direction), GetFullChargeAngle(direction), chargeRatio);
            float endAngle = MathHelper.Lerp(GetTapEndAngle(direction), GetFullChargeEndAngle(direction), chargeRatio);

            float progress = MathHelper.SmoothStep(0f, 1f, Projectile.ai[0] / SwingTime);
            float rotation = MathHelper.Lerp(holdAngle, endAngle, progress);

            if (Projectile.ai[0] >= SwingTime)
                justFinishedSwing = true;

            return rotation;
        }


        /// <summary>
        /// Checks whether the head of the club (out past the grip, along the
        /// current swing rotation) is currently overlapping a solid tile.
        /// Returns the head's world position so the impact can be spawned
        /// exactly where contact happened, rather than guessed from the
        /// player's own position.
        /// </summary>
        private bool TryGetTileSlamPosition(Player player, float swingRotation, out Vector2 slamPosition)
        {
            Vector2 gripPosition = player.MountedCenter + swingRotation.ToRotationVector2() * HandReachDistance;
            Vector2 headPosition = gripPosition + swingRotation.ToRotationVector2() * ClubHeadReach;

            slamPosition = headPosition;

            float half = ClubHeadCheckSize / 2f;
            return Collision.SolidCollision(headPosition - new Vector2(half, half), (int)ClubHeadCheckSize, (int)ClubHeadCheckSize);
        }

        private const float RestAngleRight = -0.5236f;        // -30°, held up near the shoulder
        private const float FullChargeAngleRight = -2.618f;   // -150°, raised overhead and back
        private const float TapEndAngleRight = 0.5236f;       // +30°, short quick chop
        private const float FullChargeEndAngleRight = 2.094f; // +120°, full downward follow-through

        private float Mirror(float rightAngle, int direction) =>
            direction > 0 ? rightAngle : MathHelper.Pi - rightAngle;

        private float GetRestAngle(int direction) => Mirror(RestAngleRight, direction);

        private float GetFullChargeAngle(int direction) => Mirror(FullChargeAngleRight, direction);

        private float GetTapEndAngle(int direction) => Mirror(TapEndAngleRight, direction);

        private float GetFullChargeEndAngle(int direction) => Mirror(FullChargeEndAngleRight, direction);

        /// <summary>
        /// Makes the player's front arm/hand actually track the club instead of
        /// staying in its default pose.
        /// </summary>
        private void SyncPlayerArm(Player player, int direction)
        {
            player.itemLocation = Projectile.Center;

            float armRotation = (Projectile.Center - player.MountedCenter).ToRotation();
            if (direction == -1)
                armRotation += MathHelper.Pi;

            player.itemRotation = MathHelper.WrapAngle(armRotation);
            player.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, player.itemRotation);
        }

        /// <summary>
        /// Default projectile drawing centers the entire texture on
        /// Projectile.Center, which only lines up with the hand if the
        /// handle happens to sit exactly at the texture's midpoint. Since
        /// this club's handle is at the bottom-left of the sprite, we draw
        /// it manually with the origin pinned to that grip pixel instead —
        /// so Projectile.Center (the grip) and the drawn handle are always
        /// the same point, no matter how large the texture is, and no
        /// matter which way the player is facing.
        /// </summary>
        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;

            bool flipped = Projectile.spriteDirection == -1;
            SpriteEffects effects = flipped ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            float originX = flipped ? texture.Width - GripPixel.X : GripPixel.X;
            Vector2 origin = new Vector2(originX, GripPixel.Y - SpriteVerticalNudge);

            Vector2 drawPosition = Projectile.Center - Main.screenPosition;

            Main.EntitySpriteDraw(
                texture,
                drawPosition,
                null,
                lightColor,
                Projectile.rotation,
                origin,
                Projectile.scale,
                effects,
                0
            );

            return false;
        }

        private void CreateImpact(Player player, int direction, Vector2 impactPosition)
        {
            if (Main.myPlayer != Projectile.owner)
                return;

            float chargeRatio = Projectile.localAI[1];

            SoundEngine.PlaySound(SoundID.Item70, impactPosition);
            SpawnImpactDust(impactPosition, direction, chargeRatio);
            SpawnRubble(impactPosition, direction, chargeRatio);
        }

        private void SpawnImpactDust(Vector2 impactPosition, int direction, float chargeRatio)
        {
            int dustCount = (int)MathHelper.Lerp(8f, 16f, chargeRatio);

            for (int i = 0; i < dustCount; i++)
            {
                Vector2 dustVelocity = new Vector2(
                    Main.rand.NextFloat(-2.5f, 2.5f) + direction * 1.5f,
                    Main.rand.NextFloat(-3.5f, -0.5f)
                );

                int dust = Dust.NewDust(
                    impactPosition - new Vector2(8f, 4f),
                    16,
                    8,
                    DustID.Dirt,
                    dustVelocity.X,
                    dustVelocity.Y
                );

                Main.dust[dust].noGravity = false;
                Main.dust[dust].scale = Main.rand.NextFloat(1f, 1.6f);
            }
        }

        private void SpawnRubble(Vector2 impactPosition, int direction, float chargeRatio)
        {
            int rubbleType = ModContent.ProjectileType<LivingWoodRubble>();

            // A fully-charged slam kicks up an extra piece of debris
            int pieceCount = chargeRatio >= 1f ? 3 : 2;
            float forceMultiplier = MathHelper.Lerp(0.8f, 1.4f, chargeRatio);

            for (int i = 0; i < pieceCount; i++)
            {
                Vector2 velocity = new Vector2(
                    direction * (3f + i * 2f) * forceMultiplier,
                    (-6f - i * 1.5f) * forceMultiplier
                );

                Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    impactPosition,
                    velocity,
                    rubbleType,
                    Projectile.damage / 2,
                    3f,
                    Projectile.owner
                );
            }
        }
    }

    public class LivingWoodRubble : ModProjectile
    {
        public override string Texture =>
            "ShatteredIllusion/Content/Projectiles/LivingWoodRubble";

        public override void SetDefaults()
        {
            Projectile.width = 22;
            Projectile.height = 18;

            Projectile.friendly = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 90;

            Projectile.tileCollide = true;
            Projectile.DamageType = DamageClass.Melee;
        }

        public override void AI()
        {
            Projectile.velocity.Y += 0.35f;
            Projectile.rotation += Projectile.velocity.X * 0.05f;

            if (Projectile.velocity.Y > 12f)
                Projectile.velocity.Y = 12f;
        }

        public override bool OnTileCollide(Vector2 oldVelocity)
        {
            Projectile.velocity.X *= 0.5f;
            Projectile.velocity.Y = -oldVelocity.Y * 0.35f;

            return false;
        }
    }
}