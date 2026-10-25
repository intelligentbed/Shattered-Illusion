using Microsoft.Xna.Framework;
using ShatteredIllusion.Content.Particles;
using ParticleLibrary.Core.V3.Particles;
using ParticleLibrary.Utilities;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using SystemVector2 = System.Numerics.Vector2;

namespace ShatteredIllusion.Content.NPCs.BossAI.VanillaBosses.KingSlime
{
    public partial class KingSlimeOverride
    {
        private const float NinjaPhaseDuration = 1800f;

        private const int RevertSinkStep = 0;
        private const int RevertRiseStep = 1;

        private const float CrownFlyDuration = 45f;
        private const float ArenaEdgeSpeed = 16f;
        private const float ArenaFloorPadding = 24f;
        private const float ArenaDashSpacing = 44f;

        private const float SweepRowSpawnLead = 40f;
        private static readonly float[] SweepRowOffsets = { 24f, 90f, 150f };
        private static readonly int[][] SweepWaves = { new[] { 0, 2 }, new[] { 1 }, new[] { 0, 2 } };

        private static float ArenaTempo => Main.masterMode ? 1.3f : Main.expertMode ? 1.15f : 1f;
        private static int SweepRounds => Main.masterMode ? 3 : 2;
        private static float SweepSpeed => Main.masterMode ? 16f : Main.expertMode ? 13f : 10f;

        private static bool IsArena(NPC npc) => npc.GetGlobalNPC<KingSlimeInstanceData>().ArenaActive;

        private static bool IsNinjaPhase(AttackPhase phase) =>
            phase == AttackPhase.NinjaWalk || phase == AttackPhase.NinjaStab || phase == AttackPhase.NinjaSlash
            || phase == AttackPhase.NinjaVanish || phase == AttackPhase.NinjaCounter || phase == AttackPhase.NinjaStunned;

        private static void ResizeKeepingBottom(NPC npc, int newWidth, int newHeight)
        {
            Vector2 bottom = npc.Bottom;
            npc.width = newWidth;
            npc.height = newHeight;
            npc.Bottom = bottom;
        }

        private void EnterRevert(NPC npc)
        {
            SetSlashHitbox(npc, false);
            SetPhase(npc, AttackPhase.TransformToSlime);
            SetStep(npc, RevertSinkStep);
            npc.ai[Slot_Timer] = 0f;
            npc.velocity = Vector2.Zero;
            npc.dontTakeDamage = true;
            npc.damage = 0;
            npc.alpha = SlimeAlpha;

            if (IsAuthority)
                npc.netUpdate = true;
        }

        private void DoRevert(NPC npc, Player target)
        {
            npc.noGravity = false;
            npc.noTileCollide = false;
            npc.velocity.X *= 0.8f;
            npc.dontTakeDamage = true;
            npc.damage = 0;

            ref float timer = ref npc.ai[Slot_Timer];

            if (CurrentStep(npc) == RevertSinkStep)
            {
                if (timer >= TransformRiseDuration)
                {
                    KingSlimeInstanceData data = npc.GetGlobalNPC<KingSlimeInstanceData>();
                    ResizeKeepingBottom(npc, data.OriginalWidth, data.OriginalHeight);
                    npc.scale = EffectiveBaseScale(npc);

                    for (int i = 0; i < 20; i++)
                    {
                        Dust.NewDust(npc.position, npc.width, npc.height, DustID.t_Slime,
                            Main.rand.NextFloat(-3f, 3f), -Main.rand.NextFloat(1f, 4f), 100, Color.Cyan, 1.5f);
                    }

                    SoundEngine.PlaySound(SoundID.NPCDeath39 with { Pitch = -0.4f }, npc.Center);

                    SetStep(npc, RevertRiseStep);
                    timer = 0f;
                }
            }
            else
            {
                npc.scale = EffectiveBaseScale(npc);

                if (timer >= TransformRiseDuration)
                    EnterArena(npc, target);
            }
        }

