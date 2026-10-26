using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ParticleLibrary.Core.V3.Particles;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace ShatteredIllusion.Common.Visuals.ThreeD
{
    // One object, many points: a whole 3D shape drawn, culled, sorted and hit-tested as a single unit.
    public sealed class Shape3D : IThreeDDrawable
    {
        public struct Element
        {
            public Vector3 Local;
            public float Size;
            public float Rotation;
            public Color Color;
        }

        public Texture2D Texture;
        public Rectangle? Frame;

        // Draw through ParticleLibrary's instanced pipeline: no SpriteBatch vertex building, whole texture only, world space only.
        public bool Instanced;
        public bool Additive = true;
        public bool Sorted = true;

        public float Opacity = 1f;
        public float HitDepth = 80f;
        public float HitScale = 0.6f;
        public float? LayerDepthOverride;

        // Standalone-visual controls, only used when AutoTick is on (ThreeDRenderer.Spawn turns it on).
        public bool AutoTick;
        public Vector3 Velocity;
        public Vector3 Spin;
        public int Life = -1;
        public Entity Anchor;

        private Element[] elements;
        private Vector3[] world;
        private int[] order;
        private float[] sortKeys;
        private int count;

        private Vector3 position;
        private Quaternion rotation = Quaternion.Identity;
        private float scale = 1f;

        private Matrix basis = Matrix.Identity;
        private bool dirty = true;
        private bool boundsDirty = true;
        private int stamp = -1;
        private float localRadius;
        private float maxSize;
        private bool alive = true;

        public Shape3D(Texture2D texture = null, int capacity = 32)
        {
            Texture = texture;
            capacity = Math.Max(capacity, 4);
            elements = new Element[capacity];
            world = new Vector3[capacity];
            order = new int[capacity];
            sortKeys = new float[capacity];

            for (int i = 0; i < capacity; i++)
                order[i] = i;
        }

        public int Count => count;

        public Vector3 Position
        {
            get => position;
            set { position = value; dirty = true; }
        }

        public Quaternion Rotation
        {
            get => rotation;
            set { rotation = value; dirty = true; }
        }

        public float Scale
        {
            get => scale;
            set { scale = value; dirty = true; boundsDirty = true; }
        }

        public Vector3 Center => Anchor != null ? new Vector3(Anchor.Center.X + position.X, Anchor.Center.Y + position.Y, position.Z) : position;

        // Set by ShapeProjectile so the shape dies with its projectile even if OnKill never ran.
        internal Projectile OwnerProjectile;
        internal ModProjectile OwnerMod;

        public bool Alive => alive && (Anchor == null || Anchor.active) && (OwnerMod == null || (OwnerProjectile.active && OwnerProjectile.ModProjectile == OwnerMod));

        internal bool UsingInstancing;

        public ThreeDLayer Layer
        {
            get
            {
                float z = Center.Z;

                if (z < -(LayerDepthOverride ?? HitDepth))
                    return ThreeDLayer.BehindWorld;

                return z < ThreeDMath.PlayerPlaneZ ? ThreeDLayer.BehindNPCs : ThreeDLayer.OverPlayers;
            }
        }

        public float Depth => Center.Z;

        // Wraps the player's plane, so it gets drawn in both layers (each one only draws its own side).
        internal bool Straddles => Layer != ThreeDLayer.BehindWorld && MathF.Abs(Center.Z - ThreeDMath.PlayerPlaneZ) < Radius;

        private static ThreeDLayer SideOf(float z) => z < ThreeDMath.PlayerPlaneZ ? ThreeDLayer.BehindNPCs : ThreeDLayer.OverPlayers;

        public void Kill() => alive = false;

        public void MarkDirty()
        {
            dirty = true;
            boundsDirty = true;
        }

        public ref Element this[int index] => ref elements[index];

        public void Clear()
        {
            count = 0;

            for (int i = 0; i < order.Length; i++)
                order[i] = i;

            MarkDirty();
        }

        public int Add(Vector3 local, float size, Color color, float rotation = 0f)
        {
            if (count == elements.Length)
            {
                int old = elements.Length;
                int next = old * 2;
                Array.Resize(ref elements, next);
                Array.Resize(ref world, next);
                Array.Resize(ref order, next);
                Array.Resize(ref sortKeys, next);

                for (int i = old; i < next; i++)
                    order[i] = i;
            }

            elements[count] = new Element { Local = local, Size = size, Rotation = rotation, Color = color };
            MarkDirty();
            return count++;
        }

        private float TexW => Frame?.Width ?? Texture?.Width ?? 16;
        private float TexH => Frame?.Height ?? Texture?.Height ?? 16;
        private float Unit => 1f / MathF.Max(TexW, TexH);

        private float Radius
        {
            get
            {
                if (boundsDirty)
                {
                    localRadius = 0f;
                    maxSize = 0f;

                    for (int i = 0; i < count; i++)
                    {
                        localRadius = MathF.Max(localRadius, elements[i].Local.Length());
                        maxSize = MathF.Max(maxSize, elements[i].Size);
                    }

                    boundsDirty = false;
                }

                return (localRadius + maxSize * 0.5f) * scale;
            }
        }

        public void Tick()
        {
            if (!AutoTick)
                return;

            if (Velocity != Vector3.Zero)
                Position = new Vector3(position.X + Velocity.X, position.Y + Velocity.Y, ThreeDMath.ClampZ(position.Z + Velocity.Z));

            if (Spin != Vector3.Zero)
                Rotation = Quaternion.Normalize(Quaternion.Concatenate(rotation, Quaternion.CreateFromYawPitchRoll(Spin.X, Spin.Y, Spin.Z)));

            if (Life > 0 && --Life == 0)
                alive = false;
        }

        // Recomputes world-space points once per game tick (or when something changed), never per draw call.
        private void Refresh()
        {
            int now = (int)Main.GameUpdateCount;

            if (!dirty && stamp == now)
                return;

            stamp = now;
            dirty = false;

            Vector3 center = Center;

            if (rotation == Quaternion.Identity)
            {
                for (int i = 0; i < count; i++)
                    world[i] = center + elements[i].Local * scale;

                return;
            }

            basis = Matrix.CreateScale(scale) * Matrix.CreateFromQuaternion(rotation);

            for (int i = 0; i < count; i++)
                world[i] = center + Vector3.Transform(elements[i].Local, basis);
        }

        // Nearly sorted frame to frame, so insertion sort is close to O(n). A fast-spinning shape can reshuffle
        // most of the order in one tick, so the shift count is capped and the rest falls back to Array.Sort.
        private void SortOrder()
        {
            int budget = count * 4;

            for (int i = 1; i < count; i++)
            {
                int v = order[i];
                float z = world[v].Z;
                int j = i - 1;

                while (j >= 0 && world[order[j]].Z > z)
                {
                    order[j + 1] = order[j];
                    j--;

                    if (--budget < 0)
                    {
                        order[j + 1] = v;
                        SortOrderFallback();
                        return;
                    }
                }

                order[j + 1] = v;
            }
        }

        // order is a valid permutation at any point of the insertion pass, so it can be handed over mid-sort.
        private void SortOrderFallback()
        {
            for (int k = 0; k < count; k++)
                sortKeys[k] = world[order[k]].Z;

            Array.Sort(sortKeys, order, 0, count);
        }

        private bool IsVisible(in View3 view, Rectangle bounds)
        {
            Vector3 c = Center;
            float r = Radius;
            Vector2 p = ThreeDMath.Project(c, view, out _);
            float m = ThreeDMath.ScreenReach(c, r, view.Vanish);

            return p.X + m > bounds.Left && p.X - m < bounds.Right && p.Y + m > bounds.Top && p.Y - m < bounds.Bottom;
        }

        private bool Prepare(in View3 view, Rectangle bounds)
        {
            if (count == 0 || Texture == null || Opacity <= 0f)
                return false;

            Refresh();

            if (!IsVisible(view, bounds))
                return false;

            // Additive blending is order-independent, so sorting would be wasted work.
            if (Sorted && !Additive)
                SortOrder();

            return true;
        }

        public void Draw(SpriteBatch sb, in View3 view, Rectangle bounds) => Draw(sb, view, bounds, null);

        public void Draw(SpriteBatch sb, in View3 view, Rectangle bounds, ThreeDLayer? only)
        {
            if (!Prepare(view, bounds))
                return;

            bool split = only != null && Straddles;

            Rectangle src = Frame ?? Texture.Bounds;
            Vector2 origin = src.Size() * 0.5f;
            float unit = Unit * scale;

            for (int k = 0; k < count; k++)
            {
                int i = order[k];
                ref Element e = ref elements[i];
                Vector3 w = world[i];

                if (split && SideOf(w.Z) != only)
                    continue;

                Vector2 pos = ThreeDMath.Project(w, view, out float s);
                Color color = e.Color * (ThreeDDraw.DepthBrightness(w.Z) * Opacity);

                sb.Draw(Texture, pos, src, color, e.Rotation, origin, e.Size * unit * s, SpriteEffects.None, 0f);
            }
        }

        // Fills instance data for the ParticleLibrary pipeline. Returns how many instances were written.
        internal int WriteInstances(ParticleInstance[] dst, int start, in View3 view, Rectangle bounds, ThreeDLayer? only = null)
        {
            if (start + count > dst.Length || !Prepare(view, bounds))
                return 0;

            bool split = only != null && Straddles;
            float unitX = TexW * Unit * scale;
            float unitY = TexH * Unit * scale;
            Vector2 vanish = view.Vanish;
            int n = 0;

            for (int k = 0; k < count; k++)
            {
                int i = order[k];
                ref Element e = ref elements[i];
                Vector3 w = world[i];

                if (split && SideOf(w.Z) != only)
                    continue;

                Vector2 pos = ThreeDMath.Project(w, vanish, out float s);

                dst[start + n++] = new ParticleInstance
                {
                    Position_Scale = new Vector4(pos.X, pos.Y, e.Size * unitX * s, e.Size * unitY * s),
                    Rotation_Depth = new Vector2(e.Rotation, 1f),
                    Color = e.Color * (ThreeDDraw.DepthBrightness(w.Z) * Opacity)
                };
            }

            return n;
        }

        // Is any point inside the player-plane window and overlapping the target? Cheap rejects first.
        public bool Overlaps(Rectangle target) => OverlapsAt(target, Vector3.Zero);

        // Samples the path from a previous center so fast shapes cannot skip over a target.
        public bool OverlapsSwept(Rectangle target, Vector3 from)
        {
            Vector3 delta = Center - from;
            _ = Radius;
            float step = MathF.Max(8f, maxSize * scale * HitScale * 0.5f);
            int steps = Math.Clamp((int)MathF.Ceiling(delta.Length() / step), 1, 16);

            for (int i = 1; i <= steps; i++)
            {
                if (OverlapsAt(target, -delta * (1f - i / (float)steps)))
                    return true;
            }

            return false;
        }

        private bool OverlapsAt(Rectangle target, Vector3 shift)
        {
            if (count == 0)
                return false;

            Refresh();

            float plane = ThreeDMath.PlayerPlaneZ;
            float r = Radius;
            Vector3 c = Center + shift;

            if (MathF.Abs(c.Z - plane) - r > HitDepth)
                return false;

            Vector2 cam = ThreeDMath.CameraCenter;
            Vector2 pc = ThreeDMath.Project(c, cam, out _);
            float m = ThreeDMath.ScreenReach(c, r, cam);

            if (pc.X + m < target.Left || pc.X - m > target.Right || pc.Y + m < target.Top || pc.Y - m > target.Bottom)
                return false;

            for (int i = 0; i < count; i++)
            {
                Vector3 w = world[i] + shift;

                if (MathF.Abs(w.Z - plane) > HitDepth)
                    continue;

                Vector2 p = ThreeDMath.Project(w, cam, out float s);
                float half = elements[i].Size * scale * s * HitScale * 0.5f;

                if (p.X + half > target.Left && p.X - half < target.Right && p.Y + half > target.Top && p.Y - half < target.Bottom)
                    return true;
            }

            return false;
        }

        public static Texture2D DefaultTexture => TextureAssets.Extra[ExtrasID.ThePerfectGlow].Value;

        public static Shape3D Ring(Texture2D texture, int count, float radius, float size, Color color)
        {
            Shape3D shape = new(texture ?? DefaultTexture, count);

            for (int i = 0; i < count; i++)
            {
                float a = MathHelper.TwoPi * i / count;
                shape.Add(new Vector3(MathF.Cos(a) * radius, MathF.Sin(a) * radius, 0f), size, color);
            }

            return shape;
        }

        public static Shape3D Sphere(Texture2D texture, int count, float radius, float size, Color color)
        {
            Shape3D shape = new(texture ?? DefaultTexture, count);
            const float goldenAngle = 2.3999632f;

            for (int i = 0; i < count; i++)
            {
                float y = 1f - 2f * (i + 0.5f) / count;
                float r = MathF.Sqrt(MathF.Max(0f, 1f - y * y));
                float phi = i * goldenAngle;

                shape.Add(new Vector3(MathF.Cos(phi) * r, y, MathF.Sin(phi) * r) * radius, size, color);
            }

            return shape;
        }

        public static Shape3D Helix(Texture2D texture, int count, float radius, float length, float turns, float size, Color color)
        {
            Shape3D shape = new(texture ?? DefaultTexture, count);

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)count;
                float a = t * turns * MathHelper.TwoPi;

                shape.Add(new Vector3((t - 0.5f) * length, MathF.Cos(a) * radius, MathF.Sin(a) * radius), size, color);
            }

            return shape;
        }

        public static Shape3D Line(Texture2D texture, int count, float length, float size, Color color)
        {
            Shape3D shape = new(texture ?? DefaultTexture, count);

            for (int i = 0; i < count; i++)
                shape.Add(new Vector3((i / (float)Math.Max(1, count - 1) - 0.5f) * length, 0f, 0f), size, color);

            return shape;
        }

        public static Shape3D Wall(Texture2D texture, int columns, int rows, float spacing, float size, Color color)
        {
            Shape3D shape = new(texture ?? DefaultTexture, columns * rows);

            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < columns; x++)
                    shape.Add(new Vector3((x - (columns - 1) * 0.5f) * spacing, (y - (rows - 1) * 0.5f) * spacing, 0f), size, color);
            }

            return shape;
        }
    }
}
