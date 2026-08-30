using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ReLogic.Content;
using ShatteredIllusion.Common.Players.ParrySystem;
using Terraria;
using Terraria.ModLoader;

namespace ShatteredIllusion.GUI.ResolveBar
{
    public static class SturdinessBarUI
    {
        //i need to get more opinions on a good default location but i put random numbers for now
        public const float DefaultPosX = 35f;
        public const float DefaultPosY = 15f;

        private const float MouseDragEpsilon = 0.05f;

        private const float FullPulseSpeed = 6f;
        private static readonly Color FullPulseColor = new Color(255, 220, 80); // gold

        // Background and fill (x/y)
        private static readonly Vector2 BackgroundSizeMultiplier = new Vector2(1.5f, 1.5f);
        private static readonly Vector2 BarSizeMultiplier = new Vector2(1.3f, 1.4f);

        private const float BorderSizeMultiplier = 1.3f;
        private const float BorderExtraScale = 1f;

        private static Vector2? dragOffset = null;

        private static Texture2D backgroundTex;
        private static Texture2D barTex;
        private static Texture2D borderTex;

        public static void LoadTextures()
        {
            backgroundTex = ModContent.Request<Texture2D>("ShatteredIllusion/GUI/SturdinessBar/SturdinessBarBackground", AssetRequestMode.ImmediateLoad).Value;
            barTex = ModContent.Request<Texture2D>("ShatteredIllusion/GUI/SturdinessBar/SturdinessBarFill", AssetRequestMode.ImmediateLoad).Value;
            borderTex = ModContent.Request<Texture2D>("ShatteredIllusion/GUI/SturdinessBar/SturdinessBarBorder", AssetRequestMode.ImmediateLoad).Value;
        }

        public static void UnloadTextures()
        {
            backgroundTex = null;
            barTex = null;
            borderTex = null;
            dragOffset = null;
        }

        internal static void Reset()
        {
            dragOffset = null;
        }

        public static void Draw(SpriteBatch spriteBatch, Player player)
        {
            if (backgroundTex == null || barTex == null || borderTex == null)
                return;

            var config = ModContent.GetInstance<ShatteredIllusionConfig>();

            //if it cant find it than itll go back to default (skin like fortnite
            Vector2 screenRatioPos = new Vector2(config.SturdinessBarPosX, config.SturdinessBarPosY);
            if (screenRatioPos.X < 0f || screenRatioPos.X > 100f)
                screenRatioPos.X = DefaultPosX;
            if (screenRatioPos.Y < 0f || screenRatioPos.Y > 100f)
                screenRatioPos.Y = DefaultPosY;

            Vector2 screenPos = screenRatioPos;
            screenPos.X = (int)(screenPos.X * 0.01f * Main.screenWidth);
            screenPos.Y = (int)(screenPos.Y * 0.01f * Main.screenHeight);

            var modPlayer = player.GetModPlayer<ParryPlayer>();

            float uiScale = Main.UIScale * BorderSizeMultiplier;
            Vector2 backgroundScale = Main.UIScale * BackgroundSizeMultiplier;
            Vector2 barScale = Main.UIScale * BarSizeMultiplier;

            DrawBar(spriteBatch, modPlayer, screenPos, backgroundScale, barScale, uiScale);
            HandleMouseInteraction(modPlayer, config, screenPos, screenRatioPos, uiScale);
        }

        private static void DrawBar(SpriteBatch spriteBatch, ParryPlayer modPlayer, Vector2 screenPos, Vector2 backgroundScale, Vector2 barScale, float uiScale)
        {

            Vector2 backgroundOrigin = backgroundTex.Size() * 0.5f;
            Vector2 barOrigin = barTex.Size() * 0.5f;
            Vector2 borderOrigin = borderTex.Size() * 0.5f;

            spriteBatch.Draw(backgroundTex, screenPos, null, Color.White, 0f, backgroundOrigin, backgroundScale, SpriteEffects.None, 0);

            float percent = ParryPlayer.MaxSturdinessMeter <= 0
                ? 0f
                : MathHelper.Clamp(modPlayer.DisplaySturdinessMeter / ParryPlayer.MaxSturdinessMeter, 0f, 1f);

            Rectangle cropRect = new Rectangle(0, 0, (int)(barTex.Width * percent), barTex.Height);

            // While full pulse the fill's color between its normal tint and a bright gold highlight instead of just sitting there like a chud takes 1 to know 1 
            Color barColor = Color.White;
            if (percent >= 1f)
            {
                float pulse = (float)(0.5 + 0.5 * Math.Sin(Main.GlobalTimeWrappedHourly * FullPulseSpeed));
                barColor = Color.Lerp(Color.White, FullPulseColor, pulse);
            }

            spriteBatch.Draw(
                barTex,
                screenPos,
                cropRect,
                barColor,
                0f,
                barOrigin,
                barScale,
                SpriteEffects.None,
                0);

            spriteBatch.Draw(borderTex, screenPos, null, Color.White, 0f, borderOrigin, uiScale * BorderExtraScale, SpriteEffects.None, 0);
        }

        private static void HandleMouseInteraction(
            ParryPlayer modPlayer,
            ShatteredIllusionConfig config,
            Vector2 screenPos,
            Vector2 screenRatioPos,
            float uiScale)
        {
            Rectangle mouseHitbox = new Rectangle((int)Main.MouseScreen.X, (int)Main.MouseScreen.Y, 8, 8);
            Rectangle barRect = Utils.CenteredRectangle(screenPos, borderTex.Size() * uiScale);

            bool hovering = mouseHitbox.Intersects(barRect);
            if (!hovering)
                return;

            if (!config.SturdinessBarLocked)
                Main.LocalPlayer.mouseInterface = true;

            Main.instance.MouseText($"Sturdiness: {(int)Math.Round(modPlayer.DisplaySturdinessMeter)}/{ParryPlayer.MaxSturdinessMeter}");

            MouseState ms = Mouse.GetState();
            Vector2 mousePos = Main.MouseScreen;

            Vector2 newScreenRatioPos = screenRatioPos;
            if (!config.SturdinessBarLocked && ms.LeftButton == ButtonState.Pressed)
            {
                if (!dragOffset.HasValue)
                    dragOffset = mousePos - screenPos;

                Vector2 newCorner = mousePos - dragOffset.GetValueOrDefault(Vector2.Zero);

                newScreenRatioPos.X = (100f * newCorner.X) / Main.screenWidth;
                newScreenRatioPos.Y = (100f * newCorner.Y) / Main.screenHeight;
            }

            Vector2 delta = newScreenRatioPos - screenRatioPos;
            if (Math.Abs(delta.X) >= MouseDragEpsilon || Math.Abs(delta.Y) >= MouseDragEpsilon)
            {
                config.SturdinessBarPosX = newScreenRatioPos.X;
                config.SturdinessBarPosY = newScreenRatioPos.Y;
            }

            if (dragOffset.HasValue && ms.LeftButton == ButtonState.Released)
            {
                dragOffset = null;
                config.SaveChanges();
            }
        }
    }
}