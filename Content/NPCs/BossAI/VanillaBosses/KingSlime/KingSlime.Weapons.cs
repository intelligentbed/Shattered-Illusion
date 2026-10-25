using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ShatteredIllusion.Content.Particles;
using ParticleLibrary.Core.V3.Particles;
using ParticleLibrary.Utilities;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using SystemVector2 = System.Numerics.Vector2;

namespace ShatteredIllusion.Content.NPCs.BossAI.VanillaBosses.KingSlime
{
    public partial class KingSlimeOverride
    {
        private const int WeaponMorphStep = 0;
        private const int WeaponTrackStep = 1;
        private const int WeaponStrikeStep = 2;
        private const int WeaponRecoverStep = 3;
        private const int WeaponUnmorphStep = 4;

        private const float WeaponMorphDuration = 24f;
        private const float WeaponUnmorphDuration = 20f;
        private const float WeaponStunDuration = 80f;

        private const float SwordLength = 380f;
        private const float SwordWidth = 56f;
        private const float SpearLength = 560f;
        private const float SpearThickness = 52f;
        private const float HammerHeadWidth = 300f;
        private const float HammerHeadHeight = 130f;
        private const float BowWidth = 30f;
        private const float BowHeight = 320f;

        public static float ShockwaveSpeed => Main.masterMode ? 12f : Main.expertMode ? 10f : 8f;
        public static float ShockwaveY(NPC parent) => parent.GetGlobalNPC<KingSlimeInstanceData>().ArenaMax.Y - ArenaFloorPadding - 24f;

        private static int WeaponRepeats(int count) => count + (Main.masterMode ? 1 : 0);

        private static bool IsWeaponFormPhase(AttackPhase phase) =>
            phase == AttackPhase.WeaponSword || phase == AttackPhase.WeaponSpear
            || phase == AttackPhase.WeaponHammer || phase == AttackPhase.WeaponBow;

        private static void SetWeaponBox(NPC npc, Vector2 center, float width, float height)
        {
            npc.width = Math.Max(8, (int)width);
            npc.height = Math.Max(8, (int)height);
            npc.Center = center;
        }

        private static void WeaponCommon(NPC npc)
        {
            npc.noGravity = true;
            npc.noTileCollide = true;
            npc.velocity = Vector2.Zero;
            npc.damage = 0;
            npc.alpha = SlimeAlpha;

            if (Main.rand.NextBool(5))
            {
                Dust.NewDust(npc.position, npc.width, npc.height, DustID.t_Slime, 0f, 1.5f, 100, Color.Cyan, 1.2f);
            }
        }

        private static bool MorphIn(NPC npc, KingSlimeInstanceData data, Vector2 center, Vector2 size, float timer)
        {
            float t = MathHelper.Clamp(timer / WeaponMorphDuration, 0f, 1f);
            float e = t * t * (3f - 2f * t);
            data.WeaponMorph = e;

            SetWeaponBox(npc, Vector2.Lerp(data.WeaponFrom, center, e),
                MathHelper.Lerp(data.OriginalWidth, size.X, e), MathHelper.Lerp(data.OriginalHeight, size.Y, e));

            return t >= 1f;
        }

        private void GoUnmorph(NPC npc, KingSlimeInstanceData data)
        {
            data.WeaponAnchor = new Vector2(npc.width, npc.height);
            SetStep(npc, WeaponUnmorphStep);
            npc.ai[Slot_Timer] = 0f;
        }

        private void MorphOut(NPC npc, KingSlimeInstanceData data, float timer)
        {
            float t = MathHelper.Clamp(timer / WeaponUnmorphDuration, 0f, 1f);
            float e = t * t * (3f - 2f * t);
            data.WeaponMorph = 1f - e;

            Vector2 inside = new Vector2(
                MathHelper.Clamp(npc.Center.X, data.ArenaMin.X + 80f, data.ArenaMax.X - 80f),
                MathHelper.Clamp(npc.Center.Y, data.ArenaMin.Y + 80f, data.ArenaMax.Y - 80f));
            Vector2 center = Vector2.Lerp(npc.Center, inside, 0.25f);

            SetWeaponBox(npc, center, MathHelper.Lerp(data.WeaponAnchor.X, data.OriginalWidth, e),
                MathHelper.Lerp(data.WeaponAnchor.Y, data.OriginalHeight, e));

            if (t >= 1f)
            {
                data.WeaponMorph = 0f;
                EnterCooldown(npc);
            }
        }

        private void EnterWeaponStun(NPC npc)
        {
            KingSlimeInstanceData data = npc.GetGlobalNPC<KingSlimeInstanceData>();
            data.WeaponMorph = 0f;
            ResizeKeepingCenter(npc, data.OriginalWidth, data.OriginalHeight);

            SetPhase(npc, AttackPhase.WeaponStunned);
            SetStep(npc, 0);
            npc.ai[Slot_Timer] = 0f;
            npc.velocity = Vector2.Zero;
            npc.damage = 0;

            SoundEngine.PlaySound(SoundID.NPCHit4 with { Pitch = 0.6f }, npc.Center);

            for (int i = 0; i < 12; i++)
            {
                Dust.NewDust(npc.position, npc.width, npc.height, DustID.Electric,
                    Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-2f, 2f), 100, Color.Gold, 1.2f);
            }

            if (IsAuthority)
                npc.netUpdate = true;
        }

