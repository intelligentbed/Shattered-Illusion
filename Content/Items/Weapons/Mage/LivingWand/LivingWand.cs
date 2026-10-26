using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ShatteredIllusion.Common.Systems;
using ShatteredIllusion.Content.Items.Materials;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace ShatteredIllusion.Content.Items.Weapons.Mage.LivingWand
{
    public class LivingWand : ModItem
    {
        /// <summary>Every Nth cast is a slow, heavy Bloom Seed instead of a normal seed.</summary>
        private const int BloomEvery = 4;

        private int castCount;

        public override void SetDefaults()
        {
            Item.width = 20;
            Item.height = 20;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.useTime = 16;
            Item.useAnimation = 16;
            Item.autoReuse = true;
            Item.DamageType = DamageClass.Magic;
            Item.knockBack = 2;
            Item.value = 10466; //???
            Item.rare = ItemRarityID.White;
            Item.UseSound = SoundID.Item5;
            Item.shoot = ModContent.ProjectileType<LivingWandProjectile>();
            Item.shootSpeed = 15f;
            Item.mana = 2;
            Item.damage = 4;
            Item.ArmorPenetration = 15;
        }

        public override void AddRecipes()
        {
            Recipe recipe = CreateRecipe();
            recipe.AddIngredient(ModContent.ItemType<EvergreenCrystal>(), 7);
            recipe.AddIngredient(ItemID.Wood, 40);
            recipe.AddTile(TileID.Anvils);
            recipe.Register();
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            // Hardcoded English for now - move to your .hjson localization when you like.
            tooltips.Add(new TooltipLine(Mod, "LivingWandBloom",
                "Seeds gently home in on enemies\nEvery 4th cast lobs a Bloom Seed that bursts into homing leaves"));
        }

        public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity,
            ref int type, ref int damage, ref float knockback)
        {
            Vector2 muzzle = position + velocity.SafeNormalize(Vector2.UnitX * player.direction) * 24f;
            if (Collision.CanHit(position, 0, 0, muzzle, 0, 0))
                position = muzzle;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position,
            Vector2 velocity, int type, int damage, float knockback)
        {
            castCount++;
            bool bloom = castCount % BloomEvery == 0;

            if (bloom)
            {
                castCount = 0;
                velocity *= 0.65f; // lobbed, so slower
                damage *= 2;
            }

            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI,
                ai0: bloom ? 1f : 0f);

            WeaponFX.DustCone(position, velocity, bloom ? 0.7f : 0.35f, DustID.Grass, bloom ? 10 : 4, 1.5f, 4f, 1.1f);
            if (bloom)
                WeaponFX.DustCone(position, velocity, 0.7f, DustID.GreenTorch, 6, 1f, 3f, 1.2f);

            return false;
        }
    }

    /// <summary>
    /// One projectile class, three behaviours (ai[0]):
    ///   0 = Seed  - fast, pierces once, gently homes in; sometimes sprouts a leaf on hit
    ///   1 = Bloom - slow lobbed seed (gravity), bursts into 3 homing leaves when it lands/hits
    ///   2 = Leaf  - small, short-lived, homes in hard
    /// This replaces the old "copy MagicMissile's AI" setup.
    /// </summary>
    public class LivingWandProjectile : ModProjectile
    {
        private const int Seed = 0;
        private const int Bloom = 1;
        private const int Leaf = 2;

        private int Mode => (int)Projectile.ai[0];
        private ref float Timer => ref Projectile.ai[1];

        private static readonly Color LeafGreen = new Color(130, 255, 110);
        private static readonly Color DarkGreen = new Color(30, 130, 60);
        private static readonly Color BloomPink = new Color(255, 165, 205);

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults()
        {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.timeLeft = 110;
            Projectile.penetrate = 2;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1; // each enemy can only be hit once by each projectile
        }

        public override void AI()
        {
            if (Projectile.localAI[0] == 0f)
                Init();

            Timer++;
            float speed = Projectile.localAI[1];

            switch (Mode)
            {
                case Bloom:
                    Projectile.velocity.Y += 0.14f; // lobbed arc
                    break;
                case Leaf:
                    Home(range: 520f, turn: 0.14f, delay: 4f, speed);
                    break;
                default:
                    Home(range: 380f, turn: 0.07f, delay: 12f, speed);
                    break;
            }

            if (Projectile.velocity != Vector2.Zero)
                Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            // little trail of leaves / spores
            if (Main.rand.NextBool(Mode == Bloom ? 2 : 3))
            {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Grass,
                    -Projectile.velocity * 0.05f + Main.rand.NextVector2Circular(0.5f, 0.5f), 0, default, 0.9f);
                d.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, 0.25f, 0.55f, 0.25f);
        }

        private void Init()
        {
            Projectile.localAI[0] = 1f;
            Projectile.localAI[1] = Projectile.velocity.Length(); // remember the launch speed

            switch (Mode)
            {
                case Bloom:
                    Projectile.penetrate = 1;
                    Projectile.timeLeft = 80;
                    Projectile.Resize(16, 16);
                    break;
                case Leaf:
                    Projectile.penetrate = 1;
                    Projectile.timeLeft = 70;
                    Projectile.Resize(8, 8);
                    break;
                default:
                    Projectile.penetrate = 2;
                    Projectile.timeLeft = 110;
                    break;
            }
        }

        private void Home(float range, float turn, float delay, float speed)
        {
            if (Timer <= delay)
                return;

            NPC target = FindTarget(range);
            if (target == null)
                return;

            Vector2 desired = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * speed;
            Vector2 steered = Vector2.Lerp(Projectile.velocity, desired, turn);
            Projectile.velocity = steered.SafeNormalize(desired.SafeNormalize(Vector2.UnitX)) * speed;
        }

        private NPC FindTarget(float range)
        {
            NPC best = null;
            float bestDistance = range;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (!npc.CanBeChasedBy(Projectile))
                    continue;

                float distance = Vector2.Distance(npc.Center, Projectile.Center);
                if (distance < bestDistance && Collision.CanHit(Projectile.Center, 1, 1, npc.Center, 1, 1))
                {
                    bestDistance = distance;
                    best = npc;
                }
            }

            return best;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            WeaponFX.DustCone(Projectile.Center, -Projectile.velocity, 0.9f, DustID.Grass, 5, 1.5f, 3.5f, 1f);

            // a normal seed has a 1-in-3 chance to sprout a leaf off the enemy it hit
            if (Mode == Seed && Projectile.owner == Main.myPlayer && Main.rand.NextBool(3))
                SpawnLeaves(1, target.Center, -MathHelper.PiOver2, MathHelper.ToRadians(70f), 9f);
        }

        public override void OnKill(int timeLeft)
        {
            int count = Mode == Bloom ? 14 : 6;
            WeaponFX.DustRing(Projectile.Center, DustID.Grass, count, Mode == Bloom ? 3.5f : 2f, 1.1f);

            if (Mode != Bloom)
                return;

            SoundEngine.PlaySound(SoundID.Item10 with { Pitch = 0.3f }, Projectile.Center);
            WeaponFX.DustRing(Projectile.Center, DustID.GreenTorch, 8, 2.5f, 1.2f);

            if (Projectile.owner == Main.myPlayer)
                SpawnLeaves(3, Projectile.Center, -MathHelper.PiOver2, MathHelper.ToRadians(55f), 9f);
        }

        private void SpawnLeaves(int count, Vector2 position, float centerAngle, float spread, float speed)
        {
            int damage = System.Math.Max(1, Projectile.damage / 2);

            for (int i = 0; i < count; i++)
            {
                float angle = centerAngle + (count == 1 ? Main.rand.NextFloat(-spread, spread)
                                                         : MathHelper.Lerp(-spread, spread, i / (float)(count - 1)));

                Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    position,
                    angle.ToRotationVector2() * speed,
                    Projectile.type,
                    damage,
                    Projectile.knockBack * 0.5f,
                    Projectile.owner,
                    ai0: Leaf);
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Vector2 center = Projectile.Center - Main.screenPosition;

            float size;
            Color head;
            switch (Mode)
            {
                case Bloom: size = 1.5f; head = BloomPink; break;
                case Leaf: size = 0.8f; head = LeafGreen; break;
                default: size = 1f; head = LeafGreen; break;
            }

            WeaponFX.DrawGlowTrail(Projectile, head, DarkGreen, 0.3f * size, 0.9f);
            WeaponFX.DrawGlow(center, WeaponFX.Additive(head, 0.6f), 0.55f * size);

            // two motes twisting around the seed, like a vine
            if (Mode != Leaf)
            {
                Vector2 forward = Projectile.velocity.SafeNormalize(Vector2.UnitX);
                Vector2 side = forward.RotatedBy(MathHelper.PiOver2);

                for (int i = 0; i < 2; i++)
                {
                    float phase = Timer * 0.45f + i * MathHelper.Pi;
                    Vector2 offset = side * (float)System.Math.Sin(phase) * 4.5f * size;
                    WeaponFX.DrawGlow(center + offset, WeaponFX.Additive(LeafGreen, 0.8f), 0.14f * size);
                }
            }

            Main.EntitySpriteDraw(texture, center, null, Color.White * Projectile.Opacity, Projectile.rotation,
                texture.Size() * 0.5f, Projectile.scale * size, SpriteEffects.None, 0);

            return false;
        }
    }
}