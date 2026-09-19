using ParticleLibrary.Core.V3.Particles;
using System;

namespace ShatteredIllusion.Content.Particles
{
    /// <summary>
    /// A soft filled disc rendered underneath <see cref="GroundWarningRingParticle"/>
    /// to give the danger zone more visual weight.
    /// </summary>
    public class GroundGlowFillParticle : Behavior<ParticleInfo>
    {
        public override string Texture => "ShatteredIllusion/Content/Particles/GlowDiscFill";

        public override void Update(ref ParticleInfo info)
        {
            float progress = 1f - (info.Time / (float)info.Duration); // 0 -> 1

            float pulseSpeed = 3f + progress * 9f;
            float pulse = 0.5f + 0.5f * MathF.Sin(progress * pulseSpeed * MathF.Tau);

            // Dimmer than the ring so the edge stays dominant.
            info.Color = info.InitialColor * (0.22f + 0.35f * pulse) * (1f - progress * 0.3f);
            info.Scale = info.InitialScale * (0.9f + 0.1f * pulse);

            info.Time--;
        }
    }
}