        private void DoWeaponStunned(NPC npc)
        {
            npc.noGravity = false;
            npc.noTileCollide = false;
            npc.alpha = SlimeAlpha;
            npc.scale = EffectiveBaseScale(npc);
            npc.damage = 0;
            npc.velocity.X *= 0.8f;

            if (Main.rand.NextBool(4))
                Dust.NewDust(npc.position, npc.width, npc.height, DustID.Electric, 0f, -1f, 100, Color.Gold, 1f);

            if (npc.ai[Slot_Timer] >= WeaponStunDuration)
                EnterCooldown(npc);
        }

        private static void DashLine(Vector2 from, Vector2 to, Color color, float thickness = 9f)
        {
            if (Main.dedServ)
                return;

            Vector2 delta = to - from;
            float length = delta.Length();

            if (length < 1f)
                return;

            float rotation = delta.ToRotation();

            for (float d = 0f; d <= length; d += 44f)
            {
                BossParticleSystem.TelegraphSegments.Create(new ParticleInfo(
                    position: (from + delta / length * d).ToNumerics(),
                    velocity: SystemVector2.Zero,
                    rotation: rotation,
                    scale: new SystemVector2(30f, thickness),
                    color: color,
                    duration: 2
                ));
            }
        }

        private void DoWeaponSword(NPC npc, Player target)
        {
            WeaponCommon(npc);

            ref float timer = ref npc.ai[Slot_Timer];
            int step = CurrentStep(npc);
            KingSlimeInstanceData data = npc.GetGlobalNPC<KingSlimeInstanceData>();

            float floor = data.ArenaFloorY;
            float hoverBottom = floor - 330f;
            Vector2 size = new Vector2(SwordWidth, SwordLength);
            float clampedX = MathHelper.Clamp(target.Center.X, data.ArenaMin.X + 60f, data.ArenaMax.X - 60f);

            if (step == WeaponMorphStep)
            {
                if (timer <= 1f)
                {
                    data.WeaponFrom = npc.Center;
                    data.NinjaComboDone = 0;
                    data.NinjaComboTotal = WeaponRepeats(3);
                    SoundEngine.PlaySound(SoundID.Item8 with { Pitch = -0.5f }, npc.Center);
                }

                if (MorphIn(npc, data, new Vector2(clampedX, hoverBottom - SwordLength / 2f), size, timer))
                {
                    SetStep(npc, WeaponTrackStep);
                    timer = 0f;
                }
            }
            else if (step == WeaponTrackStep)
            {
                float duration = 50f / ArenaTempo;
                bool locked = timer >= duration - 14f;
                float x = locked ? npc.Center.X : MathHelper.Lerp(npc.Center.X, clampedX, 0.14f);

                SetWeaponBox(npc, new Vector2(x, hoverBottom - SwordLength / 2f), size.X, size.Y);

                float t = MathHelper.Clamp(timer / duration, 0f, 1f);
                DashLine(new Vector2(x, hoverBottom), new Vector2(x, floor),
                    (locked ? Color.White : new Color(255, 215, 90)) * (0.25f + 0.6f * t), 12f);

                if (timer >= duration)
                {
                    SoundEngine.PlaySound(SoundID.Item1 with { Pitch = -0.6f, Volume = 1f }, npc.Center);
                    SetStep(npc, WeaponStrikeStep);
                    timer = 0f;
                }
            }
            else if (step == WeaponStrikeStep)
            {
                float t = MathHelper.Clamp(timer / 6f, 0f, 1f);
                float bottom = MathHelper.Lerp(hoverBottom, floor, t * t);
                SetWeaponBox(npc, new Vector2(npc.Center.X, bottom - SwordLength / 2f), size.X, size.Y);
                npc.damage = npc.defDamage;

                if (timer == 6f)
                {
                    ScreenShake(npc, 10f, 14, 14f);
                    SlimeImpact(new Vector2(npc.Center.X, floor), 0.7f);
                }

                if (timer >= 14f)
                {
                    data.NinjaComboDone++;
                    SetStep(npc, WeaponRecoverStep);
                    timer = 0f;
                }
            }
            else if (step == WeaponRecoverStep)
            {
                float bottom = timer < 18f ? floor : MathHelper.Lerp(floor, hoverBottom, (timer - 18f) / 12f);
                SetWeaponBox(npc, new Vector2(npc.Center.X, bottom - SwordLength / 2f), size.X, size.Y);

                if (timer >= 30f)
                {
                    if (data.NinjaComboDone < data.NinjaComboTotal)
                    {
                        SetStep(npc, WeaponTrackStep);
                        timer = 0f;
                    }
                    else
                    {
                        GoUnmorph(npc, data);
                    }
                }
            }
            else
            {
                MorphOut(npc, data, timer);
            }
        }

