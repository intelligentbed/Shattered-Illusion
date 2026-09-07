using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace ShatteredIllusion.Content.Items.SummonItems
{
    public class AntlionAttractor : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 16;
            Item.height = 16;
            Item.maxStack = 9999;
            Item.useTurn = true;
            Item.autoReuse = true;
            Item.useAnimation = 15;
            Item.useTime = 10;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.consumable = false;
        }
    }
    public class AntlionAttractorDrop : GlobalNPC
    {
        public override void ModifyNPCLoot(NPC npc, NPCLoot npcLoot)
        {
            if (npc.type is NPCID.Antlion or NPCID.WalkingAntlion or NPCID.GiantFlyingAntlion)
            {
                npcLoot.Add(ItemDropRule.Common(
                    ModContent.ItemType<Items.SummonItems.AntlionAttractor>(),
                    chanceDenominator: 4,
                    minimumDropped: 1,
                    maximumDropped: 2));
            }
        }
    }
}
