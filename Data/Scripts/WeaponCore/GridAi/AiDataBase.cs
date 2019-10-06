using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Entity;
using VRageMath;
using static WeaponCore.Support.TargetingDefinition;

namespace WeaponCore.Support
{
    public partial class GridAi
    {
        internal void UpdateTargetDb()
        {
            Targeting.AllowScanning = true;
            foreach (var ent in Targeting.TargetRoots)
            {
                if (ent == null)  continue;
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
                        if (!grid.IsPowered || grid.CubeBlocks.Count < 2)
                            continue;

                        var typeDict = BlockTypePool.Get();
                        typeDict.Add(BlockTypes.Any, CubePool.Get());
                        typeDict.Add(BlockTypes.Offense, CubePool.Get());
                        typeDict.Add(BlockTypes.Utility, CubePool.Get());
                        typeDict.Add(BlockTypes.Thrust, CubePool.Get());
                        typeDict.Add(BlockTypes.Steering, CubePool.Get());
                        typeDict.Add(BlockTypes.Jumping, CubePool.Get());
                        typeDict.Add(BlockTypes.Power, CubePool.Get());
                        typeDict.Add(BlockTypes.Production, CubePool.Get());

                        NewEntities.Add(new DetectInfo(ent, typeDict, entInfo));
                        ValidGrids.Add(ent, typeDict);
                        GridAi targetAi;
                        if (Session.Instance.GridTargetingAIs.TryGetValue(grid, out targetAi))
                        {
                            targetAi.ThreatsTmp.Add(this);
                            TargetAisTmp.Add(targetAi);
                        }
                    }
                    else NewEntities.Add(new DetectInfo(ent, null, entInfo));
                }
            }
            GetTargetBlocks(Targeting, this);
            //Targeting.AllowScanning = false;
        }

        private static void GetTargetBlocks(CoreTargeting targeting, GridAi ai)
        {
            IEnumerable<KeyValuePair<MyCubeGrid, List<MyEntity>>> allTargets = targeting.TargetBlocks;
            foreach (var targets in allTargets)
            {
                var rootGrid = targets.Key;

                Dictionary<BlockTypes, List<MyCubeBlock>> typeDict;
                if (ai.ValidGrids.TryGetValue(rootGrid, out typeDict))
                {
                    for (int i = 0; i < targets.Value.Count; i++)
                    {
                        var cube = targets.Value[i] as MyCubeBlock;
                        if (cube != null && !cube.MarkedForClose)
                        {
                            typeDict[BlockTypes.Any].Add(cube);
                            if (cube is IMyProductionBlock) typeDict[BlockTypes.Production].Add(cube);
                            else if (cube is IMyPowerProducer) typeDict[BlockTypes.Power].Add(cube);
                            else if (cube is IMyGunBaseUser || cube is IMyWarhead) typeDict[BlockTypes.Offense].Add(cube);
                            else if (cube is IMyUpgradeModule || cube is IMyRadioAntenna) typeDict[BlockTypes.Utility].Add(cube);
                            else if (cube is MyThrust) typeDict[BlockTypes.Thrust].Add(cube);
                            else if (cube is MyGyro) typeDict[BlockTypes.Steering].Add(cube);
                            else if (cube is MyJumpDrive) typeDict[BlockTypes.Jumping].Add(cube);
                        }
                    }
                }
            }
        }

        internal void FinalizeTargetDb()
        {
            MyPlanetTmp = MyGamePruningStructure.GetClosestPlanet(GridCenter);
            var gridScanRadius = MaxTargetingRange + GridRadius;
            var sphere = new BoundingSphereD(GridCenter, gridScanRadius);
            EntitiesInRange.Clear();
            MyGamePruningStructure.GetAllTopMostEntitiesInSphere(ref sphere, EntitiesInRange);
            ShieldNearTmp = false;
            for (int i = 0; i < EntitiesInRange.Count; i++)
            {
                var ent = EntitiesInRange[i];
                var hasPhysics = ent.Physics != null;
                if (Session.Instance.ShieldApiLoaded && !hasPhysics && !ent.IsPreview && !ent.Render.CastShadows)
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
                var blockingThings =  ent.Physics != null && (voxel != null && voxel.RootVoxel == voxel || grid != null);
                if (!blockingThings) continue;
                if (ent.Physics.IsStatic)
                {
                    if (voxel != null && MyPlanetTmp != null && MyPlanetTmp.PositionComp.WorldAABB.Contains(voxel.PositionComp.WorldVolume) == ContainmentType.Contains)
                        continue;
                    StaticsInRangeTmp.Add(ent);
                }
                if (grid != null && grid.IsSameConstructAs(MyGrid) || ValidGrids.ContainsKey(ent) || ent.PositionComp.LocalVolume.Radius < 6) continue;
                ObstructionsTmp.Add(ent);
            }
            ValidGrids.Clear();
        }
    }
}
