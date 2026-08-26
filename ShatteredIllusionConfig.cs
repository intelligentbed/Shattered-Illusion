using System.ComponentModel;
using Terraria.ModLoader.Config;

namespace ShatteredIllusion
{
    public class ShatteredIllusionConfig : ModConfig
    {
        public override ConfigScope Mode => ConfigScope.ClientSide;

        [Header("SturdinessBar")]

        [DefaultValue(true)]
        [Label("Lock Sturdiness Bar Position")]
        [Tooltip("When enabled, the Sturdiness bar stays fixed in place. Disable to drag it anywhere on screen.")]
        public bool SturdinessBarLocked;

        // NOTE: keep these in sync with ResolveBarUI DefaultPosX / DefaultPosY OR ELSE 
        public float SturdinessBarPosX = 35f;
        public float SturdinessBarPosY = 15f;
    }
}