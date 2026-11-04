using Microsoft.Xna.Framework;
using ShatteredIllusion.Common.Systems;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace ShatteredIllusion.Content.Items.Weapons.Mage
{
    /// <summary>
    /// Fires a frost crystal. If it hits an enemy it lodges there and chills it, and for 4
    /// seconds it pulses outward every 2/3 of a second, hurting everything nearby. Think of it
    /// as a tiny frost turret you stick on whatever is in front of you. Max 2 at once;
    /// planting a third removes the oldest.
    ///
    /// Balance target (pre-Eye of Cthulhu): ~23 dps from the bolts, plus ~8 dps for each
    /// crystal planted. Sprite: HoarfrostStaff.png is 24x44 (the 12x22 original scaled 2x).
    /// </summary>
    public class HoarfrostStaff : ModItem
    {
        private const int MaxCrystals = 2;

        public override void SetStaticDefaults()
        {
            Item.staff[Type] = true;
        }

        public override void SetDefaults()
        {
            Item.width = 24;
            Item.height = 44;

            Item.damage = 14;
            Item.DamageType = DamageClass.Magic;
            Item.mana = 9;
            Item.knockBack = 3f;

            Item.useTime = 36;
            Item.useAnimation = 36;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.autoReuse = true;
            Item.UseSound = SoundID.Item28;

            Item.shoot = ModContent.ProjectileType<HoarfrostCrystal>();
            Item.shootSpeed = 11f;

            Item.value = Item.sellPrice(silver: 80);
            Item.rare = ItemRarityID.Green;
        }

        // PLACEHOLDER recipe using vanilla materials - swap in your own tundra drops.
        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ItemID.IceBlock, 20)
                .AddIngredient(ItemID.Shiverthorn, 3)
                .AddTile(TileID.Anvils)
                .Register();
        }

        public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity,
            ref int type, ref int damage, ref float knockback)
        {
            Vector2 muzzle = position + velocity.SafeNormalize(Vector2.UnitX * player.direction) * 36f;
            if (Collision.CanHit(position, 0, 0, muzzle, 0, 0))
                position = muzzle;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position,
            Vector2 velocity, int type, int damage, float knockback)
        {
            // already 2 planted? remove the oldest one to make room
            int crystalType = ModContent.ProjectileType<HoarfrostCrystal>();
            if (player.ownedProjectileCounts[crystalType] >= MaxCrystals)
            {
                Projectile oldest = null;

                for (int i = 0; i < Main.maxProjectiles; i++)
                {
                    Projectile p = Main.projectile[i];
                    if (!p.active || p.owner != player.whoAmI || p.type != crystalType || p.ai[0] == 0f)
                        continue; // only planted ones (ai[0] > 0)

                    if (oldest == null || p.timeLeft < oldest.timeLeft)
                        oldest = p;
                }

                oldest?.Kill();
            }

            WeaponFX.DustCone(position, velocity, 0.5f, DustID.IceTorch, 6, 1.5f, 4f, 1.1f);
            return true;
        }
    }

    /// <summary>ai[0] = (index of the enemy it is lodged in) + 1, or 0 while still flying.</summary>
    public class HoarfrostCrystal : ModProjectile
    {
        // Drawn entirely in code, so this is just a placeholder texture that's never shown.
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.WoodenArrowFriendly;

        private const int LodgedLife = 240;
        private const int PulseEvery = 40;
        private const float PulseDamage = 0.4f;

        private static readonly Color IceWhite = new Color(220, 245, 255);
        private static readonly Color IceBlue = new Color(90, 190, 255);

        private int Host => (int)Projectile.ai[0] - 1;
        private bool Lodged => Projectile.ai[0] > 0f;
        private ref float Timer => ref Projectile.localAI[0];

        private Vector2 offset;
        private bool hasOffset;

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 8;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults()
        {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 120;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.extraUpdates = 1;

            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        // A lodged crystal does no contact damage - its pulses do the work.
        public override bool? CanDamage() => Lodged ? false : null;

        public override void AI()
        {
            if (!Lodged)
            {
                Projectile.rotation = Projectile.velocity.ToRotation();

                if (Main.rand.NextBool(3))
                {
                    Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.IceTorch, Vector2.Zero, 0, default, 0.9f);
                    d.noGravity = true;
                }

                Lighting.AddLight(Projectile.Center, 0.2f, 0.45f, 0.65f);
                return;
            }

            // ----- lodged: follow the host and pulse
            if (Host < 0 || Host >= Main.maxNPCs || !Main.npc[Host].active)
            {
                Projectile.Kill();
                return;
            }

            NPC npc = Main.npc[Host];
            if (!hasOffset)
            {
                offset = Projectile.Center - npc.Center;
                hasOffset = true;
            }

            Projectile.Center = npc.Center + offset;
            Projectile.velocity = Vector2.Zero;
            Projectile.tileCollide = false;
            Projectile.extraUpdates = 0;

            Timer++;

            if (Timer % PulseEvery == 0)
                Pulse();

            if (Main.rand.NextBool(4))
            {
                Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(8f, 8f), DustID.IceTorch,
                    new Vector2(0f, -0.5f), 0, default, 0.8f);
                d.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, 0.25f, 0.5f, 0.75f);
        }

        private void Pulse()
        {
            SoundEngine.PlaySound(SoundID.Item30 with { Volume = 0.5f, Pitch = 0.4f }, Projectile.Center);
            WeaponFX.DustRing(Projectile.Center, DustID.IceTorch, 14, 4.5f, 1.1f);

            if (Projectile.owner != Main.myPlayer)
                return;

            Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Vector2.Zero,
                ModContent.ProjectileType<HoarfrostPulse>(),
                System.Math.Max(1, (int)(Projectile.damage * PulseDamage)), 0f, Projectile.owner);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            target.AddBuff(BuffID.Slow, 120);
            WeaponFX.DustCone(Projectile.Center, -Projectile.velocity, 0.9f, DustID.IceTorch, 6, 1.5f, 4f, 1.1f);

            if (Lodged)
                return;

            // lodge in the first enemy it hits
            Projectile.ai[0] = target.whoAmI + 1;
            Projectile.velocity = Vector2.Zero;
            Projectile.timeLeft = LodgedLife;
            Timer = 0f;
            Projectile.netUpdate = true;

            SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.6f }, Projectile.Center);
        }

        public override bool OnTileCollide(Vector2 oldVelocity)
        {
            Projectile.Kill(); // shatters on blocks
            return false;
        }

        public override void OnKill(int timeLeft)
        {
            SoundEngine.PlaySound(SoundID.Item27, Projectile.Center);
            WeaponFX.DustRing(Projectile.Center, DustID.Ice, 10, 3.5f, 1.1f);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Vector2 forward = Projectile.rotation.ToRotationVector2();
            Vector2 center = Projectile.Center - Main.screenPosition;

            if (!Lodged)
            {
                WeaponFX.DrawGlowTrail(Projectile, IceWhite, IceBlue, 0.35f, 0.8f);
                DrawCrystal(forward, 1f);
                WeaponFX.DrawGlow(center, WeaponFX.Additive(IceBlue, 0.6f), 0.5f);
                return false;
            }

            // lodged: sits upright, glows brighter right before each pulse, fades out at the end
            float untilPulse = (Timer % PulseEvery) / PulseEvery;
            float fade = MathHelper.Clamp(Projectile.timeLeft / 30f, 0f, 1f);

            WeaponFX.DrawGlow(center, WeaponFX.Additive(IceBlue, (0.35f + 0.5f * untilPulse) * fade), 0.9f + 0.5f * untilPulse);
            DrawCrystal(-Vector2.UnitY, fade);
            return false;
        }

        // a diamond-shaped shard
        private void DrawCrystal(Vector2 forward, float opacity)
        {
            Vector2 side = forward.RotatedBy(MathHelper.PiOver2);
            Vector2 c = Projectile.Center;

            Vector2 tip = c + forward * 10f;
            Vector2 tail = c - forward * 7f;
            Vector2 left = c + side * 4f;
            Vector2 right = c - side * 4f;

            Color edge = WeaponFX.Additive(IceBlue, 0.95f * opacity);
            Color body = WeaponFX.Additive(IceWhite, opacity);

            WeaponFX.DrawLine(tail, left, edge, 3f);
            WeaponFX.DrawLine(left, tip, edge, 3f);
            WeaponFX.DrawLine(tip, right, edge, 3f);
            WeaponFX.DrawLine(right, tail, edge, 3f);
            WeaponFX.DrawLine(tail + forward * 2f, tip - forward * 2f, body, 2f);
        }
    }

    /// <summary>A short-lived ring that hurts everything inside it, once each.</summary>
    public class HoarfrostPulse : ModProjectile
    {
        // Drawn entirely in code, so this is just a placeholder texture that's never shown.
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.WoodenArrowFriendly;

        private const int Life = 8;
        private const int Size = 130;

        private static readonly Color IceBlue = new Color(90, 190, 255);

        public override void SetDefaults()
        {
            Projectile.width = Size;
            Projectile.height = Size;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = Life;

            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            // a circle, not the full square
            return Vector2.Distance(Projectile.Center, targetHitbox.ClosestPointInRect(Projectile.Center)) <= Size * 0.5f;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            target.AddBuff(BuffID.Slow, 90);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            float k = 1f - Projectile.timeLeft / (float)Life; // 0 -> 1
            float radius = MathHelper.Lerp(10f, Size * 0.5f, k);
            float fade = 1f - k;

            const int segments = 24;
            for (int i = 0; i < segments; i++)
            {
                float a1 = MathHelper.TwoPi * i / segments;
                float a2 = MathHelper.TwoPi * (i + 1) / segments;
                WeaponFX.DrawLine(Projectile.Center + a1.ToRotationVector2() * radius,
                    Projectile.Center + a2.ToRotationVector2() * radius, WeaponFX.Additive(IceBlue, 0.9f * fade), 4f * fade + 1f);
            }

            return false;
        }
    }
}
