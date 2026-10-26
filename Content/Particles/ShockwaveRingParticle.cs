using ParticleLibrary.Core.V3.Particles;
using System;

namespace ShatteredIllusion.Content.Particles
{
    /// <summary>
    /// A ring that expands outward from a fixed point and fades as it grows.
    /// </summary>
    public class ShockwaveRingParticle : Behavior<ParticleInfo>
    {
        public override string Texture => "ShatteredIllusion/Content/Particles/Ring";

        public override void Initialize(ref ParticleInfo info)
        {
            // Start collapsed
            info.Scale = System.Numerics.Vector2.Zero;
        }

        public override void Update(ref ParticleInfo info)
        {
            float progress = 1f - (info.Time / (float)info.Duration); 

            // Ease-out 
            float eased = 1f - MathF.Pow(1f - progress, 3f);

            info.Scale = info.InitialScale * eased;
            info.Color = info.InitialColor * (1f - progress); // fade out as it expands

            // freed slots keep their last scale and are still drawn, so collapse it on the final frame
            if (info.Time <= 1)
                info.Scale = System.Numerics.Vector2.Zero;

            info.Time--;
        }
    }
}
