using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace ShatteredIllusion.Common.Visuals.ThreeD
{
    public static class ThreeDDraw
    {
        public const float FarFadeDistance = 800f;
        public const float FarBrightness = 0.4f;

        private const int ShadowTextureSize = 64;
        private static Texture2D shadowTexture;

        public static Texture2D ShadowTexture
        {
            get
            {
                if (shadowTexture == null || shadowTexture.IsDisposed)
                    shadowTexture = CreateShadowTexture();

                return shadowTexture;
            }
        }

        private static Texture2D CreateShadowTexture()
        {
            Texture2D texture = new(Main.graphics.GraphicsDevice, ShadowTextureSize, ShadowTextureSize);
            Color[] data = new Color[ShadowTextureSize * ShadowTextureSize];

            for (int y = 0; y < ShadowTextureSize; y++)
            {
                for (int x = 0; x < ShadowTextureSize; x++)
                {
                    float dx = (x + 0.5f) / ShadowTextureSize * 2f - 1f;
                    float dy = (y + 0.5f) / ShadowTextureSize * 2f - 1f;
                    float falloff = MathHelper.Clamp(1f - MathF.Sqrt(dx * dx + dy * dy), 0f, 1f);
                    falloff = falloff * falloff * (3f - 2f * falloff);
                    data[y * ShadowTextureSize + x] = Color.White * falloff;
                }
            }

            texture.SetData(data);
            return texture;
        }

        public static void Unload()
        {
            Texture2D texture = shadowTexture;
            shadowTexture = null;

            if (texture != null)
                Main.QueueMainThreadAction(texture.Dispose);
        }

        // 1 at the player's plane and in front, FarBrightness at FarFadeDistance behind it.
        public static float DepthBrightness(float z)
        {
            float t = MathHelper.Clamp((z + FarFadeDistance) / FarFadeDistance, 0f, 1f);
            return MathHelper.Lerp(FarBrightness, 1f, t);
        }

        public static Color DepthTint(float z)
        {
            float b = DepthBrightness(z);
            return new Color(b, b, b, 1f);
        }

        public static void DrawSprite(SpriteBatch sb, in View3 view, Texture2D texture, Vector3 position, Color color, float rotation, float baseScale, Rectangle? frame = null)
        {
            Vector2 screenPos = ThreeDMath.Project(position, view, out float depthScale);
            Rectangle source = frame ?? texture.Bounds;

            sb.Draw(texture, screenPos, source, color, rotation, source.Size() * 0.5f, baseScale * depthScale, SpriteEffects.None, 0f);
        }

        // World-space convenience for code that runs inside a vanilla draw pass.
        public static void DrawSprite(Texture2D texture, Vector3 position, Color color, float rotation, float baseScale, Rectangle? frame = null)
            => DrawSprite(Main.spriteBatch, View3.World, texture, position, color, rotation, baseScale, frame);

        // groundPosition is a real world position and width is already perspective-scaled.
        public static void DrawShadow(SpriteBatch sb, Vector2 groundPosition, float width, float opacity = 0.4f)
        {
            float horizontal = width / ShadowTextureSize;

            sb.Draw(ShadowTexture, groundPosition - Main.screenPosition, null, Color.Black * opacity, 0f,
                new Vector2(ShadowTextureSize * 0.5f), new Vector2(horizontal, horizontal * 0.35f), SpriteEffects.None, 0f);
        }

        public static void DrawDangerHalo(SpriteBatch sb, in View3 view, Texture2D texture, Vector3 position, float rotation, float baseScale, Rectangle? frame = null, float opacity = 1f)
            => DrawSprite(sb, view, texture, position, new Color(255, 60, 60) * (0.5f * opacity), rotation, baseScale * 1.25f, frame);
    }
}
