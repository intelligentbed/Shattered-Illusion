using Terraria.ModLoader;

namespace ShatteredIllusionKeybinds
{
    public class KeybindSystem : ModSystem
    {
        public static ModKeybind ParryKeybind { get; private set; }
        public static ModKeybind SturdinessMeterUseKeybind { get; private set; }

        public override void Load()  
        {
            ParryKeybind = KeybindLoader.RegisterKeybind(Mod, "Parry", "V");
            SturdinessMeterUseKeybind = KeybindLoader.RegisterKeybind(Mod, "Resolve Meter", "C");
        }

        public override void Unload()
        {
            ParryKeybind = null;
            SturdinessMeterUseKeybind = null;
        }
    }
}