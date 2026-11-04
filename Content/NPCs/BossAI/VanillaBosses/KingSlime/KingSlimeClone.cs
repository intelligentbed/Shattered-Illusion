using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ParticleLibrary.Core.V3.Particles;
using ParticleLibrary.Utilities;
using ReLogic.Content;
using ShatteredIllusion.Common.Visuals;
using ShatteredIllusion.Content.Particles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using SystemVector2 = System.Numerics.Vector2;

namespace ShatteredIllusion.Content.NPCs.BossAI.VanillaBosses.KingSlime
{
    public class KingSlimeClone : ModNPC
    {
        private static Asset<Texture2D>? cloneAsset;

        public override void SetStaticDefaults()
        {
            Main.npcFrameCount[NPC.type] = Main.npcFrameCount[NPCID.KingSlime];
            NPCID.Sets.BossBestiaryPriority.Add(Type);
        }

        public override void SetDefaults()
        {
            NPC.width = 60;
            NPC.height = 45;
            NPC.damage = 30;
            NPC.defense = 12;
            NPC.lifeMax = 1000;
            NPC.knockBackResist = 0f;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.dontTakeDamage = true;
            NPC.aiStyle = -1;
            NPC.alpha = 30;
        }

        public override void AI()
        {
            int parentIndex = (int)NPC.ai[0];

            if (parentIndex < 0 || parentIndex >= Main.maxNPCs || !Main.npc[parentIndex].active || Main.npc[parentIndex].type != NPCID.KingSlime)
            {
                NPC.active = false;

                return;
            }


            NPC parent = Main.npc[parentIndex];

            if (NPC.ai[2] == 3f)
            {
                NPC.ai[1]++;

                if (NPC.localAI[0] == 0f)
                    NPC.localAI[0] = NPC.Center.X;

                KingSlimeInstanceData arena = parent.GetGlobalNPC<KingSlimeInstanceData>();
                float waveX = NPC.localAI[0] + NPC.ai[3] * KingSlimeOverride.ShockwaveSpeed * NPC.ai[1];

                if (waveX < arena.ArenaMin.X - 40f || waveX > arena.ArenaMax.X + 40f)
                {
                    NPC.active = false;
                    return;
                }

                NPC.Center = new Vector2(waveX, KingSlimeOverride.ShockwaveY(parent));
                float waveScale = MathHelper.Clamp(NPC.ai[1] / 6f, 0f, 1f);
                NPC.scale = 0.85f * waveScale;
                NPC.alpha = (int)MathHelper.Lerp(255, 30, waveScale);
                NPC.spriteDirection = NPC.ai[3] > 0f ? 1 : -1;
                return;
            }

            if (NPC.ai[2] == 2f)
            {
                EchoDiveAI(parent);
                return;
            }

            if (NPC.ai[2] == 4f)
            {
                GeyserAI();
                return;
            }

            NPC.ai[1]++;
            float timer = NPC.ai[1];
            bool isVertical = NPC.ai[2] == 1f;
            float side = NPC.ai[3];


            const float splitAnchorYOffset = 16f;
            Vector2 anchor = parent.Center + new Vector2(0f, splitAnchorYOffset);

            // Travel distance scales with difficulty 
            float maxOffset = Main.masterMode ? 280f : (Main.expertMode ? 240f : 200f);
            Vector2 offsetDirection = isVertical ? new Vector2(0f, side) : new Vector2(side, 0f);

            // Total duration
            float maxDuration = 70f;

            const float cloneBaseScale = 0.85f;
            const float growWindow = 10f;

            float travelProgress;

            if (timer <= maxDuration)
            {
                float t = timer / maxDuration;

                if (t < 0.5f)
                {
                    float normT = t * 2f;
                    travelProgress = 1f - (1f - normT) * (1f - normT);
                }
                else
                {
                    float normT = (t - 0.5f) * 2f;
                    travelProgress = 1f - (normT * normT);
                }

                NPC.Center = anchor + (offsetDirection * maxOffset * travelProgress);
            }
            else
            {
                NPC.active = false;

                return;
            }

            float scaleT = 1f;
            if (timer < growWindow)
                scaleT = timer / growWindow;
            else if (timer > maxDuration - growWindow)
                scaleT = MathHelper.Clamp((maxDuration - timer) / growWindow, 0f, 1f);

            NPC.scale = cloneBaseScale * scaleT;
            NPC.alpha = (int)MathHelper.Lerp(255, 30, scaleT);
            NPC.spriteDirection = parent.spriteDirection;
        }

