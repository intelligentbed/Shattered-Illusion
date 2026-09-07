using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace ShatteredIllusion.Content.NPCs.BossAI.VanillaBosses.KingSlime
{
    public class SlimyShockwave : ModProjectile
    {
        // ai[0] = spin direction (+1 clockwise / -1 counter-clockwise)
        // ai[1] = frames to "charge" in place before launching (used for the staggered second pulse)

        private const float CurlDegreesTotal = 65f; // total curl applied over the ramp - NOT per tick, NOT unbounded
        private const float CurlRampTicks = 35f;     // ticks over which that curl is applied, then it flies straight
        private const float MaxTravelDistance = 420f; // shockwave dissipates past this range instead of chasing forever

        private Vector2 _launchVelocity;
        private Vector2 _spawnPos;
        private bool _velocityCaptured;
        private bool _launched;
        private float _age;

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 5;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 0;
        }

        public override void SetDefaults()
        {
            Projectile.width = 28;
            Projectile.height = 28;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false; // im kinda iffy about this guess we'll see
            Projectile.timeLeft = 180;
            Projectile.alpha = 50;
        }

        public override void AI()
        {
            if (!_velocityCaptured)
            {
                _launchVelocity = Projectile.velocity;
                _spawnPos = Projectile.Center;
                _velocityCaptured = true;
            }

            if (!_launched)
            {
                if (Projectile.ai[1] > 0f)
                {
                    Projectile.ai[1] -= 1f;
                    Projectile.velocity = Vector2.Zero;
                    Projectile.rotation += 0.05f; // spin in place while charging so it isn't just sitting frozen

                    if (Main.rand.NextBool(3))
                    {
                        Dust d = Dust.NewDustPerfect(
                            Projectile.Center,
                            DustID.BlueCrystalShard,
                            Vector2.Zero,
                            100,
                            new Color(0, 120, 255, 180),
                            1f
                        );
                        d.noGravity = true;
                    }

                    return;
                }

                Projectile.velocity = _launchVelocity;
                _launched = true;
                SoundEngine.PlaySound(SoundID.Item9 with { Pitch = 0.4f, Volume = 0.6f }, Projectile.Center);
            }

            _age += 1f;

            if (_age <= CurlRampTicks)
            {
                float spinDir = Projectile.ai[0] == 0f ? 1f : Projectile.ai[0];
                float turnRate = MathHelper.ToRadians(CurlDegreesTotal / CurlRampTicks) * spinDir;
                Projectile.velocity = Projectile.velocity.RotatedBy(turnRate);
            }

            // Grows a bit as it spins outward
            Projectile.scale = MathHelper.Clamp(0.85f + _age * 0.01f, 0.85f, 1.6f);

            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            if (Vector2.Distance(Projectile.Center, _spawnPos) >= MaxTravelDistance)
            {
                Projectile.Kill();
                return;
            }

            if (Main.rand.NextBool(2))
            {
                Dust d = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.TintableDust,
                    -Projectile.velocity * 0.2f,
                    100,
                    new Color(0, 120, 255, 180),
                    1.2f
                );
                d.noGravity = true;
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info)
        {
            for (int i = 0; i < 6; i++)
            {
                Dust d = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.BlueCrystalShard,
                    Main.rand.NextVector2Circular(3f, 3f),
                    100,
                    Color.Cyan,
                    1.3f
                );
                d.noGravity = true;
            }
        }

        public override void Kill(int timeLeft)
        {
            SoundEngine.PlaySound(SoundID.Shatter, Projectile.Center);

            for (int i = 0; i < 10; i++)
            {
                Dust d = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.BlueCrystalShard,
                    Main.rand.NextVector2Circular(4f, 4f),
                    100,
                    Color.Cyan,
                    Main.rand.NextFloat(1.2f, 2.0f)
                );
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = Terraria.GameContent.TextureAssets.Projectile[Projectile.type].Value;
            Vector2 drawOrigin = new Vector2(texture.Width * 0.5f, texture.Height * 0.5f);
            Color drawColor = new Color(0, 150, 255, 200);

            for (int i = 0; i < Projectile.oldPos.Length; i++)
            {
                if (Projectile.oldPos[i] == Vector2.Zero)
                    continue;

                float trailProgress = 1f - (i / (float)Projectile.oldPos.Length);
                Vector2 trailDrawPos = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;

                Main.EntitySpriteDraw(
                    texture,
                    trailDrawPos,
                    null,
                    drawColor * trailProgress * 0.45f,
                    Projectile.oldRot[i],
                    drawOrigin,
                    Projectile.scale * trailProgress,
                    SpriteEffects.None,
                    0
                );
            }

            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            Main.EntitySpriteDraw(
                texture,
                drawPos,
                null,
                drawColor,
                Projectile.rotation,
                drawOrigin,
                Projectile.scale,
                SpriteEffects.None,
                0
            );

            return false;
        }
    }
}