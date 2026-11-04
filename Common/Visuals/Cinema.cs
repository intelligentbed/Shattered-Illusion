using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ParticleLibrary.Core.V3.Particles;
using ParticleLibrary.Utilities;
using ShatteredIllusion.Content.Particles;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;
using SystemVector2 = System.Numerics.Vector2;

namespace ShatteredIllusion.Common.Visuals
{
    public sealed class ImpactStyle
    {
        public Color Flash = new Color(150, 225, 255);
        public Color InnerRing = new Color(200, 245, 255);
        public Color OuterRing = new Color(0, 150, 255);
        public Color Spark = new Color(150, 225, 255);

        // -1 disables the dust layer.
        public int DebrisDust = -1;
        public Color DebrisColor = Color.White;
        public int SkirtDust = DustID.Smoke;
        public Color SkirtColor = new Color(170, 220, 255);

        // null = silent.
        public SoundStyle? Sound = SoundID.Item14 with { Pitch = -0.55f, Volume = 0.55f };

        public static readonly ImpactStyle Default = new ImpactStyle();
    }

    public sealed class TitleStyle
    {
        public Color Accent = new Color(170, 235, 255);
        public Color AccentDeep = new Color(40, 150, 220);

        public static readonly TitleStyle Default = new TitleStyle();
    }

    public static partial class Cinema
    {
        // Letterbox bars aim for a cinematic frame ratio instead of a fixed fraction of the screen height.
        // The max matches the old fixed 12%, so 16:9 and narrower screens look exactly as before;
        // ultrawide screens (already close to cinematic) get thinner bars.
        private const float CinematicAspect = 2.39f;
        private const float LetterboxMinFraction = 0.04f;
        private const float LetterboxMaxFraction = 0.12f;
        private const float MaxZoom = 0.35f;

        private static float letterbox, letterboxTarget;
        private static int letterboxAutoRelease;

        private static float holdZoom, holdZoomTarget, punchZoom;

        private static Vector2 focusPos;
        private static float focusWeight, focusTarget;

        private static float vignette, vignetteTarget;

        private static Color flashColor = Color.White;
        private static float flashAlpha;

        private static readonly HashSet<int> watchedNpcs = new HashSet<int>();

        // Applied after the cutscene camera so a scene can tilt/pan on top of it.
        private sealed class OffsetModifier : ICameraModifier
        {
            public Vector2 Offset;
            public bool Done;

            public string UniqueIdentity => "ShatteredIllusion/CinemaCameraOffset";
            public bool Finished => Done;

            public void Update(ref CameraInfo cameraPosition) => cameraPosition.CameraPosition += Offset;
        }

        private static OffsetModifier offsetMod;
        private static bool offsetReleasing;

        private static ShatteredIllusionConfig Cfg => ModContent.GetInstance<ShatteredIllusionConfig>();

        public static bool Enabled => !Main.dedServ && Cfg.Cinematics;
        public static bool ParticlesEnabled => Enabled && Cfg.ImpactParticles;
        public static bool HitStopEnabled => Enabled && Cfg.HitStop;
        public static bool IntroEnabled => Enabled && Cfg.IntroEntrance;
        public static bool DeathCameraEnabled => Enabled && Cfg.DeathSequence;

        // Whether the letterbox / vignette dressing is allowed by the player's config. Other UI that wants
        // to match the cinematic look (e.g. the journal) should respect these.
        public static bool LetterboxAllowed => Enabled && Cfg.Letterbox;
        public static bool VignetteAllowed => Enabled && Cfg.Vignette;

        // Height in UI pixels of one letterbox bar at full strength for a screen of the given UI-space size.
        // Pass different min/max fractions for UI that wants thinner bars than a cutscene.
        public static float LetterboxBarHeight(float uiWidth, float uiHeight, float minFraction = LetterboxMinFraction, float maxFraction = LetterboxMaxFraction)
        {
            if (uiWidth <= 0f || uiHeight <= 0f)
                return 0f;

            float needed = (uiHeight - uiWidth / CinematicAspect) * 0.5f;
            return MathHelper.Clamp(needed, uiHeight * minFraction, uiHeight * maxFraction);
        }

