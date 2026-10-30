using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using ShatteredIllusion.Common.Visuals;
using ShatteredIllusionKeybinds;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;
using ReLogic.Content;
using ReLogic.Graphics;

namespace ShatteredIllusion.Common.GUI.JournalUI
{
    public class JournalUISystem : ModSystem
    {
        internal JournalUIState JournalState;
        private UserInterface _journalInterface;

        public override void Load()
        {
            if (!Main.dedServ)
            {
                JournalRegistry.Build();
                JournalState = new JournalUIState();
                JournalState.Activate();
                _journalInterface = new UserInterface();
                _journalInterface.SetState(JournalState);
            }
        }

        public override void Unload()
        {
            JournalState = null;
            _journalInterface = null;
            JournalRegistry.Clear();
            JournalToast.Clear();
            if (!Main.dedServ)
            {
                JournalGfx.Dispose();
            }
        }

        // Localization is loaded by now: log the exact key of anything the journal expects but can't find.
        public override void PostSetupContent()
        {
            if (Main.dedServ)
            {
                return;
            }

            foreach (JournalChapter chapter in JournalRegistry.Chapters)
            {
                string chapterKey = $"{JournalText.Root}.Chapters.{chapter.Id}.Title";
                if (!Terraria.Localization.Language.Exists(chapterKey))
                {
                    JournalBuilder.Warn($"Missing localization key: {chapterKey}");
                }

                foreach (JournalPage page in chapter.Pages)
                {
                    string bodyKey = $"{JournalText.Root}.Chapters.{chapter.Id}.Pages.{page.Id}.Body";
                    if (!Terraria.Localization.Language.Exists(bodyKey))
                    {
                        JournalBuilder.Warn($"Missing localization key: {bodyKey}");
                    }
                }
            }
        }

        public override void OnWorldLoad()
        {
            // Unlock polling waits a moment after this so synced world flags (boss kills etc.) have arrived.
            JournalPlayer.WorldEnterTick = Main.GameUpdateCount;
        }

        public override void UpdateUI(GameTime gameTime)
        {
            if (Main.dedServ || JournalState == null)
            {
                return;
            }

            HandleToggleInput();

            if (Main.playerInventory || JournalState.IsOpen)
            {
                _journalInterface?.Update(gameTime);
            }

            // While the journal is up, the mouse belongs to the UI (no swinging items behind it).
            if (JournalState.IsOpen)
            {
                Main.LocalPlayer.mouseInterface = true;
            }
        }

        private void HandleToggleInput()
        {
            if (JournalState.IsOpen && JustPressedEscape())
            {
                JournalState.Close();
                return;
            }

            if (KeybindSystem.JournalKeybind.JustPressed)
            {
                JournalState.ToggleOpen();
            }
        }

        private static bool JustPressedEscape()
        {
            return Main.keyState.IsKeyDown(Keys.Escape) && !Main.oldKeyState.IsKeyDown(Keys.Escape);
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            int mouseTextIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Mouse Text"));
            if (mouseTextIndex != -1)
            {
                layers.Insert(mouseTextIndex, new LegacyGameInterfaceLayer(
                    "ShatteredIllusion: Journal Toast",
                    delegate
                    {
                        JournalToast.Draw(Main.spriteBatch);
                        return true;
                    },
                    InterfaceScaleType.UI));
            }

            int inventoryLayerIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Inventory"));
            if (inventoryLayerIndex != -1)
            {
                layers.Insert(inventoryLayerIndex + 1, new LegacyGameInterfaceLayer(
                    "ShatteredIllusion: Journal Button",
                    delegate
                    {
                        if (Main.playerInventory || JournalState.IsOpen)
                        {
                            _journalInterface?.Draw(Main.spriteBatch, new GameTime());
                        }
                        return true;
                    },
                    InterfaceScaleType.UI)
                );
            }
        }
    }

    internal class JournalUIState : UIState
    {
        private const float OpenSeconds = 0.6f;
        private const float CloseSeconds = 0.4f;

        private static readonly SoundStyle PullSound = new SoundStyle("ShatteredIllusion/Sounds/Journal/JournalPullUp");

