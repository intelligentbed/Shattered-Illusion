using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using ShatteredIllusion.Common.Visuals;
using ShatteredIllusion.Common.Systems;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;
using ReLogic.Content;
using ReLogic.Graphics;

namespace ShatteredIllusion.Common.GUI.JournalUI
{
    /// <summary>
    /// Shared sizing for the journal's letterbox bars. Uses Cinema's formula (so both look consistent on any
    /// aspect ratio) with thinner limits, and respects the player's letterbox config toggle.
    /// </summary>
    internal static class JournalBackdrop
    {
        private const float MinBarFraction = 0.04f;
        private const float MaxBarFraction = 0.09f;

        // Height in UI pixels of one journal letterbox bar at full strength, or 0 if letterboxing is disabled.
        public static float TargetBarHeight(float uiWidth, float uiHeight)
        {
            if (!Cinema.LetterboxAllowed)
            {
                return 0f;
            }

            return Cinema.LetterboxBarHeight(uiWidth, uiHeight, MinBarFraction, MaxBarFraction);
        }
    }
}