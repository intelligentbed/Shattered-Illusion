using Microsoft.Xna.Framework;
using ShatteredIllusion.Content.Buffs.StatBuffs;
using ShatteredIllusion.Content.Items.Materials;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace ShatteredIllusion.Content.Armor.LivingWood
{
    [AutoloadEquip(EquipType.Head)]
    public class LivingWoodHelmet : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 20;
            Item.height = 20;
            Item.maxStack = 1;
            Item.rare = ItemRarityID.Blue;
            Item.defense = 1;
        }

        public override void UpdateEquip(Player player)
        {
            player.GetDamage(DamageClass.Summon) += 0.02f;
        }
        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ItemID.Wood, 10)
                .AddIngredient(ModContent.ItemType<EvergreenCrystal>(), 5)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }

    [AutoloadEquip(EquipType.Body)]
    public class LivingWoodChestplate : ModItem
    {
        private static readonly SoundStyle RootPulseSound = new SoundStyle("ShatteredIllusion/Sounds/RootPulse"); // currently is a placeholder

        private const int PulseInterval = 300;
        private const int SproutedBuffDuration = 180; // 3 seconds

        private int pulseTimer = 0;

        public override void SetDefaults()
        {
            Item.width = 20;
            Item.height = 20;
            Item.maxStack = 1;
            Item.rare = ItemRarityID.Blue;
            Item.defense = 3;
        }
        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ItemID.Wood, 15)
                .AddIngredient(ModContent.ItemType<EvergreenCrystal>(), 10)
                .AddTile(TileID.Anvils)
                .Register();
        }

        public override void UpdateEquip(Player player)
        {
            player.maxMinions += 1;
        }

        public override bool IsArmorSet(Item head, Item body, Item legs)
        {
            return head.type == ModContent.ItemType<LivingWoodHelmet>()
                && body.type == ModContent.ItemType<LivingWoodChestplate>()
                && legs.type == ModContent.ItemType<LivingWoodLeggings>();
        }

        public override void UpdateArmorSet(Player player)
        {
            player.lifeRegen += 1;
            player.setBonus =
                "Slowly regenerate life\n" +
                "Every few seconds, roots erupt around you on the ground empowering your minions";

            pulseTimer++;
            if (pulseTimer >= PulseInterval)
            {
                pulseTimer = 0;
                TriggerRootPulse(player);
            }
        }

        private void TriggerRootPulse(Player player)
        {
            player.AddBuff(ModContent.BuffType<SproutedBuff>(), SproutedBuffDuration);

            float offsetX = Main.rand.NextFloat(-80f, 80f); 
            Vector2 spawnPosition = player.Bottom + new Vector2(offsetX, 0f);

            int tileX = (int)(spawnPosition.X / 16);
            int tileY = (int)(spawnPosition.Y / 16);

            for (int j = 0; j < 10; j++)
            {
                if (WorldGen.InWorld(tileX, tileY + j) && Framing.GetTileSafely(tileX, tileY + j).HasTile)
                {
                    spawnPosition = new Vector2(tileX * 16 + 8, (tileY + j) * 16);
                    break;
                }
            }

            SoundEngine.PlaySound(RootPulseSound, spawnPosition);

            for (int i = 0; i < 24; i++)
            {
                Vector2 dustVelocity = Main.rand.NextVector2Circular(3f, 3f);
                dustVelocity.Y -= 2f;

                Dust dust = Dust.NewDustPerfect(spawnPosition, DustID.WoodFurniture, dustVelocity, Alpha: 60, Scale: Main.rand.NextFloat(1.2f, 2f));
                dust.noGravity = true;
            }

            for (int i = 0; i < 12; i++)
            {
                Vector2 dustVelocity = Main.rand.NextVector2Circular(2f, 2f);
                dustVelocity.Y -= 1.5f;

                Dust leaf = Dust.NewDustPerfect(spawnPosition, DustID.GemEmerald, dustVelocity, Alpha: 80, Scale: Main.rand.NextFloat(0.9f, 1.4f));
                leaf.noGravity = true;
            }
        }
    }

    [AutoloadEquip(EquipType.Legs)]
    public class LivingWoodLeggings : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 20;
            Item.height = 20;
            Item.maxStack = 1;
            Item.rare = ItemRarityID.Blue;
            Item.defense = 2;
        }
        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ItemID.Wood, 10)
                .AddIngredient(ModContent.ItemType<EvergreenCrystal>(), 3)
                .AddTile(TileID.Anvils)
                .Register();
        }

        public override void UpdateEquip(Player player)
        {
            player.GetDamage(DamageClass.Summon) += 0.02f;
        }
    }

}