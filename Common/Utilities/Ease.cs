using System;
using Microsoft.Xna.Framework;

namespace ShatteredIllusion.Common.Utilities
{
    // Easing curves and small animation helpers. Every curve takes t in 0..1 (clamped) and returns ~0..1.
    //   float k = Ease.OutCubic(timer / 30f);
    //   float x = MathHelper.Lerp(a, b, Ease.InOutSine(t));
    //   float x = Ease.Lerp(a, b, t, Ease.OutBack);      // Func<float,float> overload
    public static class Ease
    {
        private const float C1 = 1.70158f;
        private const float C2 = C1 * 1.525f;
        private const float C3 = C1 + 1f;

        public static float Clamp01(float t) => t < 0f ? 0f : t > 1f ? 1f : t;

        public static float Linear(float t) => Clamp01(t);

        public static float InSine(float t) => 1f - MathF.Cos(Clamp01(t) * MathHelper.PiOver2);
        public static float OutSine(float t) => MathF.Sin(Clamp01(t) * MathHelper.PiOver2);
        public static float InOutSine(float t) => -(MathF.Cos(MathHelper.Pi * Clamp01(t)) - 1f) * 0.5f;

        public static float InQuad(float t) { t = Clamp01(t); return t * t; }
        public static float OutQuad(float t) { t = Clamp01(t); return 1f - (1f - t) * (1f - t); }
        public static float InOutQuad(float t) { t = Clamp01(t); return t < 0.5f ? 2f * t * t : 1f - MathF.Pow(-2f * t + 2f, 2f) * 0.5f; }

        public static float InCubic(float t) { t = Clamp01(t); return t * t * t; }
        public static float OutCubic(float t) { t = 1f - Clamp01(t); return 1f - t * t * t; }
        public static float InOutCubic(float t) { t = Clamp01(t); return t < 0.5f ? 4f * t * t * t : 1f - MathF.Pow(-2f * t + 2f, 3f) * 0.5f; }

        public static float InQuart(float t) { t = Clamp01(t); return t * t * t * t; }
        public static float OutQuart(float t) { t = 1f - Clamp01(t); return 1f - t * t * t * t; }
        public static float InOutQuart(float t) { t = Clamp01(t); return t < 0.5f ? 8f * t * t * t * t : 1f - MathF.Pow(-2f * t + 2f, 4f) * 0.5f; }

        public static float InExpo(float t) { t = Clamp01(t); return t <= 0f ? 0f : MathF.Pow(2f, 10f * t - 10f); }
        public static float OutExpo(float t) { t = Clamp01(t); return t >= 1f ? 1f : 1f - MathF.Pow(2f, -10f * t); }

        public static float InBack(float t) { t = Clamp01(t); return C3 * t * t * t - C1 * t * t; }
        public static float OutBack(float t) { t = Clamp01(t) - 1f; return 1f + C3 * t * t * t + C1 * t * t; }
        public static float InOutBack(float t)
        {
            t = Clamp01(t);
            return t < 0.5f
                ? (MathF.Pow(2f * t, 2f) * ((C2 + 1f) * 2f * t - C2)) * 0.5f
                : (MathF.Pow(2f * t - 2f, 2f) * ((C2 + 1f) * (t * 2f - 2f) + C2) + 2f) * 0.5f;
        }

        public static float OutElastic(float t)
        {
            t = Clamp01(t);
            if (t <= 0f || t >= 1f)
                return t;
            return MathF.Pow(2f, -10f * t) * MathF.Sin((t * 10f - 0.75f) * (MathHelper.TwoPi / 3f)) + 1f;
        }

        public static float OutBounce(float t)
        {
            t = Clamp01(t);
            const float n = 7.5625f, d = 2.75f;
            if (t < 1f / d) return n * t * t;
            if (t < 2f / d) { t -= 1.5f / d; return n * t * t + 0.75f; }
            if (t < 2.5f / d) { t -= 2.25f / d; return n * t * t + 0.9375f; }
            t -= 2.625f / d;
            return n * t * t + 0.984375f;
        }

        public static float InBounce(float t) => 1f - OutBounce(1f - t);

        // ---- shaping helpers ----

        public static float Lerp(float a, float b, float t, Func<float, float> curve) => a + (b - a) * curve(t);
        public static Vector2 Lerp(Vector2 a, Vector2 b, float t, Func<float, float> curve) => Vector2.Lerp(a, b, curve(t));
        public static Color Lerp(Color a, Color b, float t, Func<float, float> curve) => Color.Lerp(a, b, curve(t));

        // 0 -> 1 -> 0 over t (a hump). Good for flashes and telegraphs.
        public static float Hump(float t) => MathF.Sin(Clamp01(t) * MathHelper.Pi);

