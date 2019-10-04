using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.Entities.Debris;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.Weapons;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Library.Collections;
using VRageMath;

namespace WeaponCore.Support
{
    public class CoreTargeting : MyGridTargeting
    {

        private MyCubeGrid m_grid;
        private BoundingSphere m_queryLocal = new BoundingSphere(Vector3.Zero, float.MinValue);
        private List<MyEntity> m_targetRoots = new List<MyEntity>();
        private Dictionary<MyCubeGrid, List<MyEntity>> m_targetBlocks = new Dictionary<MyCubeGrid, List<MyEntity>>();
        private List<long> m_ownersB = new List<long>();
        private List<long> m_ownersA = new List<long>();

        private static FastResourceLock m_scanLock = new FastResourceLock();

        private uint m_lastScan;
        public new bool AllowScanning = true;

        public override void OnAddedToContainer()
        {
            base.OnAddedToContainer();
            this.m_grid = (base.Entity as MyCubeGrid);
            ((IMyCubeGrid)this.m_grid).OnBlockAdded += this.m_grid_OnBlockAdded;
        }

        private void m_grid_OnBlockAdded(IMySlimBlock obj)
        {
            IMyUpgradeModule myLargeTurretBase = obj.FatBlock as IMyUpgradeModule;
            if (myLargeTurretBase != null)
            {
                this.m_queryLocal.Include(new BoundingSphere(obj.FatBlock.PositionComp.LocalMatrix.Translation, 10000f));
                myLargeTurretBase.PropertiesChanged += this.TurretOnPropertiesChanged;
            }
        }

        private void TurretOnPropertiesChanged(IMyTerminalBlock obj)
        {
            IMyUpgradeModule myLargeTurretBase = obj as IMyUpgradeModule;
            if (myLargeTurretBase != null)
            {
                this.m_queryLocal.Include(new BoundingSphere(obj.PositionComp.LocalMatrix.Translation, 10000f));
            }
        }

        public new List<MyEntity> TargetRoots
        {
            get
            {
                if (this.AllowScanning && Session.Instance.Tick - this.m_lastScan > 100)
                {
                    this.Scan();
                }
                return this.m_targetRoots;
            }
        }

        public new IEnumerable<KeyValuePair<MyCubeGrid, List<MyEntity>>> TargetBlocks
        {
            get
            {
                if (this.AllowScanning && Session.Instance.Tick - this.m_lastScan > 100)
                {
                    this.Scan();
                }
                return this.m_targetBlocks;
            }
        }

        private void Scan()
        {
            using (CoreTargeting.m_scanLock.AcquireExclusiveUsing())
            {
                if (this.AllowScanning && Session.Instance.Tick - this.m_lastScan > 100)
                {
                    this.m_lastScan = Session.Instance.Tick;
                    BoundingSphereD boundingSphereD = new BoundingSphereD(Vector3D.Transform(this.m_queryLocal.Center, this.m_grid.WorldMatrix), (double)this.m_queryLocal.Radius);
                    this.m_targetRoots.Clear();
                    this.m_targetBlocks.Clear();
                    MyGamePruningStructure.GetAllTopMostEntitiesInSphere(ref boundingSphereD, this.m_targetRoots, MyEntityQueryType.Both);

                    //MyMissiles.GetAllMissilesInSphere(ref boundingSphereD, this.m_targetRoots);

                    int count = this.m_targetRoots.Count;
                    this.m_ownersA.AddRange(this.m_grid.SmallOwners);
                    this.m_ownersA.AddRange(this.m_grid.BigOwners);
                    for (int i = 0; i < count; i++)
                    {
                        MyCubeGrid myCubeGrid = this.m_targetRoots[i] as MyCubeGrid;
                        if (myCubeGrid != null && (myCubeGrid.Physics == null || myCubeGrid.Physics.Enabled))
                        {
                            bool flag = false;
                            if (myCubeGrid.BigOwners.Count == 0 && myCubeGrid.SmallOwners.Count == 0)
                            {
                                using (List<long>.Enumerator enumerator = this.m_ownersA.GetEnumerator())
                                {
                                    while (enumerator.MoveNext())
                                    {
                                        if (MyIDModule.GetRelationPlayerBlock(enumerator.Current, 0L, MyOwnershipShareModeEnum.None, MyRelationsBetweenPlayerAndBlock.Enemies, MyRelationsBetweenFactions.Enemies, MyRelationsBetweenPlayerAndBlock.FactionShare) == MyRelationsBetweenPlayerAndBlock.NoOwnership)
                                        {
                                            flag = true;
                                            break;
                                        }
                                    }
                                    goto IL_221;
                                }
                            }
                            goto IL_175;
                            IL_221:
                            if (flag)
                            {
                                
                                if (!this.m_targetBlocks.ContainsKey(myCubeGrid))
                                    this.m_targetBlocks[myCubeGrid] = new List<MyEntity>();

                                var orAdd = this.m_targetBlocks[myCubeGrid];

                                using (myCubeGrid.Pin())
                                {
                                    if (!myCubeGrid.MarkedForClose)
                                    {
                                        myCubeGrid.Hierarchy.QuerySphere(ref boundingSphereD, orAdd);
                                    }
                                    goto IL_334;
                                }
                            }
                            foreach (MyCubeBlock myCubeBlock in myCubeGrid.GetFatBlocks())
                            {
                                IMyComponentOwner<MyIDModule> myComponentOwner = myCubeBlock as IMyComponentOwner<MyIDModule>;
                                MyIDModule myIDModule;
                                if (myComponentOwner != null && myComponentOwner.GetComponent(out myIDModule))
                                {
                                    long ownerId = myCubeBlock.OwnerId;
                                    using (List<long>.Enumerator enumerator = this.m_ownersA.GetEnumerator())
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
                                goto IL_334;
                            }
                            if (!this.m_targetBlocks.ContainsKey(myCubeGrid))
                                this.m_targetBlocks[myCubeGrid] = new List<MyEntity>();

                            var orAdd2 = this.m_targetBlocks[myCubeGrid];

                            if (!myCubeGrid.Closed)
                            {
                                myCubeGrid.Hierarchy.QuerySphere(ref boundingSphereD, orAdd2);
                                goto IL_334;
                            }
                            goto IL_334;
                            IL_175:
                            this.m_ownersB.AddRange(myCubeGrid.BigOwners);
                            this.m_ownersB.AddRange(myCubeGrid.SmallOwners);
                            foreach (long owner in this.m_ownersA)
                            {
                                foreach (long user in this.m_ownersB)
                                {
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
                            this.m_ownersB.Clear();
                            goto IL_221;
                        }
                        IL_334:;
                    }
                    this.m_ownersA.Clear();
                    for (int j = this.m_targetRoots.Count - 1; j >= 0; j--)
                    {
                        MyEntity myEntity = this.m_targetRoots[j];
                        if (myEntity is MyFloatingObject || (myEntity.Physics != null && !myEntity.Physics.Enabled) || myEntity.GetTopMostParent(null).Physics == null || !myEntity.GetTopMostParent(null).Physics.Enabled)
                        {
                            this.m_targetRoots.RemoveAtFast(j);
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
            if (this.AllowScanning && Session.Instance.Tick - this.m_lastScan > 100)
            {
                this.Scan();
            }
        }
    }
}
