using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using System;
using System.Collections.Concurrent;
using Sandbox.Game;
using VRage;
using VRage.Collections;
using VRage.Game;
using WeaponCore.Platform;
using WeaponCore.Support;
using static WeaponCore.Support.GridAi.Constructs;
using static WeaponCore.Support.WeaponDefinition.TargetingDef.BlockTypes;
namespace WeaponCore
{
    public class FatMap
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
            if (DbTask.IsComplete && DbTask.valid && DbTask.Exceptions != null)
                TaskHasErrors(ref DbTask, "DbTask");

            DbTask = MyAPIGateway.Parallel.StartBackground(ProcessDbs, ProcessDbsCallBack);
        }

        private void ProcessDbs()
        {
            for (int i = 0; i < DbsToUpdate.Count; i++) {

                var db = DbsToUpdate[i];
                var ai = db.Ai;
                ai.ScanInProgress = true;

                lock (ai.AiLock) {
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
                    var ds = DbsToUpdate[d];
                    var ai = ds.Ai;
                    if (ai.MyGrid.MarkedForClose || ai.MarkedForClose || ds.Version != ai.Version) {
                        ai.ScanInProgress = false;
                        Log.Line($"[ProcessDbsCallBack] gridMarked: {ai.MyGrid.MarkedForClose} - aiMarked: {ai.MarkedForClose} - versionMismatch: {ds.Version != ai.Version}");
                        continue;
                    }

                    ai.TargetingInfo.Clean();

                    if (ai.MyPlanetTmp != null)
                        ai.MyPlanetInfo();

                    foreach (var sub in ai.PrevSubGrids) ai.SubGrids.Add(sub);
                    if (ai.SubGridsChanged) ai.SubGridChanges();

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

                        var checkFocus = ai.Focus.HasFocus && targetInfo.Target == ai.Focus.Target[0] || targetInfo.Target == ai.Focus.Target[1];

                        if (ai.RamProtection && targetInfo.DistSqr < 136900 && targetInfo.IsGrid)
                            ai.RamProximity = true;

                        if (targetInfo.DistSqr < ai.MaxTargetingRangeSqr && (checkFocus || targetInfo.OffenseRating > 0))
                        {
                            if (checkFocus || targetInfo.DistSqr < ai.TargetingInfo.ThreatRangeSqr && targetInfo.EntInfo.Relationship == MyRelationsBetweenPlayerAndBlock.Enemies) {
                                ai.TargetingInfo.ThreatInRange = true;
                                ai.TargetingInfo.ThreatRangeSqr = targetInfo.DistSqr;
                            }

                            if (checkFocus || targetInfo.DistSqr < ai.TargetingInfo.OtherRangeSqr && targetInfo.EntInfo.Relationship != MyRelationsBetweenPlayerAndBlock.Enemies) {
                                ai.TargetingInfo.OtherInRange = true;
                                ai.TargetingInfo.OtherRangeSqr = targetInfo.DistSqr;
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

                    if (ai.PlanetSurfaceInRange) ai.StaticsInRangeTmp.Add(ai.MyPlanet);
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

                    if (ai.ScanBlockGroups) ai.Construct.UpdateConstruct(UpdateType.BlockScan);
                    if (ai.ScanBlockGroupSettings) ai.Construct.UpdateConstruct(UpdateType.Overrides);

                    
                    ai.DbReady = ai.SortedTargets.Count > 0 || ai.TargetAis.Count > 0 || Tick - ai.LiveProjectileTick < 3600 || ai.LiveProjectile.Count > 0 || ai.Construct.RootAi.ControllingPlayers.Count > 0 || ai.FirstRun;

                    ai.AiSleep = ai.Construct.RootAi.ControllingPlayers.Count <= 0 && (!ai.TargetingInfo.ThreatInRange && !ai.TargetingInfo.OtherInRange || !ai.TargetNonThreats && ai.TargetingInfo.OtherInRange) && (ai.Construct.RootAi.ActiveWeaponTerminal.ActiveCube == null || !ai.SubGrids.Contains(ai.Construct.RootAi.ActiveWeaponTerminal.ActiveCube.CubeGrid));

                    ai.DbUpdated = true;
                    ai.FirstRun = false;
                    ai.ScanInProgress = false;
                }
                DbsToUpdate.Clear();
                DsUtil.Complete("db", true);
            }
            catch (Exception ex) { Log.Line($"Exception in ProcessDbsCallBack: {ex}"); }
        }

        internal void CheckWeaponStorage()
        {
            for (int i = 0; i < CheckStorage.Count; i++)
                ComputeStorage(CheckStorage[i]);
            CheckStorage.Clear();
        }

        internal void DelayedComputeStorage(object o)
        {
            var w = o as Weapon;
            ComputeStorage(w);
        }

        internal void CheckDirtyGrids()
        {
            if (!NewGrids.IsEmpty)
                AddGridToMap();

            if ((!GameLoaded || Tick20) && DirtyGrids.Count > 0)
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
            DirtyGridsTmp.AddRange(DirtyGrids);
            DirtyGrids.Clear();
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

                FatMap fatMap;
                if (GridToFatMap.TryGetValue(grid, out fatMap)) {
                    var allFat = fatMap.MyCubeBocks;
                    var terminals = 0;
                    var tStatus = fatMap.Targeting == null || fatMap.Targeting.AllowScanning;
                    for (int j = 0; j < allFat.Count; j++) {
                        var fat = allFat[j];
                        if (!(fat is IMyTerminalBlock)) continue;
                        terminals++;

                        using (fat.Pin()) {

                            if (fat.MarkedForClose) continue;
                            if (fat is IMyProductionBlock) newTypeMap[Production].Add(fat);
                            else if (fat is IMyPowerProducer) newTypeMap[Power].Add(fat);
                            else if (fat is IMyGunBaseUser || fat is IMyWarhead || fat is MyConveyorSorter && WeaponPlatforms.ContainsKey(fat.BlockDefinition.Id.SubtypeId))
                            {
                                if (!tStatus && fat is IMyGunBaseUser && !WeaponPlatforms.ContainsKey(fat.BlockDefinition.Id.SubtypeId))
                                    tStatus = fatMap.Targeting.AllowScanning = true;

                                newTypeMap[Offense].Add(fat);
                            }
                            else if (fat is IMyUpgradeModule || fat is IMyRadioAntenna || fat is MyCockpit || fat is MyRemoteControl || fat is IMyDecoy) newTypeMap[Utility].Add(fat);
                            else if (fat is MyThrust) newTypeMap[Thrust].Add(fat);
                            else if (fat is MyGyro) newTypeMap[Steering].Add(fat);
                            else if (fat is MyJumpDrive) newTypeMap[Jumping].Add(fat);
                        }
                    }

                    foreach (var type in newTypeMap)
                        type.Value.ApplyAdditions();
                    
                    fatMap.MyCubeBocks.ApplyAdditions();

                    fatMap.Trash = terminals == 0;
                    var gridBlocks = grid.BlocksCount;
                    if (gridBlocks > fatMap.MostBlocks) fatMap.MostBlocks = gridBlocks;
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
