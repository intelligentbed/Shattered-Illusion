using ParticleLibrary.Core.V3.Particles;
using System;

namespace ShatteredIllusion.Content.Particles
{
    public class TelegraphBeamParticle : Behavior<ParticleInfo>
    {
        public override string Texture => "ShatteredIllusion/Content/Particles/BeamGradient";

        public override void Update(ref ParticleInfo info)
        {
            float progress = 1f - (info.Time / (float)info.Duration); // 0 -> 1

            const float fadeIn = 0.2f;
            const float fadeOutStart = 0.8f;

            float envelope;
            if (progress < fadeIn)
                envelope = progress / fadeIn;
            else if (progress > fadeOutStart)
                envelope = 1f - (progress - fadeOutStart) / (1f - fadeOutStart);
            else
                envelope = 1f; // flat and steady through the middle of its life

            info.Color = info.InitialColor * envelope;
            info.Scale = info.InitialScale; // no breathing/pulsing - keep it looking static

            info.Time--;
        }
    }
}