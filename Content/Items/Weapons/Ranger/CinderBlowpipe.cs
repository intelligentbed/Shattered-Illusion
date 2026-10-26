using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ShatteredIllusion.Common.Systems;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace ShatteredIllusion.Content.Items.Weapons.Ranger
{
    /// <summary>
    /// Spits glowing cinder darts that set enemies on fire. Hit an enemy that is ALREADY
    /// burning and the dart flashes over, throwing 3 embers outward - so it likes to keep
    /// things lit and keep firing, and it chains nicely through a crowd.
    /// </summary>
    public class CinderBlowpipe : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 23;
            Item.height = 6;
            Item.scale = 1.7f; // sprite is small, so it's drawn bigger in the hand

            // Balance target: ~22 dps from darts + ~10 from flashover embers (pre-Eye of Cthulhu).
            // The pipe's damage ADDS to the dart's, so keep this low.
            Item.damage = 5;
            Item.DamageType = DamageClass.Ranged;
            Item.knockBack = 1.5f;

            Item.useTime = 24;
            Item.useAnimation = 24;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.autoReuse = true;
            Item.UseSound = SoundID.Item17;

            Item.shoot = ProjectileID.PoisonDart; // replaced with the cinder dart in ModifyShootStats
            Item.shootSpeed = 11f;
            Item.useAmmo = AmmoID.Dart;

            Item.value = Item.sellPrice(silver: 80);
            Item.rare = ItemRarityID.Green;
        }

        // PLACEHOLDER recipe using vanilla materials - swap in your own ashen drops.
        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ItemID.AshBlock, 25)
                .AddIngredient(ItemID.Obsidian, 6)
                .AddTile(TileID.Anvils)
                .Register();
        }

        public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity,
            ref int type, ref int damage, ref float knockback)
        {
            type = ModContent.ProjectileType<CinderDartProjectile>();

            Vector2 muzzle = position + velocity.SafeNormalize(Vector2.UnitX * player.direction) * 28f;
            if (Collision.CanHit(position, 0, 0, muzzle, 0, 0))
                position = muzzle;
        }

        public override void PostDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor,
            float rotation, float scale, int whoAmI)
        {
            WeaponFX.DrawWorldAura(Item, rotation, scale, new Color(255, 150, 60));
        }
    }

    /// <summary>Remembers, per enemy, how long until it can flash over again.</summary>
    public class CinderFlashoverNPC : GlobalNPC
    {
        public const int CooldownTicks = 45;

        public override bool InstancePerEntity => true;

        public int Cooldown;

        public override void PostAI(NPC npc)
        {
            if (Cooldown > 0)
                Cooldown--;
        }
    }

    /// <summary>ai[0]: 0 = dart, 1 = ember (thrown out by a flashover).</summary>
    public class CinderDartProjectile : ModProjectile
    {
        public const int Dart = 0;
        public const int Ember = 1;

        // Drawn entirely in code, so this is just a placeholder texture that's never shown.
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.WoodenArrowFriendly;

        private int Kind => (int)Projectile.ai[0];
        private ref float Timer => ref Projectile.localAI[0];
        private bool initialized;

        private static readonly Color Orange = new Color(255, 150, 60);
        private static readonly Color Red = new Color(200, 50, 30);
        private static readonly Color Hot = new Color(255, 235, 170);

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
            Projectile.timeLeft = 90;
            Projectile.extraUpdates = 1;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
        }

        public override void AI()
        {
            if (!initialized)
            {
                initialized = true;
                if (Kind == Ember)
                {
                    Projectile.Resize(6, 6);
                    Projectile.timeLeft = 24;
                    Projectile.extraUpdates = 0;
                }
            }

            Timer++;

            if (Kind == Ember)
                Projectile.velocity *= 0.96f;
            else if (Timer > 20f)
                Projectile.velocity.Y += 0.03f;

            if (Projectile.velocity != Vector2.Zero)
                Projectile.rotation = Projectile.velocity.ToRotation();

            if (Main.rand.NextBool(Kind == Ember ? 3 : 2))
            {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Torch,
                    Main.rand.NextVector2Circular(0.5f, 0.5f), 0, default, Kind == Ember ? 0.9f : 1.1f);
                d.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, 0.6f, 0.3f, 0.1f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            // Embers are just sparks - only real darts light enemies, and only darts can flash over.
            if (Kind != Dart)
                return;

            bool wasBurning = target.HasBuff(BuffID.OnFire);
            target.AddBuff(BuffID.OnFire, 180);

            // A flashover needs the enemy to be burning AND not to have flashed over very recently,
            // which stops rapid fire from throwing embers on every single dart.
            CinderFlashoverNPC flash = target.GetGlobalNPC<CinderFlashoverNPC>();
            if (!wasBurning || flash.Cooldown > 0)
                return;

            flash.Cooldown = CinderFlashoverNPC.CooldownTicks;

            // --- flashover: the dart ignites the fire already on the enemy
            SoundEngine.PlaySound(SoundID.Item45 with { Volume = 0.5f, Pitch = 0.3f }, target.Center);
            WeaponFX.DustRing(target.Center, DustID.Torch, 14, 4.5f, 1.4f);
            WeaponFX.DustRing(target.Center, DustID.Enchanted_Gold, 6, 2.5f, 1f);

            if (Projectile.owner != Main.myPlayer)
                return;

            float start = Main.rand.NextFloat(MathHelper.TwoPi);
            for (int i = 0; i < 3; i++)
            {
                Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    target.Center,
                    (start + MathHelper.TwoPi * i / 3f).ToRotationVector2() * 7f,
                    Projectile.type,
                    System.Math.Max(1, (int)(Projectile.damage * 0.3f)),
                    0f,
                    Projectile.owner,
                    ai0: Ember);
            }
        }

        public override void OnKill(int timeLeft)
        {
            WeaponFX.DustCone(Projectile.Center, -Projectile.velocity, 1f, DustID.Torch, Kind == Dart ? 6 : 3, 1.5f, 3.5f, 1f);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Vector2 forward = Projectile.rotation.ToRotationVector2();
            Vector2 center = Projectile.Center - Main.screenPosition;

            if (Kind == Ember)
            {
                float flicker = 0.7f + 0.3f * (float)System.Math.Sin(Timer * 1.3f + Projectile.whoAmI);
                float fade = MathHelper.Clamp(Projectile.timeLeft / 10f, 0f, 1f);

                WeaponFX.DrawGlowTrail(Projectile, Orange, Red, 0.2f, 0.8f * fade);
                WeaponFX.DrawGlow(center, WeaponFX.Additive(Orange, 0.8f * flicker * fade), 0.3f);
                WeaponFX.DrawGlow(center, WeaponFX.Additive(Hot, 0.8f * fade), 0.12f);
                return false;
            }

            WeaponFX.DrawGlowTrail(Projectile, Orange, Red, 0.3f, 0.9f);
            WeaponFX.DrawLine(Projectile.Center - forward * 7f, Projectile.Center + forward * 6f, WeaponFX.Additive(Orange, 1f), 3f);
            WeaponFX.DrawLine(Projectile.Center - forward * 2f, Projectile.Center + forward * 6f, WeaponFX.Additive(Hot, 1f), 1.5f);
            WeaponFX.DrawGlow(center, WeaponFX.Additive(Orange, 0.6f), 0.4f);

            return false;
        }
    }
}
