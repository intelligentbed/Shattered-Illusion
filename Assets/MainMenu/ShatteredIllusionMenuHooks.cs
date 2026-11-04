using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ReLogic.Graphics;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.UI.States;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.UI;
using Terraria.UI.Chat;
using PadButton = Microsoft.Xna.Framework.Input.Buttons;

namespace ShatteredIllusion.Assets.MainMenu
{
    public class ShatteredIllusionMenuHooks : ModSystem
    {
        // Stores a menu button, its localized label and what it does.
        private sealed class MenuButton
        {
            private readonly string _key;
            private readonly string _fallback;
            private LocalizedText? _text;

            public readonly Action OnClick;
            public float Hover;

            // Read live, so the label follows the language setting.
            public string Label => _text?.Value ?? _fallback;

            public MenuButton(string key, string fallback, Action onClick)
            {
                _key = key;
                _fallback = fallback;
                OnClick = onClick;
            }

            // Registers the label under Mods.ShatteredIllusion.MainMenu.Buttons.<key>.
            // The fallback is the English default used until a translation exists.
            public void Localize(Mod mod)
            {
                _text = mod.GetLocalization($"MainMenu.Buttons.{_key}", () => _fallback);
            }
        }

        // Which device last drove the focus. Keyboard and gamepad hide the mouse cursor.
        private enum InputSource
        {
            None,
            Mouse,
            Keyboard,
            Gamepad
        }

        // Main menu buttons
        private static readonly MenuButton[] Buttons =
        {
            new("SinglePlayer", "Single Player", () => Main.menuMode = 1),
            new("Multiplayer", "Multiplayer", () => Main.menuMode = 12),
            new("Achievements", "Achievements", OpenAchievements),
            new("Settings", "Settings", () => Main.menuMode = 11),
            new("Workshop", "Workshop", OpenWorkshop),
            new("Exit", "Exit", () => Main.instance.Exit()),
        }; //TODO: Add the credits button back once the credits menuMode is confirmed

        // Focus targets are the buttons in order, followed by the theme switch.
        private const int NoFocus = -1;
        private static int ThemeSwitchIndex => Buttons.Length;
        private static int FocusCount => Buttons.Length + 1;

        // Button layout settings
        private const float ButtonStartY = 300f;
        private const float ButtonSpacingYMin = 58f;
        private const float ButtonRowGap = 16f;
        private const float ButtonInset = 90f;
        private const float ButtonScale = 0.72f;
        private const string UnderlineReferenceGlyphs = "AEIOUXY";

        // Row height at layout scale 1. It depends on the label text, so it is measured again
        // when the language changes, and multiplied by the layout scale when used.
        private float _cachedRowHeight = -1f;
        private string? _cachedRowHeightCulture;

        // Button entrance animation.
        private const float SlideDistance = 140f;
        private const float StaggerDelay = 0.05f;
        private const float StaggerDuration = 0.3f;

        // Button exit animation. The selected button holds for a moment, then everything
        // slides out, starting with the buttons next to the selected one.
        private const float SelectedHold = 0.07f;
        private const float SelectedPulseScale = 0.06f;
        private const float ExitStagger = 0.02f;
        private const float ExitButtonDuration = 0.18f;

        // Hover and animation. Smoothing rates are per second so they match at any frame rate.
        private const float HoverSmoothing = 14f;
        private const float HoverScale = 1.08f;
        private const float HoverThresholdForUnderline = 0.02f;
        private const float EntranceCompleteThreshold = 0.99f;
        private const float MaxDeltaTime = 0.1f;
        private const float FooterScale = 0.42f;
        private const float ThemeSwitchScale = 0.39f;
        private const float ThemeSwitchBottomPadding = 16f;

        // Hover glow, drawn as stacked rectangles that get bigger and fainter outward.
        private const int GlowLayers = 4;
        private const float GlowMaxAlpha = 0.24f;
        private const float GlowMaxPad = 16f;

        // Small idle movement. Positions are rounded to whole pixels, so the amplitude is
        // large enough to step visibly instead of flickering.
        private const float IdleBobAmplitude = 1.5f;
        private const float IdleBobSpeed = 1.15f;
        private const float BobFadeRate = 6f;

        // Keyboard and gamepad navigation
        private const float StickThreshold = 0.55f;
        private const double RepeatInitialDelay = 0.4;
        private const double RepeatInterval = 0.11;
        private const float MouseMoveThresholdSquared = 0.25f;