        public Journal JournalButton;
        public JournalPanel JournalPanel;
        private CinematicOverlay _overlay;

        private bool _isOpen;
        private bool _openTarget;
        private float _progress;

        // Used to re-layout the UI when the window size or UI scale changes.
        private int _lastScreenWidth = -1;
        private int _lastScreenHeight = -1;
        private float _lastUIScale = -1f;

        public bool IsOpen => _isOpen;

        public override void OnInitialize()
        {
            _overlay = new CinematicOverlay();
            // Clicking the dimmed backdrop closes the book.
            _overlay.Clicked += () =>
            {
                if (_progress >= 0.9f)
                {
                    Close();
                }
            };
            Append(_overlay);

            // The panel stays centered; the slide / tilt / zoom animation is applied when it draws.
            JournalPanel = new JournalPanel();
            JournalPanel.HAlign = 0.5f;
            JournalPanel.VAlign = 0.5f;
            JournalPanel.Left.Set(0f, 0f);
            JournalPanel.Top.Set(0f, 0f);
            Append(JournalPanel);

            // Button position is intentionally unchanged.
            JournalButton = new Journal();
            JournalButton.Clicked += ToggleOpen;
            JournalButton.Left.Set(20f, 0f);
            JournalButton.Top.Set(280f, 0f);
            Append(JournalButton);
        }

        public void ToggleOpen()
        {
            if (_isOpen && _openTarget)
            {
                Close();
            }
            else
            {
                Open();
            }
        }

        public void Open()
        {
            if (_isOpen && _openTarget)
            {
                return;
            }

            if (!_isOpen)
            {
                JournalPanel.SetOpen(true);
            }

            _isOpen = true;
            _openTarget = true;
            JournalButton.SetOpenVisual(true);
            SoundEngine.PlaySound(PullSound);
        }

        public void Close()
        {
            if (!_isOpen || !_openTarget)
            {
                return;
            }

            _openTarget = false;
            JournalButton.SetOpenVisual(false);
            SoundEngine.PlaySound(PullSound with { Pitch = -0.25f });
        }

        public override void Update(GameTime gameTime)
        {
            // The state caches its size; without this the panel can sit off-center after a resolution change.
            if (Main.screenWidth != _lastScreenWidth || Main.screenHeight != _lastScreenHeight || Main.UIScale != _lastUIScale)
            {
                _lastScreenWidth = Main.screenWidth;
                _lastScreenHeight = Main.screenHeight;
                _lastUIScale = Main.UIScale;
                Recalculate();
            }

            float delta = (float)gameTime.ElapsedGameTime.TotalSeconds;

            if (_openTarget)
            {
                _progress = Math.Min(1f, _progress + delta / OpenSeconds);
            }
            else
            {
                _progress = Math.Max(0f, _progress - delta / CloseSeconds);
                if (_progress <= 0f && _isOpen)
                {
                    _isOpen = false;
                    JournalPanel.SetOpen(false);
                }
            }

            JournalPanel.SetProgress(_progress);
            _overlay.SetProgress(_progress);

            base.Update(gameTime);
        }
    }

    /// <summary>
    /// Shared sizing for the journal's letterbox bars. Uses Cinema's formula (so both look consistent on any
    /// aspect ratio) with thinner limits, and respects the player's letterbox config toggle.
    /// </summary>
    internal static class JournalBackdrop
    {
        private const float MinBarFraction = 0.04f;
        private const float MaxBarFraction = 0.09f;

        // Height in UI pixels of one journal letterbox bar at full strength, or 0 if letterboxing is disabled.
        public static float TargetBarHeight(float uiWidth, float uiHeight)
        {
            if (!Cinema.LetterboxAllowed)
            {
                return 0f;
            }

            return Cinema.LetterboxBarHeight(uiWidth, uiHeight, MinBarFraction, MaxBarFraction);
        }
    }

