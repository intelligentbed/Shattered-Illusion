using System.ComponentModel;
using ShatteredIllusion.Common.GUI.SturdinessBar;
using Terraria.ModLoader.Config;

namespace ShatteredIllusion
{
    public enum HorizontalPosition
    {
        Left,
        Right
    }

    public class ShatteredIllusionConfig : ModConfig
    {
        public override ConfigScope Mode => ConfigScope.ClientSide;

        [Header("Client")]

        [DefaultValue(true)]
        public bool SturdinessBarLocked;

        [DefaultValue(SturdinessBarUI.DefaultPosX)]
        public float SturdinessBarPosX = SturdinessBarUI.DefaultPosX;

        [DefaultValue(SturdinessBarUI.DefaultPosY)]
        public float SturdinessBarPosY = SturdinessBarUI.DefaultPosY;

        [DefaultValue(HorizontalPosition.Left)]
        public HorizontalPosition MenuButtonPosition;

<<<<<<< Updated upstream
        [Header("Cinematics")]

        // Master switch. Off = boss fights play exactly like the base version (no letterbox, zoom, flashes, hit-stop, extra particles).
=======
        [Header("BossCinematics")]

        // Master switch for the cinematic layer used by boss fights. Off = fights play without any added letterbox, zoom, flashes, hit-stop or extra particles.
>>>>>>> Stashed changes
        [DefaultValue(true)]
        public bool Cinematics;

        [DefaultValue(true)]
        public bool Letterbox;

        [DefaultValue(true)]
        public bool CameraZoom;

<<<<<<< Updated upstream
        [DefaultValue(false)]
=======
        [DefaultValue(true)]
>>>>>>> Stashed changes
        public bool Vignette;

        [DefaultValue(true)]
        public bool TitleCard;

<<<<<<< Updated upstream
        // Singleplayer only; the freeze-frames on slams, the transformation and the parry.
=======
        // Singleplayer only; brief freeze-frames on big impacts and key moments.
>>>>>>> Stashed changes
        [DefaultValue(true)]
        public bool HitStop;

        // Shockwave rings, ember bursts, slime chunks, dash trails, speed lines.
        [DefaultValue(true)]
        public bool ImpactParticles;

<<<<<<< Updated upstream
        // The growing ground ring that shows where the huge jump lands.
=======
        // Ground rings / markers that show where big attacks will land.
>>>>>>> Stashed changes
        // Independent of the master switch because it is a gameplay readability aid.
        [DefaultValue(true)]
        public bool SlamTelegraph;

<<<<<<< Updated upstream
        // Visual-only entrance during the boss spawn cutscene (e.g. King Slime drops from the sky and the crown lands on his head).
        [DefaultValue(true)]
        public bool IntroEntrance;

        // Camera, bars, flashes and freeze-frames of the death scene. The ~2.5s death delay itself always
=======
        // Visual-only boss entrances during the spawn cutscene (e.g. King Slime dropping in).
        [DefaultValue(true)]
        public bool IntroEntrance;

        // Camera, bars, flashes and freeze-frames of boss death scenes. The death delay itself always
>>>>>>> Stashed changes
        // happens (it is gameplay), so every player in a multiplayer session sees the same thing.
        [DefaultValue(true)]
        public bool DeathSequence;

        [DefaultValue(1f)]
        [Range(0f, 2f)]
        [Increment(0.1f)]
        [Slider]
        public float ZoomIntensity = 1f;

        [DefaultValue(1f)]
        [Range(0f, 2f)]
        [Increment(0.1f)]
        [Slider]
        public float FlashIntensity = 1f;

        [DefaultValue(1f)]
        [Range(0f, 2f)]
        [Increment(0.1f)]
        [Slider]
        public float ShakeIntensity = 1f;
    }
}