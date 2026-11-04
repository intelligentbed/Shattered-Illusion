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

        /// <summary>Steady rings with no pulse - respawn every tick for a smooth landing-zone marker.</summary>
        public static ParticleBuffer<GroundMarkerParticle> GroundMarkers { get; private set; }

        /// <summary>Single growing focal point for long charge-up windups (e.g. King Slime's split).</summary>
        public static ParticleBuffer<ChargeCoreParticle> ChargeCores { get; private set; }

        /// <summary>Quick soft glow pops - impact flashes, charge-up finishers. Drawn above NPCs.</summary>
        public static ParticleBuffer<FlashParticle> Flashes { get; private set; }

        // Never build particle buffers on the server - there's nothing to draw.
        public override bool IsLoadingEnabled(Mod mod) => !Main.dedServ;

        public override void OnModLoad()
        {
            // Buffer size = how many of that particle type can exist AT ONCE
            // before new ones stop spawning. ParticleLibrary's ParticleLimit setting
            // divides every size below by 8 (Low), 4 (Medium) or 2 (High).
            // SandStreaks: PhaseTransition alone keeps ~36 alive at once, so even at Low (/8) it needs 36+ with room for other effects.

            SandStreaks = new ParticleBuffer<SandStreakParticle>(600)
                .SetBlendState(BlendState.Additive);
            ParticleManagerV3.RegisterUpdatable(SandStreaks);
            ParticleManagerV3.RegisterRenderable(Layer.BeforeNPCs, SandStreaks);

            EmberBursts = new ParticleBuffer<EmberBurstParticle>(400)
                .SetBlendState(BlendState.Additive);
            ParticleManagerV3.RegisterUpdatable(EmberBursts);
            ParticleManagerV3.RegisterRenderable(Layer.BeforeNPCs, EmberBursts);

            Shockwaves = new ParticleBuffer<ShockwaveRingParticle>(48)
                .SetBlendState(BlendState.Additive);
            ParticleManagerV3.RegisterUpdatable(Shockwaves);
            ParticleManagerV3.RegisterRenderable(Layer.BeforeNPCs, Shockwaves);


            TelegraphSegments = new ParticleBuffer<TelegraphSegmentParticle>(600)
                .SetBlendState(BlendState.Additive);
            ParticleManagerV3.RegisterUpdatable(TelegraphSegments);
            ParticleManagerV3.RegisterRenderable(Layer.BeforeNPCs, TelegraphSegments);

            GroundWarnings = new ParticleBuffer<GroundWarningRingParticle>(32)
                .SetBlendState(BlendState.Additive);
            ParticleManagerV3.RegisterUpdatable(GroundWarnings);
            ParticleManagerV3.RegisterRenderable(Layer.BeforeNPCs, GroundWarnings);

            GroundMarkers = new ParticleBuffer<GroundMarkerParticle>(32)
                .SetBlendState(BlendState.Additive);
            ParticleManagerV3.RegisterUpdatable(GroundMarkers);
            ParticleManagerV3.RegisterRenderable(Layer.BeforeNPCs, GroundMarkers);

            ChargeCores = new ParticleBuffer<ChargeCoreParticle>(16)
                .SetBlendState(BlendState.Additive);
            ParticleManagerV3.RegisterUpdatable(ChargeCores);
            ParticleManagerV3.RegisterRenderable(Layer.BeforeNPCs, ChargeCores);

            Flashes = new ParticleBuffer<FlashParticle>(32)
                .SetBlendState(BlendState.Additive);
            ParticleManagerV3.RegisterUpdatable(Flashes);
            ParticleManagerV3.RegisterRenderable(Layer.BeforeProjectiles, Flashes);
        }
    }
}