        // Rises over `inFrac`, holds, falls over the last `outFrac`. Fade in/out envelope for effects.
        public static float Envelope(float t, float inFrac = 0.15f, float outFrac = 0.3f)
        {
            t = Clamp01(t);
            if (inFrac > 0f && t < inFrac) return t / inFrac;
            if (outFrac > 0f && t > 1f - outFrac) return (1f - t) / outFrac;
            return 1f;
        }

        // t at which `value` sits between min and max, clamped. (Same as Terraria's Utils.GetLerpValue, but short.)
        public static float InverseLerp(float min, float max, float value) =>
            min == max ? 0f : Clamp01((value - min) / (max - min));

        public static float Remap(float value, float fromMin, float fromMax, float toMin, float toMax) =>
            MathHelper.Lerp(toMin, toMax, InverseLerp(fromMin, fromMax, value));

        public static float Remap(float value, float fromMin, float fromMax, float toMin, float toMax, Func<float, float> curve) =>
            MathHelper.Lerp(toMin, toMax, curve(InverseLerp(fromMin, fromMax, value)));

        // Move value toward target by at most `step`, never overshooting.
        public static float Approach(float value, float target, float step) =>
            value < target ? MathF.Min(value + step, target) : MathF.Max(value - step, target);

        public static Vector2 Approach(Vector2 value, Vector2 target, float step)
        {
            Vector2 d = target - value;
            float len = d.Length();
            return len <= step || len < 0.0001f ? target : value + d / len * step;
        }

        // Exponential smoothing toward a target. rate ~ 0.1 is gentle, 0.3 is snappy (per tick).
        public static float Damp(float value, float target, float rate) => MathHelper.Lerp(value, target, Clamp01(rate));
        public static Vector2 Damp(Vector2 value, Vector2 target, float rate) => Vector2.Lerp(value, target, Clamp01(rate));

        // Critically-damped spring step (Unity-style SmoothDamp). Keep `velocity` between calls. smoothTime in ticks.
        public static float SmoothDamp(float current, float target, ref float velocity, float smoothTime, float maxSpeed = float.MaxValue)
        {
            smoothTime = MathF.Max(0.0001f, smoothTime);
            float omega = 2f / smoothTime;
            float x = omega;
            float exp = 1f / (1f + x + 0.48f * x * x + 0.235f * x * x * x);
            float change = current - target;
            float maxChange = maxSpeed * smoothTime;
            change = MathHelper.Clamp(change, -maxChange, maxChange);
            float adjustedTarget = current - change;
            float temp = velocity + omega * change;
            velocity = (velocity - omega * temp) * exp;
            float output = adjustedTarget + (change + temp) * exp;
            if ((target - current > 0f) == (output > target))
            {
                output = target;
                velocity = 0f;
            }
            return output;
        }

        public static Vector2 SmoothDamp(Vector2 current, Vector2 target, ref Vector2 velocity, float smoothTime, float maxSpeed = float.MaxValue)
        {
            float vx = velocity.X, vy = velocity.Y;
            float x = SmoothDamp(current.X, target.X, ref vx, smoothTime, maxSpeed);
            float y = SmoothDamp(current.Y, target.Y, ref vy, smoothTime, maxSpeed);
            velocity = new Vector2(vx, vy);
            return new Vector2(x, y);
        }
    }

    // A value that chases a target with spring physics. Overshoots and settles, great for squash/stretch, UI pops, recoil.
    //   Spring s = new Spring(stiffness: 0.15f, damping: 0.8f);
    //   s.Kick(0.4f);  s.Update();  scale = 1f + s.Value;
    public struct Spring
    {
        public float Value, Velocity, Target, Stiffness, Damping;

        public Spring(float stiffness = 0.15f, float damping = 0.8f, float value = 0f)
        {
            Value = value;
            Velocity = 0f;
            Target = value;
            Stiffness = stiffness;
            Damping = damping;
        }

        public void Kick(float impulse) => Velocity += impulse;

        public float Update()
        {
            Velocity += (Target - Value) * Stiffness;
            Velocity *= Damping;
            Value += Velocity;
            return Value;
        }

        public bool Settled(float epsilon = 0.001f) => MathF.Abs(Value - Target) < epsilon && MathF.Abs(Velocity) < epsilon;
    }

    // Same idea for a 2D offset (knockback wobble, recoil).
    public struct Spring2
    {
        public Vector2 Value, Velocity, Target;
        public float Stiffness, Damping;

        public Spring2(float stiffness = 0.15f, float damping = 0.8f)
        {
            Value = Velocity = Target = Vector2.Zero;
            Stiffness = stiffness;
            Damping = damping;
        }

        public void Kick(Vector2 impulse) => Velocity += impulse;

        public Vector2 Update()
        {
            Velocity += (Target - Value) * Stiffness;
            Velocity *= Damping;
            Value += Velocity;
            return Value;
        }
    }
}
