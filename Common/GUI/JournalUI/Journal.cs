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

            float scale = 1f + 0.1f * _hover;
            float rotation = (float)Math.Sin(time * 9f) * 0.04f * _hover;
            Color drawColor = Color.White * (0.85f + 0.15f * _hover);

            spriteBatch.Draw(texture, position, null, drawColor, rotation, origin, scale, SpriteEffects.None, 0f);

            if (_isHovered)
            {
                Main.hoverItemName = "Journal";
                Main.LocalPlayer.mouseInterface = true;
            }
        }
    }

    internal class JournalPanel : UIElement
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

        // TODO: replace with a real title/emblem caption.
        private const string BookTitle = "BOOK NAME HERE";
        private const string SymbolLabel = "SYMBOL HERE";

        // Format: "Title\n\nBody". The first line becomes the page heading.
        private static readonly string[] Pages =
        {
            "HELLO\n\nThis page is wrapped by measured pixel width now, so there is no character limit to worry about.",
            "How to read\n\nClick the right page or press Right to turn forward. Click the left page or press Left to turn back. Click an entry on the contents page to jump to it.",
            "Entry I\n\nI like Fargo's Sata mode.",
            "Entry II\n\nHELP I HAVE NOTHING TO SHOW THIS LOOKS STRAIGHT BUNS IMMA GET YELLED AT.",
            "Entry III\n\n;iouahfbli;uhsdfgliuhsdfgliuhdfg",
            "Entry IV\n\nHELP I HAVE NOTHING TO SHOW THIS LOOKS STRAIGHT BUNS IMMA GET YELLED AT.",
            "Entry V\n\npioajgf[iopsjdfg[oisjdfgpoiuj",
            "Entry VI\n\nHELP I HAVE NOTHING TO SHOW THIS LOOKS STRAIGHT BUNS IMMA GET YELLED AT.",
            "Entry VII\n\nsgoijsdhg;oijsdfg;oisjdfg",
            "Entry VIII\n\nHELP I HAVE NOTHING TO SHOW THIS LOOKS STRAIGHT BUNS IMMA GET YELLED AT.",
            "Entry IX\n\nauahsdgliauhsflaisdfhasdff",
            "Entry X\n\nHELP I HAVE NOTHING TO SHOW THIS LOOKS STRAIGHT BUNS IMMA GET YELLED AT.",
            "Credits\n\nIn memory of those who have contributed to the journal.\n\nProject Lead : Pickle",
            "Credits\n\nIn memory of those who have contributed to the journal.\n\nArtist : Tomato",
        };

        // Note: the left page index of the target spread is used as the target page index.
        private static readonly TocEntry[] TableOfContents =
        {
            new TocEntry("How to read", 0),
            new TocEntry("Entry I", 2),
            new TocEntry("Entry II", 2),
            new TocEntry("Entry III", 4),
            new TocEntry("Entry IV", 4),
            new TocEntry("Entry V", 6),
            new TocEntry("Entry VI", 6),
            new TocEntry("Entry VII", 8),
            new TocEntry("Entry VIII", 8),
            new TocEntry("Entry IX", 10),
            new TocEntry("Entry X", 10),
            new TocEntry("Credits", 12),
        };

        private static readonly Dictionary<string, PageLayout> LayoutCache = new Dictionary<string, PageLayout>();
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
        private readonly float[] _tocHover = new float[TableOfContents.Length];
        private readonly List<Rectangle> _tocEntryBounds = new List<Rectangle>();
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
                Array.Clear(_tocHover, 0, _tocHover.Length);
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

            StartFlip(_leftPageIndex, target, target > _leftPageIndex ? 1 : -1);
        }

        private void TurnForward()
        {
            if (_leftPageIndex == CoverPageIndex)
            {
                if (Pages.Length > 0)
                {
                    StartFlip(CoverPageIndex, 0, 1);
                }
                return;
            }

            if (_leftPageIndex + 2 >= Pages.Length)
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
            bool canForward = _leftPageIndex == CoverPageIndex ? Pages.Length > 0 : _leftPageIndex + 2 < Pages.Length;
            bool canBack = _leftPageIndex != CoverPageIndex;

            _hoverRight = MathHelper.Lerp(_hoverRight, hoverBook && onRight && canForward ? 1f : 0f, 0.2f);
            _hoverLeft = MathHelper.Lerp(_hoverLeft, hoverBook && !onRight && canBack ? 1f : 0f, 0.2f);

            DrawContent(sb, d, interactive, mouse);
            DrawEdgeGlows(sb, d);

            if (DebugLayout)
            {
                DrawDebugLayout(sb, d);
            }

            if (hoverBook)
            {
                Main.hoverItemName = _leftPageIndex == CoverPageIndex
                    ? "Table of contents"
                    : (onRight ? "Next pages" : "Previous pages");
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
            if (index < 0 || index >= Pages.Length)
            {
                return;
            }

            float px = d.X + (right ? RightPageX : LeftPageX) * Scale;
            float py = d.Y + PageTextTop * Scale;
            float textWidth = PageTextWidth * Scale;
            PageLayout layout = GetLayout(Pages[index]);

            DrawText(sb, layout.Title, MapX(px, py), TitleColor, TitleScale, xs);

            // Ink divider under the heading.
            Vector2 dividerPos = MapX(px, py + 32f);
            sb.Draw(JournalGfx.HGradient, new Rectangle((int)dividerPos.X, (int)dividerPos.Y, (int)(textWidth * 0.75f * xs), 2),
                PageTextColor * (0.8f * _alpha));

            float y = py + 44f;
            for (int i = 0; i < layout.Lines.Length; i++)
            {
                DrawText(sb, layout.Lines[i], MapX(px, y + i * PageLineHeight), PageTextColor, PageTextScale, xs);
            }

            // Page number, centered under the page.
            string number = (index + 1).ToString();
            DrawCenteredText(sb, number, px + textWidth * 0.5f, d.Y + 312f * Scale, 0.55f, xs, PageTextColor);
        }

        private void DrawCoverLeft(SpriteBatch sb, CalculatedStyle d, float xs)
        {
            float leftX = d.X + LeftPageX * Scale;
            float centerX = leftX + PageTextWidth * Scale * 0.5f;
            float topY = d.Y + PageTextTop * Scale;
            float time = Main.GlobalTimeWrappedHourly;

            DrawCenteredText(sb, BookTitle, centerX, topY, 1.15f, xs, TitleColor);

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

            DrawCenteredText(sb, SymbolLabel, centerX, symbolCenter.Y + symbolSize * 0.5f + 14f, PageTextScale, xs, PageTextColor);
        }

        private void DrawCoverRight(SpriteBatch sb, CalculatedStyle d, float xs, bool interactive, Vector2 mouse)
        {
            float rightX = d.X + RightPageX * Scale;
            float textWidth = PageTextWidth * Scale;
            float topY = d.Y + PageTextTop * Scale;
            DynamicSpriteFont font = FontAssets.MouseText.Value;

            DrawCenteredText(sb, "TABLE OF CONTENTS", rightX + textWidth * 0.5f, topY, 0.9f, xs, TitleColor);

            Vector2 dividerPos = MapX(rightX, topY + 28f);
            sb.Draw(JournalGfx.HGradient, new Rectangle((int)dividerPos.X, (int)dividerPos.Y, (int)(textWidth * 0.75f * xs), 2),
                PageTextColor * (0.8f * _alpha));

            float entryY = topY + 46f;
            int hovered = -1;
            Point mousePoint = new Point((int)mouse.X, (int)mouse.Y);

            for (int i = 0; i < TableOfContents.Length; i++)
            {
                Rectangle bounds = new Rectangle((int)rightX, (int)entryY, (int)textWidth, (int)TocLineHeight);
                if (interactive)
                {
                    _tocEntryBounds.Add(bounds);
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

            entryY = topY + 46f;
            for (int i = 0; i < TableOfContents.Length; i++)
            {
                float h = _tocHover[i];
                string label = "- " + TableOfContents[i].Label;
                Color color = Color.Lerp(PageTextColor, HoverGold, h);

                DrawText(sb, label, MapX(rightX + h * 8f, entryY), color, PageTextScale, xs);

                if (h > 0.02f)
                {
                    float labelWidth = font.MeasureString(label).X * PageTextScale;
                    Vector2 underline = MapX(rightX + h * 8f, entryY + 20f);
                    sb.Draw(TextureAssets.MagicPixel.Value,
                        new Rectangle((int)underline.X, (int)underline.Y, (int)(labelWidth * h * xs), 2),
                        HoverGold * (0.85f * h * _alpha));
                }

                entryY += TocLineHeight;
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

        private void DrawText(SpriteBatch sb, string text, Vector2 mappedPos, Color color, float scale, float xs)
        {
            if (string.IsNullOrEmpty(text) || _alpha <= 0.01f)
            {
                return;
            }

            DynamicSpriteFont font = FontAssets.MouseText.Value;
            Vector2 s = new Vector2(scale * xs, scale);
            Color outline = new Color(90, 30, 50) * (0.22f * _alpha);

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

        private static PageLayout GetLayout(string pageText)
        {
            if (LayoutCache.TryGetValue(pageText, out PageLayout layout))
            {
                return layout;
            }

            int split = pageText.IndexOf('\n');
            string title = split < 0 ? pageText : pageText.Substring(0, split);
            string body = split < 0 ? string.Empty : pageText.Substring(split + 1).TrimStart('\n');

            layout = new PageLayout(title, WrapText(body, PageTextScale, PageTextWidth * Scale));
            LayoutCache[pageText] = layout;
            return layout;
        }

        private static string[] WrapText(string text, float scale, float maxWidth)
        {
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            List<string> lines = new List<string>();

            foreach (string paragraph in text.Split('\n'))
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
                    if (!string.IsNullOrEmpty(line) && font.MeasureString(candidate).X * scale > maxWidth)
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

            return lines.ToArray();
        }

        private sealed class PageLayout
        {
            public readonly string Title;
            public readonly string[] Lines;

            public PageLayout(string title, string[] lines)
            {
                Title = title;
                Lines = lines;
            }
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