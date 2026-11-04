using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ShatteredIllusion.Common.Systems;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace ShatteredIllusion.Content.Items.Weapons.Summoner
{
    /// <summary>
    /// Summons glowing bluebell blooms. Each bloom flings homing petals at your target, AND
    /// the blooms link up with glowing threads. When you have 2 or more, they circle your
    /// target and the threads between them slice through it (3+ blooms close into a ring).
    /// More blooms = a tighter web = more damage, so every minion slot counts.
    /// </summary>
    public class BluebellStaff : ModItem
    {
        public override void SetStaticDefaults()
        {
            ItemID.Sets.GamepadWholeScreenUseRange[Type] = true;
            ItemID.Sets.LockOnIgnoresCollision[Type] = true;
        }

        public override void SetDefaults()
        {
            Item.width = 64;
            Item.height = 90;

            // Balance target (pre-Eye of Cthulhu, minion slots are scarce):
            //   1 bloom ~13 dps, 2 blooms ~35 dps, 3 blooms ~66 dps
            Item.damage = 11;
            Item.DamageType = DamageClass.Summon;
            Item.mana = 10;
            Item.knockBack = 2f;

            Item.useTime = 30;
            Item.useAnimation = 30;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.noMelee = true;
            Item.UseSound = SoundID.Item44;

            Item.shoot = ModContent.ProjectileType<BluebellMinion>();
            Item.buffType = ModContent.BuffType<BluebellBuff>();

            Item.value = Item.sellPrice(gold: 1);
            Item.rare = ItemRarityID.Green;
        }

        // PLACEHOLDER recipe using vanilla materials - swap in your own.
        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ItemID.Moonglow, 6)
                .AddIngredient(ItemID.Wood, 20)
                .AddTile(TileID.Anvils)
                .Register();
        }

        public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity,
            ref int type, ref int damage, ref float knockback)
        {
            position = Main.MouseWorld; // the bloom appears at your cursor
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position,
            Vector2 velocity, int type, int damage, float knockback)
        {
            player.AddBuff(Item.buffType, 2);

            Projectile minion = Projectile.NewProjectileDirect(source, position, Vector2.Zero, type, damage, knockback, player.whoAmI);
            minion.originalDamage = Item.damage;

            WeaponFX.DustRing(position, DustID.BlueTorch, 10, 3f, 1.2f);
            return false;
        }
    }

    public class BluebellBuff : ModBuff
    {
        // Borrowed vanilla icon - replace with your own Buff icon .png when you have one.
        public override string Texture => "Terraria/Images/Buff_" + BuffID.Sunflower;

        public override void SetStaticDefaults()
        {
            Main.buffNoSave[Type] = true;
            Main.buffNoTimeDisplay[Type] = true;
        }

        public override void Update(Player player, ref int buffIndex)
        {
            if (player.ownedProjectileCounts[ModContent.ProjectileType<BluebellMinion>()] > 0)
            {
                player.buffTime[buffIndex] = 18000;
            }
            else
            {
                player.DelBuff(buffIndex);
                buffIndex--;
            }
        }
    }

    public class BluebellMinion : ModProjectile
    {
        // texture: BluebellMinion.png next to the staff (the cropped flower head)

        private const float PetalSpeed = 11f;
        private const int PetalInterval = 45;
        private const float PetalDamage = 0.9f;  // fraction of the minion's damage
        private const float ThreadDamage = 0.4f; // threads tick often, so they hit softer
        private const float ThreadWidth = 14f;

        private int nextWho = -1; // the bloom this one is linked to (threads), -1 = none

        private static readonly Color Blue = new Color(90, 160, 255);
        private static readonly Color Pale = new Color(200, 230, 255);

        public override void SetStaticDefaults()
        {
            Main.projPet[Projectile.type] = true;
            ProjectileID.Sets.MinionSacrificable[Projectile.type] = true;
            ProjectileID.Sets.MinionTargettingFeature[Projectile.type] = true;
        }

        public override void SetDefaults()
        {
            Projectile.width = 26;
            Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.minion = true;
            Projectile.minionSlots = 1f;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.netImportant = true;
            Projectile.ContinuouslyUpdateDamageStats = true;

            // each thread can only hit the same enemy twice a second
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 30;
        }

        // Only threads ever deal contact damage, and they hit softer than petals.
        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            modifiers.SourceDamage *= ThreadDamage;
        }

        public override bool? CanCutTiles() => false;
        public override bool MinionContactDamage() => false;

        public override void AI()
        {
            Player owner = Main.player[Projectile.owner];
            if (!CheckActive(owner))
                return;

            // --- find my place among the other blooms
            int count = 0;
            int index = 0;
            int firstWho = -1;
            int followingWho = -1;

            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (!p.active || p.owner != Projectile.owner || p.type != Projectile.type)
                    continue;

                if (firstWho < 0)
                    firstWho = i;
                if (i < Projectile.whoAmI)
                    index++;
                else if (i > Projectile.whoAmI && followingWho < 0)
                    followingWho = i;

                count++;
            }

            // a chain; with 3 or more the last one links back to the first, closing the ring
            nextWho = followingWho >= 0 ? followingWho : (count >= 3 ? firstWho : -1);

            // --- where should I float?
            NPC target = FindTarget(owner);
            Vector2 anchor;
            float radius;
            float angle;

            if (target != null)
            {
                anchor = target.Center;
                radius = count == 1 ? 70f : 36f; // a tight ring so the threads cut through it
                angle = Main.GlobalTimeWrappedHourly * 2f + MathHelper.TwoPi * index / count;
            }
            else
            {
                anchor = owner.Center + new Vector2(0f, -60f);
                radius = 26f + 10f * count;
                angle = Main.GlobalTimeWrappedHourly * 1.2f + MathHelper.TwoPi * index / count;
            }

            Vector2 desired = anchor + angle.ToRotationVector2() * radius;
            Vector2 toDesired = desired - Projectile.Center;
            float maxSpeed = target != null ? 20f : 13f;

            Vector2 wanted = toDesired * 0.18f;
            if (wanted.Length() > maxSpeed)
                wanted = wanted.SafeNormalize(Vector2.Zero) * maxSpeed;

            Projectile.velocity = Vector2.Lerp(Projectile.velocity, wanted, 0.3f);

            if (Vector2.Distance(Projectile.Center, owner.Center) > 1600f)
            {
                Projectile.Center = owner.Center;
                Projectile.netUpdate = true;
            }

            // --- fire a petal at the target
            if (target != null && Projectile.owner == Main.myPlayer
                && (Main.GameUpdateCount + (uint)(index * 12)) % PetalInterval == 0)
            {
                Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    Projectile.Center,
                    (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitY) * PetalSpeed,
                    ModContent.ProjectileType<BluebellPetal>(),
                    System.Math.Max(1, (int)(Projectile.damage * PetalDamage)),
                    Projectile.knockBack * 0.5f,
                    Projectile.owner);
            }

            // drifting pollen
            if (Main.rand.NextBool(8))
            {
                Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(12f, 8f),
                    DustID.BlueTorch, new Vector2(0f, Main.rand.NextFloat(0.2f, 0.7f)), 0, default, 0.8f);
                d.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, 0.2f, 0.4f, 0.8f);
        }

        private bool CheckActive(Player owner)
        {
            int buff = ModContent.BuffType<BluebellBuff>();

            if (owner.dead || !owner.active)
            {
                owner.ClearBuff(buff);
                return false;
            }

            if (owner.HasBuff(buff))
                Projectile.timeLeft = 2;

            return true;
        }

        private NPC FindTarget(Player owner)
        {
            // the player can aim minions by right-clicking / using a whip-style target
            if (owner.HasMinionAttackTargetNPC)
            {
                NPC chosen = Main.npc[owner.MinionAttackTargetNPC];
                if (chosen.CanBeChasedBy(Projectile) && Vector2.Distance(chosen.Center, owner.Center) < 1200f)
                    return chosen;
            }

            NPC best = null;
            float bestDistance = 800f;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (!npc.CanBeChasedBy(Projectile))
                    continue;

                float distance = Vector2.Distance(npc.Center, owner.Center);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = npc;
                }
            }

            return best;
        }

        // The bloom's body is harmless. Only the thread to the NEXT bloom hurts.
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            Projectile other = LinkedBloom();
            if (other == null)
                return false;

            float point = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                Projectile.Center, other.Center, ThreadWidth, ref point);
        }

        private Projectile LinkedBloom()
        {
            if (nextWho < 0 || nextWho >= Main.maxProjectiles)
                return null;

            Projectile other = Main.projectile[nextWho];
            if (!other.active || other.type != Projectile.type || other.owner != Projectile.owner || other.whoAmI == Projectile.whoAmI)
                return null;

            return other;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            float time = Main.GlobalTimeWrappedHourly;
            Projectile linked = LinkedBloom();

            // --- thread to the next bloom, with a bright spark running along it
            if (linked != null)
            {
                float flicker = 0.75f + 0.25f * (float)System.Math.Sin(time * 14f + Projectile.whoAmI);
                WeaponFX.DrawLine(Projectile.Center, linked.Center, WeaponFX.Additive(Blue, 0.4f * flicker), 6f);
                WeaponFX.DrawLine(Projectile.Center, linked.Center, WeaponFX.Additive(Pale, 0.9f * flicker), 1.5f);

                float travel = (time * 0.8f + Projectile.whoAmI * 0.37f) % 1f;
                Vector2 spark = Vector2.Lerp(Projectile.Center, linked.Center, travel);
                WeaponFX.DrawGlow(spark - Main.screenPosition, WeaponFX.Additive(Pale, 0.9f), 0.3f);
            }

            // --- the bloom, bobbing and swaying
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            float bob = (float)System.Math.Sin(time * 3f + Projectile.whoAmI) * 3f;
            float sway = (float)System.Math.Sin(time * 2f + Projectile.whoAmI) * 0.15f;
            Vector2 center = Projectile.Center - Main.screenPosition + new Vector2(0f, bob);

            WeaponFX.DrawGlow(center, WeaponFX.Additive(Blue, 0.45f), 1f);

            Main.EntitySpriteDraw(texture, center, null, Color.White * Projectile.Opacity, sway,
                texture.Size() * 0.5f, 0.9f, SpriteEffects.None, 0);

            return false;
        }
    }

    /// <summary>The petals the blooms throw: small, homing, a little faint glow.</summary>
    public class BluebellPetal : ModProjectile
    {
        // Drawn entirely in code, so this is just a placeholder texture that's never shown.
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.WoodenArrowFriendly;

        private const float HomingRange = 320f;
        private const float TurnRate = 0.12f;

        private ref float Timer => ref Projectile.localAI[0];
        private ref float Speed => ref Projectile.localAI[1];

        private static readonly Color Blue = new Color(90, 160, 255);
        private static readonly Color Pale = new Color(200, 230, 255);

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
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 90;
            Projectile.extraUpdates = 1;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
        }

        public override void AI()
        {
            if (Speed == 0f)
                Speed = Projectile.velocity.Length();

            Timer++;

            if (Timer > 6f)
            {
                NPC target = FindTarget();
                if (target != null)
                {
                    Vector2 desired = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * Speed;
                    Vector2 steered = Vector2.Lerp(Projectile.velocity, desired, TurnRate);
                    Projectile.velocity = steered.SafeNormalize(desired.SafeNormalize(Vector2.UnitX)) * Speed;
                }
            }

            if (Projectile.velocity != Vector2.Zero)
                Projectile.rotation = Projectile.velocity.ToRotation();

            if (Main.rand.NextBool(3))
            {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.BlueTorch, Vector2.Zero, 0, default, 0.7f);
                d.noGravity = true;
            }
        }

        private NPC FindTarget()
        {
            NPC best = null;
            float bestDistance = HomingRange;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (!npc.CanBeChasedBy(Projectile))
                    continue;

                float distance = Vector2.Distance(npc.Center, Projectile.Center);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = npc;
                }
            }

            return best;
        }

        public override void OnKill(int timeLeft)
        {
            WeaponFX.DustRing(Projectile.Center, DustID.BlueTorch, 6, 2.5f, 0.9f);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Vector2 forward = Projectile.rotation.ToRotationVector2();
            Vector2 side = forward.RotatedBy(MathHelper.PiOver2);
            Vector2 center = Projectile.Center - Main.screenPosition;

            WeaponFX.DrawGlowTrail(Projectile, Pale, Blue, 0.22f, 0.8f);

            // a little diamond-shaped petal
            WeaponFX.DrawLine(Projectile.Center - forward * 5f, Projectile.Center + side * 2.5f, WeaponFX.Additive(Pale, 1f), 2f);
            WeaponFX.DrawLine(Projectile.Center + side * 2.5f, Projectile.Center + forward * 6f, WeaponFX.Additive(Pale, 1f), 2f);
            WeaponFX.DrawLine(Projectile.Center + forward * 6f, Projectile.Center - side * 2.5f, WeaponFX.Additive(Blue, 1f), 2f);
            WeaponFX.DrawLine(Projectile.Center - side * 2.5f, Projectile.Center - forward * 5f, WeaponFX.Additive(Blue, 1f), 2f);
            WeaponFX.DrawGlow(center, WeaponFX.Additive(Blue, 0.6f), 0.3f);

            return false;
        }
    }
}