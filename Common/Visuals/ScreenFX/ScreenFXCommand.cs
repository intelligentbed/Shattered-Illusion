using System;
using System.Globalization;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace ShatteredIllusion.Common.Visuals
{
    // Dev/test command, shipped on purpose.
    public class ScreenFXCommand : ModCommand
    {
        public override CommandType Type => CommandType.Chat;
        public override string Command => "sfx";
        public override string Usage => "/sfx list | stop | release [preset] | <preset> [scale] | <channel> [value] [frames] | hold <channel> <value> | ripple|swirl|bulge [value] [frames]";

        private static float Num(string[] args, int i, float fallback)
            => args.Length > i && float.TryParse(args[i], NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : fallback;

        public override void Action(CommandCaller caller, string input, string[] args)
        {
            string name = args.Length > 0 ? args[0] : "";
            Vector2 mouse = Main.MouseWorld;

            switch (name.ToLowerInvariant())
            {
                case "list":
                    caller.Reply("Presets: " + string.Join(", ", ScreenPresets.All.Keys));
                    caller.Reply("Channels: " + string.Join(", ", Enum.GetNames<Channel>()));
                    return;

                case "stop":
                    ScreenFX.Clear();
                    return;

                case "release":
                    string tag = args.Length > 1 ? ScreenPresets.TagOf(args[1]) : null;

                    if (tag != null)
                        ScreenFX.Release(tag);
                    else
                        ScreenFX.ReleaseAll();

                    return;

                case "ripple":
                    ScreenFX.Ripple(mouse, 700f, (int)Num(args, 2, 40), Num(args, 1, 1f));
                    return;

                case "swirl":
                    ScreenFX.Swirl(mouse, 500f, Num(args, 1, 1f) * 5f, Env.Pulse((int)Num(args, 2, 40)));
                    return;

                case "bulge":
                    ScreenFX.Bulge(mouse, 400f, Num(args, 1, 1f) * 0.5f, Env.Pulse((int)Num(args, 2, 40)));
                    return;

                case "hold":
                    if (args.Length > 1 && Enum.TryParse(args[1], true, out Channel held))
                    {
                        ScreenFX.Hold(held, Num(args, 2, 1f), tag: "cmd");
                        return;
                    }

                    break;
            }

            if (ScreenPresets.All.TryGetValue(name, out FxDef preset))
            {
                VisualFX.Play(preset, mouse, mouse - caller.Player.Center, Num(args, 1, 1f));
                return;
            }

            if (Enum.TryParse(name, true, out Channel channel))
            {
                ScreenFX.Pulse(channel, Num(args, 1, 1f), (int)Num(args, 2, 40), Color.White);
                return;
            }

            caller.Reply(Usage);
        }
    }
}
