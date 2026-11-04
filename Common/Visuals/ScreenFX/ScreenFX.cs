using System;
using Microsoft.Xna.Framework;
using Terraria;

namespace ShatteredIllusion.Common.Visuals
{
    // Every value is "1 = a strong, clear amount". Mul channels (Saturation, Contrast, Exposure, WobbleFreq, WobbleSpeed)
    // take the value they should reach, 1 = unchanged. Pixelate is the block size in pixels, Hue is radians.
    public enum Channel
    {
        Aberration, ZoomBlur, MotionX, MotionY, Wobble, WobbleFreq, WobbleSpeed, Glitch, Lens, Pixelate,
        Posterize, Scanlines, Grain,
        Saturation, Contrast, Exposure, Brightness, Hue, Tint, Invert, Vignette, Fade
    }

    public enum Curve { Linear, In, Out, InOut }

    public enum PointKind { Ring, Swirl, Bulge }

    // Attack frames up, Sustain frames held (-1 = until released), Release frames down.
    public readonly struct Env
    {
        public readonly int Attack, Sustain, Release;
        public readonly Curve Ease;

        public Env(int attack, int sustain, int release, Curve ease = Curve.Out)
        {
            Attack = attack;
            Sustain = sustain;
            Release = release;
            Ease = ease;
        }

        public static Env Pulse(int frames, Curve ease = Curve.Out) => new Env(0, 0, frames, ease);
        public static Env Hold(int attack = 20, int release = 30, Curve ease = Curve.Out) => new Env(attack, -1, release, ease);
        public static Env Timed(int attack, int sustain, int release, Curve ease = Curve.Out) => new Env(attack, sustain, release, ease);
    }

    public readonly struct ScreenHandle
    {
        internal readonly int Slot, Id;

        internal ScreenHandle(int slot, int id)
        {
            Slot = slot;
            Id = id;
        }

        public bool IsPlaying => ScreenFX.IsPlaying(this);
        public void Release(int frames = -1) => ScreenFX.Release(this, frames);
        public void Retarget(float value) => ScreenFX.Retarget(this, value);
        public void Stop() => ScreenFX.Stop(this);
    }

    // The public face of the screen filter system. Core.cs runs it, Shaders.cs talks to the GPU.
    // Quick pulses first, then the generic Pulse/Hold/Add that reach every channel, then custom shaders.
    public static partial class ScreenFX
    {
        // ---- point effects (a position on screen, up to 4 at once) ----

        // radius = world pixels the ring travels, strength 1 = a clear but not nauseating bend.
        public static ScreenHandle Ripple(Vector2 worldPos, float radius, int frames = 30, float strength = 1f)
            => AddPoint(PointKind.Ring, worldPos, radius, strength, Env.Pulse(frames));

        // angle in radians at the center, fading to nothing at the radius.
        public static ScreenHandle Swirl(Vector2 worldPos, float radius, float angle, Env env, Entity follow = null)
            => AddPoint(PointKind.Swirl, worldPos, radius, angle, env, follow);

        // positive magnifies, negative pinches. 0.5 is strong.
        public static ScreenHandle Bulge(Vector2 worldPos, float radius, float strength, Env env, Entity follow = null)
            => AddPoint(PointKind.Bulge, worldPos, radius, strength, env, follow);

        public static ScreenHandle AddPoint(PointKind kind, Vector2 worldPos, float radius, float strength, Env env, Entity follow = null, string tag = null)
        {
            Vector2 pos = follow != null ? worldPos - follow.Center : worldPos;

            return Spawn(new Track
            {
                IsPoint = true,
                Kind = kind,
                Pos = pos,
                Radius = Math.Max(1f, radius),
                Target = strength,
                Env = env,
                Follow = follow,
                FollowKey = VisualFX.FollowKeyOf(follow),
                Tag = tag
            }, false);
        }

        // ---- quick pulses (calling one every frame holds it at full) ----

        public static ScreenHandle Aberration(float strength, int frames = 20) => Pulse(Channel.Aberration, strength, frames);

        // Center is where the blur streams toward; null = the local player.
        public static ScreenHandle ZoomBlur(float strength, int frames = 18, Vector2? worldCenter = null)
        {
            zoomCenterSet = worldCenter.HasValue;
            zoomCenter = worldCenter ?? default;
            return Pulse(Channel.ZoomBlur, strength, frames);
        }

        // direction in screen fractions, (1, 0) smears a strong amount to the right.
        public static void MotionBlur(Vector2 direction, int frames = 12)
        {
            Pulse(Channel.MotionX, direction.X, frames);
            Pulse(Channel.MotionY, direction.Y, frames);
        }