        // tracks menu and mouse state
        private bool _wasActive;
        private double _enteredAt = -1000d;
        private bool _mouseWasDown;
        private bool _mouseWasRightDown;
        private Vector2 _lastMouse;

        // Tracks keyboard and gamepad state between frames.
        private KeyboardState _prevKeys;
        private GamePadState _prevPad;
        private int _repeatDirection;
        private double _nextRepeatAt;

        // Focus is the highlighted item. Selection is the item that was activated and is animating out.
        private int _focusIndex = NoFocus;
        private int _selectedIndex = NoFocus;
        private InputSource _source = InputSource.None;

        // Tracks the exit animation.
        private bool _exiting;
        private double _exitStartedAt;
        private float _exitDuration;
        private Action? _pendingAction;

        private float _themeSwitchHover;
        private float _bobAmount = 1f;

        private double _animationTime;
        private float _deltaTime;

        private static DynamicSpriteFont MenuFont => FontAssets.DeathText.Value;

        private MethodInfo? _offsetModMenu;
        private FieldInfo? _currentMenuField;
        private FieldInfo? _switchToMenuField;
        private FieldInfo? _lastSelectedModMenuField;
        private bool _menuLoaderAccessResolved;

        // Text shown for the theme switcher.
        private static string ThemeSwitchText =>
            Language.GetTextValue("tModLoader.ModMenuSwap") + ": " +
            (MenuLoader.CurrentMenu?.DisplayName ?? "tModLoader");

        public override void Load()
        {
            foreach (MenuButton button in Buttons)
                button.Localize(Mod);

            On_Main.DrawMenu += Main_DrawMenu;
        }

        public override void Unload()
        {
            On_Main.DrawMenu -= Main_DrawMenu;

            _wasActive = false;
            _enteredAt = -1000d;
            _mouseWasDown = false;
            _mouseWasRightDown = false;
            _lastMouse = Vector2.Zero;
            _prevKeys = default;
            _prevPad = default;
            _repeatDirection = 0;
            _nextRepeatAt = 0d;
            _focusIndex = NoFocus;
            _selectedIndex = NoFocus;
            _source = InputSource.None;
            _exiting = false;
            _exitStartedAt = 0d;
            _exitDuration = 0f;
            _pendingAction = null;
            _themeSwitchHover = 0f;
            _bobAmount = 1f;
            _animationTime = 0d;
            _deltaTime = 0f;
            _cachedRowHeight = -1f;
            _cachedRowHeightCulture = null;

            foreach (MenuButton button in Buttons)
                button.Hover = 0f;

            _offsetModMenu = null;
            _currentMenuField = null;
            _switchToMenuField = null;
            _lastSelectedModMenuField = null;
            _menuLoaderAccessResolved = false;
        }

        private void Main_DrawMenu(On_Main.orig_DrawMenu orig, Main self, GameTime gameTime)
        {
            _deltaTime = MathF.Min((float)gameTime.ElapsedGameTime.TotalSeconds, MaxDeltaTime);
            _animationTime += gameTime.ElapsedGameTime.TotalSeconds;

            if (_exiting && _animationTime - _exitStartedAt >= _exitDuration)
            {
                _exiting = false;
                _selectedIndex = NoFocus;

                Action? action = _pendingAction;
                _pendingAction = null;
                action?.Invoke();
            }

            bool active = IsMenuActive();

            if (active && !_wasActive)
                ResetInteractionState();

            _wasActive = active;

            if (!active)
            {
                _exiting = false;
                _pendingAction = null;
                _focusIndex = NoFocus;
                _selectedIndex = NoFocus;

                SnapshotInputState();

                orig(self, gameTime);
                return;
            }

            ValidateNavigationState();
            ProcessInput();
            UpdateHoverVisuals();
            DrawOwnButtons();
        }

        private static bool IsMenuActive()
        {
            return Main.menuMode == 0 &&
                   MenuLoader.CurrentMenu is ShatteredIllusionMainMenu;
        }

        // Clears animation, focus and selection state when the menu becomes active.
        private void ResetInteractionState()
        {
            _enteredAt = _animationTime;
            _exiting = false;
            _pendingAction = null;

            _focusIndex = NoFocus;
            _selectedIndex = NoFocus;
            _source = InputSource.None;
            _themeSwitchHover = 0f;
            _bobAmount = 1f;

            foreach (MenuButton button in Buttons)
                button.Hover = 0f;

            SnapshotInputState();
        }

