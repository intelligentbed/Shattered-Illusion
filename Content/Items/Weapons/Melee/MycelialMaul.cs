using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ShatteredIllusion.Common.Systems;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ShatteredIllusion.Content.Items.Weapons.Melee
{
    /// <summary>
    /// A slow, heavy mushroom hammer. Every enemy it hits is engulfed in a lingering spore
    /// cloud that poisons anything inside, and enemies that are ALREADY poisoned take extra
    /// damage from the hammer - so the clouds set up the next swing.
    /// </summary>
    public class MycelialMaul : ModItem
    {
        private const int MaxClouds = 5;
        private bool hitWasPoisoned;

        public override void SetDefaults()
        {
            Item.width = 60;
            Item.height = 60;

            // Balance target: ~27 dps, ~32 against poisoned enemies (gold-tier, pre-Eye of Cthulhu).
            Item.damage = 17;
            Item.DamageType = DamageClass.Melee;
            Item.knockBack = 8f;
            Item.crit = 4;

            Item.useTime = 38;
            Item.useAnimation = 38;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;

            Item.value = Item.sellPrice(gold: 1);
            Item.rare = ItemRarityID.Green;
        }

        // PLACEHOLDER recipe using vanilla materials - swap in your own mycelial drops.
        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ItemID.GlowingMushroom, 30)
                .AddIngredient(ItemID.MudBlock, 20)
                .AddTile(TileID.Anvils)
                .Register();
        }

        // sporey sparkles trailing the swing
        public override void MeleeEffects(Player player, Rectangle hitbox)
        {
            if (!Main.rand.NextBool(2))
                return;

            Dust d = Dust.NewDustDirect(new Vector2(hitbox.X, hitbox.Y), hitbox.Width, hitbox.Height,
                Main.rand.NextBool() ? DustID.GreenTorch : DustID.Enchanted_Gold, 0f, 0f, 100, default, 1.1f);
            d.noGravity = true;
            d.velocity *= 0.4f;
        }

        public override void ModifyHitNPC(Player player, NPC target, ref NPC.HitModifiers modifiers)
        {
            hitWasPoisoned = target.HasBuff(BuffID.Poisoned);

            if (hitWasPoisoned)
                modifiers.SourceDamage *= 1.2f;
        }

        public override void OnHitNPC(Player player, NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (hitWasPoisoned)
                WeaponFX.DustRing(target.Center, DustID.GreenTorch, 12, 4f, 1.3f); // spore pop

            if (player.whoAmI != Main.myPlayer)
                return;

            int cloudType = ModContent.ProjectileType<SporeCloud>();
            if (player.ownedProjectileCounts[cloudType] >= MaxClouds)
                return;

            int cloudDamage = System.Math.Max(1, (int)player.GetTotalDamage(Item.DamageType).ApplyTo(Item.damage * 0.2f));

            Projectile.NewProjectile(player.GetSource_ItemUse(Item), target.Center, Main.rand.NextVector2Circular(1.5f, 1.5f),
                cloudType, cloudDamage, 0f, player.whoAmI);
        }
    }

    public class SporeCloud : ModProjectile
    {
        // Drawn entirely in code, so this is just a placeholder texture that's never shown.
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.WoodenArrowFriendly;

        private const int Lifetime = 150;
        private const int FadeIn = 12;
        private const int FadeOut = 45;

        private static readonly Color Teal = new Color(90, 230, 200);
        private static readonly Color Cream = new Color(255, 245, 170);

        private ref float Timer => ref Projectile.localAI[0];

        public override void SetDefaults()
        {
            Projectile.width = 70;
            Projectile.height = 70;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = Lifetime;

            // Shared by ALL spore clouds: an enemy standing in several clouds still only
            // takes one tick every 40 frames, so stacking clouds can't multiply the damage.
            Projectile.usesIDStaticNPCImmunity = true;
            Projectile.idStaticNPCHitCooldown = 40;
        }

        public override void AI()
        {
            Timer++;

            // drift, slow down and float gently upward
            Projectile.velocity *= 0.96f;
            Projectile.velocity.Y -= 0.01f;

            if (Main.rand.NextBool(4))
            {
                Dust spore = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(28f, 28f),
                    Main.rand.NextBool() ? DustID.GreenTorch : DustID.Enchanted_Gold,
                    new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), Main.rand.NextFloat(-0.8f, -0.2f)), 0, default, 0.9f);
                spore.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, 0.25f, 0.5f, 0.4f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            target.AddBuff(BuffID.Poisoned, 180);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            float fade = MathHelper.Clamp(Timer / FadeIn, 0f, 1f)
                         * MathHelper.Clamp((Lifetime - Timer) / FadeOut, 0f, 1f);

            Vector2 center = Projectile.Center - Main.screenPosition;

            // a handful of overlapping soft puffs that slowly orbit and breathe
            for (int i = 0; i < 7; i++)
            {
                float direction = i % 2 == 0 ? 1f : -1f;
                float angle = i * 2.4f + Timer * 0.02f * direction + Projectile.whoAmI;
                float radius = 14f + 10f * (float)System.Math.Sin(Timer * 0.07f + i);
                float size = 0.85f + 0.2f * (float)System.Math.Sin(Timer * 0.05f + i * 1.7f);

                Color color = i % 2 == 0 ? Teal : Cream;
                WeaponFX.DrawGlow(center + angle.ToRotationVector2() * radius, WeaponFX.Additive(color, 0.28f * fade), size);
            }

            WeaponFX.DrawGlow(center, WeaponFX.Additive(Teal, 0.3f * fade), 1.3f);
            return false;
        }
    }
}