        private void DoWeaponSpear(NPC npc, Player target)
        {
            WeaponCommon(npc);

            ref float timer = ref npc.ai[Slot_Timer];
            int step = CurrentStep(npc);
            KingSlimeInstanceData data = npc.GetGlobalNPC<KingSlimeInstanceData>();

            Vector2 size = new Vector2(SpearLength, SpearThickness);
            float mid = (data.ArenaMin.X + data.ArenaMax.X) * 0.5f;
            float width = data.ArenaMax.X - data.ArenaMin.X;
            float clampedY = MathHelper.Clamp(target.Center.Y, data.ArenaMin.Y + 40f, data.ArenaMax.Y - 60f);

            if (step == WeaponMorphStep && timer <= 1f)
            {
                data.WeaponFrom = npc.Center;
                data.NinjaComboDone = 0;
                data.NinjaComboTotal = WeaponRepeats(3);
                data.WeaponSide = target.Center.X < mid ? -1 : 1;
                SoundEngine.PlaySound(SoundID.Item8 with { Pitch = -0.5f }, npc.Center);
            }

            int dir = data.WeaponSide;
            float startX = (dir > 0 ? data.ArenaMin.X : data.ArenaMax.X) - dir * (SpearLength / 2f + 30f);

            if (step == WeaponMorphStep)
            {
                if (MorphIn(npc, data, new Vector2(startX, clampedY), size, timer))
                {
                    SetStep(npc, WeaponTrackStep);
                    timer = 0f;
                }
            }
            else if (step == WeaponTrackStep)
            {
                float duration = 45f / ArenaTempo;
                bool locked = timer >= duration - 10f;
                Vector2 center = locked ? npc.Center : Vector2.Lerp(npc.Center, new Vector2(startX, clampedY), 0.2f);

                SetWeaponBox(npc, center, size.X, size.Y);

                float t = MathHelper.Clamp(timer / duration, 0f, 1f);
                DashLine(new Vector2(data.ArenaMin.X, center.Y), new Vector2(data.ArenaMax.X, center.Y),
                    (locked ? Color.White : new Color(120, 255, 220)) * (0.25f + 0.6f * t), 12f);

                if (timer >= duration)
                {
                    SoundEngine.PlaySound(SoundID.Item1 with { Pitch = 0.2f, Volume = 1f }, npc.Center);
                    SetStep(npc, WeaponStrikeStep);
                    timer = 0f;
                }
            }
            else if (step == WeaponStrikeStep)
            {
                float t = MathHelper.Clamp(timer / 20f, 0f, 1f);
                float e = 1f - (1f - t) * (1f - t);
                float distance = width + 40f;

                SetWeaponBox(npc, new Vector2(startX + dir * distance * e, npc.Center.Y), size.X, size.Y);
                npc.damage = npc.defDamage;

                if (timer >= 26f)
                {
                    SetStep(npc, WeaponRecoverStep);
                    timer = 0f;
                }
            }
            else if (step == WeaponRecoverStep)
            {
                float distance = width + 40f;
                float t = MathHelper.Clamp(timer / 16f, 0f, 1f);

                SetWeaponBox(npc, new Vector2(startX + dir * distance * (1f - t), npc.Center.Y), size.X, size.Y);

                if (timer >= 16f)
                {
                    data.NinjaComboDone++;

                    if (data.NinjaComboDone < data.NinjaComboTotal)
                    {
                        data.WeaponSide = -data.WeaponSide;
                        SetStep(npc, WeaponTrackStep);
                        timer = 0f;
                    }
                    else
                    {
                        GoUnmorph(npc, data);
                    }
                }
            }
            else
            {
                MorphOut(npc, data, timer);
            }
        }

        private void DoWeaponHammer(NPC npc, Player target)
        {
            WeaponCommon(npc);

            ref float timer = ref npc.ai[Slot_Timer];
            int step = CurrentStep(npc);
            KingSlimeInstanceData data = npc.GetGlobalNPC<KingSlimeInstanceData>();

            float floor = data.ArenaFloorY;
            float hoverBottom = floor - 380f;
            Vector2 size = new Vector2(HammerHeadWidth, HammerHeadHeight);
            float clampedX = MathHelper.Clamp(target.Center.X, data.ArenaMin.X + HammerHeadWidth / 2f, data.ArenaMax.X - HammerHeadWidth / 2f);

            if (step == WeaponMorphStep)
            {
                if (timer <= 1f)
                {
                    data.WeaponFrom = npc.Center;
                    data.NinjaComboDone = 0;
                    data.NinjaComboTotal = WeaponRepeats(2);
                    SoundEngine.PlaySound(SoundID.Item8 with { Pitch = -0.6f }, npc.Center);
                }

                if (MorphIn(npc, data, new Vector2(clampedX, hoverBottom - HammerHeadHeight / 2f), size, timer))
                {
                    SetStep(npc, WeaponTrackStep);
                    timer = 0f;
                }
            }
            else if (step == WeaponTrackStep)
            {
                float duration = 50f / ArenaTempo;
                bool locked = timer >= duration - 14f;
                float x = locked ? npc.Center.X : MathHelper.Lerp(npc.Center.X, clampedX, 0.12f);

                SetWeaponBox(npc, new Vector2(x, hoverBottom - HammerHeadHeight / 2f), size.X, size.Y);

                float t = MathHelper.Clamp(timer / duration, 0f, 1f);
                Color c = (locked ? Color.White : new Color(255, 215, 90)) * (0.25f + 0.6f * t);

                DashLine(new Vector2(x - HammerHeadWidth / 2f, floor - 6f), new Vector2(x + HammerHeadWidth / 2f, floor - 6f), c, 12f);
                DashLine(new Vector2(x, hoverBottom), new Vector2(x, floor), c * 0.6f, 8f);

                if (timer >= duration)
                {
                    SoundEngine.PlaySound(SoundID.Item1 with { Pitch = -0.8f, Volume = 1f }, npc.Center);
                    SetStep(npc, WeaponStrikeStep);
                    timer = 0f;
                }
            }
            else if (step == WeaponStrikeStep)
            {
                float t = MathHelper.Clamp(timer / 5f, 0f, 1f);
                float bottom = MathHelper.Lerp(hoverBottom, floor, t * t);
                SetWeaponBox(npc, new Vector2(npc.Center.X, bottom - HammerHeadHeight / 2f), size.X, size.Y);
                npc.damage = npc.defDamage;

                if (timer == 5f)
                {
                    ScreenShake(npc, 16f, 20, 18f);
                    SlimeImpact(new Vector2(npc.Center.X, floor), 1.0f);

                    if (IsAuthority)
                    {
                        foreach (int side in new[] { -1, 1 })
                        {
                            NPC.NewNPC(npc.GetSource_FromAI(), (int)npc.Center.X, (int)ShockwaveY(npc), ModContent.NPCType<KingSlimeClone>(),
                                ai0: npc.whoAmI, ai1: 0f, ai2: 3f, ai3: side);
                        }
                    }
                }

                if (timer >= 12f)
                {
                    data.NinjaComboDone++;
                    SetStep(npc, WeaponRecoverStep);
                    timer = 0f;
                }
            }
            else if (step == WeaponRecoverStep)
            {
                float bottom = timer < 22f ? floor : MathHelper.Lerp(floor, hoverBottom, (timer - 22f) / 12f);
                SetWeaponBox(npc, new Vector2(npc.Center.X, bottom - HammerHeadHeight / 2f), size.X, size.Y);

                if (timer >= 34f)
                {
                    if (data.NinjaComboDone < data.NinjaComboTotal)
                    {
                        SetStep(npc, WeaponTrackStep);
                        timer = 0f;
                    }
                    else
                    {
                        GoUnmorph(npc, data);
                    }
                }
            }
            else
            {
                MorphOut(npc, data, timer);
            }
        }

