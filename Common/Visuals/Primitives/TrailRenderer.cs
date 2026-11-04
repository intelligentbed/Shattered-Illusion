using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ShatteredIllusion.Common.Utilities;
using Terraria;
using Terraria.ModLoader;

namespace ShatteredIllusion.Common.Visuals.Primitives
{
    // Ribbon trails drawn as a triangle strip with BasicEffect (no custom shader needed).
    // Call with the SpriteBatch ended: wrap in `using (DrawUtils.Additive(sb)) { }` is NOT enough (it begins a batch); end the batch, draw, then Begin again.
    //   // in PreDraw, after pushing points into a PosHistory:
    //   TrailRenderer.Draw(history, t => 18f * (1f - t), t => Color.Orange * (1f - t), additive: true);
    // t runs 0 (newest/head) -> 1 (oldest/tail). Positions are WORLD space.
    public sealed class TrailRenderer : ModSystem
    {
        private static BasicEffect effect;
        private static VertexPositionColorTexture[] verts = new VertexPositionColorTexture[256];

        public override void Unload()
        {
            BasicEffect fx = effect;
            effect = null;
            verts = null;
            if (fx != null)
                Main.QueueMainThreadAction(() => fx.Dispose());
        }

        public static void Draw(PosHistory points, Func<float, float> width, Func<float, Color> color, bool additive = true, Texture2D texture = null)
        {
            int n = points.Count;
            if (n < 2 || Main.dedServ)
                return;

            Span<Vector2> buf = n <= 64 ? stackalloc Vector2[n] : new Vector2[n];
            for (int i = 0; i < n; i++)
                buf[i] = points[i];

            DrawStrip(buf, width, color, additive, texture);
        }

        public static void Draw(Projectile p, Func<float, float> width, Func<float, Color> color, bool additive = true, Texture2D texture = null)
        {
            int n = 0;
            Vector2 half = p.Size * 0.5f;
            Span<Vector2> buf = stackalloc Vector2[p.oldPos.Length + 1];
            buf[n++] = p.Center;
            for (int i = 0; i < p.oldPos.Length; i++)
            {
                if (p.oldPos[i] == Vector2.Zero)
                    break;
                buf[n++] = p.oldPos[i] + half;
            }
            if (n < 2)
                return;
            DrawStrip(buf.Slice(0, n), width, color, additive, texture);
        }

        private static void DrawStrip(ReadOnlySpan<Vector2> pts, Func<float, float> width, Func<float, Color> color, bool additive, Texture2D texture)
        {
            int n = pts.Length;
            int needed = n * 2;
            if (verts.Length < needed)
                Array.Resize(ref verts, needed * 2);

            GraphicsDevice gd = Main.graphics.GraphicsDevice;
            if (effect == null || effect.IsDisposed)
                effect = new BasicEffect(gd) { VertexColorEnabled = true };

            for (int i = 0; i < n; i++)
            {
                float t = i / (float)(n - 1);
                Vector2 dir = i == 0 ? pts[1] - pts[0] : i == n - 1 ? pts[i] - pts[i - 1] : pts[i + 1] - pts[i - 1];
                Vector2 normal = dir.SafeNormalize(Vector2.UnitX).Perp();
                float w = width(t) * 0.5f;
                Color c = color(t);
                if (additive)
                    c.A = 0;

                Vector2 screen = pts[i] - Main.screenPosition;
                verts[i * 2] = new VertexPositionColorTexture(new Vector3(screen + normal * w, 0f), c, new Vector2(t, 0f));
                verts[i * 2 + 1] = new VertexPositionColorTexture(new Vector3(screen - normal * w, 0f), c, new Vector2(t, 1f));
            }

            Viewport vp = gd.Viewport;
            effect.World = Main.GameViewMatrix.TransformationMatrix;
            effect.View = Matrix.Identity;
            effect.Projection = Matrix.CreateOrthographicOffCenter(0f, vp.Width, vp.Height, 0f, -1f, 1f);
            effect.TextureEnabled = texture != null;
            effect.Texture = texture;

            // The strip needs its own draw state; the caller must have ended its SpriteBatch (use DrawUtils.With / a BatchScope).
            BlendState oldBlend = gd.BlendState;
            RasterizerState oldRaster = gd.RasterizerState;
            gd.BlendState = additive ? BlendState.Additive : BlendState.AlphaBlend;
            gd.RasterizerState = RasterizerState.CullNone;
            gd.SamplerStates[0] = SamplerState.LinearClamp;

            effect.CurrentTechnique.Passes[0].Apply();
            gd.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, needed - 2);

            gd.BlendState = oldBlend;
            gd.RasterizerState = oldRaster;
        }
    }
}
