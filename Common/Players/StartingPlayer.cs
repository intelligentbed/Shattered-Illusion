using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

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
	}
}