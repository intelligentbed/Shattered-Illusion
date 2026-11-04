using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using ShatteredIllusion.Common.Visuals;
using ShatteredIllusion.Common.Systems;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;
using ReLogic.Content;
using ReLogic.Graphics;

namespace ShatteredIllusion.Common.GUI.JournalUI
{
    /// <summary>
    /// Full-screen cinematic backdrop: dimmer, vignette, letterbox bars and drifting dust motes.
    /// Vignette, bars and motes follow the player's cinematic config toggles; the dimmer always shows.
    /// </summary>
    internal class CinematicOverlay : UIElement
    {
        private const float MaxDarkenAlpha = 0.55f;
        private const int MoteCount = 96;           // pool size; how many are active depends on screen area
        private const int MotesPer1080p = 48;

        private struct Mote
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public float Size;
            public float Phase;
            public float Brightness;
        }

        private readonly Mote[] _motes = new Mote[MoteCount];
        private bool _motesSeeded;
        private float _eased;
        private int _activeMotes = MotesPer1080p;
        private float _sizeScale = 1f;

        public event Action Clicked;

        public CinematicOverlay()
        {
            Width.Set(0f, 1f);
            Height.Set(0f, 1f);
        }

        public void SetProgress(float rawProgress)
        {
            _eased = JournalGfx.EaseOutCubic(MathHelper.Clamp(rawProgress, 0f, 1f));
        }

        // Only intercept clicks once the backdrop is actually visible. It covers the whole screen,
        // so this doesn't depend on the element's cached size.
        public override bool ContainsPoint(Vector2 point)
        {
            return _eased > 0.5f;
        }

        private static CalculatedStyle ScreenDims()
        {
            return new CalculatedStyle(0f, 0f, Main.screenWidth / Main.UIScale, Main.screenHeight / Main.UIScale);
        }

        // Screen size in UI units, read straight from Main so it can never be stale.
        // Padded a couple of pixels so rounding can't leave an uncovered strip at the right or bottom.
        private static Rectangle ScreenRect()
        {
            float w = Main.screenWidth / Main.UIScale;
            float h = Main.screenHeight / Main.UIScale;
            return new Rectangle(0, 0, (int)Math.Ceiling(w) + 2, (int)Math.Ceiling(h) + 2);
        }

        public override void LeftClick(UIMouseEvent evt)
        {
            base.LeftClick(evt);
            Clicked?.Invoke();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (_eased <= 0f)
            {
                _motesSeeded = false;
                return;
            }

            // Dust is part of the cinematic dressing; skip the work entirely if the player turned that off.
            if (!Cinema.Enabled)
            {
                _motesSeeded = false;
                return;
            }

            CalculatedStyle dims = ScreenDims();

            // Keep dust density and size consistent across resolutions and aspect ratios.
            float areaRatio = dims.Width * dims.Height / (1920f * 1080f);
            _activeMotes = Math.Clamp((int)(MotesPer1080p * areaRatio), 24, MoteCount);
            _sizeScale = MathHelper.Clamp(dims.Height / 1080f, 0.7f, 1.6f);

            if (!_motesSeeded)
            {
                for (int i = 0; i < _motes.Length; i++)
                {
                    SeedMote(ref _motes[i], dims, true);
                }
                _motesSeeded = true;
            }

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            for (int i = 0; i < _activeMotes; i++)
            {
                _motes[i].Position += _motes[i].Velocity * dt;
                _motes[i].Phase += dt;

                if (_motes[i].Position.Y < dims.Y - 20f ||
                    _motes[i].Position.X < dims.X - 40f ||
                    _motes[i].Position.X > dims.X + dims.Width + 40f)
                {
                    SeedMote(ref _motes[i], dims, false);
                }
            }
        }

        private static void SeedMote(ref Mote mote, CalculatedStyle dims, bool anywhere)
        {
            mote.Position = new Vector2(
                dims.X + Main.rand.NextFloat() * dims.Width,
                anywhere ? dims.Y + Main.rand.NextFloat() * dims.Height : dims.Y + dims.Height + 10f);
            mote.Velocity = new Vector2(Main.rand.NextFloat(-6f, 6f), -Main.rand.NextFloat(6f, 22f));
            mote.Size = Main.rand.NextFloat(5f, 16f);
            mote.Phase = Main.rand.NextFloat() * MathHelper.TwoPi;
            mote.Brightness = Main.rand.NextFloat(0.25f, 0.8f);
        }

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            if (_eased <= 0.001f)
            {
                return;
            }

            JournalGfx.EnsureBuilt();

            CalculatedStyle dims = ScreenDims();
            Rectangle full = ScreenRect();
            Texture2D pixel = TextureAssets.MagicPixel.Value;

            // Dim the world, then push the edges darker.
            spriteBatch.Draw(pixel, full, Color.Black * (_eased * MaxDarkenAlpha));
            if (Cinema.VignetteAllowed)
            {
                spriteBatch.Draw(JournalGfx.Vignette, full, Color.White * (_eased * 0.9f));
            }

            // Dust motes catching the lamp light (only seeded/updated while cinematics are enabled).
            int moteCount = _motesSeeded && Cinema.Enabled ? _activeMotes : 0;
            for (int i = 0; i < moteCount; i++)
            {
                Mote mote = _motes[i];
                float twinkle = 0.55f + 0.45f * (float)Math.Sin(mote.Phase * 1.6f);
                Color color = new Color(255, 214, 160) * (mote.Brightness * twinkle * 0.45f * _eased);
                int size = Math.Max(2, (int)(mote.Size * _sizeScale));
                spriteBatch.Draw(
                    JournalGfx.Soft,
                    new Rectangle((int)mote.Position.X - size / 2, (int)mote.Position.Y - size / 2, size, size),
                    color);
            }

            // Letterbox bars slide in from the top and bottom. They aim for a cinematic aspect ratio, but
            // stay thin on screens that are already ultrawide and never eat too much of a tall/narrow one.
            int barHeight = (int)(JournalBackdrop.TargetBarHeight(dims.Width, dims.Height) * _eased);
            if (barHeight > 0)
            {
                spriteBatch.Draw(pixel, new Rectangle(full.X, full.Y, full.Width, barHeight), Color.Black);
                spriteBatch.Draw(pixel, new Rectangle(full.X, full.Bottom - barHeight, full.Width, barHeight), Color.Black);
            }
        }
    }
}