using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria;
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

        //Adds the ability to be right clicked
        public override bool CanRightClick()
        {
            return true;
        }

        public override void RightClick(Player player)
        {
            // Give the player some starter items when open bag, also VSC added this idk how it did but it works.
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
            // PLEASE ADD MAGICX STORAGE SUPPORT IDk HOW TO DO THAT PLEASE DO THAT PLEASE
        }
    }
}
