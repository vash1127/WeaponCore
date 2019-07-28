using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;

namespace WeaponCore.Support
{
    public partial class GridTargetingAi
    {
        internal void UpdateTargetDb()
        {
            NewEntities.Clear();

            Targeting.AllowScanning = true;
            foreach (var ent in Targeting.TargetRoots)
            {
                if (ent == null || ent == MyGrid || ent is MyVoxelBase || ent.Physics == null || ent is IMyFloatingObject 
                    || ent.MarkedForClose || ent.Physics.IsPhantom || !CreateEntInfo(ent, MyOwner, out var entInfo)) continue;

                switch (entInfo.Relationship)
                {
                    case MyRelationsBetweenPlayerAndBlock.Owner:
                        continue;
                    case MyRelationsBetweenPlayerAndBlock.FactionShare:
                        continue;
                    case MyRelationsBetweenPlayerAndBlock.NoOwnership:
                        if (!TargetNoOwners) continue;
                        break;
                    case MyRelationsBetweenPlayerAndBlock.Neutral:
                        if (!TargetNeutrals) continue;
                        break;
                }

                var grid = ent as MyCubeGrid;
                if (grid != null && !grid.MarkedForClose)
                {
                    var typeDict = BlockTypePool.Get();
                    typeDict.Add(BlockTypes.Any, CubePool.Get());
                    typeDict.Add(BlockTypes.Offense, CubePool.Get());
                    typeDict.Add(BlockTypes.Defense, CubePool.Get());
                    typeDict.Add(BlockTypes.Navigation, CubePool.Get());
                    typeDict.Add(BlockTypes.Power, CubePool.Get());
                    typeDict.Add(BlockTypes.Production, CubePool.Get());

                    NewEntities.Add(new DetectInfo(ent, typeDict, entInfo));
                    ValidGrids.Add(ent, typeDict);
                }
                else if (!ent.MarkedForClose) NewEntities.Add(new DetectInfo(ent, null, entInfo));
            }
            GetTargetBlocks(Targeting, this);
            Targeting.AllowScanning = false;
            ValidGrids.Clear();
        }

        private static void GetTargetBlocks(MyGridTargeting targeting, GridTargetingAi ai)
        {
            IEnumerable<KeyValuePair<MyCubeGrid, List<MyEntity>>> allTargets = targeting.TargetBlocks;
            foreach (var targets in allTargets)
            {
                var rootGrid = targets.Key;
                if (ai.ValidGrids.TryGetValue(rootGrid, out var typeDict))
                {
                    for (int i = 0; i < targets.Value.Count; i++)
                    {
                        var cube = targets.Value[i] as MyCubeBlock;
                        if (cube != null && !cube.MarkedForClose)
                        {
                            typeDict[BlockTypes.Any].Add(cube);
                            if (cube is IMyProductionBlock) typeDict[BlockTypes.Production].Add(cube);
                            else if (cube is IMyPowerProducer) typeDict[BlockTypes.Power].Add(cube);
                            else if (cube is IMyGunBaseUser) typeDict[BlockTypes.Offense].Add(cube);
                            else if (cube is IMyUpgradeModule) typeDict[BlockTypes.Defense].Add(cube);
                            else if (cube is IMyThrust || cube is IMyJumpDrive) typeDict[BlockTypes.Navigation].Add(cube);
                        }
                    }
                    if (rootGrid.GetFatBlocks().Count > 0 && typeDict[BlockTypes.Any].Count <= 0) Log.Line($"{rootGrid.DebugName} has no cubes in GetTargetBlocks");
                }
            }
        }
    }
}
