using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.UI;

namespace ShatteredIllusion.Common.Cutscenes
{
    /// <summary>
    /// Shared animated title renderer for boss cutscenes and Cinema title cards.
    /// </summary>
    public static class CutsceneTitleRenderer
    {
        private const float MaxScale = 1.1f;
        private const float MaxWidthFraction = 0.6f;
        private const float SubtitleScale = 1.1f;
        private const float SubtitleTracking = 5f;
        private const int LetterStagger = 2;
        private const float BarFraction = 0.12f;

        public static void Draw(
            SpriteBatch spriteBatch,
            Texture2D pixel,
            string title,
            string subtitle,
            float timer,
            float titleStart,
            float fadeOutStart,
            float fadeOutTicks,
            float width,
            float height,
            Color accent)
        {
            if (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(subtitle))
                return;

            title ??= "";
            subtitle ??= "";

            float globalAlpha = 1f;
            if (timer > fadeOutStart && fadeOutTicks > 0f)
                globalAlpha = 1f - Ease((timer - fadeOutStart) / fadeOutTicks);

            if (globalAlpha <= 0f || timer < titleStart)
                return;

            var titleFont = FontAssets.DeathText.Value;
            var subtitleFont = FontAssets.MouseText.Value;

            float[] widths = new float[title.Length];
            float titleWidth = 0f;
            for (int i = 0; i < title.Length; i++)
            {
                widths[i] = titleFont.MeasureString(title[i].ToString()).X;
                titleWidth += widths[i];
            }

            float scale = titleWidth > 0f
                ? Math.Min(MaxScale, width * MaxWidthFraction / titleWidth)
                : MaxScale;

            float titleHeight = titleFont.MeasureString("A").Y * scale;
            float subtitleHeight = subtitleFont.MeasureString("A").Y * SubtitleScale;
            float letterboxHeight = height * BarFraction;

            float x0 = width * 0.08f;
            float subtitleTop = height - letterboxHeight - 36f - subtitleHeight;
            float titleTop = subtitleTop - 18f - titleHeight;
            float ruleY = titleTop + titleHeight + 6f;

            float x = x0;
            for (int i = 0; i < title.Length; i++)
            {
                float letterAlpha = Ease((timer - titleStart - i * LetterStagger) / 14f);
                if (letterAlpha > 0f)
                {
                    Utils.DrawBorderStringFourWay(
                        spriteBatch,
                        titleFont,
                        title[i].ToString(),
                        x,
                        titleTop + (1f - letterAlpha) * 18f,
                        Color.White * (letterAlpha * globalAlpha),
                        Color.Black * (letterAlpha * globalAlpha),
                        Vector2.Zero,
                        scale);
                }

                x += widths[i] * scale;
            }

            int lineStart = (int)(titleStart + title.Length * LetterStagger);
            float lineProgress = Ease((timer - lineStart) / 20f);
            if (lineProgress > 0f)
            {
                const int segments = 32;
                float lineWidth = titleWidth * scale * lineProgress;
                float segmentWidth = lineWidth / segments;

                for (int i = 0; i < segments; i++)
                {
                    float fade = 1f - i / (float)segments;
                    spriteBatch.Draw(
                        pixel,
                        new Rectangle((int)(x0 + segmentWidth * i), (int)ruleY,
                            (int)Math.Ceiling(segmentWidth) + 1, 2),
                        accent * (fade * globalAlpha));
                }
            }

            float subtitleProgress = Ease((timer - lineStart - 8f) / 20f);
            if (subtitleProgress <= 0f || subtitle.Length == 0)
                return;

            float subtitleX = x0 - (1f - subtitleProgress) * 12f;
            foreach (char character in subtitle)
            {
                string letter = character.ToString();
                Utils.DrawBorderStringFourWay(
                    spriteBatch,
                    subtitleFont,
                    letter,
                    subtitleX,
                    subtitleTop,
                    accent * (subtitleProgress * globalAlpha),
                    Color.Black * (subtitleProgress * globalAlpha * 0.85f),
                    Vector2.Zero,
                    SubtitleScale);

                subtitleX += (subtitleFont.MeasureString(letter).X + SubtitleTracking) * SubtitleScale;
            }
        }

        private static float Ease(float value)
        {
            value = MathHelper.Clamp(value, 0f, 1f);
            return value * value * value * (value * (value * 6f - 15f) + 10f);
        }
    }
}
