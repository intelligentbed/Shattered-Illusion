using System.ComponentModel;
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

        [DefaultValue(35f)]   // NOTE: keep these in sync with ResolveBarUI DefaultPosX / DefaultPosY OR ELSE 
        public float SturdinessBarPosX = 35f;

        [DefaultValue(15f)]
        [Label("Sturdiness Bar Y")]
        public float SturdinessBarPosY = 15f;

        [DefaultValue(HorizontalPosition.Left)]
        public HorizontalPosition MenuButtonPosition;
    }
}