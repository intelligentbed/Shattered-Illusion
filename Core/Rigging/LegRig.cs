using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;

namespace ShatteredIllusion.Core.Rigging
{
    /// <summary>
    /// Finds the surface a foot should land on: cast from <paramref name="origin"/> along unit <paramref name="direction"/> for at most
    /// <paramref name="maxDistance"/> px. Return the hit point and the surface normal (pointing away from the surface, towards the body)
    /// <br/>Swap in your own to walk on platforms only, a moving arena, NPC backs, a heightmap... set <see cref="LegRig.Probe"/>
    /// </summary>
    public delegate bool SurfaceProbe(Vector2 origin, Vector2 direction, float maxDistance, out Vector2 hit, out Vector2 normal);

    /// <summary>
    /// Draw one leg yourself. Return true if handled (the built-in drawing is skipped for that leg)
    /// </summary>
    public delegate bool LegDrawer(SpriteBatch sb, LegRig rig, ProceduralLeg leg, Vector2 screenPos, Color light, float scale);

    /// <summary>
    /// A hand-drawn limb segment. Art points along +X from <see cref="Pivot"/> (the joint). With <see cref="Stretch"/> the art is stretched along the bone to
    /// span joint to joint (pivot to the right edge of <see cref="Source"/> maps to the bone), so the leg never tears or shows gaps
    /// </summary>
    public sealed class LegSprite
    {
        public Texture2D Texture;
        public Rectangle Source;
        public Vector2 Pivot;
        public bool Stretch = true;

        public LegSprite(Texture2D texture, Rectangle source, Vector2 pivot, bool stretch = true)
        {
            Texture = texture;
            Source = source;
            Pivot = pivot;
            Stretch = stretch;
        }
    }

    /// <summary>
    /// What the rig needs to know about the body each tick
    /// </summary>
    public struct RigBody
    {
        public Vector2 Center;
        public Vector2 Velocity;
        /// <summary>+1 / -1</summary>
        public int Facing;
        public float Scale;
        /// <summary>The body is on a surface (feet plant), as opposed to airborne (legs dangle). For wall and ceiling crawlers this means "attached"</summary>
        public bool Grounded;
        /// <summary>
        /// Body rotation in radians. The leg frame turns with it: "down" (where feet look for a surface) becomes <c>(-sin, cos)</c>, so a wall crawler on a
        /// left wall uses -PiOver2 and a ceiling crawler uses Pi
        /// </summary>
        public float Rotation;

        public RigBody(Vector2 center, Vector2 velocity, int facing, float scale, bool grounded, float rotation = 0f)
        {
            Center = center;
            Velocity = velocity;
            Facing = facing;
            Scale = scale;
            Grounded = grounded;
            Rotation = rotation;
        }
    }

    internal struct BodyFrame
    {
        public Vector2 Center, Velocity;
        /// <summary>Unit vector of the body's local +X in world space (not flipped by facing)</summary>
        public Vector2 Right;
        /// <summary>Unit vector of the body's local +Y, towards the surface the feet stand on</summary>
        public Vector2 Down;
        public int Facing;
        public float Scale;
        public bool Grounded;
        /// <summary>Velocity along <see cref="Right"/></summary>
        public float Along;
    }

    /// <summary>
    /// One procedurally animated leg: the foot stays planted in the world, steps to a new spot when the body has moved
    /// too far from it, and the knee is solved with two-bone IK. Give it a <see cref="Coxa"/> for a short third segment at the hip (spider legs)
    /// <br/>All "Local" values are in the body's frame: +X is the direction the creature faces, +Y is down (towards its surface), in unscaled pixels
    /// </summary>
    public sealed class ProceduralLeg
    {
        // ---- configuration ----
        public Vector2 LocalHip;
        public float Upper = 26f;
        public float Lower = 34f;
        /// <summary>Length of an optional first segment between the hip and the IK limb. 0 = plain two-bone leg</summary>
        public float Coxa;
        /// <summary>Direction of the coxa in the body frame, radians from forward (negative = up). Only used when <see cref="Coxa"/> &gt; 0</summary>
        public float CoxaAngle = -0.6f;
        /// <summary>Where the foot likes to land relative to the hip (local X only, forward positive)</summary>
        public float RestForward = 8f;
        /// <summary>+1 knee bends towards the front of the creature, -1 towards the back</summary>
        public float Bend = 1f;
        /// <summary>Legs of the same group step together. Groups 0..31. Legs of other groups never start a step while <see cref="LegRig.MaxSteppingGroups"/> others are mid-step (unless overstretched)</summary>
        public int Group;
        /// <summary>Distance between the planted foot and the ideal spot that triggers a step</summary>
        public float StepThreshold = 24f;
        public float StepLift = 14f;
        /// <summary>Ticks a step takes when standing still / at speed (it speeds up with body speed)</summary>
        public float StepTicks = 12f;
        public float MinStepTicks = 6f;
        /// <summary>Easing of the foot's travel during a step. <see cref="RigEase.Back"/> gives a snappy plant</summary>
        public RigEase StepEase = RigEase.Smooth;
        /// <summary>Shapes the lift arc: 1 = symmetric, below 1 the foot peaks early (quick lift, slow reach), above 1 it peaks late</summary>
        public float LiftSkew = 1f;
        /// <summary>Tilts the surface search sideways from "down" in radians, positive towards the front. Fan legs out so they find footing on walls and ledges</summary>
        public float ProbeSpread;
        /// <summary>1 = near side (full colour), below 1 = far side, drawn darker</summary>
        public float Shade = 1f;
        public float Width = 5f;
        /// <summary>Width at the knee / at the tip, as a fraction of <see cref="Width"/></summary>
        public float KneeTaper = 0.75f;
        public float TipTaper = 0.35f;
        /// <summary>Disabled legs are neither updated nor drawn (a severed or not-yet-grown leg)</summary>
        public bool Enabled = true;
        /// <summary>Per-leg colour override, null = the rig's</summary>
        public Color? Chitin, Highlight;
        /// <summary>Hand-drawn art for a segment. null = the procedural tube</summary>
        public LegSprite CoxaSprite, UpperSprite, LowerSprite;
        public object Tag;

