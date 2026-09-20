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
    }
}