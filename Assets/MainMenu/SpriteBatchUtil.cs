using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace ShatteredIllusion.Assets.MainMenu
{
    /// <summary>
    /// Provides defensive SpriteBatch state helpers for menu drawing.
    /// </summary>
    internal static class SpriteBatchUtil
    {
        /// <summary>
        /// Closes the batch if open and reopens it with the given state.
        /// </summary>
        public static void Restart(SpriteBatch spriteBatch, BlendState blendState, SamplerState samplerState)
        {
            TryEnd(spriteBatch);

        spriteBatch.Begin(
            SpriteSortMode.Deferred,
            blendState,
            samplerState,
            DepthStencilState.None,
            Main.Rasterizer,
            null,
            Main.UIScaleMatrix);
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