        // ---- state ----
        /// <summary>Where the leg attaches to the body</summary>
        public Vector2 Hip { get; private set; }
        /// <summary>End of the coxa and root of the IK limb (same as <see cref="Hip"/> when <see cref="Coxa"/> is 0)</summary>
        public Vector2 Shoulder { get; private set; }
        public Vector2 Knee { get; private set; }
        /// <summary>Where the foot is meant to be in the world</summary>
        public Vector2 Foot { get; private set; }
        /// <summary>Where the limb really ends: <see cref="Foot"/> clamped to the leg's reach, so bones never stretch. Draw to this</summary>
        public Vector2 Tip { get; private set; }
        /// <summary>Normal of the surface the foot last found (away from the surface)</summary>
        public Vector2 SurfaceNormal { get; private set; } = new Vector2(0f, -1f);
        /// <summary>True while the leg has a surface under it (feet down or mid-step), false when dangling or pinned</summary>
        public bool HasSurface { get; private set; }
        /// <summary>0..1 through the current step, -1 when not stepping</summary>
        public float StepProgress => stepT;
        public bool Stepping => stepT >= 0f;
        /// <summary>The foot is on the ground and not moving (run cycle: in the stance half)</summary>
        public bool Planted => !pinned && Enabled && (running ? runStance : grounded && stepT < 0f);
        /// <summary>Distance shoulder to foot over full reach: 1 = fully straight, above 1 = stretched (about to step)</summary>
        public float Extension { get; private set; }
        /// <summary>Set for the one tick a foot touches down</summary>
        public bool JustPlanted { get; private set; }
        /// <summary>Set for the one tick a foot leaves the ground</summary>
        public bool JustLifted { get; private set; }
        public bool IsPinned => pinned;
        /// <summary>Extra offset (0..1 of a stride) added to this leg's run cycle, on top of the share per <see cref="Group"/></summary>
        public float RunPhaseOffset;

        private const int GroundProbeInterval = 3; // ticks between tile scans for a planted foot
        private const int CoyoteTicks = 8; // how long a missing ground probe is tolerated before the leg dangles
        private const float DangleOmega = 12f;
        private static readonly float DangleDecay = LimbSolvers.DecayFor(DangleOmega);

        private Vector2 stepFrom;
        private float stepT = -1f;
        private Vector2 footVel;
        private Vector2 ideal;
        private int probeTimer;
        private int noGroundTicks;
        private bool grounded;
        private bool initialized;
        private bool running;
        private bool runStance;
        private float runBlend;
        private Vector2 runEntryFoot;
        private float runDepth;
        private bool pinned;
        private Vector2 pin;
        private float pinOmega = 20f;
        private bool prevPlanted;
        private bool suppressEvents = true;

        internal void Reset()
        {
            initialized = false;
            grounded = false;
            stepT = -1f;
            footVel = Vector2.Zero;
            probeTimer = 0;
            noGroundTicks = 0;
            running = false;
            runStance = false;
            runBlend = 0f;
            HasSurface = false;
            suppressEvents = true;
        }

        /// <summary>
        /// Force the foot to a world position (a stomp, a grab, a wall slam) instead of walking. It springs there; <paramref name="omega"/> ~ 12 is lazy, ~30 is a slam
        /// <br/>Call every tick you want it held, or once and call <see cref="Unpin"/> later. The leg still stays within reach, so the limb never stretches
        /// </summary>
        public void Pin(Vector2 world, float omega = 20f)
        {
            pinned = true;
            pin = world;
            pinOmega = omega;
        }

        /// <summary>Back to walking: the foot steps to the nearest spot from wherever it is</summary>
        public void Unpin()
        {
            if (!pinned)
                return;
            pinned = false;
            grounded = false;
            stepT = -1f;
        }

        internal void SetRunning(bool value)
        {
            if (value == running)
                return;

            running = value;
            if (value)
            {
                runBlend = 0f;
                runEntryFoot = Foot;
                stepT = -1f;
            }
            else
            {
                // back to walking: the walk logic re-plants the foot with a normal step
                grounded = false;
                runStance = false;
            }
        }

        internal void RefreshEvents()
        {
            bool now = Planted;
            if (suppressEvents)
            {
                suppressEvents = false;
                JustPlanted = JustLifted = false;
            }
            else
            {
                JustPlanted = now && !prevPlanted;
                JustLifted = !now && prevPlanted;
            }
            prevPlanted = now;
        }

