using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ShatteredIllusion.Common.Systems;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace ShatteredIllusion.Content.Items.Weapons.Melee
{
    /// <summary>
    /// A vine-wrapped boomerang. As it flies it drops thorn seeds that stay behind and prick
    /// anything that walks into them, so every throw lays a short trail of caltrops. Throw it
    /// across a doorway or in front of a charging enemy. Max 2 boomerangs and 6 seeds out.
    ///
    /// Balance target (pre-Eye of Cthulhu): ~30 dps from the boomerang, a bit more from the seeds.
    /// Sprite: Thornrang.png is 46x50 (the 23x25 original scaled 2x). The projectile reuses it.
    /// </summary>
    public class Thornrang : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 46;
            Item.height = 50;

            Item.damage = 12;
            Item.DamageType = DamageClass.Melee;
            Item.knockBack = 4f;
            Item.crit = 4;

            Item.useTime = 22;
            Item.useAnimation = 22;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;

            Item.noMelee = true;
            Item.noUseGraphic = true;

            Item.shoot = ModContent.ProjectileType<ThornrangProjectile>();
            Item.shootSpeed = 11f;

            Item.value = Item.sellPrice(silver: 80);
            Item.rare = ItemRarityID.Green;
        }

        // PLACEHOLDER recipe using vanilla materials - swap in your own.
        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ItemID.Wood, 20)
                .AddIngredient(ItemID.Vine, 4)
                .AddTile(TileID.Anvils)
                .Register();
        }

        public override bool CanUseItem(Player player)
        {
            return player.ownedProjectileCounts[Item.shoot] < 2;
        }
    }

    public class ThornrangProjectile : ModProjectile
    {
        // Re-uses the item's sprite so you only need one .png.
        public override string Texture => "ShatteredIllusion/Content/Items/Weapons/Melee/Thornrang";

        private const int OutTime = 26;
        private const int SeedEvery = 9;
        private const int MaxSeeds = 6;
        private const float ReturnSpeed = 16f;
        private const float SpriteDrawSize = 38f; // on-screen size in pixels, whatever the .png size is

        private static readonly Color LeafGreen = new Color(130, 255, 110);
        private static readonly Color DarkGreen = new Color(30, 130, 60);

        // ai[0]: 0 = flying out, 1 = coming back
        private ref float Returning => ref Projectile.ai[0];
        private ref float Timer => ref Projectile.localAI[0];

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 8;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults()
        {
            Projectile.width = 34;
            Projectile.height = 34;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 600;

            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 12;
        }

        public override void AI()
        {
            Player owner = Main.player[Projectile.owner];
            if (!owner.active || owner.dead)
            {
                Projectile.Kill();
                return;
            }

            Timer++;
            Projectile.rotation += 0.5f;

            if (Timer % 18f == 0f)
                SoundEngine.PlaySound(SoundID.Item7 with { Volume = 0.5f }, Projectile.Center);

            if (Returning == 0f)
            {
                if (Timer > OutTime - 8f)
                    Projectile.velocity *= 0.9f;

                if (Timer >= OutTime)
                    StartReturn();
            }
            else
            {
                Vector2 toPlayer = owner.Center - Projectile.Center;
                if (toPlayer.Length() < 26f)
                {
                    Projectile.Kill(); // caught
                    return;
                }

                Projectile.velocity += toPlayer.SafeNormalize(Vector2.Zero) * 1.2f;
                if (Projectile.velocity.Length() > ReturnSpeed)
                    Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.Zero) * ReturnSpeed;
            }

            DropSeeds();

            if (Main.rand.NextBool(4))
            {
                Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(14f, 14f),
                    DustID.Grass, Vector2.Zero, 0, default, 0.9f);
                d.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, 0.15f, 0.4f, 0.15f);
        }

        private void StartReturn()
        {
            Returning = 1f;
            Projectile.tileCollide = false;
            Projectile.netUpdate = true;
        }

        private void DropSeeds()
        {
            if (Projectile.owner != Main.myPlayer || Timer % SeedEvery != 0f)
                return;

            int seedType = ModContent.ProjectileType<ThornSeed>();
            if (Main.player[Projectile.owner].ownedProjectileCounts[seedType] >= MaxSeeds)
                return;

            Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Vector2.Zero, seedType,
                System.Math.Max(1, (int)(Projectile.damage * 0.4f)), 0f, Projectile.owner);
        }

        public override bool OnTileCollide(Vector2 oldVelocity)
        {
            if (Returning == 0f)
            {
                SoundEngine.PlaySound(SoundID.Item10, Projectile.Center);
                WeaponFX.DustCone(Projectile.Center, -oldVelocity, 0.9f, DustID.Grass, 7, 2f, 5f, 1.1f);
                Projectile.velocity = -oldVelocity * 0.3f;
                StartReturn();
            }

            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            Vector2 away = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
            WeaponFX.DustCone(Projectile.Center, away, 0.8f, DustID.Grass, 6, 2f, 5f, 1.1f);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Vector2 origin = texture.Size() * 0.5f;
            float drawScale = SpriteDrawSize / System.Math.Max(texture.Width, texture.Height);

            WeaponFX.DrawGlowTrail(Projectile, LeafGreen, DarkGreen, 0.3f, 0.6f);

            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, lightColor * Projectile.Opacity,
                Projectile.rotation, origin, drawScale, SpriteEffects.None, 0);

            return false;
        }
    }

    /// <summary>A little thorn caltrop left behind by the boomerang. Pricks 3 times, then withers.</summary>
    public class ThornSeed : ModProjectile
    {
        // Drawn entirely in code, so this is just a placeholder texture that's never shown.
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.WoodenArrowFriendly;

        private static readonly Color Thorn = new Color(90, 200, 80);
        private static readonly Color Dark = new Color(35, 100, 45);

        public override void SetDefaults()
        {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 3;
            Projectile.timeLeft = 100;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;

            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 30;
        }

        public override void AI()
        {
            Projectile.rotation += 0.02f;

            if (Main.rand.NextBool(12))
            {
                Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(6f, 6f), DustID.Grass,
                    Vector2.Zero, 0, default, 0.7f);
                d.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            WeaponFX.DustRing(Projectile.Center, DustID.Grass, 5, 2.5f, 0.9f);
        }

        public override void OnKill(int timeLeft)
        {
            WeaponFX.DustRing(Projectile.Center, DustID.Grass, 5, 2f, 0.9f);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            float fade = MathHelper.Clamp(Projectile.timeLeft / 20f, 0f, 1f);
            Color thorn = WeaponFX.Additive(Thorn, fade);
            Color dark = WeaponFX.Additive(Dark, 0.9f * fade);

            // three spikes radiating from the centre
            for (int i = 0; i < 3; i++)
            {
                Vector2 spike = (Projectile.rotation + MathHelper.TwoPi * i / 3f - MathHelper.PiOver2).ToRotationVector2() * 8f;
                WeaponFX.DrawLine(Projectile.Center, Projectile.Center + spike, dark, 4f);
                WeaponFX.DrawLine(Projectile.Center, Projectile.Center + spike, thorn, 2f);
            }

            WeaponFX.DrawGlow(Projectile.Center - Main.screenPosition, WeaponFX.Additive(Thorn, 0.25f * fade), 0.3f);
            return false;
        }
    }
}