    /// <summary>
    /// Full-screen cinematic backdrop: dimmer, vignette, letterbox bars and drifting dust motes.
    /// Vignette, bars and motes follow the player's cinematic config toggles; the dimmer always shows.
    /// </summary>
    internal class CinematicOverlay : UIElement
    {
        private const float MaxDarkenAlpha = 0.55f;
        private const int MoteCount = 96;           // pool size; how many are active depends on screen area
        private const int MotesPer1080p = 48;

        private struct Mote
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public float Size;
            public float Phase;
            public float Brightness;
        }

        private readonly Mote[] _motes = new Mote[MoteCount];
        private bool _motesSeeded;
        private float _eased;
        private int _activeMotes = MotesPer1080p;
        private float _sizeScale = 1f;

        public event Action Clicked;

        public CinematicOverlay()
        {
            Width.Set(0f, 1f);
            Height.Set(0f, 1f);
        }

        public void SetProgress(float rawProgress)
        {
            _eased = JournalGfx.EaseOutCubic(MathHelper.Clamp(rawProgress, 0f, 1f));
        }

        // Only intercept clicks once the backdrop is actually visible. It covers the whole screen,
        // so this doesn't depend on the element's cached size.
        public override bool ContainsPoint(Vector2 point)
        {
            return _eased > 0.5f;
        }

        private static CalculatedStyle ScreenDims()
        {
            return new CalculatedStyle(0f, 0f, Main.screenWidth / Main.UIScale, Main.screenHeight / Main.UIScale);
        }

        // Screen size in UI units, read straight from Main so it can never be stale.
        // Padded a couple of pixels so rounding can't leave an uncovered strip at the right or bottom.
        private static Rectangle ScreenRect()
        {
            float w = Main.screenWidth / Main.UIScale;
            float h = Main.screenHeight / Main.UIScale;
            return new Rectangle(0, 0, (int)Math.Ceiling(w) + 2, (int)Math.Ceiling(h) + 2);
        }

        public override void LeftClick(UIMouseEvent evt)
        {
            base.LeftClick(evt);
            Clicked?.Invoke();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (_eased <= 0f)
            {
                _motesSeeded = false;
                return;
            }

            // Dust is part of the cinematic dressing; skip the work entirely if the player turned that off.
            if (!Cinema.Enabled)
            {
                _motesSeeded = false;
                return;
            }

            CalculatedStyle dims = ScreenDims();

            // Keep dust density and size consistent across resolutions and aspect ratios.
            float areaRatio = dims.Width * dims.Height / (1920f * 1080f);
            _activeMotes = Math.Clamp((int)(MotesPer1080p * areaRatio), 24, MoteCount);
            _sizeScale = MathHelper.Clamp(dims.Height / 1080f, 0.7f, 1.6f);

            if (!_motesSeeded)
            {
                for (int i = 0; i < _motes.Length; i++)
                {
                    SeedMote(ref _motes[i], dims, true);
                }
                _motesSeeded = true;
            }

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            for (int i = 0; i < _activeMotes; i++)
            {
                _motes[i].Position += _motes[i].Velocity * dt;
                _motes[i].Phase += dt;

                if (_motes[i].Position.Y < dims.Y - 20f ||
                    _motes[i].Position.X < dims.X - 40f ||
                    _motes[i].Position.X > dims.X + dims.Width + 40f)
                {
                    SeedMote(ref _motes[i], dims, false);
                }
            }
        }

        private static void SeedMote(ref Mote mote, CalculatedStyle dims, bool anywhere)
        {
            mote.Position = new Vector2(
                dims.X + Main.rand.NextFloat() * dims.Width,
                anywhere ? dims.Y + Main.rand.NextFloat() * dims.Height : dims.Y + dims.Height + 10f);
            mote.Velocity = new Vector2(Main.rand.NextFloat(-6f, 6f), -Main.rand.NextFloat(6f, 22f));
            mote.Size = Main.rand.NextFloat(5f, 16f);
            mote.Phase = Main.rand.NextFloat() * MathHelper.TwoPi;
            mote.Brightness = Main.rand.NextFloat(0.25f, 0.8f);
        }

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            if (_eased <= 0.001f)
            {
                return;
            }

            JournalGfx.EnsureBuilt();

