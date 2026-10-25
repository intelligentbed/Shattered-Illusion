using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace ShatteredIllusion.Common.GUI.JournalUI
{
    /// <summary>
    /// Procedurally generated textures (glow, ring, vignette, gradient) and easing helpers
    /// so the journal needs no extra art assets for its cinematic effects.
    /// All textures are premultiplied-alpha, so tint them with Color * alpha.
    /// </summary>
    internal static class JournalGfx
    {
        public static Texture2D Soft;      // soft radial blob (white)
        public static Texture2D Ring;      // thin glowing ring (white)
        public static Texture2D Vignette;  // transparent center, black edges
        public static Texture2D HGradient; // 64x1, opaque on the left fading to clear on the right

        private static bool _built;

        public static void EnsureBuilt()
        {
            if (_built || Main.dedServ)
            {
                return;
            }

            GraphicsDevice gd = Main.graphics.GraphicsDevice;

            Soft = MakeRadial(gd, 128, false, d => SmoothStep(1f - d));
            Ring = MakeRadial(gd, 128, false, d => SmoothStep(1f - Math.Abs(d - 0.85f) / 0.06f));
            Vignette = MakeRadial(gd, 128, true, d => (float)Math.Pow(MathHelper.Clamp((d - 0.5f) / 0.8f, 0f, 1f), 1.6f));

            HGradient = new Texture2D(gd, 64, 1);
            Color[] data = new Color[64];
            for (int x = 0; x < 64; x++)
            {
                float a = 1f - x / 63f;
                a *= a;
                data[x] = new Color(a, a, a, a);
            }
            HGradient.SetData(data);

            _built = true;
        }

        public static void Dispose()
        {
            Main.QueueMainThreadAction(() =>
            {
                Soft?.Dispose();
                Ring?.Dispose();
                Vignette?.Dispose();
                HGradient?.Dispose();
                Soft = Ring = Vignette = HGradient = null;
                _built = false;
            });
        }

        private static Texture2D MakeRadial(GraphicsDevice gd, int size, bool black, Func<float, float> alphaOfDistance)
        {
            Texture2D tex = new Texture2D(gd, size, size);
            Color[] data = new Color[size * size];
            float half = size / 2f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - half) / half;
                    float dy = (y + 0.5f - half) / half;
                    float d = (float)Math.Sqrt(dx * dx + dy * dy);
                    float a = MathHelper.Clamp(alphaOfDistance(d), 0f, 1f);
                    data[y * size + x] = black ? new Color(0f, 0f, 0f, a) : new Color(a, a, a, a);
                }
            }

            tex.SetData(data);
            return tex;
        }

        private static float SmoothStep(float t)
        {
            t = MathHelper.Clamp(t, 0f, 1f);
            return t * t * (3f - 2f * t);
        }

        public static float EaseOutCubic(float t)
        {
            float inv = 1f - t;
            return 1f - inv * inv * inv;
        }

        public static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float p = t - 1f;
            return 1f + c3 * p * p * p + c1 * p * p;
        }

        public static float EaseInOutSine(float t)
        {
            return -((float)Math.Cos(Math.PI * t) - 1f) / 2f;
        }
    }
}
