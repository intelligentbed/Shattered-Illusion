using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ShatteredIllusion.Common.Systems
{
    /// <summary>
    /// Makes boss summoning items reusable instead of consumed on use.
    /// Works for vanilla and modded summons that register in
    /// <see cref="ItemID.Sets.SortingPriorityBossSpawns"/>.
    /// </summary>
    public class NonConsumableBossSummons : GlobalItem
    {
        // Event starters share the boss spawn sorting set but are not boss summons.
        // Remove an entry here to make that item reusable too.
        private static readonly int[] EventStarters =
        {
            ItemID.GoblinBattleStandard,
            ItemID.PirateMap,
            ItemID.SnowGlobe,
            ItemID.SolarTablet,
            ItemID.PumpkinMoonMedallion,
            ItemID.NaughtyPresent,
        };

        public static bool IsBossSummon(Item item)
        {
            if (item.type <= ItemID.None || item.type >= ItemLoader.ItemCount)
                return false;

            if (ItemID.Sets.SortingPriorityBossSpawns[item.type] < 0)
                return false;

            return System.Array.IndexOf(EventStarters, item.type) < 0;
        }

        // Clears the consumable flag so the tooltip and stack behavior match.
        public override void SetDefaults(Item item)
        {
            if (IsBossSummon(item))
                item.consumable = false;
        }

        // Backstop for items whose consumable flag is set after this runs.
        public override bool ConsumeItem(Item item, Player player)
        {
            return !IsBossSummon(item);
        }
    }
}