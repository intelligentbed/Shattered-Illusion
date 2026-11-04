using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Graphics.Effects;

namespace ShatteredIllusion.Common.Visuals
{
    public static partial class ScreenFX
    {
        internal const string EffectPath = "Common/Visuals/ScreenFX/ScreenFX";

        private const string GradeTag = "grade";
        private const int ChannelCount = 22;
        private const int MaxTracks = 64;
        private const int MaxPoints = 4;
        private const float PointAmp = 0.025f;
        private const float Eps = 0.0005f;

        // Strength left on flicker channels (and Cinema.Flash) when "reduce flashing" is on.
        internal const float ReduceFlashScale = 0.3f;

        private const int ModeGrade = 0, ModeDistort = 1, ModeFull = 2;
        private static readonly string[] FilterNames = { "ShatteredIllusion:ScreenFXGrade", "ShatteredIllusion:ScreenFXDistort", "ShatteredIllusion:ScreenFX" };
        private static readonly string[] PassNames = { "Grade", "Distort", "ScreenFX" };

        private enum Combine { Add, Max, Mul }
        private enum Cat { Warp, Color, Flicker }

        private readonly struct ChannelInfo
        {
            public readonly Combine Mode;
            public readonly float Identity, Scale;
            public readonly Cat Category;
            public readonly int ColorSlot;

            public ChannelInfo(Combine mode, float identity, float scale, Cat category, int colorSlot = -1)
            {
                Mode = mode;
                Identity = identity;
                Scale = scale;
                Category = category;
                ColorSlot = colorSlot;
            }
        }

        // Same order as Channel. Scale turns the public "1 = strong" value into shader units.
        private static readonly ChannelInfo[] Infos =
        {
            new(Combine.Add, 0f, 0.03f, Cat.Warp),
            new(Combine.Add, 0f, 0.2f, Cat.Warp),
            new(Combine.Add, 0f, 0.05f, Cat.Warp),
            new(Combine.Add, 0f, 0.05f, Cat.Warp),
            new(Combine.Add, 0f, 0.006f, Cat.Warp),
            new(Combine.Mul, 1f, 1f, Cat.Warp),
            new(Combine.Mul, 1f, 1f, Cat.Warp),
            new(Combine.Add, 0f, 1f, Cat.Flicker),
            new(Combine.Add, 0f, 0.5f, Cat.Warp),
            new(Combine.Max, 0f, 1f, Cat.Warp),
            new(Combine.Max, 0f, 1f, Cat.Color),
            new(Combine.Add, 0f, 1f, Cat.Color),
            new(Combine.Add, 0f, 1f, Cat.Color),
            new(Combine.Mul, 1f, 1f, Cat.Color),
            new(Combine.Mul, 1f, 1f, Cat.Color),
            new(Combine.Mul, 1f, 1f, Cat.Color),
            new(Combine.Add, 0f, 1f, Cat.Color),
            new(Combine.Add, 0f, 1f, Cat.Color),
            new(Combine.Add, 0f, 1f, Cat.Color, 0),
            new(Combine.Add, 0f, 1f, Cat.Flicker),
            new(Combine.Add, 0f, 1f, Cat.Color, 1),
            new(Combine.Add, 0f, 1f, Cat.Flicker, 2),
        };

        private static readonly Vector3[] ColorDefault = { Vector3.One, Vector3.Zero, Vector3.Zero };

        private struct Track
        {
            public int Id;
            public bool IsPoint;
            public Channel Chan;
            public PointKind Kind;
            public ScreenShader Shader;
            public float Target;
            public Color Color;
            public Vector2 Pos;
            public float Radius;
            public Entity Follow;
            public int FollowKey;
            public Env Env;
            public string Tag;

            public int Phase, Age, RelFrames;
            public float Level, From, ReleaseFrom;
        }

        private struct PointEval
        {
            public Vector2 Pos;
            public float Radius, Max, Ring, Swirl, Bulge;
        }

        private static readonly Track[] tracks = new Track[MaxTracks];
        private static int nextId;

        private static readonly float[] sum = new float[ChannelCount];
        private static readonly float[] uni = new float[ChannelCount];
        private static readonly Vector3[] colorAcc = new Vector3[3];
        private static readonly float[] colorW = new float[3];
        private static readonly Vector3[] colorOut = new Vector3[3];
        private static readonly PointEval[] points = new PointEval[MaxPoints];
        private static int pointCount;

        private static Vector2 zoomCenter;
        private static bool zoomCenterSet;

        private static readonly bool[] have = new bool[3];
        private static readonly List<ScreenShader> customs = new List<ScreenShader>();
        private static bool registered;
        private static bool wake;
        private static int mode = -1;

        private static bool allowed;
        private static float warpScale = 1f, colorScale = 1f;
        private static bool reduceFlash;

        public static bool Allowed => allowed;

        // ---- spawning ----