        // Records the current input so a press held from another screen is not read as a new press.
        private void SnapshotInputState()
        {
            _mouseWasDown = Main.mouseLeft;
            _mouseWasRightDown = Main.mouseRight;
            _lastMouse = SpriteBatchUtil.UiMouse;

            bool hasFocus = Main.hasFocus;
            _prevKeys = hasFocus ? Keyboard.GetState() : default;
            _prevPad = hasFocus ? GamePad.GetState(PlayerIndex.One) : default;

            _repeatDirection = 0;
        }

        // Keeps focus and selection inside valid ranges and in sync with the animation state.
        private void ValidateNavigationState()
        {
            if (_focusIndex < NoFocus || _focusIndex >= FocusCount)
                _focusIndex = NoFocus;

            if (_selectedIndex < NoFocus || _selectedIndex >= FocusCount)
                _selectedIndex = NoFocus;

            // A selection only exists while the exit animation is playing.
            if (!_exiting)
                _selectedIndex = NoFocus;

            // Focus cannot sit on an item that has not finished appearing.
            if (!_exiting && _focusIndex != NoFocus && !IsFocusable(_focusIndex))
                _focusIndex = NoFocus;
        }

        private bool IsFocusable(int index)
        {
            if (index == ThemeSwitchIndex)
                return GetFooterEase() > EntranceCompleteThreshold;

            return index >= 0
                && index < Buttons.Length
                && GetEntranceEase(index) > EntranceCompleteThreshold;
        }

        // Keyboard and gamepad navigation starts once the first button has landed.
        // Later buttons are skipped until they appear.
        private bool InputReady => GetEntranceEase(0) > EntranceCompleteThreshold;

        private static float EaseOutCubic(float t)
        {
            float inv = 1f - t;
            return 1f - inv * inv * inv;
        }

        // Exponential smoothing that behaves the same at any frame rate.
        private float Smooth(float current, float target, float rate)
        {
            return MathHelper.Lerp(current, target, 1f - MathF.Exp(-rate * _deltaTime));
        }

        // Returns the entrance animation progress for a button.
        private float GetEntranceEase(int index)
        {
            float elapsed = (float)(_animationTime - _enteredAt - index * StaggerDelay);
            return EaseOutCubic(MathHelper.Clamp(elapsed / StaggerDuration, 0f, 1f));
        }

        // Returns how long a button waits before it starts leaving.
        private float GetExitDelay(int index)
        {
            if (index == _selectedIndex)
                return SelectedHold;

            int order = Math.Max(1, Math.Abs(index - _selectedIndex));
            return (order - 1) * ExitStagger;
        }

        // Returns the exit animation progress for a button.
        private float GetExitEase(int index)
        {
            if (!_exiting)
                return 0f;

            float elapsed = (float)(_animationTime - _exitStartedAt) - GetExitDelay(index);
            float t = MathHelper.Clamp(elapsed / ExitButtonDuration, 0f, 1f);
            return t * t;
        }

        // Returns a short size pulse for the activated item while it holds in place.
        private float GetSelectedPulse(int index)
        {
            if (!_exiting || index != _selectedIndex)
                return 0f;

            float t = MathHelper.Clamp((float)(_animationTime - _exitStartedAt) / SelectedHold, 0f, 1f);
            return MathF.Sin(t * MathHelper.Pi) * SelectedPulseScale;
        }

        // Returns the footer fade progress.
        private float GetFooterEase()
        {
            if (_exiting)
            {
                float t = MathHelper.Clamp(
                    (float)((_animationTime - _exitStartedAt) / _exitDuration),
                    0f,
                    1f);

                return 1f - t * t;
            }

            float elapsed = (float)(_animationTime - _enteredAt);
            return EaseOutCubic(MathHelper.Clamp(elapsed / StaggerDuration, 0f, 1f));
        }

        // Returns the height of each button row.
        private float GetButtonRowHeight(DynamicSpriteFont font)
        {
            string culture = Language.ActiveCulture.Name;
            if (_cachedRowHeight <= 0f || culture != _cachedRowHeightCulture)
            {
                _cachedRowHeightCulture = culture;

                float tallest = 0f;
                foreach (MenuButton button in Buttons)
                    tallest = MathF.Max(tallest, font.MeasureString(button.Label).Y);

                _cachedRowHeight = MathF.Max(ButtonSpacingYMin, tallest * ButtonScale + ButtonRowGap);
            }

            return _cachedRowHeight * SpriteBatchUtil.LayoutScale;
        }

