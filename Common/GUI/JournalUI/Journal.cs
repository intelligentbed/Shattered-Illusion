using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
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
    internal class Journal : UIElement
    {
        private const string ClosedTexturePath = "ShatteredIllusion/Assets/UI/Journal_Closed";
        private const string OpenTexturePath = "ShatteredIllusion/Assets/UI/Journal_Open";

        private Asset<Texture2D> _closedTexture;
        private Asset<Texture2D> _openTexture;

        private bool _isOpenVisual;
        private bool _isHovered;

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
            Texture2D texture = (_isOpenVisual ? _openTexture : _closedTexture).Value;
            CalculatedStyle dimensions = GetDimensions();

            Color drawColor = _isHovered ? Color.White : Color.White * 0.85f;

            spriteBatch.Draw(
                texture,
                dimensions.Position(),
                null,
                drawColor,
                0f,
                Vector2.Zero,
                1f,
                SpriteEffects.None,
                0f
            );

            if (_isHovered)
            {
                Main.hoverItemName = "Journal";
            }
        }
    }

    internal class JournalPanel : UIElement
    {
        private const string TexturePath = "ShatteredIllusion/Assets/UI/Journal_UI";

        // TODO: ACTUALLY MAKE THE SYMBOL IMAGE 
        private const string SymbolTexturePath = "ShatteredIllusion/Assets/UI/Journal_Symbol";

        private const float Scale = 1.75f;
        private const float TextureWidth = 466f;
        private const float TextureHeight = 344f;
        private const float PageTextScale = 0.8f;
        private const float PageLineHeight = 22f;
        private const int MaxCharactersPerLine = 40;
        private static readonly Color PageTextColor = new Color(176, 92, 62);

        private const int CoverPageIndex = -1; //-1 indicates the cover spread 

        // TODO: replace with your real title/emblem caption.
        private const string BookTitle = "        BOOK NAME HERE";
        private const string SymbolLabel = "        SYMBOL HERE";

        private static readonly string[] Pages =
        {
            "HELLO\n\nCURRENTLY THERE IS A 40 CHARACTER LIMIT PERLINE SO IM TRYING TO TEST THAT.",
            "How to read\n\nClick the right page to turn forward. Click the left page to turn back. but this is currently that that well implemented.",
            "Entry I\n\nI like Fargo's Sata mode.",
            "Entry II\n\nHELP I HAVE NOTHING TO SHOW THIS LOOKS STRAIGHT BUNS IMMA GET YELLED AT.",
        };

        // TODO: add one entry per topic, targeting the left-page index of that spread.
        private static readonly TocEntry[] TableOfContents =
        {
            new TocEntry("How to read", 0),
            new TocEntry("Entry I", 2),
            new TocEntry("Entry II", 2),
        };

        private Asset<Texture2D> _texture;
        private Asset<Texture2D> _symbolTexture;
        private bool _isOpen;
        private int _leftPageIndex = CoverPageIndex;
        private bool _isTurningForward;

        private readonly List<Rectangle> _tocEntryBounds = new List<Rectangle>();

        public JournalPanel()
        {
            Width.Set(TextureWidth * Scale, 0f);
            Height.Set(TextureHeight * Scale, 0f);
        }

        public override void OnInitialize()
        {
            _texture = ModContent.Request<Texture2D>(TexturePath);

            try
            {
                _symbolTexture = ModContent.Request<Texture2D>(SymbolTexturePath);
            }
            catch
            {
                _symbolTexture = null;
            }
        }

        public void SetOpen(bool isOpen)
        {
            _isOpen = isOpen;
            if (isOpen)
            {
                _leftPageIndex = CoverPageIndex;
            }
        }

        public override bool ContainsPoint(Vector2 point)
        {
            return _isOpen && base.ContainsPoint(point);
        }

        public override void LeftClick(UIMouseEvent evt)
        {
            base.LeftClick(evt);

            if (_leftPageIndex == CoverPageIndex && TryHandleTocClick(evt.MousePosition))
            {
                return;
            }

            float centerX = GetDimensions().X + GetDimensions().Width / 2f;
            if (evt.MousePosition.X < centerX)
            {
                TurnBackward();
            }
            else
            {
                TurnForward();
            }
        }

        public override void MouseOver(UIMouseEvent evt)
        {
            base.MouseOver(evt);
            _isTurningForward = evt.MousePosition.X >= GetDimensions().X + GetDimensions().Width / 2f;
        }

        private bool TryHandleTocClick(Vector2 mousePosition)
        {
            Point mousePoint = new Point((int)mousePosition.X, (int)mousePosition.Y);

            for (int i = 0; i < _tocEntryBounds.Count && i < TableOfContents.Length; i++)
            {
                if (_tocEntryBounds[i].Contains(mousePoint))
                {
                    NavigateToSpread(TableOfContents[i].TargetPageIndex);
                    return true;
                }
            }

            return false;
        }

        private void NavigateToSpread(int targetPageIndex)
        {
            if (Pages.Length < 2)
            {
                return;
            }

            int target = targetPageIndex - (targetPageIndex % 2);
            int maxStart = Pages.Length % 2 == 0 ? Pages.Length - 2 : Pages.Length - 1;
            target = Math.Clamp(target, 0, maxStart);

            _leftPageIndex = target;
            SoundEngine.PlaySound(SoundID.MenuTick);
        }

        private void TurnForward()
        {
            if (_leftPageIndex == CoverPageIndex)
            {
                if (Pages.Length == 0)
                {
                    return;
                }

                _leftPageIndex = 0;
                SoundEngine.PlaySound(SoundID.MenuTick);
                return;
            }

            if (_leftPageIndex + 2 >= Pages.Length)
            {
                return;
            }

            _leftPageIndex += 2;
            SoundEngine.PlaySound(SoundID.MenuTick);
        }

        private void TurnBackward()
        {
            if (_leftPageIndex == CoverPageIndex)
            {
                return;
            }

            if (_leftPageIndex == 0)
            {
                _leftPageIndex = CoverPageIndex;
                SoundEngine.PlaySound(SoundID.MenuTick);
                return;
            }

            _leftPageIndex -= 2;
            SoundEngine.PlaySound(SoundID.MenuTick);
        }

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            if (!_isOpen)
            {
                return;
            }

            CalculatedStyle dimensions = GetDimensions();
            spriteBatch.Draw(
                _texture.Value,
                dimensions.Position(),
                null,
                Color.White,
                0f,
                Vector2.Zero,
                Scale,
                SpriteEffects.None,
                0f);

            if (_leftPageIndex == CoverPageIndex)
            {
                DrawCoverSpread(spriteBatch, dimensions);
            }
            else
            {
                _tocEntryBounds.Clear();

                DrawPage(spriteBatch, Pages[_leftPageIndex], dimensions.X + 28f * Scale, dimensions.Y + 52f * Scale);
                DrawPage(spriteBatch, Pages[_leftPageIndex + 1], dimensions.X + 253f * Scale, dimensions.Y + 52f * Scale);

                string pageNumbers = $"{_leftPageIndex + 1} - {_leftPageIndex + 2} / {Pages.Length}";
                Utils.DrawBorderString(
                    spriteBatch,
                    pageNumbers,
                    new Vector2(dimensions.X + 190f * Scale, dimensions.Y + 294f * Scale),
                    PageTextColor,
                    0.55f);
            }

            if (IsMouseHovering)
            {
                Main.hoverItemName = _leftPageIndex == CoverPageIndex
                    ? "Table of contents"
                    : (_isTurningForward ? "Next pages" : "Previous pages");
            }
        }

        private void DrawCoverSpread(SpriteBatch spriteBatch, CalculatedStyle dimensions)
        {
            float leftX = dimensions.X + 28f * Scale;
            float rightX = dimensions.X + 253f * Scale;
            float topY = dimensions.Y + 40f * Scale;

            // Left page: title, symbol image, symbol caption and more maybe 
            Utils.DrawBorderString(spriteBatch, BookTitle, new Vector2(leftX, topY), PageTextColor, 1f);

            float symbolSize = 90f * Scale;
            Vector2 symbolPos = new Vector2(leftX, topY + 40f * Scale);

            if (_symbolTexture != null && _symbolTexture.IsLoaded)
            {
                spriteBatch.Draw(
                    _symbolTexture.Value,
                    new Rectangle((int)symbolPos.X, (int)symbolPos.Y, (int)symbolSize, (int)symbolSize),
                    Color.White);
            }

            Utils.DrawBorderString(
                spriteBatch,
                SymbolLabel,
                new Vector2(leftX, symbolPos.Y + symbolSize + 10f),
                PageTextColor,
                PageTextScale);

            // Right page: table of contents
            Utils.DrawBorderString(spriteBatch, "TABLE OF CONTENTS", new Vector2(rightX, topY), PageTextColor, 0.9f);

            _tocEntryBounds.Clear();
            float entryY = topY + 40f * Scale;

            for (int i = 0; i < TableOfContents.Length; i++)
            {
                string label = $"- {TableOfContents[i].Label}";
                Utils.DrawBorderString(spriteBatch, label, new Vector2(rightX, entryY), PageTextColor, PageTextScale);

                var bounds = new Rectangle((int)rightX, (int)entryY, (int)(180f * Scale), (int)PageLineHeight);
                _tocEntryBounds.Add(bounds);

                entryY += PageLineHeight;
            }
        }

        private static void DrawPage(SpriteBatch spriteBatch, string pageText, float x, float y)
        {
            string[] lines = WrapText(pageText).ToArray();
            for (int i = 0; i < lines.Length; i++)
            {
                Utils.DrawBorderString(
                    spriteBatch,
                    lines[i],
                    new Vector2(x, y + i * PageLineHeight),
                    PageTextColor,
                    PageTextScale);
            }
        }

        private static List<string> WrapText(string pageText)
        {
            List<string> lines = new List<string>();
            foreach (string paragraph in pageText.Split('\n'))
            {
                if (string.IsNullOrWhiteSpace(paragraph))
                {
                    lines.Add(string.Empty);
                    continue;
                }

                string line = string.Empty;
                foreach (string word in paragraph.Split(' '))
                {
                    string candidate = string.IsNullOrEmpty(line) ? word : $"{line} {word}";
                    if (candidate.Length > MaxCharactersPerLine && !string.IsNullOrEmpty(line))
                    {
                        lines.Add(line);
                        line = word;
                    }
                    else
                    {
                        line = candidate;
                    }
                }

                if (!string.IsNullOrEmpty(line))
                {
                    lines.Add(line);
                }
            }

            return lines;
        }

        private readonly struct TocEntry
        {
            public readonly string Label;
            public readonly int TargetPageIndex;

            public TocEntry(string label, int targetPageIndex)
            {
                Label = label;
                TargetPageIndex = targetPageIndex;
            }
        }
    }
}