using ParticleLibrary.Core.V3.Particles;
using System;

namespace ShatteredIllusion.Content.Particles
{
    /// <summary>
    /// A glowing core that grows over its lifetime, used as the focal point for
    /// the charging telegraph. Reuses the existing SoftGlow texture.
    /// </summary>
    public class ChargeCoreParticle : Behavior<ParticleInfo>
    {
        public override string Texture => "ShatteredIllusion/Content/Particles/SoftGlow";

        public override void Update(ref ParticleInfo info)
        {
            float progress = 1f - (info.Time / (float)info.Duration); // 0 -> 1

            // Ease-in growth.
            float grow = progress * progress;

            // Prevents a static-looking glow.
            float flicker = 0.85f + 0.15f * MathF.Sin(progress * 40f);

            info.Scale = info.InitialScale * (0.35f + 0.85f * grow) * flicker;
            info.Color = info.InitialColor * (0.4f + 0.6f * grow);

            // Bright flash just before the split.
            if (progress > 0.93f)
            {
                float flashT = (progress - 0.93f) / 0.07f;
                info.Color = info.InitialColor * MathHelperLerp(0.4f + 0.6f * grow, 2.2f, flashT);
                info.Scale = info.InitialScale * MathHelperLerp(0.35f + 0.85f * grow, 1.6f, flashT);
            }

            info.Time--;
        }

        private static float MathHelperLerp(float a, float b, float t) => a + (b - a) * Math.Clamp(t, 0f, 1f);
    }
}