        private void EnterArena(NPC npc, Player target)
        {
            KingSlimeInstanceData data = npc.GetGlobalNPC<KingSlimeInstanceData>();

            data.ArenaActive = true;
            data.ArenaCenterX = target.Center.X;
            data.ArenaFloorY = npc.Bottom.Y;
            data.ArenaMin = target.Center - new Vector2(120f);
            data.ArenaMax = target.Center + new Vector2(120f);

            npc.dontTakeDamage = false;
            npc.damage = npc.defDamage;
            npc.ai[Slot_SequenceIndex] = 0f;
            SetPhase(npc, ArenaSequence[0]);
            npc.ai[Slot_Timer] = 0f;
            SetStep(npc, 0);
            SetArenaTarget(npc, ArenaSequence[0]);

            SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.2f, Volume = 1.2f }, npc.Center);

            if (IsAuthority)
                npc.netUpdate = true;
        }

        private void ExitArena(NPC npc)
        {
            npc.GetGlobalNPC<KingSlimeInstanceData>().ArenaActive = false;
            npc.ai[Slot_SequenceIndex] = 0f;
            SetPhase(npc, AttackSequence[0]);
            npc.ai[Slot_Timer] = 0f;
            SetStep(npc, 0);

            if (IsAuthority)
                npc.netUpdate = true;
        }

        private static void SetArenaTarget(NPC npc, AttackPhase phase)
        {
            KingSlimeInstanceData data = npc.GetGlobalNPC<KingSlimeInstanceData>();
            bool vertical = SplitVertical(npc);

            (float w, float h, float shift) = phase switch
            {
                AttackPhase.BigJump => (1100f, 750f, 150f),
                AttackPhase.HugeJump => (650f, 1000f, 0f),
                AttackPhase.Teleport => (1500f, 600f, 200f),
                AttackPhase.SplitAttack => vertical ? (650f, 1100f, 0f) : (1500f, 550f, 0f),
                AttackPhase.CloneSweep => (1500f, 500f, 0f),
                AttackPhase.CrownSummon => (900f, 700f, 0f),
                AttackPhase.WeaponSword => (1000f, 800f, 0f),
                AttackPhase.WeaponSpear => (1000f, 520f, 0f),
                AttackPhase.WeaponHammer => (1100f, 700f, 0f),
                AttackPhase.WeaponBow => (1400f, 700f, 0f),
                _ => (1300f, 650f, 0f),
            };

            float sign = SequenceIndex(npc) % 2 == 0 ? -1f : 1f;
            float centerX = data.ArenaCenterX + sign * shift;
            float bottom = data.ArenaFloorY + ArenaFloorPadding;

            data.ArenaTargetMin = new Vector2(centerX - w / 2f, bottom - h);
            data.ArenaTargetMax = new Vector2(centerX + w / 2f, bottom);
        }

        public static Vector2 CrownHoverPos(KingSlimeInstanceData data) =>
            new Vector2((data.ArenaMin.X + data.ArenaMax.X) * 0.5f, data.ArenaMin.Y - 26f);

        private static float Approach(float value, float target) =>
            value < target ? Math.Min(value + ArenaEdgeSpeed, target) : Math.Max(value - ArenaEdgeSpeed, target);

        private void ArenaTick(NPC npc, KingSlimeInstanceData data, Player target)
        {
            if (IsNinjaPhase(CurrentPhase(npc)))
                data.NinjaClock++;

            float crownStep = 1f / CrownFlyDuration;
            data.CrownT = MathHelper.Clamp(data.CrownT + (data.ArenaActive ? crownStep : -crownStep), 0f, 1f);

            if (!data.ArenaActive)
                return;

            data.ArenaMin = new Vector2(Approach(data.ArenaMin.X, data.ArenaTargetMin.X), Approach(data.ArenaMin.Y, data.ArenaTargetMin.Y));
            data.ArenaMax = new Vector2(Approach(data.ArenaMax.X, data.ArenaTargetMax.X), Approach(data.ArenaMax.Y, data.ArenaTargetMax.Y));

            if (target.active && !target.dead)
            {
                Vector2 p = target.position;

                if (p.X < data.ArenaMin.X)
                {
                    p.X = data.ArenaMin.X;
                    target.velocity.X = Math.Max(0f, target.velocity.X);
                }
                else if (p.X > data.ArenaMax.X - target.width)
                {
                    p.X = data.ArenaMax.X - target.width;
                    target.velocity.X = Math.Min(0f, target.velocity.X);
                }

                if (p.Y < data.ArenaMin.Y)
                {
                    p.Y = data.ArenaMin.Y;
                    target.velocity.Y = Math.Max(0f, target.velocity.Y);
                }

                target.position = p;
            }

        }

        public static void DrawArenaBorder(SpriteBatch sb, Vector2 screenPos, KingSlimeInstanceData data)
        {
            float open = data.CrownT;

            if (open <= 0f)
                return;

            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Vector2 min = data.ArenaMin;
            Vector2 max = data.ArenaMax;
            float time = Main.GameUpdateCount;

            Color outline = new Color(74, 48, 16);
            Color shade = new Color(176, 120, 28);
            Color gold = new Color(230, 170, 50);
            Color light = new Color(255, 214, 92);
            Color gloss = new Color(255, 244, 176);

            void Fill(float x0, float y0, float x1, float y1, Color color, float alpha = 1f)
            {
                int ix0 = (int)Math.Max(0f, (float)Math.Round((x0 - screenPos.X) / 2f) * 2f);
                int iy0 = (int)Math.Max(0f, (float)Math.Round((y0 - screenPos.Y) / 2f) * 2f);
                int ix1 = (int)Math.Min(Main.screenWidth, (float)Math.Round((x1 - screenPos.X) / 2f) * 2f);
                int iy1 = (int)Math.Min(Main.screenHeight, (float)Math.Round((y1 - screenPos.Y) / 2f) * 2f);

                if (ix1 <= ix0 || iy1 <= iy0)
                    return;

                sb.Draw(pixel, new Rectangle(ix0, iy0, ix1 - ix0, iy1 - iy0), color * (alpha * open));
            }

            for (int i = 0; i < 7; i++)
            {
                float a = 0.16f * (1f - i / 7f);
                float d = 6f + i * 8f;

                Fill(min.X + d, min.Y + d, max.X - d + 8f, min.Y + d + 8f, gold, a);
                Fill(min.X + d, max.Y - d - 8f, max.X - d + 8f, max.Y - d, gold, a * 0.5f);
                Fill(min.X + d, min.Y + d + 8f, min.X + d + 8f, max.Y - d - 8f, gold, a);
                Fill(max.X - d, min.Y + d + 8f, max.X - d + 8f, max.Y - d - 8f, gold, a);
            }

            Fill(min.X - 7f, min.Y - 7f, max.X + 7f, min.Y + 7f, outline);
            Fill(min.X - 7f, max.Y - 7f, max.X + 7f, max.Y + 7f, outline, 0.6f);
            Fill(min.X - 7f, min.Y - 7f, min.X + 7f, max.Y + 7f, outline);
            Fill(max.X - 7f, min.Y - 7f, max.X + 7f, max.Y + 7f, outline);

            Fill(min.X - 4f, min.Y - 4f, max.X + 4f, min.Y + 4f, gold);
            Fill(min.X - 4f, max.Y - 4f, max.X + 4f, max.Y + 4f, shade);
            Fill(min.X - 4f, min.Y - 4f, min.X + 4f, max.Y + 4f, gold);
            Fill(max.X - 4f, min.Y - 4f, max.X + 4f, max.Y + 4f, shade);

            Fill(min.X - 4f, min.Y + 2f, max.X + 4f, min.Y + 4f, light);
            Fill(min.X + 2f, min.Y - 4f, min.X + 4f, max.Y + 4f, light);
            Fill(max.X - 4f, min.Y - 4f, max.X - 2f, max.Y + 4f, shade);

            for (float x = min.X + 96f; x < max.X - 48f; x += 96f)
            {
                Fill(x - 3f, min.Y + 4f, x + 3f, min.Y + 16f, light, 0.8f);
                Fill(x - 3f, max.Y - 16f, x + 3f, max.Y - 4f, shade, 0.8f);
            }

            for (float y = min.Y + 96f; y < max.Y - 48f; y += 96f)
            {
                Fill(min.X + 4f, y - 3f, min.X + 16f, y + 3f, light, 0.8f);
                Fill(max.X - 16f, y - 3f, max.X - 4f, y + 3f, light, 0.8f);
            }

            float width = max.X - min.X;
            float height = max.Y - min.Y;
            float perimeter = (width + height) * 2f;

            for (int k = 0; k < 3; k++)
            {
                float s = (time * 7f + k * perimeter / 3f) % perimeter;
                const float len = 110f;

                for (float o = 0f; o < len; o += 10f)
                {
                    float p = (s + o) % perimeter;
                    float fade = 1f - Math.Abs(o - len / 2f) / (len / 2f);
                    Vector2 pos;

                    if (p < width)
                        pos = new Vector2(min.X + p, min.Y);
                    else if (p < width + height)
                        pos = new Vector2(max.X, min.Y + (p - width));
                    else if (p < width * 2f + height)
                        pos = new Vector2(max.X - (p - width - height), max.Y);
                    else
                        pos = new Vector2(min.X, max.Y - (p - width * 2f - height));

                    Fill(pos.X - 3f, pos.Y - 3f, pos.X + 3f, pos.Y + 3f, gloss, fade);
                }
            }

            float midX = (min.X + max.X) * 0.5f;
            Fill(midX - 48f, min.Y - 11f, midX + 48f, min.Y + 11f, outline);
            Fill(midX - 44f, min.Y - 8f, midX + 44f, min.Y + 8f, gold);
            Fill(midX - 44f, min.Y - 8f, midX + 44f, min.Y - 5f, light);
            Fill(midX - 44f, min.Y + 5f, midX + 44f, min.Y + 8f, shade);

            foreach (Vector2 corner in new[] { min, new Vector2(max.X, min.Y), new Vector2(min.X, max.Y), max })
            {
                Fill(corner.X - 14f, corner.Y - 14f, corner.X + 14f, corner.Y + 14f, outline);
                Fill(corner.X - 10f, corner.Y - 10f, corner.X + 10f, corner.Y + 10f, gold);
                Fill(corner.X - 10f, corner.Y - 10f, corner.X + 10f, corner.Y - 6f, light);
                Fill(corner.X - 5f, corner.Y - 5f, corner.X + 5f, corner.Y + 5f, new Color(219, 75, 76));
                Fill(corner.X - 5f, corner.Y - 5f, corner.X - 1f, corner.Y - 1f, new Color(243, 102, 236));
            }
        }

        public static float SweepDuration(NPC parent)
        {
            KingSlimeInstanceData data = parent.GetGlobalNPC<KingSlimeInstanceData>();
            return Math.Max(1f, (data.ArenaMax.X - data.ArenaMin.X) / SweepSpeed);
        }

        public static Vector2 SweepPosition(NPC parent, float ai3, float t)
        {
            KingSlimeInstanceData data = parent.GetGlobalNPC<KingSlimeInstanceData>();
            int side = ai3 > 0f ? 1 : -1;
            int row = (int)Math.Abs(ai3) - 1;
            float y = data.ArenaMax.Y - ArenaFloorPadding - SweepRowOffsets[row];
            float startX = side > 0 ? data.ArenaMin.X : data.ArenaMax.X;
            float endX = side > 0 ? data.ArenaMax.X : data.ArenaMin.X;

            return new Vector2(MathHelper.Lerp(startX, endX, MathHelper.Clamp(t / SweepDuration(parent), 0f, 1f)), y);
        }

        private void DoCloneSweep(NPC npc)
        {
            npc.noGravity = false;
            npc.noTileCollide = false;
            npc.alpha = SlimeAlpha;
            npc.scale = EffectiveBaseScale(npc);
            npc.velocity.X *= 0.85f;

            ref float timer = ref npc.ai[Slot_Timer];
            KingSlimeInstanceData data = npc.GetGlobalNPC<KingSlimeInstanceData>();

            float first = 55f / ArenaTempo;
            float gap = 45f / ArenaTempo;
            float lead = SweepRowSpawnLead / ArenaTempo;
            int rounds = SweepRounds;

            if (timer <= 1f)
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.1f, Volume = 1.1f }, npc.Center);

            for (int w = 0; w < rounds; w++)
            {
                float spawnAt = first + w * gap;
                int[] rows = SweepWaves[w % SweepWaves.Length];

                if (!Main.dedServ && timer < spawnAt && timer >= spawnAt - lead)
                {
                    float t = (timer - (spawnAt - lead)) / lead;

                    foreach (int row in rows)
                    {
                        float y = data.ArenaMax.Y - ArenaFloorPadding - SweepRowOffsets[row];

                        for (float x = data.ArenaMin.X; x <= data.ArenaMax.X; x += ArenaDashSpacing)
                        {
                            BossParticleSystem.TelegraphSegments.Create(new ParticleInfo(
                                position: new SystemVector2(x, y),
                                velocity: SystemVector2.Zero,
                                rotation: 0f,
                                scale: new SystemVector2(26f, 8f),
                                color: new Color(120, 255, 220, 255) * (0.25f + 0.55f * t),
                                duration: 2
                            ));
                        }
                    }
                }

                if (IsAuthority && (int)timer == (int)spawnAt)
                {
                    for (int k = 0; k < rows.Length; k++)
                    {
                        float ai3 = ((w + k) % 2 == 0 ? 1 : -1) * (rows[k] + 1);
                        Vector2 pos = SweepPosition(npc, ai3, 0f);

                        NPC.NewNPC(npc.GetSource_FromAI(), (int)pos.X, (int)pos.Y, ModContent.NPCType<KingSlimeClone>(),
                            ai0: npc.whoAmI, ai1: 0f, ai2: 2f, ai3: ai3);
                    }

                    SoundEngine.PlaySound(SoundID.Item8 with { Pitch = -0.4f, Volume = 1.0f }, npc.Center);
                }
            }

            if (timer >= first + (rounds - 1) * gap + SweepDuration(npc) + 20f)
                EnterCooldown(npc);
        }

        private void DoCrownSummon(NPC npc)
        {
            npc.noGravity = false;
            npc.noTileCollide = false;
            npc.alpha = SlimeAlpha;
            npc.scale = EffectiveBaseScale(npc);
            npc.velocity.X *= 0.85f;

            ref float timer = ref npc.ai[Slot_Timer];
            KingSlimeInstanceData data = npc.GetGlobalNPC<KingSlimeInstanceData>();
            Vector2 crown = CrownHoverPos(data);
            float windup = 50f / ArenaTempo;

            if (CurrentStep(npc) == 0)
            {
                if (timer <= 1f)
                {
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.1f, Volume = 1.1f }, npc.Center);

                    if (!Main.dedServ)
                    {
                        BossParticleSystem.ChargeCores.Create(new ParticleInfo(
                            position: crown.ToNumerics(),
                            velocity: SystemVector2.Zero,
                            rotation: 0f,
                            scale: new SystemVector2(46f, 46f),
                            color: new Color(255, 215, 90, 255),
                            duration: (int)windup + 5
                        ));
                    }
                }

                if (!Main.dedServ)
                {
                    float t = MathHelper.Clamp(timer / windup, 0f, 1f);
                    float radius = MathHelper.Lerp(160f, 30f, t);

                    for (int i = 0; i < 10; i++)
                    {
                        float angle = timer * 0.1f + MathHelper.TwoPi * i / 10f;
                        Vector2 offset = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * radius;

                        BossParticleSystem.TelegraphSegments.Create(new ParticleInfo(
                            position: (crown + offset).ToNumerics(),
                            velocity: SystemVector2.Zero,
                            rotation: angle + MathHelper.PiOver2,
                            scale: new SystemVector2(20f, 8f),
                            color: new Color(255, 215, 90, 255) * (0.3f + 0.6f * t),
                            duration: 2
                        ));
                    }
                }

                if (timer >= windup)
                {
                    if (IsAuthority)
                        SpawnMassMinions(npc, 3, crown);

                    SoundEngine.PlaySound(SoundID.Item8 with { Pitch = -0.5f, Volume = 1.2f }, crown);
                    SetStep(npc, 1);
                    timer = 0f;
                }
            }
            else if (timer >= 30f)
            {
                EnterCooldown(npc);
            }
        }
    }
}
