using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using System;
using System.Collections.Concurrent;
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
        private BoundingSphere _scanningRange = new BoundingSphere(Vector3.Zero, float.MinValue);
        private List<MyEntity> _targetGrids = new List<MyEntity>();
        private Dictionary<MyCubeGrid, List<MyEntity>> _targetBlocks = new Dictionary<MyCubeGrid, List<MyEntity>>();
        private List<long> _users = new List<long>();
        private List<long> _owners = new List<long>();

        private FastResourceLock _selfLock = new FastResourceLock();

        private static ConcurrentDictionary<MyCubeGrid, FastResourceLock> _gridLocks = new ConcurrentDictionary<MyCubeGrid, FastResourceLock>();
        private static FastResourceLock _emergencyLock = new FastResourceLock();

        private uint _lastScan;
        public new bool AllowScanning = true;

        public override void OnAddedToContainer()
        {
            base.OnAddedToContainer();
            _myGrid = (base.Entity as MyCubeGrid);
            ((IMyCubeGrid)_myGrid).OnBlockAdded += m_grid_OnBlockAdded;
            _gridLocks.TryAdd(_myGrid, new FastResourceLock());
            Log.ThreadedWrite($"CoreTargeting Added - EntityId: {_myGrid.EntityId}");
        }

        public override void OnBeforeRemovedFromContainer()
        {
            if (_gridLocks.ContainsKey(_myGrid))
            {
                FastResourceLock removedLock;
                if (_gridLocks.TryRemove(_myGrid, out removedLock))
                {
                    ((IMyCubeGrid)_myGrid).OnBlockAdded -= m_grid_OnBlockAdded;
                    Log.ThreadedWrite($"CoreTargeting Removed - EntityId: {_myGrid.EntityId}");
                }
            }
        }

        private void m_grid_OnBlockAdded(IMySlimBlock obj)
        {
            IMyUpgradeModule myLargeTurretBaseCore = obj.FatBlock as IMyUpgradeModule;
            if (myLargeTurretBaseCore != null)
            {
                _scanningRange.Include(new BoundingSphere(obj.FatBlock.PositionComp.LocalMatrix.Translation, 1500f));
                myLargeTurretBaseCore.PropertiesChanged += TurretOnPropertiesChanged;
            }
            IMyLargeTurretBase myLargeTurretBase = obj.FatBlock as IMyLargeTurretBase;
            if (myLargeTurretBase != null)
            {
                _scanningRange.Include(new BoundingSphere(obj.FatBlock.PositionComp.LocalMatrix.Translation, myLargeTurretBase.Range));
                myLargeTurretBase.PropertiesChanged += TurretOnPropertiesChanged;
            }
        }

        private void TurretOnPropertiesChanged(IMyTerminalBlock obj)
        {
            IMyUpgradeModule myLargeTurretBaseCore = obj as IMyUpgradeModule;
            if (myLargeTurretBaseCore != null)
            {
                _scanningRange.Include(new BoundingSphere(obj.PositionComp.LocalMatrix.Translation, 1500f));
            }

            IMyLargeTurretBase myLargeTurretBase = obj as IMyLargeTurretBase;
            if (myLargeTurretBase != null)
            {
                _scanningRange.Include(new BoundingSphere(obj.PositionComp.LocalMatrix.Translation, myLargeTurretBase.Range));
            }

        }

        public new List<MyEntity> TargetRoots
        {
            get
            {
                if (AllowScanning && Session.Instance.Tick - _lastScan > 100)
                {
                    Scan();
                }
                MyLog.Default.WriteLine("Get Target Blocks");
                using (_selfLock)
                    return new List<MyEntity>(_targetGrids);
            }
        }

        public new IEnumerable<KeyValuePair<MyCubeGrid, List<MyEntity>>> TargetBlocks
        {
            get
            {
                if (AllowScanning && Session.Instance.Tick - _lastScan > 100)
                {
                    Scan();
                }
                using (_selfLock)
                    return new Dictionary<MyCubeGrid, List<MyEntity>>(_targetBlocks);
            }
        }

        private void Scan()
        {
            using (_selfLock.AcquireExclusiveUsing())
            {
                if (AllowScanning && Session.Instance.Tick - _lastScan > 100)
                {
                    _lastScan = Session.Instance.Tick;
                    BoundingSphereD boundingSphereD = new BoundingSphereD(Vector3D.Transform(_scanningRange.Center, _myGrid.WorldMatrix), (double)_scanningRange.Radius);
                    _targetGrids.Clear();
                    _targetBlocks.Clear();
                    MyGamePruningStructure.GetAllTopMostEntitiesInSphere(ref boundingSphereD, _targetGrids, MyEntityQueryType.Both);

                    //MyMissiles.GetAllMissilesInSphere(ref boundingSphereD, m_targetRoots);

                    int count = _targetGrids.Count;
                    _owners.AddRange(_myGrid.SmallOwners);
                    _owners.AddRange(_myGrid.BigOwners);
                    for (int i = 0; i < count; i++)
                    {
                        MyCubeGrid myCubeGrid = _targetGrids[i] as MyCubeGrid;
                        if (myCubeGrid != null && (myCubeGrid.Physics == null || myCubeGrid.Physics.Enabled))
                        {
                            FastResourceLock gridLock;
                            if (!_gridLocks.TryGetValue(myCubeGrid, out gridLock))
                            {
                                gridLock = _emergencyLock;
                            }

                            using (gridLock.AcquireExclusiveUsing())
                            {
                                bool flag = false;
                                if (myCubeGrid.BigOwners.Count == 0 && myCubeGrid.SmallOwners.Count == 0)
                                {
                                    using (List<long>.Enumerator enumerator = _owners.GetEnumerator())
                                    {
                                        while (enumerator.MoveNext())
                                        {
                                            if (MyIDModule.GetRelationPlayerBlock(enumerator.Current, 0L, MyOwnershipShareModeEnum.None, MyRelationsBetweenPlayerAndBlock.Enemies, MyRelationsBetweenFactions.Enemies, MyRelationsBetweenPlayerAndBlock.FactionShare) == MyRelationsBetweenPlayerAndBlock.NoOwnership)
                                            {
                                                flag = true;
                                                break;
                                            }
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
                                        if (flag)
                                        {
                                            break;
                                        }
                                    }
                                    _users.Clear();
                                }
                                if (flag)
                                {

                                    if (!_targetBlocks.ContainsKey(myCubeGrid))
                                        _targetBlocks[myCubeGrid] = new List<MyEntity>();

                                    var orAdd = _targetBlocks[myCubeGrid];

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
                                    IMyComponentOwner<MyIDModule> myComponentOwner = myCubeBlock as IMyComponentOwner<MyIDModule>;
                                    MyIDModule myIDModule;
                                    if (myComponentOwner != null && myComponentOwner.GetComponent(out myIDModule))
                                    {
                                        long ownerId = myCubeBlock.OwnerId;
                                        using (List<long>.Enumerator enumerator = _owners.GetEnumerator())
                                        {
                                            while (enumerator.MoveNext())
                                            {
                                                if (MyIDModule.GetRelationPlayerBlock(enumerator.Current, ownerId, MyOwnershipShareModeEnum.None, MyRelationsBetweenPlayerAndBlock.Enemies, MyRelationsBetweenFactions.Enemies, MyRelationsBetweenPlayerAndBlock.FactionShare) == MyRelationsBetweenPlayerAndBlock.Enemies)
                                                {
                                                    flag = true;
                                                    break;
                                                }
                                            }
                                        }
                                        if (flag)
                                        {
                                            break;
                                        }
                                    }
                                }
                                if (!flag)
                                {
                                    continue;
                                }
                                if (!_targetBlocks.ContainsKey(myCubeGrid))
                                    _targetBlocks[myCubeGrid] = new List<MyEntity>();

                                var orAdd2 = _targetBlocks[myCubeGrid];

                                if (!myCubeGrid.Closed)
                                {
                                    myCubeGrid.Hierarchy.QuerySphere(ref boundingSphereD, orAdd2);
                                    continue;
                                }
                                continue;
                            }
                        }
                    }
                    _owners.Clear();
                    for (int j = _targetGrids.Count - 1; j >= 0; j--)
                    {
                        MyEntity myEntity = _targetGrids[j];
                        if (myEntity is MyFloatingObject || (myEntity.Physics != null && !myEntity.Physics.Enabled) || myEntity.GetTopMostParent(null).Physics == null || !myEntity.GetTopMostParent(null).Physics.Enabled)
                        {
                            _targetGrids.RemoveAtFast(j);
                        }
                    }
                }
            }
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
            if (AllowScanning && Session.Instance.Tick - _lastScan > 100)
            {
                Scan();
            }
        }
    }
}
