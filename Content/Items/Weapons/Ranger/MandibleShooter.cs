using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ShatteredIllusion.Common.Systems;
using ShatteredIllusion.Content.Projectiles.Ranger;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace ShatteredIllusion.Content.Items.Weapons.Ranger
{
    internal class MandibleShooter : ModItem
    {
        public override void SetDefaults()
        {
            Item.damage = 28;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 40;
            Item.height = 20;
            Item.useTime = 30;
            Item.useAnimation = 30;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.knockBack = 3f;
            Item.value = Item.sellPrice(gold: 1);
            Item.rare = ItemRarityID.Green;
            Item.UseSound = SoundID.Item11;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<MandibleShooterBullet>();
            Item.shootSpeed = 12f; // unused

            Item.useAmmo = AmmoID.None;
        }

        public override Vector2? HoldoutOffset()
        {
            return new Vector2(2f, -4f);
        }

        public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity,
            ref int type, ref int damage, ref float knockback)
        {
            Vector2 aimDir = (Main.MouseWorld - position).SafeNormalize(Vector2.UnitX * player.direction);
            float muzzleLength = 34f; // distance in pixels from hand
            position += aimDir * muzzleLength;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            Vector2 target = Main.MouseWorld;

            Projectile right = SpawnBullet(source, position, target, damage, knockback, player.whoAmI, side: 1f);
            Projectile left = SpawnBullet(source, position, target, damage, knockback, player.whoAmI, side: -1f);

            // Pair them up so they can draw the thread between them.
            if (right.ModProjectile is MandibleShooterBullet r && left.ModProjectile is MandibleShooterBullet l)
            {
                r.PartnerWho = left.whoAmI;
                l.PartnerWho = right.whoAmI;
                right.netUpdate = true;
                left.netUpdate = true;
            }

            // muzzle flash
            Vector2 aimDir = (target - position).SafeNormalize(Vector2.UnitX * player.direction);
            WeaponFX.DustCone(position, aimDir, 0.55f, DustID.Sand, 8, 2f, 6f, 1.2f);
            WeaponFX.DustCone(position, aimDir, 0.3f, DustID.Sandstorm, 4, 3f, 7f, 1f);

            return false;
        }

        private Projectile SpawnBullet(EntitySource_ItemUse_WithAmmo source, Vector2 position,
            Vector2 target, int damage, float knockback, int owner, float side)
        {
            int type = ModContent.ProjectileType<MandibleShooterBullet>();

            Projectile proj = Projectile.NewProjectileDirect(source, position, Vector2.Zero,
                type, damage, knockback, owner, ai0: side);

            if (proj.ModProjectile is MandibleShooterBullet bullet)
            {
                bullet.Start = position;
                bullet.Target = target;
                proj.netUpdate = true;
            }

            return proj;
        }
    }
}