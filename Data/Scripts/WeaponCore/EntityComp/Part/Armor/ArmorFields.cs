using System.Collections.Generic;
using VRage.Game.ModAPI;
using VRageMath;
using WeaponCore.Support;

namespace WeaponCore.Platform
{
    public partial class ArmorSupport : Part
    {
        internal readonly Dictionary<IMySlimBlock, Vector3I> EnhancedArmorBlocks = new Dictionary<IMySlimBlock, Vector3I>();
        internal uint LastBlockRefreshTick;

        internal ArmorSupport(CoreSystem system, CoreComponent comp, int partId)
        {
            Log.Line($"init armor: {system.PartName}");
            base.Init(comp, system, partId);
        }
    }
}
