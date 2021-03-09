using System;
using System.Collections.Concurrent;
using CoreSystems.Support;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage;
using VRage.Collections;
using VRage.Game;
using static CoreSystems.Support.WeaponDefinition.TargetingDef.BlockTypes;
namespace CoreSystems
{
    public class GridMap
    {
        public ConcurrentCachingList<MyCubeBlock> MyCubeBocks;
        public MyGridTargeting Targeting;
        public volatile bool Trash;
        public int MostBlocks;
        internal void Clean()
        {
            Targeting = null;
            MyCubeBocks.ClearImmediate();
        }
    }

    internal struct DeferedTypeCleaning
    {
        internal uint RequestTick;
        internal ConcurrentDictionary<WeaponDefinition.TargetingDef.BlockTypes, ConcurrentCachingList<MyCubeBlock>> Collection;
    }

    public partial class Session
    {

        public void UpdateDbsInQueue()
        {
            DbUpdating = true;

            if (DbTask.IsComplete && DbTask.valid && DbTask.Exceptions != null)
                TaskHasErrors(ref DbTask, "DbTask");

            DbTask = MyAPIGateway.Parallel.StartBackground(ProcessDbs, ProcessDbsCallBack);
        }

        private void ProcessDbs()
        {
            for (int i = 0; i < DbsToUpdate.Count; i++) {

                var db = DbsToUpdate[i];
                using (db.Ai.DbLock.AcquireExclusiveUsing())  {

                    var ai = db.Ai;
                    if (!ai.MarkedForClose && !ai.Closed && ai.Version == db.Version)
                        ai.Scan();
                }
            }
        }

