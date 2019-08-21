using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;
using static WeaponCore.Support.TargetingDefinition;

namespace WeaponCore.Support
{
    public partial class GridAi
    {
        internal void UpdateTargetDb()
        {
            NewEntities.Clear();
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
                        if (grid.MarkedForClose || !grid.InScene || grid.Physics?.Speed < 10 && !grid.IsPowered || grid.CubeBlocks.Count < 2 || MyGrid.IsSameConstructAs(grid))
                            continue;

                        var typeDict = BlockTypePool.Get();
                        typeDict.Add(BlockTypes.Any, CubePool.Get());
                        typeDict.Add(BlockTypes.Offense, CubePool.Get());
                        typeDict.Add(BlockTypes.Defense, CubePool.Get());
                        typeDict.Add(BlockTypes.Navigation, CubePool.Get());
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
            Targeting.AllowScanning = false;
        }

        private static void GetTargetBlocks(MyGridTargeting targeting, GridAi ai)
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
                            else if (cube is IMyUpgradeModule) typeDict[BlockTypes.Defense].Add(cube);
                            else if (cube is IMyThrust || cube is IMyJumpDrive) typeDict[BlockTypes.Navigation].Add(cube);
                        }
                    }
                }
            }
        }

        internal void FinalizeTargetDb()
        {
            if (NewEntities.Count > 0)
            {
                var gridScanRadius = MaxTargetingRange + MyGrid.PositionComp.LocalVolume.Radius;
                var sphere = new BoundingSphereD(MyGrid.PositionComp.WorldAABB.Center, gridScanRadius);
                EntitiesInRange.Clear();
                MyGamePruningStructure.GetAllTopMostEntitiesInSphere(ref sphere, EntitiesInRange);
                for (int i = 0; i < EntitiesInRange.Count; i++)
                {
                    var ent = EntitiesInRange[i];
                    var hasPhysics = ent.Physics != null;
                    if (!hasPhysics && !ent.IsPreview)
                    {
                        var testId = Convert.ToInt64(ent.Name);
                        if (testId != 0 && testId == MyGrid.EntityId) MyShieldTmp = ent; 
                    } 
                    var blockingThings = (ent is MyVoxelBase || ent is MyCubeGrid && ent.Physics != null);
                    if (!blockingThings || ent == MyGrid || ValidGrids.ContainsKey(ent) || ent.PositionComp.LocalVolume.Radius < 6) continue;
                    ObstructionsTmp.Add(ent);
                }
            }
            ValidGrids.Clear();
        }
    }
}
