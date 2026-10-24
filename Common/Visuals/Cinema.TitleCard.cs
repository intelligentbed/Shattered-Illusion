using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using ShatteredIllusion.Common.Cutscenes;
using Terraria;
using Terraria.GameContent;

namespace ShatteredIllusion.Common.Visuals
{
    public static partial class Cinema
    {
        private const float TitleBaseScale = 1.4f;
        private const float TitleMaxWidthFraction = 0.7f;
        private const float TitleLineGap = 24f;
        private const float SubtitleScale = 1.2f;
        private const float SubtitleTracking = 6f;
        private const float TitleBackdropPadding = 52f;

        private static string titleMain = "", titleSub = "";
        private static int titleTimer, titleDuration;
        private static TitleStyle titleStyle = TitleStyle.Default;

        public static void Title(string main, string sub, int frames, TitleStyle style = null)
        {
            if (!(Enabled && Cfg.TitleCard))
                return;

            titleMain = main;
            titleSub = sub;
            titleDuration = frames;
            titleTimer = frames;
            titleStyle = style ?? TitleStyle.Default;
        }

        private static void DrawTitleCard(SpriteBatch sb, Texture2D px, float w, float h)
        {
            float elapsed = titleDuration - titleTimer;

            CutsceneTitleRenderer.Draw(
                sb,
                px,
                titleMain,
                (titleSub ?? "").ToUpperInvariant(),
                elapsed,
                0f,
                titleDuration - 30f,
                30f,
                w,
                h,
                titleStyle.Accent);
        }


        private static float TrackedWidth(DynamicSpriteFont font, string text, float tracking)
        {
            if (string.IsNullOrEmpty(text)) return 0f;

            float width = 0f;
            foreach (char c in text)
                width += font.MeasureString(c.ToString()).X;

            return width + tracking * (text.Length - 1);
        }

        private static void DrawFadingRule(SpriteBatch sb, Texture2D px, float cx, float y, float width, int thickness, Color color)
        {
            if (width < 2f) return;

            const int segments = 48;
            float segW = width / segments;
            float left = cx - width * 0.5f;

            for (int i = 0; i < segments; i++)
            {
                float t = (i + 0.5f) / segments;
                float dist = Math.Abs(t - 0.5f) * 2f;

                sb.Draw(px,
                    new Rectangle((int)(left + segW * i), (int)y, (int)Math.Ceiling(segW) + 1, thickness),
                    color * (1f - dist * dist));
            }
        }

        private static void DrawDiamond(SpriteBatch sb, Texture2D px, Vector2 center, float size, Color color)
        {
            sb.Draw(px, center, null, color, MathHelper.PiOver4, new Vector2(0.5f, 0.5f), size, SpriteEffects.None, 0f);
        }

        private static void DrawCrest(SpriteBatch sb, Texture2D px, Vector2 center, float alpha, float size, Color accent, Color accentDeep)
        {
            DrawDiamond(sb, px, center, 15f * size, Color.Black * (alpha * 0.85f));
            DrawDiamond(sb, px, center, 11f * size, accentDeep * alpha);
            DrawDiamond(sb, px, center, 7f * size, accent * alpha);
            DrawDiamond(sb, px, center, 3f * size, Color.White * alpha);

            for (int side = -1; side <= 1; side += 2)
            {
                Vector2 p = center + new Vector2(side * 30f * size, 0f);
                DrawDiamond(sb, px, p, 8f * size, Color.Black * (alpha * 0.85f));
                DrawDiamond(sb, px, p, 5f * size, accent * alpha);
            }
        }

        private static void DrawString(SpriteBatch sb, DynamicSpriteFont font, string text, Vector2 center, Color color, float scale)
        {
            if (string.IsNullOrEmpty(text)) return;

            sb.DrawString(font, text, center, color, 0f, font.MeasureString(text) * 0.5f, scale, SpriteEffects.None, 0f);
        }

        private static void DrawOutlined(SpriteBatch sb, DynamicSpriteFont font, string text, Vector2 center, Color color, Color outline, float scale)
        {
            if (string.IsNullOrEmpty(text)) return;

            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                    if (dx != 0 || dy != 0)
                        DrawString(sb, font, text, center + new Vector2(dx, dy), outline, scale);

            DrawString(sb, font, text, center, color, scale);
        }
    }
}