        private void DoWeaponBow(NPC npc, Player target)
        {
            WeaponCommon(npc);

            ref float timer = ref npc.ai[Slot_Timer];
            int step = CurrentStep(npc);
            KingSlimeInstanceData data = npc.GetGlobalNPC<KingSlimeInstanceData>();

            Vector2 size = new Vector2(BowWidth, BowHeight);
            float mid = (data.ArenaMin.X + data.ArenaMax.X) * 0.5f;
            float clampedY = MathHelper.Clamp(target.Center.Y, data.ArenaMin.Y + BowHeight / 2f + 20f, data.ArenaMax.Y - BowHeight / 2f);

            if (step == WeaponMorphStep && timer <= 1f)
            {
                data.WeaponFrom = npc.Center;
                data.NinjaComboDone = 0;
                data.NinjaComboTotal = WeaponRepeats(3);
                data.WeaponSide = target.Center.X < mid ? 1 : -1;
                SoundEngine.PlaySound(SoundID.Item8 with { Pitch = -0.5f }, npc.Center);
            }

            int side = data.WeaponSide;
            float x = (side > 0 ? data.ArenaMax.X : data.ArenaMin.X) - side * 90f;

            if (step == WeaponMorphStep)
            {
                if (MorphIn(npc, data, new Vector2(x, clampedY), size, timer))
                {
                    SetStep(npc, WeaponTrackStep);
                    timer = 0f;
                }
            }
            else if (step == WeaponTrackStep)
            {
                float duration = 40f / ArenaTempo;
                bool locked = timer >= duration - 10f;
                Vector2 center = locked ? npc.Center : Vector2.Lerp(npc.Center, new Vector2(x, clampedY), 0.15f);

                SetWeaponBox(npc, center, size.X, size.Y);

                if (!locked)
                    data.WeaponAnchor = Vector2.Normalize(target.Center - center);

                float t = MathHelper.Clamp(timer / duration, 0f, 1f);
                float aim = data.WeaponAnchor.ToRotation();
                int arrows = Main.masterMode ? 7 : 5;

                for (int i = 0; i < arrows; i++)
                {
                    float angle = aim + (i - (arrows - 1) / 2f) * 0.17f;
                    DashLine(center, center + angle.ToRotationVector2() * 700f,
                        (locked ? Color.White : new Color(255, 215, 90)) * (0.1f + 0.35f * t), 7f);
                }

                if (timer >= duration)
                {
                    FireBowVolley(npc, center, data.WeaponAnchor.ToRotation(), arrows);
                    SetStep(npc, WeaponStrikeStep);
                    timer = 0f;
                }
            }
            else if (step == WeaponStrikeStep)
            {
                if (timer >= 14f)
                {
                    data.NinjaComboDone++;
                    SetStep(npc, WeaponRecoverStep);
                    timer = 0f;
                }
            }
            else if (step == WeaponRecoverStep)
            {
                if (timer >= 18f)
                {
                    if (data.NinjaComboDone < data.NinjaComboTotal)
                    {
                        SetStep(npc, WeaponTrackStep);
                        timer = 0f;
                    }
                    else
                    {
                        GoUnmorph(npc, data);
                    }
                }
            }
            else
            {
                MorphOut(npc, data, timer);
            }
        }

        private static void FireBowVolley(NPC npc, Vector2 from, float aim, int arrows)
        {
            SoundEngine.PlaySound(SoundID.Item5 with { Pitch = -0.2f, Volume = 1.1f }, from);

            if (!IsAuthority)
                return;

            float speed = Main.masterMode ? 14f : Main.expertMode ? 12f : 10f;
            int damage = npc.GetAttackDamage_ForProjectiles(22f, 16f);

            for (int i = 0; i < arrows; i++)
            {
                float angle = aim + (i - (arrows - 1) / 2f) * 0.17f;
                Vector2 velocity = angle.ToRotationVector2() * speed;

                Projectile.NewProjectile(npc.GetSource_FromAI(), from + velocity * 3f, velocity,
                    ProjectileID.WoodenArrowHostile, damage, 0f, Main.myPlayer);
            }
        }

        private const float PenPixel = 4f;

