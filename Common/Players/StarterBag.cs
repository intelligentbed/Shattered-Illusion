using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace ShatteredIllusion.Common.Players
{
    public class StarterBag : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 20;
            Item.height = 20;
            Item.maxStack = 1;
            Item.rare = ItemRarityID.Blue;
            Item.value = 0;
        }

        public override bool CanRightClick() => true;

        public override void ModifyItemLoot(ItemLoot itemLoot)
        {
            itemLoot.Add(ItemDropRule.Common(ItemID.Wood, 1, 100, 100));
            itemLoot.Add(ItemDropRule.Common(ItemID.RecallPotion, 1, 10, 10));
            itemLoot.Add(ItemDropRule.Common(ItemID.SwiftnessPotion, 1, 5, 5));
            itemLoot.Add(ItemDropRule.Common(ItemID.Torch, 1, 40, 40));
            itemLoot.Add(ItemDropRule.Common(ItemID.CopperBow, 1));
            itemLoot.Add(ItemDropRule.Common(ItemID.BabyBirdStaff, 1));
            itemLoot.Add(ItemDropRule.Common(ItemID.AmethystStaff, 1));
            itemLoot.Add(ItemDropRule.Common(ItemID.SilverBroadsword, 1));
            itemLoot.Add(ItemDropRule.Common(ItemID.WoodenArrow, 1, 100, 100));
            itemLoot.Add(ItemDropRule.Common(ItemID.WoodenHammer, 1));
            itemLoot.Add(ItemDropRule.Common(ItemID.Rope, 1, 10, 10));

            // Magic Storage integration we might add more cross mod stuff but 
            if (ModLoader.TryGetMod("MagicStorage", out Mod magicStorage))
            {
                if (magicStorage.TryFind("StorageHeart", out ModItem heart))
                    itemLoot.Add(ItemDropRule.Common(heart.Type, 1));

                if (magicStorage.TryFind("StorageUnit", out ModItem unit))
                    itemLoot.Add(ItemDropRule.Common(unit.Type, 1, 4, 4));

                if (magicStorage.TryFind("CraftingAccess", out ModItem crafting))
                    itemLoot.Add(ItemDropRule.Common(crafting.Type, 1));
            }
        }
    }
}
