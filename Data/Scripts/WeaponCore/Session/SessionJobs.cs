using System;
using System.Collections.Concurrent;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage.Collections;
using VRage.Game;
using VRage.Game.ModAPI;
using WeaponCore.Support;
using static WeaponCore.Support.WeaponDefinition.TargetingDef.BlockTypes;

namespace WeaponCore
{
    public class FatMap
    {
        public ConcurrentCachingList<MyCubeBlock> MyCubeBocks;
        public MyGridTargeting Targeting;
        public volatile bool Trash;
        public int MostBlocks;
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

            DbCallBackComplete = false;

            DbTask = MyAPIGateway.Parallel.StartBackground(ProcessDbs, ProcessDbsCallBack);
        }

        private void ProcessDbs()
        {
            for (int i = 0; i < DbsToUpdate.Count; i++) DbsToUpdate[i].Scan();
        }

        private void ProcessDbsCallBack()
        {
            try
            {
                DsUtil.Start("db");
                for (int d = 0; d < DbsToUpdate.Count; d++)
                {
                    var db = DbsToUpdate[d];

                    db.TargetingInfo.Clean();

                    if (db.MyPlanetTmp != null)
                        db.MyPlanetInfo();

                    foreach (var sub in db.PrevSubGrids) db.SubGrids.Add(sub);
                    if (db.SubGridsChanged) db.SubGridChanges();

                    for (int i = 0; i < db.SortedTargets.Count; i++)
                    {
                        var tInfo = db.SortedTargets[i];
                        tInfo.Target = null;
                        tInfo.MyAi = null;
                        tInfo.MyGrid = null;
                        tInfo.TargetAi = null;
                        TargetInfoPool.Return(db.SortedTargets[i]);
                    }
                    db.SortedTargets.Clear();
                    db.Targets.Clear();

                    var newEntCnt = db.NewEntities.Count;
                    db.SortedTargets.Capacity = newEntCnt;
                    for (int i = 0; i < newEntCnt; i++)
                    {
                        var detectInfo = db.NewEntities[i];
                        var ent = detectInfo.Parent;
                        if (ent.Physics == null) continue;

                        var grid = ent as MyCubeGrid;
                        GridAi targetAi = null;

                        if (grid != null)
                            GridTargetingAIs.TryGetValue(grid, out targetAi);

                        var targetInfo = TargetInfoPool.Get();
                        targetInfo.Init(ref detectInfo, db.MyGrid, db, targetAi);

                        db.SortedTargets.Add(targetInfo);
                        db.Targets[ent] = targetInfo;

                        if (targetInfo.Target == db.Focus.Target[0] || targetInfo.Target == db.Focus.Target[1] || targetInfo.DistSqr < db.MaxTargetingRangeSqr && targetInfo.DistSqr < db.TargetingInfo.ThreatRangeSqr && targetInfo.OffenseRating > 0 && (targetInfo.EntInfo.Relationship != MyRelationsBetweenPlayerAndBlock.Friends || targetInfo.EntInfo.Relationship == MyRelationsBetweenPlayerAndBlock.FactionShare))
                        {
                            db.TargetingInfo.TargetInRange = true;
                            db.TargetingInfo.ThreatRangeSqr = targetInfo.DistSqr;
                        }
                    }
                    db.NewEntities.Clear();
                    db.SortedTargets.Sort(TargetCompare);
                    db.TargetAis.Clear();
                    db.TargetAis.AddRange(db.TargetAisTmp);
                    db.TargetAisTmp.Clear();

                    db.Obstructions.Clear();
                    db.Obstructions.AddRange(db.ObstructionsTmp);
                    db.ObstructionsTmp.Clear();
                    
                    db.MyShield = null;
                    db.ShieldNear = false;
                    db.FriendlyShieldNear = false;
                    if (db.NearByShieldsTmp.Count > 0)
                        db.NearByShield();

                    if (db.PlanetSurfaceInRange) db.StaticsInRangeTmp.Add(db.MyPlanet);
                    db.StaticsInRange.Clear();
                    db.StaticsInRange.AddRange(db.StaticsInRangeTmp);
                    db.StaticsInRangeTmp.Clear();
                    db.StaticEntitiesInRange = db.StaticsInRange.Count > 0;
                    db.MyStaticInfo();

                    db.DbReady = db.SortedTargets.Count > 0 || db.TargetAis.Count > 0 || Tick - db.LiveProjectileTick < 3600 || db.LiveProjectile.Count > 0 || db.ControllingPlayers.Keys.Count > 0 || db.FirstRun;
                    db.NaturalGravity = db.FakeShipController.GetNaturalGravity();
                    db.BlockCount = db.MyGrid.BlocksCount;

                    if (!db.TargetingInfo.TargetInRange && db.LiveProjectile.Count > 0)
                        db.TargetingInfo.TargetInRange = true;

                    if (db.ScanBlockGroups || db.WeaponTerminalReleased()) db.ReScanBlockGroups();
                    if (db.ScanBlockGroupSettings) db.UpdateGroupOverRides();

                    db.FirstRun = false;
                }
                DbsToUpdate.Clear();
                DsUtil.Complete("db", true);
                DbCallBackComplete = true;
            }
            catch (Exception ex) { Log.Line($"Exception in ProcessDbsCallBack: {ex}"); }
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
