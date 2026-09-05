using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace ShatteredIllusion.Dusts
{
    public class LanternDust : ModDust
    {
        public override string Texture => "ShatteredIllusion/Dusts/LanternDust";

        public override void OnSpawn(Dust dust)
        {
            dust.noGravity = true;
            dust.noLight = false;
            dust.scale = Main.rand.NextFloat(0.8f, 1.4f);
            dust.velocity *= 0.6f;
            dust.fadeIn = 1.2f;
        }

        public override bool Update(Dust dust)
        {
            dust.position += dust.velocity;
            dust.velocity *= 0.96f;
            dust.rotation += 4f * (dust.velocity.X > 0 ? 1f : -1f);

            if (!dust.noLight)
            {
                float strength = dust.scale * (dust.alpha < 100 ? 1f : 0.5f);
                Vector3 dustLight = new Vector3(0.6f, 0.55f, 0.9f);
                Lighting.AddLight(dust.position, dustLight * strength);
            }

            dust.alpha += 3;
            if (dust.alpha >= 255)
            {
                dust.active = false;
            }

            return false;
        }

        public override Color? GetAlpha(Dust dust, Color lightColor)
        {
            int a = (int)MathHelper.Clamp(255 - dust.alpha * 0.6f, 0, 255);
            return new Color(lightColor.R, lightColor.G, lightColor.B, a);
        }
    }
}