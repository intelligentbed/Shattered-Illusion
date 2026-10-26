using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace ShatteredIllusion.Common.Systems
{
    /// <summary>
    /// Shared drawing + dust helpers so every weapon gets the same glowing look
    /// without needing extra sprite files.
    ///
    /// The trick used everywhere: a Color with Alpha = 0 drawn with the normal
    /// (premultiplied) blend mode ADDS light instead of covering things, which
    /// is what makes glows and trails look bright. <see cref="Additive"/> builds that color.
    /// </summary>
    public class WeaponFX : ModSystem
    {
        private static Texture2D softGlow;

        /// <summary>A soft white circle (64x64) generated in code, so no .png is needed.</summary>
        public static Texture2D SoftGlow
        {
            get
            {
                if (softGlow == null || softGlow.IsDisposed)
                    softGlow = CreateGlow(64);
                return softGlow;
            }
        }

        public override void Unload()
        {
            Texture2D tex = softGlow;
            softGlow = null;
            if (tex != null)
                Main.QueueMainThreadAction(() => tex.Dispose());
        }

        private static Texture2D CreateGlow(int size)
        {
            Texture2D tex = new Texture2D(Main.instance.GraphicsDevice, size, size);
            Color[] data = new Color[size * size];
            float center = (size - 1) / 2f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center)) / center;
                    float a = MathHelper.Clamp(1f - distance, 0f, 1f);
                    a *= a;
                    data[y * size + x] = Color.White * a; // premultiplied
                }
            }

            tex.SetData(data);
            return tex;
        }

        // ---------------------------------------------------------------- drawing

        /// <summary>Turns a color into an "adds light" color (alpha 0), scaled by opacity.</summary>
        public static Color Additive(Color color, float opacity = 1f)
        {
            Color c = color;
            c.A = 0;
            return c * opacity;
        }

        /// <summary>Draws a soft glow centered on a SCREEN position. scale 1 = 64px wide.</summary>
        public static void DrawGlow(Vector2 screenPosition, Color additiveColor, float scale)
        {
            Texture2D tex = SoftGlow;
            Main.spriteBatch.Draw(tex, screenPosition, null, additiveColor, 0f, tex.Size() * 0.5f, scale, SpriteEffects.None, 0f);
        }

        /// <summary>Glow stamps along a projectile's old positions (needs TrailCacheLength set).</summary>
        public static void DrawGlowTrail(Projectile p, Color head, Color tail, float scale, float opacity = 1f)
        {
            int length = p.oldPos.Length;
            for (int i = 0; i < length; i++)
            {
                if (p.oldPos[i] == Vector2.Zero)
                    continue;

                float k = 1f - i / (float)length;
                Color c = Additive(Color.Lerp(tail, head, k), opacity * k);
                DrawGlow(p.oldPos[i] + p.Size * 0.5f - Main.screenPosition, c, scale * (0.4f + 0.6f * k));
            }
        }

        /// <summary>A thin glowing line between two WORLD positions.</summary>
        public static void DrawLine(Vector2 worldStart, Vector2 worldEnd, Color additiveColor, float thickness)
        {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Vector2 delta = worldEnd - worldStart;
            Main.EntitySpriteDraw(pixel, worldStart - Main.screenPosition, new Rectangle(0, 0, 1, 1), additiveColor,
                delta.ToRotation(), new Vector2(0f, 0.5f), new Vector2(delta.Length(), thickness), SpriteEffects.None, 0);
        }

        /// <summary>Soft pulsing aura behind an item lying on the ground. Call from PostDrawInWorld.</summary>
        public static void DrawWorldAura(Item item, float rotation, float scale, Color color)
        {
            Texture2D tex = TextureAssets.Item[item.type].Value;
            Vector2 center = item.Center - Main.screenPosition;
            float pulse = 0.8f + 0.2f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 3f + item.whoAmI);
            float size = Math.Max(tex.Width, tex.Height) / 64f;

            DrawGlow(center, Additive(color, 0.45f * pulse), size * 1.8f * scale);

            for (int i = 0; i < 4; i++)
            {
                Vector2 offset = (MathHelper.PiOver2 * i + Main.GlobalTimeWrappedHourly).ToRotationVector2() * 2f;
                Main.spriteBatch.Draw(tex, center + offset, null, Additive(color, 0.3f * pulse), rotation,
                    tex.Size() * 0.5f, scale, SpriteEffects.None, 0f);
            }
        }

        // ---------------------------------------------------------------- dust

        public static void DustRing(Vector2 center, int dustType, int count, float speed, float scale = 1f, bool noGravity = true)
        {
            for (int i = 0; i < count; i++)
            {
                Vector2 velocity = (MathHelper.TwoPi * i / count).ToRotationVector2() * speed;
                Dust d = Dust.NewDustPerfect(center, dustType, velocity, 0, default, scale);
                d.noGravity = noGravity;
            }
        }

        public static void DustCone(Vector2 position, Vector2 direction, float spread, int dustType, int count,
            float minSpeed, float maxSpeed, float scale = 1f)
        {
            Vector2 dir = direction.SafeNormalize(Vector2.UnitX);
            for (int i = 0; i < count; i++)
            {
                Vector2 velocity = dir.RotatedByRandom(spread) * Main.rand.NextFloat(minSpeed, maxSpeed);
                Dust d = Dust.NewDustPerfect(position, dustType, velocity, 0, default, scale);
                d.noGravity = true;
            }
        }
    }
}
