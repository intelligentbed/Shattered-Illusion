using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ShatteredIllusion.Common.Systems;
using Terraria;
using Terraria.ModLoader;

namespace ShatteredIllusion.Common.Visuals.Metaballs
{
    // Gooey merging blobs without a custom shader.
    //
    // How it works: every blob is a soft radial gradient drawn ADDITIVELY into a (half-resolution) render target, so
    // nearby blobs add up and bridge the gap between them. The target is then drawn with an AlphaTestEffect, which throws
    // away everything under a threshold. What survives is a hard-edged shape that merges and splits like liquid.
    // Three thresholds give an outline / body / highlight look.
    //
    // Use:
    //   public static readonly MetaballLayer Sand = MetaballSystem.Register("Sand", new Color(150, 105, 55), new Color(235, 185, 105), new Color(255, 235, 175));
    //   Sand.Add(Projectile.Center, 22f);                                  // every AI tick the blob should exist
    //   Sand.Spawn(pos, vel, radius: 10f, life: 40, gravity: 0.25f);       // free-flying droplet that manages itself
    //
    // Layers draw after tiles (behind NPCs/projectiles) automatically. Nothing runs while a layer is empty.
    public sealed class MetaballLayer
    {
        public readonly string Name;
        public Color Outline, Body, Highlight;
        public float Opacity = 1f;
        // Alpha thresholds (0..1) for outline / body / highlight. Higher = tighter shape.
        public float OutlineThreshold = 0.28f, BodyThreshold = 0.5f, HighlightThreshold = 0.82f;
        // Screen pixels the highlight is nudged up, for a lit-from-above look. 0 = flat.
        public float HighlightLift = 2f;
        // Render target scale. 0.5 = quarter the pixels and soft edges, 1 = crisp.
        public float Resolution = 0.5f;
        public bool Enabled = true;

        internal struct Blob { public Vector2 Pos; public float Radius; }

        internal struct Droplet
        {
            public Vector2 Pos, Vel;
            public float Radius, StartRadius, Gravity, Drag;
            public int Life, MaxLife;
            public bool Stuck, Collide;
        }

        internal List<Blob> Pending = new(64);
        internal List<Blob> Current = new(64);
        internal Droplet[] Drops = new Droplet[96];
        internal int DropCount;
        internal RenderTarget2D Target;
        internal Vector2 TargetScreenPos;
        internal bool TargetValid;

        internal MetaballLayer(string name) { Name = name; }

        // Register a blob for the next draw. Call once per AI tick for every live blob (it is NOT remembered).
        public void Add(Vector2 worldCenter, float radius)
        {
            if (Main.dedServ || !Enabled || radius <= 0.5f)
                return;
            Pending.Add(new Blob { Pos = worldCenter, Radius = radius });
        }

        // A droplet that flies, falls, optionally splats on tiles, then shrinks away by itself.
        public void Spawn(Vector2 pos, Vector2 vel, float radius, int life, float gravity = 0.25f, float drag = 0.99f, bool tileCollide = true)
        {
            if (Main.dedServ || !Enabled)
                return;

            int slot = DropCount < Drops.Length ? DropCount++ : 0; // full: overwrite the oldest-ish slot
            Drops[slot] = new Droplet
            {
                Pos = pos, Vel = vel, Radius = radius, StartRadius = radius, Gravity = gravity, Drag = drag,
                Life = life, MaxLife = life, Collide = tileCollide
            };
        }

        public void Burst(Vector2 pos, int count, float speedMin, float speedMax, float radiusMin, float radiusMax, int life,
            Vector2 baseVelocity = default, float gravity = 0.25f)
        {
            for (int i = 0; i < count; i++)
            {
                Vector2 v = Main.rand.NextVector2Unit() * Main.rand.NextFloat(speedMin, speedMax) + baseVelocity;
                Spawn(pos, v, Main.rand.NextFloat(radiusMin, radiusMax), life + Main.rand.Next(-8, 9), gravity);
            }
        }

        internal bool HasContent => Current.Count > 0;