        private void ComputeAnchors(in BodyFrame f, out float upper, out float lower)
        {
            upper = Upper * f.Scale;
            lower = Lower * f.Scale;
            Hip = f.Center + f.Right * (LocalHip.X * f.Facing * f.Scale) + f.Down * (LocalHip.Y * f.Scale);

            if (Coxa > 0f)
            {
                float c = MathF.Cos(CoxaAngle), s = MathF.Sin(CoxaAngle);
                Shoulder = Hip + (f.Right * (c * f.Facing) + f.Down * s) * (Coxa * f.Scale);
            }
            else
            {
                Shoulder = Hip;
            }
        }

        private Vector2 ProbeDir(in BodyFrame f)
        {
            if (ProbeSpread == 0f)
                return f.Down;

            Vector2 fwd = f.Right * f.Facing;
            return f.Down * MathF.Cos(ProbeSpread) + fwd * MathF.Sin(ProbeSpread);
        }

        private static bool Cast(SurfaceProbe probe, Vector2 origin, Vector2 dir, float max, out Vector2 hit, out Vector2 normal)
            => (probe ?? LegRig.DefaultProbe)(origin, dir, max, out hit, out normal);

        /// <summary>
        /// Run gait for speeds a planted-foot gait can't keep up with: the foot cycles under the hip, sliding back along the ground
        /// at roughly body speed, then swinging forward with a lift. <paramref name="cycle"/> is the rig's shared run phase
        /// </summary>
        internal void UpdateRun(in BodyFrame f, float cycle, int groupCount, int phase, float stride, SurfaceProbe probe)
        {
            ComputeAnchors(f, out float upper, out float lower);
            Vector2 root = Shoulder;
            float reach = upper + lower;

            if (!initialized)
            {
                Foot = root + f.Down * (reach * 0.8f);
                initialized = true;
                runEntryFoot = Foot;
            }

            if (runBlend <= 0f || --probeTimer <= 0)
            {
                probeTimer = GroundProbeInterval + (phase % GroundProbeInterval);
                bool found = Cast(probe, root, f.Down, reach * 1.2f, out Vector2 hit, out Vector2 normal);
                runDepth = found ? MathF.Max(Vector2.Dot(hit - root, f.Down), 0f) - 1f : reach * 0.7f;
                if (found)
                    SurfaceNormal = normal;
                HasSurface = found;
            }

            float moveDir = f.Along >= 0f ? 1f : -1f;
            float hipHeight = MathF.Max(runDepth, 4f);
            // longest horizontal swing the leg can reach from this hip height without straightening
            float maxHalf = MathF.Sqrt(MathF.Max(reach * reach * 0.9f - hipHeight * hipHeight, 0f));
            float half = MathF.Min(MathF.Abs(f.Along) * stride, maxHalf);

            float c = cycle + Group / (float)Math.Max(groupCount, 2) + RunPhaseOffset;
            c -= MathF.Floor(c);

            float x;
            float lift = 0f;
            runStance = c < 0.5f;
            if (runStance)
            {
                // stance: foot slides back along the ground
                x = half * (1f - 4f * c);
            }
            else
            {
                // swing: foot comes forward, lifted
                float u = (c - 0.5f) * 2f;
                x = -half + 2f * half * LimbSolvers.SmoothStep(u);
                lift = MathF.Sin(u * MathHelper.Pi) * StepLift * f.Scale * 1.3f;
            }

            Vector2 target = root + f.Right * (moveDir * x) + f.Down * (runDepth - lift);
            runBlend = MathF.Min(runBlend + 1f / 6f, 1f);
            Foot = runBlend < 1f ? Vector2.Lerp(runEntryFoot, target, LimbSolvers.SmoothStep(runBlend)) : target;

            Solve(f, upper, lower);
        }

