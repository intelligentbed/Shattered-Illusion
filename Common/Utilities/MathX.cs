using System;
using Microsoft.Xna.Framework;
using Terraria;

namespace ShatteredIllusion.Common.Utilities
{
    // Vector / angle / geometry / random helpers. Names avoid Terraria.Utils clashes (WithLength, LimitLength, StepToward...).
    public static class MathX
    {
        // ---------------- angles ----------------

        // Shortest signed difference from a to b in radians (-pi..pi).
        public static float AngleDiff(float a, float b) => MathHelper.WrapAngle(b - a);

        public static float LerpAngle(float a, float b, float t) => a + AngleDiff(a, b) * t;

        // Rotate `current` toward `target` by at most `maxStep` radians.
        public static float TurnToward(float current, float target, float maxStep)
        {
            float d = AngleDiff(current, target);
            return MathF.Abs(d) <= maxStep ? target : current + MathF.Sign(d) * maxStep;
        }

        // Rotate a velocity toward a target direction, keeping its speed. The core of every homing projectile.
        public static Vector2 TurnVelocity(Vector2 velocity, Vector2 desiredDir, float maxStepRadians)
        {
            float speed = velocity.Length();
            if (speed < 0.0001f || desiredDir == Vector2.Zero)
                return velocity;
            float angle = TurnToward(velocity.ToRotation(), desiredDir.ToRotation(), maxStepRadians);
            return angle.ToRotationVector2() * speed;
        }

        // ---------------- vectors ----------------

        public static Vector2 Perp(this Vector2 v) => new Vector2(-v.Y, v.X);

        public static Vector2 WithLength(this Vector2 v, float length)
        {
            float l = v.Length();
            return l < 0.0001f ? Vector2.Zero : v * (length / l);
        }

        public static Vector2 LimitLength(this Vector2 v, float maxLength)
        {
            float l2 = v.LengthSquared();
            return l2 > maxLength * maxLength ? v * (maxLength / MathF.Sqrt(l2)) : v;
        }

        public static Vector2 StepToward(this Vector2 from, Vector2 to, float maxStep) => Ease.Approach(from, to, maxStep);

        public static Vector2 DirTo(this Vector2 from, Vector2 to) => (to - from).SafeNormalize(Vector2.UnitX);

        public static Point ToTileCoords(this Vector2 world) => new Point((int)(world.X / 16f), (int)(world.Y / 16f));

        public static Vector2 ToWorldCoords(this Point tile) => new Vector2(tile.X * 16f, tile.Y * 16f);

        public static Vector2 TileCenter(this Point tile) => new Vector2(tile.X * 16f + 8f, tile.Y * 16f + 8f);

        // ---------------- prediction ----------------

        // Where to aim a projectile of `speed` fired from `shooter` so it meets a target moving at `targetVel`.
        // Falls back to the target's current position when there is no solution.
        public static Vector2 Intercept(Vector2 shooter, Vector2 target, Vector2 targetVel, float speed)
        {
            Vector2 d = target - shooter;
            float a = targetVel.LengthSquared() - speed * speed;
            float b = 2f * Vector2.Dot(d, targetVel);
            float c = d.LengthSquared();
            float t;

            if (MathF.Abs(a) < 0.0001f)
            {
                if (MathF.Abs(b) < 0.0001f)
                    return target;
                t = -c / b;
            }
            else
            {
                float disc = b * b - 4f * a * c;
                if (disc < 0f)
                    return target;
                float sq = MathF.Sqrt(disc);
                float t1 = (-b - sq) / (2f * a);
                float t2 = (-b + sq) / (2f * a);
                t = t1 > 0f && t2 > 0f ? MathF.Min(t1, t2) : MathF.Max(t1, t2);
            }

            return t > 0f ? target + targetVel * t : target;
        }

        // Launch velocity for a ballistic arc under constant gravity that lands on `target` in `ticks` ticks.
        public static Vector2 ArcVelocity(Vector2 from, Vector2 target, float gravityPerTick, float ticks)
        {
            ticks = MathF.Max(1f, ticks);
            Vector2 d = target - from;
            return new Vector2(d.X / ticks, d.Y / ticks - 0.5f * gravityPerTick * ticks);
        }

        // ---------------- curves ----------------

