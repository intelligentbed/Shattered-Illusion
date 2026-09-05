using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ShatteredIllusion.Content.Projectiles.Ranger
{
    internal class MandibleShooterBullet : ModProjectile
    {
        private const int TravelTime = 45;
        private const float MaxSpread = 90f;

        public Vector2 Start;
        public Vector2 Target;

        private float Side => Projectile.ai[0];

        public override void SetDefaults()
        {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 1;
            Projectile.timeLeft = TravelTime + 10;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI()
        {
            if (Projectile.ai[1] == 0f && Start == Target)
            {
                Start = Projectile.Center;
                Target = Projectile.Center + Vector2.UnitX * 200f;
            }

            float t = MathHelper.Clamp(Projectile.ai[1] / TravelTime, 0f, 1f);

            Vector2 toTarget = Target - Start;
            Vector2 forward = toTarget.SafeNormalize(Vector2.UnitX);
            Vector2 right = forward.RotatedBy(MathHelper.PiOver2);

            Vector2 basePos = Vector2.Lerp(Start, Target, t);

            float amplitude = MaxSpread * (float)System.Math.Sin(t * MathHelper.Pi);
            Vector2 offset = right * amplitude * Side;

            Vector2 newPos = basePos + offset;

            Vector2 delta = newPos - Projectile.Center;
            if (delta != Vector2.Zero)
                Projectile.rotation = delta.ToRotation();

            Projectile.Center = newPos;
            Projectile.velocity = Vector2.Zero;

            // dust
            Dust trail = Dust.NewDustPerfect(Projectile.Center, DustID.Sandstorm, delta * 0.1f, 0, default, 1.1f);
            trail.noGravity = true;
            trail.fadeIn = 0.5f;

            Projectile.ai[1] += 1f;

            if (t >= 1f)
                Projectile.Kill();
        }

        public override void SendExtraAI(BinaryWriter writer)
        {
            writer.Write(Start.X);
            writer.Write(Start.Y);
            writer.Write(Target.X);
            writer.Write(Target.Y);
        }

        public override void ReceiveExtraAI(BinaryReader reader)
        {
            Start = new Vector2(reader.ReadSingle(), reader.ReadSingle());
            Target = new Vector2(reader.ReadSingle(), reader.ReadSingle());
        }

        public override void OnKill(int timeLeft)
        {
            for (int i = 0; i < 12; i++)
            {
                Dust burst = Dust.NewDustPerfect(Projectile.Center, DustID.Sand,
                    Main.rand.NextVector2Circular(3f, 3f), 0, default, 1.3f);
                burst.noGravity = true;
            }
        }
    }
}