        // Returns a button's clickable area in UI space.
        private Rectangle GetButtonHitbox(int index, ShatteredIllusionConfig config, DynamicSpriteFont font)
        {
            float layoutScale = SpriteBatchUtil.LayoutScale;
            Vector2 textSize = font.MeasureString(Buttons[index].Label) * ButtonScale * layoutScale;

            float x = config.MenuButtonPosition == HorizontalPosition.Left
                ? ButtonInset * layoutScale
                : SpriteBatchUtil.UiWidth - ButtonInset * layoutScale - textSize.X;

            Vector2 position = new(x, ButtonStartY * layoutScale + index * GetButtonRowHeight(font));

            return new Rectangle((int)position.X, (int)position.Y, (int)textSize.X, (int)textSize.Y);
        }

        private static Rectangle GetThemeSwitchHitbox(DynamicSpriteFont font)
        {
            float layoutScale = SpriteBatchUtil.LayoutScale;
            Vector2 size = font.MeasureString(ThemeSwitchText) * ThemeSwitchScale * layoutScale;
            float x = SpriteBatchUtil.UiWidth * 0.5f - size.X * 0.5f;
            float y = SpriteBatchUtil.UiHeight - ThemeSwitchBottomPadding * layoutScale - size.Y;
            return new Rectangle((int)x, (int)y, (int)size.X, (int)size.Y);
        }

        // Returns the focus index under a UI-space point, or NoFocus.
        private int HitTest(Vector2 mouse)
        {
            ShatteredIllusionConfig config = ModContent.GetInstance<ShatteredIllusionConfig>();
            DynamicSpriteFont font = MenuFont;
            Point point = mouse.ToPoint();

            for (int i = 0; i < Buttons.Length; i++)
            {
                if (IsFocusable(i) && GetButtonHitbox(i, config, font).Contains(point))
                    return i;
            }

            if (IsFocusable(ThemeSwitchIndex) && GetThemeSwitchHitbox(font).Contains(point))
                return ThemeSwitchIndex;

            return NoFocus;
        }

        // Reads mouse, keyboard and gamepad input and updates focus and activation.
        private void ProcessInput()
        {
            bool hasFocus = Main.hasFocus;
            KeyboardState keys = hasFocus ? Keyboard.GetState() : default;
            GamePadState pad = hasFocus ? GamePad.GetState(PlayerIndex.One) : default;

            try
            {
                if (_exiting)
                    return;

                Vector2 mouse = SpriteBatchUtil.UiMouse;
                bool mouseMoved = Vector2.DistanceSquared(mouse, _lastMouse) > MouseMoveThresholdSquared;
                _lastMouse = mouse;

                bool leftPressed = Main.mouseLeft && !_mouseWasDown;
                bool rightPressed = Main.mouseRight && !_mouseWasRightDown;

                UpdateMouseFocus(mouse, mouseMoved);

                if ((leftPressed || rightPressed) && HandleMouseClick(mouse, leftPressed))
                    return;

                if (!InputReady)
                    return;

                HandleDirectionalInput(keys, pad);
            }
            finally
            {
                _mouseWasDown = Main.mouseLeft;
                _mouseWasRightDown = Main.mouseRight;
                _prevKeys = keys;
                _prevPad = pad;
            }
        }

        // The mouse only takes focus when it moves, so it does not steal focus from the keyboard.
        private void UpdateMouseFocus(Vector2 mouse, bool mouseMoved)
        {
            if (!mouseMoved && _source is not (InputSource.None or InputSource.Mouse))
                return;

            int hit = HitTest(mouse);

            if (hit != NoFocus)
                SetFocus(hit, InputSource.Mouse);
            else if (_source == InputSource.Mouse)
                _focusIndex = NoFocus;
        }

        // Returns true when the click activated something.
        private bool HandleMouseClick(Vector2 mouse, bool leftPressed)
        {
            int hit = HitTest(mouse);
            if (hit == NoFocus)
                return false;

            SetFocus(hit, InputSource.Mouse);

            if (hit == ThemeSwitchIndex)
            {
                Activate(hit, leftPressed ? 1 : -1);
                return true;
            }

            if (!leftPressed)
                return false;

            Activate(hit, 1);
            return true;
        }