            CalculatedStyle dims = ScreenDims();
            Rectangle full = ScreenRect();
            Texture2D pixel = TextureAssets.MagicPixel.Value;

            // Dim the world, then push the edges darker.
            spriteBatch.Draw(pixel, full, Color.Black * (_eased * MaxDarkenAlpha));
            if (Cinema.VignetteAllowed)
            {
                spriteBatch.Draw(JournalGfx.Vignette, full, Color.White * (_eased * 0.9f));
            }

            // Dust motes catching the lamp light (only seeded/updated while cinematics are enabled).
            int moteCount = _motesSeeded && Cinema.Enabled ? _activeMotes : 0;
            for (int i = 0; i < moteCount; i++)
            {
                Mote mote = _motes[i];
                float twinkle = 0.55f + 0.45f * (float)Math.Sin(mote.Phase * 1.6f);
                Color color = new Color(255, 214, 160) * (mote.Brightness * twinkle * 0.45f * _eased);
                int size = Math.Max(2, (int)(mote.Size * _sizeScale));
                spriteBatch.Draw(
                    JournalGfx.Soft,
                    new Rectangle((int)mote.Position.X - size / 2, (int)mote.Position.Y - size / 2, size, size),
                    color);
            }

            // Letterbox bars slide in from the top and bottom. They aim for a cinematic aspect ratio, but
            // stay thin on screens that are already ultrawide and never eat too much of a tall/narrow one.
            int barHeight = (int)(JournalBackdrop.TargetBarHeight(dims.Width, dims.Height) * _eased);
            if (barHeight > 0)
            {
                spriteBatch.Draw(pixel, new Rectangle(full.X, full.Y, full.Width, barHeight), Color.Black);
                spriteBatch.Draw(pixel, new Rectangle(full.X, full.Bottom - barHeight, full.Width, barHeight), Color.Black);
            }
        }
    }
}
namespace ShatteredIllusion.Common.GUI.JournalUI
{
    internal static class JournalToast
    {
        private const float HazeRise = 0.7f;
        private const float HazeClear = 1.2f;
        private const float FadeDur = 1.1f;
        private const float HoldSeconds = 2.6f;
        private const float HoldQueuedSeconds = 0.8f;
        private const float ForgetSpan = 0.7f;
        private const float ForgetDur = 0.9f;
        private const float BottomMargin = 110f;
        private const float IconSize = 34f;
        private const int MoteCount = 12;

        private static readonly Color Tint = new Color(222, 168, 120);
        private static readonly Color Faded = new Color(176, 138, 128);
        private static readonly Color HeaderInk = new Color(236, 196, 150);
        private static readonly Color SubjectInk = new Color(255, 238, 205);
        private static readonly Color Smoke = new Color(18, 8, 12);
        private static readonly Color Plate = new Color(26, 11, 15);
        private static readonly SoundStyle Chime = new SoundStyle("ShatteredIllusion/Sounds/Journal/JournalPullUp");

        private sealed class Line
        {
            public string Text;
            public float[] X;
            public float Scale;
            public float Width;
            public float Y;
            public float Start;
            public float Spread;
            public Color Ink;
            public int Seed;
        }

        private sealed class Memory
        {
            public string Subject;
            public Line[] Lines;
            public float Age;
            public float SurfaceEnd;
            public float DissolveStart;
            public float Bar = 1f;
            public float IconY;
            public float RuleY;
            public float FrameTop;
            public float FrameHeight;
            public float Width;
            public float Life => DissolveStart + ForgetSpan + ForgetDur + 0.3f;
        }

        private static readonly Queue<string> Pending = new Queue<string>();
        private static Memory _current;
        private static long _last;
        private static float _clock;
        private static float _lineHeight;
        private static Asset<Texture2D> _icon;

        public static void Push(string subject)
        {
            if (Main.dedServ || !ModContent.GetInstance<ShatteredIllusionConfig>().JournalNotifications || (_current != null && _current.Subject == subject) || Pending.Contains(subject))
            {
                return;
            }

            Pending.Enqueue(subject);

            if (_current != null && _current.Age < _current.DissolveStart)
            {
                _current.DissolveStart = Math.Max(_current.Age, Math.Min(_current.DissolveStart, _current.SurfaceEnd + HoldQueuedSeconds));
            }
        }