        /// <summary>
        /// Advance one tick. <paramref name="phase"/> staggers the ground probes so legs don't all scan the same tick
        /// </summary>
        internal void Update(in BodyFrame f, bool blocked, int phase, SurfaceProbe probe)
        {
            ComputeAnchors(f, out float upper, out float lower);
            Vector2 root = Shoulder;
            float reach = upper + lower;

            if (pinned)
            {
                if (!initialized)
                {
                    Foot = pin;
                    initialized = true;
                    footVel = Vector2.Zero;
                }
                Vector2 p = Foot;
                LimbSolvers.Spring(ref p, ref footVel, pin, pinOmega);
                Foot = p;
                stepT = -1f;
                grounded = false;
                HasSurface = false;
                Solve(f, upper, lower);
                return;
            }

            // tile scan is the only expensive part: a planted foot only needs it every few ticks, a stepping foot every tick
            bool needProbe = !initialized || Stepping || !grounded || --probeTimer <= 0;
            bool foundGround = true;
            if (needProbe)
            {
                probeTimer = GroundProbeInterval + (phase % GroundProbeInterval);

                // look a little ahead so feet land where the body is going, not where it was
                float lead = MathHelper.Clamp(f.Along * 4f, -40f, 40f);
                Vector2 origin = root + f.Right * (RestForward * f.Facing * f.Scale + lead);
                foundGround = Cast(probe, origin, ProbeDir(f), reach * 1.2f, out Vector2 hit, out Vector2 normal);
                if (foundGround)
                {
                    Vector2 spot = hit + normal;
                    // never put the ideal foot spot behind the hip (hip buried in a tile)
                    float depth = Vector2.Dot(spot - root, f.Down);
                    if (depth < 0f)
                        spot -= f.Down * depth;
                    ideal = spot;
                    SurfaceNormal = normal;
                    noGroundTicks = 0;
                }
                else
                {
                    noGroundTicks += GroundProbeInterval;
                }
            }

            // uneven terrain makes single probes flicker; only dangle once the ground has been gone for a while
            bool haveGround = foundGround || (grounded && noGroundTicks < CoyoteTicks);
            bool useGround = f.Grounded && haveGround;
            HasSurface = useGround;

            if (!useGround)
            {
                stepT = -1f;
                grounded = false;
                Vector2 dangle = root + f.Down * (reach * 0.55f) - f.Right * (f.Along * 2.5f) - f.Down * (Vector2.Dot(f.Velocity, f.Down) * 1.5f);
                if (!initialized)
                {
                    Foot = dangle;
                    footVel = Vector2.Zero;
                    initialized = true;
                }
                Vector2 d = Foot;
                LimbSolvers.SpringCached(ref d, ref footVel, dangle, DangleOmega, DangleDecay);
                Foot = d;
                Solve(f, upper, lower);
                return;
            }

            if (!initialized)
            {
                Foot = ideal;
                initialized = true;
            }

            if (!grounded)
            {
                // just touched down (or first frame): plant by stepping straight to the spot
                BeginStep();
            }
            grounded = true;

            if (Stepping)
            {
                // speed the step up when the body is moving fast, otherwise the foot lags behind
                float ticks = MathHelper.Clamp(StepTicks - MathF.Abs(f.Along) * 0.5f, MathF.Min(MinStepTicks, StepTicks), StepTicks);
                stepT += 1f / ticks;
                if (stepT >= 1f)
                {
                    Foot = ideal;
                    stepT = -1f;
                }
                else
                {
                    float e = StepEase.Eval(stepT);
                    float lift = MathF.Sin(MathF.Pow(stepT, LiftSkew) * MathHelper.Pi) * StepLift * f.Scale;
                    Foot = Vector2.Lerp(stepFrom, ideal, e) - f.Down * lift;
                }
            }
            else
            {
                float away = Vector2.Distance(Foot, ideal);
                // stepping before the limb is fully straight keeps the planted foot from being dragged
                bool overstretched = Vector2.DistanceSquared(Foot, root) > reach * reach * 0.94f;
                bool wantsStep = away > StepThreshold * f.Scale;

                if ((wantsStep && !blocked) || overstretched)
                    BeginStep();
            }

            Solve(f, upper, lower);
        }

        private void BeginStep()
        {
            stepFrom = Foot;
            stepT = 0f;
        }

        private void Solve(in BodyFrame f, float upper, float lower)
        {
            // local bend (+1 = forward) -> left-normal sign. For a leg pointing down the left normal is world -Right,
            // so forward (Right * facing) needs sign = -facing. Relative to the body frame, so it holds on walls and ceilings too
            Knee = LimbSolvers.TwoBone(Shoulder, Foot, upper, lower, -f.Facing * Bend, out Vector2 tip);
            Tip = tip;
            Extension = Vector2.Distance(Shoulder, Foot) / MathF.Max(upper + lower, 0.001f);
        }
    }

    public enum GaitPattern
    {
        /// <summary>Diagonal pairs (tetrapod): the classic walk, always at least half the legs planted</summary>
        Alternating,
        /// <summary>One leg after the next, front to back (centipede, slow stalking). Lets two groups step at once</summary>
        Wave
    }

    /// <summary>
    /// A set of <see cref="ProceduralLeg"/>s attached to one body, plus drawing. Purely visual: no gameplay state, so every client can run it
    /// independently from the NPC's synced position and velocity
    /// <para>
    /// Hook-up for an NPC: keep a <c>LegRig</c> field, build it once (<see cref="AddLeg"/>, <see cref="AutoGait"/>), call <see cref="Update"/> from <c>PostAI</c>
    /// and call <see cref="Draw"/> in <c>PreDraw</c> before drawing the body. Add <see cref="BodyOffset"/> to the body's draw position for weight and bounce
    /// </para>
    /// </summary>
    public sealed class LegRig
    {
        public readonly List<ProceduralLeg> Legs = new();
        public Color Chitin = new Color(122, 62, 40);
        public Color Highlight = new Color(214, 150, 92);

        /// <summary>Legs are neither updated nor drawn when the body is farther than this from the screen (pixels)</summary>
        public float CullMargin = 400f;
        /// <summary>Keep updating off-screen (and on a server) so foot positions stay valid for gameplay. Drawing still needs a client</summary>
        public bool AlwaysUpdate;
        /// <summary>Horizontal speed (px/tick) above which legs switch from planting feet to the run cycle</summary>
        public float RunSpeed = 7.5f;
        /// <summary>Speed below which the run cycle hands back to planting (lower than <see cref="RunSpeed"/> so it doesn't flicker)</summary>
        public float WalkSpeed = 5.5f;
        /// <summary>Ticks per full run cycle</summary>
        public float RunPeriod = 10f;
        /// <summary>Half-stride of the run gait per pixel/tick of body speed (a 10-tick cycle at 2.5 keeps stance feet roughly planted)</summary>
        public float RunStride = 2.5f;
        /// <summary>How many different groups may be mid-step at once. 1 = classic alternating gait</summary>
        public int MaxSteppingGroups = 1;
        /// <summary>A jump of more than this many px in one tick snaps the feet to the new spot instead of stretching legs across the arena</summary>
        public float TeleportDistance = 200f;
        /// <summary>Custom surface finder, null = tiles (<see cref="FindSurface"/>)</summary>
        public SurfaceProbe Probe;
        /// <summary>Draw legs yourself, per leg. Return true when handled</summary>
        public LegDrawer CustomDraw;
        /// <summary>Called the tick a foot touches down: dust, footstep sounds, screen shake. Use <c>leg.Tip</c> for the position</summary>
        public Action<LegRig, ProceduralLeg> OnPlant;
        /// <summary>Called the tick a foot leaves the ground</summary>
        public Action<LegRig, ProceduralLeg> OnLift;

