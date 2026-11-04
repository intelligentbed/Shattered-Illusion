using System;
using Microsoft.Xna.Framework;

namespace ShatteredIllusion.Common.Visuals.ThreeD
{
    // Movement state any owner can carry: projectile, NPC, UI element, shape.
    public struct Body3
    {
        public Vector3 Pos;
        public Vector3 Vel;
        public Vector3 Accel;
        public Vector3 Prev;

        public Body3(Vector3 pos)
        {
            Pos = pos;
            Prev = pos;
            Vel = Vector3.Zero;
            Accel = Vector3.Zero;
        }

        public void Begin() => Prev = Pos;

        public void Integrate()
        {
            Vel += Accel;
            Pos += Vel;
            Pos.Z = ThreeDMath.ClampZ(Pos.Z);
        }

        public void Step()
        {
            Begin();
            Integrate();
        }

        // True if the body can touch the player's plane this tick, including crossing the whole window in one step.
        public readonly bool InHitDepth(float hitDepth)
        {
            float plane = ThreeDMath.PlayerPlaneZ;
            bool inside = MathF.Abs(Pos.Z - plane) <= hitDepth;
            bool crossed = (Prev.Z < plane - hitDepth && Pos.Z > plane + hitDepth) ||
                           (Prev.Z > plane + hitDepth && Pos.Z < plane - hitDepth);

            return inside || crossed;
        }

        public readonly ThreeDLayer LayerFor(float hitDepth, float? layerDepthOverride = null)
        {
            if (Pos.Z < -(layerDepthOverride ?? hitDepth))
                return ThreeDLayer.BehindWorld;

            return Pos.Z < ThreeDMath.PlayerPlaneZ ? ThreeDLayer.BehindNPCs : ThreeDLayer.OverPlayers;
        }

        public void Teleport(Vector3 position)
        {
            position.Z = ThreeDMath.ClampZ(position.Z);
            Pos = position;
            Prev = position;
        }

        public void MoveToward(Vector3 target, float speed)
        {
            Vector3 toTarget = target - Pos;
            float length = toTarget.Length();

            Vel = length < 0.01f ? Vector3.Zero : toTarget / length * MathF.Min(speed, length);
        }

        public void Homing(Vector3 target, float speed, float turnRate)
        {
            Vector3 toTarget = target - Pos;

            if (toTarget.LengthSquared() < 0.0001f)
                return;

            Vel = Vector3.Lerp(Vel, Vector3.Normalize(toTarget) * speed, MathHelper.Clamp(turnRate, 0f, 1f));
        }

        public void Launch(Vector3 direction, float speed)
        {
            Vel = direction.LengthSquared() < 0.0001f ? Vector3.Zero : Vector3.Normalize(direction) * speed;
        }

        public void MoveDepth(float speed) => Vel.Z = speed;

        // Circles a point on the X/Z plane. Sets Pos directly and clears Vel and Accel.
        public void Orbit(Vector3 center, float radius, float angularSpeed)
        {
            float angle = MathF.Atan2(Pos.Z - center.Z, Pos.X - center.X) + angularSpeed;

            Vel = Vector3.Zero;
            Accel = Vector3.Zero;
            Pos = center + new Vector3(MathF.Cos(angle) * radius, 0f, MathF.Sin(angle) * radius);
        }
    }
}
