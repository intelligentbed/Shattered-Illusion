using ParticleLibrary.Core.V3.Particles;
using System;
using SystemVector2 = System.Numerics.Vector2;

namespace ShatteredIllusion.Content.Particles
{
    /// <summary>
    /// A short glowing streak stretched along its direction of travel.
    /// provides a sense of motion and speed. prob gonna use for dash charge-ups, converging trails, etc.
    /// </summary>
    public class SandStreakParticle : Behavior<ParticleInfo>
    {
        public override string Texture => "ShatteredIllusion/Content/Particles/SoftGlow";

        public override void Initialize(ref ParticleInfo info)
        {
            // Orient the streak to face the direction it's moving.
            info.Rotation = MathF.Atan2(info.Velocity.Y, info.Velocity.X);
        }

        public override void Update(ref ParticleInfo info)
        {
            info.Position += info.Velocity;
            info.Velocity *= 0.93f;

            // Keep the streak pointed the right way even as velocity curves/decays.
            if (info.Velocity.LengthSquared() > 0.01f)
            {
                info.Rotation = MathF.Atan2(info.Velocity.Y, info.Velocity.X);
            }

            float progress = info.Time / (float)info.Duration; // 1 -> 0 over its life

            // Shrink length as it dies but keep the width fairly constant -
            // reads as the streak "catching up to itself" and vanishing.
            info.Scale = new SystemVector2(info.InitialScale.X * progress, info.InitialScale.Y);
            info.Color = info.InitialColor * progress;

            info.Time--;
        }
    }
}
