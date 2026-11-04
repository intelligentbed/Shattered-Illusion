using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
using ShatteredIllusion.Common.Systems;

namespace ShatteredIllusion.Common.Utilities
{
    // SpriteBatch helpers. World-space overloads subtract Main.screenPosition for you.
    public static class DrawUtils
    {
        // ---- batch state switching ----

        // using (DrawUtils.Additive(sb)) { ...draw glows... }   // ends the current batch, begins additive, restores on dispose
        public static BatchScope Additive(SpriteBatch sb) => new BatchScope(sb, BlendState.Additive);
        public static BatchScope Alpha(SpriteBatch sb) => new BatchScope(sb, BlendState.AlphaBlend);
        public static BatchScope NonPremultiplied(SpriteBatch sb) => new BatchScope(sb, BlendState.NonPremultiplied);
        public static BatchScope With(SpriteBatch sb, BlendState blend, Effect effect = null, SamplerState sampler = null) =>
            new BatchScope(sb, blend, effect, sampler);

        public readonly struct BatchScope : IDisposable
        {
            private readonly SpriteBatch sb;

            public BatchScope(SpriteBatch spriteBatch, BlendState blend, Effect effect = null, SamplerState sampler = null)
            {
                sb = spriteBatch;
                sb.End();
                sb.Begin(SpriteSortMode.Deferred, blend, sampler ?? Main.DefaultSamplerState, DepthStencilState.None,
                    Main.Rasterizer, effect, Main.GameViewMatrix.TransformationMatrix);
            }

            // Returns to the batch state vanilla uses for world drawing.
            public void Dispose()
            {
                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None,
                    Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            }
        }

        // ---- textures ----

        public static Texture2D Pixel => TextureAssets.MagicPixel.Value;

        // Load a texture by mod path once and keep it ("Content/Foo/Bar" under ShatteredIllusion).
        public static Texture2D Tex(string modPath) =>
            ModContent.Request<Texture2D>("ShatteredIllusion/" + modPath, AssetRequestMode.ImmediateLoad).Value;

        // ---- primitives (all WORLD space) ----

        public static void Line(SpriteBatch sb, Vector2 a, Vector2 b, Color color, float thickness = 2f)
        {
            Vector2 d = b - a;
            sb.Draw(Pixel, a - Main.screenPosition, new Rectangle(0, 0, 1, 1), color, d.ToRotation(),
                new Vector2(0f, 0.5f), new Vector2(d.Length(), thickness), SpriteEffects.None, 0f);
        }

        public static void Rect(SpriteBatch sb, Rectangle world, Color color) =>
            sb.Draw(Pixel, new Rectangle(world.X - (int)Main.screenPosition.X, world.Y - (int)Main.screenPosition.Y, world.Width, world.Height), color);

        public static void RectOutline(SpriteBatch sb, Rectangle world, Color color, float thickness = 2f)
        {
            Vector2 tl = world.TopLeft(), tr = world.TopRight(), bl = world.BottomLeft(), br = world.BottomRight();
            Line(sb, tl, tr, color, thickness);
            Line(sb, tr, br, color, thickness);
            Line(sb, br, bl, color, thickness);
            Line(sb, bl, tl, color, thickness);
        }

        public static void CircleOutline(SpriteBatch sb, Vector2 center, float radius, Color color, float thickness = 2f, int segments = 32)
        {
            Vector2 prev = center + new Vector2(radius, 0f);
            for (int i = 1; i <= segments; i++)
            {
                Vector2 next = center + (MathHelper.TwoPi * i / segments).ToRotationVector2() * radius;
                Line(sb, prev, next, color, thickness);
                prev = next;
            }
        }

        // Pie-slice outline for cone telegraphs: two edges plus the arc.
        public static void ConeOutline(SpriteBatch sb, Vector2 origin, Vector2 dir, float halfAngle, float range, Color color, float thickness = 2f, int segments = 16)
        {
            float baseAngle = dir.ToRotation();
            Vector2 prev = origin + (baseAngle - halfAngle).ToRotationVector2() * range;
            Line(sb, origin, prev, color, thickness);
            for (int i = 1; i <= segments; i++)
            {
                float a = baseAngle - halfAngle + halfAngle * 2f * i / segments;
                Vector2 next = origin + a.ToRotationVector2() * range;
                Line(sb, prev, next, color, thickness);
                prev = next;
            }
            Line(sb, prev, origin, color, thickness);
        }

