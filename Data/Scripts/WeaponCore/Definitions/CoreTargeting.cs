using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
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
        private static uint _lockClean;

        private uint _lastScan;
        public new bool AllowScanning = true;

        public override void OnAddedToContainer()
        {
            base.OnAddedToContainer();
            _myGrid = (base.Entity as MyCubeGrid);
            ((IMyCubeGrid)_myGrid).OnBlockAdded += OnBlockAdded;
            Log.ThreadedWrite($"CoreTargeting Added - EntityId: {_myGrid.EntityId}");

            FastResourceLock gridLock = _gridLocks.GetOrAdd(_myGrid, new FastResourceLock());
            using (gridLock.AcquireExclusiveUsing())
            {
                var existingBlocks = _myGrid.GetFatBlocks();
                if (_scanningRange.Radius == float.MinValue)
                {
                    for (int i = 0; i < existingBlocks.Count; i++)
                    {
                        if (existingBlocks[i] is IMyConveyorSorter)
                        {
                            if (!Session.Instance.WeaponPlatforms.ContainsKey(existingBlocks[i].BlockDefinition.Id.SubtypeId)) return;
                            _scanningRange.Include(new BoundingSphere(existingBlocks[i].PositionComp.LocalMatrix.Translation, 1500f));
                                ((IMyTerminalBlock)existingBlocks[i]).PropertiesChanged += TurretOnPropertiesChanged;
                        }
                        else if (existingBlocks[i] is IMyLargeTurretBase)
                        {
                            _scanningRange.Include(new BoundingSphere(existingBlocks[i].PositionComp.LocalMatrix.Translation, ((IMyLargeTurretBase)existingBlocks[i]).Range));
                            ((IMyTerminalBlock)existingBlocks[i]).PropertiesChanged += TurretOnPropertiesChanged;
                        }
                    }
                }
            }
        }

        public override void OnBeforeRemovedFromContainer()
        {
            base.OnBeforeRemovedFromContainer();
            ((IMyCubeGrid)_myGrid).OnBlockAdded -= OnBlockAdded;
        }

        private void OnBlockAdded(IMySlimBlock obj)
        {
            IMyConveyorSorter myLargeTurretBaseCore = obj.FatBlock as IMyConveyorSorter;
            if (myLargeTurretBaseCore != null)
            {
                if (!Session.Instance.WeaponPlatforms.ContainsKey(((MyCubeBlock)myLargeTurretBaseCore).BlockDefinition.Id.SubtypeId)) return;
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
            IMyConveyorSorter myLargeTurretBaseCore = obj as IMyConveyorSorter;
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

                    int count = _targetGrids.Count;
                    _owners.AddRange(_myGrid.SmallOwners);
                    _owners.AddRange(_myGrid.BigOwners);
                    for (int i = 0; i < count; i++)
                    {
                        MyCubeGrid myCubeGrid = _targetGrids[i] as MyCubeGrid;
                        if (myCubeGrid != null && (myCubeGrid.Physics == null || myCubeGrid.Physics.Enabled))
                        {
                            FastResourceLock gridLock = _gridLocks.GetOrAdd(myCubeGrid, new FastResourceLock());

                            using (gridLock.AcquireExclusiveUsing())
                            {
                                bool flag = false;
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
                                        for (int c = 0; c < _owners.Count; c++)
                                        {
                                            if (MyIDModule.GetRelationPlayerBlock(_owners[c], ownerId, MyOwnershipShareModeEnum.None, MyRelationsBetweenPlayerAndBlock.Enemies, MyRelationsBetweenFactions.Enemies, MyRelationsBetweenPlayerAndBlock.FactionShare) == MyRelationsBetweenPlayerAndBlock.Enemies)
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
                     
                    if (Session.Instance.Tick >= _lockClean) {
                        using (var enumerator = _gridLocks.GetEnumerator()) {
                            List<MyCubeGrid> gridsToRemove = new List<MyCubeGrid>();
                            while (enumerator.MoveNext()) {
                                if (enumerator.Current.Key.MarkedForClose)
                                    gridsToRemove.Add(enumerator.Current.Key);
                            }
                            for (int i = 0; i < gridsToRemove.Count; i++)
                            {
                                FastResourceLock disposeLock;
                                _gridLocks.TryRemove(gridsToRemove[i], out disposeLock);
                            }
                            gridsToRemove.Clear();
                        }
                        _lockClean = Session.Instance.Tick + 7200;
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
