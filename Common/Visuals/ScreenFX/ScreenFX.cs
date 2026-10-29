using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
using Terraria.ModLoader;

namespace ShatteredIllusion.Common.Visuals
{
    // One fullscreen shader pass for every screen effect. The filter is only active while something is playing,
    // so an idle screen costs nothing. Pulses decay on their own; re-calling one every frame holds it.
    public static class ScreenFX
    {
        internal const string EffectPath = "Common/Visuals/ScreenFX/ScreenFX";
        internal const string FilterName = "ShatteredIllusion:ScreenFX";

        private const int MaxRipples = 4;
        private const float RippleAmp = 0.025f;

        private struct Pulse
        {
            public float Peak;
            public int Life, Age;

            public float Value
            {
                get
                {
                    if (Life <= 0)
                        return 0f;

                    float t = 1f - (float)Age / Life;
                    return Peak * t * t;
                }
            }

            public void Start(float peak, int life)
            {
                if (peak < Value)
                    return;

                Peak = peak;
                Life = Math.Max(1, life);
                Age = 0;
            }

            public void Tick()
            {
                if (Life > 0 && ++Age >= Life)
                    Life = 0;
            }
        }

        private struct RippleState
        {
            public Vector2 Pos;
            public float Radius, Strength;
            public int Life, Age;
        }

        private static readonly RippleState[] ripples = new RippleState[MaxRipples];
        private static Pulse aberration, blur, wobble, glitch, invert;
        private static Vector2 blurCenter;
        private static bool blurHasCenter;

        private static float satNow = 1f, satGoal = 1f, conNow = 1f, conGoal = 1f, tintNow, tintGoal;
        private static Color tint = Color.White;
        private static int gradeRelease;

        private static bool registered;

        private static ShatteredIllusionConfig Cfg => ModContent.GetInstance<ShatteredIllusionConfig>();

        public static bool Allowed => Cinema.Enabled && Cfg.ScreenFilters && Cfg.ScreenFilterIntensity > 0.001f;

        // radius = world pixels the ring travels, strength 1 = a clear but not nauseating bend.
        public static void Ripple(Vector2 worldPos, float radius, int frames = 30, float strength = 1f)
        {
            if (!Allowed)
                return;

            int slot = 0;

            for (int i = 0; i < MaxRipples; i++)
            {
                if (ripples[i].Life <= 0)
                {
                    slot = i;
                    break;
                }

                if (ripples[i].Age > ripples[slot].Age)
                    slot = i;
            }

            ripples[slot] = new RippleState { Pos = worldPos, Radius = radius, Strength = strength, Life = Math.Max(1, frames) };
        }

        public static void Aberration(float strength, int frames = 20)
        {
            if (Allowed)
                aberration.Start(strength, frames);
        }

        public static void ZoomBlur(float strength, int frames = 18, Vector2? worldCenter = null)
        {
            if (!Allowed)
                return;

            blur.Start(strength, frames);
            blurHasCenter = worldCenter.HasValue;
            blurCenter = worldCenter ?? default;
        }

        public static void Wobble(float strength, int frames = 60)
        {
            if (Allowed)
                wobble.Start(strength, frames);
        }

        public static void Glitch(float strength, int frames = 20)
        {
            if (Allowed)
                glitch.Start(strength, frames);
        }

        public static void Invert(float strength, int frames = 8)
        {
            if (Allowed)
                invert.Start(strength, frames);
        }

        // Eases to the target and stays until released. saturation/contrast 1 = unchanged, tintAmount 0 = no tint.
        public static void Grade(float saturation = 1f, float contrast = 1f, Color? tintColor = null, float tintAmount = 0f, int autoReleaseFrames = 0)
        {
            if (!Allowed)
                return;

            satGoal = saturation;
            conGoal = contrast;
            tintGoal = tintAmount;
            tint = tintColor ?? Color.White;
            gradeRelease = autoReleaseFrames;
        }

        public static void ReleaseGrade()
        {
            satGoal = 1f;
            conGoal = 1f;
            tintGoal = 0f;
            gradeRelease = 0;
        }

        public static void Clear()
        {
            Array.Clear(ripples);
            aberration = blur = wobble = glitch = invert = default;
            satNow = satGoal = conNow = conGoal = 1f;
            tintNow = tintGoal = 0f;
            gradeRelease = 0;
            SetActive(false);
        }

        internal static void Register(Asset<Effect> effect)
        {
            Filters.Scene[FilterName] = new Filter(new Data(effect, "ScreenFX"), EffectPriority.VeryHigh);
            Filters.Scene[FilterName].Load();
            registered = true;
        }

        internal static void Update()
        {
            if (Main.dedServ || !registered)
                return;

            if (!Allowed)
            {
                Clear();
                return;
            }

            int live = 0;

            for (int i = 0; i < MaxRipples; i++)
            {
                if (ripples[i].Life <= 0)
                    continue;

                if (++ripples[i].Age >= ripples[i].Life)
                    ripples[i].Life = 0;
                else
                    live++;
            }

            aberration.Tick();
            blur.Tick();
            wobble.Tick();
            glitch.Tick();
            invert.Tick();

            if (gradeRelease > 0 && --gradeRelease == 0)
                ReleaseGrade();

            satNow = Ease(satNow, satGoal);
            conNow = Ease(conNow, conGoal);
            tintNow = Ease(tintNow, tintGoal);

            bool busy = live > 0 || aberration.Life > 0 || blur.Life > 0 || wobble.Life > 0 || glitch.Life > 0 || invert.Life > 0
                || satNow != 1f || conNow != 1f || tintNow != 0f;

            SetActive(busy);
        }

