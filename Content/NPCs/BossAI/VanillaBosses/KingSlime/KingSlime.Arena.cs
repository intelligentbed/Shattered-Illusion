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

        // Echo Dive: clones drop from the ceiling in a 45-ish degree V (down onto a locked floor spot, bounce, back up).
        public const float EchoAppear = 10f;
        private const float EchoSpawnLead = 46f;
        private const float EchoDiveSlope = 0.85f;
        private const float EchoLockTime = 14f;

        private static float ArenaTempo => Main.masterMode ? 1.3f : Main.expertMode ? 1.15f : 1f;
        private static int EchoWaves => Main.masterMode ? 5 : Main.expertMode ? 4 : 3;
        public static float EchoSpeed => Main.masterMode ? 20f : Main.expertMode ? 17f : 14f;

        private static bool IsArena(NPC npc) => npc.GetGlobalNPC<KingSlimeInstanceData>().ArenaActive;

        private static bool IsNinjaPhase(AttackPhase phase) =>
            phase == AttackPhase.NinjaWalk || phase == AttackPhase.NinjaStab || phase == AttackPhase.NinjaSlash
            || phase == AttackPhase.NinjaVanish || phase == AttackPhase.NinjaCounter || phase == AttackPhase.NinjaStunned
            || phase == AttackPhase.NinjaLeap || phase == AttackPhase.NinjaGeyser;

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

                    KingSlimeSfx.RevertGrow(npc.Center);

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
                AttackPhase.EchoDive => (1500f, 500f, 0f),
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

        // Gilded frame, lit from the top-left: every rail shares one bevel profile (lit side -> shaded side),
        // so top/left read as raised highlights and bottom/right as shadow. Everything snaps to a 2px world grid.
        public static void DrawArenaBorder(SpriteBatch sb, Vector2 screenPos, KingSlimeInstanceData data)
        {
            float open = data.CrownT;

            if (open <= 0f)
                return;

            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Vector2 min = data.ArenaMin;
            Vector2 max = data.ArenaMax;
            float time = Main.GameUpdateCount;
            float width = max.X - min.X;
            float height = max.Y - min.Y;
            float perimeter = (width + height) * 2f;
            float midX = (min.X + max.X) * 0.5f;
            float pulse = 0.5f + 0.5f * (float)Math.Sin(time * 0.08f);
            float sx = (float)Math.Floor(screenPos.X);
            float sy = (float)Math.Floor(screenPos.Y);

            Color ink = new Color(54, 33, 12);
            Color deep = new Color(122, 74, 18);
            Color shade = new Color(176, 120, 28);
            Color gold = new Color(230, 170, 50);
            Color light = new Color(255, 214, 92);
            Color gloss = new Color(255, 244, 176);
            Color shadow = new Color(16, 8, 0);
            Color gel = new Color(110, 200, 255);
            Color gelDark = new Color(56, 120, 214);
            Color gelLight = new Color(200, 240, 255);
            Color ruby = new Color(219, 75, 76);
            Color rubyLight = new Color(255, 168, 160);
            Color rubyDark = new Color(140, 32, 58);

            float Snap(float v) => (float)Math.Round(v / 2f) * 2f;

            void Fill(float x0, float y0, float x1, float y1, Color color, float alpha = 1f)
            {
                int ix0 = (int)Math.Max(0f, Snap(x0) - sx);
                int iy0 = (int)Math.Max(0f, Snap(y0) - sy);
                int ix1 = (int)Math.Min(Main.screenWidth, Snap(x1) - sx);
                int iy1 = (int)Math.Min(Main.screenHeight, Snap(y1) - sy);

                if (ix1 <= ix0 || iy1 <= iy0)
                    return;

                sb.Draw(pixel, new Rectangle(ix0, iy0, ix1 - ix0, iy1 - iy0), color * (alpha * open));
            }

            // ---- 1. Soft drop shadow outside the frame (stronger down/right, away from the light) ----
            for (int i = 0; i < 5; i++)
            {
                float a = 0.20f * (1f - i / 5f);
                float d = 8f + i * 4f;

                Fill(min.X - 4f, max.Y + d, max.X + d + 4f, max.Y + d + 4f, shadow, a);
                Fill(max.X + d, min.Y - 4f, max.X + d + 4f, max.Y + d + 4f, shadow, a);
                Fill(min.X - 4f, min.Y - d - 4f, max.X + 4f, min.Y - d, shadow, a * 0.35f);
                Fill(min.X - d - 4f, min.Y - 4f, min.X - d, max.Y + 4f, shadow, a * 0.35f);
            }

            // ---- 2. Inner shadow: the frame darkens the arena edge, deepest under the lit (top/left) rails ----
            for (int i = 0; i < 12; i++)
            {
                float t = 1f - i / 12f;
                float a = 0.30f * t * t;
                float d = 8f + i * 4f;

                Fill(min.X + 8f, min.Y + d, max.X - 8f, min.Y + d + 4f, shadow, a);
                Fill(min.X + d, min.Y + 8f, min.X + d + 4f, max.Y - 8f, shadow, a);

                if (i < 6)
                {
                    float a2 = 0.16f * t * t;

                    Fill(min.X + 8f, max.Y - d - 4f, max.X - 8f, max.Y - d, shadow, a2);
                    Fill(max.X - d - 4f, min.Y + 8f, max.X - d, max.Y - 8f, shadow, a2);
                }
            }

            // Warm bounce light along the shaded rails, and a dashed inner pinstripe.
            Fill(min.X + 8f, max.Y - 12f, max.X - 8f, max.Y - 8f, gold, 0.12f);
            Fill(max.X - 12f, min.Y + 8f, max.X - 8f, max.Y - 8f, gold, 0.12f);

            for (float x = min.X + 24f; x < max.X - 24f; x += 36f)
            {
                Fill(x, min.Y + 18f, x + 24f, min.Y + 20f, light, 0.22f);
                Fill(x, max.Y - 20f, x + 24f, max.Y - 18f, gold, 0.16f);
            }

            for (float y = min.Y + 24f; y < max.Y - 24f; y += 36f)
            {
                Fill(min.X + 18f, y, min.X + 20f, y + 24f, light, 0.22f);
                Fill(max.X - 20f, y, max.X - 18f, y + 24f, gold, 0.16f);
            }

            // ---- 3. Rails: ink / highlight / body / body / shade / shade / deep / ink, lit side first ----
            Color[] profile = { ink, light, gold, gold, shade, shade, deep, ink };

            for (int i = 0; i < profile.Length; i++)
            {
                float o = -8f + i * 2f;

                Fill(min.X - 8f, min.Y + o, max.X + 8f, min.Y + o + 2f, profile[i]);
                Fill(min.X - 8f, max.Y + o, max.X + 8f, max.Y + o + 2f, profile[i]);
                Fill(min.X + o, min.Y - 8f, min.X + o + 2f, max.Y + 8f, profile[i]);
                Fill(max.X + o, min.Y - 8f, max.X + o + 2f, max.Y + 8f, profile[i]);
            }

            // ---- 4. Sheen: two soft glints travel around the perimeter along the lit edge ----
            float Sheen(float p)
            {
                float best = 0f;

                for (int k = 0; k < 2; k++)
                {
                    float c = (time * 6f + k * perimeter * 0.5f) % perimeter;
                    float d = Math.Abs(p - c);
                    d = Math.Min(d, perimeter - d);

                    float v = Math.Max(0f, 1f - d / 70f);
                    best = Math.Max(best, v * v);
                }

                return best;
            }

            for (float x = min.X; x < max.X; x += 12f)
            {
                float top = Sheen(x - min.X + 6f);
                float bottom = Sheen(width + height + (max.X - x - 6f));

                if (top > 0.05f)
                {
                    Fill(x, min.Y - 6f, x + 12f, min.Y - 4f, gloss, top);
                    Fill(x, min.Y - 4f, x + 12f, min.Y - 2f, light, top * 0.5f);
                }

                if (bottom > 0.05f)
                {
                    Fill(x, max.Y - 6f, x + 12f, max.Y - 4f, gloss, bottom);
                    Fill(x, max.Y - 4f, x + 12f, max.Y - 2f, light, bottom * 0.5f);
                }
            }

            for (float y = min.Y; y < max.Y; y += 12f)
            {
                float right = Sheen(width + (y - min.Y) + 6f);
                float left = Sheen(width * 2f + height + (max.Y - y - 6f));

                if (right > 0.05f)
                {
                    Fill(max.X - 6f, y, max.X - 4f, y + 12f, gloss, right);
                    Fill(max.X - 4f, y, max.X - 2f, y + 12f, light, right * 0.5f);
                }

                if (left > 0.05f)
                {
                    Fill(min.X - 6f, y, min.X - 4f, y + 12f, gloss, left);
                    Fill(min.X - 4f, y, min.X - 2f, y + 12f, light, left * 0.5f);
                }
            }

            // ---- 5. Rivets (same light direction as the rails) ----
            void Rivet(float cx, float cy)
            {
                Fill(cx - 5f, cy - 5f, cx + 5f, cy + 5f, ink);
                Fill(cx - 4f, cy - 4f, cx + 4f, cy + 4f, gold);
                Fill(cx - 4f, cy - 4f, cx + 2f, cy - 2f, light);
                Fill(cx - 4f, cy - 4f, cx - 2f, cy + 2f, light);
                Fill(cx - 2f, cy + 2f, cx + 4f, cy + 4f, shade);
                Fill(cx + 2f, cy - 2f, cx + 4f, cy + 4f, shade);
            }

            for (float x = min.X + 96f; x < max.X - 48f; x += 96f)
            {
                if (Math.Abs(x - midX) < 70f)
                    continue;

                Rivet(x, min.Y);
                Rivet(x, max.Y);
            }

            for (float y = min.Y + 96f; y < max.Y - 48f; y += 96f)
            {
                Rivet(min.X, y);
                Rivet(max.X, y);
            }

            // ---- 6. Gel drips hanging off the top rail, with the odd falling droplet ----
            int drips = (int)(width / 210f);

            for (int i = 0; i < drips; i++)
            {
                float x = Snap(min.X + 130f + i * 210f + (float)Math.Sin(i * 12.9898f) * 40f);

                if (Math.Abs(x - midX) < 70f)
                    continue;

                float len = Snap(10f + 16f * (0.5f + 0.5f * (float)Math.Sin(time * 0.035f + i * 1.7f)));
                float y0 = min.Y + 8f;

                Fill(x - 3f, y0, x + 3f, y0 + len, gel, 0.85f);
                Fill(x - 3f, y0, x - 1f, y0 + len, gelLight, 0.85f);
                Fill(x + 1f, y0, x + 3f, y0 + len, gelDark, 0.85f);

                Fill(x - 5f, y0 + len - 2f, x + 5f, y0 + len + 4f, gel, 0.85f);
                Fill(x - 5f, y0 + len - 2f, x - 3f, y0 + len + 2f, gelLight, 0.85f);
                Fill(x + 3f, y0 + len, x + 5f, y0 + len + 4f, gelDark, 0.85f);

                float fall = (time * 0.9f + i * 31f) % 140f;

                if (fall < 60f)
                    Fill(x - 2f, y0 + len + 4f + fall, x + 2f, y0 + len + 8f + fall, gelLight, 0.7f * (1f - fall / 60f));
            }

            // ---- 7. Gems and crown ----
            void Gem(float cx, float cy, float half)
            {
                Fill(cx - half - 6f, cy - half - 6f, cx + half + 6f, cy + half + 6f, ruby, 0.05f + 0.07f * pulse);
                Fill(cx - half - 3f, cy - half - 3f, cx + half + 3f, cy + half + 3f, ruby, 0.10f + 0.12f * pulse);

                Fill(cx - half - 2f, cy - half - 2f, cx + half + 2f, cy + half + 2f, ink);
                Fill(cx - half, cy - half, cx + half, cy + half, ruby);
                Fill(cx - half, cy + half - 2f, cx + half, cy + half, rubyDark);
                Fill(cx + half - 2f, cy - half, cx + half, cy + half, rubyDark);
                Fill(cx - half, cy - half, cx + half - 2f, cy - half + 2f, rubyLight, 0.8f);
                Fill(cx - half, cy - half, cx - half + 2f, cy + half - 2f, rubyLight, 0.8f);
            }

            void Spike(float cx, float baseY, int rows, float baseWidth)
            {
                for (int r = 0; r < rows; r++)
                {
                    float w = baseWidth - r * 4f;
                    float y1 = baseY - r * 6f;
                    float y0 = y1 - 6f;

                    Fill(cx - w * 0.5f - 2f, y0 - 2f, cx + w * 0.5f + 2f, y1, ink);
                }

                for (int r = 0; r < rows; r++)
                {
                    float w = baseWidth - r * 4f;
                    float y1 = baseY - r * 6f;
                    float y0 = y1 - 6f;

                    Fill(cx - w * 0.5f, y0, cx + w * 0.5f, y1, gold);
                    Fill(cx - w * 0.5f, y0, cx + w * 0.5f, y0 + 2f, light);
                    Fill(cx - w * 0.5f, y0, cx - w * 0.5f + 2f, y1, light);
                    Fill(cx + w * 0.5f - 2f, y0, cx + w * 0.5f, y1, shade);
                }

                Gem(cx, baseY - rows * 6f - 2f, 3f);
            }

            // Plaque and crown on the top rail.
            Fill(midX - 50f, min.Y - 12f, midX + 50f, min.Y + 12f, ink);
            Fill(midX - 46f, min.Y - 10f, midX + 46f, min.Y + 10f, gold);
            Fill(midX - 46f, min.Y - 10f, midX + 46f, min.Y - 8f, light);
            Fill(midX - 46f, min.Y - 10f, midX - 44f, min.Y + 10f, light);
            Fill(midX - 46f, min.Y + 6f, midX + 46f, min.Y + 10f, shade);
            Fill(midX + 44f, min.Y - 10f, midX + 46f, min.Y + 10f, shade);
            Fill(midX - 46f, min.Y + 8f, midX + 46f, min.Y + 10f, deep);

            Spike(midX - 32f, min.Y - 10f, 3, 16f);
            Spike(midX + 32f, min.Y - 10f, 3, 16f);
            Spike(midX, min.Y - 10f, 4, 22f);
            Gem(midX, min.Y, 4f);

            // Corner bosses with a ruby set in each.
            foreach (Vector2 corner in new[] { min, new Vector2(max.X, min.Y), new Vector2(min.X, max.Y), max })
            {
                Fill(corner.X - 16f, corner.Y - 16f, corner.X + 16f, corner.Y + 16f, ink);
                Fill(corner.X - 14f, corner.Y - 14f, corner.X + 14f, corner.Y + 14f, gold);
                Fill(corner.X - 14f, corner.Y - 14f, corner.X + 14f, corner.Y - 12f, light);
                Fill(corner.X - 14f, corner.Y - 14f, corner.X - 12f, corner.Y + 14f, light);
                Fill(corner.X - 14f, corner.Y + 10f, corner.X + 14f, corner.Y + 14f, shade);
                Fill(corner.X + 10f, corner.Y - 14f, corner.X + 14f, corner.Y + 14f, shade);
                Fill(corner.X - 14f, corner.Y + 12f, corner.X + 14f, corner.Y + 14f, deep);
                Fill(corner.X + 12f, corner.Y - 14f, corner.X + 14f, corner.Y + 14f, deep);

                Gem(corner.X, corner.Y, 5f);
            }
        }

        // ---------------------------------------------------------------------------------------------
        // Echo Dive
        // ---------------------------------------------------------------------------------------------

        private static float EchoFloorY(KingSlimeInstanceData data) => data.ArenaFloorY - 19f;
        private static float EchoCeilY(KingSlimeInstanceData data) => data.ArenaMin.Y + 36f;
        private static float EchoDrop(KingSlimeInstanceData data) => Math.Max(80f, EchoFloorY(data) - EchoCeilY(data));
        private static float EchoRun(KingSlimeInstanceData data) => EchoDrop(data) * EchoDiveSlope;

        // side = +1 travels left -> right (starts left of the floor point), -1 the other way round.
        public static void EchoPath(NPC parent, int side, float fx, out Vector2 start, out Vector2 bounce, out Vector2 end)
        {
            KingSlimeInstanceData data = parent.GetGlobalNPC<KingSlimeInstanceData>();
            float drop = EchoDrop(data);
            float run = drop * EchoDiveSlope;
            float floorY = EchoFloorY(data);

            start = new Vector2(fx - side * run, floorY - drop);
            bounce = new Vector2(fx, floorY);

            // The rebound is cut short when it would leave the arena through a side wall.
            float room = side > 0 ? data.ArenaMax.X - 10f - fx : fx - (data.ArenaMin.X + 10f);
            float run2 = MathHelper.Clamp(room, 8f, run);
            end = new Vector2(fx + side * run2, floorY - drop * (run2 / run));
        }

        public static Vector2 EchoPosition(NPC parent, int side, float fx, float distance, out bool finished)
        {
            EchoPath(parent, side, fx, out Vector2 a, out Vector2 b, out Vector2 c);

            float l1 = Math.Max(1f, Vector2.Distance(a, b));
            float l2 = Math.Max(1f, Vector2.Distance(b, c));
            finished = distance >= l1 + l2;

            if (distance < l1)
                return Vector2.Lerp(a, b, Math.Max(0f, distance) / l1);

            return Vector2.Lerp(b, c, MathHelper.Clamp((distance - l1) / l2, 0f, 1f));
        }

        public static void EchoBounceImpact(Vector2 pos)
        {
            if (!Main.dedServ)
                SlimeImpact(pos, 0.4f);
        }

        private static int EchoFitSide(KingSlimeInstanceData data, float x, int preferred)
        {
            float run = EchoRun(data);
            bool fits = preferred > 0 ? x - run >= data.ArenaMin.X + 16f : x + run <= data.ArenaMax.X - 16f;

            return fits ? preferred : -preferred;
        }

        private static float EchoClampFx(KingSlimeInstanceData data, int side, float x)
        {
            float run = EchoRun(data);
            float lo = data.ArenaMin.X + 16f + (side > 0 ? run : 0f);
            float hi = data.ArenaMax.X - 16f - (side < 0 ? run : 0f);

            return MathHelper.Clamp(x, lo, hi);
        }

        // Wave rhythm: single dive -> pincer (two clones, same spot, opposite sides) -> lead shot (aimed where you're going).
        private static (int side, float fx)[] EchoWaveClones(KingSlimeInstanceData data, int wave)
        {
            int pattern = wave % 3;
            int preferred = wave % 2 == 0 ? 1 : -1;
            float x = data.EchoTargetX;

            if (pattern == 1)
            {
                float run = EchoRun(data);
                float fx = MathHelper.Clamp(x, data.ArenaMin.X + run + 16f, data.ArenaMax.X - run - 16f);

                return new[] { (1, fx), (-1, fx) };
            }

            int side = EchoFitSide(data, x, preferred);

            return new[] { (side, EchoClampFx(data, side, x)) };
        }

        private static void DrawEchoTelegraph(NPC npc, KingSlimeInstanceData data, int wave, float t, bool locked, bool firstTick)
        {
            Color col = (locked ? Color.White : new Color(120, 255, 220)) * (0.2f + 0.6f * t);

            foreach ((int side, float fx) in EchoWaveClones(data, wave))
            {
                EchoPath(npc, side, fx, out Vector2 a, out Vector2 b, out Vector2 c);

                DashLine(a, b, col, 9f);
                DashLine(b, c, col * 0.55f, 7f);

                const float size = 90f;
                Vector2 marker = new Vector2(b.X, data.ArenaFloorY - 3f);
                float inner = MathHelper.Lerp(size * 0.2f, size, t);

                BossParticleSystem.GroundMarkers.Create(new ParticleInfo(
                    position: marker.ToNumerics(),
                    velocity: SystemVector2.Zero,
                    rotation: 0f,
                    scale: new SystemVector2(size, size * 0.3f),
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

                if (firstTick)
                {
                    BossParticleSystem.ChargeCores.Create(new ParticleInfo(
                        position: a.ToNumerics(),
                        velocity: SystemVector2.Zero,
                        rotation: 0f,
                        scale: new SystemVector2(30f, 30f),
                        color: new Color(120, 255, 220, 255),
                        duration: (int)(EchoSpawnLead / ArenaTempo) + 2
                    ));
                }
            }
        }

        private void SpawnEchoWave(NPC npc, KingSlimeInstanceData data, int wave)
        {
            foreach ((int side, float fx) in EchoWaveClones(data, wave))
            {
                EchoPath(npc, side, fx, out Vector2 start, out _, out _);

                // ai3 carries both the side (sign) and the floor X (magnitude); world X is always positive.
                NPC.NewNPC(npc.GetSource_FromAI(), (int)start.X, (int)start.Y + 20, ModContent.NPCType<KingSlimeClone>(),
                    ai0: npc.whoAmI, ai1: 0f, ai2: 2f, ai3: side * fx);
            }

            SoundEngine.PlaySound(SoundID.Item8 with { Pitch = -0.4f, Volume = 1.0f }, npc.Center);

            // The boss hops with every wave so he reads as the one conducting it.
            if (npc.velocity.Y == 0f)
                npc.velocity.Y = -4.5f;

            npc.netUpdate = true;
        }

        private void DoEchoDive(NPC npc, Player target)
        {
            npc.noGravity = false;
            npc.noTileCollide = false;
            npc.alpha = SlimeAlpha;
            npc.scale = EffectiveBaseScale(npc);
            npc.velocity.X *= 0.85f;

            ref float timer = ref npc.ai[Slot_Timer];
            KingSlimeInstanceData data = npc.GetGlobalNPC<KingSlimeInstanceData>();

            float first = 60f / ArenaTempo;
            float gap = 64f / ArenaTempo;
            float lead = EchoSpawnLead / ArenaTempo;
            int waves = EchoWaves;

            if (timer <= 1f)
            {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.1f, Volume = 1.1f }, npc.Center);
                data.EchoTargetX = target.Center.X;
            }

            for (int w = 0; w < waves; w++)
            {
                float spawnAt = first + w * gap;
                float telegraphAt = spawnAt - lead;
                float lockAt = spawnAt - EchoLockTime;

                if (timer >= telegraphAt && timer < spawnAt)
                {
                    if (timer < lockAt)
                    {
                        float aimX = target.Center.X;

                        // Every third wave is a lead shot: aim where the player is heading, not where they are.
                        if (w % 3 == 2)
                            aimX += MathHelper.Clamp(target.velocity.X * 22f, -220f, 220f);

                        data.EchoTargetX = MathHelper.Lerp(data.EchoTargetX, aimX, 0.2f);
                    }

                    if (!Main.dedServ)
                        DrawEchoTelegraph(npc, data, w, MathHelper.Clamp((timer - telegraphAt) / lead, 0f, 1f), timer >= lockAt, (int)timer == (int)telegraphAt);
                }

                if (IsAuthority && (int)timer == (int)spawnAt)
                    SpawnEchoWave(npc, data, w);
            }

            float pathTicks = 2f * (float)Math.Sqrt(EchoRun(data) * EchoRun(data) + EchoDrop(data) * EchoDrop(data)) / EchoSpeed;

            if (timer >= first + (waves - 1) * gap + EchoAppear + pathTicks + 24f)
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