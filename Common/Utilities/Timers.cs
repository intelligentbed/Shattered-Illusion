using System;
using Microsoft.Xna.Framework;

namespace ShatteredIllusion.Common.Utilities
{
    // Counts down in ticks. Struct, so it works as a field on NPCs/projectiles with no allocation.
    //   Cooldown dash = new Cooldown(90);   dash.Start();   if (dash.Ready) ...   // call dash.Tick() once per AI tick
    public struct Cooldown
    {
        public int Duration;
        public int Remaining;

        public Cooldown(int duration, bool startReady = true)
        {
            Duration = duration;
            Remaining = startReady ? 0 : duration;
        }

        public bool Ready => Remaining <= 0;
        public float Progress => Duration <= 0 ? 1f : 1f - Remaining / (float)Duration;

        public void Start() => Remaining = Duration;
        public void Start(int duration) { Duration = duration; Remaining = duration; }
        public void Tick() { if (Remaining > 0) Remaining--; }

        // Tick and report in one go: true on the tick it becomes ready, restarting itself.
        public bool TickAndRestart()
        {
            if (Remaining > 0)
                Remaining--;
            if (Remaining > 0)
                return false;
            Remaining = Duration;
            return true;
        }
    }

    // A timed phase: 0 -> Length ticks, with Progress 0..1. Use for attack windups, tweens, cutscene beats.
    //   Phase windup = new Phase(30);  windup.Tick();  scale = Ease.OutBack(windup.Progress);  if (windup.Done) ...
    public struct Phase
    {
        public int Length;
        public int Time;

        public Phase(int length) { Length = length; Time = 0; }

        public float Progress => Length <= 0 ? 1f : Ease.Clamp01(Time / (float)Length);
        public bool Done => Time >= Length;

        public void Tick() { if (Time < Length) Time++; }
        public void Reset() => Time = 0;
        public void Reset(int length) { Length = length; Time = 0; }
    }

    // Value chasing a target with a different speed up vs down. Good for fade-ins that are slow to fade out, charge meters, glow intensity.
    public struct Meter
    {
        public float Value;
        public float Target;
        public float UpSpeed;
        public float DownSpeed;

        public Meter(float upSpeed, float downSpeed)
        {
            Value = 0f;
            Target = 0f;
            UpSpeed = upSpeed;
            DownSpeed = downSpeed;
        }

        public float Update(bool active)
        {
            Target = active ? 1f : 0f;
            Value = Ease.Approach(Value, Target, active ? UpSpeed : DownSpeed);
            return Value;
        }
    }

    // Fixed-size ring buffer of past positions (newest first). For trails, afterimages, and "where was I N ticks ago" logic.
    //   PosHistory trail = new PosHistory(20);   trail.Push(npc.Center);   trail[5] // 5 ticks ago
    public sealed class PosHistory
    {
        private readonly Vector2[] data;
        private int head;
        public int Count { get; private set; }
        public int Capacity => data.Length;

        public PosHistory(int capacity) { data = new Vector2[Math.Max(2, capacity)]; }

        public void Push(Vector2 p)
        {
            head = (head + data.Length - 1) % data.Length;
            data[head] = p;
            if (Count < data.Length)
                Count++;
        }

        public void Clear() { Count = 0; head = 0; }

        // 0 = newest.
        public Vector2 this[int age] => data[(head + Math.Min(age, Count - 1)) % data.Length];

        // Re-anchor everything (use after a teleport so the trail does not streak across the map).
        public void Fill(Vector2 p)
        {
            for (int i = 0; i < data.Length; i++) data[i] = p;
            Count = data.Length;
        }
    }
}
