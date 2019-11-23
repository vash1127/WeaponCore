using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.Entity;
using VRageMath;

namespace WeaponCore.Support
{
    public partial class GridAi
    {
        internal void Scan()
        {
            if (Scanning)
            {
                Log.Line("already scanning");
                return;
            }
            using (_scanLock.AcquireExclusiveUsing())
            {
                if (!Scanning && Session.Tick - _lastScan > 100)
                {
                    Scanning = true;
                    _lastScan = Session.Tick;
                    var boundingSphereD = MyGrid.PositionComp.WorldVolume;
                    boundingSphereD.Radius = MaxTargetingRange;
                    MyGamePruningStructure.GetAllTopMostEntitiesInSphere(ref boundingSphereD, _possibleTargets);

                    foreach (var grid in PrevSubGrids)
                        RemSubGrids.Add(grid);

                    PrevSubGrids.Clear();
                    ThreatsTmp.Clear();
                    TargetAisTmp.Clear();
                    for (int i = 0; i < _possibleTargets.Count; i++)
                    {
                        var ent = _possibleTargets[i];
                        using (ent.Pin())
                        {
                            if (ent is MyVoxelBase || ent.Physics == null || ent is MyFloatingObject
                                || ent.MarkedForClose || !ent.InScene || ent.IsPreview || ent.Physics.IsPhantom) continue;

                            var grid = ent as MyCubeGrid;
                            if (grid != null && MyGrid.IsSameConstructAs(grid))
                            {
                                PrevSubGrids.Add(grid);
                                continue;
                            }

                            Sandbox.ModAPI.Ingame.MyDetectedEntityInfo entInfo;
                            if (!CreateEntInfo(ent, MyOwner, out entInfo)) continue;

                            switch (entInfo.Relationship)
                            {
                                case MyRelationsBetweenPlayerAndBlock.Owner:
                                    continue;
                                case MyRelationsBetweenPlayerAndBlock.FactionShare:
                                    continue;
                            }
                            if (grid != null)
                            {
                                FatMap fatMap;
                                if (!Session.GridToFatMap.TryGetValue(grid, out fatMap) || fatMap.Trash)
                                {
                                    //Log.Line($"fatmap not count for: {grid.DebugName} - isTrash:{fatMap.Trash} - count:{fatMap.MyCubeBocks.Count}");
                                    continue;
                                }

                                var allFat = fatMap.MyCubeBocks;
                                var fatCount = allFat.Count;

                                if (fatCount <= 0 || !grid.IsPowered)
                                    continue;

                                if (fatCount <= 20) // possible debris
                                {
                                    var valid = false;
                                    for (int j = 0; j < fatCount; j++)
                                    {
                                        var fat = allFat[j];
                                        if (fat is IMyTerminalBlock && fat.IsWorking)
                                        {
                                            valid = true;
                                            break;
                                        }
                                        //Log.Line($"invalidBlock:{fat.DebugName} - of:{fatCount} - isWorking:{fat.IsWorking} - blockId:{((IMySlimBlock)fat.SlimBlock).BlockDefinition.Id.SubtypeName}");
                                    }
                                    if (!valid) continue;
                                }

                                NewEntities.Add(new DetectInfo(Session, ent, entInfo, fatMap.MostBlocks));
                                ValidGrids.Add(ent);
                                GridAi targetAi;
                                if (Session.GridTargetingAIs.TryGetValue(grid, out targetAi))
                                {
                                    targetAi.ThreatsTmp.Add(this);
                                    TargetAisTmp.Add(targetAi);
                                }
                            }
                            else NewEntities.Add(new DetectInfo(Session, ent, entInfo, 1));
                        }
                    }
                    FinalizeTargetDb();
                    SubGridDetect();
                }
                Scanning = false;
            }
        }

        private void FinalizeTargetDb()
        {
            MyPlanetTmp = MyGamePruningStructure.GetClosestPlanet(GridCenter);
            ShieldNearTmp = false;
            ObstructionsTmp.Clear();
            StaticsInRangeTmp.Clear();
            for (int i = 0; i < _possibleTargets.Count; i++)
            {
                var ent = _possibleTargets[i];
                var hasPhysics = ent.Physics != null;
                if (Session.ShieldApiLoaded && !hasPhysics && !ent.IsPreview && !ent.Render.CastShadows)
                {
                    long testId;
                    long.TryParse(ent.Name, out testId);
                    if (testId != 0)
                    {
                        MyEntity shieldEnt;
                        if (testId == MyGrid.EntityId) MyShieldTmp = ent;
                        else if (MyEntities.TryGetEntityById(testId, out shieldEnt))
                        {
                            var shieldGrid = shieldEnt as MyCubeGrid;
                            if (shieldGrid != null && MyGrid.IsSameConstructAs(shieldGrid)) MyShieldTmp = ent;
                        }
                        else if (!ShieldNearTmp) ShieldNearTmp = true;
                    }
                }
                var voxel = ent as MyVoxelBase;
                var grid = ent as MyCubeGrid;
                var blockingThings = ent.Physics != null && (voxel != null && voxel.RootVoxel == voxel || grid != null);
                if (!blockingThings) continue;
                if (ent.Physics.IsStatic)
                {
                    if (voxel != null && MyPlanetTmp != null && MyPlanetTmp.PositionComp.WorldAABB.Contains(voxel.PositionComp.WorldVolume) == ContainmentType.Contains)
                        continue;
                    StaticsInRangeTmp.Add(ent);
                }

                if (grid != null && (PrevSubGrids.Contains(grid) || ValidGrids.Contains(ent) || grid.PositionComp.LocalVolume.Radius < 6)) continue;
                ObstructionsTmp.Add(ent);
            }
            ValidGrids.Clear();
            _possibleTargets.Clear();
        }
    }
}