        public static void Clear()
        {
            Pending.Clear();
            _current = null;
        }

        public static void Draw(SpriteBatch sb)
        {
            if (!ModContent.GetInstance<ShatteredIllusionConfig>().JournalNotifications)
            {
                Clear();
                return;
            }

            long now = Stopwatch.GetTimestamp();
            float dt = Math.Min((now - _last) / (float)Stopwatch.Frequency, 0.05f);
            _last = now;

            DynamicSpriteFont font = FontAssets.MouseText.Value;

            if (_current == null)
            {
                if (Pending.Count == 0)
                {
                    return;
                }

                _current = Build(Pending.Dequeue(), font);
                SoundEngine.PlaySound(Chime with { Volume = 0.45f, Pitch = 0.35f });
                dt = 0f;
            }

            Memory m = _current;
            m.Age += dt;
            _clock += dt;
            if (m.Age >= m.Life)
            {
                _current = null;
                return;
            }

            JournalGfx.EnsureBuilt();

            float t = m.Age;
            float uiWidth = Main.screenWidth / Main.UIScale;
            float uiHeight = Main.screenHeight / Main.UIScale;

            float k = Math.Min(Smooth(t / HazeRise), Smooth((m.Life - t) / HazeClear));
            float forget = Smooth((t - m.DissolveStart) / 1.2f);
            float cx = uiWidth / 2f;
            float baseY = uiHeight - BottomMargin - (m.FrameTop + m.FrameHeight) - 14f * Smooth(t / m.Life);

            if (Cinema.VignetteAllowed && k > 0.01f)
            {
                sb.Draw(JournalGfx.Vignette, new Rectangle(0, 0, (int)uiWidth + 2, (int)uiHeight + 2), Color.White * (0.4f * k));
            }

            DrawHaze(sb, cx, baseY + m.FrameTop + m.FrameHeight / 2f, Math.Max(420f, m.Width * 1.6f), m.FrameHeight * 1.5f, k, t);
            DrawIcon(sb, new Vector2(cx, baseY + m.IconY), t, m.DissolveStart);

            float barTarget = 1f - MathHelper.Clamp((t - 0.9f) / (m.DissolveStart - 0.9f), 0f, 1f);
            m.Bar = Math.Max(barTarget, m.Bar - dt * 1.5f);
            DrawFrame(sb, cx, baseY, m, t, k * (1f - forget));

            if (m.Lines.Length == 2)
            {
                float half = Math.Max(70f, m.Lines[1].Width * 0.6f) * Smooth((t - 0.9f) / 0.7f);
                DrawRule(sb, cx, baseY + m.RuleY, half, (1f - forget) * k);
            }

            foreach (Line line in m.Lines)
            {
                float left = cx - line.Width / 2f;
                float y = baseY + line.Y;
                float glintX = left - 40f + (line.Width + 80f) * Smooth((t - m.SurfaceEnd + 0.3f) / 1.1f);

                for (int i = 0; i < line.Text.Length; i++)
                {
                    if (line.Text[i] == ' ')
                    {
                        continue;
                    }

                    int seed = line.Seed * 1000 + i;
                    float start = line.Start + line.Spread * i / Math.Max(1, line.Text.Length - 1) + Hash(seed * 7 + 3) % 120u / 1000f;
                    float forgetAt = m.DissolveStart + Hash(seed * 13 + 9) % 700u / 1000f * (ForgetSpan / 0.7f);
                    float p = MathHelper.Clamp((t - start) / FadeDur, 0f, 1f);
                    float q = MathHelper.Clamp((t - forgetAt) / ForgetDur, 0f, 1f);

                    if (p > 0.001f)
                    {
                        float mid = left + (line.X[i] + line.X[i + 1]) * 0.5f * line.Scale;
                        float glint = MathHelper.Clamp(1f - Math.Abs(mid - glintX) / 55f, 0f, 1f);
                        DrawLetter(sb, font, line.Text[i].ToString(), new Vector2(left + line.X[i] * line.Scale, y),
                            (line.X[i + 1] - line.X[i]) * line.Scale, line.Ink, line.Scale, p, q, seed, glint * glint);
                    }
                }
            }
        }

