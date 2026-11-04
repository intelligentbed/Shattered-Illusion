using System;
using Microsoft.Xna.Framework;
using Terraria;

namespace ShatteredIllusion.Common.Visuals.ThreeD
{
    public enum ThreeDLayer
    {
        BehindWorld,
        BehindNPCs,
        OverPlayers
    }

    // Where the vanishing point sits and what gets subtracted from the result.
    // World: vanish at the camera center, offset = screenPosition. UI: vanish anywhere in UI pixels, no offset.
    public readonly struct View3
    {
        public readonly Vector2 Vanish;
        public readonly Vector2 Offset;

        public View3(Vector2 vanish, Vector2 offset)
        {
            Vanish = vanish;
            Offset = offset;
        }

        public static View3 World => new(ThreeDMath.CameraCenter, Main.screenPosition);

        public static View3 Screen(Vector2 vanish) => new(vanish, Vector2.Zero);
    }

    // +Z is toward the camera, Z = 0 is the player's plane, scale = FocalLength / (FocalLength - Z).
    public static class ThreeDMath
    {
        public const float FocalLength = 800f;
        public const float MaxZ = FocalLength - 450f;
        public const float MinZ = -3000f;
        public const float PlayerPlaneZ = 0f;

        public static Vector2? CameraCenterOverride;

        public static Vector2 CameraCenter => CameraCenterOverride ?? Main.LocalPlayer.Center;

        public static float ClampZ(float z) => MathHelper.Clamp(z, MinZ, MaxZ);

        public static float ScaleAt(float z) => FocalLength / (FocalLength - ClampZ(z));

        // How far from c's projected spot anything within r of c can land on screen. Includes the depth shear, so far-off shapes don't clip.
        public static float ScreenReach(Vector3 c, float r, Vector2 vanish)
        {
            float s = ScaleAt(c.Z);
            float hi = ScaleAt(c.Z + r);
            float lo = ScaleAt(c.Z - r);

            return r * hi + (c.XY() - vanish).Length() * MathF.Max(hi - s, s - lo);
        }

        // 2D world position (no screenPosition subtracted), used for hitboxes.
        public static Vector2 Project(Vector3 position, out float scale) => Project(position, CameraCenter, out scale);

        // Same projection with the camera center passed in, so loops read Main.LocalPlayer.Center once instead of per element.
        public static Vector2 Project(Vector3 position, Vector2 center, out float scale)
        {
            scale = ScaleAt(position.Z);
            return center + (new Vector2(position.X, position.Y) - center) * scale;
        }

        public static Vector2 Project(Vector3 position) => Project(position, out _);

        // Draw-space position for any view (world minus screenPosition, or UI pixels).
        public static Vector2 Project(Vector3 position, in View3 view, out float scale)
        {
            scale = ScaleAt(position.Z);
            return view.Vanish + (new Vector2(position.X, position.Y) - view.Vanish) * scale - view.Offset;
        }

        public static Vector3 ToVector3(Vector2 position, float z = 0f) => new(position.X, position.Y, z);

        public static Vector2 XY(this Vector3 v) => new(v.X, v.Y);
    }
}
