using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ShatteredIllusion.Content.Projectiles
{
    public class JadeSprout : ModProjectile
    {
        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 24;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee; 
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 300;
            Projectile.penetrate = 3;
        }

        public override void AI()
        {
            if (Projectile.velocity.Y < 16f)
            {
                Projectile.velocity.Y += 0.4f;
            }

            if (Main.rand.NextBool(3))
            {
                Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.GemEmerald, 0f, -1f);
            }
        }
    }
}