        private static ScreenHandle Spawn(Track t, bool merge)
        {
            if (!allowed)
                return default;

            int free = -1;

            for (int i = 0; i < MaxTracks; i++)
            {
                ref Track o = ref tracks[i];

                if (o.Id == 0)
                {
                    if (free < 0)
                        free = i;

                    continue;
                }

                bool same = !t.IsPoint && !o.IsPoint && o.Chan == t.Chan && o.Shader == t.Shader && o.Tag == t.Tag;

                if (same && (merge || t.Tag != null))
                {
                    o.Target = t.Target;
                    o.Env = t.Env;
                    o.Color = t.Color;
                    o.Follow = t.Follow;
                    o.FollowKey = t.FollowKey;
                    Restart(ref o);
                    wake = true;
                    return new ScreenHandle(i, o.Id);
                }
            }

            if (t.IsPoint)
            {
                int count = 0, oldest = -1;

                for (int i = 0; i < MaxTracks; i++)
                {
                    if (tracks[i].Id == 0 || !tracks[i].IsPoint)
                        continue;

                    count++;

                    if (oldest < 0 || tracks[i].Id < tracks[oldest].Id)
                        oldest = i;
                }

                if (count >= MaxPoints)
                    free = oldest;
            }

            if (free < 0)
                return default;

            t.Id = ++nextId;
            Restart(ref t);
            tracks[free] = t;
            wake = true;

            return new ScreenHandle(free, t.Id);
        }

        // ---- envelope ----

        private static void Restart(ref Track t)
        {
            t.From = t.Level;
            t.Phase = 0;
            t.Age = 0;

            if (t.Env.Attack <= 0)
            {
                t.Level = 1f;
                EnterSustain(ref t);
            }
        }

        private static void EnterSustain(ref Track t)
        {
            t.Phase = 1;
            t.Age = 0;

            if (t.Env.Sustain == 0)
                BeginRelease(ref t);
        }

        private static void BeginRelease(ref Track t, int frames = -1)
        {
            if (t.Phase == 2)
                return;

            t.Phase = 2;
            t.Age = 0;
            t.ReleaseFrom = t.Level;
            t.RelFrames = Math.Max(1, frames >= 0 ? frames : t.Env.Release);
        }

        private static float EaseOf(Curve curve, float t) => curve switch
        {
            Curve.In => t * t,
            Curve.Out => 1f - (1f - t) * (1f - t),
            Curve.InOut => t * t * (3f - 2f * t),
            _ => t
        };

        private static void Tick(ref Track t)
        {
            if (t.Follow != null && t.Phase != 2 && !VisualFX.StillFollowing(t.Follow, t.FollowKey))
                BeginRelease(ref t);

            t.Age++;

            switch (t.Phase)
            {
                case 0:
                    if (t.Age >= t.Env.Attack)
                    {
                        t.Level = 1f;
                        EnterSustain(ref t);
                    }
                    else
                    {
                        t.Level = MathHelper.Lerp(t.From, 1f, EaseOf(t.Env.Ease, (float)t.Age / t.Env.Attack));
                    }

                    break;

                case 1:
                    if (t.Env.Sustain >= 0 && t.Age >= t.Env.Sustain)
                        BeginRelease(ref t);

                    break;

                default:
                    if (t.Age >= t.RelFrames)
                        t = default;
                    else
                        t.Level = t.ReleaseFrom * (1f - EaseOf(t.Env.Ease, (float)t.Age / t.RelFrames));

                    break;
            }
        }

        // ---- per frame ----

        internal static void Update()
        {
            if (Main.dedServ || (!registered && customs.Count == 0))
                return;

            ShatteredIllusionConfig cfg = Terraria.ModLoader.ModContent.GetInstance<ShatteredIllusionConfig>();

            warpScale = MathHelper.Clamp(cfg.ScreenFilterIntensity, 0f, 1f);
            colorScale = MathHelper.Clamp(cfg.ScreenColorIntensity, 0f, 1f);
            reduceFlash = cfg.ScreenReduceFlashing;
            allowed = Cinema.Enabled && cfg.ScreenFilters && (warpScale > 0.001f || colorScale > 0.001f);

            if (!allowed)
            {
                if (wake || mode >= 0)
                    Clear();

                return;
            }

            if (!wake)
                return;

            int alive = 0;

            for (int i = 0; i < MaxTracks; i++)
            {
                if (tracks[i].Id == 0)
                    continue;

                Tick(ref tracks[i]);

                if (tracks[i].Id != 0)
                    alive++;
            }

            Evaluate();

            if (alive == 0)
                wake = false;
        }

