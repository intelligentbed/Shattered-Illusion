using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace ShatteredIllusion.Common.Visuals.ThreeD
{
    // Same projection and shapes, aimed at UI pixels. Call from a UI draw with its SpriteBatch already begun.
    public static class ThreeDUI
    {
        public static Vector2 ScreenCenter => new Vector2(Main.screenWidth, Main.screenHeight) * 0.5f / Main.UIScale;

        public static Vector2 Project(Vector3 position, Vector2 vanish, out float scale)
            => ThreeDMath.Project(position, View3.Screen(vanish), out scale);

        public static void DrawSprite(SpriteBatch sb, Texture2D texture, Vector3 position, Vector2 vanish, Color color, float rotation = 0f, float scale = 1f, Rectangle? frame = null)
            => ThreeDDraw.DrawSprite(sb, View3.Screen(vanish), texture, position, color * ThreeDDraw.DepthBrightness(position.Z), rotation, scale, frame);

        public static void DrawShape(SpriteBatch sb, Shape3D shape, Vector2 vanish, Rectangle bounds)
            => shape.Draw(sb, View3.Screen(vanish), bounds);

        public static void DrawShape(SpriteBatch sb, Shape3D shape, Vector2 vanish)
            => shape.Draw(sb, View3.Screen(vanish), new Rectangle(0, 0, (int)(Main.screenWidth / Main.UIScale), (int)(Main.screenHeight / Main.UIScale)));
    }
}
