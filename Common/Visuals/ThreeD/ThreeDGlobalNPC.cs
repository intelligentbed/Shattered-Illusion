using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace ShatteredIllusion.Common.Visuals.ThreeD
{
    public static class ThreeDNPCExtensions
    {
        // Gives any NPC depth: perspective-scaled and dimmed while drawn, contact damage only inside the hit window.
        public static void Set3D(this NPC npc, float z, float hitDepth = 80f)
        {
            ThreeDGlobalNPC g = npc.GetGlobalNPC<ThreeDGlobalNPC>();
            g.Enabled = true;
            g.Z = ThreeDMath.ClampZ(z);
            g.HitDepth = hitDepth;
        }

        public static void Clear3D(this NPC npc) => npc.GetGlobalNPC<ThreeDGlobalNPC>().Enabled = false;

        public static float Z3(this NPC npc) => npc.GetGlobalNPC<ThreeDGlobalNPC>().Z;
    }

    // Draw-time swap only: position and scale are restored right after the NPC draws, so AI and hitboxes never see the projection.
    public class ThreeDGlobalNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        public bool Enabled;
        public float Z;
        public float HitDepth = 80f;

        private Vector2 savedPosition;
        private float savedScale;
        private bool swapped;

        public override void DrawEffects(NPC npc, ref Color drawColor)
        {
            if (Enabled)
                drawColor = drawColor.MultiplyRGBA(ThreeDDraw.DepthTint(Z));
        }

        public override bool PreDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            if (!Enabled)
                return true;

            savedPosition = npc.position;
            savedScale = npc.scale;
            swapped = true;

            npc.Center = ThreeDMath.Project(new Vector3(npc.Center.X, npc.Center.Y, Z), out float scale);
            npc.scale *= scale;

            return true;
        }

        public override void PostDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            if (!swapped)
                return;

            npc.position = savedPosition;
            npc.scale = savedScale;
            swapped = false;
        }

        public override bool CanHitPlayer(NPC npc, Player target, ref int cooldownSlot)
            => !Enabled || MathF.Abs(Z - ThreeDMath.PlayerPlaneZ) <= HitDepth;
    }
}
