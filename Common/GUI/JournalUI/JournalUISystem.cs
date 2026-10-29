using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using ShatteredIllusion.Common.Visuals;
using ShatteredIllusionKeybinds;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
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

            JournalToast.Update();

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
    /// <summary>A small "Journal updated" banner that slides in near the top of the screen when something unlocks.</summary>
    internal static class JournalToast
    {
        private const int Lifetime = 300;   // ticks
        private const int FadeTicks = 24;

        private sealed class Entry
        {
            public string Text;
            public int Age;
        }

        private static readonly List<Entry> Entries = new List<Entry>();
        private static Asset<Texture2D> _icon;

        public static void Push(string text)
        {
            if (Main.dedServ)
            {
                return;
            }

            Entries.Add(new Entry { Text = text });
            SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.5f, Pitch = 0.4f });
        }

        public static void Clear() => Entries.Clear();

        public static void Update()
        {
            for (int i = Entries.Count - 1; i >= 0; i--)
            {
                if (++Entries[i].Age >= Lifetime)
                {
                    Entries.RemoveAt(i);
                }
            }
        }

        public static void Draw(SpriteBatch sb)
        {
            if (Entries.Count == 0)
            {
                return;
            }

            JournalGfx.EnsureBuilt();
            _icon ??= ModContent.Request<Texture2D>("ShatteredIllusion/Assets/UI/Journal_Closed");

            DynamicSpriteFont font = FontAssets.MouseText.Value;
            float uiWidth = Main.screenWidth / Main.UIScale;
            float y = 96f;

            foreach (Entry entry in Entries)
            {
                float appear = Math.Min(1f, entry.Age / (float)FadeTicks);
                float vanish = Math.Min(1f, (Lifetime - entry.Age) / (float)FadeTicks);
                float alpha = Math.Min(JournalGfx.EaseOutCubic(appear), vanish);
                float slide = (1f - JournalGfx.EaseOutCubic(appear)) * -24f;
                float pulse = 0.5f + 0.5f * (float)Math.Sin(entry.Age * 0.2f);

                Vector2 textSize = font.MeasureString(entry.Text) * 0.9f;
                float width = textSize.X + 78f;
                float height = 44f;
                Vector2 topLeft = new Vector2((uiWidth - width) / 2f, y + slide);
                Rectangle box = new Rectangle((int)topLeft.X, (int)topLeft.Y, (int)width, (int)height);

                // Soft warm halo, dark plate, thin gold rule.
                sb.Draw(JournalGfx.Soft, new Rectangle(box.X - 40, box.Y - 30, box.Width + 80, box.Height + 60),
                    new Color(255, 190, 110) * (0.25f * (0.7f + 0.3f * pulse) * alpha));
                sb.Draw(TextureAssets.MagicPixel.Value, box, new Color(26, 10, 18) * (0.88f * alpha));
                sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(box.X, box.Bottom - 2, box.Width, 2), new Color(255, 206, 120) * (0.9f * alpha));

                if (_icon.IsLoaded)
                {
                    Texture2D icon = _icon.Value;
                    float iconScale = 30f / icon.Height;
                    sb.Draw(icon, new Vector2(box.X + 28f, box.Center.Y), null, Color.White * alpha, 0f, icon.Size() / 2f, iconScale, SpriteEffects.None, 0f);
                }

                Utils.DrawBorderString(sb, entry.Text, new Vector2(box.X + 54f, box.Center.Y - textSize.Y / 2f + 2f),
                    Color.Lerp(new Color(255, 226, 170), Color.White, pulse * 0.4f) * alpha, 0.9f);

                y += height + 8f;
            }
        }
    }
}
