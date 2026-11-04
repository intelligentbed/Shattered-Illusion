using System;
using Microsoft.Xna.Framework;
using ShatteredIllusion.Content.Items.TreasureBags;
using ShatteredIllusion.Common.Systems;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.Events;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace ShatteredIllusion.Common.Players
{
    public class StartingPlayer : ModPlayer
    {
        public override IEnumerable<Item> AddStartingItems(bool mediumCoreDeath)
        {
            if (!mediumCoreDeath)
            {
                yield return new Item(ModContent.ItemType<StarterBag>());
            }
        }

        public override void ResetEffects()
        {
            Player.moveSpeed += 0.05f;
            Player.pickSpeed -= 0.05f;
            Player.moveSpeed += 23f / 15f - 1f;
        }

        // runs at the end of vanilla death, so this overrides its respawn timer
        public override void Kill(double damage, int hitDirection, bool pvp, PlayerDeathReason damageSource)
        {
            int seconds = 3;
            if (BossDespawnTweak.BossAlive)
                seconds = 15;
            else if (EventActive())
                seconds = 6;

            Player.respawnTimer = seconds * 60;
        }

        public override void OnRespawn()
        {
            Player.statLife = Player.statLifeMax2;
        }

        private static bool EventActive()
        {
            return Main.invasionType != 0 || Main.pumpkinMoon || Main.snowMoon
                || Main.eclipse || DD2Event.Ongoing || NPC.LunarApocalypseIsUp;
        }
    }
}