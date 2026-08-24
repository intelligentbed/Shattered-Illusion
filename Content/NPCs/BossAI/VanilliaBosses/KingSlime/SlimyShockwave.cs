using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ShatteredIllusion.Content.NPCs.BossAI.VanilliaBosses.KingSlime
{
    public class SlimyShockwave : ModProjectile
    {
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

            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

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

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = Terraria.GameContent.TextureAssets.Projectile[Projectile.type].Value;
            Vector2 drawOrigin = new Vector2(texture.Width * 0.5f, texture.Height * 0.5f);
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Color drawColor = new Color(0, 150, 255, 200);

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