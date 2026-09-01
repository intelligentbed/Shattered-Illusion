using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ShatteredIllusion.MainMenu
{
    public class ShatteredIllusionMainMenu : ModMenu
    {
        public override string DisplayName => "Shattered Illusion";

        public override Asset<Texture2D> Logo => ModContent.Request<Texture2D>("ShatteredIllusion/MainMenu/Logo");
        public override Asset<Texture2D> SunTexture => ModContent.Request<Texture2D>("ShatteredIllusion/MainMenu/BlankPixel");
        public override Asset<Texture2D> MoonTexture => ModContent.Request<Texture2D>("ShatteredIllusion/MainMenu/BlankPixel");

        public override int Music => MusicID.Title;

        public override bool PreDrawLogo(SpriteBatch spriteBatch, ref Vector2 logoDrawCenter, ref float logoRotation, ref float logoScale, ref Color drawColor)
        {
            DrawBackdrop(spriteBatch);

            FreezeTimeOfDay();
            drawColor = Color.White;

            DrawLogoIsolated(spriteBatch, drawColor);

            return false;
        }

        // Draws the background art scaling it so it always covers the screen regardless of aspect ratio.
        private static void DrawBackdrop(SpriteBatch spriteBatch)
        {
            Texture2D backdrop = ModContent.Request<Texture2D>("ShatteredIllusion/MainMenu/Background").Value;
            Vector2 offset = GetCoverOffsetAndScale(backdrop, out float coverScale);

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, Main.Rasterizer, null, Main.UIScaleMatrix);
            spriteBatch.Draw(backdrop, offset, null, Color.White, 0f, Vector2.Zero, coverScale, SpriteEffects.None, 0f);
        }

        /// <summary>
        /// Works out the scale needed for a texture to fully cover the current screen
        /// dimensions, plus the offset required to keep it centered once scaled.
        /// </summary>
        private static Vector2 GetCoverOffsetAndScale(Texture2D texture, out float coverScale)
        {
            float widthRatio = (float)Main.screenWidth / texture.Width;
            float heightRatio = (float)Main.screenHeight / texture.Height;

            coverScale = Math.Max(widthRatio, heightRatio);

            Vector2 offset = Vector2.Zero;
            if (widthRatio > heightRatio)
                offset.Y -= (texture.Height * coverScale - Main.screenHeight) * 0.5f;
            else if (heightRatio > widthRatio)
                offset.X -= (texture.Width * coverScale - Main.screenWidth) * 0.5f;

            return offset;
        }

        private static void FreezeTimeOfDay()
        {
            Main.time = 27000;
            Main.dayTime = true;
        }

        /// <summary>
        /// Draws the logo in its own spritebatch pass so its blend mode doesn't bleed
        /// into the background pass above it.
        /// </summary>
        private void DrawLogoIsolated(SpriteBatch spriteBatch, Color drawColor)
        {
            Vector2 logoPosition = new Vector2(Main.screenWidth / 2f, 100f);

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointClamp, DepthStencilState.None, Main.Rasterizer, null, Main.UIScaleMatrix);
            spriteBatch.Draw(Logo.Value, logoPosition, null, drawColor, 0f, Logo.Value.Size() * 0.5f, 1f, SpriteEffects.None, 0f);
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, Main.Rasterizer, null, Main.UIScaleMatrix);
        }
    }
}