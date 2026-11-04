using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using ParticleLibrary.Core.V3.Particles;
using ParticleLibrary.Utilities;
using ShatteredIllusion.Content.Particles;
using Terraria;
using Terraria.ModLoader;
using SystemVector2 = System.Numerics.Vector2;

namespace ShatteredIllusion.Common.Visuals
{
    public readonly struct FxContext
    {
        public readonly Vector2 Pos;
        public readonly Vector2 Dir;
        public readonly float Scale;
        public readonly Color Tint;
        public readonly int Age;

        public Color Hot => Color.Lerp(Tint, Color.White, 0.6f);

        public FxContext(Vector2 pos, Vector2 dir, float scale, Color tint, int age)
        {
            Pos = pos;
            Dir = dir.SafeNormalize(Vector2.UnitX);
            Scale = scale;
            Tint = tint;
            Age = age;
        }
    }

    public readonly struct FxHandle
    {
        internal readonly int Slot;
        internal readonly int Id;

        internal FxHandle(int slot, int id)
        {
            Slot = slot;
            Id = id;
        }
    }

    public sealed class FxDef
    {
        internal readonly List<(int Delay, bool Optional, Action<FxContext> Run)> Steps = new();
        internal Color DefaultTint = new Color(150, 225, 255);

        public FxDef Tinted(Color color)
        {
            DefaultTint = color;
            return this;
        }

        // Always runs: camera, sound, anything gameplay-relevant.
        public FxDef At(int delayFrames, Action<FxContext> run) => Add(delayFrames, false, run);

        // Decorative: skipped when particles are off in config or too many effects are playing.
        public FxDef Extra(int delayFrames, Action<FxContext> run) => Add(delayFrames, true, run);

        public FxDef Every(int startFrame, int count, int interval, Action<FxContext> run)
        {
            for (int i = 0; i < count; i++)
                Add(startFrame + i * interval, true, run);

            return this;
        }

        private FxDef Add(int delay, bool optional, Action<FxContext> run)
        {
            if (Steps.Count > 0 && delay < Steps[^1].Delay)
                throw new ArgumentException("FxDef steps must be added in ascending delay order.");

            Steps.Add((delay, optional, run));
            return this;
        }
    }

    public static class VisualFX
    {
        // ponytail: fixed slot array and flat caps, raise or make config-driven if a fight needs more.
        private const int MaxInstances = 32;
        private const int ExtraCap = 8;

        // Per-frame velocity multiplier of SandStreakParticle. Converge solves its launch speed from this,
        // so the particle's Update must use the same value (reference this const there).
        public const float StreakDrag = 0.93f;

        private struct Instance
        {
            public FxDef Def;
            public Entity Follow;
            public Vector2 Pos;
            public Vector2 Dir;
            public float Scale;
            public Color Tint;
            public int Id;
            public int Age;
            public int Next;
            public int FollowKey;
        }

        private static readonly Instance[] slots = new Instance[MaxInstances];
        private static int active;
        private static int nextId;

        public static float Density =>
            ModContent.GetInstance<ShatteredIllusionConfig>().EffectDensity * (active > ExtraCap / 2 ? 0.5f : 1f);

        public static int Scaled(int count) => count <= 0 ? 0 : Math.Max(1, (int)(count * Density + 0.5f));

        public static FxHandle Play(FxDef def, Vector2 pos, Vector2 dir = default, float scale = 1f, Color? tint = null)
            => Start(def, null, pos, dir, scale, tint);

        // Follows the entity (pos becomes an offset from its center) and stops itself if the entity dies.
        public static FxHandle PlayOn(FxDef def, Entity follow, Vector2 offset = default, Vector2 dir = default, float scale = 1f, Color? tint = null)
            => Start(def, follow, offset, dir, scale, tint);