        private void ProcessDbsCallBack()
        {
            try
            {
                DsUtil.Start("db");
                for (int d = 0; d < DbsToUpdate.Count; d++)
                {
                    var db = DbsToUpdate[d];
                    using (db.Ai.DbLock.AcquireExclusiveUsing())
                    {
                        var ai = db.Ai;
                        if (ai.TopEntity.MarkedForClose || ai.MarkedForClose || db.Version != ai.Version) {
                            ai.ScanInProgress = false;
                            continue;
                        }

                        ai.DetectionInfo.Clean();

                        if (ai.MyPlanetTmp != null)
                            ai.MyPlanetInfo();

                        foreach (var sub in ai.PrevSubGrids) ai.SubGrids.Add((MyCubeGrid) sub);
                        if (ai.SubGridsChanged) ai.SubGridChanges(false, true);

                        ai.CleanSortedTargets();
                        ai.Targets.Clear();

                        var newEntCnt = ai.NewEntities.Count;
                        ai.SortedTargets.Capacity = newEntCnt;
                        for (int i = 0; i < newEntCnt; i++)
                        {
                            var detectInfo = ai.NewEntities[i];
                            var ent = detectInfo.Parent;
                            if (ent.Physics == null) continue;

                            var grid = ent as MyCubeGrid;
                            Ai targetAi = null;

                            if (grid != null)
                                GridAIs.TryGetValue(grid, out targetAi);

                            var targetInfo = TargetInfoPool.Get();
                            targetInfo.Init(ref detectInfo, ai, targetAi);

                            ai.SortedTargets.Add(targetInfo);
                            ai.Targets[ent] = targetInfo;

                            var checkFocus = ai.Construct.Data.Repo.FocusData.HasFocus && targetInfo.Target?.EntityId == ai.Construct.Data.Repo.FocusData.Target[0] || targetInfo.Target?.EntityId == ai.Construct.Data.Repo.FocusData.Target[1];

                            if (ai.RamProtection && targetInfo.DistSqr < 136900 && targetInfo.IsGrid)
                                ai.RamProximity = true;

                            if (targetInfo.DistSqr < ai.MaxTargetingRangeSqr && (checkFocus || targetInfo.OffenseRating > 0))
                            {
                                if (checkFocus || targetInfo.DistSqr < ai.DetectionInfo.PriorityRangeSqr && targetInfo.EntInfo.Relationship == MyRelationsBetweenPlayerAndBlock.Enemies)
                                {
                                    ai.DetectionInfo.PriorityInRange = true;
                                    ai.DetectionInfo.PriorityRangeSqr = targetInfo.DistSqr;
                                }

                                if (checkFocus || targetInfo.DistSqr < ai.DetectionInfo.OtherRangeSqr && targetInfo.EntInfo.Relationship != MyRelationsBetweenPlayerAndBlock.Enemies)
                                {
                                    ai.DetectionInfo.OtherInRange = true;
                                    ai.DetectionInfo.OtherRangeSqr = targetInfo.DistSqr;
                                }
                            }
                        }

                        ai.NewEntities.Clear();
                        ai.SortedTargets.Sort(TargetCompare);
                        ai.TargetAis.Clear();
                        ai.TargetAis.AddRange(ai.TargetAisTmp);
                        ai.TargetAisTmp.Clear();

                        ai.Obstructions.Clear();
                        ai.Obstructions.AddRange(ai.ObstructionsTmp);
                        ai.ObstructionsTmp.Clear();

                        ai.MyShield = null;
                        ai.ShieldNear = false;
                        ai.FriendlyShieldNear = false;
                        if (ai.NearByShieldsTmp.Count > 0)
                            ai.NearByShield();

                        ai.StaticsInRange.Clear();
                        ai.StaticsInRange.AddRange(ai.StaticsInRangeTmp);
                        ai.StaticsInRangeTmp.Clear();
                        ai.StaticEntitiesInRange = ai.StaticsInRange.Count > 0;
                        ai.MyStaticInfo();

                        ai.NaturalGravity = ai.FakeShipController.GetNaturalGravity();
                        ai.PartCount = ai.IsGrid ? ai.GridEntity.BlocksCount : 1;
                        ai.NearByEntities = ai.NearByEntitiesTmp;

                        if (!ai.DetectionInfo.PriorityInRange && ai.LiveProjectile.Count > 0)
                        {
                            ai.DetectionInfo.PriorityInRange = true;
                            ai.DetectionInfo.PriorityRangeSqr = 0;
                        }

                        ai.DetectionInfo.SomethingInRange = ai.DetectionInfo.PriorityInRange || ai.DetectionInfo.OtherInRange;

                        ai.DbReady = ai.SortedTargets.Count > 0 || ai.TargetAis.Count > 0 || Tick - ai.LiveProjectileTick < 3600 || ai.LiveProjectile.Count > 0 || ai.Construct.RootAi.Data.Repo.ControllingPlayers.Count > 0 || ai.FirstRun;

                        MyCubeBlock activeCube;
                        ai.AiSleep = ai.Construct.RootAi.Data.Repo.ControllingPlayers.Count <= 0 && (!ai.DetectionInfo.PriorityInRange && !ai.DetectionInfo.OtherInRange || !ai.DetectOtherSignals && ai.DetectionInfo.OtherInRange) && (ai.Data.Repo.ActiveTerminal <= 0 || MyEntities.TryGetEntityById(ai.Data.Repo.ActiveTerminal, out activeCube) && activeCube != null && !ai.SubGrids.Contains(activeCube.CubeGrid));

                        ai.DbUpdated = true;
                        ai.FirstRun = false;
                        ai.ScanInProgress = false;
                    }
                }
                DbsToUpdate.Clear();
                DsUtil.Complete("db", true);
                DbUpdating = false;
            }
            catch (Exception ex) { Log.Line($"Exception in ProcessDbsCallBack: {ex}", null, true); }
        }

        internal void CheckDirtyGridInfos()
        {
            if (!NewGrids.IsEmpty)
                AddGridToMap();

            if ((!GameLoaded || Tick20) && DirtyGridInfos.Count > 0)
            {
                if (GridTask.valid && GridTask.Exceptions != null)
                    TaskHasErrors(ref GridTask, "GridTask");
                if (!GameLoaded) UpdateGrids();
                else GridTask = MyAPIGateway.Parallel.StartBackground(UpdateGrids);
            }
        }

