using System.Collections.Generic;
using CoreSystems.Support;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;

namespace CoreSystems.Platform
{
    public partial class SupportSys : Part
    {

        internal readonly HashSet<IMySlimBlock> SuppotedBlocks = new HashSet<IMySlimBlock>();
        internal readonly Dictionary<IMySlimBlock, BlockBackup> BlockColorBackup = new Dictionary<IMySlimBlock, BlockBackup>();
        internal readonly SupportInfo Info;
        internal readonly SupportComponent Comp;
        internal readonly SupportSystem System;
        internal readonly MyStringHash PartHash;

        private readonly HashSet<IMySlimBlock> _updatedBlocks = new HashSet<IMySlimBlock>();
        private readonly HashSet<IMySlimBlock> _newBlocks = new HashSet<IMySlimBlock>();
        private readonly HashSet<IMySlimBlock> _lostBlocks = new HashSet<IMySlimBlock>();
        private readonly HashSet<IMySlimBlock> _agedBlocks = new HashSet<IMySlimBlock>();

        private int _charges;

        
        internal uint LastBlockRefreshTick;
        internal bool ShowAffectedBlocks;
        internal bool Active;
        internal Vector3I Min;
        internal Vector3I Max;
        internal BoundingBox Box = BoundingBox.CreateInvalid();
        internal ProtoSupportPartState PartState;

        internal SupportSys(SupportSystem system, SupportComponent comp, int partId)
        {
            System = system;
            Comp = comp;
            Info = new SupportInfo(this);

            Init(comp, system, partId);
            PartHash = Comp.Structure.PartHashes[partId];

            if (!BaseComp.Ai.BlockMonitoring)
                BaseComp.Ai.DelayedEventRegistration(true);
        }
    }
}
