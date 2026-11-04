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
    public class JournalUISystem : ModSystem
    {
        internal JournalUIState JournalState;
        private UserInterface _journalInterface;

        public override void Load()
        {
            if (!Main.dedServ)
            {
                JournalRegistry.Build();
                JournalState = new JournalUIState();
                JournalState.Activate();
                _journalInterface = new UserInterface();
                _journalInterface.SetState(JournalState);
            }
        }

        public override void Unload()
        {
            JournalState = null;
            _journalInterface = null;
            JournalRegistry.Clear();
            JournalToast.Clear();
            if (!Main.dedServ)
            {
                JournalGfx.Dispose();
            }
        }

        // Localization is loaded by now: log the exact key of anything the journal expects but can't find.
        public override void PostSetupContent()
        {
            if (Main.dedServ)
            {
                return;
            }

            foreach (JournalChapter chapter in JournalRegistry.Chapters)
            {
                string chapterKey = $"{JournalText.Root}.Chapters.{chapter.Id}.Title";
                if (!Terraria.Localization.Language.Exists(chapterKey))
                {
                    JournalBuilder.Warn($"Missing localization key: {chapterKey}");
                }

                foreach (JournalPage page in chapter.Pages)
                {
                    string bodyKey = $"{JournalText.Root}.Chapters.{chapter.Id}.Pages.{page.Id}.Body";
                    if (!Terraria.Localization.Language.Exists(bodyKey))
                    {
                        JournalBuilder.Warn($"Missing localization key: {bodyKey}");
                    }
                }
            }
        }

        public override void OnWorldLoad()
        {
            // Unlock polling waits a moment after this so synced world flags (boss kills etc.) have arrived.
            JournalPlayer.WorldEnterTick = Main.GameUpdateCount;
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

            // While the journal is up, the mouse belongs to the UI (no swinging items behind it).
            if (JournalState.IsOpen)
            {
                Main.LocalPlayer.mouseInterface = true;
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
            int mouseTextIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Mouse Text"));
            if (mouseTextIndex != -1)
            {
                layers.Insert(mouseTextIndex, new LegacyGameInterfaceLayer(
                    "ShatteredIllusion: Journal Toast",
                    delegate
                    {
                        JournalToast.Draw(Main.spriteBatch);
                        return true;
                    },
                    InterfaceScaleType.UI));
            }

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
}