        // Height in UI pixels of the letterbox bars being drawn right now (0 when none are showing).
        public static float CurrentLetterboxHeight()
        {
            if (Main.dedServ || letterbox <= 0.005f)
                return 0f;

            return LetterboxBarHeight(Main.screenWidth / Main.UIScale, Main.screenHeight / Main.UIScale) * letterbox;
        }

        public static float TotalZoom => 1f + MathHelper.Clamp(holdZoom + punchZoom, 0f, MaxZoom);

        private static bool Active =>
            letterbox > 0.001f || letterboxTarget > 0f || holdZoom > 0.001f || holdZoomTarget > 0f ||
            punchZoom > 0.001f || focusWeight > 0.001f || vignette > 0.001f || flashAlpha > 0.001f || titleTimer > 0 ||
            offsetMod != null;

        // If none of the watched NPC types are alive, everything eases out.
        public static void Watch(int npcType) => watchedNpcs.Add(npcType);

        public static void Letterbox(bool on, int autoReleaseFrames = 0)
        {
            if (on && !(Enabled && Cfg.Letterbox))
                return;

            letterboxTarget = on ? 1f : 0f;
            letterboxAutoRelease = on ? autoReleaseFrames : 0;
        }

        public static void HoldZoom(float amount)
        {
            if (amount > 0f && !(Enabled && Cfg.CameraZoom))
                return;

            holdZoomTarget = amount * Cfg.ZoomIntensity;
        }

        public static void ZoomPunch(float amount)
        {
            if (!(Enabled && Cfg.CameraZoom))
                return;

            punchZoom = Math.Min(punchZoom + amount * Cfg.ZoomIntensity, 0.14f);
        }

        public static void Vignette(float amount)
        {
            if (amount > 0f && !(Enabled && Cfg.Vignette))
                return;

            vignetteTarget = amount;
        }

        public static void Shake(Vector2 pos, Vector2 direction, float strength, float vibration, int frames, float falloff = 1400f)
        {
            if (!Enabled)
                return;

            strength *= Cfg.ShakeIntensity;

            if (strength <= 0.01f)
                return;

            Main.instance.CameraModifiers.Add(new PunchCameraModifier(pos, direction, strength, vibration, frames, falloff));
        }

        public static void Focus(Vector2 worldPos, float weight)
        {
            if (weight > 0f && !(Enabled && Cfg.CameraZoom))
                return;

            focusPos = worldPos;
            focusTarget = weight;
        }

        public static void ReleaseFocus() => focusTarget = 0f;

        public static void CameraOffset(Vector2 offset)
        {
            if (!Enabled)
                return;

            if (offsetMod == null || offsetMod.Done)
            {
                offsetMod = new OffsetModifier();
                Main.instance.CameraModifiers.Add(offsetMod);
            }

            offsetReleasing = false;
            offsetMod.Offset = offset;
        }

        public static void ReleaseCameraOffset() => offsetReleasing = true;

        public static void Flash(Color color, float alpha)
        {
            if (!Enabled)
                return;

            alpha *= Cfg.FlashIntensity;

            if (Cfg.ScreenReduceFlashing)
                alpha *= ScreenFX.ReduceFlashScale;

            if (alpha <= 0.01f)
                return;

            flashColor = color;
            flashAlpha = Math.Max(flashAlpha, alpha);
        }

        public static void ReleaseAll()
        {
            letterboxTarget = 0f;
            letterboxAutoRelease = 0;
            holdZoomTarget = 0f;
            focusTarget = 0f;
            vignetteTarget = 0f;
            ReleaseCameraOffset();
            ScreenFX.ReleaseGrade();
        }

