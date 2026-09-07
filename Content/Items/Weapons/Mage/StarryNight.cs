using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using ShatteredIllusion.Content.Projectiles.Mage;

namespace ShatteredIllusion.Content.Items.Weapons.Mage
{
    public class StarryNight : ModItem
    {
        public override void SetDefaults()
        {
            Item.damage = 3 ;
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
    }
}