        private readonly record struct PenPalette(Color Outline, Color Shade, Color Base, Color Light, Color Gloss);

        private static readonly PenPalette PalSlime = new(new Color(32, 44, 68), new Color(58, 95, 206), new Color(72, 119, 248), new Color(89, 164, 254), new Color(119, 196, 254));
        private static readonly PenPalette PalGold = new(new Color(74, 48, 16), new Color(176, 120, 28), new Color(230, 170, 50), new Color(255, 214, 92), new Color(255, 244, 176));
        private static readonly PenPalette PalLeaf = new(new Color(40, 64, 32), new Color(118, 144, 61), new Color(114, 181, 103), new Color(180, 204, 71), new Color(206, 242, 211));
        private static readonly Color PenRed = new Color(219, 75, 76);
        private static readonly Color PenRedDark = new Color(160, 40, 50);
        private static readonly Color PenPink = new Color(243, 102, 236);

        private static SpriteBatch penBatch;
        private static Vector2 penScreen;
        private static Vector3 penLit;
        private static float penAlpha;
        private static bool penHorizontal;
        private static bool penGhost;
        private static float penTime;
        private static float penWobble;

        private static float PenSnap(float v) => (float)Math.Round(v / PenPixel) * PenPixel;

        private static void PenAbs(float x0, float y0, float x1, float y1, Color color, float alpha = 1f)
        {
            x0 = PenSnap(x0);
            y0 = PenSnap(y0);
            x1 = PenSnap(x1);
            y1 = PenSnap(y1);

            if (x1 <= x0 || y1 <= y0)
                return;

            Color tint = new Color((int)(color.R * penLit.X), (int)(color.G * penLit.Y), (int)(color.B * penLit.Z)) * (penAlpha * alpha);

            penBatch.Draw(TextureAssets.MagicPixel.Value,
                new Rectangle((int)(x0 - penScreen.X), (int)(y0 - penScreen.Y), (int)(x1 - x0), (int)(y1 - y0)), tint);
        }

        private static void PenRect(Vector2 origin, float a0, float a1, float b0, float b1, Color color, float alpha = 1f)
        {
            float sway = penWobble * (float)Math.Sin(b0 * 0.05f + penTime * 0.12f);
            a0 += sway;
            a1 += sway;

            if (penHorizontal)
                PenAbs(origin.X + b0, origin.Y + a0, origin.X + b1, origin.Y + a1, color, alpha);
            else
                PenAbs(origin.X + a0, origin.Y + b0, origin.X + a1, origin.Y + b1, color, alpha);
        }

        private static void PenShape(Vector2 origin, float b0, float b1, Func<float, float> halfWidth, bool reverse = false, PenPalette? palette = null, bool glint = true)
        {
            float length = b1 - b0;

            if (length <= 0f)
                return;

            PenPalette pal = palette ?? PalSlime;

            float Width(float b)
            {
                float t = MathHelper.Clamp((b + PenPixel / 2f - b0) / length, 0f, 1f);
                return Math.Max(0f, halfWidth(reverse ? 1f - t : t));
            }

            if (!penGhost)
            {
                float pulse = 0.10f + 0.08f * (float)Math.Sin(penTime * 0.1f);

                for (float b = b0 - PenPixel * 2f; b < b1 + PenPixel * 2f; b += PenPixel)
                {
                    float hw = Width(b);
                    PenRect(origin, -hw - PenPixel * 3f, hw + PenPixel * 3f, b, b + PenPixel, pal.Gloss, pulse);
                }

                for (float b = b0 - PenPixel; b < b1 + PenPixel; b += PenPixel)
                {
                    float hw = Width(b);
                    PenRect(origin, -hw - PenPixel, hw + PenPixel, b, b + PenPixel, pal.Outline);
                }
            }

            int row = 0;

            for (float b = b0; b < b1; b += PenPixel, row++)
            {
                float hw = Width(b);

                if (hw <= 0f)
                    continue;

                hw = Math.Max(hw, PenPixel);
                float w = hw * 2f;

                PenRect(origin, -hw, hw, b, b + PenPixel, pal.Base);

                if (w < PenPixel * 5f || penGhost)
                    continue;

                float shade = w * 0.22f;
                PenRect(origin, -hw, -hw + shade, b, b + PenPixel, pal.Shade);

                if (row % 2 == 0)
                    PenRect(origin, -hw + shade, -hw + shade + PenPixel, b, b + PenPixel, pal.Shade);

                PenRect(origin, -hw + w * 0.38f, -hw + w * 0.52f, b, b + PenPixel, pal.Light);
                PenRect(origin, -hw + w * 0.30f, -hw + w * 0.36f, b, b + PenPixel, pal.Gloss);
                PenRect(origin, hw - w * 0.12f, hw, b, b + PenPixel, pal.Shade);

                if (row % 2 == 1)
                    PenRect(origin, hw - w * 0.12f - PenPixel, hw - w * 0.12f, b, b + PenPixel, pal.Shade);

                if (glint)
                {
                    float phase = (((b * 0.8f - penTime * 2.6f) % 220f) + 220f) % 220f;

                    if (phase < 16f)
                        PenRect(origin, -hw, hw, b, b + PenPixel, pal.Gloss, 0.55f);
                }
            }
        }

        private static void PenGem(Vector2 origin, float a, float b, float size)
        {
            float h = size / 2f;
            PenRect(origin, a - h - PenPixel, a + h + PenPixel, b - h - PenPixel, b + h + PenPixel, PalSlime.Outline);
            PenRect(origin, a - h, a + h, b - h, b + h, PenRed);
            PenRect(origin, a - h, a, b - h, b, PenPink);
            PenRect(origin, a + h - PenPixel, a + h, b + h - PenPixel, b + h, PenRedDark);
        }

