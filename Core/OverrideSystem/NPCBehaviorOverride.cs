using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.IO;
using Terraria;
using Terraria.ModLoader;

namespace ShatteredIllusion.Core.OverrideSystem
{
    public abstract class NPCBehaviorOverride
    {
        /// <summary>
        /// The NPC type this override applies to.
        /// </summary>
        public abstract int NPCOverrideType { get; }

        /// <summary>
        /// Called once when the override is registered during mod loading.
        /// </summary>
        public virtual void Load()
        {
        }

        /// <summary>
        /// Called after vanilla and other mods' SetDefaults for this NPC.
        /// </summary>
        public virtual void SetDefaults(NPC npc)
        {
        }

        /// <summary>
        /// Return false to replace the NPC's default AI.
        /// </summary>
        public virtual bool PreAI(NPC npc) => true;

        /// <summary>
        /// Sends custom NPC state to clients.
        /// Store per-NPC state in the NPC's ai/localAI or per-entity data.
        /// </summary>
        public virtual void SendExtraData(NPC npc, BinaryWriter writer)
        {
        }

        /// <summary>
        /// Receives custom NPC state from the server.
        /// </summary>
        public virtual void ReceiveExtraData(NPC npc, BinaryReader reader)
        {
        }

        /// <summary>
        /// Custom frame and animation selection.
        /// </summary>
        public virtual void FindFrame(NPC npc, int frameHeight)
        {
        }

        /// <summary>
        /// Return false to suppress the default sprite draw.
        /// </summary>
        public virtual bool PreDraw(
            NPC npc,
            SpriteBatch spriteBatch,
            Vector2 screenPos,
            Color drawColor)
            => true;

        /// <summary>
        /// Return false to prevent this NPC from dying.
        /// </summary>
        public virtual bool CheckDead(NPC npc) => true;

        /// <summary>
        /// Adjust this NPC's loot table before it is rolled on death.
        /// </summary>
        public virtual void ModifyNPCLoot(NPC npc, NPCLoot npcLoot)
        {
        }
    }

    /// <summary>
    /// Stores an override instance and cached information about which hooks
    /// the override actually implements.
    ///
    /// NPCBehaviorOverride instances are created once per NPC type.
    /// They must therefore not contain mutable state belonging to an
    /// individual NPC entity.
    /// </summary>
    public record NPCBehaviorOverrideContainer(
        NPCBehaviorOverride BehaviorOverride,
        bool HasPreAI,
        bool HasFindFrame,
        bool HasPreDraw,
        bool HasCheckDead,
        bool HasModifyNPCLoot);
}