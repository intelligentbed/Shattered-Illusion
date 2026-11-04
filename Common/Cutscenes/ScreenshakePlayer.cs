using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Utilities;
using ShatteredIllusion.Content.NPCs.BossAI.GreatAntlionCharger;
using ShatteredIllusion.Core.Packets;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace ShatteredIllusion.Common.Cutscenes
{
    public class ScreenshakePlayer : ModPlayer
    {
        public int screenshakeTimer;
        public int screenshakeMagnitude;

        public void StartShake(int duration, int magnitude)
        {
            if (Main.dedServ) return;

            screenshakeTimer = Math.Max(screenshakeTimer, duration);
            screenshakeMagnitude = Math.Max(screenshakeMagnitude, magnitude);
        }

        public override void ModifyScreenPosition()
        {
            if (Player.whoAmI != Main.myPlayer) return;
            if (screenshakeTimer <= 0) return;

            screenshakeTimer--;

            Main.screenPosition += new Vector2(
                Main.rand.Next(-screenshakeMagnitude, screenshakeMagnitude + 1),
                Main.rand.Next(-screenshakeMagnitude, screenshakeMagnitude + 1));

            if (screenshakeTimer <= 0)
                screenshakeMagnitude = 0;
        }
    }
}