        private static FxHandle Start(FxDef def, Entity follow, Vector2 pos, Vector2 dir, float scale, Color? tint)
        {
            if (Main.dedServ)
                return default;

            var inst = new Instance
            {
                Def = def,
                Follow = follow,
                Pos = pos,
                Dir = dir,
                Scale = scale,
                Tint = tint ?? def.DefaultTint,
                Id = ++nextId,
                FollowKey = FollowKeyOf(follow)
            };

            inst.Next = Run(ref inst);

            if (inst.Next >= def.Steps.Count)
                return default;

            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].Def != null)
                    continue;

                slots[i] = inst;
                active++;
                return new FxHandle(i, inst.Id);
            }

            WarnSlotsFull();
            return default;
        }

        private static uint lastFullWarning;

        // Effects past MaxInstances are dropped. Say so, at most about once every 5 seconds, so a busy fight doesn't fail silently.
        private static void WarnSlotsFull()
        {
            if (lastFullWarning != 0 && Main.GameUpdateCount - lastFullWarning < 300)
                return;

            lastFullWarning = Main.GameUpdateCount == 0 ? 1 : Main.GameUpdateCount;

            if (ModLoader.TryGetMod("ShatteredIllusion", out Mod mod))
                mod.Logger.Warn($"VisualFX: all {MaxInstances} effect slots are in use, dropping effects. Raise MaxInstances if this is expected.");
        }

        // Entity slots are reused: an NPC or projectile can die and a different one take over the same object or index,
        // and `active` alone cannot tell the difference. The key is the type (NPC) or spawn identity (projectile).
        internal static int FollowKeyOf(Entity e) => e switch
        {
            NPC n => n.type,
            Projectile p => p.identity,
            _ => 0
        };

        internal static bool StillFollowing(Entity e, int key)
        {
            if (!e.active)
                return false;

            switch (e)
            {
                case NPC n:
                    return n.type == key && ((uint)n.whoAmI >= Main.maxNPCs || ReferenceEquals(Main.npc[n.whoAmI], n));
                case Projectile p:
                    return p.identity == key && ((uint)p.whoAmI >= Main.maxProjectiles || ReferenceEquals(Main.projectile[p.whoAmI], p));
                default:
                    return true;
            }
        }

        public static bool IsPlaying(FxHandle h) => h.Id != 0 && slots[h.Slot].Id == h.Id && slots[h.Slot].Def != null;

        public static void Cancel(FxHandle h)
        {
            if (IsPlaying(h))
                Free(h.Slot);
        }

        public static void Cancel(FxDef def)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].Def == def)
                    Free(i);
            }
        }

        public static void Clear()
        {
            Array.Clear(slots);
            active = 0;
        }

        private static void Free(int i)
        {
            slots[i] = default;
            active--;
        }

        internal static void Update()
        {
            if (active == 0)
                return;

            for (int i = 0; i < slots.Length; i++)
            {
                ref Instance s = ref slots[i];

                if (s.Def == null)
                    continue;

                if (s.Follow != null && !StillFollowing(s.Follow, s.FollowKey))
                {
                    Free(i);
                    continue;
                }

                s.Age++;
                int next = Run(ref s);

                if (s.Def == null)
                    continue;

                s.Next = next;

                if (next >= s.Def.Steps.Count)
                    Free(i);
            }
        }

        private static int Run(ref Instance inst)
        {
            var steps = inst.Def.Steps;
            int next = inst.Next;

            if (next >= steps.Count)
                return next;

            Vector2 pos = inst.Follow != null ? inst.Follow.Center + inst.Pos : inst.Pos;
            var ctx = new FxContext(pos, inst.Dir, inst.Scale, inst.Tint, inst.Age);

            while (next < steps.Count && steps[next].Delay <= inst.Age)
            {
                var step = steps[next++];

                if (step.Optional && (!Cinema.ParticlesEnabled || active > ExtraCap))
                    continue;

                step.Run(ctx);

                if (inst.Def == null)
                    break;
            }

            return next;
        }

        public static void Ring(Vector2 pos, float size, Color color, int duration, float flatten = 1f)
        {
            BossParticleSystem.Shockwaves.Create(new ParticleInfo(
                position: pos.ToNumerics(),
                velocity: SystemVector2.Zero,
                rotation: 0f,
                scale: new SystemVector2(size, size * flatten),
                color: color,
                duration: duration));
        }

        public static void Flash(Vector2 pos, float size, Color color, int duration)
        {
            BossParticleSystem.Flashes.Create(new ParticleInfo(
                position: pos.ToNumerics(),
                velocity: SystemVector2.Zero,
                rotation: 0f,
                scale: new SystemVector2(size, size),
                color: color,
                duration: duration));
        }

        public static void Sparks(Vector2 pos, Vector2 dir, float spread, int count, float speedMin, float speedMax, Color color, float size, int duration)
        {
            if (!Cinema.ParticlesEnabled)
                return;

            count = Scaled(count);

            for (int i = 0; i < count; i++)
            {
                Vector2 v = spread >= MathHelper.Pi
                    ? Main.rand.NextVector2CircularEdge(1f, 1f)
                    : dir.RotatedByRandom(spread);

                BossParticleSystem.EmberBursts.Create(new ParticleInfo(
                    position: pos.ToNumerics(),
                    velocity: (v * Main.rand.NextFloat(speedMin, speedMax)).ToNumerics(),
                    rotation: 0f,
                    scale: new SystemVector2(size, size),
                    color: color,
                    duration: duration));
            }
        }

        // Stretched streaks flung along dir. They coast roughly 14x their speed before stopping.
        public static void Streaks(Vector2 pos, Vector2 dir, float spread, int count, float speedMin, float speedMax, float length, float width, Color color, int duration)
        {
            if (!Cinema.ParticlesEnabled)
                return;

            count = Scaled(count);

            for (int i = 0; i < count; i++)
            {
                Vector2 v = spread >= MathHelper.Pi
                    ? Main.rand.NextVector2CircularEdge(1f, 1f)
                    : dir.RotatedByRandom(spread);

                BossParticleSystem.SandStreaks.Create(new ParticleInfo(
                    position: pos.ToNumerics(),
                    velocity: (v * Main.rand.NextFloat(speedMin, speedMax)).ToNumerics(),
                    rotation: 0f,
                    scale: new SystemVector2(length, width),
                    color: color,
                    duration: duration));
            }
        }

        // Streaks that start on a circle and arrive at the center exactly when they expire.
        public static void Converge(Vector2 pos, float radius, int count, Color color, int duration, float length = 44f, float width = 6f)
        {
            if (!Cinema.ParticlesEnabled)
                return;

            count = Scaled(count);

            // duration 0 would divide by zero below (1 - drag^0 = 0)
            duration = Math.Max(1, duration);

            // distance covered over `duration` frames of drag is speed * (1 - drag^duration) / (1 - drag)
            float speed = radius * (1f - StreakDrag) / (1f - MathF.Pow(StreakDrag, duration));

            for (int i = 0; i < count; i++)
            {
                Vector2 outward = Main.rand.NextVector2CircularEdge(1f, 1f);

                BossParticleSystem.SandStreaks.Create(new ParticleInfo(
                    position: (pos + outward * radius).ToNumerics(),
                    velocity: (-outward * speed).ToNumerics(),
                    rotation: 0f,
                    scale: new SystemVector2(length, width),
                    color: color,
                    duration: duration));
            }
        }
    }

    public class VisualFXSystem : ModSystem
    {
        // Builds every Fx definition at load so a bad definition fails here, not mid-fight.
        public override void OnModLoad()
        {
            if (!Main.dedServ)
                RuntimeHelpers.RunClassConstructor(typeof(Fx).TypeHandle);
        }

        public override void PostUpdateEverything() => VisualFX.Update();

        public override void OnWorldUnload() => VisualFX.Clear();
    }
}
