using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using System.Collections.Generic;
using VRage;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;

namespace WeaponCore.Support
{
    public class CoreTargeting : MyGridTargeting
    {
        private MyCubeGrid _myGrid;
        private readonly Session _session;
        private double _scanningRange = double.MinValue;
        internal readonly List<MyEntity> Targets = new List<MyEntity>();
        internal readonly Dictionary<MyCubeGrid, List<MyEntity>> GridBlocks = new Dictionary<MyCubeGrid, List<MyEntity>>();
        private readonly List<long> _users = new List<long>();
        private readonly List<long> _owners = new List<long>();
        private static readonly FastResourceLock SelfLock = new FastResourceLock();
        private bool _inited;
        private uint _lastScan;
        public new bool AllowScanning = true;

        public CoreTargeting(Session session)
        {
            _session = session;
        }
        public override void OnAddedToContainer()
        {
            base.OnAddedToContainer();
            _myGrid = (MyCubeGrid)Entity;
            _myGrid.OnFatBlockAdded += OnFatBlockChanged;
            _myGrid.OnFatBlockRemoved += OnFatBlockChanged;

        }

        public override void OnBeforeRemovedFromContainer()
        {
            base.OnBeforeRemovedFromContainer();
            _myGrid.OnFatBlockAdded -= OnFatBlockChanged;
            _myGrid.OnFatBlockRemoved -= OnFatBlockChanged;

        }

        private void OnFatBlockChanged(MyCubeBlock myCubeBlock)
        {
            GridAi gridAi;
            if (_session.GridTargetingAIs.TryGetValue(myCubeBlock.CubeGrid, out gridAi))
                _scanningRange = gridAi.MaxTargetingRange;
            else _scanningRange = double.MinValue;
        }

        private void Init()
        {
            _inited = true;
            GridAi gridAi;
            if (_session.GridTargetingAIs.TryGetValue(_myGrid, out gridAi))
                _scanningRange = gridAi.MaxTargetingRange;
        }

        private void Scan()
        {
            //_session.DsUtil.Start("");
            using (SelfLock.AcquireExclusiveUsing())
            {
                if (AllowScanning && _session.Tick - _lastScan > 100)
                {
                    _lastScan = _session.Tick;
                    var boundingSphereD = _myGrid.PositionComp.WorldVolume;
                    boundingSphereD.Radius = _scanningRange;
                    Targets.Clear();
                    GridBlocks.Clear();
                    MyGamePruningStructure.GetAllTopMostEntitiesInSphere(ref boundingSphereD, Targets);

                    int count = Targets.Count;
                    _owners.AddRange(_myGrid.SmallOwners);
                    _owners.AddRange(_myGrid.BigOwners);
                    for (int i = 0; i < count; i++)
                    {
                        var myCubeGrid = Targets[i] as MyCubeGrid;
                        if (myCubeGrid != null && (myCubeGrid.Physics == null || myCubeGrid.Physics.Enabled))
                        {
                            var flag = false;
                            if (myCubeGrid.BigOwners.Count == 0 && myCubeGrid.SmallOwners.Count == 0)
                            {

                                for(int j = 0; j < _owners.Count; j++)
                                {
                                    if (MyIDModule.GetRelationPlayerBlock(_owners[j], 0L, MyOwnershipShareModeEnum.None, MyRelationsBetweenPlayerAndBlock.Enemies, MyRelationsBetweenFactions.Enemies, MyRelationsBetweenPlayerAndBlock.FactionShare) == MyRelationsBetweenPlayerAndBlock.NoOwnership)
                                    {
                                        flag = true;
                                        break;
                                    }
                                }

                            }
                            else
                            {
                                _users.AddRange(myCubeGrid.BigOwners);
                                _users.AddRange(myCubeGrid.SmallOwners);
                                for (int j = 0; j < _owners.Count; j++)
                                {
                                    var owner = _owners[j];
                                    for (int c = 0; c < _users.Count; c++)
                                    {
                                        var user = _users[c];
                                        if (MyIDModule.GetRelationPlayerBlock(owner, user, MyOwnershipShareModeEnum.None, MyRelationsBetweenPlayerAndBlock.Enemies, MyRelationsBetweenFactions.Enemies, MyRelationsBetweenPlayerAndBlock.FactionShare) == MyRelationsBetweenPlayerAndBlock.Enemies)
                                        {
                                            flag = true;
                                            break;
                                        }
                                    }
                                    if (flag) break;
                                }
                                _users.Clear();
                            }
                            if (flag)
                            {
                                if (!GridBlocks.ContainsKey(myCubeGrid))
                                    GridBlocks[myCubeGrid] = new List<MyEntity>();

                                var orAdd = GridBlocks[myCubeGrid];

                                using (myCubeGrid.Pin())
                                {
                                    if (!myCubeGrid.MarkedForClose)
                                    {
                                        myCubeGrid.Hierarchy.QuerySphere(ref boundingSphereD, orAdd);
                                    }
                                    continue;
                                }
                            }
                            var fatBlocks = myCubeGrid.GetFatBlocks();
                            for (int j = 0; j < fatBlocks.Count; j++){
                                var myCubeBlock = fatBlocks[j];
                                IMyComponentOwner<MyIDModule> myComponentOwner = myCubeBlock;
                                MyIDModule myIdModule;
                                if (myComponentOwner != null && myComponentOwner.GetComponent(out myIdModule))
                                {
                                    var ownerId = myCubeBlock.OwnerId;
                                    for (int c = 0; c < _owners.Count; c++)
                                    {
                                        if (MyIDModule.GetRelationPlayerBlock(_owners[c], ownerId, MyOwnershipShareModeEnum.None, MyRelationsBetweenPlayerAndBlock.Enemies, MyRelationsBetweenFactions.Enemies, MyRelationsBetweenPlayerAndBlock.FactionShare) == MyRelationsBetweenPlayerAndBlock.Enemies)
                                        {
                                            flag = true;
                                            break;
                                        }
                                    }
                                    if (flag) break;
                                }
                            }
                            if (!flag) continue;
                            if (!GridBlocks.ContainsKey(myCubeGrid))
                                GridBlocks[myCubeGrid] = new List<MyEntity>();

                            var orAdd2 = GridBlocks[myCubeGrid];

                            if (!myCubeGrid.Closed)
                            {
                                myCubeGrid.Hierarchy.QuerySphere(ref boundingSphereD, orAdd2);
                            }
                        }
                    }
                    _owners.Clear();
                    for (int j = Targets.Count - 1; j >= 0; j--)
                    {
                        MyEntity myEntity = Targets[j];
                        if (myEntity is MyFloatingObject || (myEntity.Physics != null && !myEntity.Physics.Enabled) || myEntity.GetTopMostParent(null).Physics == null || !myEntity.GetTopMostParent(null).Physics.Enabled)
                        {
                            Targets.RemoveAtFast(j);
                        }
                    }
                }
            }
            //_session.DsUtil.Complete("", false, true);
        }
        
        public override string ComponentTypeDebugString
        {
            get
            {
                return "MyGridTargeting";
            }
        }

        public new void RescanIfNeeded()
        {
            if (AllowScanning && _session.Tick - _lastScan > 100)
            {
                if (!_inited) Init();
                Scan();
            }
        }
    }
}
