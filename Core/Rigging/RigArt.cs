using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace ShatteredIllusion.Core.Rigging
{
    /// <summary>
    /// Tiny procedural textures the leg renderer shades with, built lazily on first draw (so always on the main thread) and shared by every rig.
    /// Both are greyscale and premultiplied, so a <see cref="Color"/> tint gives the limb its colour: bright core, dark rim, soft 1px edge
    /// <br/>Call <see cref="LegRig.Unload"/> from your ModSystem.Unload
    /// </summary>
    internal static class RigArt
    {
        public const int StripHeight = 32;
        public const int DiscSize = 32;

        private static Texture2D strip, disc;

        /// <summary>1 x 32 cross-section of a round tube (bright centre, dark rim). Stretch along the bone, scale Y to the width</summary>
        public static Texture2D Strip
        {
            get
            {
                if (strip == null || strip.IsDisposed)
                    strip = BuildStrip();
                return strip;
            }
        }

        /// <summary>32 x 32 shaded ball with a soft edge: joint caps, claw tips, contact shadows (tinted black)</summary>
        public static Texture2D Disc
        {
            get
            {
                if (disc == null || disc.IsDisposed)
                    disc = BuildDisc();
                return disc;
            }
        }

        public static void Unload()
        {
            Texture2D a = strip, b = disc;
            strip = disc = null;
            if (a == null && b == null)
                return;

            Main.QueueMainThreadAction(() =>
            {
                a?.Dispose();
                b?.Dispose();
            });
        }

        private static Color Premult(float v, float a) => new Color(v * a, v * a, v * a, a);

        private static Texture2D BuildStrip()
        {
            Texture2D tex = new Texture2D(Main.instance.GraphicsDevice, 1, StripHeight);
            Color[] data = new Color[StripHeight];
            for (int y = 0; y < StripHeight; y++)
            {
                float ay = MathF.Abs((y + 0.5f - StripHeight * 0.5f) / (StripHeight * 0.5f));
                float z = MathF.Sqrt(MathF.Max(1f - ay * ay, 0f));
                float v = 0.46f + 0.54f * z;
                v *= 1f - 0.45f * RigUtil.Smooth(0.72f, 1f, ay);
                float a = MathHelper.Clamp((1f - ay) * 14f, 0f, 1f);
                data[y] = Premult(v, a);
            }
            tex.SetData(data);
            return tex;
        }

        private static Texture2D BuildDisc()
        {
            Texture2D tex = new Texture2D(Main.instance.GraphicsDevice, DiscSize, DiscSize);
            Color[] data = new Color[DiscSize * DiscSize];
            Vector3 light = Vector3.Normalize(new Vector3(-0.35f, -0.55f, 0.76f));
            float half = DiscSize * 0.5f;

            for (int y = 0; y < DiscSize; y++)
            {
                for (int x = 0; x < DiscSize; x++)
                {
                    float nx = (x + 0.5f - half) / half;
                    float ny = (y + 0.5f - half) / half;
                    float r = MathF.Sqrt(nx * nx + ny * ny);
                    float z = MathF.Sqrt(MathF.Max(1f - r * r, 0f));
                    float lam = MathF.Max(0f, nx * light.X + ny * light.Y + z * light.Z);
                    float v = 0.42f + 0.58f * MathF.Min(1f, lam / 0.76f);
                    v *= 1f - 0.45f * RigUtil.Smooth(0.72f, 1f, r);
                    float a = MathHelper.Clamp((1f - r) * 14f, 0f, 1f);
                    data[y * DiscSize + x] = Premult(v, a);
                }
            }
            tex.SetData(data);
            return tex;
        }
    }
}
