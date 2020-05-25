using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;

namespace WeaponCore.Support
{
    public partial class GridAi
    {
        internal void Scan()
        {
            using (_scanLock.AcquireExclusiveUsing()) {

                if (!Scanning && Session.Tick - _lastScan > 100) {

                    Scanning = true;
                    _lastScan = Session.Tick;
                    ScanVolume.Radius = MaxTargetingRange;
                    MyGamePruningStructure.GetAllTopMostEntitiesInSphere(ref ScanVolume, _possibleTargets);
                    foreach (var grid in PrevSubGrids)
                        RemSubGrids.Add(grid);

                    PrevSubGrids.Clear();
                    for (int i = 0; i < _possibleTargets.Count; i++) {

                        var ent = _possibleTargets[i];
                        using (ent.Pin()) {

                            if (ent is MyVoxelBase || ent.Physics == null || ent is MyFloatingObject
                                || ent.MarkedForClose || !ent.InScene || ent.IsPreview || ent.Physics.IsPhantom) continue;

                            var grid = ent as MyCubeGrid;
                            if (grid != null && MyGrid.IsSameConstructAs(grid)) {
                                PrevSubGrids.Add(grid);
                                continue;
                            }

                            Sandbox.ModAPI.Ingame.MyDetectedEntityInfo entInfo;
                            if (!CreateEntInfo(ent, MyOwner, out entInfo)) continue;

                            switch (entInfo.Relationship) {
                                case MyRelationsBetweenPlayerAndBlock.Owner:
                                case MyRelationsBetweenPlayerAndBlock.FactionShare:
                                case MyRelationsBetweenPlayerAndBlock.Friends:
                                    continue;
                            }

                            if (grid != null) {

                                FatMap fatMap;
                                if (!Session.GridToFatMap.TryGetValue(grid, out fatMap) || fatMap.Trash)
                                    continue;

                                var allFat = fatMap.MyCubeBocks;
                                var fatCount = allFat.Count;

                                if (fatCount <= 0 || !grid.IsPowered)
                                    continue;

                                if (fatCount <= 20)  { // possible debris

                                    var valid = false;
                                    for (int j = 0; j < fatCount; j++) {
                                        var fat = allFat[j];
                                        if (fat is IMyTerminalBlock && fat.IsWorking) {
                                            valid = true;
                                            break;
                                        }
                                    }
                                    if (!valid) continue;
                                }

                                int partCount;
                                GridAi targetAi;
                                if (Session.GridTargetingAIs.TryGetValue(grid, out targetAi)) {
                                    targetAi.TargetAisTmp.Add(this);
                                    TargetAisTmp.Add(targetAi);
                                    partCount = targetAi.Construct.BlockCount;
                                }
                                else 
                                    partCount = fatMap.MostBlocks;

                                NewEntities.Add(new DetectInfo(Session, ent, entInfo, partCount, fatCount));
                                ValidGrids.Add(ent);
                            }
                            else NewEntities.Add(new DetectInfo(Session, ent, entInfo, 1, 0));
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
            MyPlanetTmp = MyGamePruningStructure.GetClosestPlanet(ScanVolume.Center);
            ObstructionsTmp.Clear();
            StaticsInRangeTmp.Clear();
            for (int i = 0; i < _possibleTargets.Count; i++)
            {
                var ent = _possibleTargets[i];
                var hasPhysics = ent.Physics != null;
                if (Session.ShieldApiLoaded && hasPhysics && !ent.Physics.Enabled && ent.Physics.IsPhantom && !ent.Render.CastShadows && ent.Render.Visible)
                {
                    long testId;
                    if (long.TryParse(ent.Name, out testId))
                    {
                        var shieldblock = Session.SApi.MatchEntToShieldFast(ent, false);
                        if (shieldblock != null)
                        {
                            NearByShieldsTmp.Add(new Shields { Id = testId, ShieldEnt = ent, ShieldBlock = (MyCubeBlock)shieldblock});
                        }
                    }
                }
                var voxel = ent as MyVoxelBase;
                var grid = ent as MyCubeGrid;
                var blockingThings = ent.Physics != null && (voxel != null && voxel.RootVoxel == voxel || grid != null);
                if (!blockingThings) continue;
                if (ent.Physics.IsStatic)
                {

                    if (voxel is MyPlanet) continue;

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
