using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
<<<<<<< Updated upstream
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.UI;
=======
using ReLogic.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
>>>>>>> Stashed changes

namespace ShatteredIllusion.Common.Cutscenes
{
    /// <summary>
    /// Shared animated title renderer for boss cutscenes and Cinema title cards.
<<<<<<< Updated upstream
    /// </summary>
    public static class CutsceneTitleRenderer
    {
        private const float MaxScale = 1.1f;
        private const float MaxWidthFraction = 0.6f;
        private const float SubtitleScale = 1.1f;
        private const float SubtitleTracking = 5f;
        private const int LetterStagger = 2;
        private const float BarFraction = 0.12f;

=======
    /// Minimal layout: no frames or shapes, the animation does the work.
    /// </summary>
    public static class CutsceneTitleRenderer
    {
        // true = centered, false = left aligned.
        private const bool CenterAligned = true;

        private const float MaxScale = 1.6f;
        private const float MaxWidthFraction = 0.55f;

        // Letters start tight and breathe outward as they land, then keep drifting very slowly.
        private const float TrackingStart = 1f;
        private const float TrackingSettled = 11f;
        private const float TrackingDriftPerTick = 0.02f;
        private const float TrackingDriftMax = 6f;

        private const int LetterStagger = 2;
        private const float LetterFadeTicks = 13f;
        private const float SplitTicks = 30f;

        private const float SubtitleScale = 1.0f;
        private const float SubtitleTracking = 10f;
        private const int SubtitleStagger = 1;

        private const float BarFraction = 0.12f;

        private static readonly Color TitleColor = new Color(255, 248, 232);
        private static readonly Color SplitWarm = new Color(255, 80, 70);
        private static readonly Color SplitCool = new Color(70, 200, 255);

        private static Texture2D backdropTex;

        public static void Unload()
        {
            Main.QueueMainThreadAction(() =>
            {
                backdropTex?.Dispose();
                backdropTex = null;
            });
        }

>>>>>>> Stashed changes
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

<<<<<<< Updated upstream
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

=======
            float elapsed = timer - titleStart;

            // ---- measure title ----
            float[] widths = new float[title.Length];
            float rawWidth = 0f;
            for (int i = 0; i < title.Length; i++)
            {
                widths[i] = titleFont.MeasureString(title[i].ToString()).X;
                rawWidth += widths[i];
            }

            int gaps = Math.Max(0, title.Length - 1);
            float maxTracking = TrackingSettled + TrackingDriftMax;

            // Fit using the widest the title will ever get so it never outgrows the screen.
            float scale = rawWidth > 0f
                ? Math.Min(MaxScale, (width * MaxWidthFraction - maxTracking * gaps) / rawWidth)
                : MaxScale;
            scale = Math.Max(scale, 0.3f);

            float tracking = MathHelper.Lerp(TrackingStart, TrackingSettled, Ease(elapsed / 46f))
                           + Math.Min(Math.Max(0f, elapsed - 46f) * TrackingDriftPerTick, TrackingDriftMax);

            float titleWidth = rawWidth * scale + tracking * gaps;
            float titleHeight = titleFont.MeasureString("A").Y * scale;
            float subtitleHeight = subtitleFont.MeasureString("A").Y * SubtitleScale;

            float subtitleWidth = SubtitleWidth(subtitleFont, subtitle);
            float blockWidth = Math.Max(titleWidth, subtitleWidth);

            // ---- layout ----
            float barHeight = height * BarFraction;
            float bottom = height - barHeight - 56f;
            float subtitleTop = bottom - subtitleHeight;
            float titleTop = subtitleTop - 20f - titleHeight;

            float cx = CenterAligned ? width * 0.5f : width * 0.08f + blockWidth * 0.5f;

            // ---- soft backdrop so text stays readable on bright scenes ----
            float backdropAlpha = Ease(elapsed / 16f) * globalAlpha;
            if (backdropAlpha > 0.01f)
            {
                float bw = blockWidth + 560f;
                float bh = (bottom - titleTop) + 260f;
                float by = (titleTop + bottom) * 0.5f;

                spriteBatch.Draw(
                    GetBackdrop(spriteBatch.GraphicsDevice),
                    new Rectangle((int)(cx - bw * 0.5f), (int)(by - bh * 0.5f), (int)bw, (int)bh),
                    Color.White * backdropAlpha);
            }

            // ---- title letters ----
            int titleEnd = (int)(titleStart + title.Length * LetterStagger);
            float titleX = cx - titleWidth * 0.5f;
            float x = titleX;

