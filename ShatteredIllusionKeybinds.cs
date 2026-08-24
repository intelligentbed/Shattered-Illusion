using Terraria.ModLoader;

namespace ShatteredIllusionKeybinds
{
    public class KeybindSystem : ModSystem
    {
        public static ModKeybind ParryKeybind { get; private set; }

        public override void Load()
        {
            ParryKeybind = KeybindLoader.RegisterKeybind(Mod, "Parry", "V");
        }

        public override void Unload()
        {
            ParryKeybind = null;
        }
    }
}