        private static float Ease(float now, float goal)
        {
            now = MathHelper.Lerp(now, goal, 0.06f);
            return Math.Abs(now - goal) < 0.002f ? goal : now;
        }

        private static void SetActive(bool on)
        {
            if (!registered)
                return;

            Filter f = Filters.Scene[FilterName];

            if (on && !f.IsActive())
                Filters.Scene.Activate(FilterName);
            else if (!on && f.IsActive())
                Filters.Scene.Deactivate(FilterName);
        }

        internal static void Unload()
        {
            if (registered)
                Clear();

            registered = false;
        }

        // World position to 0-1 screen UV, matching what the zoomed world was drawn with.
        private static Vector2 ToUV(Vector2 world, Matrix zoom, Vector2 size)
            => Vector2.Transform(world - Main.screenPosition, zoom) / size;

        private sealed class Data : ScreenShaderData
        {
            private readonly Vector4[] rippleData = new Vector4[MaxRipples];
            private readonly float[] rippleWidth = new float[MaxRipples];
            private bool bound;

            private EffectParameter pRipple, pRippleWidth, pAspect, pTime, pAberration, pBlur, pBlurCenter,
                pWobble, pGlitch, pSaturation, pContrast, pTint, pTintAmount, pInvert;

            public Data(Asset<Effect> effect, string pass) : base(effect, pass) { }

            public override void Apply()
            {
                if (!bound)
                    Bind();

                Push();
                base.Apply();
            }

            private void Bind()
            {
                var p = Shader.Parameters;

                pRipple = p["fxRipple"];
                pRippleWidth = p["fxRippleWidth"];
                pAspect = p["fxAspect"];
                pTime = p["fxTime"];
                pAberration = p["fxAberration"];
                pBlur = p["fxBlur"];
                pBlurCenter = p["fxBlurCenter"];
                pWobble = p["fxWobble"];
                pGlitch = p["fxGlitch"];
                pSaturation = p["fxSaturation"];
                pContrast = p["fxContrast"];
                pTint = p["fxTint"];
                pTintAmount = p["fxTintAmount"];
                pInvert = p["fxInvert"];
                bound = true;
            }

            private void Push()
            {
                float k = MathHelper.Clamp(Cfg.ScreenFilterIntensity, 0f, 1f);
                Vector2 size = new Vector2(Main.screenWidth, Main.screenHeight);
                Matrix zoomMatrix = Main.GameViewMatrix.ZoomMatrix;
                float zoom = Main.GameViewMatrix.Zoom.Y;

                for (int i = 0; i < MaxRipples; i++)
                {
                    ref RippleState r = ref ripples[i];

                    if (r.Life <= 0)
                    {
                        rippleData[i] = default;
                        rippleWidth[i] = 1f;
                        continue;
                    }

                    float inv = 1f - (float)r.Age / r.Life;
                    float radiusUV = r.Radius * zoom / size.Y;
                    Vector2 uv = ToUV(r.Pos, zoomMatrix, size);

                    rippleData[i] = new Vector4(uv.X, uv.Y, radiusUV * (1f - inv * inv), RippleAmp * r.Strength * inv * inv * k);
                    rippleWidth[i] = MathHelper.Clamp(radiusUV * 0.3f, 0.03f, 0.12f);
                }

                Vector2 center = blurHasCenter ? blurCenter : Main.LocalPlayer.Center;

                pRipple?.SetValue(rippleData);
                pRippleWidth?.SetValue(rippleWidth);
                pAspect?.SetValue(size.X / size.Y);
                pTime?.SetValue(Main.GlobalTimeWrappedHourly);
                pAberration?.SetValue(aberration.Value * 0.03f * k);
                pBlur?.SetValue(blur.Value * 0.2f * k);
                pBlurCenter?.SetValue(ToUV(center, zoomMatrix, size));
                pWobble?.SetValue(wobble.Value * 0.006f * k);
                pGlitch?.SetValue(MathHelper.Clamp(glitch.Value, 0f, 1f) * k);
                pSaturation?.SetValue(MathHelper.Lerp(1f, satNow, k));
                pContrast?.SetValue(MathHelper.Lerp(1f, conNow, k));
                pTint?.SetValue(tint.ToVector3());
                pTintAmount?.SetValue(tintNow * k);
                pInvert?.SetValue(MathHelper.Clamp(invert.Value, 0f, 1f) * k);
            }
        }
    }

    public class ScreenFXSystem : ModSystem
    {
        public override void OnModLoad()
        {
            if (Main.dedServ)
                return;

            ScreenFX.Register(Mod.Assets.Request<Effect>(ScreenFX.EffectPath, AssetRequestMode.ImmediateLoad));
        }

        public override void PostUpdateEverything() => ScreenFX.Update();

        public override void OnWorldUnload()
        {
            if (!Main.dedServ)
                ScreenFX.Clear();
        }

        public override void Unload() => ScreenFX.Unload();
    }
}