        public static void HardReset()
        {
            if (offsetMod != null)
            {
                offsetMod.Done = true;
                offsetMod = null;
            }

            offsetReleasing = false;
            letterbox = letterboxTarget = 0f;
            letterboxAutoRelease = 0;
            holdZoom = holdZoomTarget = punchZoom = 0f;
            focusWeight = focusTarget = 0f;
            vignette = vignetteTarget = 0f;
            flashAlpha = 0f;
            titleTimer = 0;
        }

        internal static void Update()
        {
            if (Main.dedServ)
                return;

            if (!Enabled)
            {
                if (Active)
                    HardReset();

                return;
            }

            if (!Active)
                return;

            if (watchedNpcs.Count > 0 && !AnyWatchedAlive())
                ReleaseAll();

            if (letterboxAutoRelease > 0 && --letterboxAutoRelease == 0)
                letterboxTarget = 0f;

            if (offsetMod != null && offsetReleasing)
            {
                offsetMod.Offset *= 0.88f;

                if (offsetMod.Offset.LengthSquared() < 1f)
                {
                    offsetMod.Done = true;
                    offsetMod = null;
                    offsetReleasing = false;
                }
            }

            letterbox = MathHelper.Lerp(letterbox, letterboxTarget, 0.09f);
            holdZoom = MathHelper.Lerp(holdZoom, holdZoomTarget, 0.05f);
            punchZoom *= 0.87f;
            focusWeight = MathHelper.Lerp(focusWeight, focusTarget, 0.06f);
            vignette = MathHelper.Lerp(vignette, vignetteTarget, 0.06f);
            flashAlpha = Math.Max(0f, flashAlpha - 0.045f);

            if (titleTimer > 0)
                titleTimer--;
        }

        private static bool AnyWatchedAlive()
        {
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC n = Main.npc[i];

                if (n.active && watchedNpcs.Contains(n.type))
                    return true;
            }

            return false;
        }

        internal static void ApplyScreenPosition()
        {
            if (focusWeight <= 0.001f)
                return;

            Vector2 desired = focusPos - new Vector2(Main.screenWidth, Main.screenHeight) * 0.5f;
            Main.screenPosition = Vector2.Lerp(Main.screenPosition, desired, focusWeight);
        }

        private static Texture2D vignetteTex;

