using Microsoft.Xna.Framework;
using ShatteredIllusion.Content.Dusts;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace ShatteredIllusion.Content.Projectiles.Mage
{
    internal class StarryProjectile : ModProjectile
    {
        private bool IsMiniStar => Projectile.ai[0] == 1f;

        private const int BoltMaxTime = 45;
        private const int MiniStarMaxTime = 60;
        private const int MiniStarCount = 5;

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 8;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults()
        {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = 1;
            Projectile.timeLeft = BoltMaxTime;
            Projectile.light = 0.6f;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.extraUpdates = 1;
            Projectile.alpha = 0;
        }

        public override void AI()
        {
            if (IsMiniStar)
            {
                MiniStarAI();
            }
            else
            {
                BoltAI();
            }
        }

        private void BoltAI()
        {
            Projectile.rotation += 0.15f;
            Projectile.velocity.Y += 0.02f;

            if (Main.rand.NextBool(3))
            {
                Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
                    ModContent.DustType<LanternDust>(), 0f, 0f, 0, default, 1.1f);
            }
        }

        private void MiniStarAI()
        {
            if (Projectile.localAI[0] == 0f)
            {
                Projectile.localAI[0] = 1f;
                Projectile.width = 8;
                Projectile.height = 8;
                Projectile.light = 0.35f;
                Projectile.timeLeft = MiniStarMaxTime;
                Projectile.extraUpdates = 0;
                Projectile.Resize(8, 8);
            }

            Projectile.rotation += 0.25f;
            Projectile.velocity.Y += 0.15f; 

            if (Main.rand.NextBool(2))
            {
                Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
                    ModContent.DustType<LanternDust>(), 0f, 0f, 0, default, 0.8f);
            }
        }

        public override void OnKill(int timeLeft)
        {
            if (IsMiniStar)
            {
                for (int i = 0; i < 4; i++)
                {
                    Dust.NewDust(Projectile.position, Projectile.width, Projectile.height,
                        ModContent.DustType<LanternDust>(), 0f, 0f, 0, default, 1f);
                }
            }
            else
            {
                Burst();
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity)
        {
            if (!IsMiniStar)
            {
                Burst();
            }
            Projectile.Kill();
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (!IsMiniStar)
            {
                Burst();
            }
        }

        private void Burst()
        {
            SoundEngine.PlaySound(SoundID.Item29, Projectile.position);

            for (int i = 0; i < MiniStarCount; i++)
            {
                float angle = MathHelper.ToRadians(-90) + MathHelper.ToRadians(Main.rand.NextFloat(-60f, 60f));
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(3f, 6f);

                Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    Projectile.Center,
                    velocity,
                    Projectile.type,
                    (int)(Projectile.damage * 0.5f),
                    Projectile.knockBack * 0.5f,
                    Projectile.owner,
                    ai0: 1f); 
            }
        }
    }
}