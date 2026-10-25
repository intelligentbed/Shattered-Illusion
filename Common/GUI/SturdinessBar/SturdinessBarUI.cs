using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using ShatteredIllusion.Common.Players.ParrySystem;
using Terraria;
using Terraria.ModLoader;

namespace ShatteredIllusion.Common.GUI.SturdinessBar
{
    public static class SturdinessBarUI
    {
        //i need to get more opinions on a good default location but i put random numbers for now
        public const float DefaultPosX = 35f;
        public const float DefaultPosY = 15f;

        private const float MouseDragEpsilon = 0.05f;

        private const float FullPulseSpeed = 6f;
        private static readonly Color FullPulseColor = new Color(255, 220, 80); // gold

        // Background and fill (x/y)
        private static readonly Vector2 BackgroundSizeMultiplier = new Vector2(1.5f, 1.5f);
        private static readonly Vector2 BarSizeMultiplier = new Vector2(1.3f, 1.4f);

        private const float BorderSizeMultiplier = 1.3f;
        private const float BorderExtraScale = 1f;

        // feel stuff
        private const int PopFrames = 14;
        private const float PopScale = 0.08f;
        private const float FullPopScale = 0.16f;
        private const float FillChase = 0.22f;
        private static readonly Color DrainColor = new Color(130, 190, 255);

        private const float SweepSeconds = 2.4f;
        private const float SweepActive = 0.65f;
        private const float SweepBand = 0.12f;
        private static readonly Color SweepColor = new Color(120, 110, 70, 0);

        private static bool dragging;
        private static Vector2 dragOffset;

        private static float shownPercent;
        private static float lastPercent;
        private static int popTimer;
        private static float popStrength;

        private static Texture2D backgroundTex;
        private static Texture2D barTex;
        private static Texture2D borderTex;

        public static void LoadTextures()
        {
            backgroundTex = ModContent.Request<Texture2D>("ShatteredIllusion/Assets/UI/SturdinessBarBackground", AssetRequestMode.ImmediateLoad).Value;
            barTex = ModContent.Request<Texture2D>("ShatteredIllusion/Assets/UI/SturdinessBarFill", AssetRequestMode.ImmediateLoad).Value;
            borderTex = ModContent.Request<Texture2D>("ShatteredIllusion/Assets/UI/SturdinessBarBorder", AssetRequestMode.ImmediateLoad).Value;
        }

        public static void UnloadTextures()
        {
            backgroundTex = null;
            barTex = null;
            borderTex = null;
            dragging = false;
        }

        internal static void Reset()
        {
            dragging = false;
            popTimer = 0;
        }

        private static float Percent(ParryPlayer modPlayer)
        {
            return ParryPlayer.MaxSturdinessMeter <= 0
                ? 0f
                : MathHelper.Clamp(modPlayer.DisplaySturdinessMeter / ParryPlayer.MaxSturdinessMeter, 0f, 1f);
        }

        // runs per game tick from the system so the pop timing doesnt depend on fps
        internal static void Tick(Player player)
        {
            float target = Percent(player.GetModPlayer<ParryPlayer>());

            if (target > lastPercent + 0.001f)
            {
                popTimer = PopFrames;
                popStrength = target >= 1f && lastPercent < 1f ? FullPopScale : PopScale;
            }

            lastPercent = target;

            // chase up so a gain reads as motion, drops (the steeled drain) are already smooth
            shownPercent = target > shownPercent ? MathHelper.Lerp(shownPercent, target, FillChase) : target;
            if (target - shownPercent < 0.002f)
                shownPercent = target;

            if (popTimer > 0)
                popTimer--;
        }

        public static void Draw(SpriteBatch spriteBatch, Player player)
        {
            if (backgroundTex == null || barTex == null || borderTex == null)
                return;

            if (Main.hideUI)
                return;

            var config = ModContent.GetInstance<ShatteredIllusionConfig>();

            //if it cant find it than itll go back to default (skin like fortnite
            Vector2 screenRatioPos = new Vector2(config.SturdinessBarPosX, config.SturdinessBarPosY);
            if (screenRatioPos.X < 0f || screenRatioPos.X > 100f)
                screenRatioPos.X = DefaultPosX;
            if (screenRatioPos.Y < 0f || screenRatioPos.Y > 100f)
                screenRatioPos.Y = DefaultPosY;

            // this layer is already drawn in UI space so size/position use that, not raw screen px
            float uiWidth = Main.screenWidth / Main.UIScale;
            float uiHeight = Main.screenHeight / Main.UIScale;

            Vector2 screenPos = screenRatioPos;
            screenPos.X = (int)(screenPos.X * 0.01f * uiWidth);
            screenPos.Y = (int)(screenPos.Y * 0.01f * uiHeight);

            var modPlayer = player.GetModPlayer<ParryPlayer>();

            float t = popTimer / (float)PopFrames;
            float pop = 1f + popStrength * t * t;

            float uiScale = BorderSizeMultiplier;
            Vector2 backgroundScale = BackgroundSizeMultiplier * pop;
            Vector2 barScale = BarSizeMultiplier * pop;

            DrawBar(spriteBatch, modPlayer, screenPos, backgroundScale, barScale, uiScale * pop, t);
            HandleMouseInteraction(modPlayer, config, screenPos, screenRatioPos, uiScale, uiWidth, uiHeight);
        }

