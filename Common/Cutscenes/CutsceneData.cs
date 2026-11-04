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
    public class CutsceneData
    {
        public string TitleKey = "Mods.ShatteredIllusion.Cutscenes.Unknown.Title";
        public int TotalTicks = 220;
        public int IntroTicks = 45;
        public int OutroTicks = 40;
        public float Zoom = 1.8f;
        public bool FreezeNpc = true;

        public int ShakeStart = -1;
        public int ShakeDuration;
        public int ShakeMagnitude;

        public SoundStyle? Sound;
        public int SoundStart = -1;
        public int SoundDuration;
    }
}