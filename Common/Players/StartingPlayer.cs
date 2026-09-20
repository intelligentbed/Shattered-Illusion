using System;
using Microsoft.Xna.Framework;
using ShatteredIllusion.Content.Items.TreasureBags;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace ShatteredIllusion.Common.Players
{
    public class StartingPlayer : ModPlayer
    {
        public override IEnumerable<Item> AddStartingItems(bool mediumCoreDeath)
        {
            if (!mediumCoreDeath)
            {
                yield return new Item(ModContent.ItemType<StarterBag>());
            }
        }

        public override void ResetEffects()
        {
            Player.moveSpeed += 0.05f;
            Player.pickSpeed -= 0.05f;
        }
    }
}