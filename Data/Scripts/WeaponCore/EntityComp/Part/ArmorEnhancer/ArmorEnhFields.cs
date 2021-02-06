using System.Collections.Generic;
using Sandbox.Game.Entities;
using VRage.Game.ModAPI;
using VRageMath;
using WeaponCore.Support;

namespace WeaponCore.Platform
{
    public partial class ArmorSupport : Part
    {
        internal readonly Dictionary<MyCube, Vector3I> EnhancedArmorBlocks = new Dictionary<MyCube, Vector3I>();
        private readonly Dictionary<MyCube, Vector3> _blockColorBackup = new Dictionary<MyCube, Vector3>();

        internal uint LastBlockRefreshTick;
        internal bool ShowAffectedBlocks;
        internal Vector3I Min;
        internal Vector3I Max;

        internal ArmorSupport(CoreSystem system, CoreComponent comp, int partId)
        {
            base.Init(comp, system, partId);

            Log.Line($"init armor: {system.PartName} - BlockMonitoring:{Comp.Ai.BlockMonitoring}");

            if (!Comp.Ai.BlockMonitoring)
                Comp.Ai.DelayedEventRegistration(true);
        }
    }
}
