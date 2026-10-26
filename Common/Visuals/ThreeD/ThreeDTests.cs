using System.Globalization;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace ShatteredIllusion.Common.Visuals.ThreeD
{
    // Test-only: delete this file before release.
    public class ThreeDTestWall : ShapeProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Bullet;

        public override void ShapeSetDefaults()
        {
            Projectile.hostile = true;
            Projectile.damage = 10;
            Projectile.timeLeft = 240;
        }

        public override Shape3D BuildShape()
            => Shape3D.Wall(null, 7, 5, 42f, 46f, new Color(255, 80, 80, 0));
    }

    public class ThreeDTestSprite : ThreeDProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Bullet;

        public override void ThreeDSetDefaults()
        {
            Projectile.hostile = true;
            Projectile.damage = 10;
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.scale = 3f;
            Projectile.timeLeft = 240;
        }

        public override void ThreeDAI() => Projectile.rotation += 0.2f;
    }

    public class Fx3Command : ModCommand
    {
        public override CommandType Type => CommandType.Chat;
        public override string Command => "fx3";
        public override string Usage => "/fx3 <ring|sphere|helix|line|wall|orbit|wallhit|sprite|burst|stop> [z] [i]  (i = instanced)";

        public override void Action(CommandCaller caller, string input, string[] args)
        {
            string name = args.Length > 0 ? args[0].ToLowerInvariant() : "";

            if (name == "stop")
            {
                ThreeDRenderer.ClearAll();
                return;
            }

            float z = 0f;

            if (args.Length > 1)
                float.TryParse(args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out z);

            bool instanced = args.Length > 2 && args[2] == "i";

            Player player = caller.Player;
            Vector2 target = Main.MouseWorld;
            Vector3 pos = new(target.X, target.Y, z);
            Color glow = new(120, 200, 255, 0);

            Shape3D shape = name switch
            {
                "ring" => Shape3D.Ring(null, 48, 120f, 28f, glow),
                "sphere" => Shape3D.Sphere(null, 160, 110f, 22f, glow),
                "helix" => Shape3D.Helix(null, 120, 70f, 360f, 4f, 20f, glow),
                "line" => Shape3D.Line(null, 40, 400f, 24f, glow),
                "wall" => Shape3D.Wall(null, 9, 6, 40f, 36f, glow),
                "orbit" => Shape3D.Ring(null, 48, 120f, 28f, glow),
                _ => null
            };

            if (shape != null)
            {
                if (name == "orbit")
                {
                    pos = new Vector3(player.Center.X, player.Center.Y, z);
                    shape.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathHelper.PiOver2);
                }

                shape.Position = pos;
                shape.Instanced = instanced;
                shape.Spin = new Vector3(0.03f, 0.02f, 0.01f);
                shape.Life = 600;
                ThreeDRenderer.Spawn(shape);
                return;
            }

            IEntitySource source = player.GetSource_Misc("fx3");

            switch (name)
            {
                case "wallhit":
                    ShapeProjectile.Spawn<ThreeDTestWall>(source, new Vector3(player.Center.X, player.Center.Y, -900f), new Vector3(0f, 0f, 14f), 10, 1f);
                    break;

                case "sprite":
                    ThreeDProjectile.Spawn<ThreeDTestSprite>(source, new Vector3(player.Center.X, player.Center.Y, -900f), new Vector3(0f, 0f, 14f), 10, 1f);
                    break;

                case "burst":
                    ThreeDFX.Play(Fx.BossLanding, pos, scale: 1.5f);
                    break;

                default:
                    caller.Reply(Usage);
                    break;
            }
        }
    }
}
