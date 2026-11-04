using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ReLogic.Content;
using ReLogic.Graphics;
using ShatteredIllusion.Common.Visuals;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace ShatteredIllusion.Common.GUI.JournalUI
{
    /// <summary>The inventory-side journal button. Position is set by JournalUIState (unchanged).</summary>
    internal class Journal : UIElement
    {
        private const string ClosedTexturePath = "ShatteredIllusion/Assets/UI/Journal_Closed";
        private const string OpenTexturePath = "ShatteredIllusion/Assets/UI/Journal_Open";

        private Asset<Texture2D> _closedTexture;
        private Asset<Texture2D> _openTexture;

        private bool _isOpenVisual;
        private bool _isHovered;
        private float _hover;
        private float _openGlow;
        private float _newsPulse;
        private bool _hasNews;

        public event Action Clicked;

        public Journal()
        {
            Width.Set(52f, 0f);
            Height.Set(52f, 0f);
        }

        public override void OnInitialize()
        {
            _closedTexture = ModContent.Request<Texture2D>(ClosedTexturePath);
            _openTexture = ModContent.Request<Texture2D>(OpenTexturePath);
        }

        public override void LeftClick(UIMouseEvent evt)
        {
            base.LeftClick(evt);
            Clicked?.Invoke();
        }

        public override void MouseOver(UIMouseEvent evt)
        {
            base.MouseOver(evt);
            _isHovered = true;
            SoundEngine.PlaySound(SoundID.MenuTick);
        }

        public override void MouseOut(UIMouseEvent evt)
        {
            base.MouseOut(evt);
            _isHovered = false;
        }

        public void SetOpenVisual(bool isOpen)
        {
            _isOpenVisual = isOpen;
        }

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            JournalGfx.EnsureBuilt();

            _hover = MathHelper.Lerp(_hover, _isHovered ? 1f : 0f, 0.2f);
            _openGlow = MathHelper.Lerp(_openGlow, _isOpenVisual ? 1f : 0f, 0.15f);

            Texture2D texture = (_isOpenVisual ? _openTexture : _closedTexture).Value;
            CalculatedStyle dimensions = GetDimensions();
            float time = Main.GlobalTimeWrappedHourly;

            // Keep the top-left anchored exactly where it always was; scale/rotate around the sprite's center.
            Vector2 origin = texture.Size() / 2f;
            Vector2 position = dimensions.Position() + origin;
            position.Y += (float)Math.Sin(time * 2f) * 1.5f * (1f - _hover); // gentle idle bob

            // Warm halo that breathes on hover and stays lit while the book is open.
            float glow = Math.Max(_hover * 0.55f, _openGlow * 0.4f);
            if (glow > 0.01f)
            {
                float breathe = 0.8f + 0.2f * (float)Math.Sin(time * 3f);
                int size = (int)(texture.Width * 2.4f);
                spriteBatch.Draw(
                    JournalGfx.Soft,
                    new Rectangle((int)position.X - size / 2, (int)position.Y - size / 2, size, size),
                    new Color(255, 176, 96) * (glow * breathe));
            }

            // Gold beacon while something has unlocked that the player hasn't watched open yet.
            _hasNews = !_isOpenVisual && Main.LocalPlayer.GetModPlayer<JournalPlayer>().HasNews;
            _newsPulse = MathHelper.Lerp(_newsPulse, _hasNews ? 1f : 0f, 0.1f);
            if (_newsPulse > 0.01f)
            {
                float beat = 0.5f + 0.5f * (float)Math.Sin(time * 4f);
                int halo = (int)(texture.Width * (2.0f + 0.5f * beat));
                spriteBatch.Draw(JournalGfx.Soft,
                    new Rectangle((int)position.X - halo / 2, (int)position.Y - halo / 2, halo, halo),
                    new Color(255, 205, 110) * (_newsPulse * (0.35f + 0.3f * beat)));

                float ringSize = texture.Width * (1.1f + 0.9f * ((time * 0.8f) % 1f));
                spriteBatch.Draw(JournalGfx.Ring, position, null,
                    new Color(255, 215, 130) * (_newsPulse * (1f - (time * 0.8f) % 1f) * 0.8f),
                    0f, JournalGfx.Ring.Size() / 2f, ringSize / JournalGfx.Ring.Width, SpriteEffects.None, 0f);
            }

            float scale = 1f + 0.1f * _hover;
            float rotation = (float)Math.Sin(time * 9f) * 0.04f * _hover;
            Color drawColor = Color.White * (0.85f + 0.15f * _hover);

            spriteBatch.Draw(texture, position, null, drawColor, rotation, origin, scale, SpriteEffects.None, 0f);

            if (_newsPulse > 0.01f)
            {
                // Small bright pip on the corner.
                Vector2 pip = position + new Vector2(texture.Width * 0.34f, -texture.Height * 0.34f);
                int pipSize = (int)(14f * (0.9f + 0.2f * (float)Math.Sin(time * 6f)));
                spriteBatch.Draw(JournalGfx.Soft, new Rectangle((int)pip.X - pipSize, (int)pip.Y - pipSize, pipSize * 2, pipSize * 2), new Color(255, 232, 160) * _newsPulse);
                spriteBatch.Draw(JournalGfx.Soft, new Rectangle((int)pip.X - 4, (int)pip.Y - 4, 8, 8), Color.White * _newsPulse);
            }

            if (_isHovered)
            {
                Main.hoverItemName = _hasNews ? JournalText.Ui("JournalNews") : JournalText.Ui("Journal");
                Main.LocalPlayer.mouseInterface = true;
            }
        }
    }
}