        // Gel geyser (mode 4): a marker shows on the ground for ai[3] ticks, then a column of gel erupts. ai[1] = timer.
        // localAI[2] = eruption amount (0..1) for drawing.
        private void GeyserAI()
        {
            const float grow = 6f;
            const float hold = 14f;
            const float shrink = 8f;

            NPC.ai[1]++;
            float t = NPC.ai[1];
            float delay = Math.Max(1f, NPC.ai[3]);
            float e = 0f;

            if (t >= delay)
            {
                float u = t - delay;

                if (u < grow)
                    e = u / grow;
                else if (u < grow + hold)
                    e = 1f;
                else if (u < grow + hold + shrink)
                    e = 1f - (u - grow - hold) / shrink;
                else
                {
                    NPC.active = false;
                    return;
                }
            }

            // The base stays planted on the ground while the hitbox grows upward.
            Vector2 bottom = NPC.Bottom;
            NPC.width = 54;
            NPC.height = (int)(16f + 110f * e);
            NPC.Bottom = bottom;
            NPC.velocity = Vector2.Zero;
            NPC.scale = 1f;
            NPC.localAI[2] = e;
            NPC.damage = e > 0.3f ? NPC.defDamage : 0;
            NPC.alpha = e > 0f ? 30 : 255;

            if (Main.dedServ)
                return;

            if (t < delay)
            {
                float tt = MathHelper.Clamp(t / delay, 0f, 1f);
                Color col = Color.Lerp(new Color(255, 215, 90), Color.White, tt * tt) * (0.3f + 0.6f * tt);
                Vector2 marker = NPC.Bottom - new Vector2(0f, 3f);
                float inner = MathHelper.Lerp(14f, 66f, tt);

                BossParticleSystem.GroundMarkers.Create(new ParticleInfo(
                    position: marker.ToNumerics(),
                    velocity: SystemVector2.Zero,
                    rotation: 0f,
                    scale: new SystemVector2(66f, 20f),
                    color: col,
                    duration: 2
                ));

                BossParticleSystem.GroundMarkers.Create(new ParticleInfo(
                    position: marker.ToNumerics(),
                    velocity: SystemVector2.Zero,
                    rotation: 0f,
                    scale: new SystemVector2(inner, inner * 0.3f),
                    color: col,
                    duration: 2
                ));

                return;
            }

            if ((int)t == (int)delay)
            {
                SoundEngine.PlaySound(SoundID.NPCDeath39 with { Pitch = 0.1f, Volume = 0.7f }, NPC.Bottom);
                VisualFX.Play(KingSlimeFx.GeyserBurst, NPC.Bottom);

                for (int i = 0; i < 10; i++)
                {
                    Dust.NewDustPerfect(NPC.Bottom + new Vector2(Main.rand.NextFloat(-20f, 20f), -4f), DustID.t_Slime,
                        new Vector2(Main.rand.NextFloat(-2f, 2f), -Main.rand.NextFloat(2f, 6f)), 100, Color.Cyan, 1.5f);
                }
            }

            if (e > 0.3f && Main.rand.NextBool(2))
            {
                Dust d = Dust.NewDustPerfect(NPC.Top + new Vector2(Main.rand.NextFloat(-18f, 18f), 4f), DustID.t_Slime,
                    new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), -Main.rand.NextFloat(1f, 3f)), 100, Color.Cyan, 1.3f);
                d.noGravity = true;
            }
        }

        private static float EaseOutBack(float x)
        {
            x = MathHelper.Clamp(x, 0f, 1f);
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float i = x - 1f;

            return 1f + c3 * i * i * i + c1 * i * i;
        }

        // Echo Dive: pop in at the ceiling, dive onto the locked floor spot, bounce, rise on the mirrored diagonal.
        // localAI[0] = distance travelled along the path, localAI[1] = distance last tick, localAI[2] = stretch (for drawing).
        private void EchoDiveAI(NPC parent)
        {
            if ((int)parent.ai[0] != (int)KingSlimeOverride.AttackPhase.EchoDive)
            {
                NPC.active = false;
                return;
            }

            NPC.ai[1]++;
            float t = NPC.ai[1];
            int side = NPC.ai[3] >= 0f ? 1 : -1;
            float fx = Math.Abs(NPC.ai[3]);
            const float baseScale = 0.85f;

            NPC.spriteDirection = side;

            float appear = KingSlimeOverride.EchoAppear;
            bool appearing = t <= appear;
            float pop = MathHelper.Clamp(t / appear, 0f, 1f);

            float distance = Math.Max(0f, t - appear) * KingSlimeOverride.EchoSpeed;
            Vector2 pos = KingSlimeOverride.EchoPosition(parent, side, fx, distance, out bool finished);

            KingSlimeOverride.EchoPath(parent, side, fx, out Vector2 a, out Vector2 b, out Vector2 c);
            float l1 = Vector2.Distance(a, b);

            if (t <= 1f && !Main.dedServ)
            {
                VisualFX.Play(KingSlimeFx.EchoSpawn, a);

                for (int i = 0; i < 6; i++)
                {
                    Dust d = Dust.NewDustPerfect(a + Main.rand.NextVector2Circular(14f, 14f), DustID.t_Slime,
                        Main.rand.NextVector2Circular(2.5f, 2.5f), 100, Color.Cyan, 1.4f);
                    d.noGravity = true;
                }
            }

            if (finished)
            {
                if (!Main.dedServ)
                {
                    for (int i = 0; i < 12; i++)
                    {
                        Dust.NewDustPerfect(pos, DustID.t_Slime, Main.rand.NextVector2Circular(4f, 4f), 100, Color.Cyan, 1.5f);
                    }
                }

                NPC.active = false;
                return;
            }

            // Bounce beat: the moment we cross from the dive into the rebound.
            float previous = NPC.localAI[1];

            if (previous < l1 && distance >= l1 && distance > 0f)
            {
                SoundEngine.PlaySound(SoundID.NPCDeath39 with { Pitch = 0.3f, Volume = 0.7f }, pos);
                KingSlimeOverride.EchoBounceImpact(new Vector2(pos.X, pos.Y + 19f));
                VisualFX.Play(KingSlimeFx.EchoBounce, new Vector2(pos.X, pos.Y + 19f), new Vector2(side, 0f));

                if (!Main.dedServ)
                {
                    for (int i = 0; i < 14; i++)
                    {
                        Vector2 v = new Vector2(Main.rand.NextFloat(-5f, 5f), -Main.rand.NextFloat(1f, 5f));
                        Dust.NewDustPerfect(pos + new Vector2(0f, 12f), DustID.t_Slime, v, 100, Color.Cyan, 1.6f);
                    }
                }
            }

            NPC.localAI[1] = distance;
            NPC.localAI[0] = distance;

            Vector2 heading = distance < l1 ? b - a : c - b;
            heading = heading.LengthSquared() > 0.01f ? Vector2.Normalize(heading) : new Vector2(side, 0f);

            // Tilt into the travel direction; mirrored for the flipped sprite.
            NPC.rotation = side * (float)Math.Atan2(heading.Y, Math.Abs(heading.X)) * 0.6f;

            float stretch = appearing ? 0f : 0.28f;
            NPC.localAI[2] = stretch;

            NPC.Center = pos;
            NPC.scale = baseScale * (appearing ? EaseOutBack(pop) : 1f);
            NPC.alpha = (int)MathHelper.Lerp(255, 30, pop);
            NPC.damage = appearing ? 0 : NPC.defDamage;

            // Gel trail.
            if (!appearing && !Main.dedServ)
            {
                Dust d = Dust.NewDustPerfect(pos - heading * 14f + Main.rand.NextVector2Circular(8f, 8f), DustID.t_Slime,
                    -heading * 1.5f, 100, Color.Cyan, 1.2f);
                d.noGravity = true;
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            Texture2D texture = (cloneAsset ??= ModContent.Request<Texture2D>("ShatteredIllusion/Content/NPCs/BossAI/VanillaBosses/KingSlime/KingSlimeClone")).Value;

            Rectangle sourceRectangle = new Rectangle(0, 0, texture.Width, texture.Height);
            Vector2 drawOrigin = new Vector2(texture.Width / 2f, texture.Height / 2f);
            Vector2 drawPos = NPC.Center - screenPos;
            SpriteEffects effects = NPC.spriteDirection == -1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            float opacity = (255 - NPC.alpha) / 255f;

            if (NPC.ai[2] == 4f)
            {
                float e = NPC.localAI[2];

                if (e <= 0.02f)
                    return false;

                // A tall wobbling column of gel, anchored at the ground.
                float wobble = (float)Math.Sin(Main.GameUpdateCount * 0.5f) * 0.04f;
                Vector2 columnScale = new Vector2(0.38f + 0.12f * (1f - e) + wobble, (NPC.height + 14f) / texture.Height);
                Vector2 columnOrigin = new Vector2(texture.Width / 2f, texture.Height);
                Color tint = Color.Lerp(drawColor, new Color(150, 235, 255), 0.25f);

                spriteBatch.Draw(texture, NPC.Bottom - screenPos + new Vector2(0f, 6f), sourceRectangle, tint, 0f, columnOrigin, columnScale, SpriteEffects.None, 0f);

                return false;
            }

            Vector2 scale = new Vector2(NPC.scale);
            bool echo = NPC.ai[2] == 2f;

            if (echo)
            {
                float stretch = NPC.localAI[2];
                scale = new Vector2(NPC.scale * (1f + stretch), NPC.scale * (1f - stretch * 0.55f));

                int parentIndex = (int)NPC.ai[0];

                if (stretch > 0f && parentIndex >= 0 && parentIndex < Main.maxNPCs && Main.npc[parentIndex].active)
                {
                    NPC parent = Main.npc[parentIndex];
                    int side = NPC.ai[3] >= 0f ? 1 : -1;
                    float fx = Math.Abs(NPC.ai[3]);
                    float distance = NPC.localAI[0];
                    Color ghostTint = Color.Lerp(drawColor, new Color(120, 255, 220), 0.7f);

                    for (int k = 5; k >= 1; k--)
                    {
                        float ghostDistance = distance - k * KingSlimeOverride.EchoSpeed * 0.75f;

                        if (ghostDistance < 0f)
                            continue;

                        Vector2 ghostPos = KingSlimeOverride.EchoPosition(parent, side, fx, ghostDistance, out _);
                        float fade = 1f - k / 6f;

                        spriteBatch.Draw(texture, ghostPos - screenPos, sourceRectangle, ghostTint * (opacity * fade * fade * 0.55f),
                            NPC.rotation, drawOrigin, scale * (1f - k * 0.05f), effects, 0f);
                    }
                }
            }

            spriteBatch.Draw(texture, drawPos, sourceRectangle, drawColor * opacity, NPC.rotation, drawOrigin, scale, effects, 0f);

            return false;
        }
    }
}