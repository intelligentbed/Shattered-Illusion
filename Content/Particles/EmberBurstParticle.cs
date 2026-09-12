using ParticleLibrary.Core.V3.Particles;

namespace ShatteredIllusion.Content.Particles
{
    /// <summary>
    /// A small round glowing spark that flies outward, decelerates, and fades/shrinks.
    /// Uses the shared SoftGlow texture
    /// </summary>
    public class EmberBurstParticle : Behavior<ParticleInfo>
    {
        public override string Texture => "ShatteredIllusion/Content/Particles/SoftGlow";

        public override void Update(ref ParticleInfo info)
        {
            info.Velocity *= 0.90f;
            info.Position += info.Velocity;

            float progress = info.Time / (float)info.Duration; // 1 -> 0

            info.Scale = info.InitialScale * progress;
            info.Color = info.InitialColor * progress;

            info.Time--;
        }
    }
}
