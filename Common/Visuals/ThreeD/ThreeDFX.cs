using Microsoft.Xna.Framework;
using Terraria;

namespace ShatteredIllusion.Common.Visuals.ThreeD
{
    // Plays Fx effects and spawns particles at a 3D position: projected once, sized and dimmed by depth.
    public static class ThreeDFX
    {
        public static FxHandle Play(FxDef def, Vector3 position, Vector2 dir = default, float scale = 1f, Color? tint = null)
        {
            Vector2 pos = ThreeDMath.Project(position, out float depthScale);
            Color color = (tint ?? def.DefaultTint).MultiplyRGB(ThreeDDraw.DepthTint(position.Z));

            return VisualFX.Play(def, pos, dir, scale * depthScale, color);
        }

        public static void ScreenRipple(Vector3 position, float radius, int frames = 30, float strength = 1f)
        {
            Vector2 pos = ThreeDMath.Project(position, out float s);
            ScreenFX.Ripple(pos, radius * s, frames, strength * s);
        }

        public static void Ring(Vector3 position, float size, Color color, int duration, float flatten = 1f)
        {
            Vector2 pos = ThreeDMath.Project(position, out float s);
            VisualFX.Ring(pos, size * s, color.MultiplyRGB(ThreeDDraw.DepthTint(position.Z)), duration, flatten);
        }

        public static void Flash(Vector3 position, float size, Color color, int duration)
        {
            Vector2 pos = ThreeDMath.Project(position, out float s);
            VisualFX.Flash(pos, size * s, color.MultiplyRGB(ThreeDDraw.DepthTint(position.Z)), duration);
        }

        public static void Sparks(Vector3 position, Vector2 dir, float spread, int count, float speedMin, float speedMax, Color color, float size, int duration)
        {
            Vector2 pos = ThreeDMath.Project(position, out float s);
            VisualFX.Sparks(pos, dir, spread, count, speedMin * s, speedMax * s, color.MultiplyRGB(ThreeDDraw.DepthTint(position.Z)), size * s, duration);
        }
    }
}
