using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using System;
using System.Collections.Concurrent;
using System.Threading;
using Sandbox.Game;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Entity;
using WeaponCore.Platform;
using WeaponCore.Support;
using static WeaponCore.Support.GridAi.Constructs;
using static WeaponCore.Support.WeaponDefinition.TargetingDef.BlockTypes;
namespace WeaponCore
{
    public class GridMap
    {
        public ConcurrentCachingList<MyCubeBlock> MyCubeBocks;
        public MyGridTargeting Targeting;
        public volatile bool Trash;
        public int MostBlocks;
        public bool SuspectedDrone;

        internal void Clean()
        {
            Targeting = null;
            MyCubeBocks.ClearImmediate();
            MostBlocks = 0;
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
                        if (ai.MyGrid.MarkedForClose || ai.MarkedForClose || db.Version != ai.Version) {
                            ai.ScanInProgress = false;
                            continue;
                        }


                        if (ai.MyPlanetTmp != null)
                            ai.MyPlanetInfo();

                        foreach (var sub in ai.PrevSubGrids) ai.SubGrids.Add((MyCubeGrid) sub);
                        if (ai.SubGridsChanged) ai.SubGridChanges(false, true);
                        
                        ai.TargetingInfo.Clean(ai);
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
                            GridAi targetAi = null;

                            if (grid != null)
                                GridTargetingAIs.TryGetValue(grid, out targetAi);

                            var targetInfo = TargetInfoPool.Get();
                            targetInfo.Init(ref detectInfo, ai.MyGrid, ai, targetAi);

                            ai.SortedTargets.Add(targetInfo);
                            ai.Targets[ent] = targetInfo;

                            var checkFocus = ai.Construct.Data.Repo.FocusData.HasFocus && targetInfo.Target?.EntityId == ai.Construct.Data.Repo.FocusData.Target[0] || targetInfo.Target?.EntityId == ai.Construct.Data.Repo.FocusData.Target[1];

                            if (targetInfo.Drone) 
                                ai.TargetingInfo.DroneAdd(ai, targetInfo);

                            if (ai.RamProtection && targetInfo.DistSqr < 136900 && targetInfo.IsGrid) 
                                ai.TargetingInfo.RamProximity = true;

                            if (targetInfo.DistSqr < ai.MaxTargetingRangeSqr && (checkFocus || targetInfo.OffenseRating > 0))
                            {
                                if (checkFocus || targetInfo.DistSqr < ai.TargetingInfo.ThreatRangeSqr && targetInfo.EntInfo.Relationship == MyRelationsBetweenPlayerAndBlock.Enemies)
                                {
                                    ai.TargetingInfo.ThreatInRange = true;
                                    ai.TargetingInfo.ThreatRangeSqr = targetInfo.DistSqr;
                                }

                                if (checkFocus || targetInfo.DistSqr < ai.TargetingInfo.OtherRangeSqr && targetInfo.EntInfo.Relationship != MyRelationsBetweenPlayerAndBlock.Enemies)
                                {
                                    ai.TargetingInfo.OtherInRange = true;
                                    ai.TargetingInfo.OtherRangeSqr = targetInfo.DistSqr;
                                }

                                if (targetInfo.Drone && targetInfo.DistSqr < ai.TargetingInfo.DroneRangeSqr)
                                {
                                    ai.TargetingInfo.DroneInRange = true;
                                    ai.TargetingInfo.DroneRangeSqr = targetInfo.DistSqr;
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
                        ai.BlockCount = ai.MyGrid.BlocksCount;
                        ai.NearByEntities = ai.NearByEntitiesTmp;

                        if (!ai.TargetingInfo.ThreatInRange && ai.LiveProjectile.Count > 0)
                        {
                            ai.TargetingInfo.ThreatInRange = true;
                            ai.TargetingInfo.ThreatRangeSqr = 0;
                        }

                        ai.TargetingInfo.SomethingInRange = ai.TargetingInfo.ThreatInRange || ai.TargetingInfo.OtherInRange;

                        ai.DbReady = ai.SortedTargets.Count > 0 || ai.TargetAis.Count > 0 || Tick - ai.LiveProjectileTick < 3600 || ai.LiveProjectile.Count > 0 || ai.Construct.RootAi.Data.Repo.ControllingPlayers.Count > 0 || ai.FirstRun;

                        MyCubeBlock activeCube;
                        ai.AiSleep = ai.Construct.RootAi.Data.Repo.ControllingPlayers.Count <= 0 && (!ai.TargetingInfo.ThreatInRange && !ai.TargetingInfo.OtherInRange || !ai.TargetNonThreats && ai.TargetingInfo.OtherInRange) && (ai.Data.Repo.ActiveTerminal <= 0 || MyEntities.TryGetEntityById(ai.Data.Repo.ActiveTerminal, out activeCube) && activeCube != null && !ai.SubGrids.Contains(activeCube.CubeGrid));

                        ai.DbUpdated = true;
                        ai.FirstRun = false;
                        ai.ScanInProgress = false;
                    }
                }
                DbsToUpdate.Clear();
                DsUtil.Complete("db", true);
                DbUpdating = false;
            }
            catch (Exception ex) { Log.Line($"Exception in ProcessDbsCallBack: {ex}"); }
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
                    var thrusters = 0;
                    var gyros = 0;
                    var powerProducers = 0;
                    var warHead = 0;

                    for (int j = 0; j < allFat.Count; j++) {
                        var fat = allFat[j];
                        if (!(fat is IMyTerminalBlock)) continue;
                        terminals++;
                        using (fat.Pin()) {

                            if (fat.MarkedForClose) continue;
                            var cockpit = fat as MyCockpit;
                            var decoy = fat as IMyDecoy;
                            var bomb = fat as IMyWarhead;

                            if (decoy != null) {
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
                            else if (fat is IMyPowerProducer)
                            {
                                newTypeMap[Power].Add(fat);
                                powerProducers++;
                            }
                            else if (fat is IMyGunBaseUser || bomb != null || fat is MyConveyorSorter && WeaponPlatforms.ContainsKey(fat.BlockDefinition.Id))
                            {
                                if (bomb != null)
                                    warHead++;

                                if (!tStatus && fat is IMyGunBaseUser && !WeaponPlatforms.ContainsKey(fat.BlockDefinition.Id))
                                    tStatus = gridMap.Targeting.AllowScanning = true;

                                newTypeMap[Offense].Add(fat);
                            }
                            else if (fat is IMyUpgradeModule || fat is IMyRadioAntenna || cockpit != null && cockpit.EnableShipControl || fat is MyRemoteControl || fat is IMyShipGrinder || fat is IMyShipDrill) newTypeMap[Utility].Add(fat);
                            else if (fat is MyThrust)
                            {
                                newTypeMap[Thrust].Add(fat);
                                thrusters++;
                            }
                            else if (fat is MyGyro)
                            {
                                newTypeMap[Steering].Add(fat);
                                gyros++;
                            }

                            else if (fat is MyJumpDrive) newTypeMap[Jumping].Add(fat);
                        }
                    }

                    foreach (var type in newTypeMap)
                        type.Value.ApplyAdditions();
                    
                    gridMap.MyCubeBocks.ApplyAdditions();
                    gridMap.SuspectedDrone = warHead > 0 || powerProducers > 0 && thrusters > 0 && gyros > 0;
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
