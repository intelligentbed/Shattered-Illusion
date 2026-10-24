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

        [Header("Cinematics")]

        // Master switch. Off = boss fights play exactly like the base version (no letterbox, zoom, flashes, hit-stop, extra particles).
        [DefaultValue(true)]
        public bool Cinematics;

        [DefaultValue(true)]
        public bool Letterbox;

        [DefaultValue(true)]
        public bool CameraZoom;

        [DefaultValue(false)]
        public bool Vignette;

        [DefaultValue(true)]
        public bool TitleCard;

        // Singleplayer only; the freeze-frames on slams, the transformation and the parry.
        [DefaultValue(true)]
        public bool HitStop;

        // Shockwave rings, ember bursts, slime chunks, dash trails, speed lines.
        [DefaultValue(true)]
        public bool ImpactParticles;

        // The growing ground ring that shows where the huge jump lands.
        // Independent of the master switch because it is a gameplay readability aid.
        [DefaultValue(true)]
        public bool SlamTelegraph;

        // Visual-only entrance during the boss spawn cutscene (e.g. King Slime drops from the sky and the crown lands on his head).
        [DefaultValue(true)]
        public bool IntroEntrance;

        // Camera, bars, flashes and freeze-frames of the death scene. The ~2.5s death delay itself always
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