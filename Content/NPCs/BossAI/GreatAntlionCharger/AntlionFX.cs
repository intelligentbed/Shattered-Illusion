using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ParticleLibrary.Core.V3.Particles;
using ParticleLibrary.Utilities;
using ReLogic.Content;
using ShatteredIllusion.Common.Visuals;
using ShatteredIllusion.Content.Particles;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using SystemVector2 = System.Numerics.Vector2;

namespace ShatteredIllusion.Content.NPCs.BossAI.GreatAntlionCharger
{
    // keeps the sky tint + flushes the zoom hold once per tick, everything else goes through Cinema
    public class AntlionFXSystem : ModSystem
    {
        public override void PostUpdateEverything() => AntlionFX.Tick();

        public override void ModifySunLightColor(ref Color tileColor, ref Color backgroundColor)
        {
            float mood = AntlionFX.MoodAmount;

            if (mood < 0.01f)
                return;

            tileColor = Color.Lerp(tileColor, tileColor.MultiplyRGB(new Color(255, 190, 135)), mood);
            backgroundColor = Color.Lerp(backgroundColor, backgroundColor.MultiplyRGB(new Color(255, 165, 115)), mood);
        }

        public override void Unload() => AntlionFX.Unload();
    }

    // thin wrappers over Cinema (range check + config) and the draw/particle helpers for the antlion
    internal static class AntlionFX
    {
        private const float MaxViewRange = 2400f;
        private const string BeamPath = "ShatteredIllusion/Content/NPCs/BossAI/GreatAntlionCharger/RubbleTelegraph";

        private static Asset<Texture2D>? beamAsset;
        private static float holdGoal, mood, moodGoal;
        private static bool holdActive;

        public static readonly ImpactStyle SandStyle = new ImpactStyle
        {
            Flash = new Color(255, 225, 180),
            InnerRing = new Color(255, 200, 130),
            OuterRing = new Color(235, 150, 80),
            Spark = new Color(255, 190, 110),
            DebrisDust = DustID.Sand,
            SkirtDust = DustID.Sand,
            SkirtColor = new Color(235, 200, 140),
            Sound = null
        };

        public static readonly ImpactStyle ParryStyle = new ImpactStyle
        {
            Flash = new Color(255, 230, 150),
            InnerRing = new Color(255, 235, 150),
            OuterRing = new Color(255, 190, 60),
            Spark = new Color(255, 225, 120),
            SkirtDust = -1,
            Sound = null
        };

        public static float MoodAmount => Cinema.Enabled ? mood : 0f;

        public static Texture2D BeamTexture =>
            (beamAsset ??= ModContent.Request<Texture2D>(BeamPath, AssetRequestMode.ImmediateLoad)).Value;

        public static void Unload() => beamAsset = null;

        private static bool Near(Vector2 pos) =>
            !Main.dedServ && Main.LocalPlayer.active && Vector2.Distance(Main.LocalPlayer.Center, pos) < MaxViewRange;

        public static void Tick()
        {
            if (Main.dedServ)
                return;

            if (holdGoal > 0f || holdActive)
            {
                Cinema.HoldZoom(holdGoal);
                holdActive = holdGoal > 0f;
            }

            holdGoal = 0f;
            mood = MathHelper.Lerp(mood, moodGoal, 0.02f);
            moodGoal = 0f;
        }

        public static void Bars(Vector2 pos, int ticks = 3)
        {
            if (Near(pos))
                Cinema.Letterbox(true, ticks);
        }

        public static void ZoomPunch(Vector2 pos, float amount)
        {
            if (Near(pos))
                Cinema.ZoomPunch(amount);
        }

        // call every tick while you want it held
        public static void ZoomHold(Vector2 pos, float amount)
        {
            if (Near(pos))
                holdGoal = Math.Max(holdGoal, amount);
        }

        public static void Flash(Vector2 pos, Color color, float strength)
        {
            if (Near(pos))
                Cinema.Flash(color, strength);
        }

