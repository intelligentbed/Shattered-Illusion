using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
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

namespace ShatteredIllusion.MainMenu
{
    public class ShatteredIllusionMenuHooks : ModSystem
    {
        // Stores a menu button and what it does.
        private sealed class MenuButton
        {
            public readonly string Label;
            public readonly Action OnClick;
            public float Hover;

            public MenuButton(string label, Action onClick)
            {
                Label = label;
                OnClick = onClick;
            }
        }

        // Main menu buttons.
        private static readonly MenuButton[] Buttons =
        {
            new("Single Player", () => Main.menuMode = 1),
            new("Multiplayer", () => Main.menuMode = 12),
            new("Achievements", OpenAchievements),
            new("Settings", () => Main.menuMode = 11),
            new("Workshop", OpenWorkshop),
            new("Exit", () => Main.instance.Exit()),
        };

        // Button layout settings.
        private const float ButtonStartY = 300f;
        private const float ButtonSpacingYMin = 58f;
        private const float ButtonRowGap = 16f;
        private const float ButtonInset = 90f;
        private const float ButtonScale = 0.78f;
        private const string UnderlineReferenceGlyphs = "AEIOUXY";

        private static float _cachedRowHeight = -1f;

        // Button entrance animation.
        private const float SlideDistance = 140f;
        private const float StaggerDelay = 0.07f;
        private const float StaggerDuration = 0.35f;

        // Button exit animation.
        private const float ExitStagger = 0.045f;
        private const float ExitButtonDuration = 0.22f;
        private static readonly float TotalExitDuration =
            ExitStagger * (Buttons.Length - 1) + ExitButtonDuration;

        // Hover and animation settings.
        private const float HoverLerpSpeed = 0.2f;
        private const float HoverThresholdForClickSound = 0.05f;
        private const float HoverThresholdForUnderline = 0.02f;
        private const float EntranceCompleteThreshold = 0.99f;
        private const float FooterScale = 0.42f;
        private const float ThemeSwitchScale = 0.39f;
        private const float ThemeSwitchBottomPadding = 16f;

        // Hover glow settings.
        private const int GlowLayers = 4;
        private const float GlowMaxAlpha = 0.10f;
        private const float GlowMaxPad = 14f;

        // Small idle movement.
        private const float IdleBobAmplitude = 0.8f;
        private const float IdleBobSpeed = 1.15f;

        // Tracks menu and mouse state.
        private static bool _wasActive;
        private static float _enteredAt = -1000f;
        private static bool _mouseWasDown;
        private static bool _mouseWasRightDown;

        // Tracks the exit animation.
        private static bool _exiting;
        private static float _exitStartedAt;
        private static Action _pendingAction;

        private static float _themeSwitchHover;

        // Font used by the menu.
        private static DynamicSpriteFont MenuFont => FontAssets.DeathText.Value;

        // tModLoader menu fields.
        private static MethodInfo _offsetModMenu;
        private static FieldInfo _currentMenuField;
        private static FieldInfo _switchToMenuField;
        private static FieldInfo _lastSelectedModMenuField;
        private static bool _menuLoaderAccessResolved;

        // Text shown for the theme switcher.
        private static string ThemeSwitchText =>
            Language.GetTextValue("tModLoader.ModMenuSwap") + ": " +
            (MenuLoader.CurrentMenu?.DisplayName ?? "tModLoader");

        public override void Load()
        {
            On_Main.DrawMenu += Main_DrawMenu;
        }

        public override void Unload()
        {
            On_Main.DrawMenu -= Main_DrawMenu;
        }

        // Handles drawing and menu input.
        private void Main_DrawMenu(On_Main.orig_DrawMenu orig, Main self, GameTime gameTime)
        {
            if (_exiting && Main.GlobalTimeWrappedHourly - _exitStartedAt >= TotalExitDuration)
            {
                _exiting = false;
                Action action = _pendingAction;
                _pendingAction = null;
                action?.Invoke();
            }

            bool active = IsOurMenuActive();

            if (active && !_wasActive)
            {
                _enteredAt = Main.GlobalTimeWrappedHourly;
                _exiting = false;
                _pendingAction = null;
            }

            _wasActive = active;

            if (!active)
            {
                _mouseWasDown = Main.mouseLeft;
                _mouseWasRightDown = Main.mouseRight;
                orig(self, gameTime);
                return;
            }

            ProcessButtonInput();
            RepaintAndDrawOwnButtons();
        }

