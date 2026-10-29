using System.Globalization;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace ShatteredIllusion.Common.Visuals
{
    // Test-only: delete before release.
    public class FxCommand : ModCommand
    {
        public override CommandType Type => CommandType.Chat;
        public override string Command => "fx";
        public override string Usage => "/fx <parry|landing|impact|phase|charge|ripple|aberration|blur|glitch|wobble|invert|grade|stop> [scale]";

        public override void Action(CommandCaller caller, string input, string[] args)
        {
            string name = args.Length > 0 ? args[0].ToLowerInvariant() : "";

            float scale = 1f;

            if (args.Length > 1 && !float.TryParse(args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out scale))
                scale = 1f;

            if (name == "stop")
            {
                VisualFX.Clear();
                ScreenFX.Clear();
                return;
            }

            Vector2 mouse = Main.MouseWorld;

            switch (name)
            {
                case "ripple": ScreenFX.Ripple(mouse, 700f * scale, 40, scale); return;
                case "aberration": ScreenFX.Aberration(scale, 30); return;
                case "blur": ScreenFX.ZoomBlur(scale, 25, mouse); return;
                case "glitch": ScreenFX.Glitch(scale, 40); return;
                case "wobble": ScreenFX.Wobble(scale, 90); return;
                case "invert": ScreenFX.Invert(scale, 10); return;
                case "grade": ScreenFX.Grade(0.2f, 1.15f, new Color(120, 160, 255), 0.35f * scale, 180); return;
            }

            FxDef def = name switch
            {
                "parry" => Fx.PerfectParry,
                "landing" => Fx.BossLanding,
                "impact" => Fx.ProjectileImpact,
                "phase" => Fx.PhaseTransition,
                "charge" => Fx.ChargeUp,
                _ => null
            };

            if (def == null)
            {
                caller.Reply(Usage);
                return;
            }

            Player player = caller.Player;
            Vector2 target = mouse;
            Vector2 dir = target - player.Center;

            if (def == Fx.ChargeUp)
                VisualFX.PlayOn(def, player, dir: dir, scale: scale);
            else
                VisualFX.Play(def, target, dir, scale);

            // the camera half is local-only in real use, and a chat command is always local
            if (def == Fx.PerfectParry)
                VisualFX.Play(Fx.PerfectParryCamera, target, dir, scale);
        }
    }
}
