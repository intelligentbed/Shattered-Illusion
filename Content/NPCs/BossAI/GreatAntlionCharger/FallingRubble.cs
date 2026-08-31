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


        private ref float CachedEndY => ref Projectile.localAI[0];
        private ref float CachedStartY => ref Projectile.localAI[1];

        private const int TelegraphTicks = 50; 
        private const int DustPerTick = 2;     
        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 5;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 0;
        }

        public override void SetDefaults()
        {
            Projectile.width = 36;
            Projectile.height = 36;
            Projectile.hostile = false;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 600;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI()
        {
            if (State == 0f)
            {
                Projectile.rotation = Main.rand.NextFloat(MathHelper.TwoPi);
                Projectile.scale = Main.rand.NextFloat(1.5f, 2.0f);
                CalculateTelegraphLine();
                State = 1f;
            }

            if (Projectile.timeLeft > 600 - TelegraphTicks)
            {
                Projectile.hostile = false;
                Projectile.tileCollide = false;
                Projectile.velocity = Vector2.Zero;

                DoTelegraphDust();
                return;
            }

            Projectile.hostile = true;
            Projectile.tileCollide = true;

            Projectile.velocity.Y = Math.Min(Projectile.velocity.Y + 0.35f, 16f);

            if (Main.rand.NextBool(2))
            {
                Dust fallDust = Dust.NewDustDirect(
                    Projectile.position,
                    Projectile.width,
                    Projectile.height,
                    DustID.Dirt,
                    Projectile.velocity.X * 0.2f,
                    Projectile.velocity.Y * 0.2f,
                    100,
                    default,
                    1.0f
                );
                fallDust.noGravity = true;
            }
        }

        /// <summary>
        /// Raycasts down from the rubble to find where the telegraph line should end.
        /// Computed once (on spawn) rather than every tick/frame.
        /// </summary>
        private void CalculateTelegraphLine()
        {
            float startY = Projectile.Center.Y + (Projectile.height / 2f);
            float endY = Projectile.Center.Y + 400f;

            int tileX = (int)(Projectile.Center.X / 16f);
            int startTileY = (int)(Projectile.Center.Y / 16f);

            if (tileX >= 10 && tileX < Main.maxTilesX - 10)
            {
                int maxSearchY = Math.Min(Main.maxTilesY - 10, startTileY + 35);
                for (int y = startTileY; y < maxSearchY; y++)
                {
                    Tile tile = Main.tile[tileX, y];

                    if (tile.HasTile && !tile.IsActuated && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType])
                    {
                        endY = y * 16f;
                        break;
                    }
                }
            }

            CachedStartY = startY;
            CachedEndY = endY;
        }

        /// <summary>
        /// Spawns telegraph dust along the cached line. Runs in AI() so it fires every
        /// logic tick regardless of whether the projectile is currently on-screen, and
        /// is rate-limited so many simultaneous rubble instances don't exhaust the
        /// global dust pool (Main.dust has a hard cap around 6000, shared by everything
        /// in the world - torches, liquids, other effects, etc).
        /// </summary>
        private void DoTelegraphDust()
        {
            float startY = CachedStartY;
            float totalLength = MathHelper.Clamp(CachedEndY - startY, 50f, 600f);

            float progress = MathHelper.Clamp((TelegraphTicks - Projectile.timeLeft + (600 - TelegraphTicks)) / (float)TelegraphTicks, 0f, 1f);
            float intensity = MathHelper.Lerp(0.6f, 1.4f, progress);

            for (int i = 0; i < DustPerTick; i++)
            {
                float t = Main.rand.NextFloat();
                Vector2 pos = new Vector2(Projectile.Center.X + Main.rand.NextFloat(-6f, 6f), startY + t * totalLength);

                Dust d = Dust.NewDustPerfect(pos, Main.rand.NextBool() ? DustID.Stone : DustID.Sandstorm, Vector2.Zero, 0, default, 1.6f * intensity);
                d.noGravity = true;
                d.fadeIn = 0.3f;

                if (Main.rand.NextBool(8))
                {
                    Dust flare = Dust.NewDustPerfect(pos, DustID.RedTorch, Vector2.Zero, 0, default, 2.0f * intensity);
                    flare.noGravity = true;
                }
            }

            Lighting.AddLight(new Vector2(Projectile.Center.X, startY + totalLength / 2f), 0.9f * intensity, 0.15f, 0.15f);
        }

        public override bool PreDraw(ref Color lightColor)
        {

            if (Projectile.timeLeft > 600 - TelegraphTicks)
            {
                return false;
            }

            return true;
        }

        public override void OnKill(int timeLeft)
        {
            SoundEngine.PlaySound(SoundID.Dig, Projectile.position);

            for (int i = 0; i < 20; i++)
            {
                Vector2 dustVelocity = Main.rand.NextVector2Circular(6f, 6f);

                Dust.NewDust(
                    Projectile.position,
                    Projectile.width,
                    Projectile.height,
                    DustID.Dirt,
                    dustVelocity.X,
                    dustVelocity.Y,
                    100,
                    default,
                    1.4f
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