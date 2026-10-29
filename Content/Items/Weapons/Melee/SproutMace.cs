using Microsoft.Xna.Framework;
using ShatteredIllusion.Common.Systems;
using ShatteredIllusion.Content.Items.Materials;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace ShatteredIllusion.Content.Items.Weapons.Melee
{
    /// <summary>
    /// A leafy mace. Every hit plants a sprout in the enemy. The sprout blooms after 3/4 of a
    /// second and bursts, hurting everything around it. Keep swinging and the blooms pop one
    /// after another, so it is great against groups. Max 4 sprouts out at once.
    ///
    /// Balance target (pre-Eye of Cthulhu): ~23 dps from swings, ~30 dps with blooms landing.
    /// Sprite: SproutMace.png is 33x51 (the 11x17 original scaled 3x).
    /// </summary>
    public class SproutMace : ModItem
    {
        private const int MaxSprouts = 4;

        public override void SetDefaults()
        {
            Item.width = 33;
            Item.height = 51;

            Item.damage = 12;
            Item.DamageType = DamageClass.Melee;
            Item.knockBack = 6f;
            Item.crit = 4;

            Item.useTime = 31;
            Item.useAnimation = 31;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;

            Item.value = Item.sellPrice(silver: 80);
            Item.rare = ItemRarityID.Green;
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<EvergreenCrystal>(), 6)
                .AddIngredient(ItemID.Wood, 25)
                .AddTile(TileID.Anvils)
                .Register();
        }

        public override void MeleeEffects(Player player, Rectangle hitbox)
        {
            if (!Main.rand.NextBool(3))
                return;

            Dust d = Dust.NewDustDirect(new Vector2(hitbox.X, hitbox.Y), hitbox.Width, hitbox.Height,
                DustID.Grass, 0f, 0f, 0, default, 1f);
            d.noGravity = true;
            d.velocity *= 0.4f;
        }

        public override void OnHitNPC(Player player, NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (player.whoAmI != Main.myPlayer)
                return;

            int bloomType = ModContent.ProjectileType<SproutBloom>();
            if (player.ownedProjectileCounts[bloomType] >= MaxSprouts)
                return;

            int bloomDamage = System.Math.Max(1, (int)player.GetTotalDamage(Item.DamageType).ApplyTo(Item.damage * 0.5f));

            Projectile.NewProjectile(player.GetSource_ItemUse(Item),
                target.Center + Main.rand.NextVector2Circular(target.width * 0.25f, target.height * 0.25f),
                Vector2.Zero, bloomType, bloomDamage, 0f, player.whoAmI, ai0: target.whoAmI + 1);

            WeaponFX.DustCone(target.Center, -Vector2.UnitY, 1.2f, DustID.Grass, 5, 1f, 3f, 1f);
        }
    }

    /// <summary>ai[0] = (index of the enemy it is planted in) + 1.</summary>
    public class SproutBloom : ModProjectile
    {
        // Drawn entirely in code, so this is just a placeholder texture that's never shown.
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.WoodenArrowFriendly;

        private const int FuseTime = 45;
        private const int BurstTicks = 4;
        private const int BurstSize = 96;

        private static readonly Color LeafGreen = new Color(130, 255, 110);
        private static readonly Color Pink = new Color(255, 150, 190);
        private static readonly Color Yellow = new Color(255, 230, 120);

        private ref float Timer => ref Projectile.localAI[0];
        private int StuckNpc => (int)Projectile.ai[0] - 1;

        private Vector2 offset;
        private bool hasOffset;
        private bool burst;

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 120;

            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1; // each enemy is hit by a burst only once
        }

        // Harmless until it blooms.
        public override bool? CanDamage() => burst ? null : false;

        public override void AI()
        {
            Timer++;

            // ride along on the enemy it is planted in
            if (StuckNpc >= 0 && StuckNpc < Main.maxNPCs)
            {
                NPC npc = Main.npc[StuckNpc];
                if (npc.active)
                {
                    if (!hasOffset)
                    {
                        offset = Projectile.Center - npc.Center;
                        hasOffset = true;
                    }

                    Projectile.Center = npc.Center + offset;
                }
                else if (!burst && Timer < FuseTime)
                {
                    Timer = FuseTime; // the host died, so bloom right now
                }
            }

            if (!burst && Timer >= FuseTime)
                Burst();

            if (burst && Timer >= FuseTime + BurstTicks)
            {
                Projectile.Kill();
                return;
            }

            if (!burst && Main.rand.NextBool(6))
            {
                Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(6f, 6f), DustID.Grass,
                    new Vector2(0f, -0.6f), 0, default, 0.8f);
                d.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, 0.2f, 0.45f, 0.2f);
        }

        private void Burst()
        {
            burst = true;
            Projectile.Resize(BurstSize, BurstSize);

            SoundEngine.PlaySound(SoundID.Item10 with { Pitch = 0.5f }, Projectile.Center);
            WeaponFX.DustRing(Projectile.Center, DustID.Grass, 16, 4.5f, 1.3f);
            WeaponFX.DustRing(Projectile.Center, DustID.PinkTorch, 8, 2.5f, 1.1f);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Vector2 center = Projectile.Center - Main.screenPosition;

            if (burst)
            {
                float life = 1f - (Timer - FuseTime) / BurstTicks;
                WeaponFX.DrawGlow(center, WeaponFX.Additive(Pink, 0.8f * life), 2.4f);
                WeaponFX.DrawGlow(center, WeaponFX.Additive(Color.White, 0.6f * life), 1.1f);
                return false;
            }

            // a bud that opens as the fuse runs down
            float t = Timer / FuseTime;
            float pulse = 0.8f + 0.2f * (float)System.Math.Sin(Timer * (0.2f + t * 0.6f));
            float open = 3f + 9f * t;

            for (int i = 0; i < 5; i++)
            {
                float angle = MathHelper.TwoPi * i / 5f + Timer * 0.05f;
                WeaponFX.DrawGlow(center + angle.ToRotationVector2() * open, WeaponFX.Additive(Pink, 0.9f * pulse), 0.22f);
            }

            WeaponFX.DrawGlow(center, WeaponFX.Additive(Yellow, 0.9f), 0.2f);
            WeaponFX.DrawGlow(center, WeaponFX.Additive(LeafGreen, 0.35f * t), 0.9f);

            return false;
        }
    }
}