        private static Memory Build(string subject, DynamicSpriteFont font)
        {
            string key = $"{JournalText.Root}.UI.Remembers";
            string header = Language.Exists(key) ? Language.GetTextValue(key) : "The journal remembers...";
            _lineHeight = font.MeasureString("Ag").Y;

            float iconY = -IconSize / 2f - 14f;
            float top = iconY - IconSize / 2f - 14f;
            float textBottom;
            float ruleY = 0f;
            Line[] lines;

            if (subject == null)
            {
                lines = new[] { MakeLine(font, header, 1.1f, 8f, 0.35f, 1f, SubjectInk, 1) };
                textBottom = 8f + _lineHeight * 1.1f;
            }
            else
            {
                float headerBottom = _lineHeight * 0.8f;
                ruleY = headerBottom + 8f;
                float subjectY = ruleY + 12f;
                lines = new[]
                {
                    MakeLine(font, header, 0.8f, 0f, 0.35f, 0.8f, HeaderInk, 1),
                    MakeLine(font, subject, 1.25f, subjectY, 0.95f, Math.Min(1.3f, 0.05f * subject.Length), SubjectInk, 2),
                };
                textBottom = subjectY + _lineHeight * 1.25f;
            }

            float done = 0f;
            float maxWidth = 0f;
            foreach (Line line in lines)
            {
                done = Math.Max(done, line.Start + line.Spread + 0.12f + FadeDur);
                maxWidth = Math.Max(maxWidth, line.Width);
            }

            float bottom = textBottom + 14f + 3f + 16f;
            return new Memory
            {
                Subject = subject,
                Lines = lines,
                SurfaceEnd = done,
                DissolveStart = done + HoldSeconds,
                IconY = iconY,
                RuleY = ruleY,
                FrameTop = top,
                FrameHeight = bottom - top,
                Width = Math.Max(320f, maxWidth + 110f),
            };
        }

        private static Line MakeLine(DynamicSpriteFont font, string text, float scale, float y, float start, float spread, Color ink, int seed)
        {
            float[] x = new float[text.Length + 1];
            for (int i = 1; i <= text.Length; i++)
            {
                x[i] = font.MeasureString(text.Substring(0, i)).X;
            }

            return new Line { Text = text, X = x, Scale = scale, Width = x[text.Length] * scale, Y = y, Start = start, Spread = spread, Ink = ink, Seed = seed };
        }

        private static void DrawHaze(SpriteBatch sb, float cx, float cy, float w, float h, float k, float t)
        {
            if (k <= 0.01f)
            {
                return;
            }

            float breathe = 0.85f + 0.15f * (float)Math.Sin(_clock * 1.3f);
            Texture2D soft = JournalGfx.Soft;

            sb.Draw(soft, new Rectangle((int)(cx - w * 0.6f), (int)(cy - h * 0.6f), (int)(w * 1.2f), (int)(h * 1.2f)), Smoke * (0.5f * k));
            sb.Draw(soft, new Rectangle((int)(cx - w * 0.5f), (int)(cy - h * 0.4f), (int)w, (int)(h * 0.8f)), Tint * (0.2f * k * breathe));

            for (int i = 0; i < MoteCount; i++)
            {
                uint hash = Hash(i * 613 + 41);
                float speed = 0.06f + (hash % 14u) / 200f;
                float life = (t * speed + (hash >> 8) % 300u / 300f) % 1f;
                float x = cx + ((hash >> 4) % 1000u / 1000f - 0.5f) * w * 0.8f + (float)Math.Sin(_clock * 0.9f + i) * 8f;
                float y = cy + h * 0.4f - life * h * 0.8f;
                float twinkle = 0.5f + 0.5f * (float)Math.Sin(_clock * 2.4f + i * 1.9f);
                int size = 4 + (int)(hash % 5u);
                sb.Draw(soft, new Rectangle((int)x - size, (int)y - size, size * 2, size * 2),
                    Color.Lerp(Tint, Color.White, 0.5f) * (0.55f * k * twinkle * (float)Math.Sin(life * Math.PI)));
            }
        }

