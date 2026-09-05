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
        // -------------------------------
        // BUTTON MODEL
        // -------------------------------
        // Each button carries its own label, click action, and per-frame hover
        // state, so there's no risk of a parallel "Hover[]" array drifting out
        // of sync with the button list.
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

        private static readonly MenuButton[] Buttons =
        {
            new("Single Player", () => Main.menuMode = 1),
            new("Multiplayer", () => Main.menuMode = 12),
            new("Achievements", OpenAchievements),
            new("Settings", () => Main.menuMode = 11),
            new("Workshop", OpenWorkshop),
            new("Exit", () => Main.instance.Exit()),
        };

        // -------------------------------
        // LAYOUT / TIMING CONSTANTS
        // -------------------------------
        private const float ButtonStartY = 300f;
        // Was a flat 58f, tuned for the old custom font. Death-text's
        // measured line height (MeasureString().Y) includes a lot more
        // built-in padding below the baseline than the old font did -- that
        // extra padding is exactly what made the hover underline (drawn
        // relative to that measured height) land at or past the next
        // button's row, 58px down. Row height is now computed from the
        // font itself in GetButtonRowHeight below, so it can't silently
        // desync from font/scale changes again; this constant is just the
        // floor it will never go under.
        private const float ButtonSpacingYMin = 58f;
        private const float ButtonRowGap = 16f;
        private const float ButtonInset = 90f;
        // Vanilla death-text font reads noticeably bigger than the old custom
        // font did at the same scale -- 0.78 lands close to how the original
        // custom-font 0.75 actually looked on screen.
        private const float ButtonScale = 0.78f;
        // Reference glyphs with no descenders, used to find where the
        // *visible* text actually ends -- as opposed to font.MeasureString
        // on the real label, which grows with the label's own descenders
        // ("Multiplayer"'s "y"/"p" vs. "Settings" having none) and made the
        // underline sit at a different depth per button.
        private const string UnderlineReferenceGlyphs = "AEIOUXY";

        private static float _cachedRowHeight = -1f;

        // -------------------------------
        // ENTRANCE (in)
        // -------------------------------
        private const float SlideDistance = 140f;
        private const float StaggerDelay = 0.07f;
        private const float StaggerDuration = 0.35f;

        // -------------------------------
        // EXIT (out) -- plays before a click's action actually takes effect,
        // so leaving the menu feels like a deliberate transition instead of
        // an instant cut. Same cascade direction as the entrance, played in
        // reverse motion (buttons slide back out the way they came in).
        // -------------------------------
        private const float ExitStagger = 0.045f;
        private const float ExitButtonDuration = 0.22f;
        private static readonly float TotalExitDuration =
            ExitStagger * (Buttons.Length - 1) + ExitButtonDuration;

        private const float HoverLerpSpeed = 0.2f;
        private const float HoverThresholdForClickSound = 0.05f;
        private const float HoverThresholdForUnderline = 0.02f;
        private const float EntranceCompleteThreshold = 0.99f;
        private const float FooterScale = 0.42f;
        private const float ThemeSwitchScale = 0.39f;
        private const float ThemeSwitchBottomPadding = 16f;

        // Soft glow drawn behind a hovered label -- a few stacked, slightly
        // oversized, low-alpha rectangles fake a blur without needing an
        // actual blur shader/render target.
        private const int GlowLayers = 4;
        private const float GlowMaxAlpha = 0.10f;
        private const float GlowMaxPad = 14f;

        // Idle bob only kicks in once a button has fully entered (and never
        // while exiting), so it can't fight the slide animations. Toned way
        // down from the first pass -- this should read as "alive", not "seasick".
        private const float IdleBobAmplitude = 0.8f;
        private const float IdleBobSpeed = 1.15f;

        private static bool _wasActive;
        private static float _enteredAt = -1000f;
        // Tracks mouse buttons ourselves instead of relying on Main.mouseLeftRelease
        // / mouseRightRelease to reset -- see the long comment on ProcessButtonInput for why.
        private static bool _mouseWasDown;
        private static bool _mouseWasRightDown;

        // Set true the moment a click is accepted; the actual consequence of
        // that click (menu-mode change, exit, cycle theme) is
        // deferred in _pendingAction until the collapse animation finishes.
        // Input is frozen while this is true.
        private static bool _exiting;
        private static float _exitStartedAt;
        private static Action _pendingAction;

        private static float _themeSwitchHover;

        // -------------------------------
        // MENU FONT
        // -------------------------------
        // Per request: no more custom .xnb font. Buttons/footer all use the
        // same vanilla death-text font everything else on the title screen
        // uses, so there's nothing to load and nothing that can fail to load.
        private static DynamicSpriteFont MenuFont => FontAssets.DeathText.Value;

        // -------------------------------
        // THEME SWITCH (footer)
        // -------------------------------
        // Skipping orig() means vanilla's "Switch Menu Theme" control never
        // runs. The only public MenuLoader API (ActivateOldVanillaMenu) jumps
        // to the 1.3 logo -- it does not cycle. tModLoader's real cycle lives
        // in private OffsetModMenu, which we invoke through reflection so
        // left-click advances and right-click goes back, same as vanilla.
        //
        // We used to "hand off" to orig() instead (_suppressed). That left
        // CurrentMenu on our theme while drawing tModLoader's stock buttons,
        // so cycling back could land on our backdrop with no custom buttons
        // (and sometimes no vanilla ones either) until you left and returned.
        private static MethodInfo _offsetModMenu;
        private static FieldInfo _currentMenuField;
        private static FieldInfo _switchToMenuField;
        private static FieldInfo _lastSelectedModMenuField;
        private static bool _menuLoaderAccessResolved;

        private static string ThemeSwitchText =>
            Language.GetTextValue("tModLoader.ModMenuSwap") + ": " +
            (MenuLoader.CurrentMenu?.DisplayName ?? "tModLoader");

        // -------------------------------
        // HOOK SETUP
        // -------------------------------
        public override void Load()
        {
            On_Main.DrawMenu += Main_DrawMenu;
        }

        public override void Unload()
        {
            On_Main.DrawMenu -= Main_DrawMenu;
        }

        // -------------------------------
        // MAIN MENU OVERRIDE
        // -------------------------------
        // This is a full detour, not a partial one: when our menu is active,
        // `orig` (vanilla Main.DrawMenu) never runs at all. Vanilla's button
        // hit-testing, click handling, and drawing all live inside that one
        // method with no separate sub-method to hook, so skipping `orig`
        // entirely is what removes the vanilla buttons from both rendering
        // AND interaction -- there is nothing left running to click or draw.
        private void Main_DrawMenu(On_Main.orig_DrawMenu orig, Main self, GameTime gameTime)
        {
            // Collapse animation finished -- now actually apply whatever the
            // click was for (change menuMode, open a UI, cycle the theme).
            if (_exiting && Main.GlobalTimeWrappedHourly - _exitStartedAt >= TotalExitDuration)
            {
                _exiting = false;
                Action action = _pendingAction;
                _pendingAction = null;
                action?.Invoke();
            }

            bool active = IsOurMenuActive();

            // The inactive -> active edge has to be caught here, not inside
            // ProcessButtonInput. ProcessButtonInput is only ever called from
            // the branch below, which already requires active == true -- so
            // by the time it ran, "active" was always true and the
            // "!_wasActive" check next to it was dead code. _wasActive
            // latched true on the very first frame and never went back to
            // false, so the entrance animation could only ever play once
            // per game session and never replayed on later visits to the
            // menu (e.g. backing out of Single Player selection back to the
            // title screen) -- which is why only the exit animation ever
            // seemed to work.
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

        // -------------------------------
        // MENU STATE CHECK
        // -------------------------------
        private static bool IsOurMenuActive()
        {
            return Main.menuMode == 0 &&
                   MenuLoader.CurrentMenu is ShatteredIllusionMainMenu;
        }

        // -------------------------------
        // BUTTON ANIMATION
        // -------------------------------
        private static float GetEntranceEase(int index)
        {
            float elapsed = Main.GlobalTimeWrappedHourly - _enteredAt - index * StaggerDelay;
            float t = MathHelper.Clamp(elapsed / StaggerDuration, 0f, 1f);
            float inv = 1f - t;
            return 1f - inv * inv * inv;
        }

        // 0 = not collapsing, 1 = fully collapsed. Same top-to-bottom cascade
        // order as the entrance, just running the slide back out instead of in.
        private static float GetExitEase(int index)
        {
            if (!_exiting)
                return 0f;

            float elapsed = Main.GlobalTimeWrappedHourly - _exitStartedAt - index * ExitStagger;
            float t = MathHelper.Clamp(elapsed / ExitButtonDuration, 0f, 1f);
            return t * t * t; // ease-in -- starts slow, snaps out at the end
        }

        private static float GetFooterEase()
        {
            if (_exiting)
                return 1f - MathHelper.Clamp((Main.GlobalTimeWrappedHourly - _exitStartedAt) / TotalExitDuration, 0f, 1f);

            float elapsed = Main.GlobalTimeWrappedHourly - _enteredAt;
            return MathHelper.Clamp(elapsed / StaggerDuration, 0f, 1f);
        }

        // Row height cleared by the tallest label's own full measured height
        // (worst case, whichever button that is) plus room for the hover
        // underline drawn below it -- computed once and cached rather than
        // hard-coded, so it tracks whatever the font/scale actually need
        // instead of a number that can quietly go stale again later.
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

        private static Rectangle GetButtonHitbox(int index, ShatteredIllusionConfig config, DynamicSpriteFont font)
        {
            Vector2 textSize = font.MeasureString(Buttons[index].Label) * ButtonScale;

            float x = config.MenuButtonPosition == HorizontalPosition.Left
                ? ButtonInset
                : Main.screenWidth - ButtonInset - textSize.X;

            Vector2 position = new(x, ButtonStartY + index * GetButtonRowHeight(font));

            return new Rectangle((int)position.X, (int)position.Y, (int)textSize.X, (int)textSize.Y);
        }

        // Bottom-center, mirroring where vanilla/tModLoader itself puts the
        // "Switch Menu Theme" text.
        private static Rectangle GetThemeSwitchHitbox(DynamicSpriteFont font)
        {
            Vector2 size = font.MeasureString(ThemeSwitchText) * ThemeSwitchScale;
            float x = Main.screenWidth * 0.5f - size.X * 0.5f;
            float y = Main.screenHeight - ThemeSwitchBottomPadding - size.Y;
            return new Rectangle((int)x, (int)y, (int)size.X, (int)size.Y);
        }

        // -------------------------------
        // INPUT HANDLING
        // -------------------------------
        // Doesn't touch the SpriteBatch at all -- safe to run before we know
        // anything about batch state.
        //
        // CLICK DETECTION: Main.mouseLeftRelease is only ever reset back to
        // true by vanilla's own Main.DrawMenu (the same inline hit-test/click/
        // draw code the comment on Main_DrawMenu above describes) -- confirmed
        // by logging it: once consumed, it stayed false across totally
        // separate clicks, seconds apart, with the mouse fully released in
        // between. Since orig() never runs in full-detour mode, nothing is
        // left to ever re-arm it. So we track the press edge ourselves via
        // _mouseWasDown instead of depending on that shared field's reset.
        private static void ProcessButtonInput()
        {
            // Buttons are mid-collapse -- ignore new input until the pending
            // action actually fires (handled up in Main_DrawMenu).
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

            // Recorded last so justPressed above reflects this frame's edge.
            _mouseWasDown = Main.mouseLeft;
            _mouseWasRightDown = Main.mouseRight;
        }

        // Same sequence vanilla uses in MenuLoader.UpdateAndDrawModMenuInner:
        // OffsetModMenu picks the next/previous available ModMenu, then the
        // current/switchTo fields actually swap. OffsetModMenu alone is not
        // enough -- the swap is applied inside orig(), which we skip.
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

        private static void BeginExit(Action action)
        {
            if (_exiting)
                return;

            _exiting = true;
            _exitStartedAt = Main.GlobalTimeWrappedHourly;
            _pendingAction = action;
        }

        // -------------------------------
        // DRAW CUSTOM MENU BUTTONS
        // -------------------------------
        // Deliberately does NOT early-return based on IsOurMenuActive(). The
        // previous version did, which was the actual root cause of the
        // "Begin has been called before calling End" crash: ProcessButtonInput
        // (above) can change state that flips IsOurMenuActive() to false for
        // the rest of this same frame -- but PreDrawLogo already left the
        // SpriteBatch open earlier in this frame regardless. An early return
        // here skipped ever closing it, so it stayed open across the frame
        // boundary and crashed the engine's own next Begin() call one frame
        // later. The try/finally below guarantees the batch is closed before
        // this method returns, no matter what.
        private static void RepaintAndDrawOwnButtons()
        {
            SpriteBatch spriteBatch = Main.spriteBatch;

            try
            {
                // orig() is never called while our menu is active, so nothing
                // else invokes PreDrawLogo this frame -- call it ourselves or
                // the backdrop/vignette/logo never draw at all.
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
                // Swallowed so a draw-time exception can't crash the game on
                // the title screen, but logged so it doesn't disappear silently.
                ModContent.GetInstance<ShatteredIllusionMenuHooks>()?.Mod?.Logger.Warn($"Menu draw failed: {ex}");
            }
            finally
            {
                // Always runs -- success, caught exception, or a menuMode
                // change mid-method from a button click. This is what
                // actually prevents the batch from ever leaking open past
                // this method, closing the loop the old early-return left open.
                SpriteBatchUtil.TryEnd(spriteBatch);
            }
        }

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
                // Entrance slide-in and exit slide-out share the same
                // formula and direction, so leaving looks like a mirror of
                // arriving rather than a different motion entirely.
                float slideOffset = ((1f - entranceEase) + exitEase) * SlideDistance * dir;

                Vector2 position = new(hitbox.X + slideOffset, hitbox.Y);

                // Idle bob: only once fully entered and not collapsing, so it
                // never fights the slide animations. Each button gets its own
                // phase so they don't all bob in lockstep.
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

        // Cheap stand-in for a blur: a handful of progressively larger, more
        // transparent rectangles stacked behind the label. Nowhere near a
        // real glow shader, but it reads as one at menu-button size and
        // costs nothing extra to set up.
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

        // -------------------------------
        // CUSTOM FOOTER
        // -------------------------------
        // Mod name/version plus the theme-switch line. Left-click advances
        // to the next installed theme, right-click goes back -- same as
        // tModLoader's own control.
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

        private static void DrawUnderline(SpriteBatch spriteBatch, Vector2 position, DynamicSpriteFont font, MenuButton button, float alpha, float scale)
        {
            Vector2 textSize = font.MeasureString(button.Label) * scale;

            // Deliberately NOT textSize.Y here -- that grows with whatever
            // descenders happen to be in this specific label ("Multiplayer"
            // vs "Settings"), which put the underline at a different depth
            // per button and, combined with the old flat row spacing, let it
            // reach into the next button's row. capHeight instead measures
            // where the visible glyphs actually end, so every button's
            // underline sits the same short distance below its own text.
            float capHeight = font.MeasureString(UnderlineReferenceGlyphs).Y * scale;

            Rectangle bar = new(
                (int)position.X,
                (int)(position.Y + capHeight + 2f),
                (int)textSize.X,
                2);

            spriteBatch.Draw(TextureAssets.MagicPixel.Value, bar, Main.OurFavoriteColor * alpha);
        }

        // -------------------------------
        // BUTTON ACTIONS
        // -------------------------------
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