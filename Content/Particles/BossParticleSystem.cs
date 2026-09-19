using Microsoft.Xna.Framework.Graphics;
using ParticleLibrary.Core;
using ParticleLibrary.Core.V3;
using ParticleLibrary.Core.V3.Particles;
using Terraria;
using Terraria.ModLoader;

namespace ShatteredIllusion.Content.Particles
{
    /// <summary>
    /// Owns every particle buffer used by boss AI in the mod. Register new
    /// buffers here rather than creating one ModSystem per boss - buffers are
    /// cheap to keep around and this keeps all the wiring in one place.
    /// </summary>
    public class BossParticleSystem : ModSystem
    {
        /// <summary>Elongated glow streaks - dash charge-ups, converging trails.</summary>
        public static ParticleBuffer<SandStreakParticle> SandStreaks { get; private set; }

        /// <summary>Small outward sparks - parries, impacts, transition bursts.</summary>
        public static ParticleBuffer<EmberBurstParticle> EmberBursts { get; private set; }

        /// <summary>Expanding rings - phase transitions, burrow eruptions.</summary>
        public static ParticleBuffer<ShockwaveRingParticle> Shockwaves { get; private set; }

        /// <summary>Flickering vertical warning bars - falling/telegraphed attack lines.</summary>
        public static ParticleBuffer<TelegraphSegmentParticle> TelegraphSegments { get; private set; }

        /// <summary>Stationary pulsing rings - "this exact spot is about to get hit" markers.</summary>
        public static ParticleBuffer<GroundWarningRingParticle> GroundWarnings { get; private set; }

        /// <summary>Single growing focal point for long charge-up windups (e.g. King Slime's split).</summary>
        public static ParticleBuffer<ChargeCoreParticle> ChargeCores { get; private set; }

        // Never build particle buffers on the server - there's nothing to draw.
        public override bool IsLoadingEnabled(Mod mod) => !Main.dedServ;

        public override void OnModLoad()
        {
            // Buffer size = how many of that particle type can exist AT ONCE
            // before new ones stop spawning. 

            SandStreaks = new ParticleBuffer<SandStreakParticle>(200);
            ParticleManagerV3.RegisterUpdatable(SandStreaks);
            ParticleManagerV3.RegisterRenderable(Layer.BeforeNPCs, SandStreaks);

            EmberBursts = new ParticleBuffer<EmberBurstParticle>(150)
                .SetBlendState(BlendState.Additive); // glowy sparks
            ParticleManagerV3.RegisterUpdatable(EmberBursts);
            ParticleManagerV3.RegisterRenderable(Layer.BeforeNPCs, EmberBursts);

            Shockwaves = new ParticleBuffer<ShockwaveRingParticle>(16)
                .SetBlendState(BlendState.Additive); // glowy ring
            ParticleManagerV3.RegisterUpdatable(Shockwaves);
            ParticleManagerV3.RegisterRenderable(Layer.BeforeNPCs, Shockwaves);


            TelegraphSegments = new ParticleBuffer<TelegraphSegmentParticle>(400)
                .SetBlendState(BlendState.Additive);
            ParticleManagerV3.RegisterUpdatable(TelegraphSegments);
            ParticleManagerV3.RegisterRenderable(Layer.BeforeNPCs, TelegraphSegments);

            GroundWarnings = new ParticleBuffer<GroundWarningRingParticle>(16)
                .SetBlendState(BlendState.Additive); // glowy ring
            ParticleManagerV3.RegisterUpdatable(GroundWarnings);
            ParticleManagerV3.RegisterRenderable(Layer.BeforeNPCs, GroundWarnings);

            ChargeCores = new ParticleBuffer<ChargeCoreParticle>(8)
                .SetBlendState(BlendState.Additive);
            ParticleManagerV3.RegisterUpdatable(ChargeCores);
            ParticleManagerV3.RegisterRenderable(Layer.BeforeNPCs, ChargeCores);
        }
    }
}