        private static void PenSquare(Vector2 center, float size, Color color, float alpha = 1f) =>
            PenAbs(center.X - size / 2f, center.Y - size / 2f, center.X + size / 2f, center.Y + size / 2f, color, alpha);

        private static void PenLine(Vector2 from, Vector2 to, Color color, float size, float alpha = 1f)
        {
            Vector2 delta = to - from;
            int steps = Math.Max(1, (int)(delta.Length() / PenPixel));

            for (int i = 0; i <= steps; i++)
                PenSquare(from + delta * (i / (float)steps), size, color, alpha);
        }

        private static void PenBubble(float x, float y, float radius)
        {
            for (int k = 0; k < 8; k++)
            {
                float angle = k * MathHelper.PiOver4;
                PenSquare(new Vector2(x + (float)Math.Cos(angle) * radius, y + (float)Math.Sin(angle) * radius), PenPixel, PalSlime.Gloss, 0.75f);
            }

            PenSquare(new Vector2(x - radius * 0.4f, y - radius * 0.4f), PenPixel, Color.White, 0.9f);
        }

        private static void PenSpark(float x, float y, float radius, float alpha)
        {
            PenSquare(new Vector2(x, y), radius * 2f, Color.White, alpha);
            PenAbs(x - radius * 3f, y - PenPixel / 2f, x + radius * 3f, y + PenPixel / 2f, Color.White, alpha * 0.8f);
            PenAbs(x - PenPixel / 2f, y - radius * 3f, x + PenPixel / 2f, y + radius * 3f, Color.White, alpha * 0.8f);
        }

        private static void PenCrown(float x, float y, float width, float height)
        {
            penHorizontal = false;
            PenShape(new Vector2(x, 0f), y - height * 0.35f, y, t => width / 2f, false, PalGold, false);

            foreach (float dx in new[] { -width / 2f + 6f, 0f, width / 2f - 6f })
            {
                PenShape(new Vector2(x + dx, 0f), y - height, y - height * 0.3f, t => width * 0.12f * t + 2f, false, PalGold, false);
                PenSquare(new Vector2(x + dx, y - height - 3f), 6f, PenRed);
            }
        }

        private static void DrawWeaponForm(NPC npc, SpriteBatch spriteBatch, Texture2D texture, Rectangle frame,
            Vector2 screenPos, Color drawColor, KingSlimeInstanceData data)
        {
            float m = data.WeaponMorph;
            Vector2 c = npc.Center;

            if (m < 1f)
            {
                spriteBatch.Draw(texture, c - screenPos, frame, npc.GetAlpha(drawColor) * (1f - m), 0f, frame.Size() * 0.5f,
                    new Vector2(npc.width / (float)frame.Width, npc.height / (float)frame.Height), SpriteEffects.None, 0f);
            }

            if (m <= 0.02f)
                return;

            penBatch = spriteBatch;
            penScreen = new Vector2((int)screenPos.X, (int)screenPos.Y);
            penLit = drawColor.ToVector3();
            penTime = Main.GameUpdateCount;
            penWobble = 2f * m;
            penGhost = false;

            Vector2 delta = npc.position - npc.oldPosition;
            bool striking = CurrentStep(npc) == WeaponStrikeStep && CurrentPhase(npc) != AttackPhase.WeaponBow;

            if (striking && delta.LengthSquared() > 4f)
            {
                penGhost = true;

                for (int i = 3; i >= 1; i--)
                {
                    penAlpha = m * 0.14f * (4 - i);
                    DrawWeaponPass(npc, c - delta * (i * 1.3f), data, m);
                }

                penGhost = false;
            }

            penAlpha = m;
            DrawWeaponPass(npc, c, data, m);
        }