        private static void DrawBar(SpriteBatch spriteBatch, ParryPlayer modPlayer, Vector2 screenPos, Vector2 backgroundScale, Vector2 barScale, float uiScale, float popT)
        {

            Vector2 backgroundOrigin = backgroundTex.Size() * 0.5f;
            Vector2 barOrigin = barTex.Size() * 0.5f;
            Vector2 borderOrigin = borderTex.Size() * 0.5f;

            spriteBatch.Draw(backgroundTex, screenPos, null, Color.White, 0f, backgroundOrigin, backgroundScale, SpriteEffects.None, 0);

            float percent = shownPercent;
            float realPercent = Percent(modPlayer);
            bool draining = modPlayer.DisplaySturdinessMeter > modPlayer.SturdinessMeter + 0.01f;

            // While full pulse the fill's color between its normal tint and a bright gold highlight instead of just sitting there like a chud takes 1 to know 1
            Color barColor = Color.White;
            if (draining)
            {
                barColor = Color.Lerp(Color.White, DrainColor, 0.7f);
            }
            else if (realPercent >= 1f)
            {
                float pulse = (float)(0.5 + 0.5 * Math.Sin(Main.GlobalTimeWrappedHourly * FullPulseSpeed));
                barColor = Color.Lerp(Color.White, FullPulseColor, pulse);
            }

            // gold flash that settles back as the pop fades
            if (popT > 0f)
                barColor = Color.Lerp(barColor, FullPulseColor, popT * 0.8f);

            int fillWidth = (int)(barTex.Width * percent);
            int usableX = (int)(barTex.Width * ParryPlayer.SteeledCurveThreshold);

            // the part past the steeled threshold is a bit warmer so you can tell its usable
            Color usableColor = draining ? barColor : Color.Lerp(barColor, FullPulseColor, 0.3f);

            DrawFillSlice(spriteBatch, screenPos, barOrigin, barScale, 0, Math.Min(fillWidth, usableX), barColor);
            DrawFillSlice(spriteBatch, screenPos, barOrigin, barScale, usableX, fillWidth - usableX, usableColor);

            if (!draining && realPercent >= 1f)
                DrawFullSweep(spriteBatch, screenPos, barOrigin, barScale);

            spriteBatch.Draw(borderTex, screenPos, null, Color.White, 0f, borderOrigin, uiScale * BorderExtraScale, SpriteEffects.None, 0);
        }

        // draws a vertical slice of the fill texture where it would sit in the full bar
        private static void DrawFillSlice(SpriteBatch spriteBatch, Vector2 screenPos, Vector2 barOrigin, Vector2 barScale, int x, int width, Color color)
        {
            int x0 = Math.Max(x, 0);
            int x1 = Math.Min(x + width, barTex.Width);

            if (x1 <= x0)
                return;

            spriteBatch.Draw(
                barTex,
                screenPos,
                new Rectangle(x0, 0, x1 - x0, barTex.Height),
                color,
                0f,
                barOrigin - new Vector2(x0, 0f),
                barScale,
                SpriteEffects.None,
                0);
        }

        // light band sweeping across while full, alpha 0 colors add instead of blend
        private static void DrawFullSweep(SpriteBatch spriteBatch, Vector2 screenPos, Vector2 barOrigin, Vector2 barScale)
        {
            float cycle = (Main.GlobalTimeWrappedHourly % SweepSeconds) / SweepSeconds;
            float p = cycle / SweepActive;

            if (p > 1f)
                return;

            p = p * p * (3f - 2f * p);
            float center = MathHelper.Lerp(-SweepBand, 1f + SweepBand, p);
            int bandWidth = (int)(SweepBand * barTex.Width);

            for (int i = -1; i <= 1; i++)
            {
                int x = (int)((center + i * SweepBand - SweepBand * 0.5f) * barTex.Width);
                DrawFillSlice(spriteBatch, screenPos, barOrigin, barScale, x, bandWidth, SweepColor * (i == 0 ? 1f : 0.4f));
            }
        }

        private static void HandleMouseInteraction(
            ParryPlayer modPlayer,
            ShatteredIllusionConfig config,
            Vector2 screenPos,
            Vector2 screenRatioPos,
            float uiScale,
            float uiWidth,
            float uiHeight)
        {
            // mouse is in raw screen px, bring it into the same space the bar is drawn in
            Vector2 mousePos = Main.MouseScreen / Main.UIScale;
            Rectangle barRect = Utils.CenteredRectangle(screenPos, borderTex.Size() * uiScale);

            bool hovering = barRect.Contains(mousePos.ToPoint());

            if (dragging)
            {
                // keep following even if the cursor outruns the bar, and always let go cleanly
                if (!Main.mouseLeft || config.SturdinessBarLocked)
                {
                    dragging = false;
                    config.SaveChanges();
                }
                else
                {
                    Main.LocalPlayer.mouseInterface = true;

                    Vector2 newCorner = mousePos - dragOffset;
                    Vector2 newScreenRatioPos = new Vector2(
                        MathHelper.Clamp(100f * newCorner.X / uiWidth, 0f, 100f),
                        MathHelper.Clamp(100f * newCorner.Y / uiHeight, 0f, 100f));

                    Vector2 delta = newScreenRatioPos - screenRatioPos;
                    if (Math.Abs(delta.X) >= MouseDragEpsilon || Math.Abs(delta.Y) >= MouseDragEpsilon)
                    {
                        config.SturdinessBarPosX = newScreenRatioPos.X;
                        config.SturdinessBarPosY = newScreenRatioPos.Y;
                    }
                }
            }
            else if (hovering && !config.SturdinessBarLocked && Main.mouseLeft && Main.mouseLeftRelease)
            {
                dragging = true;
                dragOffset = mousePos - screenPos;
            }

            if (!hovering && !dragging)
                return;

            if (!config.SturdinessBarLocked)
                Main.LocalPlayer.mouseInterface = true;

            Main.instance.MouseText($"Sturdiness: {(int)Math.Round(modPlayer.DisplaySturdinessMeter)}/{ParryPlayer.MaxSturdinessMeter}");
        }
    }
}