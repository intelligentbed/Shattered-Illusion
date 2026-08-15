using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace ShatteredIllusion.Content.Placeables.Blocks
{
    public class AntlionBossAltar : ModTile
    {
        public override void SetStaticDefaults()
        {
            Main.tileSolid[Type] = false;
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = true;

            TileObjectData.newTile.CopyFrom(TileObjectData.Style3x3);
            TileObjectData.newTile.CoordinateHeights = new int[] { 16, 17, 16 };
            TileObjectData.newTile.CoordinatePadding = 0;
            TileObjectData.newTile.CoordinateWidth = 16;
            TileObjectData.newTile.DrawYOffset = 4;
            TileObjectData.addTile(Type);

            AnimationFrameHeight = 46;

            var mapName = CreateMapEntryName();
            AddMapEntry(new Color(200, 200, 200), mapName);
        }

        public override void AnimateTile(ref int frame, ref int frameCounter)
        {
            frameCounter++;
            if (frameCounter >= 6)
            {
                frameCounter = 0;
                frame++;
                if (frame >= 4) 
                {
                    frame = 0;
                }
            }
        }
    }

    public class AntlionAltar : ModItem
    {
        public override void SetDefaults()
        {
            // id like to thank other mod devs for having their mod's code to be public helping with the code 
            Main.RegisterItemAnimation(Item.type, new Terraria.DataStructures.DrawAnimationVertical(6, 4));

            ItemID.Sets.AnimatesAsSoul[Item.type] = true;

            Item.width = 16;
            Item.height = 16;
            Item.maxStack = 1;
            Item.useTurn = true;
            Item.autoReuse = true;
            Item.useAnimation = 15;
            Item.useTime = 10;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.consumable = true;
            Item.createTile = ModContent.TileType<AntlionBossAltar>();
        }
    }
}