        // Checks if our custom menu is active.
        private static bool IsOurMenuActive()
        {
            return Main.menuMode == 0 &&
                   MenuLoader.CurrentMenu is ShatteredIllusionMainMenu;
        }

        // Gets the entrance animation progress.
        private static float GetEntranceEase(int index)
        {
            float elapsed = Main.GlobalTimeWrappedHourly - _enteredAt - index * StaggerDelay;
            float t = MathHelper.Clamp(elapsed / StaggerDuration, 0f, 1f);
            float inv = 1f - t;
            return 1f - inv * inv * inv;
        }

        // Gets the exit animation progress.
        private static float GetExitEase(int index)
        {
            if (!_exiting)
                return 0f;

            float elapsed = Main.GlobalTimeWrappedHourly - _exitStartedAt - index * ExitStagger;
            float t = MathHelper.Clamp(elapsed / ExitButtonDuration, 0f, 1f);
            return t * t * t;
        }

        // Gets the footer animation progress.
        private static float GetFooterEase()
        {
            if (_exiting)
                return 1f - MathHelper.Clamp((Main.GlobalTimeWrappedHourly - _exitStartedAt) / TotalExitDuration, 0f, 1f);

            float elapsed = Main.GlobalTimeWrappedHourly - _enteredAt;
            return MathHelper.Clamp(elapsed / StaggerDuration, 0f, 1f);
        }

        // Gets the height of each button row.
        private static float GetButtonRowHeight(DynamicSpriteFont font)
        {
            if (_cachedRowHeight > 0f)
                return _cachedRowHeight;

            float tallest = 0f;
            foreach (MenuButton button in Buttons)
                tallest = MathF.Max(tallest, font.MeasureString(button.Label).Y);

            _cachedRowHeight = MathF.Max(ButtonSpacingYMin, tallest * ButtonScale + ButtonRowGap);
            return _cachedRowHeight;
        }

        // Gets a button's clickable area.
        private static Rectangle GetButtonHitbox(int index, ShatteredIllusionConfig config, DynamicSpriteFont font)
        {
            Vector2 textSize = font.MeasureString(Buttons[index].Label) * ButtonScale;

            float x = config.MenuButtonPosition == HorizontalPosition.Left
                ? ButtonInset
                : Main.screenWidth - ButtonInset - textSize.X;

            Vector2 position = new(x, ButtonStartY + index * GetButtonRowHeight(font));

            return new Rectangle((int)position.X, (int)position.Y, (int)textSize.X, (int)textSize.Y);
        }

        // Gets the theme switcher's clickable area.
        private static Rectangle GetThemeSwitchHitbox(DynamicSpriteFont font)
        {
            Vector2 size = font.MeasureString(ThemeSwitchText) * ThemeSwitchScale;
            float x = Main.screenWidth * 0.5f - size.X * 0.5f;
            float y = Main.screenHeight - ThemeSwitchBottomPadding - size.Y;
            return new Rectangle((int)x, (int)y, (int)size.X, (int)size.Y);
        }