        // ---- look ----
        /// <summary>0 = few big slices (cheap, slightly faceted), 1 = default, 2 = smooth for hero enemies and bosses</summary>
        public int Quality = 1;
        /// <summary>Darkness of the soft contact shadow under planted feet (0 = off)</summary>
        public float FootShadow = 0.28f;
        /// <summary>Sample the world light at each leg instead of using one colour for the whole rig (costs a lighting lookup per leg)</summary>
        public bool SampleLight;
        /// <summary>Fades the whole rig (spawn-in, death fade)</summary>
        public float Opacity = 1f;
        /// <summary>Draw the thin specular line on the lit side of each tube</summary>
        public bool Highlights = true;

        // ---- body motion ----
        /// <summary>Pixels the body bobs: it rides up while running and dips when feet are in the air. 0 = off. Apply <see cref="BodyOffset"/> when drawing the body</summary>
        public float BobAmount;
        /// <summary>Smoothed offset to add to the body's draw position (zero unless <see cref="BobAmount"/> is set)</summary>
        public Vector2 BodyOffset { get; private set; }

        public bool Culled => culled;

        private Vector2 lastCenter;
        private bool hasLast;
        private bool culled;
        private bool running;
        private float runCycle;
        private int lastFacing = 1;
        private Vector2 bob, bobVel;
        private static readonly float BobDecay = LimbSolvers.DecayFor(14f);

        /// <summary>The default tile-based surface finder, used when <see cref="Probe"/> is null</summary>
        internal static readonly SurfaceProbe DefaultProbe = FindSurface;

        public void Reset()
        {
            hasLast = false;
            running = false;
            bob = bobVel = Vector2.Zero;
            foreach (ProceduralLeg leg in Legs)
                leg.Reset();
        }

        /// <summary>
        /// Add a leg and return it for further tweaking. <paramref name="far"/> legs are drawn behind and darker
        /// </summary>
        public ProceduralLeg AddLeg(Vector2 localHip, float upper, float lower, int group = 0, bool far = false, float width = 5f)
        {
            ProceduralLeg leg = new ProceduralLeg
            {
                LocalHip = localHip,
                Upper = upper,
                Lower = lower,
                Group = group,
                Shade = far ? 0.6f : 1f,
                Width = width
            };
            Legs.Add(leg);
            return leg;
        }

        /// <summary>Run <paramref name="action"/> on every leg: <c>rig.Configure(l =&gt; l.StepLift = 20f)</c></summary>
        public void Configure(Action<ProceduralLeg> action)
        {
            foreach (ProceduralLeg leg in Legs)
                action(leg);
        }

        /// <summary>
        /// Assign step groups from the leg layout: near and far legs are paired by their hip X so diagonal legs share a group.
        /// Run once after adding all legs (far = <see cref="ProceduralLeg.Shade"/> below 1)
        /// </summary>
        public void AutoGait(GaitPattern pattern = GaitPattern.Alternating)
        {
            for (int side = 0; side < 2; side++)
            {
                List<ProceduralLeg> list = new List<ProceduralLeg>();
                foreach (ProceduralLeg leg in Legs)
                {
                    if ((leg.Shade < 1f ? 1 : 0) == side)
                        list.Add(leg);
                }
                // front to back
                list.Sort((a, b) => b.LocalHip.X.CompareTo(a.LocalHip.X));

                for (int rank = 0; rank < list.Count; rank++)
                {
                    list[rank].Group = pattern == GaitPattern.Wave
                        ? rank + side * Math.Max(1, list.Count / 2)
                        : (rank + side) & 1;
                }
            }
            MaxSteppingGroups = pattern == GaitPattern.Wave ? 2 : 1;
        }

        public void Update(Vector2 center, Vector2 velocity, int facing, float scale, bool grounded)
            => Update(new RigBody(center, velocity, facing, scale, grounded));

