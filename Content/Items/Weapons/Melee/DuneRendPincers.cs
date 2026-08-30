using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace ShatteredIllusion.Content.Items.Weapons.Melee
{
    public class DuneRendPincers : ModItem
    {
        public int NextComboStage = 0;
        private int comboExpireTimer = 0;

        public override void SetDefaults()
        {
            Item.width = 42;
            Item.height = 42;

            Item.damage = 21;
            Item.DamageType = DamageClass.Melee;
            Item.knockBack = 5f;
            Item.crit = 4;

         
            Item.useTime = 20;
            Item.useAnimation = 20;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;

            Item.noMelee = true;      
            Item.noUseGraphic = true; 

            Item.shoot = ModContent.ProjectileType<DuneRendPincersProjectile>();
            Item.shootSpeed = 1f; 

            Item.value = Item.sellPrice(gold: 5);
            Item.rare = ItemRarityID.LightRed;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI, NextComboStage);

            NextComboStage = (NextComboStage + 1) % 3;
            comboExpireTimer = 0;

            return false; 
        }

        public override void UpdateInventory(Player player)
        {
            if (comboExpireTimer++ >= 120)
                NextComboStage = 0;
        }

        public override bool MeleePrefix() => true;
    }

    internal class DuneRendPincersProjectile : ModProjectile
    {
        public override string Texture => "ShatteredIllusion/Content/Items/Weapons/Melee/DuneRendPincersHeld";

        private enum Stage { Overhead = 0, Underhand = 1, Dash = 2 }
        private Stage CurrentStage => (Stage)Projectile.ai[0];

        private ref float Timer => ref Projectile.ai[1];
        private ref float Progress => ref Projectile.localAI[0]; 
        private ref float Size => ref Projectile.localAI[1];

        private const float SwingRange = MathHelper.Pi * 0.9f; 
        private const float WindupTime = 6f;
        private const float SwingTime = 10f;
        private const float FadeTime = 6f;
        private const float DashSpeed = 11f;
        private const float HeldSpriteScale = 1.5f;  
        private const float DashKnockbackMult = 2.2f; 

        private float InitialAngle;
        private Player Owner => Main.player[Projectile.owner];

        public override void SetDefaults()
        {
            Projectile.width = 42;
            Projectile.height = 42;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.timeLeft = 10000;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.ownerHitCheck = true;
        }

        public override void OnSpawn(IEntitySource source)
        {
            Projectile.spriteDirection = Owner.direction;

            switch (CurrentStage)
            {
                case Stage.Overhead: 
                    InitialAngle = -MathHelper.PiOver2 - SwingRange * 0.5f;
                    break;

                case Stage.Underhand: 
                    InitialAngle = MathHelper.PiOver2 + SwingRange * 0.5f;
                    break;

                default: 
                    InitialAngle = -SwingRange * 0.5f;
                    Owner.velocity.X = DashSpeed * Owner.direction;
                    break;
            }
        }

        public override void AI()
        {
            Owner.itemAnimation = 2;
            Owner.itemTime = 2;

            if (CurrentStage == Stage.Dash)
            {
                Owner.immune = true;
                Owner.immuneTime = 2;
            }

            if (!Owner.active || Owner.dead || Owner.noItems || Owner.CCed)
            {
                Projectile.Kill();
                return;
            }

            float sweep = CurrentStage == Stage.Underhand ? -SwingRange : SwingRange;

            if (Timer < WindupTime)
            {
                Size = MathHelper.SmoothStep(0f, 1f, Timer / WindupTime);
            }
            else if (Timer < WindupTime + SwingTime)
            {
                float t = (Timer - WindupTime) / SwingTime;
                Progress = MathHelper.SmoothStep(0f, sweep, t);
            }
            else if (Timer < WindupTime + SwingTime + FadeTime)
            {
                float t = (Timer - WindupTime - SwingTime) / FadeTime;
                Size = 1f - MathHelper.SmoothStep(0f, 1f, t);
            }
            else
            {
                Projectile.Kill();
                return;
            }

            PositionSword();
            Timer++;
        }

        private void PositionSword()
        {
            Projectile.rotation = InitialAngle + Owner.direction * Progress;

            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, Projectile.rotation - MathHelper.PiOver2);
            Vector2 handPosition = Owner.GetFrontHandPosition(Player.CompositeArmStretchAmount.Full, Projectile.rotation - MathHelper.PiOver2);

            if (Owner.gravDir == -1f)
            {
                Projectile.rotation = -Projectile.rotation;
                handPosition.Y = Owner.Bottom.Y + (Owner.position.Y - handPosition.Y);
            }

            handPosition.Y += Owner.gfxOffY;
            Projectile.Center = handPosition;
            Projectile.scale = Size * Owner.GetAdjustedItemScale(Owner.HeldItem) * HeldSpriteScale;
            Owner.heldProj = Projectile.whoAmI;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Type].Value;

            Vector2 origin;
            float rotationOffset;
            SpriteEffects effects = SpriteEffects.None;

            if (Owner.direction > 0)
            {
                origin = new Vector2(0, texture.Height);
                rotationOffset = MathHelper.ToRadians(45f);
            }
            else
            {
                origin = new Vector2(texture.Width, texture.Height);
                rotationOffset = MathHelper.ToRadians(135f);
                effects = SpriteEffects.FlipHorizontally;
            }


            Main.spriteBatch.Draw(texture, Projectile.Center - Main.screenPosition, null,
                lightColor * Projectile.Opacity, Projectile.rotation + rotationOffset, origin, Projectile.scale, effects, 0f);

            return false;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            if (CurrentStage == Stage.Dash)
            {
                Rectangle dashHitbox = Owner.Hitbox;
                dashHitbox.Inflate(10, 10);
                return dashHitbox.Intersects(targetHitbox);
            }

            Vector2 start = Owner.MountedCenter;
            Vector2 end = start + Projectile.rotation.ToRotationVector2() * (Projectile.Size.Length() * Projectile.scale);
            float collisionPoint = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), start, end, 15f * Projectile.scale, ref collisionPoint);
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            if (CurrentStage == Stage.Dash)
                modifiers.Knockback *= DashKnockbackMult;
        }
    }
}