        private static void DrawFrame(SpriteBatch sb, float cx, float baseY, Memory m, float t, float alpha)
        {
            if (alpha <= 0.01f)
            {
                return;
            }

            float draw = Smooth((t - 0.2f) / 0.8f);
            float w = m.Width;
            float h = m.FrameHeight;
            float x = cx - w / 2f;
            float y = baseY + m.FrameTop;
            Texture2D px = TextureAssets.MagicPixel.Value;
            Color edge = Color.Lerp(Tint, Color.White, 0.2f);
            Color corner = Color.Lerp(Tint, Color.White, 0.5f);

            float fw = w * draw;
            float fh = h * draw;
            Fill(sb, px, cx - fw / 2f, y + (h - fh) / 2f, fw, fh, Plate * (0.68f * alpha));
            Fill(sb, px, cx - fw / 2f, y + (h - fh) / 2f, fw, fh * 0.45f, Color.White * (0.03f * alpha));

            Outline(sb, px, cx, y + h / 2f, w * draw, h * draw, edge * (0.6f * alpha));
            Outline(sb, px, cx, y + h / 2f, (w - 10f) * draw, (h - 10f) * draw, edge * (0.2f * alpha));

            if (draw > 0.95f)
            {
                for (int sx = 0; sx < 2; sx++)
                {
                    for (int sy = 0; sy < 2; sy++)
                    {
                        float cxp = sx == 0 ? x : x + w - 14f;
                        float cyp = sy == 0 ? y : y + h - 2f;
                        Fill(sb, px, cxp, cyp, 14f, 2f, corner * (0.95f * alpha));
                        Fill(sb, px, sx == 0 ? x : x + w - 2f, sy == 0 ? y : y + h - 14f, 2f, 14f, corner * (0.95f * alpha));
                    }
                }
            }

            float ba = alpha * draw * draw * draw;
            float barW = w - 48f;
            float barX = x + 24f;
            float barY = y + h - 16f - 3f;
            Fill(sb, px, barX, barY, barW, 3f, Smoke * (0.7f * ba));
            Fill(sb, px, barX, barY, barW, 1f, Tint * (0.18f * ba));
            Fill(sb, px, barX, barY, barW * m.Bar, 3f, Color.Lerp(Tint, Color.White, 0.25f) * (0.95f * ba));

            if (m.Bar > 0.002f)
            {
                float head = barX + barW * m.Bar;
                sb.Draw(JournalGfx.Soft, new Rectangle((int)head - 14, (int)barY - 10, 28, 24), Tint * (0.7f * ba));
            }
        }

        private static void Outline(SpriteBatch sb, Texture2D px, float cx, float cy, float w, float h, Color color)
        {
            Fill(sb, px, cx - w / 2f, cy - h / 2f, w, 1f, color);
            Fill(sb, px, cx - w / 2f, cy + h / 2f - 1f, w, 1f, color);
            Fill(sb, px, cx - w / 2f, cy - h / 2f, 1f, h, color);
            Fill(sb, px, cx + w / 2f - 1f, cy - h / 2f, 1f, h, color);
        }

        private static void Fill(SpriteBatch sb, Texture2D px, float x, float y, float w, float h, Color color)
        {
            sb.Draw(px, new Vector2(x, y), null, color, 0f, Vector2.Zero, new Vector2(w / px.Width, h / px.Height), SpriteEffects.None, 0f);
        }