        // One smooth gradient texture stretched over the screen: no layered strips, so no banding lines.
        private static Texture2D GetVignetteTexture()
        {
            if (vignetteTex != null && !vignetteTex.IsDisposed)
                return vignetteTex;

            const int size = 512;
            var data = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = Math.Abs((x + 0.5f) / size * 2f - 1f);
                    float v = Math.Abs((y + 0.5f) / size * 2f - 1f);

                    float d = (float)Math.Pow(Math.Pow(u, 4) + Math.Pow(v, 4), 0.25);

                    float t = MathHelper.Clamp((d - 0.5f) / 0.7f, 0f, 1f);
                    t = t * t * (3f - 2f * t);

                    byte a = (byte)(t * 0.75f * 255f + 0.5f);
                    data[y * size + x] = new Color((byte)0, (byte)0, (byte)0, a);
                }
            }

            vignetteTex = new Texture2D(Main.graphics.GraphicsDevice, size, size);
            vignetteTex.SetData(data);
            return vignetteTex;
        }

        internal static void DisposeVignette()
        {
            Main.QueueMainThreadAction(() =>
            {
                vignetteTex?.Dispose();
                vignetteTex = null;
            });
        }

        internal static void DrawOverlay(SpriteBatch sb)
        {
            if (Main.dedServ || !Active)
                return;

            Texture2D px = TextureAssets.MagicPixel.Value;

            float w = Main.screenWidth / Main.UIScale;
            float h = Main.screenHeight / Main.UIScale;

            if (vignette > 0.01f)
            {
                sb.Draw(GetVignetteTexture(),
                    new Rectangle(0, 0, (int)Math.Ceiling(w) + 2, (int)Math.Ceiling(h) + 2),
                    Color.White * vignette);
            }

            if (letterbox > 0.005f)
            {
                int barH = (int)(LetterboxBarHeight(w, h) * letterbox);
                sb.Draw(px, new Rectangle(0, 0, (int)Math.Ceiling(w) + 2, barH), Color.Black);
                sb.Draw(px, new Rectangle(0, (int)Math.Ceiling(h) - barH, (int)Math.Ceiling(w) + 2, barH + 2), Color.Black);
            }

            if (titleTimer > 0 && titleDuration > 0)
                DrawTitleCard(sb, px, w, h);

            if (flashAlpha > 0.01f)
                sb.Draw(px, new Rectangle(0, 0, (int)Math.Ceiling(w) + 2, (int)Math.Ceiling(h) + 2), flashColor * flashAlpha);
        }

        // Camera half of a heavy impact: zoom punch, flash, shake and screen ripple.
        // Shared by Impact and Fx.BossLanding so the two can't drift apart.
        public static void ImpactCamera(Vector2 pos, Vector2 shakeDir, float power, Color flash)
        {
            if (!Enabled)
                return;

            ZoomPunch(0.035f * power);
            Flash(flash, 0.22f * power);
            Shake(pos, shakeDir, 8f * power, 14f, 8 + (int)(14 * power));
            ScreenFX.Ripple(pos, 520f * power, 28, 0.8f * power);
        }

        // Camera work, ring, upward sparks, optional dust debris and ground skirt.
        // ground = true flattens the ring into a ground-hugging ellipse; false gives a full circle.
        public static void Impact(Vector2 pos, float power, ImpactStyle style = null, bool ground = true)
        {
            if (!Enabled)
                return;

            style ??= ImpactStyle.Default;

            ImpactCamera(pos, Main.rand.NextVector2Unit(), power, style.Flash);

            if (style.Sound is SoundStyle sound)
                SoundEngine.PlaySound(sound, pos);

            if (!ParticlesEnabled)
                return;

            float flatten = ground ? 0.28f : 1f;
            Vector2 ringPos = ground ? pos - new Vector2(0f, 3f) : pos;

            VisualFX.Ring(ringPos, 150f * power, style.InnerRing, 20, flatten);

            // Only the big moments get a second, wider ring.
            if (power >= 1.5f)
                VisualFX.Ring(ringPos, 250f * power, style.OuterRing, 32, flatten);

            VisualFX.Sparks(pos, -Vector2.UnitY, ground ? 1.26f : MathHelper.Pi, (int)(8 * power), 2.5f, 7f, style.Spark, 9f, 21);

            if (style.DebrisDust >= 0)
            {
                int chunks = (int)(26 * power);
                for (int i = 0; i < chunks; i++)
                {
                    Dust.NewDustPerfect(
                        pos + new Vector2(Main.rand.NextFloat(-30f, 30f), -4f),
                        style.DebrisDust,
                        new Vector2(Main.rand.NextFloat(-9f, 9f), -Main.rand.NextFloat(2f, 11f)),
                        100,
                        style.DebrisColor,
                        Main.rand.NextFloat(1.4f, 2.2f));
                }
            }

            if (style.SkirtDust >= 0)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    for (int i = 0; i < (int)(8 * power); i++)
                    {
                        Dust d = Dust.NewDustPerfect(
                            pos + new Vector2(side * Main.rand.NextFloat(0f, 20f), -2f),
                            style.SkirtDust,
                            new Vector2(side * Main.rand.NextFloat(4f, 9f), -Main.rand.NextFloat(0f, 1.2f)),
                            170,
                            style.SkirtColor,
                            Main.rand.NextFloat(1.2f, 1.9f));
                        d.noGravity = true;
                    }
                }
            }
        }
    }
}