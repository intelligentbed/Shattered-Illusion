using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using ShatteredIllusion.Common.Visuals;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace ShatteredIllusion.Content.NPCs.BossAI.GreatAntlionCharger //Lowkey this shouldve been called spit ball but whatever
{
    public class SandBall : ModProjectile
    {
        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 6;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 0;
        }

        public override void SetDefaults()
        {
            Projectile.width = 18;
            Projectile.height = 18;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 300;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = false;
        }

        public override void AI()
        {
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            if (Cinema.Enabled)
                Lighting.AddLight(Projectile.Center, 0.9f, 0.55f, 0.25f);

            if (Main.rand.NextBool(2))
            {
                Dust sandDust = Dust.NewDustPerfect(
                    Projectile.Center + Main.rand.NextVector2Circular(6f, 6f),
                    DustID.SandstormInABottle,
                    -Projectile.velocity * 0.2f
                );
                sandDust.scale = Main.rand.NextFloat(1.2f, 1.8f);
                sandDust.noGravity = true;
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info)
        {
            if (Main.rand.NextBool(1))
            {
                target.AddBuff(BuffID.Poisoned, 300);
            }
        }

        public override void OnKill(int timeLeft)
        {
            SoundEngine.PlaySound(SoundID.NPCHit1, Projectile.Center);

            for (int i = 0; i < 20; i++)
            {
                Vector2 dustVel = Main.rand.NextVector2Circular(5f, 5f) - (Projectile.velocity * 0.2f);
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Sand, dustVel);
                d.scale = Main.rand.NextFloat(1.4f, 2.2f);
            }

            AntlionFX.Ring(Projectile.Center, new Vector2(110f, 110f), new Color(255, 190, 110), 14);
            AntlionFX.Embers(Projectile.Center, 6, 2f, 6f, new Color(255, 190, 110), 8f);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            if (!Cinema.Enabled)
                return DrawBase();

            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 origin = texture.Size() / 2f;
            Vector2 half = Projectile.Size / 2f;

            for (int k = Projectile.oldPos.Length - 1; k >= 1; k--)
            {
                if (Projectile.oldPos[k] == Vector2.Zero)
                    continue;

                float fade = 1f - k / (float)Projectile.oldPos.Length;
                Vector2 pos = Projectile.oldPos[k] + half - Main.screenPosition;

                Main.EntitySpriteDraw(texture, pos, null, new Color(255, 170, 90, 0) * (fade * 0.55f), Projectile.rotation, origin, Projectile.scale * (0.5f + 0.5f * fade), SpriteEffects.None, 0);
                AntlionFX.Glow(Main.spriteBatch, Projectile.oldPos[k] + half, new Color(255, 140, 60) * (fade * 0.35f), new Vector2(40f, 40f) * fade);
            }

            AntlionFX.Glow(Main.spriteBatch, Projectile.Center, new Color(255, 150, 70) * 0.6f, new Vector2(78f, 78f));
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, Color.Lerp(lightColor, Color.White, 0.7f), Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);

            return false;
        }

        private bool DrawBase()
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 drawOrigin = new Vector2(texture.Width * 0.5f, Projectile.height * 0.5f);

            for (int k = 0; k < Projectile.oldPos.Length; k++)
            {
                Vector2 drawPos = Projectile.oldPos[k] - Main.screenPosition + drawOrigin + new Vector2(0f, Projectile.gfxOffY);
                Color color = Projectile.GetAlpha(Color.White) * ((float)(Projectile.oldPos.Length - k) / (float)Projectile.oldPos.Length);

                Main.EntitySpriteDraw(
                    texture,
                    drawPos,
                    null,
                    color * 0.6f,
                    Projectile.rotation,
                    drawOrigin,
                    Projectile.scale,
                    SpriteEffects.None,
                    0
                );
            }

            return true;
        }
    }
}