        public static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, float t)
        {
            float u = 1f - t;
            return u * u * a + 2f * u * t * b + t * t * c;
        }

        public static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float t)
        {
            float u = 1f - t;
            return u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d;
        }

        // Smooth path through p1..p2 using neighbours p0 and p3 for tangents.
        public static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t) => Vector2.CatmullRom(p0, p1, p2, p3, t);

        // ---------------- collision ----------------

        public static bool CircleRect(Vector2 center, float radius, Rectangle rect)
        {
            float cx = MathHelper.Clamp(center.X, rect.Left, rect.Right);
            float cy = MathHelper.Clamp(center.Y, rect.Top, rect.Bottom);
            float dx = center.X - cx, dy = center.Y - cy;
            return dx * dx + dy * dy <= radius * radius;
        }

        public static bool CircleCircle(Vector2 a, float ra, Vector2 b, float rb)
        {
            float r = ra + rb;
            return Vector2.DistanceSquared(a, b) <= r * r;
        }

        // Thick line vs rectangle (laser / beam hitboxes).
        public static bool LineRect(Vector2 start, Vector2 end, float width, Rectangle rect)
        {
            float point = 0f;
            return Collision.CheckAABBvLineCollision(rect.TopLeft(), rect.Size(), start, end, width, ref point);
        }

        // Is `point` inside a cone of half-angle `halfAngle` opening along `dir` from `origin`, within `range`?
        public static bool InCone(Vector2 origin, Vector2 dir, float halfAngle, float range, Vector2 point)
        {
            Vector2 d = point - origin;
            if (d.LengthSquared() > range * range)
                return false;
            return MathF.Abs(AngleDiff(dir.ToRotation(), d.ToRotation())) <= halfAngle;
        }

        public static Rectangle RectFromCenter(Vector2 center, float width, float height) =>
            new Rectangle((int)(center.X - width * 0.5f), (int)(center.Y - height * 0.5f), (int)width, (int)height);

        // ---------------- random ----------------

        // Uniform point in a disc (Main.rand.NextVector2Circular is an ellipse-friendly equivalent; this one takes a single radius).
        public static Vector2 InCircle(float radius) => Main.rand.NextVector2Circular(radius, radius);

        // Point on a ring between inner and outer radius, optionally limited to an arc around `facing`.
        public static Vector2 InRing(float inner, float outer, float facing = 0f, float arc = MathHelper.TwoPi)
        {
            float angle = facing + Main.rand.NextFloat(-arc * 0.5f, arc * 0.5f);
            float r = MathF.Sqrt(Main.rand.NextFloat(inner * inner, outer * outer));
            return angle.ToRotationVector2() * r;
        }

        // Pick an index by weight. Zero/negative weights are never picked. Returns -1 if all are zero.
        public static int PickWeighted(params float[] weights)
        {
            float total = 0f;
            foreach (float w in weights)
                if (w > 0f) total += w;
            if (total <= 0f)
                return -1;

            float roll = Main.rand.NextFloat(total);
            for (int i = 0; i < weights.Length; i++)
            {
                if (weights[i] <= 0f) continue;
                roll -= weights[i];
                if (roll <= 0f) return i;
            }
            return weights.Length - 1;
        }

        public static T PickRandom<T>(this T[] array) => array[Main.rand.Next(array.Length)];

        public static bool Chance(float probability) => Main.rand.NextFloat() < probability;

        public static float Signed() => Main.rand.NextBool() ? 1f : -1f;

        // Shuffle without allocating a new array.
        public static void Shuffle<T>(T[] array)
        {
            for (int i = array.Length - 1; i > 0; i--)
            {
                int j = Main.rand.Next(i + 1);
                (array[i], array[j]) = (array[j], array[i]);
            }
        }

        // Cheap smooth 1D noise (value noise) for wobble / drift. Deterministic for a given seed.
        public static float Noise1D(float x, int seed = 0)
        {
            int i = (int)MathF.Floor(x);
            float f = x - i;
            f = f * f * (3f - 2f * f);
            return MathHelper.Lerp(Hash(i, seed), Hash(i + 1, seed), f) * 2f - 1f;
        }

        private static float Hash(int n, int seed)
        {
            unchecked
            {
                uint h = (uint)(n * 374761393 + seed * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }
    }
}
