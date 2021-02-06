using System;
using System.Collections.Generic;
using Sandbox.Definitions;
using VRage.Game.Entity;
using VRageMath;
using WeaponCore.Support;

namespace WeaponCore.Platform
{
    public partial class ArmorSupport : Part
    {
        internal void RefreshBlocks()
        {
            Session.GetCubesInRange(Comp.Cube.CubeGrid, Comp.Cube, 3, EnhancedArmorBlocks, Session.CubeTypes.Slims);
            foreach (var e in EnhancedArmorBlocks)
            {
                var blockDef = (MyCubeBlockDefinition)e.Key.BlockDefinition;
                Log.Line($"{blockDef.DisplayNameText}");
            }
            LastBlockRefreshTick = System.Session.Tick;
        }
    }
}
