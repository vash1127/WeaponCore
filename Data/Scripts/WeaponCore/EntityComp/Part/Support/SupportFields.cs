using System.Collections.Generic;
using Sandbox.Game.Entities;
using VRage.Game.ModAPI;
using VRageMath;
using WeaponCore.Support;

namespace WeaponCore.Platform
{
    public partial class SupportSys : Part
    {
        internal readonly Dictionary<MyCube, Vector3I> EnhancedArmorBlocks = new Dictionary<MyCube, Vector3I>();
        private readonly Dictionary<MyCube, Vector3> _blockColorBackup = new Dictionary<MyCube, Vector3>();
        internal readonly SupportComponent Comp;
        internal ProtoSupportPartState PartState;

        internal uint LastBlockRefreshTick;
        internal bool ShowAffectedBlocks;
        internal Vector3I Min;
        internal Vector3I Max;

        internal SupportSys(CoreSystem system, SupportComponent comp, int partId)
        {
            Comp = comp;
            base.Init(comp, system, partId);

            Log.Line($"init armor: {system.PartName} - BlockMonitoring:{BaseComp.Ai.BlockMonitoring}");

            if (!BaseComp.Ai.BlockMonitoring)
                BaseComp.Ai.DelayedEventRegistration(true);
        }
    }
}
