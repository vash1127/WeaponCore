using System;
using System.Collections.Generic;
using VRage.Game.Entity;
using VRageMath;
using WeaponCore.Support;

namespace WeaponCore.Platform
{
    internal partial class Upgrades : Part
    {
        internal readonly Upgrade.UpgradeComponent Comp;
        internal ProtoUpgradePartState PartState;
        internal Upgrades(UpgradeSystem system, Upgrade.UpgradeComponent comp, int partId)
        {
            Comp = comp;
            base.Init(comp, system, partId);

            Log.Line($"init Upgrades: {system.PartName}");
        }
    }
}
