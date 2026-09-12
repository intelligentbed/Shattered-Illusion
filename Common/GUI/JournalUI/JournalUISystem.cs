using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using ShatteredIllusionKeybinds;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace ShatteredIllusion.Common.GUI.JournalUI
{
    public class JournalUISystem : ModSystem
    {
        internal JournalUIState JournalState;
        private UserInterface _journalInterface;

        public override void Load()
        {
            if (!Main.dedServ)
            {
                JournalState = new JournalUIState();
                JournalState.Activate();
                _journalInterface = new UserInterface();
                _journalInterface.SetState(JournalState);
            }
        }

        public override void UpdateUI(GameTime gameTime)
        {
            if (Main.dedServ || JournalState == null)
            {
                return;
            }

            HandleToggleInput();

            if (Main.playerInventory || JournalState.IsOpen)
            {
                _journalInterface?.Update(gameTime);
            }
        }

        private void HandleToggleInput()
        {
            if (JournalState.IsOpen && JustPressedEscape())
            {
                JournalState.Close();
                return;
            }

            if (KeybindSystem.JournalKeybind.JustPressed)
            {
                JournalState.ToggleOpen();
            }
        }

        private static bool JustPressedEscape()
        {
            return Main.keyState.IsKeyDown(Keys.Escape) && !Main.oldKeyState.IsKeyDown(Keys.Escape);
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            int inventoryLayerIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Inventory"));
            if (inventoryLayerIndex != -1)
            {
                layers.Insert(inventoryLayerIndex + 1, new LegacyGameInterfaceLayer(
                    "ShatteredIllusion: Journal Button",
                    delegate
                    {
                        if (Main.playerInventory || JournalState.IsOpen)
                        {
                            _journalInterface?.Draw(Main.spriteBatch, new GameTime());
                        }
                        return true;
                    },
                    InterfaceScaleType.UI)
                );
            }
        }
    }

    internal class JournalUIState : UIState
    {
        private const float AnimationSeconds = 0.35f;
        private const float MaxDarkenAlpha = 0.6f;

        private static readonly SoundStyle PullSound = new SoundStyle("ShatteredIllusion/Sounds/Journal/JournalPullUp");

        public Journal JournalButton;
        public JournalPanel JournalPanel;
        private DarkenOverlay _darkenOverlay;

        private bool _isOpen;
        private bool _openTarget;
        private float _progress;

        public bool IsOpen => _isOpen;

        public override void OnInitialize()
        {
            _darkenOverlay = new DarkenOverlay();
            Append(_darkenOverlay);

            JournalPanel = new JournalPanel();
            JournalPanel.HAlign = 0.5f;
            JournalPanel.VAlign = 0.5f;

            JournalPanel.Left.Set(0f, 0f);
            JournalPanel.Top.Set(0f, 0f);
            Append(JournalPanel);

            JournalButton = new Journal();
            JournalButton.Clicked += ToggleOpen;

            JournalButton.Left.Set(20f, 0f);
            JournalButton.Top.Set(280f, 0f);

            Append(JournalButton);
        }

        public void ToggleOpen()
        {
            if (_isOpen)
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

            _isOpen = true;
            _openTarget = true;
            JournalPanel.SetOpen(true);
            JournalButton.SetOpenVisual(true);
            SoundEngine.PlaySound(PullSound);
        }

        public void Close()
        {
            if (!_isOpen)
            {
                return;
            }

            _openTarget = false;
            JournalButton.SetOpenVisual(false);
            SoundEngine.PlaySound(PullSound);
        }

        public override void Update(GameTime gameTime)
        {
            float delta = (float)gameTime.ElapsedGameTime.TotalSeconds;
            float step = AnimationSeconds <= 0f ? 1f : delta / AnimationSeconds;

            if (_openTarget)
            {
                _progress = Math.Min(1f, _progress + step);
            }
            else
            {
                _progress = Math.Max(0f, _progress - step);
                if (_progress <= 0f && _isOpen)
                {
                    _isOpen = false;
                    JournalPanel.SetOpen(false);
                }
            }

            float eased = EaseOutCubic(_progress);

            // Slide up from below the screen as it opens, back down as it closes.
            JournalPanel.Top.Set((1f - eased) * Main.screenHeight, 0f);
            _darkenOverlay.Alpha = eased * MaxDarkenAlpha;

            base.Update(gameTime);
        }

        private static float EaseOutCubic(float t)
        {
            float inv = 1f - t;
            return 1f - inv * inv * inv;
        }
    }

    // Simple full-screen dimmer drawn behind the journal panel.
    internal class DarkenOverlay : UIElement
    {
        public float Alpha;

        public DarkenOverlay()
        {
            Width.Set(0f, 1f);
            Height.Set(0f, 1f);
        }

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            if (Alpha <= 0f)
            {
                return;
            }

            CalculatedStyle dimensions = GetDimensions();
            spriteBatch.Draw(TextureAssets.MagicPixel.Value, dimensions.ToRectangle(), Color.Black * Alpha);
        }
    }
}