        internal void TickDrops()
        {
            for (int i = DropCount - 1; i >= 0; i--)
            {
                ref Droplet d = ref Drops[i];
                d.Life--;
                if (d.Life <= 0 || d.Radius < 0.6f)
                {
                    Drops[i] = Drops[--DropCount];
                    continue;
                }

                float life01 = d.Life / (float)d.MaxLife;
                if (!d.Stuck)
                {
                    d.Vel.Y += d.Gravity;
                    d.Vel *= d.Drag;
                    if (d.Vel.Y > 14f) d.Vel.Y = 14f;

                    Vector2 next = d.Pos + d.Vel;
                    if (d.Collide && Collision.SolidCollision(next - new Vector2(2f), 4, 4))
                    {
                        d.Stuck = true;
                        d.Vel = Vector2.Zero;
                        d.Life = Math.Min(d.Life, 28); // splat then fade
                        d.MaxLife = Math.Max(d.Life, 1);
                    }
                    else d.Pos = next;
                }

                // Shrink over the last half of life; stuck droplets flatten toward their own radius.
                d.Radius = d.StartRadius * (d.Stuck ? Math.Min(1f, life01 * 1.4f) : Math.Min(1f, life01 * 2f));
                Add(d.Pos, d.Radius);
            }
        }
    }

    public sealed class MetaballSystem : ModSystem
    {
        private static readonly List<MetaballLayer> layers = new();
        private static Texture2D blobTexture;
        private static AlphaTestEffect alphaTest;
        private static BlendState additiveAlpha;

        public static MetaballLayer Register(string name, Color outline, Color body, Color highlight)
        {
            var layer = new MetaballLayer(name) { Outline = outline, Body = body, Highlight = highlight };
            layers.Add(layer);
            return layer;
        }

        public override void OnModLoad()
        {
            if (Main.dedServ)
                return;
            Main.OnPreDraw += PreDraw;
        }

        public override void Unload()
        {
            if (!Main.dedServ)
                Main.OnPreDraw -= PreDraw;

            var targets = new List<RenderTarget2D>();
            foreach (var l in layers)
                if (l.Target != null) targets.Add(l.Target);
            Texture2D tex = blobTexture;
            AlphaTestEffect fx = alphaTest;
            layers.Clear();
            blobTexture = null;
            alphaTest = null;

            Main.QueueMainThreadAction(() =>
            {
                foreach (var t in targets) t.Dispose();
                tex?.Dispose();
                fx?.Dispose();
            });
        }

        public override void PostUpdateEverything()
        {
            // Droplets add themselves to Pending, then Pending becomes what draws until the next update.
            foreach (var l in layers)
            {
                l.TickDrops();
                (l.Current, l.Pending) = (l.Pending, l.Current);
                l.Pending.Clear();
            }
        }

        public override void OnWorldUnload()
        {
            foreach (var l in layers)
            {
                l.DropCount = 0;
                l.Pending.Clear();
                l.Current.Clear();
            }
        }

        // ---------------- rendering ----------------

