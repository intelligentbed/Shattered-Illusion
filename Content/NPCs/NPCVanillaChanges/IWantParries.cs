using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using ShatteredIllusion.Common.Players.ParrySystem;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ShatteredIllusion.Content.NPCs.NPCVanillaChanges
{
    internal interface IWantParries
    {
    }

    // vanilla enemies that actually swing a melee weapon, strict list on purpose
    // plain contact damage guys (slimes, bats, worms etc) never go in here
    public static class VanillaMeleeParry
    {
        // how close a player has to be for the swing to count as a parryable attack
        public const float SwingRange = 90f;

        public static readonly HashSet<int> MeleeWielders = new HashSet<int>
        {
            // pre hardmode
            NPCID.UndeadMiner,
            NPCID.UndeadViking,
            NPCID.DoctorBones,
            NPCID.GoblinPeon,
            NPCID.GoblinThief,
            NPCID.GoblinWarrior,

            // hardmode
            NPCID.ArmoredViking,
            NPCID.Paladin,
            NPCID.CrimsonAxe,
            NPCID.CursedHammer,
            NPCID.EnchantedSword,

            NPCID.BlueArmoredBonesMace,
            NPCID.BlueArmoredBonesSword,
            NPCID.RustyArmoredBonesAxe,
            NPCID.RustyArmoredBonesFlail,
            NPCID.RustyArmoredBonesSword,
            NPCID.RustyArmoredBonesSwordNoArmor,
            NPCID.HellArmoredBonesMace,
            NPCID.HellArmoredBonesSword,

            // events
            NPCID.Butcher,
            NPCID.Psycho,
            NPCID.Reaper,
            NPCID.PirateDeckhand,
            NPCID.PirateCorsair,
            NPCID.PirateCaptain,
            NPCID.MisterStabby,
            NPCID.HeadlessHorseman,
            NPCID.Nutcracker,
            NPCID.NutcrackerSpinning,
        };

        public static bool IsParryable(NPC npc)
        {
            if (!npc.active || npc.friendly || !MeleeWielders.Contains(npc.type))
                return false;

            if (npc.target < 0 || npc.target >= Main.maxPlayers)
                return false;

            Player target = Main.player[npc.target];
            if (!target.active || target.dead)
                return false;

            return Vector2.Distance(npc.Hitbox.ClosestPointInRect(target.Center), target.Center) <= SwingRange;
        }
    }

    // wrapper so TryGetParryable can hand back an IParryable for plain vanilla npcs
    public class VanillaMeleeParryable : IParryable
    {
        private readonly NPC npc;

        public VanillaMeleeParryable(NPC npc)
        {
            this.npc = npc;
        }

        public bool IsParryable => VanillaMeleeParry.IsParryable(npc);

        public void OnParried(Player player)
        {
        }
    }

    // red tint while the swing is parryable, no tutorial for these
    public class VanillaMeleeParryTint : GlobalNPC
    {
        public override bool AppliesToEntity(NPC entity, bool lateRequest)
        {
            return VanillaMeleeParry.MeleeWielders.Contains(entity.type);
        }

        public override void DrawEffects(NPC npc, ref Color drawColor)
        {
            if (!VanillaMeleeParry.IsParryable(npc))
                return;

            float pulse = 0.5f + 0.15f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 18f);
            drawColor = Color.Lerp(drawColor, new Color(255, 60, 60), pulse);
        }
    }
}