using ParticleLibrary.Core.V3.Particles;
using System;

namespace ShatteredIllusion.Content.Particles
{
    /// <summary>
    /// A ring that stays put and pulses in brightness/size, instead of expanding
    /// and dying like <see cref="ShockwaveRingParticle"/>. Built for "this exact
    /// spot is about to get hit" markers - Huge Jump's landing zone, Split's
    /// re-merge point, King Slime Chunk's telegraphed orbit, etc.
    ///
    /// The pulse speeds up as the particle's lifetime runs out, so a single
    /// spawn reads like a heartbeat quickening right up to the real hit. Reuses
    /// the shared Ring texture - no new art needed.
    /// </summary>
    public class GroundWarningRingParticle : Behavior<ParticleInfo>
    {
        public override string Texture => "ShatteredIllusion/Content/Particles/Ring";

        public override void Update(ref ParticleInfo info)
        {
            float progress = 1f - (info.Time / (float)info.Duration); // 0 -> 1

            // Pulse frequency ramps up as progress approaches 1 - starts as a slow
            // breathing glow, ends as a rapid flicker right before the real attack lands.
            float pulseSpeed = 3f + progress * 9f;
            float pulse = 0.5f + 0.5f * MathF.Sin(progress * pulseSpeed * MathF.Tau);

            info.Color = info.InitialColor * (0.35f + 0.65f * pulse) * (1f - progress * 0.3f);
            info.Scale = info.InitialScale * (0.85f + 0.15f * pulse);

            info.Time--;
        }
    }
}
