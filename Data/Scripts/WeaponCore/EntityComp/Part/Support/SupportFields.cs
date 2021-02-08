using System.Collections.Generic;
using Sandbox.Game.Entities;
using VRage.Game.ModAPI;
using VRageMath;
using WeaponCore.Support;

namespace WeaponCore.Platform
{
    public partial class SupportSys : Part
    {
        private HashSet<MyCube> _updatedBlocks = new HashSet<MyCube>();
        private HashSet<MyCube> _newBlocks = new HashSet<MyCube>();
        private HashSet<MyCube> _lostBlocks = new HashSet<MyCube>();
        private HashSet<MyCube> _agedBlocks = new HashSet<MyCube>();
        private readonly Dictionary<MyCube, Vector3> _blockColorBackup = new Dictionary<MyCube, Vector3>();
        internal readonly SupportComponent Comp;
        internal ProtoSupportPartState PartState;
        internal HashSet<MyCube> SuppotedBlocks = new HashSet<MyCube>();

        internal uint LastBlockRefreshTick;
        internal bool ShowAffectedBlocks;
        internal Vector3I Min;
        internal Vector3I Max;
        internal BoundingBoxI Box = BoundingBoxI.CreateInvalid();

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
