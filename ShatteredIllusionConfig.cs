using System.ComponentModel;
using Terraria.ModLoader.Config;

namespace ShatteredIllusion
{
    public class ShatteredIllusionConfig : ModConfig
    {
        public override ConfigScope Mode => ConfigScope.ClientSide;

        [Header("ResolveBar")]

        [DefaultValue(true)]
        [Label("Lock Resolve Bar Position")]
        [Tooltip("When enabled, the Resolve bar stays fixed in place. Disable to drag it anywhere on screen.")]
        public bool ResolveBarLocked;

        // These are percentages of screen width/height, not pixel coordinates, so the
        // saved position scales correctly across different resolutions. Not meant to be
        // hand-edited in the config UI — set by dragging the bar in-game.
        // NOTE: keep these in sync with ResolveBarUI.DefaultPosX / DefaultPosY.
        public float ResolveBarPosX = 35f;

        public float ResolveBarPosY = 15f;
    }
}