using System;
using Microsoft.Xna.Framework;

namespace ShatteredIllusion.Common.Visuals
{
    // For other mods: add this to your Mod class and they can use the screen filters without a reference.
    //     public override object Call(params object[] args) => ShatteredIllusion.Common.Visuals.ScreenFXCall.Handle(args);
    // Then from the other mod: ModLoader.GetMod("ShatteredIllusion").Call("Ripple", pos, 600f)
    //   "Ripple", Vector2 pos, float radius, [int frames, float strength]
    //   "Preset", string name, Vector2 pos, [float scale]
    //   "Pulse", string channel, float value, [int frames]
    //   "Hold", string channel, float value, string tag
    //   "Release", string tag        "Clear"
    public static class ScreenFXCall
    {
        public static object Handle(params object[] args)
        {
            if (args.Length == 0 || args[0] is not string verb)
                return null;

            switch (verb.ToLowerInvariant())
            {
                case "ripple":
                    ScreenFX.Ripple((Vector2)args[1], Convert.ToSingle(args[2]), args.Length > 3 ? Convert.ToInt32(args[3]) : 30, args.Length > 4 ? Convert.ToSingle(args[4]) : 1f);
                    return true;

                case "preset":
                    if (!ScreenPresets.All.TryGetValue((string)args[1], out FxDef def))
                        return false;

                    VisualFX.Play(def, (Vector2)args[2], default, args.Length > 3 ? Convert.ToSingle(args[3]) : 1f);
                    return true;

                case "pulse":
                    if (!Enum.TryParse((string)args[1], true, out Channel pulse))
                        return false;

                    ScreenFX.Pulse(pulse, Convert.ToSingle(args[2]), args.Length > 3 ? Convert.ToInt32(args[3]) : 30);
                    return true;

                case "hold":
                    if (!Enum.TryParse((string)args[1], true, out Channel held))
                        return false;

                    ScreenFX.Hold(held, Convert.ToSingle(args[2]), tag: (string)args[3]);
                    return true;

                case "release":
                    ScreenFX.Release((string)args[1]);
                    return true;

                case "clear":
                    ScreenFX.Clear();
                    return true;
            }

            return null;
        }
    }
}
