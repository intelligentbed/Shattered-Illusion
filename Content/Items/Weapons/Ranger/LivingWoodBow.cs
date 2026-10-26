using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ShatteredIllusion.Common.Systems;
using ShatteredIllusion.Content.Items.Materials;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace ShatteredIllusion.Content.Items.Weapons.Ranger
{
    /// <summary>
    /// A bow that grows its arrows. Wooden arrows become Leaf Arrows: a short way into flight
    /// they SPROUT, splitting into three arrows in a fan. If the arrow hits something before it
    /// sprouts, leaves burst out of the impact instead - so it's good at point blank AND at range.
    /// Other arrow types fire normally.
    /// </summary>
    public class LivingWoodBow : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 12;
            Item.height = 24;
            Item.scale = 1.5f; // sprite is small, so it's drawn bigger in the hand

            // Balance target: ~18 dps with one arrow, ~38 at best when the sprout lands
            // all 3 arrows (pre-Eye of Cthulhu). The bow's damage ADDS to the arrow's,
            // so keep this low.
            Item.damage = 4;
            Item.DamageType = DamageClass.Ranged;
            Item.knockBack = 2f;

            Item.useTime = 30;
            Item.useAnimation = 30;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.autoReuse = true;
            Item.UseSound = SoundID.Item5;

            Item.shoot = ProjectileID.WoodenArrowFriendly;
            Item.shootSpeed = 9f;
            Item.useAmmo = AmmoID.Arrow;

            Item.value = Item.sellPrice(silver: 80);
            Item.rare = ItemRarityID.Green;
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<EvergreenCrystal>(), 7)
                .AddIngredient(ItemID.Wood, 30)
                .AddTile(TileID.Anvils)
                .Register();
        }

        public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity,
            ref int type, ref int damage, ref float knockback)
        {
            if (type == ProjectileID.WoodenArrowFriendly)
                type = ModContent.ProjectileType<LeafArrowProjectile>();

            Vector2 muzzle = position + velocity.SafeNormalize(Vector2.UnitX * player.direction) * 18f;
            if (Collision.CanHit(position, 0, 0, muzzle, 0, 0))
                position = muzzle;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position,
            Vector2 velocity, int type, int damage, float knockback)
        {
            if (type != ModContent.ProjectileType<LeafArrowProjectile>())
                return true; // special arrows behave like normal

            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI,
                ai0: LeafArrowProjectile.Mother);

            WeaponFX.DustCone(position, velocity, 0.4f, DustID.Grass, 3, 1f, 3f, 0.9f);
            return false;
        }

        public override void PostDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor,
            float rotation, float scale, int whoAmI)
        {
            WeaponFX.DrawWorldAura(Item, rotation, scale, new Color(255, 220, 140));
        }
    }

    /// <summary>ai[0]: 0 = mother arrow (splits), 1 = leaf arrow (what it splits into).</summary>
    public class LeafArrowProjectile : ModProjectile
    {
        public const int Mother = 0;
        public const int Leaf = 1;

        // Drawn entirely in code, so this is just a placeholder texture that's never shown.
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.WoodenArrowFriendly;

        private const int SplitTick = 16;
        private const float SplitAngle = 0.22f;

        // Leaf arrows are weaker than the arrow they came from (fractions of its damage).
        private const float SproutDamage = 0.55f; // the two extra arrows from the mid-air split
        private const float BurstDamage = 0.4f;   // the three leaves from hitting before the split

        private int Kind => (int)Projectile.ai[0];
        private ref float Timer => ref Projectile.localAI[0];

        private bool initialized;
        private bool split;

        private static readonly Color Shaft = new Color(170, 120, 60);
        private static readonly Color LeafGreen = new Color(120, 255, 110);
        private static readonly Color DarkGreen = new Color(30, 130, 60);

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 8;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults()
        {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.arrow = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 240;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
        }

        public override void AI()
        {
            if (!initialized)
            {
                initialized = true;
                if (Kind == Leaf)
                    Projectile.timeLeft = 60;
            }

            Timer++;

            if (Timer > 14f)
                Projectile.velocity.Y = System.Math.Min(Projectile.velocity.Y + 0.05f, 14f);

            if (Projectile.velocity != Vector2.Zero)
                Projectile.rotation = Projectile.velocity.ToRotation();

            if (Kind == Mother && !split && Timer >= SplitTick)
                Sprout();

            if (Main.rand.NextBool(3))
            {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Grass,
                    Main.rand.NextVector2Circular(0.4f, 0.4f), 0, default, 0.8f);
                d.noGravity = true;
            }
        }

        // The arrow sprouts into three, fanned out.
        private void Sprout()
        {
            split = true;

            SoundEngine.PlaySound(SoundID.Item10 with { Pitch = 0.6f, Volume = 0.6f }, Projectile.Center);
            WeaponFX.DustCone(Projectile.Center, Projectile.velocity, 1.2f, DustID.Grass, 8, 1.5f, 4f, 1.1f);
            WeaponFX.DustRing(Projectile.Center, DustID.GreenTorch, 6, 2f, 1f);

            if (Projectile.owner != Main.myPlayer)
                return;

            for (int sign = -1; sign <= 1; sign += 2)
            {
                SpawnLeaf(Projectile.velocity.RotatedBy(sign * SplitAngle), SproutDamage);
            }
        }

        private void SpawnLeaf(Vector2 velocity, float damageMultiplier)
        {
            Projectile.NewProjectile(
                Projectile.GetSource_FromThis(),
                Projectile.Center,
                velocity,
                Projectile.type,
                System.Math.Max(1, (int)(Projectile.damage * damageMultiplier)),
                Projectile.knockBack * 0.5f,
                Projectile.owner,
                ai0: Leaf);
        }

        public override void OnKill(int timeLeft)
        {
            WeaponFX.DustCone(Projectile.Center, -Projectile.velocity, 1f, DustID.Grass, 5, 1.5f, 3.5f, 1f);

            // Hit something before sprouting: leaves burst out of the impact instead.
            if (Kind != Mother || split || Projectile.owner != Main.myPlayer)
                return;

            SoundEngine.PlaySound(SoundID.Item10 with { Pitch = 0.6f, Volume = 0.6f }, Projectile.Center);

            float start = Main.rand.NextFloat(MathHelper.TwoPi);
            for (int i = 0; i < 3; i++)
                SpawnLeaf((start + MathHelper.TwoPi * i / 3f).ToRotationVector2() * 6f, BurstDamage);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Vector2 forward = Projectile.rotation.ToRotationVector2();
            Vector2 center = Projectile.Center - Main.screenPosition;

            WeaponFX.DrawGlowTrail(Projectile, LeafGreen, DarkGreen, Kind == Mother ? 0.25f : 0.18f, 0.7f);

            if (Kind == Mother)
            {
                // a wooden shaft with a glowing green tip
                WeaponFX.DrawLine(Projectile.Center - forward * 9f, Projectile.Center + forward * 7f, Shaft, 2f);
                WeaponFX.DrawLine(Projectile.Center + forward * 5f, Projectile.Center + forward * 10f,
                    WeaponFX.Additive(LeafGreen, 1f), 3f);
                WeaponFX.DrawGlow(center + forward * 8f, WeaponFX.Additive(LeafGreen, 0.6f), 0.3f);

                // a little sprouting flash as it nears the split
                float charge = MathHelper.Clamp(Timer / SplitTick, 0f, 1f);
                WeaponFX.DrawGlow(center, WeaponFX.Additive(LeafGreen, 0.35f * charge), 0.5f);
            }
            else
            {
                // a small leaf: two angled strokes
                Vector2 side = forward.RotatedBy(MathHelper.PiOver2) * 2.5f;
                WeaponFX.DrawLine(Projectile.Center - forward * 6f, Projectile.Center + side, WeaponFX.Additive(LeafGreen, 1f), 2f);
                WeaponFX.DrawLine(Projectile.Center + side, Projectile.Center + forward * 7f, WeaponFX.Additive(LeafGreen, 1f), 2f);
                WeaponFX.DrawLine(Projectile.Center - forward * 6f, Projectile.Center - side, WeaponFX.Additive(LeafGreen, 0.8f), 2f);
                WeaponFX.DrawLine(Projectile.Center - side, Projectile.Center + forward * 7f, WeaponFX.Additive(LeafGreen, 0.8f), 2f);
                WeaponFX.DrawGlow(center, WeaponFX.Additive(LeafGreen, 0.4f), 0.25f);
            }

            return false;
        }
    }
}