        private void UpdateGrids()
        {
            DeferedUpBlockTypeCleanUp();

            DirtyGridsTmp.Clear();
            DirtyGridsTmp.AddRange(DirtyGridInfos);
            DirtyGridInfos.Clear();
            for (int i = 0; i < DirtyGridsTmp.Count; i++) {
                var grid = DirtyGridsTmp[i];
                var newTypeMap = BlockTypePool.Get();
                newTypeMap[Offense] = ConcurrentListPool.Get();
                newTypeMap[Utility] = ConcurrentListPool.Get();
                newTypeMap[Thrust] = ConcurrentListPool.Get();
                newTypeMap[Steering] = ConcurrentListPool.Get();
                newTypeMap[Jumping] = ConcurrentListPool.Get();
                newTypeMap[Power] = ConcurrentListPool.Get();
                newTypeMap[Production] = ConcurrentListPool.Get();

                ConcurrentDictionary<WeaponDefinition.TargetingDef.BlockTypes, ConcurrentCachingList<MyCubeBlock>> noFatTypeMap;

                GridMap gridMap;
                if (GridToInfoMap.TryGetValue(grid, out gridMap)) {
                    var allFat = gridMap.MyCubeBocks;
                    var terminals = 0;
                    var tStatus = gridMap.Targeting == null || gridMap.Targeting.AllowScanning;
                    for (int j = 0; j < allFat.Count; j++) {
                        var fat = allFat[j];
                        if (!(fat is IMyTerminalBlock)) continue;
                        terminals++;
                        using (fat.Pin()) {


                            if (fat.MarkedForClose) continue;
                            var cockpit = fat as MyCockpit;
                            var decoy = fat as IMyDecoy;

                            if (decoy != null)
                            {
                                WeaponDefinition.TargetingDef.BlockTypes type;
                                if (DecoyMap.TryGetValue(fat, out type))
                                    newTypeMap[type].Add(fat);
                                else
                                {
                                    newTypeMap[Utility].Add(fat);
                                    DecoyMap[fat] = Utility;
                                }
                                continue;
                            }


                            if (fat is IMyProductionBlock) newTypeMap[Production].Add(fat);
                            else if (fat is IMyPowerProducer) newTypeMap[Power].Add(fat);
                            else if (fat is IMyGunBaseUser || fat is IMyWarhead || fat is MyConveyorSorter && PartPlatforms.ContainsKey(fat.BlockDefinition.Id))
                            {
                                if (!tStatus && fat is IMyGunBaseUser && !PartPlatforms.ContainsKey(fat.BlockDefinition.Id))
                                    tStatus = gridMap.Targeting.AllowScanning = true;

                                newTypeMap[Offense].Add(fat);
                            }
                            else if (fat is IMyUpgradeModule || fat is IMyRadioAntenna || cockpit != null && cockpit.EnableShipControl || fat is MyRemoteControl || fat is IMyShipGrinder || fat is IMyShipDrill) newTypeMap[Utility].Add(fat);
                            else if (fat is MyThrust) newTypeMap[Thrust].Add(fat);
                            else if (fat is MyGyro) newTypeMap[Steering].Add(fat);
                            else if (fat is MyJumpDrive) newTypeMap[Jumping].Add(fat);
                        }
                    }

                    foreach (var type in newTypeMap)
                        type.Value.ApplyAdditions();
                    
                    gridMap.MyCubeBocks.ApplyAdditions();

                    gridMap.Trash = terminals == 0;
                    var gridBlocks = grid.BlocksCount;
                    if (gridBlocks > gridMap.MostBlocks) gridMap.MostBlocks = gridBlocks;
                    ConcurrentDictionary<WeaponDefinition.TargetingDef.BlockTypes, ConcurrentCachingList<MyCubeBlock>> oldTypeMap; 
                    if (GridToBlockTypeMap.TryGetValue(grid, out oldTypeMap)) {
                        GridToBlockTypeMap[grid] = newTypeMap;
                        BlockTypeCleanUp.Enqueue(new DeferedTypeCleaning {Collection = oldTypeMap, RequestTick = Tick});
                    }
                    else GridToBlockTypeMap[grid] = newTypeMap;
                }
                else if (GridToBlockTypeMap.TryRemove(grid, out noFatTypeMap))
                    BlockTypeCleanUp.Enqueue(new DeferedTypeCleaning { Collection = noFatTypeMap, RequestTick = Tick });
            }
            DirtyGridsTmp.Clear();
        }
    }
}
