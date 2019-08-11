using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using static WeaponCore.Support.SubSystemDefinition;

namespace WeaponCore.Support
{
    public partial class GridAi
    {
        internal void UpdateTargetDb()
        {
            NewEntities.Clear();

            Targeting.AllowScanning = true;
            Targeting.RescanIfNeeded();
            foreach (var ent in Targeting.TargetRoots)
            {
                if (ent == null)  continue;
                //using (ent.Pin())
                {
                    Sandbox.ModAPI.Ingame.MyDetectedEntityInfo entInfo;
                    if (ent == MyGrid || ent is MyVoxelBase || ent.Physics == null || ent is IMyFloatingObject
                        || ent.MarkedForClose || !ent.InScene || ent.IsPreview || ent.Physics.IsPhantom || !CreateEntInfo(ent, MyOwner, out entInfo)) continue;

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
                    if (grid != null)
                    {
                        if (grid.MarkedForClose || !grid.InScene || grid.Physics?.Speed < 10 && !grid.IsPowered || grid.CubeBlocks.Count < 2 && !((grid.CubeBlocks.FirstElement() as IMySlimBlock)?.FatBlock is Sandbox.ModAPI.IMyWarhead))
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
                    }
                    else NewEntities.Add(new DetectInfo(ent, null, entInfo));
                }
            }
            GetTargetBlocks(Targeting, this);
            Targeting.AllowScanning = false;
            ValidGrids.Clear();
        }

        private static void GetTargetBlocks(MyGridTargeting targeting, GridAi ai)
        {
            IEnumerable<KeyValuePair<MyCubeGrid, List<MyEntity>>> allTargets = targeting.TargetBlocks;
            foreach (var targets in allTargets)
            {
                //Log.Line($"{targets.Key.DebugName} - {targets.Value.Count}");
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
                            else if (cube is IMyGunBaseUser) typeDict[BlockTypes.Offense].Add(cube);
                            else if (cube is IMyUpgradeModule) typeDict[BlockTypes.Defense].Add(cube);
                            else if (cube is IMyThrust || cube is IMyJumpDrive) typeDict[BlockTypes.Navigation].Add(cube);
                        }
                    }
                }
            }
        }
    }
}