        public static void Bezier(SpriteBatch sb, Vector2 a, Vector2 control, Vector2 b, Color color, float thickness = 2f, int segments = 20)
        {
            Vector2 prev = a;
            for (int i = 1; i <= segments; i++)
            {
                Vector2 next = MathX.Bezier(a, control, b, i / (float)segments);
                Line(sb, prev, next, color, thickness);
                prev = next;
            }
        }

        // Soft glow (WeaponFX.SoftGlow) in WORLD space. diameter in pixels. Pass an additive color (alpha 0) inside an Additive scope.
        public static void Glow(SpriteBatch sb, Vector2 world, Color color, float diameter)
        {
            Texture2D tex = WeaponFX.SoftGlow;
            sb.Draw(tex, world - Main.screenPosition, null, color, 0f, tex.Size() * 0.5f, diameter / tex.Width, SpriteEffects.None, 0f);
        }

        // ---- entity drawing ----

        // Draw an arbitrary texture centered at a world position.
        public static void Centered(SpriteBatch sb, Texture2D tex, Vector2 world, Color color, float rotation = 0f, float scale = 1f,
            SpriteEffects fx = SpriteEffects.None, Rectangle? frame = null)
        {
            Vector2 origin = frame.HasValue ? frame.Value.Size() * 0.5f : tex.Size() * 0.5f;
            sb.Draw(tex, world - Main.screenPosition, frame, color, rotation, origin, scale, fx, 0f);
        }

        // Ghosted copies along a projectile's oldPos (needs TrailCacheLength + TrailingMode set in SetStaticDefaults).
        public static void Afterimages(SpriteBatch sb, Projectile p, Texture2D tex, Color color, float startOpacity = 0.6f, bool shrink = true)
        {
            Vector2 origin = tex.Size() * 0.5f;
            int n = p.oldPos.Length;
            for (int i = n - 1; i >= 0; i--)
            {
                if (p.oldPos[i] == Vector2.Zero)
                    continue;
                float k = 1f - i / (float)n;
                Vector2 pos = p.oldPos[i] + p.Size * 0.5f - Main.screenPosition;
                float rot = p.oldRot[i];
                sb.Draw(tex, pos, null, color * (k * startOpacity), rot, origin, p.scale * (shrink ? 0.5f + 0.5f * k : 1f), SpriteEffects.None, 0f);
            }
        }

        // Same for NPCs (needs NPCID.Sets.TrailCacheLength).
        public static void Afterimages(SpriteBatch sb, NPC npc, Texture2D tex, Color color, Rectangle? frame = null, float startOpacity = 0.5f)
        {
            Vector2 origin = frame.HasValue ? frame.Value.Size() * 0.5f : tex.Size() * 0.5f;
            SpriteEffects fx = npc.spriteDirection == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            int n = npc.oldPos.Length;
            for (int i = n - 1; i >= 0; i--)
            {
                if (npc.oldPos[i] == Vector2.Zero)
                    continue;
                float k = 1f - i / (float)n;
                sb.Draw(tex, npc.oldPos[i] + npc.Size * 0.5f - Main.screenPosition, frame, color * (k * startOpacity), npc.oldRot[i], origin, npc.scale, fx, 0f);
            }
        }

        // ---- color ----

        // Premultiplied "adds light" color: alpha 0 with the given opacity folded into RGB.
        public static Color Glowing(Color c, float opacity = 1f) => WeaponFX.Additive(c, opacity);

        // Evenly blend through a list of colors with t in 0..1.
        public static Color Gradient(float t, params Color[] stops)
        {
            if (stops.Length == 0) return Color.White;
            if (stops.Length == 1) return stops[0];
            t = Ease.Clamp01(t) * (stops.Length - 1);
            int i = Math.Min((int)t, stops.Length - 2);
            return Color.Lerp(stops[i], stops[i + 1], t - i);
        }

        public static Color Rainbow(float t, float saturation = 1f, float lightness = 0.6f) => Main.hslToRgb(t - MathF.Floor(t), saturation, lightness);
    }
}