        private static void DrawWeaponPass(NPC npc, Vector2 c, KingSlimeInstanceData data, float m)
        {
            float w = npc.width;
            float h = npc.height;
            int step = CurrentStep(npc);
            float timer = npc.ai[Slot_Timer];
            float spark = 0.6f + 0.4f * (float)Math.Sin(penTime * 0.2f);

            switch (CurrentPhase(npc))
            {
                case AttackPhase.WeaponSword:
                {
                    penHorizontal = false;
                    Vector2 o = c;
                    float top = -h / 2f;

                    PenShape(o, top, h / 2f, t => t < 0.8f ? w / 2f * (1f - 0.08f * t) : w / 2f * 0.94f * (1f - t) / 0.2f);
                    PenRect(o, -3f, 3f, top + 24f, h / 2f - 100f, PalSlime.Shade);
                    PenRect(o, -1f, 3f, top + 24f, h / 2f - 100f, PalSlime.Gloss);
                    PenRect(o, -7f, -5f, top + 40f, h / 2f - 120f, PalSlime.Light, 0.8f);
                    PenRect(o, 5f, 7f, top + 40f, h / 2f - 120f, PalSlime.Shade);

                    float[,] bubbles = { { -12f, 0.18f, 6f }, { 10f, 0.34f, 5f }, { -8f, 0.52f, 7f }, { 12f, 0.66f, 4f } };

                    for (int k = 0; k < 4; k++)
                    {
                        float yy = top + (bubbles[k, 1] * h + penTime * 0.5f * (1f + k * 0.15f)) % (h * 0.78f);
                        PenBubble(o.X + bubbles[k, 0], o.Y + yy, bubbles[k, 2]);
                    }

                    penHorizontal = true;
                    PenShape(o, -90f, 90f, t => 11f + 13f * (float)Math.Pow(Math.Abs(2f * t - 1f), 2f), false, PalGold);

                    penHorizontal = false;

                    foreach (float wing in new[] { -82f, 82f })
                        PenShape(new Vector2(o.X + wing, o.Y), top - 34f, top - 6f, t => 6f * t, false, PalGold, false);

                    PenGem(o, 0f, top - 4f, 24f);
                    PenSpark(o.X, o.Y + top - 4f, 3f, spark);

                    PenShape(o, top - 84f, top - 30f, t => 11f, false, PalLeaf);

                    for (float g = top - 80f; g < top - 34f; g += 14f)
                        PenRect(o, -11f, 11f, g, g + PenPixel, PalLeaf.Outline, 0.8f);

                    PenCrown(o.X, o.Y + top - 84f, 52f, 34f);
                    break;
                }

                case AttackPhase.WeaponSpear:
                {
                    penHorizontal = true;
                    bool rev = data.WeaponSide < 0;
                    float headLength = 230f;
                    Vector2 o = c;

                    PenShape(o, -w / 2f, w / 2f, t => 8f, rev);

                    for (int k = 1; k < 6; k++)
                    {
                        float x = -w / 2f + k * w / 6f;
                        PenRect(o, -10f, 10f, x, x + 8f, PalGold.Base);
                        PenRect(o, -10f, 10f, x, x + PenPixel, PalGold.Light);
                    }

                    float headStart = rev ? -w / 2f : w / 2f - headLength;
                    float headEnd = rev ? -w / 2f + headLength : w / 2f;

                    PenShape(o, headStart, headEnd, u =>
                        u < 0.16f ? 12f + 20f * u / 0.16f
                        : u < 0.26f ? 32f - 10f * (u - 0.16f) / 0.10f
                        : 22f * (float)Math.Pow(1f - (u - 0.26f) / 0.74f, 0.85f) + 1f, rev);

                    PenShape(o, rev ? headEnd - 4f : headStart - 20f, rev ? headEnd + 20f : headStart + 4f, t => 16f, rev, PalGold, false);

                    for (int k = 0; k < 2; k++)
                    {
                        float along = (k == 0 ? 0.45f : 0.62f) * headLength;
                        PenBubble(o.X + (rev ? headEnd - along : headStart + along), o.Y + (k == 0 ? 6f : -6f), k == 0 ? 5f : 4f);
                    }

                    PenGem(o, 0f, rev ? headEnd - headLength * 0.3f : headStart + headLength * 0.3f, 16f);

                    float anchor = rev ? headEnd + 20f : headStart - 20f;
                    float tail = rev ? 1f : -1f;

                    for (int k = 1; k < 9; k++)
                    {
                        float bp = anchor + tail * k * 10f;
                        float up = 8f + k * 1.6f * (float)Math.Sin(penTime * 0.2f + k * 0.7f);
                        float down = -18f - k * 1.6f * (float)Math.Sin(penTime * 0.2f + k * 0.7f + 1.3f);

                        PenRect(o, up - PenPixel, up + 10f + PenPixel, bp - PenPixel, bp + 10f + PenPixel, PalSlime.Outline);
                        PenRect(o, up, up + 10f, bp, bp + 10f, k % 2 == 1 ? PenRed : PenPink);
                        PenRect(o, down - PenPixel, down + 10f + PenPixel, bp - PenPixel, bp + 10f + PenPixel, PalSlime.Outline);
                        PenRect(o, down, down + 10f, bp, bp + 10f, k % 2 == 1 ? PenPink : PenRed);
                    }

                    PenShape(o, rev ? w / 2f - 30f : -w / 2f - 10f, rev ? w / 2f + 10f : -w / 2f + 30f, t => 14f, rev, PalGold);
                    break;
                }

                case AttackPhase.WeaponHammer:
                {
                    float headTop = c.Y - h / 2f;
                    Vector2 o = c;
                    Vector2 oh = new Vector2(c.X, 0f);

                    penHorizontal = false;
                    PenShape(oh, headTop - 300f, headTop + 6f, t => 14f, false, PalLeaf);

                    int ring = 0;

                    for (float g = headTop - 290f; g < headTop - 20f; g += 12f, ring++)
                    {
                        PenRect(oh, -14f, 14f, g, g + PenPixel, PalLeaf.Outline, 0.9f);

                        if (ring % 4 == 0)
                        {
                            PenRect(oh, -18f, 18f, g, g + 8f, PalGold.Base);
                            PenRect(oh, -18f, 18f, g, g + PenPixel, PalGold.Light);
                        }
                    }

                    PenCrown(c.X, headTop - 290f, 56f, 36f);

                    foreach (float spikeX in new[] { -105f, -55f, 55f, 105f })
                        PenShape(new Vector2(c.X + spikeX, 0f), headTop - 34f, headTop + 4f, t => 13f * t, false, PalSlime, false);

                    penHorizontal = true;
                    PenShape(o, -w / 2f, w / 2f, t => h / 2f * (1f - (float)Math.Pow(Math.Abs(t * 2f - 1f), 12f) * 0.35f));
                    PenRect(o, -h / 2f + 4f, -h / 2f + 10f, -w / 2f + 60f, w / 2f - 60f, PalSlime.Gloss, 0.7f);

                    foreach (int side in new[] { -1, 1 })
                    {
                        float inner = side * (w / 2f - 56f);
                        float outer = side * w / 2f;
                        float b0 = Math.Min(inner, outer);
                        float b1 = Math.Max(inner, outer);
                        float mid = (b0 + b1) / 2f;

                        PenRect(o, -h / 2f + 6f, h / 2f - 6f, b0 - PenPixel, b1 + PenPixel, PalSlime.Outline);
                        PenRect(o, -h / 2f + 10f, h / 2f - 10f, b0, b1, PalLeaf.Base);
                        PenRect(o, 0f, h / 2f - 10f, b0, b1, PalLeaf.Shade);
                        PenRect(o, -h / 2f + 10f, -h / 2f + 16f, b0, b1, PalLeaf.Gloss);

                        foreach (float stud in new[] { -28f, 28f })
                        {
                            PenSquare(new Vector2(o.X + mid, o.Y + stud), 10f, PalGold.Light);
                            PenSquare(new Vector2(o.X + mid, o.Y + stud), 6f, PalGold.Base);
                        }
                    }

                    PenRect(o, -30f, 30f, -46f, 46f, PalGold.Outline);
                    PenRect(o, -26f, 26f, -42f, 42f, PalGold.Base);
                    PenRect(o, -26f, 26f, -42f, -38f, PalGold.Gloss);
                    PenGem(o, 0f, 0f, 30f);
                    PenSpark(o.X - 6f, o.Y - 6f, 3f, spark);

                    float[,] bubbles = { { -90f, -14f, 7f }, { 80f, 16f, 6f }, { -110f, 22f, 4f }, { 100f, -20f, 5f } };

                    for (int k = 0; k < 4; k++)
                        PenBubble(o.X + bubbles[k, 0], o.Y + bubbles[k, 1] + 3f * (float)Math.Sin(penTime * 0.1f + k), bubbles[k, 2]);

                    break;
                }

                case AttackPhase.WeaponBow:
                {
                    int face = -data.WeaponSide;
                    float half = h / 2f;
                    float belly = 44f;
                    float pull = step == WeaponTrackStep ? MathHelper.Clamp(timer / (40f / ArenaTempo), 0f, 1f) * 30f
                        : step == WeaponStrikeStep ? Math.Max(0f, 30f * (1f - timer / 6f)) : 0f;

                    float tipX = c.X - face * 8f;
                    Vector2 top = new Vector2(tipX, c.Y - half);
                    Vector2 bottom = new Vector2(tipX, c.Y + half);
                    Vector2 nock = new Vector2(tipX - face * pull, c.Y);

                    for (int pass = 0; pass < 2; pass++)
                    {
                        for (int k = 0; k <= 32; k++)
                        {
                            float t = k / 32f * 2f - 1f;
                            float size = MathHelper.Lerp(20f, 10f, Math.Abs(t));
                            Vector2 p = new Vector2(tipX + face * (belly + 8f) * (1f - t * t), c.Y + t * half);

                            if (pass == 0)
                            {
                                PenSquare(p, size + PenPixel * 2f, PalSlime.Outline);
                            }
                            else
                            {
                                PenSquare(p, size, PalSlime.Base);
                                PenSquare(p + new Vector2(face * size * 0.15f, 0f), size * 0.5f, PalSlime.Light);

                                if (k % 4 == 0)
                                    PenSquare(p + new Vector2(-face * size * 0.2f, -size * 0.2f), PenPixel, PalSlime.Gloss);
                            }
                        }
                    }

                    foreach (Vector2 tip in new[] { top, bottom })
                    {
                        PenSquare(tip, 18f, PalGold.Outline);
                        PenSquare(tip, 14f, PalGold.Base);
                        PenSquare(tip + new Vector2(-2f, -2f), 6f, PalGold.Gloss);
                    }

                    penHorizontal = false;
                    Vector2 grip = new Vector2(c.X + face * belly, c.Y);
                    PenShape(grip, -34f, 34f, t => 12f, false, PalLeaf);

                    for (float g = -30f; g < 30f; g += 10f)
                        PenRect(grip, -12f, 12f, g, g + PenPixel, PalLeaf.Outline, 0.8f);

                    foreach (float capY in new[] { -40f, 30f })
                    {
                        PenRect(grip, -14f, 14f, capY, capY + 10f, PalGold.Outline);
                        PenRect(grip, -12f, 12f, capY + 2f, capY + 8f, PalGold.Base);
                    }

                    PenGem(grip, 0f, 0f, 12f);

                    float stringAlpha = 0.5f + 0.3f * (float)Math.Sin(penTime * 0.3f);
                    PenLine(top, nock, PalSlime.Gloss, 4f, stringAlpha);
                    PenLine(nock, bottom, PalSlime.Gloss, 4f, stringAlpha);

                    if (step == WeaponTrackStep)
                    {
                        Vector2 aimDir = data.WeaponAnchor;
                        Vector2 arrowTip = nock + aimDir * 100f;

                        PenLine(nock, arrowTip, PalSlime.Light, 4f);

                        for (int k = 0; k < 3; k++)
                        {
                            Vector2 f = nock + aimDir * (8f + k * 8f);
                            PenSquare(f + new Vector2(0f, -6f), 6f, PenRed);
                            PenSquare(f + new Vector2(0f, 6f), 6f, PenPink);
                        }

                        PenSquare(arrowTip + aimDir * 6f, 14f, PalSlime.Outline);
                        PenSquare(arrowTip + aimDir * 6f, 10f, PalGold.Light);
                        PenSpark(nock.X, nock.Y, 3f, 0.7f);
                    }

                    break;
                }
            }
        }
    }
}
