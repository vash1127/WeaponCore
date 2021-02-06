using System;
using System.Collections.Generic;
using VRage.Game.Entity;
using VRageMath;
using WeaponCore.Support;

namespace WeaponCore.Platform
{
    public partial class Upgrades : Part
    {
        internal Upgrades(CoreSystem system, CoreComponent comp, int partId)
        {
            base.Init(comp, system, partId);
            Log.Line($"init Upgrades: {system.PartName}");
        }
    }
}