        // Soft falloff that saturates quickly so the interior of a blob is fully opaque once thresholded.
        private static Texture2D BlobTexture(GraphicsDevice gd)
        {
            if (blobTexture != null && !blobTexture.IsDisposed)
                return blobTexture;

            const int size = 64;
            blobTexture = new Texture2D(gd, size, size);
            var data = new Color[size * size];
            float c = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c)) / c;
                    float a = MathHelper.Clamp(1f - d, 0f, 1f);
                    a = a * a * (3f - 2f * a);          // smoothstep falloff
                    data[y * size + x] = Color.White * a; // premultiplied
                }
            }
            blobTexture.SetData(data);
            return blobTexture;
        }

        private static void PreDraw(GameTime _)
        {
            if (Main.gameMenu || Main.dedServ)
                return;

            GraphicsDevice gd = Main.graphics.GraphicsDevice;

            foreach (var layer in layers)
            {
                layer.TargetValid = false;
                if (!layer.Enabled || !layer.HasContent)
                    continue;

                int w = Math.Max(16, (int)(Main.screenWidth * layer.Resolution));
                int h = Math.Max(16, (int)(Main.screenHeight * layer.Resolution));
                if (layer.Target == null || layer.Target.IsDisposed || layer.Target.Width != w || layer.Target.Height != h)
                {
                    layer.Target?.Dispose();
                    layer.Target = new RenderTarget2D(gd, w, h, false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
                }

                RenderTargetBinding[] previous = gd.GetRenderTargets();
                gd.SetRenderTarget(layer.Target);
                gd.Clear(Color.Transparent);

                Texture2D blob = BlobTexture(gd);
                Vector2 origin = blob.Size() * 0.5f;
                Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);

                Vector2 screenPos = Main.screenPosition;
                foreach (var b in layer.Current)
                {
                    Vector2 s = (b.Pos - screenPos) * layer.Resolution;
                    float pad = b.Radius * 2f * layer.Resolution;
                    if (s.X < -pad || s.Y < -pad || s.X > w + pad || s.Y > h + pad)
                        continue;
                    // The gradient reaches 0 at the texture edge, so diameter on screen = 2 * radius * 1.25 gives a visible radius ~= radius.
                    float scale = b.Radius * 2.5f * layer.Resolution / blob.Width;
                    Main.spriteBatch.Draw(blob, s, null, Color.White, 0f, origin, scale, SpriteEffects.None, 0f);
                }

                Main.spriteBatch.End();
                gd.SetRenderTargets(previous);

                layer.TargetScreenPos = screenPos;
                layer.TargetValid = true;
            }
        }

        // Draw after tiles, behind NPCs and projectiles.
        public override void PostDrawTiles()
        {
            foreach (var layer in layers)
            {
                if (layer.TargetValid)
                    Composite(Main.spriteBatch, layer);
            }
        }

        // Draw a layer yourself (e.g. in front of an NPC from its PostDraw). Call with the SpriteBatch NOT begun.
        public static void Composite(SpriteBatch sb, MetaballLayer layer)
        {
            if (!layer.TargetValid || layer.Target == null || layer.Target.IsDisposed)
                return;

            GraphicsDevice gd = Main.graphics.GraphicsDevice;
            Viewport vp = gd.Viewport;

            alphaTest ??= new AlphaTestEffect(gd) { VertexColorEnabled = true };
            alphaTest.World = Main.GameViewMatrix.TransformationMatrix;
            alphaTest.View = Matrix.Identity;
            alphaTest.Projection = Matrix.CreateOrthographicOffCenter(0f, vp.Width, vp.Height, 0f, 0f, 1f);
            alphaTest.AlphaFunction = CompareFunction.Greater;

            // The target was rendered at an earlier screenPosition; slide it by the difference so blobs never jitter with the camera.
            Vector2 offset = layer.TargetScreenPos - Main.screenPosition;
            float inv = 1f / layer.Resolution;

            Pass(sb, layer, layer.OutlineThreshold, layer.Outline, offset, inv, 0f);
            Pass(sb, layer, layer.BodyThreshold, layer.Body, offset, inv, 0f);
            Pass(sb, layer, layer.HighlightThreshold, layer.Highlight, offset, inv, -layer.HighlightLift);
        }

        private static void Pass(SpriteBatch sb, MetaballLayer layer, float threshold, Color color, Vector2 offset, float inv, float lift)
        {
            alphaTest.ReferenceAlpha = (int)MathHelper.Clamp(threshold * 255f, 1f, 254f);

            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, alphaTest);
            // Two draws: survivors of the alpha test keep their (>= threshold) alpha, so stacking makes the rim opaque.
            Color c = color * layer.Opacity;
            sb.Draw(layer.Target, offset + new Vector2(0f, lift), null, c, 0f, Vector2.Zero, inv, SpriteEffects.None, 0f);
            sb.Draw(layer.Target, offset + new Vector2(0f, lift), null, c, 0f, Vector2.Zero, inv, SpriteEffects.None, 0f);
            sb.End();
        }
    }
}
