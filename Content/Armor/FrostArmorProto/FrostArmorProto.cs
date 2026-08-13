using Terraria.ModLoader;
using Terraria;
using Terraria.ID;

namespace ShatteredIllusion.Content.Armor.FrostArmorProto
{
    [AutoloadEquip(EquipType.Head)]
    public class FrostHelmet : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 20;
            Item.height = 20;
            Item.maxStack = 1;
            Item.rare = ItemRarityID.White;
            Item.defense = 3;
        }

        public override void UpdateEquip(Player player)
        {
            player.GetDamage(DamageClass.Ranged) += 0.01f;
        }
        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ItemID.IceBlock, 10)
                .AddIngredient(ItemID.Sapphire, 1)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }

    [AutoloadEquip(EquipType.Body)]
    public class FrostChestplate : ModItem //chestpiece//
    {
        public override void SetDefaults()
        {
            Item.width = 20;
            Item.height = 20;
            Item.maxStack = 1;
            Item.rare = ItemRarityID.White;
            Item.defense = 4;
        }
        public override void AddRecipes() //RECIPIES//
        {
            CreateRecipe()
                .AddIngredient(ItemID.IceBlock, 15)
                .AddIngredient(ItemID.Sapphire, 2)
                .AddTile(TileID.Anvils)
                .Register();
        }

        public override void UpdateEquip(Player player)
        {
            player.GetDamage(DamageClass.Ranged) += 0.03f;
        }

        public override bool IsArmorSet(Item head, Item body, Item legs)
        {
            return head.type == ModContent.ItemType<FrostHelmet>()
                && body.type == ModContent.ItemType<FrostChestplate>()
                && legs.type == ModContent.ItemType<FrostLeggings>();
        }

        public override void UpdateArmorSet(Player player) //SET BONUS//
        {
            player.GetModPlayer<FrostArmorPlayer>().frostSet = true;
            player.setBonus = "Ranged hits inflict Frostburn";
        }
    }

    [AutoloadEquip(EquipType.Legs)]
    public class FrostLeggings : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 20;
            Item.height = 20;
            Item.maxStack = 1;
            Item.rare = ItemRarityID.White;
            Item.defense = 3;
        }
        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ItemID.IceBlock, 10)
                .AddIngredient(ItemID.Sapphire, 1)
                .AddTile(TileID.Anvils)
                .Register();
        }

        public override void UpdateEquip(Player player)
        {
            player.GetDamage(DamageClass.Ranged) += 0.01f;
        }
    }

    public class FrostArmorPlayer : ModPlayer
    {
        public bool frostSet;

        public override void ResetEffects()
        {
            frostSet = false;
        }
    }

    public class FrostArmorGlobalProjectile : GlobalProjectile
    {
        public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (!projectile.npcProj && !projectile.trap)
            {
                Player owner = Main.player[projectile.owner];
                if (owner.active && owner.GetModPlayer<FrostArmorPlayer>().frostSet
                    && projectile.DamageType == DamageClass.Ranged)
                {
                    target.AddBuff(BuffID.Frostburn, 180);
                }
            }
        }
    }
}