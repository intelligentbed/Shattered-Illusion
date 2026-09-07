using System;
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

        public override Asset<Texture2D> Logo =>
            ModContent.Request<Texture2D>("ShatteredIllusion/Assets/MainMenu/Logo");

        public override Asset<Texture2D> SunTexture =>
            ModContent.Request<Texture2D>("ShatteredIllusion/Assets/MainMenu/BlankPixel");

        public override Asset<Texture2D> MoonTexture =>
            ModContent.Request<Texture2D>("ShatteredIllusion/Assets/MainMenu/BlankPixel");

        public override int Music => MusicID.Title;

        private const float IntroDuration = 1.6f;
        private const float IntroLogoScaleStart = 0.85f;
        private const float IntroLogoScaleEnd = 1f;
        private const float FrameStep = 1f / 60f;

        private static bool _introPlayed;
        private static float _introTimer;

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
                ModContent.GetInstance<ShatteredIllusionMenuHooks>()?.Mod?.Logger.Warn($"Menu backdrop/logo draw failed: {ex}");

                SpriteBatchUtil.Restart(spriteBatch, ReturnBlendState, ReturnSamplerState);
            }

            return false;
        }

        private static void AdvanceIntroTimer()
        {
            if (_introPlayed)
            {
                _introTimer = IntroDuration;
                return;
            }

            _introTimer += FrameStep;

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

        private static void DrawBackdrop(SpriteBatch spriteBatch)
        {
            Texture2D background = ModContent.Request<Texture2D>("ShatteredIllusion/Assets/MainMenu/Background").Value;

            float widthRatio = (float)Main.screenWidth / background.Width;
            float heightRatio = (float)Main.screenHeight / background.Height;
            float scale = MathHelper.Max(widthRatio, heightRatio);

            Vector2 size = new(background.Width * scale, background.Height * scale);
            Vector2 position = new((Main.screenWidth - size.X) * 0.5f, (Main.screenHeight - size.Y) * 0.5f);

            SpriteBatchUtil.Restart(spriteBatch, BlendState.AlphaBlend, SamplerState.PointClamp);

            spriteBatch.Draw(
                background,
                position,
                null,
                Color.White,
                0f,
                Vector2.Zero,
                scale,
                SpriteEffects.None,
                0f);
        }

        private static void DrawVignette(SpriteBatch spriteBatch)
        {
            spriteBatch.Draw(
                TextureAssets.MagicPixel.Value,
                new Rectangle(0, 0, Main.screenWidth, Main.screenHeight),
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

            float logoScale = LogoBaseScale * MathHelper.Lerp(IntroLogoScaleStart, IntroLogoScaleEnd, introEase);

            Vector2 logoSize = new Vector2(logoTex.Width, logoTex.Height) * logoScale;

            float x = buttonsOnLeft
                ? Main.screenWidth - LogoPaddingX - logoSize.X / 2f
                : LogoPaddingX + logoSize.X / 2f;

            float y = LogoPaddingY + logoSize.Y / 2f;

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