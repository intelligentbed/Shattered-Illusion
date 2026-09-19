using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ShatteredIllusion.Common.Players.ParrySystem;
using ShatteredIllusion.Content.Particles;
using ParticleLibrary.Core.V3.Particles;
using ParticleLibrary.Utilities;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using SystemVector2 = System.Numerics.Vector2;

namespace ShatteredIllusion.Content.NPCs.BossAI.VanillaBosses.KingSlime
{
    public partial class KingSlimeOverride
    {
        private const int NinjaWalkFrameCount = 4;
        private const int NinjaWalkFrameDuration = 6;

        private const int NinjaHitboxWidth = 34;
        private const int NinjaHitboxHeight = 44;

        private const float NinjaChaseAcceleration = 0.04f;
        private const float NinjaMaxSpeed = 6f;
        private const float NinjaVelocityLerp = 0.15f;

        //This is because a certain SOMEONE was too lazy and didnt want to make a re-emerge of the ninja 
        private static readonly Rectangle[] NinjaPopupFrames =
        {
            new Rectangle(0, 0,   34, 44),
            new Rectangle(0, 46,  34, 44),
            new Rectangle(0, 92,  34, 44),
            new Rectangle(0, 144, 34, 34),
            new Rectangle(0, 202, 34, 26),
            new Rectangle(0, 258, 34, 16),
        };

        private const int NinjaPopupFrameCount = 6;
        private const float NinjaPopupFrameDuration = TransformRiseDuration / NinjaPopupFrameCount;

        private const int NinjaStabWindupStep = 0;
        private const int NinjaStabStrikeStep = 1;
        private const int NinjaStabRecoveryStep = 2;

        private const int NinjaStabFrameCount = 6;

        private const float NinjaStabTelegraphDuration = 50f;
        private const float NinjaStabDashDuration = 10f;
        private const float NinjaStabRecoveryDuration = 24f;

        private const float NinjaStabDashDistance = 420f;
        private const float NinjaStabRange = 460f;
        private const float NinjaWalkAttackCooldown = 70f;
        private const float NinjaStabSwingAnticipation = 8f;

        private const float NinjaStabTrackAcceleration = 0.09f;
        private const float NinjaStabTrackMaxSpeed = 4.5f;
        private const float NinjaStabTrackLerp = 0.12f;
        private const float NinjaStabTrackDeadzone = 10f;
        private const float NinjaStabTrackLockLerp = 0.4f;

        private const int NinjaStunnedFrameCount = 5;
        private const float NinjaStunnedMaterializeDuration = 40f;
        private const float NinjaStunnedDuration = 80f;
        private static readonly Rectangle[] NinjaStunFrames =
        {
            new Rectangle(0, 0,   38, 45),
            new Rectangle(0, 46,  38, 45),
            new Rectangle(0, 94,  38, 43),
            new Rectangle(0, 138, 38, 45),
            new Rectangle(0, 184, 38, 44),
        };

        public bool IsParryable => FindParryableNinja(null) != null;

        public void OnParried(Player player)
        {
            NPC npc = FindParryableNinja(player);
            if (npc == null)
                return;

            EnterStunned(npc);
        }

        private static NPC FindParryableNinja(Player player)
        {
            NPC closest = null;
            float closestDistSq = float.MaxValue;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];

                if (!npc.active || npc.type != NPCID.KingSlime || !IsInStabParryWindow(npc))
                    continue;

                if (player == null)
                    return npc; // just checking existence - first match is enough

                float distSq = Vector2.DistanceSquared(npc.Center, player.Center);
                if (distSq < closestDistSq)
                {
                    closestDistSq = distSq;
                    closest = npc;
                }
            }

