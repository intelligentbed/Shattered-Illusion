using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ShatteredIllusion.Common.Systems;
using Terraria;
using Terraria.DataStructures;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;

namespace ShatteredIllusion.Content.Items.Weapons.Mage
{
    /// <summary>
    /// Conjures a pair of giant antlion jaws at your cursor. They hang open for a moment
    /// (dragging nearby enemies toward the middle), then SNAP shut and bite everything inside.
    /// Rewards you for reading where enemies are going to be.
    /// </summary>
    public class MandibleStaff : ModItem
    {
        private const float MaxCastRange = 650f;

        public override void SetStaticDefaults()
        {
            Item.staff[Type] = true; // held like a staff
        }

        public override void SetDefaults()
        {
            Item.width = 42;
            Item.height = 54;

            // Balance target: ~23 dps on one enemy (gold-tier, pre-Eye of Cthulhu),
            // more when the bite catches a group.
            Item.damage = 16;
            Item.DamageType = DamageClass.Magic;
            Item.mana = 10;
            Item.knockBack = 4f;

            Item.useTime = 42;
            Item.useAnimation = 42;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.autoReuse = true;
            Item.UseSound = SoundID.Item8;

            Item.shoot = ModContent.ProjectileType<MandibleJaws>();
            Item.shootSpeed = 1f;

            Item.value = Item.sellPrice(gold: 1);
            Item.rare = ItemRarityID.Green;
        }

