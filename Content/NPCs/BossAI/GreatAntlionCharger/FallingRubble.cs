using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace ShatteredIllusion.Content.NPCs.BossAI.GreatAntlionCharger
{
    internal class FallingRubble : ModProjectile
    {
        private ref float State => ref Projectile.ai[0];
        private ref float FallSpeed => ref Projectile.ai[1];

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 5;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 0;
        }

        public override void SetDefaults()
        {
            Projectile.width = 36;
            Projectile.height = 36;
            Projectile.hostile = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 600;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
        }

        public override void AI()
        {
            if (State == 0f)
            {
                Projectile.rotation = Main.rand.NextFloat(MathHelper.TwoPi);
                Projectile.scale = Main.rand.NextFloat(1.5f, 2.0f);
                State = 1f;
            }

            Projectile.velocity.Y = Math.Min(Projectile.velocity.Y + 0.4f, 14f);
            Projectile.velocity.X *= 0.98f;
            Projectile.rotation += Projectile.velocity.X * 0.05f + Math.Sign(Projectile.velocity.Y) * 0.05f;

            if (Main.rand.NextBool(2))
            {
                Dust dust = Dust.NewDustDirect(
                    Projectile.position,
                    Projectile.width,
                    Projectile.height,
                    DustID.Dirt,
                    Projectile.velocity.X * 0.2f,
                    Projectile.velocity.Y * 0.2f,
                    100,
                    default,
                    0.8f
                );

                dust.noGravity = true;
            }
        }

        public override void OnKill(int timeLeft)
        {
            SoundEngine.PlaySound(SoundID.Dig, Projectile.position);

            for (int i = 0; i < 15; i++)
            {
                Vector2 dustVelocity = Main.rand.NextVector2Circular(4f, 4f);

                Dust.NewDust(
                    Projectile.position,
                    Projectile.width,
                    Projectile.height,
                    DustID.Dirt,
                    dustVelocity.X,
                    dustVelocity.Y,
                    100,
                    default,
                    1.2f
                );
            }

            for (int i = 0; i < 3; i++)
            {
                Vector2 goreVelocity = new Vector2(
                    Main.rand.NextFloat(-3f, 3f),
                    Main.rand.NextFloat(-4f, -1f)
                );

                Gore.NewGore(
                    Projectile.GetSource_Death(),
                    Projectile.position,
                    goreVelocity,
                    GoreID.Smoke1
                );
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity)
        {
            return true;
        }
    }
}