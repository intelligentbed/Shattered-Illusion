using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace ShatteredIllusion.MainMenu
{
    /// <summary>
    /// Defensive SpriteBatch state helpers shared by our main-menu drawing code.
    ///
    /// BACKGROUND: tModLoader's ModMenu.PreDrawLogo receives an ALREADY-OPEN
    /// SpriteBatch and is expected to hand one back open too — confirmed by
    /// the official ExampleModMenu (tModLoader/ExampleMod/Content/ExampleModMenu.cs),
    /// which draws straight into the batch it's given without ever calling
    /// Begin/End itself.
    ///
    /// Our menu draws a custom fullscreen backdrop and buttons, which need
    /// different blend/sampler states than whatever vanilla started with, so
    /// we have to End()/Begin() to swap states. SpriteBatch throws if Begin()
    /// is called while already open, or if End() is called while NOT open —
    /// and it exposes no public way to ask "am I currently open?" Worse,
    /// game state can change mid-frame in ways that make an "is our menu
    /// still active" check unreliable for deciding whether cleanup is needed
    /// (e.g. clicking a button changes Main.menuMode partway through the
    /// same frame's drawing).
    ///
    /// Every state swap in our menu code should go through Restart/TryEnd
    /// below instead of raw Begin/End calls. They tolerate either starting
    /// state and guarantee a known, closed-or-reopened state on return, so
    /// an open batch can never leak past a frame and crash the engine's own
    /// next Begin() call.
    /// </summary>
    internal static class SpriteBatchUtil
    {
        /// <summary>
        /// Closes the batch (if open) and reopens it with the given state.
        /// Safe to call whether the batch is currently open or already closed.
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
        /// Closes the batch if it's currently open. Safe to call even when
        /// it's already closed — SpriteBatch's "End called without a
        /// matching Begin" exception is swallowed, since for our purposes
        /// "already closed" and "successfully closed" are the same outcome.
        /// This is what guarantees a batch never leaks open across a frame
        /// boundary no matter what happened upstream.
        /// </summary>
        public static void TryEnd(SpriteBatch spriteBatch)
        {
            try
            {
                spriteBatch.End();
            }
            catch (InvalidOperationException)
            {
                // Wasn't open. That's fine — nothing to clean up.
            }
        }
    }
}