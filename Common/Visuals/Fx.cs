using Microsoft.Xna.Framework;
using Terraria.Audio;
using Terraria.ID;

namespace ShatteredIllusion.Common.Visuals
{
    public static class Fx
    {
        private static readonly SoundStyle HeavyImpact = SoundID.Item14 with { Pitch = -0.55f, Volume = 0.55f };
        private static readonly SoundStyle BigBlast = SoundID.Item14 with { Pitch = -0.8f, Volume = 0.8f };

        // Spawn windup/flash timings are shared between the spawn calls and the burst steps so they cannot drift apart.
        private const int PhaseBurstFrame = 26;
        private const int ChargeFlashFrame = 40;

        // Life for a converge streak spawned now so it arrives exactly on `frame`.
        private static int Until(int frame, FxContext c) => System.Math.Max(1, frame - c.Age);

        // World-space visuals only, safe to play on every client. Sound, dust and the owner-only hit hold stay in ParryPlayer.
        public static readonly FxDef PerfectParry = new FxDef()
            .Tinted(new Color(255, 210, 100))
            .Extra(0, static c =>
            {
                VisualFX.Flash(c.Pos, 130f * c.Scale, c.Hot, 8);
                VisualFX.Ring(c.Pos, 90f * c.Scale, c.Tint, 14);
                VisualFX.Sparks(c.Pos, -c.Dir, 0.8f, 9, 5f, 9f, c.Hot, 9f, 20);
                VisualFX.Sparks(c.Pos, c.Dir, MathHelper.Pi, 7, 2f, 5f, c.Tint, 7f, 18);
            })
            .Extra(4, static c =>
            {
                VisualFX.Ring(c.Pos, 160f * c.Scale, c.Tint, 22);
                VisualFX.Sparks(c.Pos, -c.Dir, 1.4f, 6, 1.5f, 4f, c.Tint, 6f, 24);
                VisualFX.Streaks(c.Pos, -c.Dir, 0.5f, 4, 4f, 8f, 50f, 5f, c.Hot, 16);
            });

        // Camera half of a perfect parry. Play it only for the player who parried: it shakes, zooms and flashes the local screen.
        public static readonly FxDef PerfectParryCamera = new FxDef()
            .At(0, static c =>
            {
                Cinema.Shake(c.Pos, c.Dir, 6f * c.Scale, 7f, 9);
                Cinema.ZoomPunch(0.015f * c.Scale);
                Cinema.Flash(new Color(255, 230, 160), 0.07f);
                ScreenFX.Ripple(c.Pos, 260f * c.Scale, 18, 0.6f);
                ScreenFX.Aberration(0.5f * c.Scale, 8);
            });

        // pos = feet, scale = power (1 = normal slam, 1.5+ adds the wide second ring).
        public static readonly FxDef BossLanding = new FxDef()
            .Tinted(new Color(120, 200, 255))
            .At(0, static c =>
            {
                Cinema.ZoomPunch(0.035f * c.Scale);
                Cinema.Flash(c.Hot, 0.22f * c.Scale);
                Cinema.Shake(c.Pos, Vector2.UnitY, 8f * c.Scale, 14f, 8 + (int)(14 * c.Scale));
                SoundEngine.PlaySound(HeavyImpact, c.Pos);
                ScreenFX.Ripple(c.Pos, 520f * c.Scale, 28, 0.8f * c.Scale);
            })
            .Extra(0, static c =>
            {
                VisualFX.Flash(c.Pos - new Vector2(0f, 12f), 220f * c.Scale, c.Hot, 10);
                VisualFX.Ring(c.Pos - new Vector2(0f, 3f), 150f * c.Scale, c.Hot, 20, 0.28f);
                VisualFX.Sparks(c.Pos, -Vector2.UnitY, 1.4f, (int)(8 * c.Scale), 2.5f, 7f, c.Tint, 9f, 20);

                Vector2 skirt = c.Pos - new Vector2(0f, 2f);
                VisualFX.Streaks(skirt, Vector2.UnitX, 0.15f, 4, 5f, 9f, 60f * c.Scale, 8f, c.Tint, 16);
                VisualFX.Streaks(skirt, -Vector2.UnitX, 0.15f, 4, 5f, 9f, 60f * c.Scale, 8f, c.Tint, 16);
            })
            .Extra(3, static c =>
            {
                if (c.Scale >= 1.5f)
                    VisualFX.Ring(c.Pos - new Vector2(0f, 3f), 250f * c.Scale, c.Tint, 32, 0.28f);
            });

        // pos = hit point, dir = projectile travel direction. No camera work: most hits shouldn't move the screen.
        public static readonly FxDef ProjectileImpact = new FxDef()
            .Tinted(new Color(255, 220, 160))
            .Extra(0, static c =>
            {
                VisualFX.Flash(c.Pos, 60f * c.Scale, c.Hot, 6);
                VisualFX.Ring(c.Pos, 50f * c.Scale, c.Tint, 10);
                VisualFX.Sparks(c.Pos, -c.Dir, 0.9f, 6, 3f, 7f, c.Tint, 6f, 14);
            });

        // Streaks are spawned frames 0-18 and all land on PhaseBurstFrame; the last one is spawned 8 frames out
        // so it never has to cross the whole radius in a couple of frames.
        public static readonly FxDef PhaseTransition = new FxDef()
            .Tinted(new Color(255, 120, 90))
            .Every(0, 7, 3, static c => VisualFX.Converge(c.Pos, 260f * c.Scale, 6, c.Tint, Until(PhaseBurstFrame, c)))
            .At(PhaseBurstFrame, static c =>
            {
                Cinema.Shake(c.Pos, Vector2.UnitY, 12f * c.Scale, 16f, 30);
                Cinema.ZoomPunch(0.06f * c.Scale);
                Cinema.Flash(c.Hot, 0.5f);
                SoundEngine.PlaySound(BigBlast, c.Pos);
                ScreenFX.Ripple(c.Pos, 900f * c.Scale, 40, 1.2f);
                ScreenFX.Aberration(1f, 22);
                ScreenFX.ZoomBlur(0.6f, 14, c.Pos);
            })
            .Extra(PhaseBurstFrame, static c =>
            {
                VisualFX.Flash(c.Pos, 400f * c.Scale, c.Hot, 14);
                VisualFX.Ring(c.Pos, 220f * c.Scale, c.Hot, 24);
                VisualFX.Sparks(c.Pos, Vector2.UnitX, MathHelper.Pi, 24, 4f, 11f, c.Tint, 10f, 26);
            })
            .Extra(PhaseBurstFrame + 5, static c => VisualFX.Ring(c.Pos, 380f * c.Scale, c.Tint, 34))
            .Extra(PhaseBurstFrame + 10, static c =>
            {
                VisualFX.Ring(c.Pos, 560f * c.Scale, c.Tint, 44);
                VisualFX.Streaks(c.Pos, Vector2.UnitX, MathHelper.Pi, 14, 6f, 12f, 90f, 6f, c.Hot, 22);
            });

        // Use with PlayOn so the windup tracks the boss.
        public static readonly FxDef ChargeUp = new FxDef()
            .Tinted(new Color(255, 200, 120))
            .Every(0, 17, 2, static c => VisualFX.Converge(c.Pos, 180f * c.Scale, 3, c.Tint, Until(ChargeFlashFrame, c)))
            .Extra(ChargeFlashFrame, static c => VisualFX.Flash(c.Pos, 160f * c.Scale, c.Hot, 10));
    }
}
