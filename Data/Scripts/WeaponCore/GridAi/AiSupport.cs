using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;

namespace WeaponCore.Support
{
    public partial class GridTargetingAi
    {
        public void SubGridInfo()
        {
            SubUpdate = false;
            SubTick = Session.Instance.Tick + 10;
            SubGridUpdate = true;
            SubGrids.Clear();
            foreach (var sub in MyAPIGateway.GridGroups.GetGroup(MyGrid, GridLinkTypeEnum.Mechanical))
                SubGrids.Add((MyCubeGrid) sub);

            foreach (var sub in SubGrids)
                    GroupAABB.Include(sub.PositionComp.WorldAABB);

            SubGridUpdate = false;
        }
    }
}
