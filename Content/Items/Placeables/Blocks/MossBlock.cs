using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace ShatteredIllusion.Content.Items.Placeables.Blocks
{
    public class MossBlock : ModTile
    {
        public override void SetStaticDefaults()
        {
            Main.tileSolid[Type] = true;
            Main.tileMergeDirt[Type] = true;
            Main.tileBlockLight[Type] = true;
            Main.tileLighted[Type] = true;
            Main.tileLavaDeath[Type] = true;

            TileObjectData.newTile.CopyFrom(TileObjectData.Style1x1);
            TileObjectData.addTile(Type);

            var mapName = CreateMapEntryName();
            AddMapEntry(new Color(200, 200, 200), mapName);
        }
    public override IEnumerable<Item> GetItemDrops(int i, int j)
        {
            yield return new Item(ModContent.ItemType<MossItem>());
        }
    }
}