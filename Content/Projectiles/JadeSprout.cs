using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace ShatteredIllusion.Content.Projectiles
{
    public class JadeSprout : ModProjectile
    {
        public override void SetDefaults()
        {
            Projectile.width = 32;
            Projectile.height = 48;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 300;
            Projectile.penetrate = 3;
            Projectile.scale = 1.5f;
        }

        public override void AI()
        {
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            if (Projectile.velocity.Y < 16f)
            {
                Projectile.velocity.Y += 0.4f;
            }

            if (Main.rand.NextBool(3))
            {
                Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.GemEmerald, 0f, -1f);
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity)
        {
            SoundEngine.PlaySound(SoundID.Item27, Projectile.position);

            return base.OnTileCollide(oldVelocity);
        }
    }
}