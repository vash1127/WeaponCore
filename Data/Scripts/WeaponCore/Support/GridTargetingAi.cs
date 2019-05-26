using System.Collections.Generic;
using Sandbox.Game.Entities;

namespace WeaponCore.Support
{
    internal class GridTargetingAi
    {
        internal readonly MyCubeGrid MyGrid;
        internal readonly Dictionary<MyCubeBlock, WeaponComponent> WeaponBase = new Dictionary<MyCubeBlock, WeaponComponent>();
        internal GridTargetingAi(MyCubeGrid grid)
        {
            MyGrid = grid;
        }
    }
}
