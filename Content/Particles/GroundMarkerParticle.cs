using ParticleLibrary.Core.V3.Particles;

namespace ShatteredIllusion.Content.Particles
{
    // Steady ring with no pulse of its own. Respawn it every tick (duration 2) for a smooth, non-stacking marker.
    public class GroundMarkerParticle : Behavior<ParticleInfo>
    {
        public override string Texture => "ShatteredIllusion/Content/Particles/Ring";

        public override void Update(ref ParticleInfo info)
        {
            info.Time--;
        }
    }
}
