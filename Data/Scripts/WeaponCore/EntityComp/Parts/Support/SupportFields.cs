using System.Collections.Generic;
using Sandbox.Game.Entities;
using VRage.Game.ModAPI;
using VRageMath;
using WeaponCore.Support;

namespace WeaponCore.Platform
{
    public partial class SupportSys : Part
    {

        internal readonly SupportComponent Comp;
        internal ProtoSupportPartState PartState;
        internal HashSet<IMySlimBlock> SuppotedBlocks = new HashSet<IMySlimBlock>();
        internal readonly Dictionary<IMySlimBlock, BlockBackup> BlockColorBackup = new Dictionary<IMySlimBlock, BlockBackup>();
        internal readonly SupportSystem System;
        internal uint LastBlockRefreshTick;
        internal bool ShowAffectedBlocks;
        internal Vector3I Min;
        internal Vector3I Max;
        internal BoundingBox Box = BoundingBox.CreateInvalid();

        private readonly HashSet<IMySlimBlock> _updatedBlocks = new HashSet<IMySlimBlock>();
        private readonly HashSet<IMySlimBlock> _newBlocks = new HashSet<IMySlimBlock>();
        private readonly HashSet<IMySlimBlock> _lostBlocks = new HashSet<IMySlimBlock>();
        private readonly HashSet<IMySlimBlock> _agedBlocks = new HashSet<IMySlimBlock>();

        internal SupportSys(SupportSystem system, SupportComponent comp, int partId)
        {
            System = system;
            Comp = comp;
            base.Init(comp, system, partId);

            if (!BaseComp.Ai.BlockMonitoring)
                BaseComp.Ai.DelayedEventRegistration(true);
        }
    }
}
