using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ShatteredIllusion.Common.Systems;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace ShatteredIllusion.Content.Items.Weapons.Ranger
{
    /// <summary>
    /// Turns any bullet into a frost bolt that inflicts Frostburn. Every 4th shot is an Icicle:
    /// it lodges in the enemy it hits, glows hotter and hotter, then SHATTERS into 6 shards.
    /// </summary>
    public class FrostBlaster : ModItem
    {
        private const int IcicleEvery = 4;
        private int shotCount;

        public override void SetDefaults()
        {
            Item.width = 22;
            Item.height = 13;
            Item.scale = 1.6f; // sprite is small, so it's drawn bigger in the hand

            // Balance target: ~28 dps with Musket Balls (gold-tier, pre-Eye of Cthulhu).
            // NOTE: the gun's damage ADDS to the bullet's damage, so keep this number low -
            // it used to be 12 at useTime 15, which worked out to ~145 dps.
            Item.damage = 3;
            Item.DamageType = DamageClass.Ranged;
            Item.knockBack = 2f;

            Item.useTime = 30;
            Item.useAnimation = 30;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.autoReuse = true;
            Item.UseSound = SoundID.Item11;

            Item.shoot = ProjectileID.Bullet; // replaced with the frost bolt in ModifyShootStats
            Item.shootSpeed = 12f;
            Item.useAmmo = AmmoID.Bullet;

            Item.value = Item.sellPrice(gold: 1);
            Item.rare = ItemRarityID.Green;
        }

        // PLACEHOLDER recipe using vanilla materials - swap in your own tundra drops.
        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ItemID.IceBlock, 15)
                .AddRecipeGroup(RecipeGroupID.IronBar, 8)
                .AddTile(TileID.Anvils)
                .Register();
        }

        public override Vector2? HoldoutOffset() => new Vector2(-4f, 0f);

        public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity,
            ref int type, ref int damage, ref float knockback)
        {
            type = ModContent.ProjectileType<FrostBoltProjectile>();

            Vector2 muzzle = position + velocity.SafeNormalize(Vector2.UnitX * player.direction) * 28f;
            if (Collision.CanHit(position, 0, 0, muzzle, 0, 0))
                position = muzzle;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position,
            Vector2 velocity, int type, int damage, float knockback)
        {
            shotCount++;
            bool icicle = shotCount % IcicleEvery == 0;

            if (icicle)
            {
                shotCount = 0;
                velocity *= 0.85f;
                damage = (int)(damage * 1.4f);
                knockback *= 1.5f;
                SoundEngine.PlaySound(SoundID.Item28, position);
            }

            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI,
                ai0: icicle ? FrostBoltProjectile.Icicle : FrostBoltProjectile.Bolt);

            WeaponFX.DustCone(position, velocity, 0.4f, DustID.BlueTorch, icicle ? 8 : 3, 1.5f, 4f, 1f);

            return false;
        }

        public override void PostDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor,
            float rotation, float scale, int whoAmI)
        {
            WeaponFX.DrawWorldAura(Item, rotation, scale, new Color(130, 210, 255));
        }
    }

    /// <summary>One class, three shapes (ai[0]): 0 = bolt, 1 = icicle, 2 = shard.</summary>
    public class FrostBoltProjectile : ModProjectile
    {
        public const int Bolt = 0;
        public const int Icicle = 1;
        public const int Shard = 2;

        // Drawn entirely in code, so this is just a placeholder texture that's never shown.
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.WoodenArrowFriendly;

        private const int ShatterDelay = 50; // ticks an icicle stays lodged before it shatters
        private const int ShardCount = 6;

        private int Kind => (int)Projectile.ai[0];

        // ai[1] on an icicle = (npc index the icicle is stuck in) + 1, or 0 if not stuck
        private int StuckNpc => (int)Projectile.ai[1] - 1;

        private ref float Timer => ref Projectile.localAI[0];
        private bool initialized;
        private Vector2 stuckOffset;
        private bool hasOffset;

        private static readonly Color IceWhite = new Color(220, 245, 255);
        private static readonly Color IceBlue = new Color(90, 160, 255);

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 8;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults()
        {
            Projectile.width = 8;
            Projectile.height = 8;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 120;
            Projectile.extraUpdates = 1;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
        }

        // A lodged icicle stops hurting - the shatter does the damage.
        public override bool? CanDamage()
        {
            if (Kind == Icicle && StuckNpc >= 0)
                return false;
            return null;
        }

        public override void AI()
        {
            if (!initialized)
                Initialize();

            if (Kind == Icicle && StuckNpc >= 0)
            {
                StuckAI();
                return;
            }

            if (Projectile.velocity != Vector2.Zero)
                Projectile.rotation = Projectile.velocity.ToRotation();

            if (Kind == Shard)
                Projectile.velocity *= 0.97f;

            if (Main.rand.NextBool(Kind == Icicle ? 2 : 4))
            {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.BlueTorch, Vector2.Zero, 0, default, 0.9f);
                d.noGravity = true;
                d.velocity *= 0.2f;
            }

            Lighting.AddLight(Projectile.Center, 0.2f, 0.4f, 0.6f);
        }

        private void Initialize()
        {
            initialized = true;

            switch (Kind)
            {
                case Icicle:
                    Projectile.penetrate = -1; // it sticks instead of being used up
                    Projectile.Resize(12, 12);
                    Projectile.extraUpdates = 0;
                    Projectile.timeLeft = 180;
                    break;

                case Shard:
                    Projectile.Resize(6, 6);
                    Projectile.timeLeft = 30;
                    break;
            }
        }

        private void StuckAI()
        {
            NPC npc = Main.npc[StuckNpc];

            if (!npc.active)
            {
                Projectile.Kill(); // enemy died - shatter right away
                return;
            }

            if (!hasOffset)
            {
                stuckOffset = Projectile.Center - npc.Center;
                hasOffset = true;
            }

            Projectile.Center = npc.Center + stuckOffset;
            Projectile.velocity = Vector2.Zero;
            Projectile.tileCollide = false;

            Timer++;

            if (Main.rand.NextBool(2))
            {
                float charge = Timer / ShatterDelay;
                Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(8f, 8f),
                    DustID.Ice, Main.rand.NextVector2Circular(1f, 1f) * (0.5f + charge), 0, default, 0.8f + charge * 0.6f);
                d.noGravity = true;
            }

            if (Timer >= ShatterDelay)
                Projectile.Kill();
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            target.AddBuff(BuffID.Frostburn, 150);
            WeaponFX.DustCone(Projectile.Center, -Projectile.velocity, 0.8f, DustID.Ice, 5, 1.5f, 3.5f, 1f);

            // an icicle lodges in the first enemy it hits
            if (Kind == Icicle && StuckNpc < 0)
            {
                Projectile.ai[1] = target.whoAmI + 1;
                Projectile.velocity = Vector2.Zero;
                Timer = 0f;
                Projectile.timeLeft = ShatterDelay + 20;
                Projectile.netUpdate = true;
            }
        }

        public override void OnKill(int timeLeft)
        {
            if (Kind != Icicle)
            {
                WeaponFX.DustRing(Projectile.Center, DustID.Ice, 4, 2f, 0.9f);
                return;
            }

            // --- icicle shatters
            SoundEngine.PlaySound(SoundID.Item27, Projectile.Center);
            WeaponFX.DustRing(Projectile.Center, DustID.Ice, 16, 5f, 1.3f);
            WeaponFX.DustRing(Projectile.Center, DustID.BlueTorch, 10, 3f, 1.2f);

            if (Projectile.owner != Main.myPlayer)
                return;

            float startAngle = Main.rand.NextFloat(MathHelper.TwoPi);
            for (int i = 0; i < ShardCount; i++)
            {
                float angle = startAngle + MathHelper.TwoPi * i / ShardCount;

                Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    Projectile.Center,
                    angle.ToRotationVector2() * 9f,
                    Projectile.type,
                    System.Math.Max(1, (int)(Projectile.damage * 0.3f)),
                    Projectile.knockBack * 0.5f,
                    Projectile.owner,
                    ai0: Shard);
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Vector2 center = Projectile.Center - Main.screenPosition;
            Vector2 forward = Projectile.rotation.ToRotationVector2();

            switch (Kind)
            {
                case Icicle:
                    DrawIcicle(center, forward);
                    break;

                case Shard:
                    WeaponFX.DrawGlowTrail(Projectile, IceWhite, IceBlue, 0.2f, 0.8f);
                    WeaponFX.DrawLine(Projectile.Center - forward * 6f, Projectile.Center + forward * 6f,
                        WeaponFX.Additive(IceWhite, 0.9f), 2f);
                    WeaponFX.DrawGlow(center, WeaponFX.Additive(IceBlue, 0.6f), 0.25f);
                    break;

                default:
                    WeaponFX.DrawGlowTrail(Projectile, IceWhite, IceBlue, 0.3f, 0.9f);
                    WeaponFX.DrawLine(Projectile.Center - forward * 7f, Projectile.Center + forward * 7f,
                        WeaponFX.Additive(IceWhite, 1f), 3f);
                    WeaponFX.DrawGlow(center, WeaponFX.Additive(IceBlue, 0.7f), 0.4f);
                    break;
            }

            return false;
        }

        // A spike of ice. Once lodged, it pulses faster and brighter as it nears shattering.
        private void DrawIcicle(Vector2 center, Vector2 forward)
        {
            bool stuck = StuckNpc >= 0;
            float charge = stuck ? MathHelper.Clamp(Timer / ShatterDelay, 0f, 1f) : 0f;
            float pulse = stuck ? 0.7f + 0.3f * (float)System.Math.Sin(Timer * (0.3f + charge * 0.8f)) : 1f;

            if (!stuck)
                WeaponFX.DrawGlowTrail(Projectile, IceWhite, IceBlue, 0.45f, 0.9f);

            Color body = Color.Lerp(IceWhite, new Color(255, 255, 255), charge);

            WeaponFX.DrawLine(Projectile.Center - forward * 14f, Projectile.Center + forward * 10f,
                WeaponFX.Additive(IceBlue, 0.9f), 7f);
            WeaponFX.DrawLine(Projectile.Center - forward * 12f, Projectile.Center + forward * 10f,
                WeaponFX.Additive(body, 1f), 3.5f);

            WeaponFX.DrawGlow(center, WeaponFX.Additive(IceBlue, (0.5f + 0.5f * charge) * pulse), 0.7f + 0.9f * charge);
            WeaponFX.DrawGlow(center, WeaponFX.Additive(Color.White, 0.6f * pulse), 0.3f + 0.3f * charge);
        }
    }
}