        private void HandleDirectionalInput(KeyboardState keys, GamePadState pad)
        {
            // Vertical movement, with repeat while held.
            bool keyUp = keys.IsKeyDown(Keys.Up) || keys.IsKeyDown(Keys.W);
            bool keyDown = keys.IsKeyDown(Keys.Down) || keys.IsKeyDown(Keys.S);
            bool padUp = pad.IsButtonDown(PadButton.DPadUp) || pad.ThumbSticks.Left.Y > StickThreshold;
            bool padDown = pad.IsButtonDown(PadButton.DPadDown) || pad.ThumbSticks.Left.Y < -StickThreshold;

            int delta = ReadRepeatedDirection(keyUp || padUp, keyDown || padDown);
            InputSource source = (padUp || padDown) && !(keyUp || keyDown)
                ? InputSource.Gamepad
                : InputSource.Keyboard;

            if (delta == 0 && KeyPressed(keys, Keys.Tab))
            {
                bool shift = keys.IsKeyDown(Keys.LeftShift) || keys.IsKeyDown(Keys.RightShift);
                delta = shift ? -1 : 1;
                source = InputSource.Keyboard;
            }

            if (delta != 0)
            {
                MoveFocus(delta, source);
                return;
            }

            // Left and right cycle the theme when the theme switch is focused.
            int keyHorizontal = Axis(
                KeyPressed(keys, Keys.Left) || KeyPressed(keys, Keys.A),
                KeyPressed(keys, Keys.Right) || KeyPressed(keys, Keys.D));
            int padHorizontal = Axis(
                PadPressed(pad, PadButton.DPadLeft),
                PadPressed(pad, PadButton.DPadRight));

            int horizontal = keyHorizontal != 0 ? keyHorizontal : padHorizontal;
            if (horizontal != 0 && _focusIndex == ThemeSwitchIndex)
            {
                _source = keyHorizontal != 0 ? InputSource.Keyboard : InputSource.Gamepad;
                Activate(ThemeSwitchIndex, horizontal);
                return;
            }

            // Shoulder buttons cycle the theme from anywhere.
            int shoulder = Axis(
                PadPressed(pad, PadButton.LeftShoulder),
                PadPressed(pad, PadButton.RightShoulder));
            if (shoulder != 0)
            {
                _source = InputSource.Gamepad;
                Activate(ThemeSwitchIndex, shoulder);
                return;
            }

            // Alt+Enter is the fullscreen shortcut, so it must not activate a button.
            bool alt = keys.IsKeyDown(Keys.LeftAlt) || keys.IsKeyDown(Keys.RightAlt);
            bool keyActivate = (KeyPressed(keys, Keys.Enter) && !alt) || KeyPressed(keys, Keys.Space);
            bool padActivate = PadPressed(pad, PadButton.A);

            if ((keyActivate || padActivate) && _focusIndex != NoFocus)
            {
                _source = keyActivate ? InputSource.Keyboard : InputSource.Gamepad;
                Activate(_focusIndex, 1);
            }
        }

        // Returns -1 or 1 on the first frame a direction is held, then again on a repeat timer.
        private int ReadRepeatedDirection(bool up, bool down)
        {
            int direction = up == down ? 0 : (up ? -1 : 1);

            if (direction == 0)
            {
                _repeatDirection = 0;
                return 0;
            }

            if (direction != _repeatDirection)
            {
                _repeatDirection = direction;
                _nextRepeatAt = _animationTime + RepeatInitialDelay;
                return direction;
            }

            if (_animationTime >= _nextRepeatAt)
            {
                _nextRepeatAt = _animationTime + RepeatInterval;
                return direction;
            }

            return 0;
        }

        private static int Axis(bool negative, bool positive)
        {
            if (negative == positive)
                return 0;

            return negative ? -1 : 1;
        }

        private bool KeyPressed(KeyboardState keys, Keys key) =>
            keys.IsKeyDown(key) && _prevKeys.IsKeyUp(key);

        private bool PadPressed(GamePadState pad, PadButton button) =>
            pad.IsButtonDown(button) && _prevPad.IsButtonUp(button);

