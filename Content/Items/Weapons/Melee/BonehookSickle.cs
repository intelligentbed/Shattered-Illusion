using Microsoft.Xna.Framework;
using ShatteredIllusion.Common.Systems;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace ShatteredIllusion.Content.Items.Weapons.Melee
{
    /// <summary>
    /// A curved bone sickle. Every 3rd swing also throws a crescent of reaping energy that
    /// flies forward and cuts through up to 3 enemies - so a steady rhythm of swings keeps
    /// hitting things at range as well as up close.
    ///
    /// Balance target (pre-Eye of Cthulhu): ~28 dps from swings, a little more from crescents.
    /// Sprite: BonehookSickle.png is 70x44 (the 35x22 original scaled 2x).
    /// </summary>
    public class BonehookSickle : ModItem
    {
        private const int CrescentEvery = 3;
        private int swingCount;

        public override void SetDefaults()
        {
            Item.width = 70;
            Item.height = 44;

            Item.damage = 13;
            Item.DamageType = DamageClass.Melee;
            Item.knockBack = 4.5f;
            Item.crit = 6;

            Item.useTime = 28;
            Item.useAnimation = 28;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;

            Item.shoot = ModContent.ProjectileType<ReapingCrescent>();
            Item.shootSpeed = 10f;

            Item.value = Item.sellPrice(silver: 80);
            Item.rare = ItemRarityID.Green;
        }

        // PLACEHOLDER recipe using vanilla materials - swap in your own.
        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ItemID.Bone, 15)
                .AddRecipeGroup(RecipeGroupID.IronBar, 6)
                .AddTile(TileID.Anvils)
                .Register();
        }

        public override void MeleeEffects(Player player, Rectangle hitbox)
        {
            if (!Main.rand.NextBool(3))
                return;

            Dust d = Dust.NewDustDirect(new Vector2(hitbox.X, hitbox.Y), hitbox.Width, hitbox.Height,
                DustID.Shadowflame, 0f, 0f, 100, default, 1f);
            d.noGravity = true;
            d.velocity *= 0.3f;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position,
            Vector2 velocity, int type, int damage, float knockback)
        {
            swingCount++;
            if (swingCount % CrescentEvery != 0)
                return false; // normal swing, no crescent

            swingCount = 0;

            Projectile.NewProjectile(source, player.MountedCenter, velocity, type, (int)(damage * 0.8f), knockback,
                player.whoAmI);

            SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.2f }, player.Center);
            WeaponFX.DustCone(player.MountedCenter + velocity.SafeNormalize(Vector2.UnitX) * 30f, velocity, 0.5f,
                DustID.Shadowflame, 8, 2f, 5f, 1.2f);

            return false;
        }
    }

    public class ReapingCrescent : ModProjectile
    {
        // Drawn entirely in code, so this is just a placeholder texture that's never shown.
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.WoodenArrowFriendly;

        private static readonly Color Crimson = new Color(210, 60, 90);
        private static readonly Color Violet = new Color(130, 80, 200);
        private static readonly Color Bone = new Color(255, 235, 220);

        private ref float Timer => ref Projectile.localAI[0];

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 6;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults()
        {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 3;
            Projectile.timeLeft = 36;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;

            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1; // each enemy is cut once
        }

        public override void AI()
        {
            Timer++;

            Projectile.velocity *= 0.97f;
            if (Projectile.velocity != Vector2.Zero)
                Projectile.rotation = Projectile.velocity.ToRotation();

            if (Main.rand.NextBool(2))
            {
                Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(14f, 14f),
                    DustID.Shadowflame, -Projectile.velocity * 0.1f, 0, default, 0.9f);
                d.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, 0.5f, 0.2f, 0.4f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            WeaponFX.DustCone(Projectile.Center, -Projectile.velocity, 1f, DustID.Shadowflame, 6, 1.5f, 4f, 1.1f);
        }

        public override void OnKill(int timeLeft)
        {
            WeaponFX.DustRing(Projectile.Center, DustID.Shadowflame, 8, 3f, 1.1f);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Vector2 center = Projectile.Center - Main.screenPosition;
            float fade = MathHelper.Clamp(Projectile.timeLeft / 10f, 0f, 1f);

            WeaponFX.DrawGlowTrail(Projectile, Crimson, Violet, 0.4f, 0.6f * fade);

            // a crescent: a curved arc bulging forward, thick in the middle and thin at the tips
            const int segments = 10;
            const float halfArc = 1.05f;
            const float radius = 17f;

            Vector2 previous = Vector2.Zero;
            for (int i = 0; i <= segments; i++)
            {
                float u = i / (float)segments;
                float angle = Projectile.rotation + MathHelper.Lerp(-halfArc, halfArc, u);
                Vector2 point = Projectile.Center + angle.ToRotationVector2() * radius - Projectile.rotation.ToRotationVector2() * radius * 0.45f;
                float thickness = 2f + 7f * (float)System.Math.Sin(u * MathHelper.Pi);

                if (i > 0)
                {
                    WeaponFX.DrawLine(previous, point, WeaponFX.Additive(Violet, 0.8f * fade), thickness + 4f);
                    WeaponFX.DrawLine(previous, point, WeaponFX.Additive(Crimson, 0.95f * fade), thickness + 1f);
                    WeaponFX.DrawLine(previous, point, WeaponFX.Additive(Bone, fade), thickness * 0.5f);
                }

                previous = point;
            }

            WeaponFX.DrawGlow(center, WeaponFX.Additive(Crimson, 0.4f * fade), 1f);
            return false;
        }
    }
}