        public static void Mood(float goal)
        {
            if (!Main.dedServ)
                moodGoal = Math.Max(moodGoal, goal);
        }

        public static void Impact(Vector2 pos, float power, bool ground = true, ImpactStyle? style = null)
        {
            if (Near(pos))
                Cinema.Impact(pos, power, style ?? SandStyle, ground);
        }

        public static void Ring(Vector2 pos, Vector2 size, Color color, int duration)
        {
            if (!Cinema.ParticlesEnabled)
                return;

            BossParticleSystem.Shockwaves.Create(new ParticleInfo(
                position: pos.ToNumerics(),
                velocity: SystemVector2.Zero,
                rotation: 0f,
                scale: new SystemVector2(size.X, size.Y),
                color: new Color(color.R, color.G, color.B, 0),
                duration: duration
            ));
        }

        public static void Embers(Vector2 pos, int count, float minSpeed, float maxSpeed, Color color, float size = 12f, Vector2 bias = default)
        {
            if (!Cinema.ParticlesEnabled)
                return;

            for (int i = 0; i < count; i++)
            {
                Vector2 vel = Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(minSpeed, maxSpeed) + bias;

                BossParticleSystem.EmberBursts.Create(new ParticleInfo(
                    position: pos.ToNumerics(),
                    velocity: vel.ToNumerics(),
                    rotation: 0f,
                    scale: new SystemVector2(size, size),
                    color: new Color(color.R, color.G, color.B, 0),
                    duration: Main.rand.Next(22, 38)
                ));
            }
        }

        public static void Streak(Vector2 pos, Vector2 vel, Vector2 size, Color color, int duration)
        {
            if (!Cinema.ParticlesEnabled)
                return;

            BossParticleSystem.SandStreaks.Create(new ParticleInfo(
                position: pos.ToNumerics(),
                velocity: vel.ToNumerics(),
                rotation: vel == Vector2.Zero ? 0f : vel.ToRotation(),
                scale: new SystemVector2(size.X, size.Y),
                color: new Color(color.R, color.G, color.B, 0),
                duration: duration
            ));
        }

        // draw helpers drop alpha to 0 so they add up like light, scale brightness by multiplying the color
        public static void Glow(SpriteBatch sb, Vector2 worldPos, Color color, Vector2 size, float rotation = 0f)
        {
            Texture2D tex = TextureAssets.Extra[ExtrasID.ThePerfectGlow].Value;
            sb.Draw(tex, worldPos - Main.screenPosition, null, new Color(color.R, color.G, color.B, 0), rotation, tex.Size() / 2f, size / tex.Size(), SpriteEffects.None, 0f);
        }

        // 4 point star, size in pixels
        public static void Flare(SpriteBatch sb, Vector2 worldPos, Color color, float size, float rotation = 0f)
        {
            Texture2D tex = TextureAssets.Extra[ExtrasID.SharpTears].Value;
            float scale = size / tex.Height;
            Color c = new Color(color.R, color.G, color.B, 0);
            Vector2 pos = worldPos - Main.screenPosition;

            sb.Draw(tex, pos, null, c, rotation, tex.Size() / 2f, new Vector2(scale * 0.5f, scale), SpriteEffects.None, 0f);
            sb.Draw(tex, pos, null, c, rotation + MathHelper.PiOver2, tex.Size() / 2f, new Vector2(scale * 0.5f, scale * 0.7f), SpriteEffects.None, 0f);
        }

        // thin glowing line using the RubbleTelegraph strip
        public static void Beam(SpriteBatch sb, Vector2 worldStart, float rotation, float length, float width, Color color)
        {
            Texture2D tex = BeamTexture;
            Color c = new Color(color.R, color.G, color.B, 0);

            sb.Draw(tex, worldStart - Main.screenPosition, null, c, rotation, new Vector2(0f, tex.Height / 2f), new Vector2(length / tex.Width, width / tex.Height), SpriteEffects.None, 0f);
        }
    }
}