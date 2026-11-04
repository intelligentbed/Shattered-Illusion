using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace ShatteredIllusion.Core.Rigging
{
    /// <summary>
    /// Offset from a bone's rest pose. Position in model px, rotation in radians
    /// <br/><see cref="Stretch"/> is added to the bone's sprite scale (0 = unchanged, 0.2 = 20% bigger), for squash and stretch
    /// </summary>
    public struct BonePose
    {
        public Vector2 Pos;
        public float Rot;
        public Vector2 Stretch;
    }

    /// <summary>
    /// Custom draw hook for one bone. Return true if you drew it yourself (the default sprite is then skipped)
    /// <br/><paramref name="light"/> already has the bone's <see cref="Bone.Tint"/> and <see cref="Bone.Opacity"/> applied
    /// </summary>
    public delegate bool BoneDrawer(Skeleton skeleton, Bone bone, SpriteBatch sb, Vector2 screenPos, Color light);

    /// <summary>
    /// One sprite piece plus its joint. Art is drawn pointing along the bone's own +X, so bake any "art points down" into <see cref="RestRot"/>
    /// <br/>A bone with no <see cref="Frames"/> is just a joint (handy for pivots, hitbox anchors, projectile muzzles)
    /// </summary>
    public sealed class Bone
    {
        public string Name;
        public int Parent = -1;
        /// <summary>Joint position in the parent's frame (unflipped, unscaled)</summary>
        public Vector2 RestPos;
        public float RestRot;
        /// <summary>Joint to tip along the bone's +X, only the IK and <see cref="Skeleton.TipPos"/> read this</summary>
        public float Length;
        /// <summary>Draw order, higher is in front. Independent of the parent/child order, and can be changed at runtime (a hand going behind the body mid-clip)</summary>
        public int Z;
        /// <summary>Hand-drawn variants of this piece in the atlas, a clip key picks which one shows</summary>
        public Rectangle[] Frames;
        /// <summary>Joint position inside the source rect, in texture px (unflipped; the skeleton mirrors it for you when facing left)</summary>
        public Vector2 Pivot;
        /// <summary>Use another texture than the skeleton's atlas for this bone (glow layers, separate boss phases)</summary>
        public Texture2D Atlas;
        /// <summary>Persistent, unlike the code layer. Hidden bones still solve, so children and <see cref="Skeleton.PointOn"/> keep working</summary>
        public bool Visible = true;
        /// <summary>Replace how this bone is drawn (glow, afterimages, shaders). Return true when handled</summary>
        public BoneDrawer CustomDraw;
        /// <summary>Anything you want to hang off the bone</summary>
        public object Tag;
        public int Frame { get; internal set; }

        // ---- code layer: added on top of the clip pose, cleared at the start of every Update, so set it from the code pass ----
        public float CodeRot;
        public Vector2 CodeOffset;
        /// <summary>Multiplies this bone's own sprite only (children are not scaled)</summary>
        public Vector2 CodeScale = Vector2.One;
        public Color Tint = Color.White;
        public float Opacity = 1f;

        /// <summary>World position and rotation (flip, scale and body rotation applied). Use these to spawn projectiles from a hand etc.</summary>
        public Vector2 Pos { get; internal set; }
        public float Rot { get; internal set; }

        internal Vector2 mPos;
        internal float mRot, sin, cos;

        internal Vector2 lag, lagVel;
        internal float lagOmega, lagDecay, lastDesired;
        internal bool lagInit;

        internal Vector2 lagP, lagPVel;
        internal float lagPOmega, lagPDecay;

        internal void ResetSprings()
        {
            lag = lagVel = lagP = lagPVel = Vector2.Zero;
            lagInit = false;
        }
    }

    /// <summary>
    /// Hand-posed keyframes. Every key is a full pose (unset bones sit at rest), the clip tweens between keys
    /// <br/>Sprite index is stepped, never blended, so hand-drawn swaps stay crisp. <c>hold</c> keys snap to the next key instead of tweening
    /// <br/>Events (<see cref="Event"/>) fire a callback at a clip time: footsteps, hit frames, projectile spawns
    /// </summary>
    public sealed class BoneClip
    {
        public string Name;
        public bool Loop;
        /// <summary>Tween rotations the short way round instead of straight numeric lerp. Leave off if a clip relies on spinning more than half a turn between keys</summary>
        public bool ShortestRotation;
        public float Total { get; private set; }

        internal readonly int bones;
        internal int count;
        internal float[] ticks = Array.Empty<float>();
        internal bool[] holds = Array.Empty<bool>();
        internal RigEase[] eases = Array.Empty<RigEase>();
        internal BonePose[] poses = Array.Empty<BonePose>();
        internal int[] sprites = Array.Empty<int>();

        internal int eventCount;
        internal float[] eventTicks = Array.Empty<float>();
        internal int[] eventIds = Array.Empty<int>();

        public BoneClip(int boneCount, bool loop = true)
        {
            bones = boneCount;
            Loop = loop;
        }

        /// <summary>
        /// Add a key lasting <paramref name="duration"/> ticks (the time it takes to tween into the next one). Returns its index
        /// </summary>
        public int Key(float duration, bool hold = false) => Key(duration, hold, RigEase.Smooth);

        /// <summary>Same, with the easing used for the tween out of this key</summary>
        public int Key(float duration, bool hold, RigEase ease)
        {
            int k = count++;
            Array.Resize(ref ticks, count);
            Array.Resize(ref holds, count);
            Array.Resize(ref eases, count);
            Array.Resize(ref poses, count * bones);
            Array.Resize(ref sprites, count * bones);
            ticks[k] = duration;
            holds[k] = hold;
            eases[k] = ease;
            Total += duration;
            return k;
        }

        public BoneClip Set(int key, int bone, Vector2 pos, float rot, int sprite = 0)
        {
            int i = key * bones + bone;
            poses[i].Pos = pos;
            poses[i].Rot = rot;
            sprites[i] = sprite;
            return this;
        }

        public BoneClip Set(int key, int bone, float rot, int sprite = 0) => Set(key, bone, Vector2.Zero, rot, sprite);

        /// <summary>Squash and stretch for one bone's sprite at a key. Call after <see cref="Set(int,int,Vector2,float,int)"/> (Set does not touch it)</summary>
        public BoneClip Stretch(int key, int bone, Vector2 stretch)
        {
            poses[key * bones + bone].Stretch = stretch;
            return this;
        }

        /// <summary>Fire <see cref="Skeleton.OnEvent"/> with <paramref name="id"/> when playback passes <paramref name="tick"/> (clip time, same units as key durations)</summary>
        public BoneClip Event(float tick, int id)
        {
            int at = eventCount++;
            Array.Resize(ref eventTicks, eventCount);
            Array.Resize(ref eventIds, eventCount);
            while (at > 0 && eventTicks[at - 1] > tick)
            {
                eventTicks[at] = eventTicks[at - 1];
                eventIds[at] = eventIds[at - 1];
                at--;
            }
            eventTicks[at] = tick;
            eventIds[at] = id;
            return this;
        }

        internal void Sample(float t, BonePose[] dst, int[] spr)
        {
            if (count == 0 || Total <= 0f)
            {
                Array.Clear(dst);
                Array.Clear(spr);
                return;
            }

            if (Loop)
                t -= MathF.Floor(t / Total) * Total;
            else
                t = MathF.Min(MathF.Max(t, 0f), Total);

            int k = 0;
            while (k < count - 1 && t >= ticks[k])
            {
                t -= ticks[k];
                k++;
            }

            int next = k + 1 < count ? k + 1 : (Loop ? 0 : k);
            float u = holds[k] || ticks[k] <= 0f ? 0f : eases[k].Eval(MathF.Min(t / ticks[k], 1f));

            for (int i = 0; i < bones; i++)
            {
                BonePose a = poses[k * bones + i];
                BonePose b = poses[next * bones + i];
                dst[i].Pos = Vector2.Lerp(a.Pos, b.Pos, u);
                dst[i].Rot = ShortestRotation ? LimbSolvers.LerpAngle(a.Rot, b.Rot, u) : MathHelper.Lerp(a.Rot, b.Rot, u);
                dst[i].Stretch = Vector2.Lerp(a.Stretch, b.Stretch, u);
                spr[i] = sprites[k * bones + i];
            }
        }

        internal void FireEvents(float from, float to, Action<int> fire)
        {
            if (eventCount == 0 || Total <= 0f || to <= from)
                return;

            if (Loop)
            {
                if (to - from >= Total)
                {
                    for (int i = 0; i < eventCount; i++)
                        fire(eventIds[i]);
                    return;
                }

                float a = from - MathF.Floor(from / Total) * Total;
                float b = a + (to - from);
                // [a, b) on an unrolled timeline, the second lap is the wrapped part
                for (int i = 0; i < eventCount; i++)
                {
                    float e = eventTicks[i];
                    if ((e >= a && e < b) || (e + Total >= a && e + Total < b))
                        fire(eventIds[i]);
                }
            }
            else
            {
                float end = MathF.Min(to, Total);
                for (int i = 0; i < eventCount; i++)
                {
                    float e = eventTicks[i];
                    if (e >= from && (e < end || (to >= Total && e <= Total && from < Total)))
                        fire(eventIds[i]);
                }
            }
        }
    }

    /// <summary>
    /// One animation track. Layer 0 is the base pose; layers above it override (or add to) it, optionally only for some bones:
    /// walk on layer 0, an arm swing with a <see cref="Skeleton.SubtreeMask"/> on layer 1, a hit flinch additive on layer 2
    /// </summary>
    public sealed class AnimLayer
    {
        public BoneClip Clip { get; private set; }
        /// <summary>Playback speed of this layer, 1 = authored (multiplied with <see cref="Skeleton.Speed"/>)</summary>
        public float Speed = 1f;
        /// <summary>0..1 influence over the pose below. Layer 0 ignores it</summary>
        public float Weight = 1f;
        /// <summary>Add the layer's offsets on top of the pose below instead of replacing it (sprite indices are not touched). Layer 0 ignores it</summary>
        public bool Additive;
        /// <summary>Per-bone 0..1 influence, null = every bone. See <see cref="Skeleton.SubtreeMask"/></summary>
        public float[] Mask;
        public float Time;

        public bool Finished => Clip != null && !Clip.Loop && Time >= Clip.Total;
        /// <summary>0..1 through the clip (looping clips wrap)</summary>
        public float Progress
        {
            get
            {
                if (Clip == null || Clip.Total <= 0f)
                    return 0f;
                return Clip.Loop ? (Time - MathF.Floor(Time / Clip.Total) * Clip.Total) / Clip.Total : MathF.Min(Time / Clip.Total, 1f);
            }
        }

        internal readonly BonePose[] raw, pose, from;
        internal readonly int[] spr;
        internal float blendT, blendLen;
        private float fadeFrom, fadeTo, fadeT, fadeLen;

        internal AnimLayer(int bones)
        {
            raw = new BonePose[bones];
            pose = new BonePose[bones];
            from = new BonePose[bones];
            spr = new int[bones];
        }

        /// <summary>Switch clips, crossfading from whatever this layer is showing right now. Same clip again is ignored unless <paramref name="restart"/></summary>
        public void Play(BoneClip next, int blendTicks = 6, bool restart = false)
        {
            if (next == Clip && !restart)
                return;

            Array.Copy(pose, from, pose.Length);
            Clip = next;
            Time = 0f;
            blendT = 0f;
            blendLen = blendTicks;
            if (next == null)
            {
                Array.Clear(raw);
                Array.Clear(spr);
            }
        }

        /// <summary>Stop the layer, fading its pose back to nothing (the layer below shows through)</summary>
        public void Stop(int blendTicks = 6) => Play(null, blendTicks);

        /// <summary>Ramp <see cref="Weight"/> to a value over some ticks</summary>
        public void FadeTo(float weight, int ticks)
        {
            if (ticks <= 0)
            {
                Weight = weight;
                fadeLen = 0f;
                return;
            }
            fadeFrom = Weight;
            fadeTo = weight;
            fadeT = 0f;
            fadeLen = ticks;
        }

        internal void TickFade()
        {
            if (fadeLen <= 0f)
                return;
            fadeT++;
            float u = LimbSolvers.SmoothStep(fadeT / fadeLen);
            Weight = MathHelper.Lerp(fadeFrom, fadeTo, u);
            if (fadeT >= fadeLen)
                fadeLen = 0f;
        }
    }

    /// <summary>
    /// Sprite bone hierarchy: plays hand-posed <see cref="BoneClip"/>s on one or more <see cref="AnimLayer"/>s (crossfaded on <see cref="Play"/>), then lets code
    /// push bones around on top of the clips (aim, IK, springs) before the final solve. Purely visual like <see cref="LegRig"/>, skipped when off-screen
    /// <para>
    /// Hook-up: build once (<see cref="Add"/> parents first, then <see cref="Build"/>), <see cref="Play"/> a clip, call <see cref="Update"/> from PostAI
    /// and <see cref="Draw"/> from PreDraw. Cache the code pass delegate in a static field, a method group allocates every call
    /// </para>
    /// <para>
    /// Need bone positions on the server (projectiles from a hand, hitboxes)? Set <see cref="AlwaysUpdate"/> and call Update there too, it
    /// only skips the off-screen test, drawing still needs a client
    /// </para>
    /// </summary>
    public sealed class Skeleton
    {
        public readonly Texture2D Atlas;
        public readonly List<Bone> Bones = new();
        public readonly List<AnimLayer> Layers = new();

        public float CullMargin = 400f;
        /// <summary>Never cull: keeps updating when off-screen or with no client (dedicated server), so bone positions stay valid for gameplay</summary>
        public bool AlwaysUpdate;
        /// <summary>Clip playback speed for every layer, 1 = authored</summary>
        public float Speed = 1f;
        /// <summary>A jump of more than this many px in one tick counts as a teleport and clears springs (lag) instead of whipping them</summary>
        public float TeleportDistance = 200f;
        /// <summary>Fired while updating when a clip passes one of its <see cref="BoneClip.Event"/> ticks</summary>
        public Action<Skeleton, AnimLayer, int> OnEvent;

        public Vector2 Root { get; private set; }
        public int Facing { get; private set; } = 1;
        public float Scale { get; private set; } = 1f;
        /// <summary>Whole-skeleton rotation in radians (leaning, wall crawling, tumbling), applied after the horizontal flip</summary>
        public float Rotation { get; private set; }
        public bool Culled => culled;
        /// <summary>The base layer (layer 0)</summary>
        public AnimLayer Base => Layers[0];

        private BonePose[] cur;
        private int[] spr, order;
        private bool culled = true;
        private int solvedTo = -1;
        private float rotSin, rotCos = 1f;
        private Vector2 lastRoot, rootDeltaModel;
        private bool hasLast;
        private AnimLayer eventLayer;
        private readonly Action<int> fireEvent;

        public Skeleton(Texture2D atlas)
        {
            Atlas = atlas;
            fireEvent = id => OnEvent?.Invoke(this, eventLayer, id);
        }

        /// <summary>
        /// Parents must be added before their children. Returns the bone index
        /// </summary>
        public int Add(string name, int parent, Vector2 restPos, float restRot, Rectangle[] frames = null, Vector2 pivot = default, float length = 0f, int z = 0)
        {
            Bones.Add(new Bone { Name = name, Parent = parent, RestPos = restPos, RestRot = restRot, Frames = frames, Pivot = pivot, Length = length, Z = z });
            return Bones.Count - 1;
        }

        public int Find(string name)
        {
            int i = TryFind(name);
            if (i < 0)
                throw new ArgumentException("No bone called " + name, nameof(name));
            return i;
        }

        /// <summary>Bone index by name, or -1</summary>
        public int TryFind(string name)
        {
            for (int i = 0; i < Bones.Count; i++)
            {
                if (Bones[i].Name == name)
                    return i;
            }
            return -1;
        }

        public Skeleton Build()
        {
            int n = Bones.Count;
            cur = new BonePose[n];
            spr = new int[n];
            order = new int[n];

            for (int i = 0; i < n; i++)
            {
                if (Bones[i].Parent >= i)
                    throw new InvalidOperationException("Bone " + Bones[i].Name + " is added before its parent");
                order[i] = i;
            }

            Layers.Clear();
            Layers.Add(new AnimLayer(n));
            return this;
        }

        /// <summary>
        /// Layer by index, created on demand (after <see cref="Build"/>). Layer 0 is the base
        /// </summary>
        public AnimLayer Layer(int index)
        {
            EnsureBuilt();
            while (Layers.Count <= index)
                Layers.Add(new AnimLayer(Bones.Count));
            return Layers[index];
        }

        /// <summary>
        /// Per-bone mask that is 1 for <paramref name="root"/> and everything below it in the hierarchy, 0 elsewhere. Assign to <see cref="AnimLayer.Mask"/>
        /// </summary>
        public float[] SubtreeMask(int root, bool includeRoot = true)
        {
            float[] mask = new float[Bones.Count];
            mask[root] = includeRoot ? 1f : 0f;
            for (int i = root + 1; i < Bones.Count; i++)
            {
                int p = Bones[i].Parent;
                if (p >= root && (mask[p] > 0f || (p == root && !includeRoot)))
                    mask[i] = 1f;
            }
            return mask;
        }

        /// <summary>
        /// Switch the base layer's clip, crossfading from whatever pose is showing right now. Same clip again is ignored unless <paramref name="restart"/>
        /// </summary>
        public void Play(BoneClip next, int blendTicks = 6, bool restart = false)
        {
            EnsureBuilt();
            Layers[0].Play(next, blendTicks, restart);
        }

        /// <summary>Clear springs and re-snap after a teleport or cutscene cut</summary>
        public void Reset()
        {
            hasLast = false;
            foreach (Bone b in Bones)
                b.ResetSprings();
        }

        /// <summary>
        /// Advance one tick: sample the clips, run <paramref name="codePass"/> (set <see cref="Bone.CodeRot"/>, call <see cref="Ik"/>), then solve the hierarchy
        /// </summary>
        public void Update(Vector2 center, int facing, float scale, Action<Skeleton> codePass = null) => Update(center, facing, scale, 0f, codePass);

        /// <summary>Same, with a whole-body rotation in radians</summary>
        public void Update(Vector2 center, int facing, float scale, float rotation, Action<Skeleton> codePass = null)
        {
            EnsureBuilt();
            culled = !AlwaysUpdate && !LegRig.OnScreen(center, CullMargin);
            if (culled)
            {
                hasLast = false;
                return;
            }

            Root = center;
            Facing = facing == 0 ? 1 : facing;
            Scale = scale;
            Rotation = rotation;
            rotSin = MathF.Sin(rotation);
            rotCos = MathF.Cos(rotation);

            if (!hasLast || Vector2.DistanceSquared(center, lastRoot) > TeleportDistance * TeleportDistance)
            {
                foreach (Bone b in Bones)
                    b.ResetSprings();
                rootDeltaModel = Vector2.Zero;
            }
            else
            {
                rootDeltaModel = ToModelVector(center - lastRoot);
            }
            lastRoot = center;
            hasLast = true;

            for (int li = 0; li < Layers.Count; li++)
                AdvanceLayer(Layers[li]);
            Combine();

            for (int i = 0; i < Bones.Count; i++)
            {
                Bone b = Bones[i];
                b.CodeRot = 0f;
                b.CodeOffset = Vector2.Zero;
                b.CodeScale = Vector2.One;
                b.Tint = Color.White;
                b.Opacity = 1f;
                b.Frame = spr[i];
            }

            solvedTo = -1;
            codePass?.Invoke(this);
            SolveTo(Bones.Count - 1);
        }

        private void AdvanceLayer(AnimLayer L)
        {
            L.TickFade();

            if (L.Clip != null)
            {
                float prev = L.Time;
                L.Time += L.Speed * Speed;
                if (OnEvent != null)
                {
                    eventLayer = L;
                    L.Clip.FireEvents(prev, L.Time, fireEvent);
                }
                L.Clip.Sample(L.Time, L.raw, L.spr);
            }

            if (L.blendT < L.blendLen)
            {
                L.blendT++;
                float w = LimbSolvers.SmoothStep(L.blendT / L.blendLen);
                for (int i = 0; i < L.pose.Length; i++)
                {
                    L.pose[i].Pos = Vector2.Lerp(L.from[i].Pos, L.raw[i].Pos, w);
                    L.pose[i].Rot = MathHelper.Lerp(L.from[i].Rot, L.raw[i].Rot, w);
                    L.pose[i].Stretch = Vector2.Lerp(L.from[i].Stretch, L.raw[i].Stretch, w);
                }
            }
            else
            {
                Array.Copy(L.raw, L.pose, L.pose.Length);
            }
        }

        private void Combine()
        {
            AnimLayer baseLayer = Layers[0];
            Array.Copy(baseLayer.pose, cur, cur.Length);
            Array.Copy(baseLayer.spr, spr, spr.Length);

            for (int li = 1; li < Layers.Count; li++)
            {
                AnimLayer L = Layers[li];
                // a stopped layer that has finished fading out has nothing to contribute
                if (L.Weight <= 0f || (L.Clip == null && L.blendT >= L.blendLen))
                    continue;

                for (int i = 0; i < cur.Length; i++)
                {
                    float w = L.Weight * (L.Mask != null ? L.Mask[i] : 1f);
                    if (w <= 0f)
                        continue;

                    if (L.Additive)
                    {
                        cur[i].Pos += L.pose[i].Pos * w;
                        cur[i].Rot += L.pose[i].Rot * w;
                        cur[i].Stretch += L.pose[i].Stretch * w;
                    }
                    else
                    {
                        cur[i].Pos = Vector2.Lerp(cur[i].Pos, L.pose[i].Pos, w);
                        cur[i].Rot = MathHelper.Lerp(cur[i].Rot, L.pose[i].Rot, w);
                        cur[i].Stretch = Vector2.Lerp(cur[i].Stretch, L.pose[i].Stretch, w);
                        if (w >= 0.5f && L.Clip != null)
                            spr[i] = L.spr[i];
                    }
                }
            }
        }

        private void EnsureBuilt()
        {
            if (cur == null)
                throw new InvalidOperationException("Call Skeleton.Build() after adding bones and before Play/Update");
        }

        /// <summary>
        /// Fresh world position of a bone's joint this tick (Bone.Pos is last tick's until the final solve). Use it to root chains and legs
        /// </summary>
        public Vector2 WorldPos(int bone)
        {
            SolveTo(bone);
            return Bones[bone].Pos;
        }

        /// <summary>
        /// A point in a bone's own frame (unscaled model px, +X along the bone) as a world position, this tick. Muzzles, hitbox anchors, attachment points:
        /// <c>PointOn(hand, new Vector2(12, 0))</c>
        /// </summary>
        public Vector2 PointOn(int bone, Vector2 local)
        {
            SolveTo(bone);
            Bone b = Bones[bone];
            Vector2 m = b.mPos + new Vector2(local.X * b.cos - local.Y * b.sin, local.X * b.sin + local.Y * b.cos);
            return ModelToWorld(m);
        }

        /// <summary>World position of the end of a bone (joint + <see cref="Bone.Length"/> along its +X)</summary>
        public Vector2 TipPos(int bone) => PointOn(bone, new Vector2(Bones[bone].Length, 0f));

        /// <summary>
        /// Point a bone's +X at a world position, optionally capped to <paramref name="limit"/> radians off its pose. Overwrites <see cref="Bone.CodeRot"/>
        /// <br/><paramref name="weight"/> below 1 blends from the bone's current CodeRot (0 = no effect)
        /// <br/>Set any direct CodeRot/CodeOffset on bones before the Aim/Ik/Lag calls, those solve as they go
        /// </summary>
        public void Aim(int bone, Vector2 worldTarget, float limit = MathHelper.Pi, float weight = 1f)
        {
            SolveTo(bone);
            Bone b = Bones[bone];
            Vector2 t = ToModel(worldTarget);

            float want = MathF.Atan2(t.Y - b.mPos.Y, t.X - b.mPos.X);
            float parentRot = b.Parent < 0 ? 0f : Bones[b.Parent].mRot;
            float rot = MathHelper.Clamp(MathHelper.WrapAngle(want - parentRot - b.RestRot - cur[bone].Rot), -limit, limit);
            b.CodeRot = weight >= 1f ? rot : b.CodeRot + (rot - b.CodeRot) * MathHelper.Clamp(weight, 0f, 1f);
            solvedTo = Math.Min(solvedTo, bone - 1);
        }

        /// <summary>
        /// Secondary motion: the bone's final angle trails behind whatever its parents (and pose) are doing, then settles. Call after anything that sets its CodeRot
        /// <br/><paramref name="omega"/> ~ 6 is floppy, ~18 is stiff
        /// </summary>
        public void Lag(int bone, float omega)
        {
            SolveTo(bone);
            Bone b = Bones[bone];

            if (b.lagOmega != omega)
            {
                b.lagOmega = omega;
                b.lagDecay = LimbSolvers.DecayFor(omega);
            }

            float desired = b.mRot;
            if (b.lagInit)
                b.lag.X -= MathHelper.WrapAngle(desired - b.lastDesired);
            b.lagInit = true;
            b.lastDesired = desired;

            LimbSolvers.SpringCached(ref b.lag, ref b.lagVel, Vector2.Zero, omega, b.lagDecay);
            b.lag.X = MathHelper.Clamp(b.lag.X, -1.2f, 1.2f);

            b.CodeRot += b.lag.X;
            solvedTo = Math.Min(solvedTo, bone - 1);
        }

        /// <summary>
        /// Positional secondary motion: the bone's joint is left behind when the whole body moves, then springs back (hanging antennae, cloth, a dangling lure).
        /// Adds to <see cref="Bone.CodeOffset"/>. <paramref name="maxOffset"/> caps the drag in model px
        /// </summary>
        public void LagPos(int bone, float omega, float maxOffset = 40f)
        {
            Bone b = Bones[bone];
            if (b.Parent >= 0)
                SolveTo(b.Parent);

            if (b.lagPOmega != omega)
            {
                b.lagPOmega = omega;
                b.lagPDecay = LimbSolvers.DecayFor(omega);
            }

            b.lagP -= rootDeltaModel;
            LimbSolvers.SpringCached(ref b.lagP, ref b.lagPVel, Vector2.Zero, omega, b.lagPDecay);
            float len = b.lagP.Length();
            if (len > maxOffset)
                b.lagP *= maxOffset / len;

            // lagP lives in model space, CodeOffset in the parent's frame
            float parentRot = b.Parent < 0 ? 0f : Bones[b.Parent].mRot;
            b.CodeOffset += LimbSolvers.Rotate(b.lagP, -parentRot);
            solvedTo = Math.Min(solvedTo, bone - 1);
        }

        /// <summary>
        /// Skin a tail/tentacle: bones <paramref name="first"/>, first+1... (each the child of the last, joint at the previous bone's length) follow the joints of a
        /// <see cref="FabrikChain"/> with matching bone lengths. Solve the chain from <c>WorldPos(first)</c> first
        /// </summary>
        public void Chain(int first, FabrikChain chain, float weight = 1f)
        {
            for (int k = 0; k < chain.Points.Length - 1; k++)
                Aim(first + k, chain.Points[k + 1], MathHelper.Pi, weight);
        }

        private Vector2 ToModel(Vector2 world)
        {
            Vector2 v = world - Root;
            v = new Vector2(v.X * rotCos + v.Y * rotSin, -v.X * rotSin + v.Y * rotCos); // rotate by -Rotation
            return new Vector2(v.X * Facing, v.Y) / Scale;
        }

        private Vector2 ToModelVector(Vector2 worldDelta)
        {
            Vector2 v = new Vector2(worldDelta.X * rotCos + worldDelta.Y * rotSin, -worldDelta.X * rotSin + worldDelta.Y * rotCos);
            return new Vector2(v.X * Facing, v.Y) / Scale;
        }

        private Vector2 ModelToWorld(Vector2 m)
        {
            Vector2 v = new Vector2(m.X * Facing, m.Y) * Scale;
            return Root + new Vector2(v.X * rotCos - v.Y * rotSin, v.X * rotSin + v.Y * rotCos);
        }

        /// <summary>
        /// Two-bone IK from the code pass: reaches <paramref name="lower"/>'s tip for <paramref name="worldTarget"/>. Overwrites <see cref="Bone.CodeRot"/> on both bones
        /// <br/><paramref name="lower"/> must be a child of <paramref name="upper"/>, sitting at (upper length, 0) in its frame. Flip <paramref name="bend"/> for the other elbow
        /// <br/><paramref name="weight"/> below 1 blends between the clip pose and the IK result (reach for a target only while attacking)
        /// </summary>
        public void Ik(int upper, int lower, Vector2 worldTarget, float bend = 1f, float weight = 1f)
        {
            Bone u = Bones[upper];
            Bone l = Bones[lower];
            if (l.Parent != upper)
                throw new ArgumentException("Ik: bone " + l.Name + " must be a child of " + u.Name);

            SolveTo(upper);

            Vector2 elbow = LimbSolvers.TwoBone(u.mPos, ToModel(worldTarget), l.RestPos.Length(), l.Length, bend, out Vector2 tip);

            float ru = MathF.Atan2(elbow.Y - u.mPos.Y, elbow.X - u.mPos.X);
            float rl = MathF.Atan2(tip.Y - elbow.Y, tip.X - elbow.X);
            float parentRot = u.Parent < 0 ? 0f : Bones[u.Parent].mRot;

            float cu = ru - parentRot - u.RestRot - cur[upper].Rot;
            float cl = rl - ru - l.RestRot - cur[lower].Rot;

            if (weight >= 1f)
            {
                u.CodeRot = cu;
                l.CodeRot = cl;
            }
            else
            {
                float w = MathHelper.Clamp(weight, 0f, 1f);
                u.CodeRot += MathHelper.WrapAngle(cu - u.CodeRot) * w;
                l.CodeRot += MathHelper.WrapAngle(cl - l.CodeRot) * w;
            }
            solvedTo = Math.Min(solvedTo, upper - 1);
        }

        private void SolveTo(int last)
        {
            int start = solvedTo + 1;
            if (last > solvedTo)
                solvedTo = last;

            for (int i = start; i <= last; i++)
            {
                Bone b = Bones[i];
                Vector2 lp = b.RestPos + cur[i].Pos + b.CodeOffset;
                float lr = b.RestRot + cur[i].Rot + b.CodeRot;

                if (b.Parent < 0)
                {
                    b.mPos = lp;
                    b.mRot = lr;
                }
                else
                {
                    Bone p = Bones[b.Parent];
                    b.mPos = p.mPos + new Vector2(lp.X * p.cos - lp.Y * p.sin, lp.X * p.sin + lp.Y * p.cos);
                    b.mRot = p.mRot + lr;
                }

                b.sin = MathF.Sin(b.mRot);
                b.cos = MathF.Cos(b.mRot);
                b.Pos = ModelToWorld(b.mPos);
                b.Rot = b.mRot * Facing + Rotation;
            }
        }

        private void SortByZ()
        {
            // insertion sort: the order barely changes tick to tick, so this is O(n)
            for (int i = 1; i < order.Length; i++)
            {
                int v = order[i];
                int z = Bones[v].Z;
                int j = i - 1;
                while (j >= 0 && Bones[order[j]].Z > z)
                {
                    order[j + 1] = order[j];
                    j--;
                }
                order[j + 1] = v;
            }
        }

        public void Draw(SpriteBatch sb, Vector2 screenPos, Color light) => Draw(sb, screenPos, light, 1f);

        /// <summary><paramref name="opacity"/> fades the whole skeleton (spawn-in, death fade) on top of each bone's own <see cref="Bone.Opacity"/></summary>
        public void Draw(SpriteBatch sb, Vector2 screenPos, Color light, float opacity)
        {
            if (culled || cur == null)
                return;

            SortByZ();
            SpriteEffects fx = Facing < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            for (int k = 0; k < order.Length; k++)
            {
                int i = order[k];
                Bone b = Bones[i];
                if (!b.Visible || b.Opacity <= 0f)
                    continue;

                Color col = RigUtil.Modulate(light, b.Tint, b.Opacity * opacity);
                if (b.CustomDraw != null && b.CustomDraw(this, b, sb, screenPos, col))
                    continue;

                if (b.Frames == null || b.Frames.Length == 0)
                    continue;

                Rectangle src = b.Frames[Math.Clamp(b.Frame, 0, b.Frames.Length - 1)];
                // SpriteBatch applies the origin in unflipped quad space, so a mirrored sprite needs its pivot mirrored too
                Vector2 pivot = Facing < 0 ? new Vector2(src.Width - b.Pivot.X, b.Pivot.Y) : b.Pivot;
                Vector2 scale = new Vector2((1f + cur[i].Stretch.X) * b.CodeScale.X, (1f + cur[i].Stretch.Y) * b.CodeScale.Y) * Scale;

                sb.Draw(b.Atlas ?? Atlas, b.Pos - screenPos, src, col, b.Rot, pivot, scale, fx, 0f);
            }
        }
    }
}
