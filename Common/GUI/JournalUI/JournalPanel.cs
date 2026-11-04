using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ReLogic.Content;
using ReLogic.Graphics;
using ShatteredIllusion.Common.Visuals;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace ShatteredIllusion.Common.GUI.JournalUI
{
    internal partial class JournalPanel : UIElement
    {
        private const string TexturePath = "ShatteredIllusion/Assets/UI/Journal_UI";

        // TODO: ACTUALLY MAKE THE SYMBOL IMAGE
        private const string SymbolTexturePath = "ShatteredIllusion/Assets/UI/Journal_Symbol";

        private static readonly SoundStyle PageTurnSound = new SoundStyle("ShatteredIllusion/Sounds/Journal/JournalPageTurn")
        {
            PitchVariance = 0.12f
        };

        // ---- Layout (texture-space pixels, multiplied by Scale). Tune these to match your art. ----
        // Measured from the new open-book sprite (586 x 416). If the PNG's real size differs, change these two.
        private const float Scale = 1.5f;
        private const float TextureWidth = 586f;
        private const float TextureHeight = 416f;
        private const float SpineCenterX = 293.5f;  // center of the gap between the two pages (measured)

        // Text columns (texture px). Measured paper: left x 20..289, right x 298..567.
        private const float LeftPageX = 40f;
        private const float RightPageX = 322f;
        private const float PageTextTop = 48f;
        private const float PageTextWidth = 225f;   // wrap width, measured in pixels (replaces the old 40-char limit)

        // Paper area used by the turning leaf (texture px), measured from the sprite.
        private const float LeafInnerOffset = 5f;   // gap from spine center to the paper's inner edge
        private const float LeafOuterLeftX = 20f;   // paper's outer edge, left page
        private const float LeafOuterRightX = 568f; // paper's outer edge, right page
        private const float PaperTop = 8f;                // top edge across most of the page
        private const float PaperTopDipAtSpine = 34f;     // extra drop of the top edge right at the spine
        private const float PaperTopDipLength = 45f;      // how far from the spine that dip fades out
        private const float PaperBottomAtSpine = 308f;    // bottom edge slopes down from here...
        private const float PaperBottomAtOuter = 368f;    // ...to here at the outer edge
        private const int LeafStrips = 28;                // vertical slices used to follow the curved edges

        // Flip to true to draw outlines of the text columns and paper area for lining up against the art.
        private static readonly bool DebugLayout = false;

        // ---- Text ----
        private const float TitleScale = 1.0f;
        private const float PageTextScale = 0.8f;
        private const float PageLineHeight = 22f;
        private const float TocLineHeight = 24f;
        private static readonly Color PageTextColor = new Color(112, 42, 64);   // wine, reads well on the peach paper
        private static readonly Color TitleColor = new Color(84, 24, 48);
        private static readonly Color HoverGold = new Color(214, 48, 102);      // hover accent (crimson, matches the spine)
        private static readonly Color LeafPaperColor = new Color(246, 224, 208); // color of the turning page's blank back
        private static readonly Color GlowColor = new Color(255, 100, 150);     // lamp glow / aura behind the book

        private const float FlipSeconds = 0.5f;
        private const int CoverPageIndex = -1; // -1 indicates the cover spread

        private const int MaxPageLines = 15;      // body lines that fit under the heading; extra lines are clipped (and logged)
        private const float TocTop = 46f;          // y offset of the first contents entry under the page top

        // Light paper wants a faint drop shadow rather than a dark outline.
        private static readonly Point[] OutlineOffsets =
        {
            new Point(1, 1)
        };

        private Asset<Texture2D> _texture;
        private Asset<Texture2D> _symbolTexture;

        private bool _isOpen;
        private float _progress;   // raw 0..1 open progress from the state
        private float _alpha;      // eased fade used for all drawing

        private int _leftPageIndex = CoverPageIndex;

        // page-turn animation
        private bool _flipping;
        private float _flipT;
        private int _flipFrom;
        private int _flipTo;
        private int _flipDir;

        // hover state
        private float _hoverLeft;
        private float _hoverRight;
        private int _lastTocHover = -1;
        private float[] _tocHover = Array.Empty<float>();
        private readonly List<Rectangle> _tocEntryBounds = new List<Rectangle>();
        private readonly List<int> _tocEntryChapter = new List<int>();   // chapter index for each bounds entry (scrolling shifts them)
        private int _tocScroll;
        private float _clock;                                            // seconds the book has been open; drives all journal animations
        private string _hoverHint;                                       // tooltip for a locked paragraph under the cursor
        private float _spineX;
        private float _fit = 1f;   // extra uniform scale so the whole book fits any screen size / aspect ratio

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
                _flipping = false;
                _lastTocHover = -1;
                _tocScroll = 0;
                _tocHover = new float[JournalRegistry.Chapters.Length];
                ResetReveals();
            }
        }

        public void SetProgress(float progress)
        {
            _progress = MathHelper.Clamp(progress, 0f, 1f);
        }

        private bool Interactive => _isOpen && _progress >= 0.98f && !_flipping;

        // The book is laid out at a fixed "design size" and then scaled as a whole to fit the screen
        // (see UpdateFit). These helpers convert between screen/UI space and that design space.
        private void UpdateFit()
        {
            float uiWidth = Main.screenWidth / Main.UIScale;
            float uiHeight = Main.screenHeight / Main.UIScale;
            float designWidth = TextureWidth * Scale;
            float designHeight = TextureHeight * Scale;

            // Stay clear of whichever letterbox bars are larger: the journal's own, or a boss/cutscene's
            // (Cinema draws its bars on top of all UI, so they would otherwise clip the book's edges).
            // The journal's target height is used rather than its animated height so the book doesn't resize while opening.
            float bar = Math.Max(JournalBackdrop.TargetBarHeight(uiWidth, uiHeight), Cinema.CurrentLetterboxHeight());
            float usableHeight = Math.Max(1f, uiHeight - 2f * bar);

            // The book takes this share of the free space; lower it to make the book smaller everywhere.
            const float ScreenFill = 0.76f;
            float fit = Math.Min(usableHeight * ScreenFill / designHeight, uiWidth * 0.8f / designWidth);
            _fit = MathHelper.Clamp(fit, 0.5f, 1.5f);
        }

        private Vector2 ToDesignSpace(Vector2 uiPoint)
        {
            Vector2 center = GetDimensions().Center();
            return center + (uiPoint - center) / _fit;
        }

        public override bool ContainsPoint(Vector2 point)
        {
            return _isOpen && _progress >= 0.95f && base.ContainsPoint(ToDesignSpace(point));
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (!_isOpen)
            {
                return;
            }

            UpdateFit();
            _clock += (float)gameTime.ElapsedGameTime.TotalSeconds;

            if (!_flipping && _leftPageIndex == CoverPageIndex && Interactive)
            {
                int max = Math.Max(0, JournalRegistry.Chapters.Length - TocCapacity);
                int wheel = PlayerInput.ScrollWheelDeltaForUI;
                if (wheel != 0 && max > 0)
                {
                    _tocScroll = Math.Clamp(_tocScroll - Math.Sign(wheel), 0, max);
                }
            }

            if (_flipping)
            {
                _flipT += (float)gameTime.ElapsedGameTime.TotalSeconds / FlipSeconds;
                if (_flipT >= 1f)
                {
                    _flipping = false;
                    _leftPageIndex = _flipTo;
                }
                return;
            }

            if (_progress >= 0.98f)
            {
                if (Main.keyState.IsKeyDown(Keys.Right) && !Main.oldKeyState.IsKeyDown(Keys.Right))
                {
                    TurnForward();
                }
                else if (Main.keyState.IsKeyDown(Keys.Left) && !Main.oldKeyState.IsKeyDown(Keys.Left))
                {
                    TurnBackward();
                }
            }
        }

        public override void LeftClick(UIMouseEvent evt)
        {
            base.LeftClick(evt);

            if (!Interactive)
            {
                return;
            }

            Vector2 mouse = ToDesignSpace(evt.MousePosition);

            if (_leftPageIndex == CoverPageIndex && TryHandleTocClick(mouse))
            {
                return;
            }

            float centerX = GetDimensions().X + SpineCenterX * Scale;
            if (mouse.X < centerX)
            {
                TurnBackward();
            }
            else
            {
                TurnForward();
            }
        }

        // ------------------------------------------------------------------ navigation

        private bool TryHandleTocClick(Vector2 mousePosition)
        {
            Point mousePoint = new Point((int)mousePosition.X, (int)mousePosition.Y);

            for (int i = 0; i < _tocEntryBounds.Count; i++)
            {
                if (_tocEntryBounds[i].Contains(mousePoint))
                {
                    NavigateToSpread(JournalRegistry.Chapters[_tocEntryChapter[i]].FirstPageIndex);
                    return true;
                }
            }

            return false;
        }

        private void NavigateToSpread(int targetPageIndex)
        {
            int pageCount = JournalRegistry.Pages.Length;
            if (pageCount < 2)
            {
                return;
            }

            int target = targetPageIndex - (targetPageIndex % 2);
            int maxStart = pageCount % 2 == 0 ? pageCount - 2 : pageCount - 1;
            target = Math.Clamp(target, 0, maxStart);

            StartFlip(_leftPageIndex, target, target > _leftPageIndex ? 1 : -1);
        }

        private void TurnForward()
        {
            if (_leftPageIndex == CoverPageIndex)
            {
                if (JournalRegistry.Pages.Length > 0)
                {
                    StartFlip(CoverPageIndex, 0, 1);
                }
                return;
            }

            if (_leftPageIndex + 2 >= JournalRegistry.Pages.Length)
            {
                return;
            }

            StartFlip(_leftPageIndex, _leftPageIndex + 2, 1);
        }

        private void TurnBackward()
        {
            if (_leftPageIndex == CoverPageIndex)
            {
                return;
            }

            StartFlip(_leftPageIndex, _leftPageIndex == 0 ? CoverPageIndex : _leftPageIndex - 2, -1);
        }

        private void StartFlip(int from, int to, int direction)
        {
            if (_flipping || from == to)
            {
                return;
            }

            ResetReveals();   // a reveal interrupted by a flip replays the next time its page is on screen
            _flipping = true;
            _flipT = 0f;
            _flipFrom = from;
            _flipTo = to;
            _flipDir = direction;
            SoundEngine.PlaySound(PageTurnSound);
        }

        // ------------------------------------------------------------------ drawing

        public override void Draw(SpriteBatch spriteBatch)
        {
            if (!_isOpen || _texture == null)
            {
                return;
            }

            JournalGfx.EnsureBuilt();
            UpdateFit();

            float p = _progress;
            float eased = JournalGfx.EaseOutCubic(p);
            _alpha = eased;

            CalculatedStyle d = GetDimensions();
            _spineX = d.X + SpineCenterX * Scale;
            Vector2 center = d.Center();

            // The book rises from below with a slight tilt and a small overshoot, like it's being set on a desk.
            // _fit scales the whole book to the screen; everything inside is still laid out in design space.
            float zoom = MathHelper.Lerp(0.82f, 1f, JournalGfx.EaseOutBack(p)) * _fit;
            float tilt = (1f - eased) * -0.06f;
            float rise = (1f - eased) * (Main.screenHeight / Main.UIScale) * 0.25f;

            Matrix transform =
                Matrix.CreateTranslation(-center.X, -center.Y, 0f) *
                Matrix.CreateRotationZ(tilt) *
                Matrix.CreateScale(zoom, zoom, 1f) *
                Matrix.CreateTranslation(center.X, center.Y + rise, 0f) *
                Main.UIScaleMatrix;

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.SamplerStateForCursor,
                DepthStencilState.None, RasterizerState.CullCounterClockwise, null, transform);

            DrawBook(spriteBatch, d);

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.SamplerStateForCursor,
                DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.UIScaleMatrix);
        }

        private void DrawBook(SpriteBatch sb, CalculatedStyle d)
        {
            float time = Main.GlobalTimeWrappedHourly;
            bool interactive = Interactive;

            // Contact shadow under the book.
            sb.Draw(JournalGfx.Soft,
                new Rectangle((int)(d.X - d.Width * 0.08f), (int)(d.Y + d.Height * 0.8f), (int)(d.Width * 1.16f), (int)(d.Height * 0.32f)),
                Color.Black * (0.55f * _alpha));

            // Warm lamp glow behind the book.
            float pulse = 0.85f + 0.15f * (float)Math.Sin(time * 1.7f);
            sb.Draw(JournalGfx.Soft,
                new Rectangle((int)(d.X - d.Width * 0.25f), (int)(d.Y - d.Height * 0.3f), (int)(d.Width * 1.5f), (int)(d.Height * 1.6f)),
                GlowColor * (0.22f * pulse * _alpha));

            // The book itself.
            sb.Draw(_texture.Value, d.Position(), null, Color.White * _alpha, 0f, Vector2.Zero, Scale, SpriteEffects.None, 0f);

            // Mouse info (UI-space).
            Vector2 mouse = ToDesignSpace(UserInterface.ActiveInstance.MousePosition);
            Point mousePoint = new Point((int)mouse.X, (int)mouse.Y);
            bool hoverBook = interactive && d.ToRectangle().Contains(mousePoint);
            bool onRight = mouse.X >= _spineX;
            int pageCount = JournalRegistry.Pages.Length;
            bool canForward = _leftPageIndex == CoverPageIndex ? pageCount > 0 : _leftPageIndex + 2 < pageCount;
            bool canBack = _leftPageIndex != CoverPageIndex;

            _hoverRight = MathHelper.Lerp(_hoverRight, hoverBook && onRight && canForward ? 1f : 0f, 0.2f);
            _hoverLeft = MathHelper.Lerp(_hoverLeft, hoverBook && !onRight && canBack ? 1f : 0f, 0.2f);

            _hoverHint = null;
            DrawContent(sb, d, interactive, mouse);
            DrawEdgeGlows(sb, d);

            if (DebugLayout)
            {
                DrawDebugLayout(sb, d);
            }

            if (hoverBook)
            {
                Main.hoverItemName = _hoverHint ?? (_leftPageIndex == CoverPageIndex
                    ? JournalText.Ui("TocTooltip")
                    : (onRight ? JournalText.Ui("NextPages") : JournalText.Ui("PreviousPages")));
            }
        }

        private void DrawEdgeGlows(SpriteBatch sb, CalculatedStyle d)
        {
            float top = d.Y + PaperTop * Scale;
            int height = (int)((PaperBottomAtOuter - 50f - PaperTop) * Scale);
            int glowWidth = (int)(36f * Scale);

            if (_hoverRight > 0.01f)
            {
                float edge = d.X + LeafOuterRightX * Scale;
                sb.Draw(JournalGfx.HGradient, new Rectangle((int)edge - glowWidth, (int)top, glowWidth, height), null,
                    new Color(255, 120, 170) * (0.3f * _hoverRight * _alpha), 0f, Vector2.Zero, SpriteEffects.FlipHorizontally, 0f);
            }

            if (_hoverLeft > 0.01f)
            {
                float edge = d.X + LeafOuterLeftX * Scale;
                sb.Draw(JournalGfx.HGradient, new Rectangle((int)edge, (int)top, glowWidth, height), null,
                    new Color(255, 120, 170) * (0.3f * _hoverLeft * _alpha), 0f, Vector2.Zero, SpriteEffects.None, 0f);
            }
        }

        private void DrawContent(SpriteBatch sb, CalculatedStyle d, bool interactive, Vector2 mouse)
        {
            _tocEntryBounds.Clear();

            if (!_flipping)
            {
                DrawSide(sb, d, _leftPageIndex, false, 1f, interactive, mouse);
                DrawSide(sb, d, _leftPageIndex, true, 1f, interactive, mouse);
                return;
            }

            float f = JournalGfx.EaseInOutSine(MathHelper.Clamp(_flipT, 0f, 1f));
            float u = f * MathHelper.Pi;
            float xs = Math.Abs((float)Math.Cos(u));
            bool firstHalf = f < 0.5f;
            bool forward = _flipDir > 0;

            // Pages that stay put: what's left of the old spread and what's revealed of the new one.
            DrawSide(sb, d, forward ? _flipFrom : _flipTo, false, 1f, false, mouse);
            DrawSide(sb, d, forward ? _flipTo : _flipFrom, true, 1f, false, mouse);

            // The turning leaf: shows the old page while rising, then the new page while landing.
            bool leafOnRight = forward ? firstHalf : !firstHalf;
            int leafSpread = firstHalf ? _flipFrom : _flipTo;
            DrawLeaf(sb, d, leafSpread, leafOnRight, xs, u, mouse);
        }

        private void DrawLeaf(SpriteBatch sb, CalculatedStyle d, int spread, bool onRight, float xs, float u, Vector2 mouse)
        {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            float lift = (float)Math.Sin(u);

            // The leaf is built from thin vertical strips so its bottom edge can follow the sprite's curved page bottom.
            float innerOffset = LeafInnerOffset * Scale;
            float outerWidth = (onRight ? LeafOuterRightX - SpineCenterX : SpineCenterX - LeafOuterLeftX) * Scale - innerOffset;
            float top = d.Y + PaperTop * Scale;
            float freeEdge = _spineX;
            int outerBottom = 0;
            float outerWidthTex = outerWidth / Scale;

            for (int i = 0; i < LeafStrips; i++)
            {
                float t0 = i / (float)LeafStrips;
                float t1 = (i + 1) / (float)LeafStrips;
                float tMid = (t0 + t1) * 0.5f;

                // Distance from spine (squashed by xs), measured outward.
                float dist0 = innerOffset * xs + outerWidth * t0 * xs;
                float dist1 = innerOffset * xs + outerWidth * t1 * xs;
                float xStart = onRight ? _spineX + dist0 : _spineX - dist1;
                int w = Math.Max(1, (int)Math.Ceiling(dist1 - dist0) + 1);

                // Bottom edge slopes roughly linearly from the spine out, with the outer corner rounded off.
                float bottomTex = MathHelper.Lerp(PaperBottomAtSpine, PaperBottomAtOuter, tMid);
                if (tMid > 0.93f)
                {
                    bottomTex = MathHelper.Lerp(bottomTex, 320f, (tMid - 0.93f) / 0.07f);
                }
                float bottom = d.Y + bottomTex * Scale;
                outerBottom = (int)bottom;

                // Top edge dips down right next to the spine.
                float distFromSpineTex = LeafInnerOffset + outerWidthTex * tMid;
                float dip = PaperTopDipAtSpine * Math.Max(0f, 1f - distFromSpineTex / PaperTopDipLength);
                float stripTop = top + dip * Scale;

                // Slightly darker toward the spine so the leaf reads as curved.
                Color paper = Color.Lerp(LeafPaperColor, new Color(206, 160, 160), (1f - tMid) * 0.35f);
                sb.Draw(pixel, new Rectangle((int)xStart, (int)stripTop, w, (int)(bottom - stripTop)), paper * _alpha);

                freeEdge = onRight ? _spineX + dist1 : _spineX - dist1;
            }

            // Shadow the leaf casts on the page beneath it.
            int shadowWidth = (int)(36f * Scale);
            int shadowHeight = (int)(PaperBottomAtOuter * Scale - PaperTop * Scale - 24f);
            if (onRight)
            {
                sb.Draw(JournalGfx.HGradient, new Rectangle((int)freeEdge, (int)top, shadowWidth, shadowHeight), null,
                    Color.Black * (0.35f * lift * _alpha), 0f, Vector2.Zero, SpriteEffects.None, 0f);
            }
            else
            {
                sb.Draw(JournalGfx.HGradient, new Rectangle((int)freeEdge - shadowWidth, (int)top, shadowWidth, shadowHeight), null,
                    Color.Black * (0.35f * lift * _alpha), 0f, Vector2.Zero, SpriteEffects.FlipHorizontally, 0f);
            }

            // Page content, squashed horizontally with the leaf.
            DrawSide(sb, d, spread, onRight, xs, false, mouse);

            // Bright free edge.
            int edgeX = onRight ? (int)freeEdge - 2 : (int)freeEdge;
            int edgeTop = (int)(top + 6f * Scale);
            sb.Draw(pixel, new Rectangle(edgeX, edgeTop, 2, outerBottom - edgeTop - (int)(40f * Scale)), Color.White * (0.4f * _alpha));
        }

        private void DrawDebugLayout(SpriteBatch sb, CalculatedStyle d)
        {
            Texture2D pixel = TextureAssets.MagicPixel.Value;

            void Outline(Rectangle r, Color c)
            {
                sb.Draw(pixel, new Rectangle(r.X, r.Y, r.Width, 1), c);
                sb.Draw(pixel, new Rectangle(r.X, r.Bottom - 1, r.Width, 1), c);
                sb.Draw(pixel, new Rectangle(r.X, r.Y, 1, r.Height), c);
                sb.Draw(pixel, new Rectangle(r.Right - 1, r.Y, 1, r.Height), c);
            }

            float textTop = d.Y + PageTextTop * Scale;
            int textHeight = (int)((PaperBottomAtSpine - 12f - PageTextTop) * Scale);
            int textWidth = (int)(PageTextWidth * Scale);

            // Text columns (green), paper extents (cyan), spine (yellow).
            Outline(new Rectangle((int)(d.X + LeftPageX * Scale), (int)textTop, textWidth, textHeight), Color.Lime);
            Outline(new Rectangle((int)(d.X + RightPageX * Scale), (int)textTop, textWidth, textHeight), Color.Lime);
            Outline(new Rectangle((int)(d.X + LeafOuterLeftX * Scale), (int)(d.Y + PaperTop * Scale),
                (int)((SpineCenterX - LeafOuterLeftX - LeafInnerOffset) * Scale),
                (int)((PaperBottomAtSpine - PaperTop) * Scale)), Color.Cyan);
            Outline(new Rectangle((int)(d.X + (SpineCenterX + LeafInnerOffset) * Scale), (int)(d.Y + PaperTop * Scale),
                (int)((LeafOuterRightX - SpineCenterX - LeafInnerOffset) * Scale),
                (int)((PaperBottomAtSpine - PaperTop) * Scale)), Color.Cyan);
            sb.Draw(pixel, new Rectangle((int)_spineX, (int)d.Y, 1, (int)d.Height), Color.Yellow);
        }

        // ------------------------------------------------------------------ page sides

        private void DrawSide(SpriteBatch sb, CalculatedStyle d, int spread, bool right, float xs, bool interactive, Vector2 mouse)
        {
            _currentXs = xs;
            if (spread == CoverPageIndex)
            {
                if (right)
                {
                    DrawCoverRight(sb, d, xs, interactive, mouse);
                }
                else
                {
                    DrawCoverLeft(sb, d, xs);
                }
                return;
            }

            int index = spread + (right ? 1 : 0);
            if (index < 0 || index >= JournalRegistry.Pages.Length)
            {
                return;
            }

            DrawPage(sb, d, index, right, xs, interactive, mouse);
        }

        private void DrawCoverLeft(SpriteBatch sb, CalculatedStyle d, float xs)
        {
            float leftX = d.X + LeftPageX * Scale;
            float centerX = leftX + PageTextWidth * Scale * 0.5f;
            float topY = d.Y + PageTextTop * Scale;
            float time = Main.GlobalTimeWrappedHourly;

            DrawCenteredText(sb, JournalText.Ui("BookTitle"), centerX, topY, 1.15f, xs, TitleColor);

            float symbolSize = 110f * Scale;
            Vector2 symbolCenter = new Vector2(centerX, topY + 48f * Scale + symbolSize * 0.5f);
            Vector2 mapped = MapX(symbolCenter.X, symbolCenter.Y);

            // Pulsing aura behind the emblem.
            float aura = 0.7f + 0.3f * (float)Math.Sin(time * 1.4f);
            int auraSize = (int)(symbolSize * 1.8f);
            int auraWidth = Math.Max(1, (int)(auraSize * xs));
            sb.Draw(JournalGfx.Soft,
                new Rectangle((int)mapped.X - auraWidth / 2, (int)mapped.Y - auraSize / 2, auraWidth, auraSize),
                GlowColor * (0.35f * aura * _alpha));

            if (_symbolTexture != null && _symbolTexture.IsLoaded)
            {
                int width = Math.Max(1, (int)(symbolSize * xs));
                sb.Draw(_symbolTexture.Value,
                    new Rectangle((int)mapped.X - width / 2, (int)mapped.Y - (int)symbolSize / 2, width, (int)symbolSize),
                    Color.White * _alpha);
            }
            else
            {
                // Placeholder emblem until the real symbol art exists: a slowly turning ring.
                Texture2D ring = JournalGfx.Ring;
                float rotation = xs >= 0.99f ? time * 0.25f : 0f;
                Vector2 scale = new Vector2(symbolSize * xs / ring.Width, symbolSize / ring.Height);
                sb.Draw(ring, mapped, null, PageTextColor * (0.9f * _alpha), rotation, ring.Size() / 2f, scale, SpriteEffects.None, 0f);
            }

            DrawCenteredText(sb, JournalText.Ui("SymbolLabel"), centerX, symbolCenter.Y + symbolSize * 0.5f + 14f, PageTextScale, xs, PageTextColor);
        }

        // How many contents rows fit on the right-hand cover page before the list has to scroll.
        private static int TocCapacity => Math.Max(1, (int)(((PaperBottomAtSpine - 24f) * Scale - (PageTextTop * Scale + TocTop)) / TocLineHeight));

        private void DrawCoverRight(SpriteBatch sb, CalculatedStyle d, float xs, bool interactive, Vector2 mouse)
        {
            float rightX = d.X + RightPageX * Scale;
            float textWidth = PageTextWidth * Scale;
            float topY = d.Y + PageTextTop * Scale;
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            JournalChapter[] chapters = JournalRegistry.Chapters;
            JournalPlayer jp = Main.LocalPlayer.GetModPlayer<JournalPlayer>();

            if (_tocHover.Length != chapters.Length)
            {
                _tocHover = new float[chapters.Length];
            }

            DrawCenteredText(sb, JournalText.Ui("Contents"), rightX + textWidth * 0.5f, topY, 0.9f, xs, TitleColor);

            Vector2 dividerPos = MapX(rightX, topY + 28f);
            sb.Draw(JournalGfx.HGradient, new Rectangle((int)dividerPos.X, (int)dividerPos.Y, (int)(textWidth * 0.75f * xs), 2),
                PageTextColor * (0.8f * _alpha));

            int capacity = TocCapacity;
            _tocScroll = Math.Clamp(_tocScroll, 0, Math.Max(0, chapters.Length - capacity));
            int first = _tocScroll;
            int last = Math.Min(chapters.Length, first + capacity);

            float entryY = topY + TocTop;
            int hovered = -1;
            Point mousePoint = new Point((int)mouse.X, (int)mouse.Y);
            _tocEntryChapter.Clear();

            for (int i = first; i < last; i++)
            {
                Rectangle bounds = new Rectangle((int)rightX, (int)entryY, (int)textWidth, (int)TocLineHeight);
                if (interactive)
                {
                    _tocEntryBounds.Add(bounds);
                    _tocEntryChapter.Add(i);
                    if (bounds.Contains(mousePoint))
                    {
                        hovered = i;
                    }
                }
                entryY += TocLineHeight;
            }

            if (interactive)
            {
                if (hovered != _lastTocHover)
                {
                    if (hovered >= 0)
                    {
                        SoundEngine.PlaySound(SoundID.MenuTick);
                    }
                    _lastTocHover = hovered;
                }

                for (int i = 0; i < _tocHover.Length; i++)
                {
                    _tocHover[i] = MathHelper.Lerp(_tocHover[i], i == hovered ? 1f : 0f, 0.25f);
                }
            }

            entryY = topY + TocTop;
            for (int i = first; i < last; i++)
            {
                JournalChapter chapter = chapters[i];
                float h = _tocHover[i];
                bool sealedChapter = chapter.Pages.All(p => jp.StateOf(p) == JournalState.Locked);
                bool news = jp.ChapterHasNews(chapter);
                string label = "- " + (sealedChapter ? JournalText.Ui("LockedChapter") : JournalText.ChapterTitle(chapter));
                Color color = Color.Lerp(PageTextColor * (sealedChapter ? 0.6f : 1f), HoverGold, h);

                DrawText(sb, label, MapX(rightX + h * 8f, entryY), color, PageTextScale, xs);

                if (h > 0.02f)
                {
                    float labelWidth = font.MeasureString(label).X * PageTextScale;
                    Vector2 underline = MapX(rightX + h * 8f, entryY + 20f);
                    sb.Draw(TextureAssets.MagicPixel.Value,
                        new Rectangle((int)underline.X, (int)underline.Y, (int)(labelWidth * h * xs), 2),
                        HoverGold * (0.85f * h * _alpha));
                }

                if (news)
                {
                    // Pulsing gold tag on chapters with something freshly unlocked.
                    string tag = JournalText.Ui("New");
                    float pulse = 0.65f + 0.35f * (float)Math.Sin(_clock * 5f);
                    float tagWidth = font.MeasureString(tag).X * 0.6f;
                    Vector2 tagPos = MapX(rightX + textWidth - tagWidth - 6f, entryY + 3f);
                    int glow = (int)((tagWidth + 26f) * xs);
                    sb.Draw(JournalGfx.Soft, new Rectangle((int)(tagPos.X + tagWidth * xs * 0.5f) - glow / 2, (int)tagPos.Y - 12, glow, 44),
                        new Color(255, 200, 100) * (0.45f * pulse * _alpha));
                    DrawText(sb, tag, tagPos, HoverGold * pulse, 0.6f, xs);
                }

                entryY += TocLineHeight;
            }

            // Scroll cues when the list is longer than the page.
            if (chapters.Length > capacity)
            {
                if (first > 0)
                {
                    DrawCenteredText(sb, "^", rightX + textWidth - 6f, topY + 26f, 0.7f, xs, HoverGold);
                }
                if (last < chapters.Length)
                {
                    DrawCenteredText(sb, "v", rightX + textWidth - 6f, entryY - 4f, 0.7f, xs, HoverGold);
                }
            }
        }

        // ------------------------------------------------------------------ text helpers

        // Maps an unscaled x/y to where it sits when the page is squashed toward the spine by xs.
        // Uses the squash factor of the side currently being drawn (set at the top of each DrawSide/DrawCover* call).
        private Vector2 MapX(float x, float y)
        {
            return new Vector2(_spineX + (x - _spineX) * _currentXs, y);
        }

        private float _currentXs = 1f;

        private void DrawText(SpriteBatch sb, string text, Vector2 mappedPos, Color color, float scale, float xs, float fade = 1f)
        {
            if (string.IsNullOrEmpty(text) || _alpha <= 0.01f)
            {
                return;
            }

            DynamicSpriteFont font = FontAssets.MouseText.Value;
            Vector2 s = new Vector2(scale * xs, scale);
            Color outline = new Color(90, 30, 50) * (0.22f * _alpha * fade);

            for (int i = 0; i < OutlineOffsets.Length; i++)
            {
                Vector2 offset = new Vector2(OutlineOffsets[i].X * xs, OutlineOffsets[i].Y);
                sb.DrawString(font, text, mappedPos + offset, outline, 0f, Vector2.Zero, s, SpriteEffects.None, 0f);
            }

            sb.DrawString(font, text, mappedPos, color * _alpha, 0f, Vector2.Zero, s, SpriteEffects.None, 0f);
        }

        private void DrawCenteredText(SpriteBatch sb, string text, float centerX, float y, float scale, float xs, Color color)
        {
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            float width = font.MeasureString(text).X * scale;
            DrawText(sb, text, MapX(centerX - width * 0.5f, y), color, scale, xs);
        }
    }
}
