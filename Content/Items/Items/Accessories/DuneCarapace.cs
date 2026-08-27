using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ShatteredIllusion.Content.Items.Accessories
{
    internal class DuneCarapace : ModItem
    {
        private const int BaseDefense = 4; 
        private const int BonusDefense = 8;
        private const float HealthThreshold = 0.25f; 

        public override void SetDefaults()
        {
            Item.width = 26;
            Item.height = 26;
            Item.accessory = true;

            Item.value = Item.sellPrice(gold: 5);
            Item.rare = ItemRarityID.LightRed;
            Item.defense = BaseDefense;
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            float lifeRatio = (float)player.statLife / player.statLifeMax2;

            if (lifeRatio < HealthThreshold)
            {
                float scale = 1f - (lifeRatio / HealthThreshold); 
                int bonus = (int)System.Math.Round(BonusDefense * scale);
                player.statDefense += bonus;
            }
        }

    }
}