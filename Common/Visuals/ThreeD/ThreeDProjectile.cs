using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace ShatteredIllusion.Common.Visuals.ThreeD
{
    // Base for single-sprite 3D projectiles. For a whole shape (ring, sphere, wall...) use ShapeProjectile: one entity, one draw.
    public abstract class ThreeDProjectile : ModProjectile, IThreeDDrawable
    {
        private const int GroundRecheckTicks = 5;
        private const int GroundSearchTiles = 90;

        public Body3 Body;

        public ref Vector3 Pos3 => ref Body.Pos;
        public ref Vector3 Vel3 => ref Body.Vel;
        public ref Vector3 Accel3 => ref Body.Accel;

        public float DepthScale => ThreeDMath.ScaleAt(Body.Pos.Z);

        public float HitDepth = 80f;
        public float? LayerDepthOverride;

        public float GroundY { get; private set; }

        public bool InHitDepth => Body.InHitDepth(HitDepth);

        public ThreeDLayer Layer => Body.LayerFor(HitDepth, LayerDepthOverride);

        public virtual bool HasShadow => true;
        public virtual bool HasDangerHalo => true;
        public virtual bool AffectedByLighting => false;
        public virtual bool DebugText => false;

        private bool initialized;
        private int groundTimer;
        private bool hasGround;

        public virtual void ThreeDSetDefaults() { }

        public virtual void ThreeDAI() { }

        bool IThreeDDrawable.Alive => Projectile.active && Projectile.ModProjectile == this;

        float IThreeDDrawable.Depth => Body.Pos.Z;

        public sealed override void SetDefaults()
        {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.aiStyle = -1;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            Projectile.hide = true;

            ThreeDSetDefaults();
        }

        public override void OnSpawn(IEntitySource source)
        {
            Initialize();
        }

        public override void OnKill(int timeLeft)
        {
            ThreeDRenderer.Remove(this);
        }

        private void Initialize()
        {
            if (initialized)
                return;

            Body = new Body3(ThreeDMath.ToVector3(Projectile.Center, 0f));
            GroundY = Body.Pos.Y;
            groundTimer = Projectile.whoAmI % GroundRecheckTicks;
            initialized = true;

            ThreeDRenderer.Add(this);
        }

        public sealed override void AI()
        {
            Initialize();

            Body.Begin();
            ThreeDAI();
            Body.Integrate();
            Projectile.velocity = Vector2.Zero;

            ApplyProjection();

            if (HasShadow && Layer != ThreeDLayer.BehindWorld)
                UpdateGround();
            else
                groundTimer = 0;
        }

        private void ApplyProjection()
        {
            Projectile.Center = ThreeDMath.Project(Body.Pos);
        }

        private void UpdateGround(bool force = false)
        {
            if (!force && --groundTimer > 0)
                return;

            groundTimer = GroundRecheckTicks;

            Point tile = Projectile.Center.ToTileCoordinates();
            int bottom = Math.Min(tile.Y + GroundSearchTiles, Main.maxTilesY - 1);

            hasGround = false;

            if (!WorldGen.InWorld(tile.X, tile.Y) || WorldGen.SolidTile(tile.X, tile.Y))
                return;

            for (int y = tile.Y + 1; y <= bottom; y++)
            {
                if (!WorldGen.SolidTile(tile.X, y))
                    continue;

                GroundY = y * 16f;
                hasGround = true;
                return;
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            float low = ThreeDMath.PlayerPlaneZ - HitDepth;
            float high = ThreeDMath.PlayerPlaneZ + HitDepth;
            Vector2 baseSize = new Vector2(Projectile.width, Projectile.height) * Projectile.scale;

            float step = MathF.Max(1f, MathF.Min(baseSize.X, baseSize.Y) * 0.5f);
            int steps = Math.Clamp((int)MathF.Ceiling((Body.Pos - Body.Prev).Length() / step), 1, 32);

            for (int i = 1; i <= steps; i++)
            {
                Vector3 pos = Vector3.Lerp(Body.Prev, Body.Pos, i / (float)steps);

                if (pos.Z < low || pos.Z > high)
                    continue;

                Vector2 center = ThreeDMath.Project(pos, out float scale);

                if (Utils.CenteredRectangle(center, baseSize * scale).Intersects(targetHitbox))
                    return true;
            }

            return false;
        }

        public override bool PreDraw(ref Color lightColor) => false;

        void IThreeDDrawable.Draw(SpriteBatch sb, in View3 view, Rectangle bounds)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;

            Vector2 pos = ThreeDMath.Project(Body.Pos, view, out float depthScale);
            float margin = MathF.Max(texture.Width, texture.Height) * Projectile.scale * depthScale;

            if (pos.X + margin < bounds.Left || pos.X - margin > bounds.Right || pos.Y + margin < bounds.Top || pos.Y - margin > bounds.Bottom)
                return;

            int frameCount = Math.Max(1, Main.projFrames[Projectile.type]);
            Rectangle frame = texture.Frame(1, frameCount, 0, Math.Clamp(Projectile.frame, 0, frameCount - 1));

            if (HasDangerHalo && InHitDepth && Projectile.hostile)
                ThreeDDraw.DrawDangerHalo(sb, view, texture, Body.Pos, Projectile.rotation, Projectile.scale, frame, Projectile.Opacity);

            Color color = ThreeDDraw.DepthTint(Body.Pos.Z);

            if (AffectedByLighting)
                color = color.MultiplyRGBA(Lighting.GetColor(Projectile.Center.ToTileCoordinates()));

            ThreeDDraw.DrawSprite(sb, view, texture, Body.Pos, color * Projectile.Opacity, Projectile.rotation, Projectile.scale, frame);

            if (DebugText)
                Utils.DrawBorderString(sb, $"({Body.Pos.X:0}, {Body.Pos.Y:0}, {Body.Pos.Z:0})", pos + new Vector2(0f, -40f), Color.White, 0.8f, 0.5f);
        }

        void IThreeDDrawable.DrawShadow(SpriteBatch sb)
        {
            if (!HasShadow || !hasGround || Projectile.Opacity <= 0f)
                return;

            Vector2 center = ThreeDMath.Project(Body.Pos, out float scale);
            float height = GroundY - center.Y;

            if (height < 0f)
                return;

            float fade = MathHelper.Clamp(1f - height / 480f, 0f, 1f);
            ThreeDDraw.DrawShadow(sb, new Vector2(center.X, GroundY), Projectile.width * Projectile.scale * scale * 1.2f, 0.4f * fade * Projectile.Opacity);
        }

        public void MoveToward(Vector3 target, float speed) => Body.MoveToward(target, speed);

        public void Homing(Vector3 target, float speed, float turnRate) => Body.Homing(target, speed, turnRate);

        public void Launch(Vector3 direction, float speed) => Body.Launch(direction, speed);

        public void MoveDepth(float speed) => Body.MoveDepth(speed);

        public void Orbit(Vector3 center, float radius, float angularSpeed) => Body.Orbit(center, radius, angularSpeed);

        public void TeleportTo(Vector3 position) => Body.Teleport(position);

        public static T? Spawn<T>(IEntitySource source, Vector3 position, Vector3 velocity, int damage, float knockback, int owner = -1, Action<T>? init = null) where T : ThreeDProjectile
        {
            position.Z = ThreeDMath.ClampZ(position.Z);

            int type = ModContent.ProjectileType<T>();
            int index = Projectile.NewProjectile(source, position.XY(), Vector2.Zero, type, damage, knockback, owner == -1 ? Main.myPlayer : owner);

            if (index < 0 || index >= Main.maxProjectiles)
                return null;

            if (Main.projectile[index].ModProjectile is not T threeD)
                return null;

            threeD.Initialize();
            threeD.Body.Pos = position;
            threeD.Body.Vel = velocity;

            init?.Invoke(threeD);

            threeD.Body.Pos.Z = ThreeDMath.ClampZ(threeD.Body.Pos.Z);
            threeD.Body.Prev = threeD.Body.Pos;

            threeD.ApplyProjection();

            if (threeD.HasShadow)
                threeD.UpdateGround(force: true);

            return threeD;
        }
    }
}
