using Microsoft.Xna.Framework;
using System;

namespace ShatteredIllusion.Core.Rigging
{
    /// <summary>
    /// Easing curves for clip keys and leg steps. All map 0..1 to 0..1 (<see cref="Back"/> overshoots in between)
    /// </summary>
    public enum RigEase
    {
        /// <summary>t^2(3-2t), the default everywhere</summary>
        Smooth,
        Linear,
        /// <summary>Slow start, fast end (wind-ups, anticipation)</summary>
        In,
        /// <summary>Fast start, slow end (strikes, slams)</summary>
        Out,
        InOut,
        /// <summary>Smoother than <see cref="Smooth"/>, zero first and second derivative at both ends</summary>
        Smoother,
        /// <summary>Overshoots the target slightly then settles. Good for snappy foot plants and recoils</summary>
        Back
    }

    public static class RigEaseExtensions
    {
        public static float Eval(this RigEase ease, float t)
        {
            t = MathHelper.Clamp(t, 0f, 1f);
            switch (ease)
            {
                case RigEase.Linear:
                    return t;
                case RigEase.In:
                    return t * t * t;
                case RigEase.Out:
                    float o = 1f - t;
                    return 1f - o * o * o;
                case RigEase.InOut:
                    return t < 0.5f ? 4f * t * t * t : 1f - MathF.Pow(-2f * t + 2f, 3f) * 0.5f;
                case RigEase.Smoother:
                    return t * t * t * (t * (t * 6f - 15f) + 10f);
                case RigEase.Back:
                    const float c1 = 1.70158f;
                    const float c3 = c1 + 1f;
                    float u = t - 1f;
                    return 1f + c3 * u * u * u + c1 * u * u;
                default:
                    return t * t * (3f - 2f * t);
            }
        }
    }

    /// <summary>
    /// Pure, stateless 2D limb math. Nothing here touches Terraria, so it is safe to call from anywhere
    /// </summary>
    public static class LimbSolvers
    {
        /// <summary>
        /// Analytic two-bone IK (law of cosines). Returns the knee/elbow position
        /// </summary>
        /// <param name="root">Hip / shoulder</param>
        /// <param name="target">Where the end of the limb wants to be</param>
        /// <param name="upper">Upper bone length</param>
        /// <param name="lower">Lower bone length</param>
        /// <param name="bendSign">+1 / -1: which side of the root-&gt;target line the joint bends towards (relative to the line's left normal)</param>
        /// <param name="tip">Where the tip really ends up (clamped to reach, so the limb straightens instead of tearing)</param>
        public static Vector2 TwoBone(Vector2 root, Vector2 target, float upper, float lower, float bendSign, out Vector2 tip)
        {
            Vector2 toTarget = target - root;
            float dist = toTarget.Length();

            float minReach = MathF.Abs(upper - lower) + 0.01f;
            float maxReach = upper + lower - 0.01f;

            Vector2 dir = dist > 0.0001f ? toTarget / dist : new Vector2(0f, 1f);
            dist = MathHelper.Clamp(dist, minReach, maxReach);
            tip = root + dir * dist;

            // distance along the root->tip line to the point right under the knee, and the knee's height off that line
            float along = (upper * upper - lower * lower + dist * dist) / (2f * dist);
            float height = MathF.Sqrt(MathF.Max(upper * upper - along * along, 0f));

            Vector2 normal = new Vector2(-dir.Y, dir.X);
            return root + dir * along + normal * (height * (bendSign >= 0f ? 1f : -1f));
        }

        /// <summary>
        /// Critically damped spring (no overshoot). <paramref name="omega"/> ~ 9 is slow and heavy, ~24 is snappy
        /// </summary>
        public static void Spring(ref Vector2 pos, ref Vector2 vel, Vector2 target, float omega, float dt = 1f / 60f)
        {
            Vector2 x = pos - target;
            Vector2 temp = (vel + x * omega) * dt;
            float decay = MathF.Exp(-omega * dt);
            vel = (vel - temp * omega) * decay;
            pos = target + (x + temp) * decay;
        }

        /// <summary>
        /// Decay factor for <see cref="SpringCached"/> at a fixed 60 fps tick. Compute once per omega and reuse it
        /// </summary>
        public static float DecayFor(float omega, float dt = 1f / 60f) => MathF.Exp(-omega * dt);

        /// <summary>
        /// Same spring, but with the exp() already worked out by <see cref="DecayFor"/>, so no per-tick transcendental
        /// </summary>
        public static void SpringCached(ref Vector2 pos, ref Vector2 vel, Vector2 target, float omega, float decay, float dt = 1f / 60f)
        {
            Vector2 x = pos - target;
            Vector2 temp = (vel + x * omega) * dt;
            vel = (vel - temp * omega) * decay;
            pos = target + (x + temp) * decay;
        }

