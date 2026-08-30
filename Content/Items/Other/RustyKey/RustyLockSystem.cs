using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace ShatteredIllusion.Content.Items.Other.RustyKey
{
    public class RustyLockSystem : ModSystem
    {
        private readonly HashSet<Point16> lockedChests = new HashSet<Point16>();

        public bool IsLocked(int tileX, int tileY)
        {
            Point16? topLeft = GetChestTopLeft(tileX, tileY);
            return topLeft.HasValue && lockedChests.Contains(topLeft.Value);
        }

        public bool Lock(int tileX, int tileY)
        {
            Point16? topLeft = GetChestTopLeft(tileX, tileY);
            return topLeft.HasValue && lockedChests.Add(topLeft.Value);
        }

        public bool Unlock(int tileX, int tileY)
        {
            Point16? topLeft = GetChestTopLeft(tileX, tileY);
            return topLeft.HasValue && lockedChests.Remove(topLeft.Value);
        }

        private static Point16? GetChestTopLeft(int tileX, int tileY)
        {
            int chestIndex = Chest.FindChest(tileX, tileY);
            if (chestIndex < 0)
            {
                return null;
            }

            Chest chest = Main.chest[chestIndex];
            return new Point16(chest.x, chest.y);
        }

        public override void SaveWorldData(TagCompound tag)
        {
            if (lockedChests.Count > 0)
            {
                tag["rustyLockedChests"] = new List<Point16>(lockedChests);
            }
        }

        public override void LoadWorldData(TagCompound tag)
        {
            lockedChests.Clear();
            if (tag.ContainsKey("rustyLockedChests"))
            {
                foreach (Point16 pos in tag.GetList<Point16>("rustyLockedChests"))
                {
                    lockedChests.Add(pos);
                }
            }
        }

        public override void ClearWorld()
        {
            lockedChests.Clear();
        }

        public override void PostUpdateEverything()
        {
            Player player = Main.LocalPlayer;
            if (player.chest < 0)
            {
                return;
            }

            Chest openChest = Main.chest[player.chest];
            if (!IsLocked(openChest.x, openChest.y))
            {
                return;
            }

            player.chest = -1;
            SoundEngine.PlaySound(SoundID.MenuClose);
            Main.NewText("This chest is rusted shut. It needs a key.", 175, 75, 255);
        }
    }

    public class RustyLockGlobalTile : GlobalTile
    {
        public override void RightClick(int i, int j, int type)
        {
            if (!IsVanillaChest(type))
            {
                return;
            }

            Player player = Main.LocalPlayer;
            RustyLockSystem locks = ModContent.GetInstance<RustyLockSystem>();
            bool locked = locks.IsLocked(i, j);
            bool holdingLocker = player.HeldItem.type == ModContent.ItemType<Rustylocker>();
            bool holdingKey = player.HeldItem.type == ModContent.ItemType<RustyKeys>();

            if (!locked && holdingLocker)
            {
                LockChest(player, locks, i, j);
            }
            else if (locked && holdingKey)
            {
                UnlockChest(player, locks, i, j);
            }
        }

        private static bool IsVanillaChest(int type)
        {
            return type == TileID.Containers || type == TileID.Containers2;
        }

        private static void LockChest(Player player, RustyLockSystem locks, int i, int j)
        {
            locks.Lock(i, j);
            ConsumeOne(player);
            SoundEngine.PlaySound(SoundID.Dig, new Vector2(i * 16, j * 16));
            Main.NewText("The chest rusts shut.", 175, 75, 255);
        }

        private static void UnlockChest(Player player, RustyLockSystem locks, int i, int j)
        {
            locks.Unlock(i, j);
            ConsumeOne(player);
            SoundEngine.PlaySound(SoundID.Unlock, new Vector2(i * 16, j * 16));
        }

        private static void ConsumeOne(Player player)
        {
            if (player.whoAmI != Main.myPlayer || player.creativeGodMode)
            {
                return;
            }

            player.HeldItem.stack--;
            if (player.HeldItem.stack <= 0)
            {
                player.HeldItem.TurnToAir();
            }
        }
    }
}