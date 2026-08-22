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
                State = 1f;
            }

            if (Projectile.timeLeft > 550)
            {
                Projectile.hostile = false;
                Projectile.tileCollide = false;
                Projectile.velocity = Vector2.Zero;
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

        public override bool PreDraw(ref Color lightColor)
        {
            if (Projectile.timeLeft > 550)
            {
                Texture2D telegraphTex = ModContent.Request<Texture2D>("ShatteredIllusion/Content/NPCs/BossAI/GreatAntlionCharger/RubbleTelegraph").Value;

                float startY = Projectile.Center.Y - 25f;
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

                float totalLength = MathHelper.Clamp(endY - startY, 50f, 600f);
                Vector2 drawPos = new Vector2(Projectile.Center.X, startY) - Main.screenPosition;

                float progress = (600 - Projectile.timeLeft) / 50f;
                float alpha = MathHelper.Clamp(1f - progress, 0.4f, 1.0f);
                Color drawColor = Color.Red * alpha;

                float rotation = MathHelper.PiOver2;
                Vector2 origin = new Vector2(0f, telegraphTex.Height / 2f);
                Vector2 scale = new Vector2(totalLength / telegraphTex.Width, 6f);

                Main.spriteBatch.End();
                Main.spriteBatch.Begin(
                    SpriteSortMode.Deferred,
                    BlendState.Additive,
                    Main.DefaultSamplerState,
                    DepthStencilState.None,
                    RasterizerState.CullCounterClockwise,
                    null,
                    Main.GameViewMatrix.TransformationMatrix
                );

                Main.EntitySpriteDraw(
                    telegraphTex,
                    drawPos,
                    null,
                    drawColor,
                    rotation,
                    origin,
                    scale,
                    SpriteEffects.None,
                    0
                );

                Main.spriteBatch.End();
                Main.spriteBatch.Begin(
                    SpriteSortMode.Deferred,
                    BlendState.AlphaBlend,
                    Main.DefaultSamplerState,
                    DepthStencilState.None,
                    RasterizerState.CullCounterClockwise,
                    null,
                    Main.GameViewMatrix.TransformationMatrix
                );
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