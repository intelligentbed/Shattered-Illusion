using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ShatteredIllusion.Content.NPCs.BossAI.VanillaBosses.KingSlime
{
    //now you might be wondering why this is in a separate file and not in the main kingslime file
    //im sorry to tell you poor poor soul but this is because the main kingslime file is already a mess and i dont want to make it worse
    public class KingSlimeInstanceData : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        public int SpawnGraceTimer;
        public float SquishOffset;
        public float SquishSpringVel;
        public float LastVelocityY;
        public float WobbleClock;

        // Vanilla King Slime hitbox size, cached once so it can be restored if he
        // reverts from ninja form back to slime form (e.g. despawning mid-fight).
        public int OriginalWidth;
        public int OriginalHeight;

        public override bool AppliesToEntity(NPC npc, bool lateInstantiation) => npc.type == NPCID.KingSlime;
    }
}