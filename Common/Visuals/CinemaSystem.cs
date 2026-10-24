using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Graphics;
using Terraria.ModLoader;

namespace ShatteredIllusion.Common.Visuals
{
    public class CinemaSystem : ModSystem
    {
        public override void PostUpdateEverything() => Cinema.Update();

        public override void ModifyScreenPosition() => Cinema.ApplyScreenPosition();

        public override void ModifyTransformMatrix(ref SpriteViewMatrix Transform)
        {
            float z = Cinema.TotalZoom;

            if (z > 1.0005f)
                Transform.Zoom *= z;
        }

        public override void PostDrawInterface(SpriteBatch spriteBatch) => Cinema.DrawOverlay(spriteBatch);

        public override void OnWorldUnload() => Cinema.HardReset();
    }
}