        // Handles mouse input.
        private static void ProcessButtonInput()
        {
            if (_exiting)
            {
                _mouseWasDown = Main.mouseLeft;
                _mouseWasRightDown = Main.mouseRight;
                return;
            }

            bool justPressed = Main.mouseLeft && !_mouseWasDown;
            bool justPressedRight = Main.mouseRight && !_mouseWasRightDown;

            Point mouse = Main.MouseScreen.ToPoint();
            ShatteredIllusionConfig config = ModContent.GetInstance<ShatteredIllusionConfig>();
            DynamicSpriteFont font = MenuFont;

            for (int i = 0; i < Buttons.Length; i++)
            {
                MenuButton button = Buttons[i];
                Rectangle hitbox = GetButtonHitbox(i, config, font);

                bool isHovered = GetEntranceEase(i) > EntranceCompleteThreshold && hitbox.Contains(mouse);

                float previousHover = button.Hover;
                button.Hover = MathHelper.Lerp(button.Hover, isHovered ? 1f : 0f, HoverLerpSpeed);

                if (isHovered && previousHover < HoverThresholdForClickSound)
                    SoundEngine.PlaySound(SoundID.MenuTick);

                if (isHovered && justPressed)
                {
                    SoundEngine.PlaySound(SoundID.MenuOpen);
                    BeginExit(button.OnClick);
                    break;
                }
            }

            if (!_exiting)
            {
                bool switchHovered = GetThemeSwitchHitbox(font).Contains(mouse);
                float previousSwitchHover = _themeSwitchHover;
                _themeSwitchHover = MathHelper.Lerp(_themeSwitchHover, switchHovered ? 1f : 0f, HoverLerpSpeed);

                if (switchHovered && previousSwitchHover < HoverThresholdForClickSound)
                    SoundEngine.PlaySound(SoundID.MenuTick);

                if (switchHovered && justPressed)
                {
                    SoundEngine.PlaySound(SoundID.MenuTick);
                    BeginExit(() => CycleMenuTheme(1));
                }
                else if (switchHovered && justPressedRight)
                {
                    SoundEngine.PlaySound(SoundID.MenuTick);
                    BeginExit(() => CycleMenuTheme(-1));
                }
            }

            _mouseWasDown = Main.mouseLeft;
            _mouseWasRightDown = Main.mouseRight;
        }

        // Switches to another tModLoader menu theme.
        private static void CycleMenuTheme(int offset)
        {
            EnsureMenuLoaderAccess();
            if (_offsetModMenu == null || _currentMenuField == null || _switchToMenuField == null)
            {
                ModContent.GetInstance<ShatteredIllusionMenuHooks>()?.Mod?.Logger.Warn(
                    "Could not cycle menu themes: MenuLoader internals were not found.");
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

        // Finds the private MenuLoader members we need.
        private static void EnsureMenuLoaderAccess()
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

        // Starts the button exit animation.
        private static void BeginExit(Action action)
        {
            if (_exiting)
                return;

            _exiting = true;
            _exitStartedAt = Main.GlobalTimeWrappedHourly;
            _pendingAction = action;
        }

        // Draws the custom menu.
        private static void RepaintAndDrawOwnButtons()
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

                Main.DrawCursor(Main.DrawThickCursor(false), false);
            }
            catch (Exception ex)
            {
                ModContent.GetInstance<ShatteredIllusionMenuHooks>()?.Mod?.Logger.Warn($"Menu draw failed: {ex}");
            }
            finally
            {
                SpriteBatchUtil.TryEnd(spriteBatch);
            }
        }

