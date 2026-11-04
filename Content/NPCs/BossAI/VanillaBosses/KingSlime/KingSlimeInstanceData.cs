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

        public bool ArenaActive;
        public float ArenaCenterX;
        public float ArenaFloorY;
        public Vector2 ArenaMin;
        public Vector2 ArenaMax;
        public Vector2 ArenaTargetMin;
        public Vector2 ArenaTargetMax;
        public float CrownT;
        public Vector2 WeaponFrom;
        public Vector2 WeaponAnchor;
        public float WeaponMorph;
        public int WeaponSide;
        public int NinjaClock;

        // Ninja walk-cycle phase. Advances with ground speed so the legs match the movement.
        public float WalkClock;

        // Obstacle handling while walking (jump cooldown, and how long he's been wedged against something).
        public int NinjaJumpCooldown;
        public int NinjaStuck;

        // Shadow Dive: hover height (Y) for the leap.
        public Vector2 NinjaLeapPoint;

        // Smoothed floor-impact X for the Echo Dive telegraph (tracks the player, then locks).
        public float EchoTargetX;

        // Weapon -> weapon transformation: the first weapon melts into the goo blob and the next one
        // grows straight out of it instead of dropping to the floor in between.
        public bool WeaponHandoff;
        public bool WeaponHandoffIn;

        public int NinjaComboDone;
        public int NinjaComboTotal;
        public bool NinjaFlicker;
        public bool NinjaSingleSlash;
        public int NinjaCounterLife;

        public int IntroBeats;
        public int DeathBeats;
        public bool AllowDeath;

        public override bool CheckDead(NPC npc) => !KingSlimeOverride.InterceptDeath(npc, this);

        public override bool AppliesToEntity(NPC npc, bool lateInstantiation) => npc.type == NPCID.KingSlime;
    }
}