        private static void DrawIcon(SpriteBatch sb, Vector2 at, float t, float dissolveStart)
        {
            _icon ??= ModContent.Request<Texture2D>("ShatteredIllusion/Assets/UI/Journal_Closed");
            if (!_icon.IsLoaded)
            {
                return;
            }

            float p = Smooth((t - 0.15f) / 1.2f);
            float q = Smooth((t - dissolveStart) / 1.2f);
            float alpha = p * p * (1f - q);
            if (alpha <= 0.002f)
            {
                return;
            }

            Texture2D icon = _icon.Value;
            float scale = IconSize / icon.Height;
            float inv = 1f - p;
            float spread = inv * inv * 10f + q * q * 12f;
            Vector2 bob = new Vector2(0f, (float)Math.Sin(_clock * 1.6f) * 2f + inv * 6f - q * 14f);
            Vector2 origin = icon.Size() / 2f;
            float waver = 0.88f + 0.12f * (float)Math.Sin(_clock * 3.1f);

            sb.Draw(JournalGfx.Soft, at + bob, null, Tint * (0.35f * alpha), 0f, JournalGfx.Soft.Size() / 2f, 90f / JournalGfx.Soft.Width, SpriteEffects.None, 0f);
            sb.Draw(icon, at + bob + new Vector2(spread, 0f), null, Tint * (0.3f * alpha * waver), 0f, origin, scale, SpriteEffects.None, 0f);
            sb.Draw(icon, at + bob - new Vector2(spread * 0.7f, 0f), null, Tint * (0.22f * alpha * waver), 0f, origin, scale, SpriteEffects.None, 0f);
            sb.Draw(icon, at + bob, null, Color.Lerp(Faded, Color.White, p) * (alpha * waver), 0f, origin, scale, SpriteEffects.None, 0f);
        }

        private static void DrawRule(SpriteBatch sb, float cx, float y, float half, float alpha)
        {
            if (half < 1f || alpha <= 0.01f)
            {
                return;
            }

            Texture2D grad = JournalGfx.HGradient;
            Vector2 scale = new Vector2(half / grad.Width, 1f);
            Color color = Color.Lerp(Tint, Color.White, 0.3f) * (0.7f * alpha);

            sb.Draw(grad, new Vector2(cx, y), null, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            sb.Draw(grad, new Vector2(cx - half, y), null, color, 0f, Vector2.Zero, scale, SpriteEffects.FlipHorizontally, 0f);

            Texture2D px = TextureAssets.MagicPixel.Value;
            sb.Draw(px, new Vector2(cx, y + 0.5f), null, color, MathHelper.PiOver4, px.Size() / 2f, new Vector2(4f / px.Width, 4f / px.Height), SpriteEffects.None, 0f);
        }

        private static void DrawLetter(SpriteBatch sb, DynamicSpriteFont font, string ch, Vector2 at, float width, Color ink, float scale, float p, float q, int seed, float glint)
        {
            float e = Smooth(p);
            float inv = 1f - e;
            float fade = 1f - Smooth(q);
            float alpha = (float)Math.Pow(p, 1.6) * fade;
            if (alpha <= 0.002f)
            {
                return;
            }

            float waver = 0.88f + 0.12f * (float)Math.Sin(_clock * 3.1f + seed * 1.7f);
            Color color = Color.Lerp(Color.Lerp(Faded, ink, e * fade), Color.White, glint * 0.7f);

            float smudge = inv * Math.Min(1f, p * 6f) + 0.8f * (float)Math.Sin(q * Math.PI) + glint * 0.7f;
            int halo = (int)((18f + 24f * smudge) * scale);
            sb.Draw(JournalGfx.Soft, new Rectangle((int)(at.X + width / 2f - halo / 2f), (int)(at.Y + _lineHeight * scale * 0.5f - halo / 2f), halo, halo),
                Tint * (0.25f * smudge));

            float angle = Hash(seed * 31 + 5) % 628u / 100f;
            Vector2 dir = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
            float spread = inv * inv * 7f + q * q * 9f;
            Vector2 rise = new Vector2(0f, inv * 4f - q * 12f);

            sb.DrawString(font, ch, at + rise + dir * spread, color * (0.30f * alpha * waver), 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            sb.DrawString(font, ch, at + rise - dir * spread * 0.7f, color * (0.22f * alpha * waver), 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            Utils.DrawBorderString(sb, ch, at + rise, color * (alpha * waver), scale);
        }

        private static float Smooth(float t)
        {
            t = MathHelper.Clamp(t, 0f, 1f);
            return t * t * (3f - 2f * t);
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
    }
}