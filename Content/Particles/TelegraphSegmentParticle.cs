using ParticleLibrary.Core.V3.Particles;
using System;

namespace ShatteredIllusion.Content.Particles
{
    /// <summary>
    /// A short vertical glow bar that pulses in then out over its (short)
    /// life instead of just fading
    /// </summary>
    public class TelegraphSegmentParticle : Behavior<ParticleInfo>
    {
        public override string Texture => "ShatteredIllusion/Content/Particles/SoftGlow";

        public override void Update(ref ParticleInfo info)
        {
            float progress = 1f - (info.Time / (float)info.Duration); // 0 -> 1
            float pulse = MathF.Sin(MathF.PI * progress);             // 0 -> 1 -> 0, one flicker

            info.Color = info.InitialColor * pulse;
            info.Scale = info.InitialScale * (0.7f + 0.3f * pulse);   // slight "breathing" size too

            info.Time--;
        }
    }
}
