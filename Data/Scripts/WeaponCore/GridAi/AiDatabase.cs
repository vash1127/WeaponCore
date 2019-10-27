using Sandbox.Game.Entities;
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
                    _possibleTargets.Clear();
                    MyGamePruningStructure.GetAllTopMostEntitiesInSphere(ref boundingSphereD, _possibleTargets);
                    for (int i = 0; i < _possibleTargets.Count; i++)
                    {
                        var ent = _possibleTargets[i];
                        using (ent.Pin())
                        {
                            Sandbox.ModAPI.Ingame.MyDetectedEntityInfo entInfo;
                            if (ent == MyGrid || ent is MyVoxelBase || ent.Physics == null || ent is MyFloatingObject
                                || ent.MarkedForClose || !ent.InScene || ent.IsPreview || ent.Physics.IsPhantom || !CreateEntInfo(ent, MyOwner, out entInfo)) continue;

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
                                if (MyGrid.IsSameConstructAs(grid))
                                {
                                    SubGridsTmp.Add(grid);
                                    continue;
                                }

                                if (!grid.IsPowered)
                                    continue;

                                NewEntities.Add(new DetectInfo(Session, ent, entInfo));
                                ValidGrids.Add(ent);
                                GridAi targetAi;
                                if (Session.GridTargetingAIs.TryGetValue(grid, out targetAi))
                                {
                                    targetAi.ThreatsTmp.Add(this);
                                    TargetAisTmp.Add(targetAi);
                                }
                            }
                            else NewEntities.Add(new DetectInfo(Session, ent, entInfo));
                        }
                    }
                    FinalizeTargetDb();
                }
                Scanning = false;
            }
        }

        private void FinalizeTargetDb()
        {
            MyPlanetTmp = MyGamePruningStructure.GetClosestPlanet(GridCenter);
            ShieldNearTmp = false;
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
                if (grid != null && grid.IsSameConstructAs(MyGrid) || ValidGrids.Contains(ent) || ent.PositionComp.LocalVolume.Radius < 6) continue;
                ObstructionsTmp.Add(ent);
            }
            ValidGrids.Clear();
        }
    }
}
