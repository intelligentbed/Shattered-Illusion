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
        [Label("Lock Sturdiness Bar Position")]
        [Tooltip("When enabled, the Sturdiness bar stays fixed in place. Disable to drag it anywhere on screen.")]
        public bool SturdinessBarLocked;

        [DefaultValue(35f)]   // NOTE: keep these in sync with ResolveBarUI DefaultPosX / DefaultPosY OR ELSE 
        [Label("Sturdiness Bar X")]
        public float SturdinessBarPosX = 35f;

        [DefaultValue(15f)]
        [Label("Sturdiness Bar Y")]
        public float SturdinessBarPosY = 15f;

        [DefaultValue(HorizontalPosition.Left)]
        [Label("Menu Button Side")]
        [Tooltip("Which side of the screen the main menu buttons (Single Player, Multiplayer, etc.) are drawn on.")]
        public HorizontalPosition MenuButtonPosition;
    }
}