        // Moves focus up or down, wrapping at the ends and skipping items that have not appeared yet.
        private void MoveFocus(int delta, InputSource source)
        {
            int candidate = _focusIndex;

            for (int step = 0; step < FocusCount; step++)
            {
                candidate = candidate == NoFocus
                    ? (delta > 0 ? 0 : FocusCount - 1)
                    : (candidate + delta + FocusCount) % FocusCount;

                if (IsFocusable(candidate))
                {
                    SetFocus(candidate, source);
                    return;
                }
            }
        }

        private void SetFocus(int index, InputSource source)
        {
            _source = source;

            if (index == _focusIndex)
                return;

            _focusIndex = index;

            if (index != NoFocus)
                SoundEngine.PlaySound(SoundID.MenuTick);
        }

        // Activates a focus target. Direction only matters for the theme switch.
        private void Activate(int index, int direction)
        {
            if (_exiting)
                return;

            if (index == ThemeSwitchIndex)
            {
                SoundEngine.PlaySound(SoundID.MenuTick);
                BeginExit(index, () => CycleMenuTheme(direction));
                return;
            }

            if (index < 0 || index >= Buttons.Length)
                return;

            SoundEngine.PlaySound(SoundID.MenuOpen);
            BeginExit(index, Buttons[index].OnClick);
        }

        // Moves hover values toward the focused or selected item.
        private void UpdateHoverVisuals()
        {
            for (int i = 0; i < Buttons.Length; i++)
            {
                bool highlighted = (i == _focusIndex || i == _selectedIndex)
                    && GetEntranceEase(i) > EntranceCompleteThreshold;

                Buttons[i].Hover = Smooth(Buttons[i].Hover, highlighted ? 1f : 0f, HoverSmoothing);
            }

            bool themeHighlighted = _focusIndex == ThemeSwitchIndex || _selectedIndex == ThemeSwitchIndex;
            _themeSwitchHover = Smooth(_themeSwitchHover, themeHighlighted ? 1f : 0f, HoverSmoothing);

            _bobAmount = Smooth(_bobAmount, _exiting ? 0f : 1f, BobFadeRate);
        }

        //The Larp menu cycle of doom
        private void CycleMenuTheme(int offset)
        {
            EnsureMenuLoaderAccess();
            if (_offsetModMenu == null || _currentMenuField == null || _switchToMenuField == null)
            {
                LogWarn("Could not cycle menu themes: MenuLoader internals were not found.");
                return;
            }

            _offsetModMenu.Invoke(null, new object[] { offset });

            if (_switchToMenuField.GetValue(null) is not ModMenu switchTo)
                return;

            if (_currentMenuField.GetValue(null) is not ModMenu current || current == switchTo)
                return;

            current.OnDeselected();
            _currentMenuField.SetValue(null, switchTo);
            switchTo.OnSelected();
            _switchToMenuField.SetValue(null, null);

            _lastSelectedModMenuField?.SetValue(null, switchTo.FullName);
            Main.SaveSettings();
        }

        //MAN I HATE WARNINGS SO MUCH LIKE WHY CANT THE CODE BE ASS LIKE WHY
        [SuppressMessage("Security", "S3011:Method should not allow accessing private members via reflection", Justification = "No public tModLoader API exists for cycling MenuLoader's active mod menu; reflection into its internals is required.")]
        private void EnsureMenuLoaderAccess()
        {
            if (_menuLoaderAccessResolved)
                return;

            _menuLoaderAccessResolved = true;
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            Type menuLoader = typeof(MenuLoader);
            _offsetModMenu = menuLoader.GetMethod("OffsetModMenu", flags);
            _currentMenuField = menuLoader.GetField("currentMenu", flags);
            _switchToMenuField = menuLoader.GetField("switchToMenu", flags);
            _lastSelectedModMenuField = menuLoader.GetField("LastSelectedModMenu", flags);
        }

        // Starts the button exit animation and marks the activated item as selected.
        private void BeginExit(int selectedIndex, Action action)
        {
            if (_exiting)
                return;

            _exiting = true;
            _exitStartedAt = _animationTime;
            _selectedIndex = selectedIndex;
            _pendingAction = action;

            // The action runs once the last button has finished leaving.
            float longest = 0f;
            for (int i = 0; i < Buttons.Length; i++)
                longest = MathF.Max(longest, GetExitDelay(i) + ExitButtonDuration);

            _exitDuration = longest;
        }