        // Combines every live track into one set of shader values and picks the cheapest pass that can draw them.
        private static void Evaluate()
        {
            for (int i = 0; i < ChannelCount; i++)
                sum[i] = Infos[i].Identity;

            Array.Clear(colorAcc);
            Array.Clear(colorW);
            pointCount = 0;

            for (int i = 0; i < customs.Count; i++)
                customs[i].Level = 0f;

            for (int i = 0; i < MaxTracks; i++)
            {
                ref Track t = ref tracks[i];

                if (t.Id == 0 || t.Level <= 0f)
                    continue;

                if (t.IsPoint)
                {
                    PointEval e = EvalPoint(ref t);

                    if (pointCount < MaxPoints && Math.Abs(e.Ring) + Math.Abs(e.Swirl) + Math.Abs(e.Bulge) > Eps)
                        points[pointCount++] = e;

                    continue;
                }

                if (t.Shader != null)
                {
                    t.Shader.Level = Math.Max(t.Shader.Level, t.Target * t.Level);
                    continue;
                }

                int c = (int)t.Chan;
                ChannelInfo info = Infos[c];

                switch (info.Mode)
                {
                    case Combine.Add:
                        sum[c] += t.Target * t.Level;
                        break;

                    case Combine.Max:
                        sum[c] = Math.Max(sum[c], t.Target * t.Level);
                        break;

                    default:
                        sum[c] *= MathHelper.Lerp(1f, t.Target, t.Level);
                        break;
                }

                if (info.ColorSlot >= 0)
                {
                    colorAcc[info.ColorSlot] += t.Color.ToVector3() * t.Level;
                    colorW[info.ColorSlot] += t.Level;
                }
            }

            bool any = pointCount > 0;

            for (int i = 0; i < ChannelCount; i++)
            {
                ChannelInfo info = Infos[i];
                float v = sum[i];
                float k = info.Category == Cat.Color ? colorScale : warpScale;

                if (info.Category == Cat.Flicker && reduceFlash)
                    k *= ReduceFlashScale;

                v = info.Mode == Combine.Mul ? MathHelper.Lerp(1f, v, k) : v * k * info.Scale;

                if (i == (int)Channel.Posterize)
                    v = v > Eps ? MathHelper.Lerp(32f, 4f, Math.Min(v, 1f)) : 0f;

                uni[i] = v;

                if (Math.Abs(v - info.Identity) > Eps)
                    any = true;
            }

            for (int s = 0; s < 3; s++)
                colorOut[s] = colorW[s] > 0f ? colorAcc[s] / colorW[s] : ColorDefault[s];

            bool motion = Lit(Channel.ZoomBlur) || Lit(Channel.MotionX) || Lit(Channel.MotionY);
            bool warp = pointCount > 0 || Lit(Channel.Aberration) || Lit(Channel.Wobble) || Lit(Channel.Glitch)
                || Lit(Channel.Lens) || Lit(Channel.Pixelate);

            SetMode(!any ? -1 : motion ? ModeFull : warp ? ModeDistort : ModeGrade);

            for (int i = 0; i < customs.Count; i++)
            {
                ScreenShader s = customs[i];
                bool on = s.Level > Eps;

                if (on == s.Active)
                    continue;

                s.Active = on;
                SetFilter(s.FilterName, on);
            }
        }

        private static bool Lit(Channel c) => Math.Abs(uni[(int)c]) > Eps;

        private static PointEval EvalPoint(ref Track t)
        {
            var e = new PointEval { Pos = t.Follow != null ? t.Follow.Center + t.Pos : t.Pos, Max = t.Radius, Radius = t.Radius };

            switch (t.Kind)
            {
                case PointKind.Ring:
                    e.Radius = t.Radius * (1f - t.Level);
                    e.Ring = PointAmp * t.Target * t.Level * warpScale;
                    break;

                case PointKind.Swirl:
                    e.Swirl = t.Target * t.Level * warpScale;
                    break;

                default:
                    e.Bulge = t.Target * t.Level * warpScale;
                    break;
            }

            return e;
        }

        // ---- filters ----

        // A missing cheaper pass falls back to the next heavier one.
        private static void SetMode(int want)
        {
            while (want >= 0 && want < ModeFull && !have[want])
                want++;

            if (want >= 0 && !have[want])
                want = -1;

            if (want == mode)
                return;

            int old = mode;
            mode = want;

            if (want >= 0)
                SetFilter(FilterNames[want], true);

            if (old >= 0)
                SetFilter(FilterNames[old], false);
        }

        private static void SetFilter(string name, bool on)
        {
            Filter f = Filters.Scene[name];

            if (on && !f.IsActive())
                Filters.Scene.Activate(name);
            else if (!on && f.IsActive())
                Filters.Scene.Deactivate(name);
        }

        public static void Clear()
        {
            Array.Clear(tracks);
            wake = false;
            Evaluate();
        }

        internal static void Unload()
        {
            Clear();

            customs.Clear();
            Array.Clear(have);
            registered = false;
            mode = -1;
        }

        // World position to 0-1 screen UV, matching what the zoomed world was drawn with.
        public static Vector2 WorldToUV(Vector2 world)
            => Vector2.Transform(world - Main.screenPosition, Main.GameViewMatrix.ZoomMatrix) / new Vector2(Main.screenWidth, Main.screenHeight);
    }
}
