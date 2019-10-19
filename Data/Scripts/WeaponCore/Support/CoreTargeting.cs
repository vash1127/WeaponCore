using System;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using System.Collections.Generic;
using VRage;
using VRage.Game;
using VRage.Game.Entity;
using VRageMath;
using static WeaponCore.Support.TargetingDefinition.BlockTypes;

namespace WeaponCore.Support
{
    public class CoreTargeting : MyGridTargeting
    {
        private MyCubeGrid _myGrid;
        private readonly Session _session;
        private double _scanningRange = double.MinValue;
        internal readonly List<MyEntity> Targets = new List<MyEntity>();
        private static readonly FastResourceLock SelfLock = new FastResourceLock();
        private bool _inited;
        private uint _lastScan;
        public volatile bool Scanning = true;

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

        public void Scan()
        {
            using (SelfLock.AcquireExclusiveUsing())
            {
                if (Scanning && _session.Tick - _lastScan > 100)
                {
                    if (!_inited) Init();

                    GridAi gridAi;
                    if (_session.GridTargetingAIs.TryGetValue(_myGrid, out gridAi))
                    {
                        _lastScan = _session.Tick;
                        var boundingSphereD = _myGrid.PositionComp.WorldVolume;
                        boundingSphereD.Radius = _scanningRange;
                        Targets.Clear();
                        MyGamePruningStructure.GetAllTopMostEntitiesInSphere(ref boundingSphereD, Targets);
                        for (int i = 0; i < Targets.Count; i++)
                        {
                            var ent = Targets[i];
                            using (ent.Pin())
                            {
                                Sandbox.ModAPI.Ingame.MyDetectedEntityInfo entInfo;
                                if (ent == gridAi.MyGrid || ent is MyVoxelBase || ent.Physics == null || ent is MyFloatingObject
                                    || ent.MarkedForClose || !ent.InScene || ent.IsPreview || ent.Physics.IsPhantom || !gridAi.CreateEntInfo(ent, gridAi.MyOwner, out entInfo)) continue;

                                switch (entInfo.Relationship)
                                {
                                    case MyRelationsBetweenPlayerAndBlock.Owner:
                                        continue;
                                    case MyRelationsBetweenPlayerAndBlock.FactionShare:
                                        continue;
                                }
                                var grid = ent as MyCubeGrid;
                                if (grid != null)
                                {
                                    if (gridAi.MyGrid.IsSameConstructAs(grid))
                                    {
                                        gridAi.SubGridsTmp.Add(grid);
                                        continue;
                                    }
                                    if (!grid.IsPowered || grid.CubeBlocks.Count < 2)
                                        continue;

                                    var typeDict = gridAi.BlockTypePool.Get();
                                    var allFat = gridAi.CubePool.Get();

                                    var retries = 1;
                                    while (retries >= 0)
                                    {
                                        try
                                        {
                                            allFat.AddRange(grid.GetFatBlocks());
                                            break;
                                        }
                                        catch (Exception ex)
                                        {
                                            Log.Line($"Exception in CoreTargeting GetFat, retries:{retries}: {ex}");
                                            retries--;
                                        }
                                    }
                                    typeDict.Add(Any, allFat);
                                    typeDict.Add(Offense, gridAi.CubePool.Get());
                                    typeDict.Add(Utility, gridAi.CubePool.Get());
                                    typeDict.Add(Thrust, gridAi.CubePool.Get());
                                    typeDict.Add(Steering, gridAi.CubePool.Get());
                                    typeDict.Add(Jumping, gridAi.CubePool.Get());
                                    typeDict.Add(Power, gridAi.CubePool.Get());
                                    typeDict.Add(Production, gridAi.CubePool.Get());

                                    foreach (var cube in typeDict[Any])
                                    {
                                        if (cube == null) continue;
                                        using (cube.Pin())
                                        {
                                            if (cube.MarkedForClose) continue;
                                            if (cube is IMyProductionBlock) typeDict[Production].Add(cube);
                                            else if (cube is IMyPowerProducer) typeDict[Power].Add(cube);
                                            else if (cube is IMyGunBaseUser || cube is IMyWarhead) typeDict[Offense].Add(cube);
                                            else if (cube is IMyUpgradeModule || cube is IMyRadioAntenna) typeDict[Utility].Add(cube);
                                            else if (cube is MyThrust) typeDict[Thrust].Add(cube);
                                            else if (cube is MyGyro) typeDict[Steering].Add(cube);
                                            else if (cube is MyJumpDrive) typeDict[Jumping].Add(cube);
                                        }
                                    }
                                    gridAi.NewEntities.Add(new GridAi.DetectInfo(ent, typeDict, entInfo));
                                    gridAi.ValidGrids.Add(ent);
                                    GridAi targetAi;
                                    if (_session.GridTargetingAIs.TryGetValue(grid, out targetAi))
                                    {
                                        targetAi.ThreatsTmp.Add(gridAi);
                                        gridAi.TargetAisTmp.Add(targetAi);
                                    }
                                }
                                else gridAi.NewEntities.Add(new GridAi.DetectInfo(ent, null, entInfo));
                            }
                        }
                        FinalizeTargetDb(gridAi);
                    }
                    else Log.Line("nogridAi in CoreTargeting");
                }
            }
        }

        internal void FinalizeTargetDb(GridAi gridAi)
        {
            gridAi.MyPlanetTmp = MyGamePruningStructure.GetClosestPlanet(gridAi.GridCenter);
            gridAi.ShieldNearTmp = false;
            for (int i = 0; i < Targets.Count; i++)
            {
                var ent = Targets[i];
                var hasPhysics = ent.Physics != null;
                if (_session.ShieldApiLoaded && !hasPhysics && !ent.IsPreview && !ent.Render.CastShadows)
                {
                    long testId;
                    long.TryParse(ent.Name, out testId);
                    if (testId != 0)
                    {
                        MyEntity shieldEnt;
                        if (testId == gridAi.MyGrid.EntityId) gridAi.MyShieldTmp = ent;
                        else if (MyEntities.TryGetEntityById(testId, out shieldEnt))
                        {
                            var shieldGrid = shieldEnt as MyCubeGrid;
                            if (shieldGrid != null && gridAi.MyGrid.IsSameConstructAs(shieldGrid)) gridAi.MyShieldTmp = ent;
                        }
                        else if (!gridAi.ShieldNearTmp) gridAi.ShieldNearTmp = true;
                    }
                }
                var voxel = ent as MyVoxelBase;
                var grid = ent as MyCubeGrid;
                var blockingThings = ent.Physics != null && (voxel != null && voxel.RootVoxel == voxel || grid != null);
                if (!blockingThings) continue;
                if (ent.Physics.IsStatic)
                {
                    if (voxel != null && gridAi.MyPlanetTmp != null && gridAi.MyPlanetTmp.PositionComp.WorldAABB.Contains(voxel.PositionComp.WorldVolume) == ContainmentType.Contains)
                        continue;
                    gridAi.StaticsInRangeTmp.Add(ent);
                }
                if (grid != null && grid.IsSameConstructAs(gridAi.MyGrid) || gridAi.ValidGrids.Contains(ent) || ent.PositionComp.LocalVolume.Radius < 6) continue;
                gridAi.ObstructionsTmp.Add(ent);
            }
            gridAi.ValidGrids.Clear();
        }

        public override string ComponentTypeDebugString
        {
            get
            {
                return "MyGridTargeting";
            }
        }
    }
}
