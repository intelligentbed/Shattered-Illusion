using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using Terraria.UI;

namespace ShatteredIllusion.GUI.ResolveBar
{
    [Autoload(Side = ModSide.Client)]
    public sealed class SturdinessBarSystem : ModSystem
    {
        public override void OnModLoad()
        {
            SturdinessBarUI.LoadTextures();
        }

        public override void Unload()
        {
            SturdinessBarUI.UnloadTextures();
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            int index = layers.FindIndex(l => l.Name.Equals("Vanilla: Resource Bars"));
            if (index != -1)
            {
                layers.Insert(index, new LegacyGameInterfaceLayer(
                    "ShatteredIllusion: Sturdiness Bar",
                    delegate
                    {
                        SturdinessBarUI.Draw(Main.spriteBatch, Main.LocalPlayer);
                        return true;
                    },
                    InterfaceScaleType.UI));
            }
        }
    }
}