        public void Update(in RigBody body)
        {
            if (Legs.Count == 0)
                return;

            int facing = body.Facing == 0 ? 1 : body.Facing;

            // off-screen: skip the work and re-snap when coming back, there is nobody to see the legs
            culled = !AlwaysUpdate && !OnScreen(body.Center, CullMargin);
            if (culled)
            {
                hasLast = false;
                return;
            }

            float cr = MathF.Cos(body.Rotation), sr = MathF.Sin(body.Rotation);
            BodyFrame f = new BodyFrame
            {
                Center = body.Center,
                Velocity = body.Velocity,
                Right = new Vector2(cr, sr),
                Down = new Vector2(-sr, cr),
                Facing = facing,
                Scale = body.Scale,
                Grounded = body.Grounded
            };
            f.Along = Vector2.Dot(f.Velocity, f.Right);
            lastFacing = facing;

            // teleports (burrow emerge, dive landing, cutscenes) or coming back from off-screen: snap instead of stretching legs across the arena
            if (!hasLast || Vector2.DistanceSquared(body.Center, lastCenter) > TeleportDistance * TeleportDistance)
            {
                running = false;
                bob = bobVel = Vector2.Zero;
                foreach (ProceduralLeg leg in Legs)
                    leg.Reset();
            }
            lastCenter = body.Center;
            hasLast = true;

            // fast enough that feet can't keep up: switch the whole rig to the run cycle (with hysteresis)
            float speed = MathF.Abs(f.Along);
            bool wantRun = body.Grounded && speed > (running ? WalkSpeed : RunSpeed);
            if (wantRun != running)
            {
                running = wantRun;
                foreach (ProceduralLeg leg in Legs)
                    leg.SetRunning(running);
            }

            int groups = 2;
            int stepping = 0;
            int active = 0;
            for (int i = 0; i < Legs.Count; i++)
            {
                if (Legs[i].Enabled)
                    groups = Math.Max(groups, Legs[i].Group + 1);
            }

            if (running)
            {
                runCycle += 1f / MathF.Max(RunPeriod, 2f);
                runCycle -= MathF.Floor(runCycle);
                for (int i = 0; i < Legs.Count; i++)
                {
                    if (!Legs[i].Enabled)
                        continue;
                    Legs[i].UpdateRun(f, runCycle, groups, i, RunStride, Probe);
                    active++;
                }
            }
            else
            {
                // which groups are mid-step, as a bitmask
                uint mask = 0;
                foreach (ProceduralLeg leg in Legs)
                {
                    if (leg.Enabled && leg.Stepping)
                        mask |= 1u << (leg.Group & 31);
                }

                for (int i = 0; i < Legs.Count; i++)
                {
                    ProceduralLeg leg = Legs[i];
                    if (!leg.Enabled)
                        continue;

                    uint own = 1u << (leg.Group & 31);
                    bool blocked = System.Numerics.BitOperations.PopCount(mask & ~own) >= MaxSteppingGroups;
                    leg.Update(f, blocked, i, Probe);
                    active++;
                    if (leg.Stepping)
                    {
                        // legs updated later this tick must see a step that just started, or two groups can begin at once
                        mask |= own;
                        stepping++;
                    }
                }
            }

            for (int i = 0; i < Legs.Count; i++)
            {
                ProceduralLeg leg = Legs[i];
                if (!leg.Enabled)
                    continue;

                leg.RefreshEvents();
                if (leg.JustPlanted)
                    OnPlant?.Invoke(this, leg);
                if (leg.JustLifted)
                    OnLift?.Invoke(this, leg);
            }

            UpdateBob(f, active, stepping);
        }

        private void UpdateBob(in BodyFrame f, int active, int stepping)
        {
            if (BobAmount == 0f)
            {
                BodyOffset = Vector2.Zero;
                return;
            }

            // Down is towards the surface, so "up" is negative. Running: highest mid-stride, lowest as a foot lands. Walking: sinks while feet are in the air
            float target;
            if (running)
                target = -MathF.Abs(MathF.Sin(runCycle * MathHelper.TwoPi)) * BobAmount * f.Scale;
            else
                target = active > 0 ? stepping / (float)active * BobAmount * 0.5f * f.Scale : 0f;

            LimbSolvers.SpringCached(ref bob, ref bobVel, new Vector2(target, 0f), 14f, BobDecay);
            BodyOffset = f.Down * bob.X;
        }

        /// <summary>
        /// Draw far-side legs first (darker), then near-side. Call before drawing the body to put legs behind it
        /// </summary>
        public void Draw(SpriteBatch sb, Vector2 screenPos, Color light, float scale)
        {
            if (culled || !hasLast)
                return;

            Texture2D pixel = TextureAssets.MagicPixel.Value;

            if (FootShadow > 0f)
            {
                for (int i = 0; i < Legs.Count; i++)
                    DrawFootShadow(sb, Legs[i], screenPos, light, scale);
            }

            for (int i = 0; i < Legs.Count; i++)
            {
                if (Legs[i].Shade < 1f)
                    DrawLeg(sb, pixel, Legs[i], screenPos, light, scale);
            }
            for (int i = 0; i < Legs.Count; i++)
            {
                if (Legs[i].Shade >= 1f)
                    DrawLeg(sb, pixel, Legs[i], screenPos, light, scale);
            }
        }

        private void DrawFootShadow(SpriteBatch sb, ProceduralLeg leg, Vector2 screenPos, Color light, float scale)
        {
            if (!leg.Enabled || !leg.Planted || !leg.HasSurface)
                return;

            float bright = (light.R + light.G + light.B) / 765f;
            float a = FootShadow * bright * Opacity;
            if (a <= 0.01f)
                return;

            Vector2 n = leg.SurfaceNormal;
            float w = leg.Width * scale;
            float rot = MathF.Atan2(n.Y, n.X) + MathHelper.PiOver2; // along the surface
            sb.Draw(RigArt.Disc, leg.Tip + n * 0.5f - screenPos, null, Color.Black * a, rot,
                new Vector2(RigArt.DiscSize * 0.5f), new Vector2(w * 2.1f, w * 0.6f) / RigArt.DiscSize, SpriteEffects.None, 0f);
        }

