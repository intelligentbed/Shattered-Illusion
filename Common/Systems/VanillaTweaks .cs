using System.Linq;
using ShatteredIllusion.Content.Items.Weapons.Melee;
using Terraria;
using Terraria.GameContent.ItemDropRules;
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

    /// <summary>
    /// Adds the Cha Crown to King Slime's Treasure Bag.
    ///
    /// First it tries to join the bag's existing "one of these" pool (so the crown competes with
    /// the vanilla pick instead of dropping on top of it). If it can't find that pool, it falls
    /// back to its own separate 1-in-FallbackChance roll so it still drops.
    /// </summary>
    public class KingSlimeBagLoot : GlobalItem
    {
        // Used only if the bag's pick-one rule can't be found.
        private const int FallbackChance = 3; // 1 in 3 bags

        // Items we expect to see in the bag's pick-one pool, used to recognise the right rule.
        private static readonly int[] PoolAnchors =
        {
            ItemID.SlimeHook,
            ItemID.SlimySaddle,
            ItemID.NinjaHood,
        };

        public override bool AppliesToEntity(Item entity, bool lateInstantiation)
        {
            return entity.type == ItemID.KingSlimeBossBag;
        }

        public override void ModifyItemLoot(Item item, ItemLoot itemLoot)
        {
            int chaCrown = ModContent.ItemType<ChaCrown>();

            foreach (IItemDropRule rule in itemLoot.Get(false))
            {
                int[] ids = null;

                if (rule is OneFromOptionsNotScaledWithLuckDropRule notScaled)
                    ids = notScaled.dropIds;
                else if (rule is OneFromOptionsDropRule scaled)
                    ids = scaled.dropIds;

                if (ids == null || !ids.Any(id => PoolAnchors.Contains(id)))
                    continue;

                int[] extended = ids.Append(chaCrown).ToArray();

                if (rule is OneFromOptionsNotScaledWithLuckDropRule a)
                    a.dropIds = extended;
                else if (rule is OneFromOptionsDropRule b)
                    b.dropIds = extended;

                Mod.Logger.Info("Cha Crown added to King Slime bag pick-one pool.");
                return;
            }

            // Couldn't find the pool - drop it separately instead.
            itemLoot.Add(ItemDropRule.Common(chaCrown, FallbackChance));
            Mod.Logger.Info("King Slime bag pool not found; Cha Crown added as a separate drop.");
        }
    }
}