        private void DrawOwnButtons()
        {
            SpriteBatch spriteBatch = Main.spriteBatch;

            try
            {
                if (MenuLoader.CurrentMenu is ShatteredIllusionMainMenu menu)
                {
                    Vector2 logoCenter = default;
                    float logoRotation = 0f;
                    float logoScale = 1f;
                    Color drawColor = Color.White;
                    menu.PreDrawLogo(spriteBatch, ref logoCenter, ref logoRotation, ref logoScale, ref drawColor);
                }

                SpriteBatchUtil.Restart(spriteBatch, BlendState.AlphaBlend, SamplerState.PointClamp);

                DrawButtons(spriteBatch);

                DrawFooter(spriteBatch);

                // Keyboard and gamepad users do not need the mouse cursor on screen.
                if (_source is InputSource.None or InputSource.Mouse)
                    Main.DrawCursor(Main.DrawThickCursor(false), false);
            }
            catch (Exception ex)
            {
                LogWarn("Menu draw failed", ex);
            }
            finally
            {
                SpriteBatchUtil.TryEnd(spriteBatch);
            }
        }

        // Draws all menu buttons.
        private void DrawButtons(SpriteBatch spriteBatch)
        {
            ShatteredIllusionConfig config = ModContent.GetInstance<ShatteredIllusionConfig>();
            DynamicSpriteFont font = MenuFont;

            bool leftSide = config.MenuButtonPosition == HorizontalPosition.Left;

            // Text scales around the edge it is aligned to, so hovering never pushes it
            // toward the screen edge or into the logo.
            float anchorX = leftSide ? 0f : 1f;
            float layoutScale = SpriteBatchUtil.LayoutScale;

            for (int i = 0; i < Buttons.Length; i++)
            {
                MenuButton button = Buttons[i];

                float entranceEase = GetEntranceEase(i);
                float exitEase = GetExitEase(i);
                float visibility = entranceEase * (1f - exitEase);
                if (visibility <= 0f)
                    continue;

                Rectangle hitbox = GetButtonHitbox(i, config, font);

                float scale = ButtonScale * layoutScale
                    * MathHelper.Lerp(1f, HoverScale, button.Hover)
                    * (1f + GetSelectedPulse(i));
                Vector2 scaledSize = font.MeasureString(button.Label) * scale;

                float dir = leftSide ? -1f : 1f;
                float slideOffset = ((1f - entranceEase) + exitEase) * SlideDistance * layoutScale * dir;

                Vector2 anchor = new(
                    hitbox.X + hitbox.Width * anchorX + slideOffset,
                    hitbox.Y + hitbox.Height * 0.5f);

                // The bob fades in as the button lands and fades out when leaving.
                float bobPhase = Main.GlobalTimeWrappedHourly * IdleBobSpeed + i * 0.9f;
                anchor.Y += MathF.Sin(bobPhase) * IdleBobAmplitude * entranceEase * _bobAmount;

                Vector2 position = new(
                    MathF.Round(anchor.X - scaledSize.X * anchorX),
                    MathF.Round(anchor.Y - scaledSize.Y * 0.5f));

                Color baseColor = Color.Lerp(Color.White, Main.OurFavoriteColor, button.Hover);
                Color color = baseColor * visibility;

                // While exiting, only the selected button keeps its highlight.
                bool showHighlight = !_exiting || i == _selectedIndex;
                if (showHighlight && button.Hover > HoverThresholdForUnderline)
                {
                    DrawHoverGlow(spriteBatch, position, scaledSize, button.Hover * visibility);
                    DrawUnderline(spriteBatch, position, font, scaledSize, button.Hover * visibility, scale);
                }

                ChatManager.DrawColorCodedStringWithShadow(
                    spriteBatch,
                    font,
                    button.Label,
                    position,
                    color,
                    0f,
                    Vector2.Zero,
                    new Vector2(scale));
            }
        }

        // Draws the glow behind a highlighted button as stacked rectangles, bigger and fainter outward.
        private static void DrawHoverGlow(SpriteBatch spriteBatch, Vector2 position, Vector2 textSize, float alpha)
        {
            if (alpha <= 0f)
                return;

            float layerAlpha = GlowMaxAlpha / GlowLayers * alpha;

            for (int layer = GlowLayers; layer >= 1; layer--)
            {
                float pad = GlowMaxPad * SpriteBatchUtil.LayoutScale * layer / GlowLayers;

                Rectangle glowRect = new(
                    (int)(position.X - pad),
                    (int)(position.Y - pad * 0.5f),
                    (int)(textSize.X + pad * 2f),
                    (int)(textSize.Y + pad));

                spriteBatch.Draw(TextureAssets.MagicPixel.Value, glowRect, Main.OurFavoriteColor * layerAlpha);
            }
        }

