using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace ShatteredIllusion.Common.Visuals.ThreeD
{
    // One projectile for a whole 3D shape: one AI, one broadphase hit test, one draw. Replaces spawning dozens of ThreeDProjectiles.
    public abstract class ShapeProjectile : ModProjectile
    {
        public Body3 Body;
        public Shape3D? Shape;

        public ref Vector3 Pos3 => ref Body.Pos;
        public ref Vector3 Vel3 => ref Body.Vel;
        public ref Vector3 Accel3 => ref Body.Accel;

        public float HitDepth = 80f;

        public abstract Shape3D BuildShape();

        public virtual void ShapeSetDefaults() { }

        public virtual void ShapeAI() { }

        private bool initialized;

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

            ShapeSetDefaults();
        }

        public override void OnSpawn(IEntitySource source) => Initialize();

        public override void OnKill(int timeLeft)
        {
            if (Shape == null)
                return;

            Shape.Kill();
            ThreeDRenderer.Remove(Shape);
        }

        private void Initialize()
        {
            if (initialized)
                return;

            initialized = true;
            Body = new Body3(ThreeDMath.ToVector3(Projectile.Center, 0f));

            if (Main.dedServ)
                return;

            Shape = BuildShape();
            Shape.HitDepth = HitDepth;
            Shape.Position = Body.Pos;
            Shape.OwnerProjectile = Projectile;
            Shape.OwnerMod = this;
            ThreeDRenderer.Add(Shape);
        }

        public sealed override void AI()
        {
            Initialize();

            Body.Begin();
            ShapeAI();
            Body.Integrate();
            Projectile.velocity = Vector2.Zero;
            Projectile.Center = ThreeDMath.Project(Body.Pos);

            if (Shape == null)
                return;

            Shape.HitDepth = HitDepth;
            Shape.Position = Body.Pos;
            Shape.Opacity = Projectile.Opacity;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            if (Shape == null || !Body.InHitDepth(HitDepth))
                return false;

            return Shape.OverlapsSwept(targetHitbox, Body.Prev);
        }

        public override bool PreDraw(ref Color lightColor) => false;

        public static T? Spawn<T>(IEntitySource source, Vector3 position, Vector3 velocity, int damage, float knockback, int owner = -1, Action<T>? init = null) where T : ShapeProjectile
        {
            position.Z = ThreeDMath.ClampZ(position.Z);

            int type = ModContent.ProjectileType<T>();
            int index = Projectile.NewProjectile(source, position.XY(), Vector2.Zero, type, damage, knockback, owner == -1 ? Main.myPlayer : owner);

            if (index < 0 || index >= Main.maxProjectiles)
                return null;

            if (Main.projectile[index].ModProjectile is not T proj)
                return null;

            proj.Initialize();
            proj.Body.Pos = position;
            proj.Body.Vel = velocity;

            init?.Invoke(proj);

            proj.Body.Pos.Z = ThreeDMath.ClampZ(proj.Body.Pos.Z);
            proj.Body.Prev = proj.Body.Pos;
            proj.Projectile.Center = ThreeDMath.Project(proj.Body.Pos);

            if (proj.Shape != null)
                proj.Shape.Position = proj.Body.Pos;

            return proj;
        }
    }
}