            for (int i = 0; i < title.Length; i++)
            {
                float t = (timer - titleStart - i * LetterStagger) / LetterFadeTicks;
                float letterAlpha = Ease(t);

                if (letterAlpha > 0f)
                {
                    string s = title[i].ToString();
                    float a = letterAlpha * globalAlpha;
                    float y = titleTop + (1f - letterAlpha) * 20f;
                    Vector2 pos = new Vector2(x, y);

                    // Red / cyan split that converges into the letter as it settles.
                    float splitT = MathHelper.Clamp((timer - titleStart - i * LetterStagger) / SplitTicks, 0f, 1f);
                    float split = 1f - Ease(splitT);
                    if (split > 0.01f)
                    {
                        float off = split * 12f * scale;
                        float k = split * 0.55f * a;

                        spriteBatch.DrawString(titleFont, s, pos + new Vector2(-off, 0f), Additive(SplitWarm, k), 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                        spriteBatch.DrawString(titleFont, s, pos + new Vector2(off, 0f), Additive(SplitCool, k), 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                    }

                    // Halo: flares as the letter lands, then settles to a faint glow that slowly breathes.
                    float flash = (float)Math.Sin(MathHelper.Clamp(t, 0f, 1f) * MathHelper.Pi);
                    float breathe = 0.5f + 0.5f * (float)Math.Sin(timer * 0.06f + i * 0.45f);
                    float glow = (0.07f + 0.05f * breathe + 0.30f * flash) * a;

                    Color glowColor = Additive(accent, glow);
                    for (int gx = -1; gx <= 1; gx++)
                        for (int gy = -1; gy <= 1; gy++)
                            if (gx != 0 || gy != 0)
                                spriteBatch.DrawString(titleFont, s, pos + new Vector2(gx * 3f, gy * 3f), glowColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

                    Color wide = Additive(accent, glow * 0.45f);
                    for (int gx = -1; gx <= 1; gx++)
                        for (int gy = -1; gy <= 1; gy++)
                            if (gx != 0 || gy != 0)
                                spriteBatch.DrawString(titleFont, s, pos + new Vector2(gx * 6f, gy * 6f), wide, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

                    // Drop shadow, then the outlined letter with a faint warm shimmer.
                    spriteBatch.DrawString(titleFont, s, pos + new Vector2(2f, 4f), Color.Black * (0.55f * a), 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

                    float shimmer = 0.12f + 0.10f * (float)Math.Sin(timer * 0.05f + i * 0.6f);
                    Color fill = Color.Lerp(TitleColor, accent, shimmer);

                    Utils.DrawBorderStringFourWay(
                        spriteBatch,
                        titleFont,
                        s,
                        x,
                        y,
                        fill * a,
                        Color.Black * a,
                        Vector2.Zero,
                        scale);

                    // One light glint sweeping left to right across the title.
                    float sweepT = (timer - titleEnd - 3f) / 32f;
                    if (sweepT > 0f && sweepT < 1f)
                    {
                        float sweepX = MathHelper.Lerp(titleX - 100f, titleX + titleWidth + 100f, Ease(sweepT));
                        float letterCenter = x + widths[i] * scale * 0.5f;
                        float k = Math.Max(0f, 1f - Math.Abs(letterCenter - sweepX) / 100f);
                        k *= k;

                        if (k > 0.01f)
                            spriteBatch.DrawString(titleFont, s, pos, Additive(Color.White, k * 0.85f * a), 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                    }
                }

                x += widths[i] * scale + tracking;
            }

            // ---- subtitle: types in letter by letter ----
            if (subtitle.Length == 0)
                return;

            float subStart = titleEnd + 4f;
            float sx = cx - subtitleWidth * 0.5f;

            for (int j = 0; j < subtitle.Length; j++)
            {
                string letter = subtitle[j].ToString();
                float st = (timer - subStart - j * SubtitleStagger) / 12f;
                float sa = Ease(st);

                if (sa > 0f)
                {
                    float a = sa * globalAlpha;
                    float sy = subtitleTop + (1f - sa) * 8f;

                    float flash = (float)Math.Sin(MathHelper.Clamp(st, 0f, 1f) * MathHelper.Pi);
                    Color glowColor = Additive(accent, (0.10f + 0.35f * flash) * a);

                    for (int gx = -1; gx <= 1; gx++)
                        for (int gy = -1; gy <= 1; gy++)
                            if (gx != 0 || gy != 0)
                                spriteBatch.DrawString(subtitleFont, letter, new Vector2(sx + gx * 2f, sy + gy * 2f), glowColor, 0f, Vector2.Zero, SubtitleScale, SpriteEffects.None, 0f);

                    Utils.DrawBorderStringFourWay(
                        spriteBatch,
                        subtitleFont,
                        letter,
                        sx,
                        sy,
                        accent * a,
                        Color.Black * (a * 0.9f),
                        Vector2.Zero,
                        SubtitleScale);
                }

                sx += (subtitleFont.MeasureString(letter).X + SubtitleTracking) * SubtitleScale;
            }
        }

        private static float SubtitleWidth(DynamicSpriteFont font, string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0f;

            float w = 0f;
            foreach (char c in text)
                w += (font.MeasureString(c.ToString()).X + SubtitleTracking) * SubtitleScale;

            return w - SubtitleTracking * SubtitleScale;
        }

        // Premultiplied color with zero alpha draws additively (adds light instead of covering).
        private static Color Additive(Color color, float strength)
        {
            Color c = color * strength;
            c.A = 0;
            return c;
        }

        // Smooth elliptical black falloff, generated once and stretched.
        private static Texture2D GetBackdrop(GraphicsDevice device)
        {
            if (backdropTex != null && !backdropTex.IsDisposed)
                return backdropTex;

            const int w = 256, h = 128;
            var data = new Color[w * h];

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w * 2f - 1f;
                    float v = (y + 0.5f) / h * 2f - 1f;
                    float d = (float)Math.Sqrt(u * u + v * v);

                    float t = MathHelper.Clamp(1f - d, 0f, 1f);
                    t = t * t * (3f - 2f * t);

                    byte a = (byte)(t * 0.58f * 255f + 0.5f);
                    data[y * w + x] = new Color((byte)0, (byte)0, (byte)0, a);
                }
            }

            backdropTex = new Texture2D(device, w, h);
            backdropTex.SetData(data);
            return backdropTex;
        }

>>>>>>> Stashed changes
        private static float Ease(float value)
        {
            value = MathHelper.Clamp(value, 0f, 1f);
            return value * value * value * (value * (value * 6f - 15f) + 10f);
        }
    }
<<<<<<< Updated upstream
}
=======
}
>>>>>>> Stashed changes
