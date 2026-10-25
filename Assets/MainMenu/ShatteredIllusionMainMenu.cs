using System;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace ShatteredIllusion.Assets.MainMenu
{
    public class ShatteredIllusionMainMenu : ModMenu
    {
        public override string DisplayName => "Shattered Illusion";

        private static Asset<Texture2D>? logoAsset;
        private static Asset<Texture2D>? blankPixelAsset;
        private static Asset<Texture2D>? backgroundAsset;

        public override Asset<Texture2D> Logo =>
            logoAsset ??= ModContent.Request<Texture2D>("ShatteredIllusion/Assets/MainMenu/Logo");

        public override Asset<Texture2D> SunTexture =>
            blankPixelAsset ??= ModContent.Request<Texture2D>("ShatteredIllusion/Assets/MainMenu/BlankPixel");

        public override Asset<Texture2D> MoonTexture =>
            blankPixelAsset ??= ModContent.Request<Texture2D>("ShatteredIllusion/Assets/MainMenu/BlankPixel");

        public override int Music => MusicID.Title;

        private const float IntroDuration = 1.6f;
        private const float IntroLogoScaleStart = 0.85f;
        private const float IntroLogoScaleEnd = 1f;
        private const float FrameStep = 1f / 60f;

        private const float MaxIntroDeltaTime = 0.1f;

        private static bool _introPlayed;
        private static float _introTimer;
        private static long _lastIntroTimestamp;

        private const float LogoBaseScale = 0.85f;
        private const float LogoPaddingX = 80f;
        private const float LogoPaddingY = 60f;

        private const float VignetteAlpha = 0.18f;

        // Freeze time at midday for a static background.
        private const double FrozenTimeOfDay = 27000;

        private static readonly BlendState ReturnBlendState = BlendState.AlphaBlend;
        private static readonly SamplerState ReturnSamplerState = SamplerState.LinearClamp;

        public override bool PreDrawLogo(
            SpriteBatch spriteBatch,
            ref Vector2 logoDrawCenter,
            ref float logoRotation,
            ref float logoScale,
            ref Color drawColor)
        {
            try
            {
                AdvanceIntroTimer();

                DrawBackdrop(spriteBatch);
                DrawVignette(spriteBatch);
                FreezeTimeOfDay();

                float introEase = EaseOutCubic(_introTimer / IntroDuration);

                drawColor = Color.White * introEase;

                DrawLogoIsolated(spriteBatch, drawColor, introEase);
            }
            catch (Exception ex)
            {
                Mod.Logger.Warn($"Menu backdrop/logo draw failed: {ex}");

                SpriteBatchUtil.Restart(spriteBatch, ReturnBlendState, ReturnSamplerState);
            }

            return false;
        }

        // Advances the intro by real elapsed time so it lasts the same at any frame rate.
        private static void AdvanceIntroTimer()
        {
            if (_introPlayed)
            {
                _introTimer = IntroDuration;
                return;
            }

            long now = Stopwatch.GetTimestamp();
            float deltaTime = _lastIntroTimestamp == 0
                ? FrameStep
                : (float)(now - _lastIntroTimestamp) / Stopwatch.Frequency;
            _lastIntroTimestamp = now;

            _introTimer += MathHelper.Clamp(deltaTime, 0f, MaxIntroDeltaTime);

            if (_introTimer >= IntroDuration)
            {
                _introTimer = IntroDuration;
                _introPlayed = true;
            }
        }

        private static float EaseOutCubic(float t)
        {
            t = MathHelper.Clamp(t, 0f, 1f);

            float inv = 1f - t;

            return 1f - inv * inv * inv;
        }

        // The backdrop and vignette are drawn in raw pixels using the real back buffer size, so
        // they cover the whole window at any resolution, aspect ratio or UI scale. The logo
        // and buttons use UI space.
        private static void DrawBackdrop(SpriteBatch spriteBatch)
        {
            Texture2D background = (backgroundAsset ??= ModContent.Request<Texture2D>("ShatteredIllusion/Assets/MainMenu/Background")).Value;

            if (background.Width <= 0 || background.Height <= 0)
                return;

            Rectangle screen = SpriteBatchUtil.ScreenBounds;

            // Scale to cover, then crop the overflow evenly. Sizes round up so no edge pixel is left bare.
            float scale = MathHelper.Max(
                screen.Width / (float)background.Width,
                screen.Height / (float)background.Height);

            int width = (int)MathF.Ceiling(background.Width * scale);
            int height = (int)MathF.Ceiling(background.Height * scale);

            Rectangle destination = new(
                (screen.Width - width) / 2,
                (screen.Height - height) / 2,
                width,
                height);

            SpriteBatchUtil.Restart(spriteBatch, BlendState.AlphaBlend, SamplerState.PointClamp, Matrix.Identity);

            spriteBatch.Draw(background, destination, Color.White);
        }

        private static void DrawVignette(SpriteBatch spriteBatch)
        {
            spriteBatch.Draw(
                TextureAssets.MagicPixel.Value,
                SpriteBatchUtil.ScreenBounds,
                Color.Black * VignetteAlpha);
        }

        private static void FreezeTimeOfDay()
        {
            Main.time = FrozenTimeOfDay;
            Main.dayTime = true;
        }

        private void DrawLogoIsolated(SpriteBatch spriteBatch, Color drawColor, float introEase)
        {
            ShatteredIllusionConfig config = ModContent.GetInstance<ShatteredIllusionConfig>();

            bool buttonsOnLeft = config.MenuButtonPosition == HorizontalPosition.Left;

            Texture2D logoTex = Logo.Value;

            float layoutScale = SpriteBatchUtil.LayoutScale;

            float logoScale = LogoBaseScale * layoutScale * MathHelper.Lerp(IntroLogoScaleStart, IntroLogoScaleEnd, introEase);

            Vector2 logoSize = new Vector2(logoTex.Width, logoTex.Height) * logoScale;

            float x = buttonsOnLeft
                ? SpriteBatchUtil.UiWidth - LogoPaddingX * layoutScale - logoSize.X / 2f
                : LogoPaddingX * layoutScale + logoSize.X / 2f;

            float y = LogoPaddingY * layoutScale + logoSize.Y / 2f;

            Vector2 logoPosition = new(x, y);

            SpriteBatchUtil.Restart(spriteBatch, BlendState.NonPremultiplied, SamplerState.PointClamp);

            spriteBatch.Draw(
                logoTex,
                logoPosition,
                null,
                drawColor,
                0f,
                logoTex.Size() * 0.5f,
                logoScale,
                SpriteEffects.None,
                0f);

            SpriteBatchUtil.Restart(spriteBatch, ReturnBlendState, ReturnSamplerState);
        }
    }
}