        /// <summary>
        /// Classic t^2(3-2t) ease
        /// </summary>
        public static float SmoothStep(float t)
        {
            t = MathHelper.Clamp(t, 0f, 1f);
            return t * t * (3f - 2f * t);
        }

        /// <summary>Shortest-path angle lerp (radians), so a pose that wraps past pi doesn't spin the long way round</summary>
        public static float LerpAngle(float a, float b, float t) => a + MathHelper.WrapAngle(b - a) * t;

        public static Vector2 Rotate(Vector2 v, float radians)
        {
            float s = MathF.Sin(radians), c = MathF.Cos(radians);
            return new Vector2(v.X * c - v.Y * s, v.X * s + v.Y * c);
        }
    }

    internal static class RigUtil
    {
        /// <summary>Premultiplied-colour multiply (every channel incl. alpha), with an extra opacity factor</summary>
        public static Color Modulate(Color a, Color b, float opacity = 1f)
        {
            float k = opacity / 255f / 255f;
            return new Color(
                (int)(a.R * b.R * k * 255f),
                (int)(a.G * b.G * k * 255f),
                (int)(a.B * b.B * k * 255f),
                (int)(a.A * b.A * k * 255f));
        }

        public static float Smooth(float e0, float e1, float x)
        {
            float t = MathHelper.Clamp((x - e0) / (e1 - e0), 0f, 1f);
            return t * t * (3f - 2f * t);
        }
    }

    /// <summary>
    /// N-bone FABRIK chain with a stateful point array. Good for tails, tentacles, antennae and mandible stalks
    /// <br/>Usage: create once, call <see cref="Solve"/> every tick with the anchor and where the tip should go, read <see cref="Points"/> to draw
    /// </summary>
    public sealed class FabrikChain
    {
        private readonly float[] lengths;

        /// <summary>
        /// Joint positions, index 0 is the root and the last index is the tip
        /// </summary>
        public Vector2[] Points { get; }
        public float TotalLength { get; }
        public int BoneCount => lengths.Length;
        public Vector2 Root => Points[0];
        public Vector2 Tip => Points[Points.Length - 1];

        /// <summary>
        /// Sideways bow at the middle of the chain, in pixels. Stops long chains from looking like a ruler
        /// </summary>
        public float Bow { get; set; }

        public FabrikChain(params float[] boneLengths)
        {
            if (boneLengths.Length == 0)
                throw new ArgumentException("Need at least one bone", nameof(boneLengths));

            lengths = (float[])boneLengths.Clone();
            Points = new Vector2[lengths.Length + 1];
            foreach (float l in lengths)
                TotalLength += l;
        }

        public float LengthOf(int bone) => lengths[bone];

        /// <summary>
        /// Lay the chain out straight from <paramref name="root"/> along <paramref name="direction"/>
        /// </summary>
        public void Snap(Vector2 root, Vector2 direction)
        {
            if (direction.LengthSquared() < 0.0001f)
                direction = Vector2.UnitX;
            direction.Normalize();

            Points[0] = root;
            for (int i = 0; i < lengths.Length; i++)
                Points[i + 1] = Points[i] + direction * lengths[i];
        }

        public void Solve(Vector2 root, Vector2 target, int iterations = 2)
        {
            int n = lengths.Length;

            // can't reach: aim at the farthest reachable point so the chain straightens
            Vector2 toTarget = target - root;
            float reach = TotalLength * 0.999f;
            if (toTarget.LengthSquared() > reach * reach)
                target = root + DirOr(toTarget, Vector2.UnitX) * reach;

            for (int it = 0; it < iterations; it++)
            {
                // backward: pin the tip on the target and drag the chain towards the root
                Points[n] = target;
                for (int i = n - 1; i >= 0; i--)
                    Points[i] = Points[i + 1] + DirOr(Points[i] - Points[i + 1], Vector2.UnitX) * lengths[i] + BowOffset(i, Points[i] - Points[i + 1]);

                // forward: pin the root back and push outwards
                Points[0] = root;
                for (int i = 1; i <= n; i++)
                    Points[i] = Points[i - 1] + DirOr(Points[i] - Points[i - 1], Vector2.UnitX) * lengths[i - 1] + BowOffset(i, Points[i] - Points[i - 1]);
            }
        }

        private Vector2 BowOffset(int joint, Vector2 d)
        {
            if (Bow == 0f)
                return Vector2.Zero;

            Vector2 dir = DirOr(d, Vector2.UnitX);
            float weight = MathF.Sin(joint / (float)(lengths.Length) * MathHelper.Pi);
            return new Vector2(-dir.Y, dir.X) * (weight * Bow * 0.25f);
        }

        private static Vector2 DirOr(Vector2 v, Vector2 fallback)
        {
            float len = v.Length();
            return len > 0.0001f ? v / len : fallback;
        }
    }
}