            return closest;
        }

        private static bool IsInStabParryWindow(NPC npc) =>
            CurrentPhase(npc) == AttackPhase.NinjaStab && CurrentStep(npc) == NinjaStabStrikeStep;

        private void DoNinjaWalk(NPC npc, Player target)
        {
            npc.noGravity = false;
            npc.noTileCollide = false;

            float dirX = target.Center.X - npc.Center.X;
            npc.velocity.X = MathHelper.Lerp(
                npc.velocity.X,
                MathHelper.Clamp(dirX * NinjaChaseAcceleration, -NinjaMaxSpeed, NinjaMaxSpeed),
                NinjaVelocityLerp
            );

            if (npc.velocity.X != 0f)
            {
                npc.direction = npc.velocity.X > 0 ? 1 : -1;
                npc.spriteDirection = npc.direction;
            }

            float timer = npc.ai[Slot_Timer];
            float distanceToTarget = Vector2.Distance(npc.Center, target.Center);


            if (timer >= NinjaWalkAttackCooldown && distanceToTarget <= NinjaStabRange)
            {
                EnterNinjaStab(npc);
            }
        }

        private void EnterNinjaStab(NPC npc)
        {
            SetPhase(npc, AttackPhase.NinjaStab);
            SetStep(npc, NinjaStabWindupStep);
            npc.ai[Slot_Timer] = 0f;
            npc.velocity.X = 0f;
            npc.damage = 0;

            if (IsAuthority)
                npc.netUpdate = true;
        }

        private void DoNinjaStab(NPC npc, Player target)
        {
            ref float timer = ref npc.ai[Slot_Timer];
            int step = CurrentStep(npc);

            if (step == NinjaStabWindupStep)
            {
                // He hovers for the whole startup so he can line the dash up vertically.
                npc.noGravity = true;
                npc.noTileCollide = false;
                npc.damage = 0;
                npc.velocity.X *= 0.7f;

                bool lockedIn = timer >= NinjaStabTelegraphDuration - NinjaStabSwingAnticipation;

                if (lockedIn)
                {
                    // Anticipation window: stop tracking, freeze the height, wind the swing.
                    npc.velocity.Y = MathHelper.Lerp(npc.velocity.Y, 0f, NinjaStabTrackLockLerp);

                    if (Math.Abs(npc.velocity.Y) < 0.05f)
                        npc.velocity.Y = 0f;
                }
                else
                {
                    float yDiff = target.Center.Y - npc.Center.Y;
                    float desiredYVel = Math.Abs(yDiff) <= NinjaStabTrackDeadzone
                        ? 0f
                        : MathHelper.Clamp(yDiff * NinjaStabTrackAcceleration, -NinjaStabTrackMaxSpeed, NinjaStabTrackMaxSpeed);

                    npc.velocity.Y = MathHelper.Lerp(npc.velocity.Y, desiredYVel, NinjaStabTrackLerp);
                }

                // Lock in facing (HOLY MEGA MOG LOL, gosh i hate myself)
                npc.direction = target.Center.X > npc.Center.X ? 1 : -1;
                npc.spriteDirection = npc.direction;

                if (timer <= 1f)
                {
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.2f, Volume = 0.9f }, npc.Center);
                }

                if (Main.rand.NextBool(2))
                {
                    Dust.NewDust(npc.position, npc.width, npc.height, DustID.Smoke,
                        Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-1f, 0f), 150, Color.White, 0.9f);
                }

                // The stab telegraph 
                if (!Main.dedServ && timer % 3f == 0f)
                {
                    float windupT = MathHelper.Clamp(timer / NinjaStabTelegraphDuration, 0f, 1f);
                    int segments = (int)MathHelper.Lerp(3f, 10f, windupT);
                    Vector2 dashDir = new Vector2(npc.direction, 0f);

                    for (int i = 0; i < segments; i++)
                    {
                        float t = (i + Main.rand.NextFloat()) / segments;
                        Vector2 segmentPos = npc.Center + dashDir * (NinjaStabDashDistance * t);

                        BossParticleSystem.TelegraphSegments.Create(new ParticleInfo(
                            position: segmentPos.ToNumerics(),
                            velocity: SystemVector2.Zero,
                            rotation: 0f,
                            scale: new SystemVector2(20f, 9f),
                            color: new Color(120, 255, 220, 255),
                            duration: 14
                        ));
                    }
                }

                if (timer >= NinjaStabTelegraphDuration)
                {
                    npc.velocity.Y = 0f;

                    if (IsAuthority)
                    {
                        npc.velocity.X = (NinjaStabDashDistance / NinjaStabDashDuration) * npc.direction;
                        npc.netUpdate = true;
                    }

                    SoundEngine.PlaySound(SoundID.Item1 with { Pitch = 0.4f, Volume = 0.7f }, npc.Center);

                    SetStep(npc, NinjaStabStrikeStep);
                    timer = 0f;
                }
            }
            else if (step == NinjaStabStrikeStep)
            {
                // The dash itself, and the parryable window. noGravity noTileCollide and stuff like that
                npc.noGravity = true;
                npc.noTileCollide = true;
                npc.damage = npc.defDamage;
                npc.velocity.Y = 0f;

                if (timer <= 1f && !Main.dedServ)
                {
                    Vector2 trailVelocity = new Vector2(npc.direction * 2f, 0f);

                    BossParticleSystem.EmberBursts.Create(new ParticleInfo(
                        position: npc.Center.ToNumerics(),
                        velocity: trailVelocity.ToNumerics(),
                        rotation: 0f,
                        scale: new SystemVector2(18f, 18f),
                        color: new Color(120, 255, 220, 255),
                        duration: 14
                    ));
                }

                // Afterimage smear along the dash line every tick
                if (!Main.dedServ)
                {
                    Dust.NewDust(npc.position, npc.width, npc.height, DustID.Smoke,
                        -npc.velocity.X * 0.15f, 0f, 100, new Color(120, 255, 220), 1.1f);
                }

                if (timer >= NinjaStabDashDuration)
                {
                    npc.noGravity = false;
                    npc.noTileCollide = false;
                    npc.velocity.X *= 0.2f;

                    SetStep(npc, NinjaStabRecoveryStep);
                    timer = 0f;

                    if (IsAuthority)
                        npc.netUpdate = true;
                }
            }
            else if (step == NinjaStabRecoveryStep)
            {
                npc.noGravity = false;
                npc.noTileCollide = false;
                npc.damage = 0;
                npc.velocity.X *= 0.85f;

                if (timer >= NinjaStabRecoveryDuration)
                {
                    SetPhase(npc, AttackPhase.NinjaWalk);
                    npc.ai[Slot_Timer] = 0f;
                    SetStep(npc, 0);

                    if (IsAuthority)
                        npc.netUpdate = true;
                }
            }
        }

        private void EnterStunned(NPC npc)
        {
            SetPhase(npc, AttackPhase.NinjaStunned);
            SetStep(npc, 0);
            npc.ai[Slot_Timer] = 0f;
            npc.velocity.X = 0f;
            npc.damage = 0;

            SoundEngine.PlaySound(SoundID.NPCHit4 with { Pitch = 0.6f }, npc.Center);

            if (!Main.dedServ)
            {
                for (int i = 0; i < 10; i++)
                {
                    Dust.NewDust(npc.position, npc.width, npc.height, DustID.Electric,
                        Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-2f, 2f), 100, Color.Gold, 1.2f);
                }
            }

            if (IsAuthority)
                npc.netUpdate = true;
        }

        private void DoNinjaStunned(NPC npc)
        {
            npc.noGravity = false;
            npc.noTileCollide = false;
            npc.velocity.X *= 0.8f;
            npc.damage = 0;

            ref float timer = ref npc.ai[Slot_Timer];

            if (timer >= NinjaStunnedDuration)
            {
                npc.damage = npc.defDamage;
                SetPhase(npc, AttackPhase.NinjaWalk);
                npc.ai[Slot_Timer] = 0f;
                SetStep(npc, 0);

                if (IsAuthority)
                    npc.netUpdate = true;
            }
        }

        private void DrawNinja(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            switch (CurrentPhase(npc))
            {
                case AttackPhase.TransformToNinja:
                    DrawNinjaPopup(npc, spriteBatch, screenPos, drawColor);
                    return;

                case AttackPhase.NinjaStab:
                    DrawNinjaStab(npc, spriteBatch, screenPos, drawColor);
                    return;

                case AttackPhase.NinjaStunned:
                    DrawNinjaStunned(npc, spriteBatch, screenPos, drawColor);
                    return;
            }

            Texture2D ninjaTexture = ModContent.Request<Texture2D>(
                "ShatteredIllusion/Content/NPCs/BossAI/VanillaBosses/KingSlime/NinjaWalk"
            ).Value;

            int frameCount = Math.Max(1, NinjaWalkFrameCount);
            int frameHeight = Math.Max(1, ninjaTexture.Height / frameCount);

            float animTimer = npc.ai[Slot_Timer];
            int frameIndex = (int)(animTimer / NinjaWalkFrameDuration) % frameCount;

            var frame = new Rectangle(0, frameIndex * frameHeight, ninjaTexture.Width, frameHeight);

            SpriteEffects spriteEffects = npc.spriteDirection == 1
                ? SpriteEffects.None
                : SpriteEffects.FlipHorizontally;

            Vector2 origin = frame.Size() * new Vector2(0.5f, 1f);
            Vector2 drawPos = npc.Bottom - screenPos;

            var draw = new DrawData(
                ninjaTexture,
                drawPos,
                frame,
                npc.GetAlpha(drawColor),
                npc.rotation,
                origin,
                npc.scale,
                spriteEffects,
                0f
            );

            draw.Draw(spriteBatch);
        }

        private void DrawNinjaStab(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            Texture2D stabTexture = ModContent.Request<Texture2D>(
                "ShatteredIllusion/Content/NPCs/BossAI/VanillaBosses/KingSlime/NinjaStab"
            ).Value;

            int step = CurrentStep(npc);
            float timer = npc.ai[Slot_Timer];

            int frameIndex = step switch
            {
                NinjaStabWindupStep => timer >= NinjaStabTelegraphDuration - NinjaStabSwingAnticipation ? 1 : 0,
                NinjaStabStrikeStep => 2,
                _ => 3 + (int)MathHelper.Clamp(timer / (NinjaStabRecoveryDuration / 3f), 0f, 2f),
            };

            frameIndex = Math.Clamp(frameIndex, 0, NinjaStabFrameCount - 1);

            var frame = stabTexture.Frame(1, NinjaStabFrameCount, 0, frameIndex);

            SpriteEffects spriteEffects = npc.spriteDirection == 1
                ? SpriteEffects.None
                : SpriteEffects.FlipHorizontally;

            Vector2 origin = frame.Size() * new Vector2(0.5f, 1f);
            Vector2 drawPos = npc.Bottom - screenPos;

            var draw = new DrawData(
                stabTexture,
                drawPos,
                frame,
                npc.GetAlpha(drawColor),
                npc.rotation,
                origin,
                npc.scale,
                spriteEffects,
                0f
            );

            draw.Draw(spriteBatch);
        }

        private void DrawNinjaStunned(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            Texture2D parryTexture = ModContent.Request<Texture2D>(
                "ShatteredIllusion/Content/NPCs/BossAI/VanillaBosses/KingSlime/NinjaStun"
            ).Value;

            float timer = npc.ai[Slot_Timer];

            int frameIndex = (int)MathHelper.Clamp(
                timer / (NinjaStunnedMaterializeDuration / NinjaStunnedFrameCount),
                0f,
                NinjaStunnedFrameCount - 1
            );

            Rectangle frame = NinjaStunFrames[frameIndex];

            SpriteEffects spriteEffects = npc.spriteDirection == 1
                ? SpriteEffects.None
                : SpriteEffects.FlipHorizontally;

            Vector2 origin = frame.Size() * new Vector2(0.5f, 1f);
            Vector2 drawPos = npc.Bottom - screenPos;

            var draw = new DrawData(
                parryTexture,
                drawPos,
                frame,
                npc.GetAlpha(drawColor),
                npc.rotation,
                origin,
                npc.scale,
                spriteEffects,
                0f
            );

            draw.Draw(spriteBatch);
        }

        private void DrawNinjaPopup(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            Texture2D popupTexture = ModContent.Request<Texture2D>(
                "ShatteredIllusion/Content/NPCs/BossAI/VanillaBosses/KingSlime/NinjaPopup"
            ).Value;

            float timer = npc.ai[Slot_Timer];
            int stageIndex = (int)MathHelper.Clamp(timer / NinjaPopupFrameDuration, 0f, NinjaPopupFrameCount - 1);

            // Read the sheet back to front - see the comment on NinjaPopupFrames above.
            Rectangle frame = NinjaPopupFrames[NinjaPopupFrameCount - 1 - stageIndex];

            SpriteEffects spriteEffects = npc.spriteDirection == 1
                ? SpriteEffects.None
                : SpriteEffects.FlipHorizontally;

            Vector2 origin = frame.Size() * new Vector2(0.5f, 1f);
            Vector2 drawPos = npc.Bottom - screenPos;

            var draw = new DrawData(
                popupTexture,
                drawPos,
                frame,
                npc.GetAlpha(drawColor),
                npc.rotation,
                origin,
                npc.scale,
                spriteEffects,
                0f
            );

            draw.Draw(spriteBatch);
        }
    }
}