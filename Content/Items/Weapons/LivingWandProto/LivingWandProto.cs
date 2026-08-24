using Terraria.ModLoader;
using Terraria;
using Terraria.ID;
using ShatteredIllusion.Content.Items.Materials;

namespace ShatteredIllusion.Content.Items.Weapons.LivingWandProto
{
    public class LivingWandProto : ModItem
    {
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
            Item.value = 10466;
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
    }

    public class LivingWandProjectile : ModProjectile
    {
        public override void SetDefaults()
        {
            Projectile.width = 5;
            Projectile.height = 5;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.timeLeft = 600;
            AIType = ProjectileID.MagicMissile;
            Projectile.damage = 15;
            Projectile.penetrate = 3;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 4800; 
        }
    }
}