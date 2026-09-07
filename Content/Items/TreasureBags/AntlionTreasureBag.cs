using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;
using ShatteredIllusion.Content.Items.Weapons.Melee;
using ShatteredIllusion.Content.Items.Weapons.Mage;
using ShatteredIllusion.Content.Items.Weapons.Ranger;
using ShatteredIllusion.Content.NPCs.BossAI.GreatAntlionCharger;

namespace ShatteredIllusion.Content.Items.TreasureBags
{
    public class AntlionTreasureBag : ModItem
    {
        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 3;
            ItemID.Sets.BossBag[Item.type] = true;
        }

        public override void SetDefaults()
        {
            Item.width = 24;
            Item.height = 24;
            Item.maxStack = Item.CommonMaxStack;
            Item.consumable = true;
            Item.expert = true;
            Item.rare = ItemRarityID.Blue;
            Item.value = 50;
        }

        public override void ModifyResearchSorting(ref ContentSamples.CreativeHelper.ItemGroup itemGroup)
        {
            itemGroup = ContentSamples.CreativeHelper.ItemGroup.BossBags;
        }

        public override bool CanRightClick() => true;

        public override void ModifyItemLoot(ItemLoot itemLoot)
        {
            // Money — tied to the boss's value
            itemLoot.Add(ItemDropRule.CoinsBasedOnNPCValue(ModContent.NPCType<GreatAntlionCharger>()));

            // Weapon pool — one random pick from these
            itemLoot.Add(ItemDropRule.OneFromOptions(1, new int[]
            {
                ModContent.ItemType<MandibleShooter>(),
                ModContent.ItemType<DuneRendPincers>()
            }));
        }
    }
}