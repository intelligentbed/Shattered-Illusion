using Microsoft.Xna.Framework;
using ShatteredIllusion.Common.Systems;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace ShatteredIllusion.Content.Items.Weapons.Ranger
{
    /// <summary>
    /// Lobs a heavy mossy log. The log tumbles and bounces up to 3 times, hurting every enemy
    /// it passes through and kicking up leaves on each bounce - so it is good against things
    /// on the ground and around corners. Uses no ammo.
    ///
    /// Balance target (pre-Eye of Cthulhu): ~19 dps on one enemy, more if the log bounces
    /// through a group. Sprite: MosslogLauncher.png is 68x36 (the 34x18 original scaled 2x).
    /// </summary>
    public class MosslogLauncher : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 68;
            Item.height = 36;

            Item.damage = 14;
            Item.DamageType = DamageClass.Ranged;
            Item.knockBack = 5f;
            Item.crit = 4;

            Item.useTime = 44;
            Item.useAnimation = 44;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.autoReuse = true;
            Item.UseSound = SoundID.Item61;

            Item.shoot = ModContent.ProjectileType<MosslogProjectile>();
            Item.shootSpeed = 9f;

            Item.value = Item.sellPrice(silver: 80);
            Item.rare = ItemRarityID.Green;
        }

        // PLACEHOLDER recipe using vanilla materials - swap in your own.
        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ItemID.Wood, 30)
                .AddIngredient(ItemID.Acorn, 10)
                .AddTile(TileID.WorkBenches)
                .Register();
        }

        public override Vector2? HoldoutOffset() => new Vector2(-6f, 0f);

        public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity,
            ref int type, ref int damage, ref float knockback)
        {
            Vector2 muzzle = position + velocity.SafeNormalize(Vector2.UnitX * player.direction) * 34f;
            if (Collision.CanHit(position, 0, 0, muzzle, 0, 0))
                position = muzzle;

            // a small upward lob so the log arcs instead of dropping straight away
            velocity.Y -= 1.5f;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position,
            Vector2 velocity, int type, int damage, float knockback)
        {
            WeaponFX.DustCone(position, velocity, 0.5f, DustID.Grass, 6, 1.5f, 4f, 1f);
            WeaponFX.DustCone(position, velocity, 0.4f, DustID.WoodFurniture, 4, 1f, 3f, 1f);
            return true;
        }
    }

    public class MosslogProjectile : ModProjectile
    {
        // Drawn entirely in code, so this is just a placeholder texture that's never shown.
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.WoodenArrowFriendly;

        private const int MaxBounces = 3;
        private const float Gravity = 0.28f;

        private static readonly Color Bark = new Color(115, 80, 45);
        private static readonly Color Moss = new Color(80, 150, 60);
        private static readonly Color LeafGreen = new Color(130, 255, 110);
        private static readonly Color DarkGreen = new Color(30, 130, 60);

        private ref float Bounces => ref Projectile.localAI[0];

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 6;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults()
        {
            Projectile.width = 18;
            Projectile.height = 18;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 4;
            Projectile.timeLeft = 150;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;

            // after a bounce it may hit the same enemy again, but not every frame
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
        }

        public override void AI()
        {
            Projectile.velocity.Y = System.Math.Min(Projectile.velocity.Y + Gravity, 14f);
            Projectile.rotation += 0.22f * (Projectile.velocity.X >= 0f ? 1f : -1f);

            if (Main.rand.NextBool(4))
            {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Grass,
                    Main.rand.NextVector2Circular(0.6f, 0.6f), 0, default, 0.8f);
                d.noGravity = true;
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity)
        {
            Bounces++;

            if (Bounces > MaxBounces)
                return true; // let it die

            // collision has already zeroed the axis we hit, so flip whichever one changed
            if (Projectile.velocity.X != oldVelocity.X)
                Projectile.velocity.X = -oldVelocity.X * 0.8f;
            if (Projectile.velocity.Y != oldVelocity.Y)
                Projectile.velocity.Y = -oldVelocity.Y * 0.6f;

            SoundEngine.PlaySound(SoundID.Item10 with { Pitch = -0.3f, Volume = 0.7f }, Projectile.Center);
            WeaponFX.DustCone(Projectile.Center, -oldVelocity, 1.2f, DustID.Grass, 7, 1.5f, 4f, 1.1f);
            WeaponFX.DustCone(Projectile.Center, -oldVelocity, 1.2f, DustID.WoodFurniture, 4, 1f, 3f, 1f);

            return false;
        }

        public override void OnKill(int timeLeft)
        {
            WeaponFX.DustRing(Projectile.Center, DustID.Grass, 10, 3f, 1.1f);
            WeaponFX.DustRing(Projectile.Center, DustID.WoodFurniture, 6, 2f, 1f);
        }

        private static Color Shade(Color color, Color light)
        {
            return new Color(color.R * light.R / 255, color.G * light.G / 255, color.B * light.B / 255);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Vector2 forward = Projectile.rotation.ToRotationVector2();
            Vector2 center = Projectile.Center - Main.screenPosition;

            WeaponFX.DrawGlowTrail(Projectile, LeafGreen, DarkGreen, 0.3f, 0.5f);

            // a log: thick bark body with a mossy stripe along it
            WeaponFX.DrawLine(Projectile.Center - forward * 10f, Projectile.Center + forward * 10f, Shade(Bark, lightColor), 9f);
            WeaponFX.DrawLine(Projectile.Center - forward * 8f, Projectile.Center + forward * 7f,
                Shade(Moss, lightColor), 4f);
            WeaponFX.DrawLine(Projectile.Center + forward * 10f, Projectile.Center + forward * 11f,
                Shade(new Color(200, 160, 100), lightColor), 8f); // pale cut end

            return false;
        }
    }
}
