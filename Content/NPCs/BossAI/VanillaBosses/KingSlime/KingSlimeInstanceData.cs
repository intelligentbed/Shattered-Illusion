using Microsoft.Xna.Framework;
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

        // Active minion count cached per game tick (see KingSlimeOverride.CountActiveMinions).
        public int ActiveMinionCount;
        public uint ActiveMinionCountTick = uint.MaxValue;

        // Set once the split-attack windup roar has played, cleared when he leaves that attack.
        public bool SplitRoarPlayed;

        // Vanilla King Slime hitbox size, cached once so it can be restored if he
        // reverts from ninja form back to slime form (e.g. despawning mid-fight).
        public int OriginalWidth;
        public int OriginalHeight;

        public int CinePhase = -1;
        public int CineStep = -1;
        public int HitStopFrames;
        public bool HitStopActive;
        public Vector2 HitStopVelocity;

        public int IntroBeats;
        public int DeathBeats;
        public bool AllowDeath;

        public override bool CheckDead(NPC npc) => !KingSlimeOverride.InterceptDeath(npc, this);

        public override bool AppliesToEntity(NPC npc, bool lateInstantiation) => npc.type == NPCID.KingSlime;
    }
}