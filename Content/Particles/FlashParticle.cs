using ParticleLibrary.Core.V3.Particles;
using System;

namespace ShatteredIllusion.Content.Particles
{
    public class FlashParticle : Behavior<ParticleInfo>
    {
        public override string Texture => "ShatteredIllusion/Content/Particles/SoftGlow";

        public override void Update(ref ParticleInfo info)
        {
            float progress = 1f - (info.Time / (float)info.Duration); // 0 -> 1

            float pop = 1f - MathF.Pow(1f - progress, 3f);
            float fade = 1f - progress;

            info.Scale = info.InitialScale * (0.5f + 0.5f * pop);
            info.Color = info.InitialColor * (fade * fade);

            if (info.Time <= 1)
                info.Scale = System.Numerics.Vector2.Zero;

            info.Time--;
        }
    }
}
