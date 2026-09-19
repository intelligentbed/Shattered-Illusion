using ShatteredIllusion.Core.OverrideSystem;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ShatteredIllusion.Content.NPCs.BossAI.VanillaBosses.EoC
{
    public class EyeofCthulhuOverride : NPCBehaviorOverride
    {
        public override int NPCOverrideType => NPCID.EyeofCthulhu;

        // The attack register for the boss.
        public enum AttackPhase
        {
            Placeholder
        }

        private static readonly AttackPhase[] AttackSequence =
        {
            AttackPhase.Placeholder,
        };

        // NPC AI slots.
        private const int AttackTimer = 0;
        private const int SequenceIndex = 1;
        private const int AttackStep = 2;

        // NPC local AI slots.
        private const int DespawnTimer = 0;

        private const float DespawnDistance = 3000f; 
        private const int DespawnDelay = 60;

        public override bool PreAI(NPC npc)
        {
            npc.TargetClosest(true);

            // Check if the boss has a valid target
            if (!npc.HasValidTarget)
            {
                HandleDespawn(npc);
                return false;
            }

            Player target = Main.player[npc.target];

            // Despawn if the player gets too far away
            if (npc.Distance(target.Center) > DespawnDistance)
            {
                HandleDespawn(npc);
                return false;
            }

            // Reset the despawn timer while the player is nearby.
            npc.localAI[DespawnTimer] = 0f;

            npc.ai[AttackTimer]++;

            AttackPhase currentAttack =
                AttackSequence[(int)npc.ai[SequenceIndex]];

            switch (currentAttack)
            {
                case AttackPhase.Placeholder:
                    DoPlaceholder(npc, target);
                    break;
            }

            return false;
        }
       
        private void HandleDespawn(NPC npc)
        {
            npc.localAI[DespawnTimer]++;

            // Give the player a short amount of time
            // to come back before the boss disappears.
            if (npc.localAI[DespawnTimer] >= DespawnDelay)
            {
                npc.active = false;
                npc.netUpdate = true;
            }
        }

        // Call this when the current attack is finished.
        // It automatically moves to the next attack
        // and loops back to the beginning.
        private void NextAttack(NPC npc)
        {
            if (AttackSequence.Length == 0)
                return;

            npc.ai[SequenceIndex]++;

            if (npc.ai[SequenceIndex] >= AttackSequence.Length)
            {
                npc.ai[SequenceIndex] = 0;
            }

            npc.ai[AttackTimer] = 0f;
            npc.ai[AttackStep] = 0f;
            npc.netUpdate = true;
        }

        private void DoPlaceholder(NPC npc, Player target)
        {
            // Placeholder attack logic
            if (npc.ai[AttackTimer] > 120) // Example duration for the placeholder attack
            {
                NextAttack(npc);
            }
        }
    }
}