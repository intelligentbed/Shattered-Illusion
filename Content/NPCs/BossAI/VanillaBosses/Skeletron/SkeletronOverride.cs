using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ShatteredIllusion.Core.OverrideSystem;
using ShatteredIllusion.Core.Rigging;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace ShatteredIllusion.Content.NPCs.BossAI.VanillaBosses.Skeletron
{
    public class SkeletronOverride : NPCBehaviorOverride
    {
        public override int[] NPCOverrideTypes => new int[]
        {
            NPCID.SkeletronHead,
            NPCID.SkeletronHand
        };

        public enum AttackPhase
        {
            Hover,     // drift above the player, hands sway like pendulums
            HandSlam,  // hands take turns winding up and slamming the player's position
            Dash       // telegraphed charge
        }

        private static readonly AttackPhase[] AttackSequence =
        {
            AttackPhase.Hover,
            AttackPhase.HandSlam,
            AttackPhase.Dash,
            AttackPhase.HandSlam,
        };

        // HEAD ai slots (all four are synced in multiplayer).
        private const int AttackTimer = 0;
        private const int SequenceIndex = 1;
        private const int AttackStep = 2;
        private const int SlamLockX = 3;   // where the active hand will land

        // HEAD local ai slots.
        private const int DespawnTimer = 0;
        private const int HandsSpawned = 1;

        // HAND ai slots (vanilla layout: side + owner head).
        private const int HandSide = 0;    // -1 left, +1 right
        private const int HandVanillaLink = 1; // vanilla's arm drawing reads this as "the head". Pointed at the hand itself so vanilla's arm has zero length
        private const int HandOwner = 2;   // head whoAmI (ours)

        private const float DespawnDistance = 3000f;
        private const int DespawnDelay = 60;

        private const int SlamCycle = 150;      // ticks per hand
        private const int SlamWindup = 60;
        private const int SlamStrike = 80;

        public override bool PreAI(NPC npc)
        {
            if (npc.type == NPCID.SkeletronHand)
                return HandAI(npc);

            return HeadAI(npc);
        }

        #region Head

        private bool HeadAI(NPC npc)
        {
            npc.TargetClosest(true);

            if (!npc.HasValidTarget)
            {
                HandleDespawn(npc);
                return false;
            }

            Player target = Main.player[npc.target];

            if (npc.Distance(target.Center) > DespawnDistance)
            {
                HandleDespawn(npc);
                return false;
            }

            npc.localAI[DespawnTimer] = 0f;

            // The override replaces vanilla AI, so vanilla's hand spawning never runs. Do it here (server / singleplayer only).
            if (npc.localAI[HandsSpawned] == 0f && Main.netMode != NetmodeID.MultiplayerClient)
            {
                npc.localAI[HandsSpawned] = 1f;
                for (int side = -1; side <= 1; side += 2)
                {
                    int i = NPC.NewNPC(npc.GetSource_FromAI(), (int)npc.Center.X, (int)npc.Center.Y, NPCID.SkeletronHand, npc.whoAmI);
                    Main.npc[i].ai[HandSide] = side;
                    Main.npc[i].ai[HandVanillaLink] = i;
                    Main.npc[i].ai[HandOwner] = npc.whoAmI;
                    Main.npc[i].netUpdate = true;
                }
            }

            npc.ai[AttackTimer]++;

            AttackPhase currentAttack = AttackSequence[(int)npc.ai[SequenceIndex]];
            switch (currentAttack)
            {
                case AttackPhase.Hover:
                    DoHover(npc, target);
                    break;
                case AttackPhase.HandSlam:
                    DoHandSlam(npc, target);
                    break;
                case AttackPhase.Dash:
                    DoDash(npc, target);
                    break;
            }

            // Tilt into the motion.
            npc.rotation = MathHelper.Lerp(npc.rotation, MathHelper.Clamp(npc.velocity.X * 0.04f, -0.5f, 0.5f), 0.15f);
            return false;
        }

        private void HandleDespawn(NPC npc)
        {
            npc.localAI[DespawnTimer]++;
            if (npc.localAI[DespawnTimer] >= DespawnDelay)
            {
                npc.active = false;
                npc.netUpdate = true;
            }
        }

        private void NextAttack(NPC npc)
        {
            if (AttackSequence.Length == 0)
                return;

            npc.ai[SequenceIndex]++;
            if (npc.ai[SequenceIndex] >= AttackSequence.Length)
                npc.ai[SequenceIndex] = 0;

            npc.ai[AttackTimer] = 0f;
            npc.ai[AttackStep] = 0f;
            npc.netUpdate = true;
        }

        /// <summary>Critically damped follow: no overshoot, no jitter. Velocity is derived from last tick's displacement, so no extra state.</summary>
        private static void SpringTo(NPC npc, Vector2 goal, float omega)
        {
            Vector2 pos = npc.Center;
            Vector2 vel = npc.velocity * 60f;
            LimbSolvers.Spring(ref pos, ref vel, goal, omega);
            npc.velocity = pos - npc.Center; // the game applies velocity after AI, landing exactly on pos
        }

        private void DoHover(NPC npc, Player target)
        {
            float t = npc.ai[AttackTimer];
            Vector2 goal = target.Center + new Vector2(MathF.Sin(t * 0.03f) * 220f, -260f + MathF.Sin(t * 0.07f) * 25f);
            SpringTo(npc, goal, 4f);

            if (t > 240)
                NextAttack(npc);
        }

        private void DoHandSlam(NPC npc, Player target)
        {
            int t = (int)npc.ai[AttackTimer];
            int local = t % SlamCycle;

            // Head drifts and keeps its distance while the hands work.
            SpringTo(npc, target.Center + new Vector2(0f, -300f), 3f);

            // Freeze the landing spot at the top of the strike so the player can dodge it.
            if (local == SlamWindup && Main.netMode != NetmodeID.MultiplayerClient)
            {
                npc.ai[SlamLockX] = target.Center.X;
                npc.netUpdate = true;
            }

            if (t >= SlamCycle * 2)
                NextAttack(npc);
        }

        private void DoDash(NPC npc, Player target)
        {
            int t = (int)npc.ai[AttackTimer];

            const int telegraph = 45;
            const int dash = 30;

            if (t < telegraph)
            {
                // Back off and line up, shaking as it charges.
                Vector2 away = (npc.Center - target.Center).SafeNormalize(Vector2.UnitY) * 420f;
                SpringTo(npc, target.Center + away, 6f);
                npc.rotation += MathF.Sin(t * 1.4f) * 0.04f;
            }
            else if (t == telegraph)
            {
                npc.velocity = (target.Center - npc.Center).SafeNormalize(Vector2.UnitY) * 20f;
                npc.netUpdate = true;
            }
            else if (t < telegraph + dash)
            {
                // Velocity is left alone: pure momentum.
            }
            else
            {
                npc.velocity *= 0.9f;
                if (t > telegraph + dash + 40)
                    NextAttack(npc);
            }
        }

        #endregion


        #region Drawing

        public override bool PreDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            if (npc.type != NPCID.SkeletronHand)
                return true;

            int ownerIndex = (int)npc.ai[HandOwner];
            if (ownerIndex < 0 || ownerIndex >= Main.maxNPCs || !Main.npc[ownerIndex].active)
                return true;

            NPC head = Main.npc[ownerIndex];
            float side = npc.ai[HandSide] >= 0f ? 1f : -1f;

            Vector2 shoulder = head.Center + new Vector2(side * 30f, 40f);
            Vector2 wrist = npc.Center;

            // Stateless two-bone arm. Only a little slack in the bones, so it's a gentle droop, not a zigzag.
            float dist = Vector2.Distance(shoulder, wrist);
            float boneLen = dist * 0.5f + 14f;
            Vector2 elbow = LimbSolvers.TwoBone(shoulder, wrist, boneLen, boneLen, side, out _);

            // Terraria's own bone-arm sprite, repeated along each bone, so it matches the hand and head pixel art.
            Texture2D bone = TextureAssets.BoneArm.Value;
            DrawBoneChain(spriteBatch, bone, shoulder, elbow, screenPos, drawColor);
            DrawBoneChain(spriteBatch, bone, elbow, wrist, screenPos, drawColor);

            // Draw the hand ourselves (this return-false also stops vanilla's sprite).
            Texture2D tex = TextureAssets.Npc[npc.type].Value;
            Rectangle frame = npc.frame;
            Vector2 origin = frame.Size() * 0.5f;
            SpriteEffects fx = npc.spriteDirection == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            spriteBatch.Draw(tex, npc.Center - screenPos, frame, npc.GetAlpha(drawColor), npc.rotation, origin, npc.scale, fx, 0f);

            return false;
        }

        /// <summary>Repeats the bone sprite end to end along a line (same idea as vanilla's arm), lit per segment.</summary>
        private static void DrawBoneChain(SpriteBatch sb, Texture2D tex, Vector2 from, Vector2 to, Vector2 screenPos, Color fallbackLight)
        {
            Vector2 d = to - from;
            float len = d.Length();
            if (len < 1f)
                return;

            Vector2 dir = d / len;
            float rot = dir.ToRotation() + MathHelper.PiOver2; // sprite is drawn vertical
            float step = tex.Height;
            Vector2 origin = tex.Size() * 0.5f;

            int count = Math.Max(1, (int)MathF.Ceiling(len / step));
            float spacing = len / count; // stretch spacing slightly so the chain lands exactly on the joint
            for (int i = 0; i < count; i++)
            {
                Vector2 pos = from + dir * (spacing * (i + 0.5f));
                Color light = Lighting.GetColor((int)(pos.X / 16f), (int)(pos.Y / 16f));
                sb.Draw(tex, pos - screenPos, null, light, rot, origin, 1f, SpriteEffects.None, 0f);
            }
        }

        #endregion

        #region Hands

        private bool HandAI(NPC npc)
        {
            int ownerIndex = (int)npc.ai[HandOwner];
            if (ownerIndex < 0 || ownerIndex >= Main.maxNPCs || !Main.npc[ownerIndex].active || Main.npc[ownerIndex].type != NPCID.SkeletronHead)
            {
                npc.active = false;
                return false;
            }

            NPC head = Main.npc[ownerIndex];
            npc.target = head.target;
            Player target = Main.player[npc.target];

            float side = npc.ai[HandSide] >= 0f ? 1f : -1f;
            float time = head.ai[AttackTimer];
            AttackPhase phase = AttackSequence[(int)head.ai[SequenceIndex]];

            // Resting pose: hanging off the head's shoulders, swaying out of phase with each other.
            Vector2 rest = head.Center + new Vector2(side * 150f, 70f + MathF.Sin(time * 0.06f + side) * 18f);
            Vector2 goal = rest;
            float omega = 8f;

            if (phase == AttackPhase.HandSlam)
            {
                int t = (int)time;
                bool myTurn = (t < SlamCycle) == (side < 0f);
                int local = t % SlamCycle;

                if (myTurn)
                {
                    if (local < SlamWindup)
                    {
                        // Wind up: rise above the player, tracking them. Anticipation reads as a telegraph.
                        float lift = RigEase.Out.Eval(local / (float)SlamWindup);
                        goal = target.Center + new Vector2(0f, -320f * lift);
                        omega = 10f;
                    }
                    else if (local < SlamStrike)
                    {
                        // Strike: snap to the locked spot with a stiff spring.
                        goal = new Vector2(head.ai[SlamLockX], target.Center.Y + 30f);
                        omega = 34f;
                    }
                    else
                    {
                        goal = rest;
                        omega = 6f;
                    }
                }
            }
            else if (phase == AttackPhase.Dash)
            {
                // Trail the head like dangling limbs.
                goal = head.Center - head.velocity * 6f + new Vector2(side * 110f, 90f);
                omega = 5f;
            }

            SpringTo(npc, goal, omega);
            npc.rotation = (npc.Center - head.Center).ToRotation() - MathHelper.PiOver2;
            return false;
        }

        #endregion
    }
}