        // Draws the version text and theme switcher.
        private void DrawFooter(SpriteBatch spriteBatch)
        {
            float footerEase = GetFooterEase();
            if (footerEase <= 0f)
                return;

            DynamicSpriteFont font = MenuFont;

            float layoutScale = SpriteBatchUtil.LayoutScale;

            string versionText = $"{Mod.Name} v{Mod.Version}";
            Vector2 position = new(20f * layoutScale, SpriteBatchUtil.UiHeight - 34f * layoutScale);

            ChatManager.DrawColorCodedStringWithShadow(
                spriteBatch,
                font,
                versionText,
                position,
                Color.White * (0.7f * footerEase),
                0f,
                Vector2.Zero,
                new Vector2(FooterScale * layoutScale));

            DrawThemeSwitch(spriteBatch, font, footerEase);
        }

        // Draws the theme switch button.
        private void DrawThemeSwitch(SpriteBatch spriteBatch, DynamicSpriteFont font, float footerEase)
        {
            string text = ThemeSwitchText;
            Rectangle hitbox = GetThemeSwitchHitbox(font);
            Color baseColor = Color.Lerp(new Color(200, 200, 200), Main.OurFavoriteColor, _themeSwitchHover);

            // Scales around its center so the selection pulse stays in place.
            float scale = ThemeSwitchScale * SpriteBatchUtil.LayoutScale * (1f + GetSelectedPulse(ThemeSwitchIndex));
            Vector2 scaledSize = font.MeasureString(text) * scale;
            Vector2 center = new(hitbox.X + hitbox.Width * 0.5f, hitbox.Y + hitbox.Height * 0.5f);
            Vector2 position = new(
                MathF.Round(center.X - scaledSize.X * 0.5f),
                MathF.Round(center.Y - scaledSize.Y * 0.5f));

            ChatManager.DrawColorCodedStringWithShadow(
                spriteBatch,
                font,
                text,
                position,
                baseColor * footerEase,
                0f,
                Vector2.Zero,
                new Vector2(scale));

            bool showHighlight = !_exiting || _selectedIndex == ThemeSwitchIndex;
            if (showHighlight && _themeSwitchHover > HoverThresholdForUnderline)
            {
                Rectangle bar = new(hitbox.X, hitbox.Bottom + 1, hitbox.Width, 1);
                spriteBatch.Draw(TextureAssets.MagicPixel.Value, bar, Main.OurFavoriteColor * (_themeSwitchHover * footerEase));
            }
        }

        // Draws the underline under a highlighted button.
        private static void DrawUnderline(SpriteBatch spriteBatch, Vector2 position, DynamicSpriteFont font, Vector2 textSize, float alpha, float scale)
        {
            float capHeight = font.MeasureString(UnderlineReferenceGlyphs).Y * scale;

            Rectangle bar = new(
                (int)position.X,
                (int)(position.Y + capHeight + 2f),
                (int)textSize.X,
                Math.Max(2, (int)MathF.Round(2f * SpriteBatchUtil.LayoutScale)));

            spriteBatch.Draw(TextureAssets.MagicPixel.Value, bar, Main.OurFavoriteColor * alpha);
        }

        private static void LogWarn(string message)
        {
            ModContent.GetInstance<ShatteredIllusionMenuHooks>()?.Mod.Logger.Warn(message);
        }

        private static void LogWarn(string message, Exception ex)
        {
            ModContent.GetInstance<ShatteredIllusionMenuHooks>()?.Mod.Logger.Warn($"{message}: {ex}");
        }

        private static void OpenAchievements()
        {
            try
            {
                Main.menuMode = 888;
                Main.MenuUI.SetState(Main.AchievementsMenu);
            }
            catch (Exception ex)
            {
                LogWarn("Failed to open achievements", ex);
            }
        }

        private static void OpenWorkshop()
        {
            try
            {
                Main.menuMode = 888;
                UIWorkshopHub workshopHub = new(null);
                workshopHub.EnterHub();
                Main.MenuUI.SetState(workshopHub);
            }
            catch (Exception ex)
            {
                LogWarn("Failed to open workshop", ex);
            }
        }
    }
}