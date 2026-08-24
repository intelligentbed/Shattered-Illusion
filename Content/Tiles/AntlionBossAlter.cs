using Microsoft.Xna.Framework;
using ShatteredIllusion.Content.Items.SummonItems;
using ShatteredIllusion.Content.NPCs.BossAI.GreatAntlionCharger;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace ShatteredIllusion.Content.Tiles
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
            TileObjectData.newTile.DrawYOffset = 6;
            TileObjectData.addTile(Type);

            AnimationFrameHeight = 46;

            var mapName = CreateMapEntryName();
            AddMapEntry(new Color(200, 200, 200), mapName);
        }
        public override void MouseOver(int i, int j)
        {
            Player player = Main.LocalPlayer;
            player.cursorItemIconEnabled = true;
            player.cursorItemIconID = ModContent.ItemType<AntlionAttractor>();
        }

        public override bool RightClick(int i, int j)
        {
            Player player = Main.LocalPlayer;
            int bossType = ModContent.NPCType<GreatAntlionCharger>();
            int itemType = ModContent.ItemType<AntlionAttractor>();

            if (NPC.AnyNPCs(bossType))
            {
                return true;
            }

            if (player.HeldItem.type == itemType)
            {
                if (player.HeldItem.stack > 0)
                {
                    player.HeldItem.stack--;
                    SoundEngine.PlaySound(new SoundStyle("ShatteredIllusion/Sounds/AntlionRumble"));
                    if (player.HeldItem.stack <= 0)
                    {
                        player.HeldItem.TurnToAir();
                    }
                }

                Tile tile = Main.tile[i, j];

                int frameX = tile.TileFrameX % 48;
                int tileOffsetX = frameX / 16;

                int frameY = tile.TileFrameY % AnimationFrameHeight;
                int tileOffsetY = 0;

                if (frameY >= 33)
                {
                    tileOffsetY = 2;
                }
                else if (frameY >= 16)
                {
                    tileOffsetY = 1;
                }

                int originI = i - tileOffsetX;
                int originJ = j - tileOffsetY;

                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    int spawnX = (originI + 12) * 16;
                    int spawnY = (originJ - 5) * 16;

                    NPC.NewNPC(new EntitySource_TileInteraction(player, originI, originJ), spawnX, spawnY, bossType);
                }
                else
                {
                    NetMessage.SendData(MessageID.SpawnBossUseLicenseStartEvent, -1, -1, null, player.whoAmI, bossType);
                }

                return true;
            }

            return base.RightClick(i, j);
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
            Main.RegisterItemAnimation(Item.type, new DrawAnimationVertical(6, 4));

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