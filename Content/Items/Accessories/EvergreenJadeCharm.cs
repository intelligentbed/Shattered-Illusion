using Microsoft.Xna.Framework; 
using ShatteredIllusion.Content.Items.Materials;
using ShatteredIllusion.Content.Projectiles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace ShatteredIllusion.Content.Items.Accessories
{
    public class EvergreenJadeCharm : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 24;
            Item.height = 24;
            Item.value = Item.buyPrice(gold: 1, silver: 50);
            Item.rare = ItemRarityID.Blue;
            Item.accessory = true;
        }
        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            player.GetModPlayer<EvergreenJadeCharmPlayer>().hasEvergreenJadeCharm = true;
        }

        public override void AddRecipes()
        {
            Recipe recipe = CreateRecipe();
            recipe.AddIngredient(ModContent.ItemType<EvergreenCrystal>(), 3);
            recipe.AddIngredient(ItemID.Wood, 25);
            recipe.AddTile(TileID.Anvils);
            recipe.Register();
        }
    }
    public class EvergreenJadeCharmPlayer : ModPlayer
    {
        public bool hasEvergreenJadeCharm = false;

        public override void ResetEffects()
        {
            hasEvergreenJadeCharm = false;
        }

        public override void PostUpdateMiscEffects()
        {
            if (hasEvergreenJadeCharm)
            {
                Player.GetAttackSpeed(DamageClass.Melee) += 0.10f;
            }
        }

        // avtivates when hitting an enemy with tmelee 
        public override void OnHitNPCWithItem(Item item, NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (hasEvergreenJadeCharm && item.CountsAsClass(DamageClass.Melee))
            {
                TrySpawnJadeSprout(Player.GetSource_ItemUse(item));
            }
        }

        // same as the comment above but with projectile
        public override void OnHitNPCWithProj(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (hasEvergreenJadeCharm && proj.type != ModContent.ProjectileType<JadeSprout>() && proj.CountsAsClass(DamageClass.Melee))
            {
                TrySpawnJadeSprout(proj.GetSource_FromThis());
            }
        }

        private void TrySpawnJadeSprout(IEntitySource source)
        {
            // the chance to spawn a Jade Sprout is 1 in 2 so 50% chance
            if (Main.rand.NextBool(2) && Player.whoAmI == Main.myPlayer)
            {
   
                Vector2 direction = Vector2.Normalize(Main.MouseWorld - Player.Center);

                // Set speed
                Vector2 velocity = direction * 26f;

                Projectile.NewProjectile(
                    source,
                    Player.Center,
                    velocity,
                    ModContent.ProjectileType<JadeSprout>(),
                    5, // Base damage
                    2f, // Knockback
                    Player.whoAmI
                );
            }
        }
    }
}
