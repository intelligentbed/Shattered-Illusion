using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ShatteredIllusion.Common.Systems;
using ShatteredIllusion.Content.Dusts;
using ShatteredIllusion.Content.Projectiles.Mage;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace ShatteredIllusion.Content.Items.Weapons.Mage
{
    public class StarryNight : ModItem
    {
        public override void SetDefaults()
        {
            Item.damage = 3;
            Item.DamageType = DamageClass.Magic;
            Item.mana = 8;

            Item.width = 26;
            Item.height = 26;

            Item.useTime = 24;
            Item.useAnimation = 24;
            Item.useStyle = ItemUseStyleID.Shoot;

            Item.noMelee = true;
            Item.knockBack = 2.5f;

            Item.value = Item.sellPrice(silver: 60);
            Item.rare = ItemRarityID.Blue;
            Item.UseSound = SoundID.Item9;
            Item.autoReuse = true;

            Item.shoot = ModContent.ProjectileType<StarryProjectile>();
            Item.shootSpeed = 9f;
        }

        // Start the bolt a little way out from the player so the flash happens at the "tip".
        public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity,
            ref int type, ref int damage, ref float knockback)
        {
            Vector2 muzzle = position + velocity.SafeNormalize(Vector2.UnitX * player.direction) * 30f;
            if (Collision.CanHit(position, 0, 0, muzzle, 0, 0))
                position = muzzle;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position,
            Vector2 velocity, int type, int damage, float knockback)
        {
            WeaponFX.DustCone(position, velocity, 0.5f, DustID.Enchanted_Gold, 7, 1.5f, 4.5f, 1f);

            for (int i = 0; i < 3; i++)
            {
                Dust.NewDustPerfect(position + Main.rand.NextVector2Circular(6f, 6f),
                    ModContent.DustType<LanternDust>(), velocity.SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(1f, 3f),
                    0, default, 1f);
            }

            return true; // let the game spawn the bolt normally
        }

        public override void PostDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor,
            float rotation, float scale, int whoAmI)
        {
            WeaponFX.DrawWorldAura(Item, rotation, scale, new Color(255, 214, 110));
        }
    }
}