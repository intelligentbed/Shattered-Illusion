using System.Globalization;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace ShatteredIllusion.Common.Visuals
{
    // Dev/test command, shipped on purpose.
    public class FxCommand : ModCommand
    {
        public override CommandType Type => CommandType.Chat;
        public override string Command => "fx";
        public override string Usage => "/fx <parry|landing|impact|phase|charge|stop> [scale]";

        public override void Action(CommandCaller caller, string input, string[] args)
        {
            string name = args.Length > 0 ? args[0].ToLowerInvariant() : "";

            if (name == "stop")
            {
                VisualFX.Clear();
                ScreenFX.Clear();
                return;
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

            float scale = 1f;

            if (args.Length > 1 && !float.TryParse(args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out scale))
                scale = 1f;

            Player player = caller.Player;
            Vector2 target = Main.MouseWorld;
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
