using Terraria;
using Terraria.ModLoader;

namespace ShatteredIllusion.World
{
    public class UnbreakableHighwayTile : GlobalTile
    {
        public override bool CanKillTile(int i, int j, int type, ref bool blockDamaged)
        {
            if (World_Gen.HighwayGeneration.HighwayArea.Contains(i, j))
            {
                return false; 
            }

            return base.CanKillTile(i, j, type, ref blockDamaged);
        }

        public override bool CanExplode(int i, int j, int type)
        {
            if (World_Gen.HighwayGeneration.HighwayArea.Contains(i, j))
            {
                return false;
            }

            return base.CanExplode(i, j, type);
        }
    }

    public class UnbreakableHighwayWall : GlobalWall
    {
        public override void KillWall(int i, int j, int type, ref bool fail)
        {
            if (World_Gen.HighwayGeneration.HighwayArea.Contains(i, j))
            {
                fail = true;
            }
        }

        public override bool CanExplode(int i, int j, int type)
        {
            if (World_Gen.HighwayGeneration.HighwayArea.Contains(i, j))
            {
                return false; 
            }

            return base.CanExplode(i, j, type);
        }
    }
}