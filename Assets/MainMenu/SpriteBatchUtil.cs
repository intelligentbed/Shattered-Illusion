using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace ShatteredIllusion.Assets.MainMenu
{
    /// <summary>
    /// Provides defensive SpriteBatch state helpers and screen-size helpers for menu drawing.
    /// </summary>
    internal static class SpriteBatchUtil
    {
        // The menu layout is designed for this many UI units and scaled from there.
        private const float ReferenceWidth = 1600f;
        private const float ReferenceHeight = 900f;
        private const float MinLayoutScale = 0.65f;
        private const float MaxLayoutScale = 1.75f;

        /// <summary>
        /// Width of the screen in UI units. While the menu draws, <see cref="Main.screenWidth"/>
        /// is already divided by the UI scale, so it matches what is drawn under
        /// <see cref="Main.UIScaleMatrix"/>.
        /// </summary>
        public static float UiWidth => Main.screenWidth;

        /// <summary>
        /// Height of the screen in UI units.
        /// </summary>
        public static float UiHeight => Main.screenHeight;

        /// <summary>
        /// Mouse position in UI units, matching coordinates drawn under <see cref="Main.UIScaleMatrix"/>.
        /// </summary>
        public static Vector2 UiMouse => Main.MouseScreen;

        /// <summary>
        /// The real back buffer size in pixels. Use this with <see cref="Matrix.Identity"/>
        /// for anything that must cover the whole window.
        /// </summary>
        public static Rectangle ScreenBounds
        {
            get
            {
                Viewport viewport = Main.instance.GraphicsDevice.Viewport;
                return new Rectangle(0, 0, viewport.Width, viewport.Height);
            }
        }

        /// <summary>
        /// How much to scale menu sizes and spacing so the layout fits the current screen.
        /// It is 1 at 1600x900 UI units and follows the smaller of the width and height ratios,
        /// so small windows, tall windows and big monitors all keep the same proportions.
        /// </summary>
        public static float LayoutScale =>
            MathHelper.Clamp(
                MathF.Min(UiWidth / ReferenceWidth, UiHeight / ReferenceHeight),
                MinLayoutScale,
                MaxLayoutScale);

        /// <summary>
        /// Closes the batch if open and reopens it in UI space.
        /// </summary>
        public static void Restart(SpriteBatch spriteBatch, BlendState blendState, SamplerState samplerState)
        {
            Restart(spriteBatch, blendState, samplerState, Main.UIScaleMatrix);
        }

        /// <summary>
        /// Closes the batch if open and reopens it with the given transform.
        /// Pass <see cref="Matrix.Identity"/> for raw screen-space drawing.
        /// </summary>
        public static void Restart(SpriteBatch spriteBatch, BlendState blendState, SamplerState samplerState, Matrix transform)
        {
            TryEnd(spriteBatch);

            spriteBatch.Begin(
                SpriteSortMode.Deferred,
                blendState,
                samplerState,
                DepthStencilState.None,
                Main.Rasterizer,
                null,
                transform);
        }

        /// <summary>
        /// Safely closes the batch if currently open.
        /// </summary>
        public static void TryEnd(SpriteBatch spriteBatch)
        {
            try
            {
                spriteBatch.End();
            }
            catch (InvalidOperationException)
            {
            }
        }
    }
}