        private void DrawLeg(SpriteBatch sb, Texture2D pixel, ProceduralLeg leg, Vector2 screenPos, Color light, float scale)
        {
            if (!leg.Enabled)
                return;

            Color lc = light;
            if (SampleLight)
                lc = Lighting.GetColor((int)(leg.Knee.X / 16f), (int)(leg.Knee.Y / 16f));
            lc = RigUtil.Modulate(lc, Color.White, Opacity);

            if (CustomDraw != null && CustomDraw(sb, this, leg, screenPos, lc, scale))
                return;

            Color baseCol = RigUtil.Modulate(Color.Lerp(Color.Black, leg.Chitin ?? Chitin, leg.Shade), lc);
            Color hiCol = RigUtil.Modulate(Color.Lerp(Color.Black, leg.Highlight ?? Highlight, leg.Shade), lc);
            bool highlight = Highlights && leg.Shade >= 1f; // far legs are in shadow anyway, skip the highlight pass

            float w = leg.Width * scale;
            float wKnee = w * leg.KneeTaper;
            float wTip = w * leg.TipTaper;
            bool coxa = leg.Coxa > 0f;
            float wShoulder = coxa ? w * 0.9f : w;

            Vector2 hip = leg.Hip - screenPos;
            Vector2 shoulder = leg.Shoulder - screenPos;
            Vector2 knee = leg.Knee - screenPos;
            Vector2 tip = leg.Tip - screenPos;

            if (coxa)
                DrawSegment(sb, pixel, leg.CoxaSprite, hip, shoulder, w, wShoulder, baseCol, hiCol, highlight, scale);
            DrawSegment(sb, pixel, leg.UpperSprite, shoulder, knee, wShoulder, wKnee, baseCol, hiCol, highlight, scale);
            DrawSegment(sb, pixel, leg.LowerSprite, knee, tip, wKnee, wTip, baseCol, hiCol, highlight, scale);

            // rounded joints hide the seams between tubes
            if (leg.UpperSprite == null || leg.LowerSprite == null)
                DrawCap(sb, knee, wKnee * 1.15f, baseCol);
            if (coxa && (leg.CoxaSprite == null || leg.UpperSprite == null))
                DrawCap(sb, shoulder, wShoulder * 1.05f, baseCol);
            if (leg.CoxaSprite == null && coxa)
                DrawCap(sb, hip, w * 1.0f, baseCol);
            else if (!coxa && leg.UpperSprite == null)
                DrawCap(sb, hip, w * 1.0f, baseCol);
            if (leg.LowerSprite == null)
                DrawCap(sb, tip, MathF.Max(wTip * 1.1f, 1.5f), baseCol);
        }

        private void DrawSegment(SpriteBatch sb, Texture2D pixel, LegSprite sprite, Vector2 a, Vector2 b, float wA, float wB, Color col, Color hi, bool highlight, float scale)
        {
            if (sprite != null && sprite.Texture != null)
                DrawSprite(sb, sprite, a, b, col, scale);
            else
                DrawTube(sb, pixel, a, b, wA, wB, col, hi, highlight, Quality);
        }

        private void DrawSprite(SpriteBatch sb, LegSprite s, Vector2 a, Vector2 b, Color col, float scale)
        {
            Vector2 d = b - a;
            float len = d.Length();
            if (len < 0.5f)
                return;

            float rot = MathF.Atan2(d.Y, d.X);
            float span = MathF.Max(s.Source.Width - s.Pivot.X, 1f);
            Vector2 sc = s.Stretch ? new Vector2(len / span, scale) : new Vector2(scale);
            // keep the art upright for a mirrored body; the origin is applied before the flip, so mirror the pivot with it
            bool flip = lastFacing < 0;
            Vector2 pivot = flip ? new Vector2(s.Pivot.X, s.Source.Height - s.Pivot.Y) : s.Pivot;
            sb.Draw(s.Texture, a, s.Source, col, rot, pivot, sc, flip ? SpriteEffects.FlipVertically : SpriteEffects.None, 0f);
        }

        private static void DrawCap(SpriteBatch sb, Vector2 p, float diameter, Color col)
        {
            sb.Draw(RigArt.Disc, p, null, col, 0f, new Vector2(RigArt.DiscSize * 0.5f), diameter / RigArt.DiscSize, SpriteEffects.None, 0f);
        }

        /// <summary>
        /// A tapered, shaded tube: slices of a 1px cross-section strip (bright core, dark rim) rotated along the bone, so it reads as a round limb instead of a flat bar.
        /// The specular line rides the lit (upper) side and fades out as the bone turns vertical, so it never pops between sides
        /// </summary>
        internal static void DrawTube(SpriteBatch sb, Texture2D pixel, Vector2 a, Vector2 b, float widthA, float widthB, Color col, Color hi, bool highlight, int quality)
        {
            Vector2 d = b - a;
            float len = d.Length();
            if (len < 0.5f)
                return;

            float rot = MathF.Atan2(d.Y, d.X);
            float seg = quality <= 0 ? 24f : quality == 1 ? 10f : 5f;
            int slices = (int)MathHelper.Clamp(MathF.Ceiling(len / seg), 1f, 14f);
            float sliceLen = len / slices;

            Texture2D strip = RigArt.Strip;
            Vector2 origin = new Vector2(0f, RigArt.StripHeight * 0.5f);
            for (int i = 0; i < slices; i++)
            {
                float t = (i + 0.5f) / slices;
                float w = MathF.Max(MathHelper.Lerp(widthA, widthB, t), 1f);
                Vector2 start = a + d * (i / (float)slices);
                // +0.75 length overlap hides seams between slices
                sb.Draw(strip, start, null, col, rot, origin, new Vector2(sliceLen + 0.75f, w / RigArt.StripHeight), SpriteEffects.None, 0f);
            }

            if (highlight)
            {
                Vector2 n = new Vector2(-d.Y, d.X) / len;
                float up = MathHelper.Clamp(-n.Y * 3f, -1f, 1f); // +1 when the left normal points up the screen
                float side = up * (widthA + widthB) * 0.5f * 0.2f;
                Vector2 start = a + d * 0.1f + n * side;
                sb.Draw(pixel, start, new Rectangle(0, 0, 1, 1), hi * (0.35f + 0.65f * MathF.Abs(up)), rot, new Vector2(0f, 0.5f),
                    new Vector2(len * 0.8f, MathF.Max(MathF.Min(widthA, widthB) * 0.3f, 1f)), SpriteEffects.None, 0f);
            }
        }