        // Draws all menu buttons.
        private static void DrawButtons(SpriteBatch spriteBatch)
        {
            ShatteredIllusionConfig config = ModContent.GetInstance<ShatteredIllusionConfig>();
            DynamicSpriteFont font = MenuFont;

            bool leftSide = config.MenuButtonPosition == HorizontalPosition.Left;

            for (int i = 0; i < Buttons.Length; i++)
            {
                MenuButton button = Buttons[i];

                float entranceEase = GetEntranceEase(i);
                float exitEase = GetExitEase(i);
                float visibility = entranceEase * (1f - exitEase);
                if (visibility <= 0f)
                    continue;

                Rectangle hitbox = GetButtonHitbox(i, config, font);

                float dir = leftSide ? -1f : 1f;
                float slideOffset = ((1f - entranceEase) + exitEase) * SlideDistance * dir;

                Vector2 position = new(hitbox.X + slideOffset, hitbox.Y);

                if (entranceEase > EntranceCompleteThreshold && !_exiting)
                {
                    float bobPhase = Main.GlobalTimeWrappedHourly * IdleBobSpeed + i * 0.9f;
                    position.Y += MathF.Sin(bobPhase) * IdleBobAmplitude;
                }

                Color baseColor = Color.Lerp(Color.White, Main.OurFavoriteColor, button.Hover);
                Color color = baseColor * visibility;

                float scale = ButtonScale * MathHelper.Lerp(1f, 1.08f, button.Hover);

                if (!_exiting && button.Hover > HoverThresholdForUnderline)
                {
                    DrawHoverGlow(spriteBatch, position, font, button, button.Hover * visibility, scale);
                    DrawUnderline(spriteBatch, position, font, button, button.Hover * visibility, scale);
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

        // Draws the glow behind a hovered button.
        private static void DrawHoverGlow(SpriteBatch spriteBatch, Vector2 position, DynamicSpriteFont font, MenuButton button, float alpha, float scale)
        {
            if (alpha <= 0f)
                return;

            Vector2 textSize = font.MeasureString(button.Label) * scale;

            for (int layer = GlowLayers; layer >= 1; layer--)
            {
                float layerT = layer / (float)GlowLayers;
                float pad = GlowMaxPad * layerT;
                float layerAlpha = GlowMaxAlpha * (1f - layerT) * alpha;

                Rectangle glowRect = new(
                    (int)(position.X - pad),
                    (int)(position.Y - pad * 0.5f),
                    (int)(textSize.X + pad * 2f),
                    (int)(textSize.Y + pad));

                spriteBatch.Draw(TextureAssets.MagicPixel.Value, glowRect, Main.OurFavoriteColor * layerAlpha);
            }
        }

        // Draws the version text and theme switcher.
        private static void DrawFooter(SpriteBatch spriteBatch)
        {
            Mod mod = ModContent.GetInstance<ShatteredIllusionMenuHooks>()?.Mod;
            if (mod == null)
                return;

            float footerEase = GetFooterEase();
            if (footerEase <= 0f)
                return;

            DynamicSpriteFont font = MenuFont;

            string versionText = $"{mod.Name} v{mod.Version}";
            Vector2 position = new(20f, Main.screenHeight - 34f);

            ChatManager.DrawColorCodedStringWithShadow(
                spriteBatch,
                font,
                versionText,
                position,
                Color.White * (0.7f * footerEase),
                0f,
                Vector2.Zero,
                new Vector2(FooterScale));

            DrawThemeSwitch(spriteBatch, font, footerEase);
        }

        // Draws the theme switch button.
        private static void DrawThemeSwitch(SpriteBatch spriteBatch, DynamicSpriteFont font, float footerEase)
        {
            Rectangle hitbox = GetThemeSwitchHitbox(font);
            Color baseColor = Color.Lerp(new Color(200, 200, 200), Main.OurFavoriteColor, _themeSwitchHover);

            ChatManager.DrawColorCodedStringWithShadow(
                spriteBatch,
                font,
                ThemeSwitchText,
                new Vector2(hitbox.X, hitbox.Y),
                baseColor * footerEase,
                0f,
                Vector2.Zero,
                new Vector2(ThemeSwitchScale));

            if (!_exiting && _themeSwitchHover > HoverThresholdForUnderline)
            {
                Rectangle bar = new(hitbox.X, hitbox.Bottom + 1, hitbox.Width, 1);
                spriteBatch.Draw(TextureAssets.MagicPixel.Value, bar, Main.OurFavoriteColor * (_themeSwitchHover * footerEase));
            }
        }

        // Draws the underline under a hovered button.
        private static void DrawUnderline(SpriteBatch spriteBatch, Vector2 position, DynamicSpriteFont font, MenuButton button, float alpha, float scale)
        {
            Vector2 textSize = font.MeasureString(button.Label) * scale;
            float capHeight = font.MeasureString(UnderlineReferenceGlyphs).Y * scale;

            Rectangle bar = new(
                (int)position.X,
                (int)(position.Y + capHeight + 2f),
                (int)textSize.X,
                2);

            spriteBatch.Draw(TextureAssets.MagicPixel.Value, bar, Main.OurFavoriteColor * alpha);
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
                ModContent.GetInstance<ShatteredIllusionMenuHooks>()?.Mod?.Logger.Warn($"Failed to open achievements: {ex}");
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
                ModContent.GetInstance<ShatteredIllusionMenuHooks>()?.Mod?.Logger.Warn($"Failed to open workshop: {ex}");
            }
        }
    }
}
