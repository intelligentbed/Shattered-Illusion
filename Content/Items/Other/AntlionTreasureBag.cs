using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ShatteredIllusion.Content.Items.Other
{
    public class AntlionTreasureBag : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 20;
            Item.height = 20;
            Item.maxStack = 9999;
            Item.rare = ItemRarityID.Blue;
            Item.value = 50;
        }

        public override bool CanRightClick()
        {
            return true;
        }

        public override void RightClick(Player player)
        {
            player.QuickSpawnItem(player.GetSource_OpenItem(Item.type), ItemID.Wood, 100);
            player.QuickSpawnItem(player.GetSource_OpenItem(Item.type), ItemID.RecallPotion, 10);
            player.QuickSpawnItem(player.GetSource_OpenItem(Item.type), ItemID.SwiftnessPotion, 5);
            player.QuickSpawnItem(player.GetSource_OpenItem(Item.type), ItemID.Torch, 40);
            player.QuickSpawnItem(player.GetSource_OpenItem(Item.type), ItemID.CopperBow, 1);
            player.QuickSpawnItem(player.GetSource_OpenItem(Item.type), ItemID.BabyBirdStaff, 1);
            player.QuickSpawnItem(player.GetSource_OpenItem(Item.type), ItemID.AmethystStaff, 1);
            player.QuickSpawnItem(player.GetSource_OpenItem(Item.type), ItemID.SilverBroadsword, 1);
            player.QuickSpawnItem(player.GetSource_OpenItem(Item.type), ItemID.WoodenArrow, 100);
            player.QuickSpawnItem(player.GetSource_OpenItem(Item.type), ItemID.WoodenHammer, 1);
            player.QuickSpawnItem(player.GetSource_OpenItem(Item.type), ItemID.Rope, 10);
        }
    }
}