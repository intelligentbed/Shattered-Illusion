using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace ShatteredIllusion.Common.GUI.JournalUI
{
    /// <summary>
    /// Page rendering: layout from localized text, the blank look of a sealed page, and the unlock reveal.
    /// A sealed page is reachable but empty. When it unlocks, the page "remembers": a warm haze rises and every
    /// letter surfaces on its own beat, ghosted and drifting, slowly sharpening into ink, like recalling a memory
    /// from decades ago.
    /// </summary>
    internal partial class JournalPanel
    {
        // Reveal timeline (seconds). A warm haze rises, then every letter surfaces on its own beat.
        private const float PageIntro = 0.5f;     // haze builds before the first letter appears
        private const float GateIntro = 0.2f;
        private const float FadeDur = 1.5f;       // how long one letter takes to come into focus
        private const float RevealTail = 0.6f;    // haze lingers a moment after the last letter lands

        private static readonly Color FadedInk = new Color(176, 138, 128);    // how a half-forgotten letter looks
        private static readonly Color MemoryTint = new Color(222, 168, 120);  // sepia haze / dust

        private readonly Dictionary<string, float> _revealStart = new Dictionary<string, float>();
        private readonly HashSet<string> _revealBurst = new HashSet<string>();   // reveals whose opening chime already played
        private readonly List<RevealUnit> _units = new List<RevealUnit>();
        private readonly Dictionary<JournalPage, PageLayout> _layouts = new Dictionary<JournalPage, PageLayout>();
        private string _layoutCulture;

        private sealed class RevealUnit
        {
            public string Key;
            public bool IsPage;
            public float T;
            public int Total;
            public int Running;
            public float Intro;
            public float Spread;   // window over which letters begin to surface
            public float Duration => Intro + Spread + FadeDur + RevealTail;
        }

        private sealed class LayoutLine
        {
            public string Text;
            public float[] CharX;
            public string GateId;
        }

        private sealed class PageLayout
        {
            public string Heading;
            public float[] HeadingX;
            public LayoutLine[] Lines;
        }

        private void ResetReveals()
        {
            _revealStart.Clear();
            _revealBurst.Clear();
        }

        private float RevealTime(string key)
        {
            if (!_revealStart.TryGetValue(key, out float start))
            {
                start = _clock;
                _revealStart[key] = start;
            }
            return _clock - start;
        }

        // ------------------------------------------------------------------ one page

        private void DrawPage(SpriteBatch sb, CalculatedStyle d, int index, bool right, float xs, bool animate, Vector2 mouse)
        {
            JournalPage page = JournalRegistry.Pages[index];
            float px = d.X + (right ? RightPageX : LeftPageX) * Scale;
            float py = d.Y + PageTextTop * Scale;
            float textWidth = PageTextWidth * Scale;

            // Page number, centered under the page (sealed and filler pages still count).
            DrawCenteredText(sb, (index + 1).ToString(), px + textWidth * 0.5f, d.Y + 312f * Scale, 0.55f, xs, PageTextColor);
            if (page.IsPadding)
            {
                return;
            }

            JournalPlayer jp = Main.LocalPlayer.GetModPlayer<JournalPlayer>();
            PageLayout layout = GetLayout(page);
            int n = layout.Lines.Length;
            Point mousePoint = new Point((int)mouse.X, (int)mouse.Y);

            // ---- Work out the state of the heading and every line. Index 0 is the heading.
            JournalState pageState = jp.StateOf(page);
            if (pageState == JournalState.Revealing && !animate)
            {
                pageState = JournalState.Locked; // stays blank until the reveal can actually play
            }

            JournalState[] states = new JournalState[n + 1];
            string[] keys = new string[n + 1];
            string pageKey = pageState == JournalState.Revealing ? page.Key : null;
            states[0] = pageState;
            keys[0] = pageKey;

            for (int i = 0; i < n; i++)
            {
                LayoutLine line = layout.Lines[i];
                JournalState st = pageState;
                string key = pageKey;

                if (line.GateId != null)
                {
                    JournalState gate = jp.StateOfGate(page, line.GateId);
                    if (pageState == JournalState.Open)
                    {
                        st = gate;
                        key = gate == JournalState.Revealing ? page.Key + "#" + line.GateId : null;
                        if (st == JournalState.Revealing && !animate)
                        {
                            st = JournalState.Locked;
                            key = null;
                        }
                    }
                    else if (gate != JournalState.Open)
                    {
                        st = JournalState.Locked; // a sealed paragraph waits for its own turn
                        key = null;
                    }
                }

                states[i + 1] = st;
                keys[i + 1] = key;
            }

            // ---- Collect reveal units (the page itself and/or individual paragraphs) and advance their clocks.
            _units.Clear();
            for (int k = 0; k <= n; k++)
            {
                if (states[k] != JournalState.Revealing)
                {
                    continue;
                }

                RevealUnit unit = _units.Find(u => u.Key == keys[k]);
                if (unit == null)
                {
                    unit = new RevealUnit { Key = keys[k], IsPage = keys[k] == page.Key, T = RevealTime(keys[k]) };
                    _units.Add(unit);
                }
                unit.Total += k == 0 ? layout.Heading.Length : layout.Lines[k - 1].Text.Length;
            }

            for (int u = _units.Count - 1; u >= 0; u--)
            {
                RevealUnit unit = _units[u];
                unit.Intro = unit.IsPage ? PageIntro : GateIntro;
                unit.Spread = MathHelper.Clamp(unit.Total * 0.03f, 1.4f, 3.2f);

                if (_revealBurst.Add(unit.Key))
                {
                    // One soft, low chime as the memory stirs.
                    SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.35f, Pitch = -0.45f });
                }

                if (unit.T >= unit.Duration)
                {
                    jp.MarkRevealed(unit.Key);
                    _revealStart.Remove(unit.Key);
                    _revealBurst.Remove(unit.Key);
                    for (int k = 0; k <= n; k++)
                    {
                        if (keys[k] == unit.Key)
                        {
                            states[k] = JournalState.Open;
                        }
                    }
                    _units.RemoveAt(u);
                }
            }

            // ---- Heading + divider. Sealed text isn't drawn at all: the page is simply blank.
            DrawStateLine(sb, layout.Heading, layout.HeadingX, MapX(px, py), TitleColor, TitleScale, xs, states[0], FindUnit(keys[0]));

            float dividerAlpha = states[0] == JournalState.Open ? 1f
                : states[0] == JournalState.Revealing ? MathHelper.Clamp((FindUnit(keys[0]).T - PageIntro) / 1.2f, 0f, 1f) : 0f;
            if (dividerAlpha > 0.01f)
            {
                Vector2 dividerPos = MapX(px, py + 32f);
                sb.Draw(JournalGfx.HGradient, new Rectangle((int)dividerPos.X, (int)dividerPos.Y, (int)(textWidth * 0.75f * xs), 2),
                    PageTextColor * (0.8f * dividerAlpha * _alpha));
            }

            // ---- Body.
            float y = py + 44f;
            for (int i = 0; i < n; i++)
            {
                LayoutLine line = layout.Lines[i];
                float lineY = y + i * PageLineHeight;
                DrawStateLine(sb, line.Text, line.CharX, MapX(px, lineY), PageTextColor, PageTextScale, xs, states[i + 1], FindUnit(keys[i + 1]));

                // A sealed paragraph is invisible, but hovering where it sits still says how to open it.
                if (animate && line.GateId != null && states[i + 1] == JournalState.Locked &&
                    new Rectangle((int)px, (int)lineY, (int)textWidth, (int)PageLineHeight).Contains(mousePoint))
                {
                    _hoverHint = JournalText.HintFor(line.GateId) ?? JournalText.Ui("DefaultHint");
                }
            }

            // Hovering a sealed (blank) page shows its hint as a tooltip.
            if (animate && pageState == JournalState.Locked &&
                new Rectangle((int)px, (int)py, (int)textWidth, (int)(PageLineHeight * 16f)).Contains(mousePoint))
            {
                _hoverHint = JournalText.LockedHint(page);
            }

            // ---- The haze of remembering, over everything.
            foreach (RevealUnit unit in _units)
            {
                DrawMemoryHaze(sb, d, px, py, textWidth, xs, unit);
            }
        }

        private RevealUnit FindUnit(string key) => key == null ? null : _units.Find(u => u.Key == key);

        // ------------------------------------------------------------------ text in each state

        private void DrawStateLine(SpriteBatch sb, string text, float[] charX, Vector2 pos, Color color, float scale, float xs,
            JournalState state, RevealUnit unit)
        {
            int len = text.Length;
            if (len == 0 || state == JournalState.Locked)
            {
                if (unit != null) unit.Running += len;
                return;
            }

            if (state == JournalState.Open || unit == null)
            {
                DrawText(sb, text, pos, color, scale, xs);
                return;
            }

            DrawMemoryLine(sb, text, charX, pos, color, scale, xs, unit);
            unit.Running += len;
        }

        // Smooth 0..1 ramp.
        private static float Smooth(float t)
        {
            t = MathHelper.Clamp(t, 0f, 1f);
            return t * t * (3f - 2f * t);
        }

        // When letter number `index` (of `total`) starts to surface, in seconds after the intro.
        // Mostly left-to-right with a lot of noise, so it reads as recollection, not typing.
        private static float CharStart(int index, int total, float spread)
        {
            float order = index / (float)Math.Max(1, total - 1);
            float noise = (Hash(index * 2654 + 17) % 1000u) / 1000f;
            return (0.6f * order + 0.4f * noise) * spread;
        }

        // Letters that are done are drawn in contiguous runs (cheap); only letters mid-surfacing are drawn one by one.
        private void DrawMemoryLine(SpriteBatch sb, string text, float[] charX, Vector2 pos, Color color, float scale, float xs, RevealUnit unit)
        {
            int len = text.Length;
            float step = scale * xs;
            float since = unit.T - unit.Intro;
            int runStart = -1;

            for (int i = 0; i < len; i++)
            {
                float p = since < 0f ? 0f : MathHelper.Clamp((since - CharStart(unit.Running + i, unit.Total, unit.Spread)) / FadeDur, 0f, 1f);
                bool settled = p >= 1f || text[i] == ' ';

                if (settled)
                {
                    if (runStart < 0) runStart = i;
                    continue;
                }

                if (runStart >= 0)
                {
                    DrawText(sb, text.Substring(runStart, i - runStart), pos + new Vector2(charX[runStart] * step, 0f), color, scale, xs);
                    runStart = -1;
                }

                if (p > 0.001f)
                {
                    DrawSurfacingChar(sb, text[i].ToString(), pos + new Vector2(charX[i] * step, 0f),
                        (charX[i + 1] - charX[i]) * step, color, scale, xs, p, unit.Running + i);
                }
            }

            if (runStart >= 0)
            {
                DrawText(sb, text.Substring(runStart), pos + new Vector2(charX[runStart] * step, 0f), color, scale, xs);
            }
        }

        // One letter coming into focus: a warm smudge of light, a double image that converges, a slow rise into place,
        // and ink that goes from faded to full.
        private void DrawSurfacingChar(SpriteBatch sb, string ch, Vector2 at, float width, Color ink, float scale, float xs, float p, int seed)
        {
            float e = Smooth(p);
            float inv = 1f - e;
            float alpha = (float)Math.Pow(p, 1.6);
            float waver = 0.88f + 0.12f * (float)Math.Sin(_clock * 3.1f + seed * 1.7f); // a letter that isn't quite sure yet
            Color color = Color.Lerp(FadedInk, ink, e);

            // Soft halo: the letter is a smear of warmth before it is a shape.
            int halo = (int)((18f + 24f * inv) * xs);
            sb.Draw(JournalGfx.Soft,
                new Rectangle((int)(at.X + width * 0.5f - halo / 2f), (int)(at.Y + 10f - halo / 2f), Math.Max(2, halo), (int)(18f + 24f * inv)),
                MemoryTint * (0.22f * inv * Math.Min(1f, p * 6f) * _alpha));

            // Double vision: two ghost copies that drift together as the memory sharpens.
            float angle = (Hash(seed * 31 + 5) % 628u) / 100f;
            Vector2 dir = new Vector2((float)Math.Cos(angle) * xs, (float)Math.Sin(angle));
            float spread = inv * inv * 7f;
            Vector2 rise = new Vector2(0f, inv * 4f); // settles downward into its line

            DrawText(sb, ch, at + rise + dir * spread, color * (0.30f * alpha * waver), scale, xs, alpha * 0.3f);
            DrawText(sb, ch, at + rise - dir * spread * 0.7f, color * (0.22f * alpha * waver), scale, xs, alpha * 0.2f);
            DrawText(sb, ch, at + rise, color * (alpha * waver), scale, xs, alpha);
        }

        private static uint Hash(int value)
        {
            uint x = unchecked((uint)value);
            x ^= x >> 16;
            x = unchecked(x * 0x7feb352du);
            x ^= x >> 15;
            x = unchecked(x * 0x846ca68bu);
            x ^= x >> 16;
            return x;
        }

        // ------------------------------------------------------------------ the haze

        // A sepia wash that rises over the page, with dust drifting up through it, then clears as the last letters land.
        private void DrawMemoryHaze(SpriteBatch sb, CalculatedStyle d, float px, float py, float textWidth, float xs, RevealUnit unit)
        {
            float rise = Smooth(unit.T / 0.9f);
            float clear = MathHelper.Clamp((unit.Duration - unit.T) / 1.1f, 0f, 1f);
            float k = Math.Min(rise, Smooth(clear));
            if (k <= 0.01f)
            {
                return;
            }

            float height = PageLineHeight * 16f + 60f;
            float cx = MapX(px + textWidth * 0.5f, 0f).X;
            float cy = py + height * 0.45f;
            float breathe = 0.85f + 0.15f * (float)Math.Sin(_clock * 1.3f);

            // Two overlapping blooms so the edges stay feathered.
            sb.Draw(JournalGfx.Soft,
                new Rectangle((int)(cx - (textWidth * 0.75f) * xs), (int)(cy - height * 0.6f), (int)(textWidth * 1.5f * xs), (int)(height * 1.2f)),
                MemoryTint * (0.30f * k * breathe * _alpha));
            sb.Draw(JournalGfx.Soft,
                new Rectangle((int)(cx - (textWidth * 0.45f) * xs), (int)(cy - height * 0.35f), (int)(textWidth * 0.9f * xs), (int)(height * 0.7f)),
                Color.Lerp(MemoryTint, Color.White, 0.35f) * (0.22f * k * _alpha));

            // Dust that catches the light as it rises. Only the page's own reveal gets it.
            if (!unit.IsPage)
            {
                return;
            }

            for (int i = 0; i < 16; i++)
            {
                uint h = Hash(i * 613 + 41);
                float speed = 10f + (h % 14u);
                float life = ((unit.T * speed + (h >> 8) % 300u) % 300f) / 300f;
                float x = cx + (((h >> 4) % 1000u) / 1000f - 0.5f) * textWidth * 0.95f * xs + (float)Math.Sin(_clock * 0.9f + i) * 10f * xs;
                float yy = py + height - life * height;
                float twinkle = 0.5f + 0.5f * (float)Math.Sin(_clock * 2.4f + i * 1.9f);
                int size = 6 + (int)(h % 7u);
                sb.Draw(JournalGfx.Soft, new Rectangle((int)x - size, (int)yy - size, size * 2, size * 2),
                    Color.Lerp(MemoryTint, Color.White, 0.5f) * (0.5f * k * twinkle * (float)Math.Sin(life * Math.PI) * _alpha));
            }
        }

        // ------------------------------------------------------------------ layout from localization

        private PageLayout GetLayout(JournalPage page)
        {
            string culture = JournalText.CultureStamp;
            if (culture != _layoutCulture)
            {
                _layoutCulture = culture;
                _layouts.Clear();
            }

            if (_layouts.TryGetValue(page, out PageLayout cached))
            {
                return cached;
            }

            DynamicSpriteFont font = FontAssets.MouseText.Value;
            float textWidth = PageTextWidth * Scale;
            List<LayoutLine> lines = new List<LayoutLine>();

            foreach (BodyParagraph paragraph in JournalText.Body(page))
            {
                foreach (string wrapped in WrapText(paragraph.Text, PageTextScale, textWidth))
                {
                    lines.Add(new LayoutLine { Text = wrapped, GateId = paragraph.GateId });
                }
            }

            if (lines.Count > MaxPageLines)
            {
                JournalBuilder.Warn($"Page '{page.Key}' has {lines.Count} lines but only {MaxPageLines} fit; the rest is cut off. Split it into another page.");
                lines.RemoveRange(MaxPageLines, lines.Count - MaxPageLines);
            }

            foreach (LayoutLine line in lines)
            {
                line.CharX = MeasurePrefixes(font, line.Text);
            }

            string heading = JournalText.Heading(page);
            PageLayout layout = new PageLayout
            {
                Heading = heading,
                HeadingX = MeasurePrefixes(font, heading),
                Lines = lines.ToArray(),
            };

            _layouts[page] = layout;
            return layout;
        }

        // x offset (unscaled font units) of every character boundary, so partial lines can be positioned exactly.
        private static float[] MeasurePrefixes(DynamicSpriteFont font, string text)
        {
            float[] x = new float[text.Length + 1];
            for (int i = 1; i <= text.Length; i++)
            {
                x[i] = font.MeasureString(text.Substring(0, i)).X;
            }
            return x;
        }

        private static string[] WrapText(string text, float scale, float maxWidth)
        {
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            List<string> lines = new List<string>();

            foreach (string paragraph in text.Split('\n'))
            {
                if (string.IsNullOrWhiteSpace(paragraph))
                {
                    lines.Add(string.Empty);
                    continue;
                }

                string line = string.Empty;
                foreach (string word in paragraph.Split(' '))
                {
                    string candidate = string.IsNullOrEmpty(line) ? word : $"{line} {word}";
                    if (!string.IsNullOrEmpty(line) && font.MeasureString(candidate).X * scale > maxWidth)
                    {
                        lines.Add(line);
                        line = word;
                    }
                    else
                    {
                        line = candidate;
                    }
                }

                if (!string.IsNullOrEmpty(line))
                {
                    lines.Add(line);
                }
            }

            return lines.ToArray();
        }
    }
}
