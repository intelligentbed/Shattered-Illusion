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
    internal class JournalUIState : UIState
    {
        private const float OpenSeconds = 0.6f;
        private const float CloseSeconds = 0.4f;

        private static readonly SoundStyle PullSound = new SoundStyle("ShatteredIllusion/Sounds/Journal/JournalPullUp");

        public Journal JournalButton;
        public JournalPanel JournalPanel;
        private CinematicOverlay _overlay;

        private bool _isOpen;
        private bool _openTarget;
        private float _progress;

        // Used to re-layout the UI when the window size or UI scale changes.
        private int _lastScreenWidth = -1;
        private int _lastScreenHeight = -1;
        private float _lastUIScale = -1f;

        public bool IsOpen => _isOpen;

        public override void OnInitialize()
        {
            _overlay = new CinematicOverlay();
            // Clicking the dimmed backdrop closes the book.
            _overlay.Clicked += () =>
            {
                if (_progress >= 0.9f)
                {
                    Close();
                }
            };
            Append(_overlay);

            // The panel stays centered; the slide / tilt / zoom animation is applied when it draws.
            JournalPanel = new JournalPanel();
            JournalPanel.HAlign = 0.5f;
            JournalPanel.VAlign = 0.5f;
            JournalPanel.Left.Set(0f, 0f);
            JournalPanel.Top.Set(0f, 0f);
            Append(JournalPanel);

            // Button position is intentionally unchanged.
            JournalButton = new Journal();
            JournalButton.Clicked += ToggleOpen;
            JournalButton.Left.Set(20f, 0f);
            JournalButton.Top.Set(280f, 0f);
            Append(JournalButton);
        }

        public void ToggleOpen()
        {
            if (_isOpen && _openTarget)
            {
                Close();
            }
            else
            {
                Open();
            }
        }

        public void Open()
        {
            if (_isOpen && _openTarget)
            {
                return;
            }

            if (!_isOpen)
            {
                JournalPanel.SetOpen(true);
            }

            _isOpen = true;
            _openTarget = true;
            JournalButton.SetOpenVisual(true);
            SoundEngine.PlaySound(PullSound);
        }

        public void Close()
        {
            if (!_isOpen || !_openTarget)
            {
                return;
            }

            _openTarget = false;
            JournalButton.SetOpenVisual(false);
            SoundEngine.PlaySound(PullSound with { Pitch = -0.25f });
        }

        public override void Update(GameTime gameTime)
        {
            // The state caches its size; without this the panel can sit off-center after a resolution change.
            if (Main.screenWidth != _lastScreenWidth || Main.screenHeight != _lastScreenHeight || Main.UIScale != _lastUIScale)
            {
                _lastScreenWidth = Main.screenWidth;
                _lastScreenHeight = Main.screenHeight;
                _lastUIScale = Main.UIScale;
                Recalculate();
            }

            float delta = (float)gameTime.ElapsedGameTime.TotalSeconds;

            if (_openTarget)
            {
                _progress = Math.Min(1f, _progress + delta / OpenSeconds);
            }
            else
            {
                _progress = Math.Max(0f, _progress - delta / CloseSeconds);
                if (_progress <= 0f && _isOpen)
                {
                    _isOpen = false;
                    JournalPanel.SetOpen(false);
                }
            }

            JournalPanel.SetProgress(_progress);
            _overlay.SetProgress(_progress);

            base.Update(gameTime);
        }
    }
}