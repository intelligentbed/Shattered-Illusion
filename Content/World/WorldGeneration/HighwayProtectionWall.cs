using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.Generation;
using Terraria.ID;
using Terraria.IO;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.WorldBuilding;

namespace ShatteredIllusion.Content.World.WorldGeneration
{
    public class HighwayProtectionWall : GlobalWall
    {
        public override void KillWall(
            int i,
            int j,
            int type,
            ref bool fail)
        {
            if (!HighwayGeneration.HighwayArea.IsEmpty &&
                HighwayGeneration.HighwayArea.Contains(i, j))
            {
                fail = true;
            }
        }

        public override bool CanExplode(
            int i,
            int j,
            int type)
        {
            if (!HighwayGeneration.HighwayArea.IsEmpty &&
                HighwayGeneration.HighwayArea.Contains(i, j))
            {
                return false;
            }

            return base.CanExplode(i, j, type);
        }
    }
}