        // PLACEHOLDER recipe using vanilla materials - swap in your own antlion drops.
        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ItemID.AntlionMandible, 10)
                .AddIngredient(ItemID.SandBlock, 20)
                .AddTile(TileID.Anvils)
                .Register();
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position,
            Vector2 velocity, int type, int damage, float knockback)
        {
            // jaws appear at the cursor, but no further than MaxCastRange from you
            Vector2 toCursor = Main.MouseWorld - player.Center;
            Vector2 target = toCursor.Length() > MaxCastRange
                ? player.Center + toCursor.SafeNormalize(Vector2.UnitX) * MaxCastRange
                : Main.MouseWorld;

            Projectile.NewProjectile(source, target, Vector2.Zero, type, damage, knockback, player.whoAmI);

            // little flash at the staff tip
            Vector2 tip = position + velocity.SafeNormalize(Vector2.UnitX * player.direction) * 38f;
            WeaponFX.DustCone(tip, velocity, 0.7f, DustID.Sand, 8, 1.5f, 4f, 1.1f);

            return false;
        }
    }

    public class MandibleJaws : ModProjectile
    {
        // Drawn entirely in code, so this is just a placeholder texture that's never shown.
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.WoodenArrowFriendly;

        private const int OpenTime = 24;   // jaws hang open (and pull enemies in)
        private const int CloseTime = 6;   // the fast snap
        private const int LingerTime = 14; // closed jaws fade out
        private const float PullRange = 140f;
        private const int BiteSize = 110;

        private static readonly Color Sand = new Color(255, 190, 110);
        private static readonly Color Deep = new Color(190, 90, 40);

        private ref float Timer => ref Projectile.localAI[0];
        private bool snapped;

        private int SnapTick => OpenTime + CloseTime;

        public override void SetDefaults()
        {
            Projectile.width = BiteSize;
            Projectile.height = BiteSize;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = OpenTime + CloseTime + LingerTime;

            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1; // one bite per enemy
        }

        // The bite only hurts for a few ticks around the snap.
        public override bool? CanDamage()
        {
            return Timer >= SnapTick - 2 && Timer <= SnapTick + 4;
        }

        public override void AI()
        {
            Timer++;
            Projectile.velocity = Vector2.Zero;

            if (Timer < SnapTick)
                PullEnemies(Timer < OpenTime ? 0.35f : 0.9f);

            if (Timer < OpenTime && Main.rand.NextBool(2))
            {
                Vector2 edge = Main.rand.NextVector2CircularEdge(70f, 70f);
                Dust d = Dust.NewDustPerfect(Projectile.Center + edge, DustID.Sand, -edge * 0.06f, 0, default, 1f);
                d.noGravity = true;
            }

            if (Timer >= SnapTick && !snapped)
                Snap();

            Lighting.AddLight(Projectile.Center, 0.6f, 0.4f, 0.2f);
        }

        private void PullEnemies(float strength)
        {
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (!npc.CanBeChasedBy(Projectile) || npc.boss || npc.knockBackResist <= 0f)
                    continue;

                Vector2 toCenter = Projectile.Center - npc.Center;
                float distance = toCenter.Length();
                if (distance > PullRange || distance < 16f)
                    continue;

                npc.velocity += toCenter.SafeNormalize(Vector2.Zero) * strength * npc.knockBackResist;
            }
        }

        private void Snap()
        {
            snapped = true;

            Terraria.Audio.SoundEngine.PlaySound(SoundID.Item10 with { Pitch = -0.4f }, Projectile.Center);
            WeaponFX.DustRing(Projectile.Center, DustID.Sand, 22, 6f, 1.4f);
            WeaponFX.DustRing(Projectile.Center, DustID.Sandstorm, 12, 3f, 1.2f);

            Main.instance.CameraModifiers.Add(new PunchCameraModifier(Projectile.Center, Vector2.UnitY, 3f, 6f, 10,
                900f, "MandibleStaffSnap"));
        }

        // 0 = closed, 1 = fully open
        private float Openness()
        {
            float t = Timer;
            if (t < 6f)
                return MathHelper.SmoothStep(0f, 1f, t / 6f);
            if (t < OpenTime)
                return 1f;
            if (t < SnapTick)
            {
                float k = (t - OpenTime) / CloseTime;
                return 1f - k * k; // accelerates shut
            }
            return 0f;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            float t = Timer;
            float fade = t > SnapTick ? 1f - MathHelper.Clamp((t - SnapTick) / LingerTime, 0f, 1f) : 1f;
            float openness = Openness();

            // faint ring showing the area that will be bitten
            if (t < SnapTick)
            {
                const int segments = 24;
                for (int i = 0; i < segments; i++)
                {
                    float a1 = MathHelper.TwoPi * i / segments;
                    float a2 = a1 + MathHelper.TwoPi / segments * 0.5f;
                    WeaponFX.DrawLine(Projectile.Center + a1.ToRotationVector2() * 55f,
                        Projectile.Center + a2.ToRotationVector2() * 55f,
                        WeaponFX.Additive(Sand, 0.25f * openness), 2f);
                }
            }

            WeaponFX.DrawGlow(Projectile.Center - Main.screenPosition, WeaponFX.Additive(Sand, 0.35f * fade), 1.6f);

            DrawClaw(-1f, openness, fade);
            DrawClaw(1f, openness, fade);

            return false;
        }

        // Each claw is a curve from a base below-and-beside the center up to a tip near the top.
        // As `openness` drops to 0 the tips swing together in the middle.
        private void DrawClaw(float side, float openness, float fade)
        {
            Vector2 c = Projectile.Center;
            Vector2 p0 = c + new Vector2(side * 70f, 30f);
            Vector2 p1 = c + new Vector2(side * 88f, -8f);
            Vector2 p2 = c + new Vector2(side * (3f + 52f * openness), -42f);

            const int segments = 14;
            Vector2 previous = p0;

            for (int i = 1; i <= segments; i++)
            {
                float u = i / (float)segments;
                Vector2 point = (1f - u) * (1f - u) * p0 + 2f * (1f - u) * u * p1 + u * u * p2;
                float thickness = MathHelper.Lerp(10f, 3f, u);

                WeaponFX.DrawLine(previous, point, WeaponFX.Additive(Deep, 0.9f * fade), thickness + 3f);
                WeaponFX.DrawLine(previous, point, WeaponFX.Additive(Sand, fade), thickness);

                // teeth along the inner edge
                if (i == 8 || i == 10 || i == 12)
                {
                    Vector2 inward = new Vector2(-side, 0.35f).SafeNormalize(Vector2.UnitX);
                    WeaponFX.DrawLine(point, point + inward * 9f, WeaponFX.Additive(Sand, fade), 3f);
                }

                previous = point;
            }

            WeaponFX.DrawGlow(p2 - Main.screenPosition, WeaponFX.Additive(Color.White, 0.5f * fade), 0.4f);
        }
    }
}