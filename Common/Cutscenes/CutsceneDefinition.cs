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
    internal static class CutsceneDefinition
    {
        public static CutsceneData Get(int npcType)
        {
            if (npcType == ModContent.NPCType<GreatAntlionCharger>())
            {
                return new CutsceneData
                {
                    TitleKey = "Mods.ShatteredIllusion.Cutscenes.GreatAntlionCharger.Title",
                    Zoom = 1.9f,
                    ShakeStart = 0,
                    ShakeDuration = 200,
                    ShakeMagnitude = 4,
                    Sound = new SoundStyle("ShatteredIllusion/Sounds/Silence"),
                    SoundStart = 0,
                    SoundDuration = 120
                };
            }

            if (npcType == NPCID.KingSlime)
            {
                return new CutsceneData
                {
                    TitleKey = "Mods.ShatteredIllusion.Cutscenes.KingSlime.Title",
                    FreezeNpc = false,
                    ShakeStart = 120,
                    ShakeDuration = 60,
                    ShakeMagnitude = 4,
                    Sound = new SoundStyle("ShatteredIllusion/Sounds/Silence"), //im sorry barkfart but im showcasing right now so bye bye
                    SoundStart = 60,
                    SoundDuration = 120
                };
            }

            return null;
        }
    }
}