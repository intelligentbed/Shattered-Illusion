using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.IO;
using Terraria;
using Terraria.ModLoader;

namespace ShatteredIllusion.Core.OverrideSystem
{

    public abstract class NPCBehaviorOverride
    {
        /// <summary>The NPC type this override applies to.</summary>
        public abstract int NPCOverrideType { get; }

        /// <summary>Called once when the override is registered (mod load).</summary>
        public virtual void Load() { }

        /// <summary>Called after vanilla/other mods' SetDefaults for this NPC.</summary>
        public virtual void SetDefaults(NPC npc) { }

        /// <summary>Return false to fully replace NPC.AI() with your own logic.</summary>
        public virtual bool PreAI(NPC npc) => true;

        public virtual void SendExtraData(NPC npc, BinaryWriter writer) { }

        public virtual void ReceiveExtraData(NPC npc, BinaryReader reader) { }

        /// <summary>Custom frame/animation selection.</summary>
        public virtual void FindFrame(NPC npc, int frameHeight) { }

        /// <summary>Return false to suppress the default sprite draw.</summary>
        public virtual bool PreDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) => true;

        /// <summary>Return false to prevent this NPC from despawning.</summary>
        public virtual bool CheckDead(NPC npc) => true;

        /// <summary>Adjust this NPC's loot table before it's rolled on death.</summary>
        public virtual void ModifyNPCLoot(NPC npc, NPCLoot npcLoot) { }
    }

    /// <summary>
    /// Wraps an override instance with cached flags for which virtuals it implements,
    /// so hooks can skip calling ones that were never overridden.
    /// </summary>
    public record NPCBehaviorOverrideContainer(
        NPCBehaviorOverride BehaviorOverride,
        bool HasPreAI,
        bool HasFindFrame,
        bool HasPreDraw,
        bool HasCheckDead,
        bool HasModifyNPCLoot);
}