using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace ShatteredIllusion.Common.Players
{
	public class StartingPlayer : ModPlayer
	{
	//I'd like to thank Cal devs for leaving thier githubup as a reference
		public override IEnumerable<Item> AddStartingItems(bool mediumCoreDeath)
		{
			if (!mediumCoreDeath)
			{
				yield return new Item(ModContent.ItemType<StarterBag>());
			}
		}
	}
}
