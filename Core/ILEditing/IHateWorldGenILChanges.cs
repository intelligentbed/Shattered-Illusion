using System;
using Microsoft.Xna.Framework;
using MonoMod.Cil;
using Terraria;
using Terraria.ModLoader;
using Terraria.WorldBuilding;
using ShatteredIllusion.Content.World;

namespace ShatteredIllusion.Core.ILEditing
{
    public sealed class IHateWorldGenILChanges : ILoadable
    {
        private static Mod modInstance;

        public const int DungeonShorePadding = 250;

        // Safety cap so a wide shore on a Small world can't shove the dungeon toward the middle of the map.
        private const float MaxLeftLimitFraction = 0.3f;

        /// <summary>
        /// only left side
        /// </summary>
        public static int? DungeonLeftXLimitOverride { get; set; }

        public static int DungeonLeftXLimit
        {
            get
            {
                int limit = DungeonLeftXLimitOverride ?? (AshenShore.BiomeWidth + DungeonShorePadding);
                return Math.Min(limit, (int)(Main.maxTilesX * MaxLeftLimitFraction));
            }
        }

        public void Load(Mod mod)
        {
            modInstance = mod;
            On_WorldGen.MakeDungeon += PushDungeonEntranceOffShore;
            IL_WorldGen.DungeonHalls += PushDungeonHallsOffShore;
        }

        public void Unload()
        {
            On_WorldGen.MakeDungeon -= PushDungeonEntranceOffShore;
            IL_WorldGen.DungeonHalls -= PushDungeonHallsOffShore;
            modInstance = null;
        }

        /// <summary>
        /// only dungeons on the left half of the world get moved, and only to the right.
        /// </summary>
        public static int ClampDungeonX(int x)
        {
            if (x < Main.maxTilesX / 2)
                return Math.Max(x, DungeonLeftXLimit);

            return x;
        }

        private static void PushDungeonEntranceOffShore(On_WorldGen.orig_MakeDungeon orig, int x, int y)
        {
            int newX = ClampDungeonX(x);

            if (newX != x)
            {
                x = newX;

                bool groundFound = WorldUtils.Find(
                    new Point(x, y),
                    Searches.Chain(new Searches.Down(9001), new Conditions.IsSolid()),
                    out Point result);

                if (groundFound)
                    y = result.Y - 10;
            }

            orig(x, y);
        }

        private static void PushDungeonHallsOffShore(ILContext il)
        {
            var c = new ILCursor(il);

            // Land right after the first ldarg.0 (the hall's starting X), before it's stored into the vector.
            if (!c.TryGotoNext(MoveType.After, x => x.MatchLdarg0()))
            {
                modInstance?.Logger.Warn("[WorldGen IL] Could not match ldarg.0 in WorldGen.DungeonHalls; dungeon halls will not be pushed off the Ashen Shore.");
                return;
            }

            c.EmitDelegate<Func<int, int>>(ClampDungeonX);
        }
    }
}