        // kept for older call sites: same signature as before, now draws the shaded tube
        internal static void DrawBone(SpriteBatch sb, Texture2D pixel, Vector2 a, Vector2 b, float widthA, float widthB, Color col, Color hi, bool highlight)
            => DrawTube(sb, pixel, a, b, widthA, widthB, col, hi, highlight, 1);

        /// <summary>Dispose the shared leg textures. Call from your ModSystem.Unload</summary>
        public static void Unload() => RigArt.Unload();

        internal static bool OnScreen(Vector2 worldPos, float margin)
        {
            Vector2 p = worldPos - Main.screenPosition;
            return p.X > -margin && p.Y > -margin && p.X < Main.screenWidth + margin && p.Y < Main.screenHeight + margin;
        }

        /// <summary>
        /// Scan straight down from <paramref name="from"/> for the top of the first solid tile within <paramref name="maxDrop"/> pixels
        /// </summary>
        public static bool FindGround(Vector2 from, float maxDrop, out float groundY)
        {
            int x = (int)(from.X / 16f);
            int startY = (int)(from.Y / 16f);
            int steps = (int)(maxDrop / 16f) + 1;

            if (!WorldGen.InWorld(x, startY, 10))
            {
                groundY = from.Y;
                return false;
            }

            for (int i = 0; i <= steps; i++)
            {
                int y = startY + i;
                if (y >= Main.maxTilesY - 10)
                    break;

                Tile tile = Main.tile[x, y];
                if (tile.HasUnactuatedTile && (Main.tileSolid[tile.TileType] || Main.tileSolidTop[tile.TileType]))
                {
                    groundY = y * 16f;
                    return true;
                }
            }

            groundY = from.Y;
            return false;
        }

        /// <summary>
        /// Cast a ray through the tile grid: the first solid tile along <paramref name="direction"/> (unit) within <paramref name="maxDistance"/>, with its surface normal.
        /// Straight down takes the fast column scan (<see cref="FindGround"/>); any other direction (walls, ceilings, slopes) marches the ray and refines the hit.
        /// One-way platforms only count when the ray goes downward
        /// </summary>
        public static bool FindSurface(Vector2 origin, Vector2 direction, float maxDistance, out Vector2 hit, out Vector2 normal)
        {
            hit = origin;
            normal = -direction;

            if (MathF.Abs(direction.X) < 0.0001f && direction.Y > 0.9999f)
            {
                if (!FindGround(origin, maxDistance, out float gy))
                    return false;
                hit = new Vector2(origin.X, gy);
                normal = new Vector2(0f, -1f);
                return true;
            }

            bool platforms = direction.Y > 0.5f;
            const float Step = 6f;
            for (float d = 0f; d <= maxDistance + Step; d += Step)
            {
                float dd = MathF.Min(d, maxDistance);
                if (!SolidAt(origin + direction * dd, platforms))
                {
                    if (dd >= maxDistance)
                        break;
                    continue;
                }

                float lo = MathF.Max(dd - Step, 0f), hi = dd;
                for (int i = 0; i < 4; i++)
                {
                    float mid = (lo + hi) * 0.5f;
                    if (SolidAt(origin + direction * mid, platforms))
                        hi = mid;
                    else
                        lo = mid;
                }

                hit = origin + direction * hi;
                normal = EstimateNormal(hit, direction, platforms);
                return true;
            }
            return false;
        }

        private static bool SolidAt(Vector2 p, bool platforms)
        {
            int tx = (int)MathF.Floor(p.X / 16f);
            int ty = (int)MathF.Floor(p.Y / 16f);
            if (!WorldGen.InWorld(tx, ty, 10))
                return false;

            Tile tile = Main.tile[tx, ty];
            if (!tile.HasUnactuatedTile)
                return false;

            ushort type = tile.TileType;
            return platforms ? (Main.tileSolid[type] || Main.tileSolidTop[type]) : (Main.tileSolid[type] && !Main.tileSolidTop[type]);
        }

        // points from the solid mass out into the open: sum of the directions to empty neighbour tiles
        private static Vector2 EstimateNormal(Vector2 hit, Vector2 rayDir, bool platforms)
        {
            int tx = (int)MathF.Floor(hit.X / 16f);
            int ty = (int)MathF.Floor(hit.Y / 16f);
            Vector2 n = Vector2.Zero;

            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0)
                        continue;
                    if (!SolidAt(new Vector2((tx + dx) * 16f + 8f, (ty + dy) * 16f + 8f), platforms))
                        n += new Vector2(dx, dy) * (dx != 0 && dy != 0 ? 0.7071f : 1f);
                }
            }

            if (n.LengthSquared() < 0.01f)
                return -rayDir;
            n.Normalize();
            return n;
        }
    }
}