        public static ScreenHandle Wobble(float strength, int frames = 60) => Pulse(Channel.Wobble, strength, frames);
        public static ScreenHandle Glitch(float strength, int frames = 20) => Pulse(Channel.Glitch, strength, frames);
        public static ScreenHandle Invert(float strength, int frames = 8) => Pulse(Channel.Invert, strength, frames);
        public static ScreenHandle Lens(float strength, int frames = 30) => Pulse(Channel.Lens, strength, frames);
        public static ScreenHandle Pixelate(float blockPixels, int frames = 30) => Pulse(Channel.Pixelate, blockPixels, frames);
        public static ScreenHandle Posterize(float strength, int frames = 30) => Pulse(Channel.Posterize, strength, frames);
        public static ScreenHandle Scanlines(float strength, int frames = 60) => Pulse(Channel.Scanlines, strength, frames);
        public static ScreenHandle Grain(float strength, int frames = 60) => Pulse(Channel.Grain, strength, frames);

        public static ScreenHandle Vignette(float strength, Color? color = null, int frames = 40)
            => Pulse(Channel.Vignette, strength, frames, color ?? Color.Black);

        // Fades the whole picture toward a color. This is the screen flash, and it works under zoom.
        public static ScreenHandle Flash(Color color, float amount = 0.5f, int frames = 10)
            => Pulse(Channel.Fade, amount, frames, color);

        // ---- generic: reaches every channel ----

        public static ScreenHandle Pulse(Channel channel, float value, int frames, Color? color = null)
            => Spawn(MakeTrack(channel, value, Env.Pulse(frames), color, null, null), true);

        // Eases in and stays until released: ScreenFX.Hold(...).Release() or ScreenFX.Release("tag").
        // Calling it again with the same tag retargets the running one instead of stacking.
        public static ScreenHandle Hold(Channel channel, float value, int attack = 20, Color? color = null, Entity follow = null, string tag = null)
            => Add(channel, value, Env.Hold(attack), color, follow, tag);

        // Full control. color only matters for Tint, Vignette and Fade. follow releases it when the entity dies.
        public static ScreenHandle Add(Channel channel, float value, Env env, Color? color = null, Entity follow = null, string tag = null)
            => Spawn(MakeTrack(channel, value, env, color, follow, tag), false);

        // ---- color grade ----

        // Eases to the target and stays until released (or autoReleaseFrames). saturation/contrast 1 = unchanged.
        public static void Grade(float saturation = 1f, float contrast = 1f, Color? tintColor = null, float tintAmount = 0f, int autoReleaseFrames = 0)
        {
            Release(GradeTag, 40);

            Env env = autoReleaseFrames > 0 ? Env.Timed(25, autoReleaseFrames, 40) : Env.Hold(25, 40);

            if (saturation != 1f)
                Add(Channel.Saturation, saturation, env, tag: GradeTag);

            if (contrast != 1f)
                Add(Channel.Contrast, contrast, env, tag: GradeTag);

            if (tintAmount != 0f)
                Add(Channel.Tint, tintAmount, env, tintColor ?? Color.White, tag: GradeTag);
        }

        public static void ReleaseGrade() => Release(GradeTag, 40);

        // ---- custom shaders ----

        // Runs your own shader as a filter. level is 0-1 (times peak, times the player's intensity setting) and the
        // filter is only active while level is above zero, so make the shader do nothing at level 0.
        // apply sets your uniforms each frame. tML's standard uTime, uScreenResolution and uScreenPosition also work.
        public static ScreenHandle Play(ScreenShader shader, float peak, Env env, string tag = null)
        {
            if (shader == null)
                return default;

            return Spawn(new Track { Shader = shader, Target = peak, Env = env, Tag = tag, Color = Color.White }, false);
        }

        // ---- control ----

        public static void Release(ScreenHandle handle, int frames = -1)
        {
            if (IsPlaying(handle))
                BeginRelease(ref tracks[handle.Slot], frames);
        }

        // Releases everything started with this tag.
        public static void Release(string tag, int frames = -1)
        {
            for (int i = 0; i < MaxTracks; i++)
            {
                if (tracks[i].Id != 0 && tracks[i].Tag == tag)
                    BeginRelease(ref tracks[i], frames);
            }
        }

        // Releases every hold. Pulses and timed effects run out on their own.
        public static void ReleaseAll(int frames = -1)
        {
            for (int i = 0; i < MaxTracks; i++)
            {
                if (tracks[i].Id != 0 && tracks[i].Env.Sustain < 0)
                    BeginRelease(ref tracks[i], frames);
            }
        }

        public static void Retarget(ScreenHandle handle, float value)
        {
            if (IsPlaying(handle))
                tracks[handle.Slot].Target = value;
        }

        public static void Stop(ScreenHandle handle)
        {
            if (IsPlaying(handle))
                tracks[handle.Slot] = default;
        }

        public static bool IsPlaying(ScreenHandle handle)
            => handle.Id != 0 && tracks[handle.Slot].Id == handle.Id;

        private static Track MakeTrack(Channel channel, float value, Env env, Color? color, Entity follow, string tag) => new Track
        {
            Chan = channel,
            Target = value,
            Env = env,
            Color = color ?? Color.White,
            Follow = follow,
            FollowKey = VisualFX.FollowKeyOf(follow),
            Tag = tag
        };
    }
}
