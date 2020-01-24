using System;
using System.Collections.Concurrent;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage.Collections;
using VRage.Game;
using WeaponCore.Support;
using static WeaponCore.Support.TargetingDefinition;
using static WeaponCore.Support.TargetingDefinition.BlockTypes;

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
        internal ConcurrentDictionary<BlockTypes, ConcurrentCachingList<MyCubeBlock>> Collection;
    }

    public partial class Session
    {

        public void UpdateDbsInQueue()
        {
            if (DbTask.IsComplete && DbTask.valid && DbTask.Exceptions != null)
                TaskHasErrors(ref DbTask, "DbTask");

            DbCallBackComplete = false;
            DbTask = MyAPIGateway.Parallel.Start(ProcessDbs, ProcessDbsCallBack);
        }

        private void ProcessDbs()
        {
            for (int i = 0; i < DbsToUpdate.Count; i++) DbsToUpdate[i].Scan();
        }

        private void ProcessDbsCallBack()
        {
            DsUtil.Start("db");
            for (int d = 0; d < DbsToUpdate.Count; d++)
            {
                var db = DbsToUpdate[d];

                db.TargetingInfo.Clean();

                if (db.MyPlanetTmp != null)
                {
                    var gridBox = db.MyGrid.PositionComp.WorldAABB;
                    if (db.MyPlanetTmp.IntersectsWithGravityFast(ref gridBox)) db.MyPlanetInfo();
                    else if (db.MyPlanet != null) db.MyPlanetInfo(clear: true);
                }

                db.MyStaticInfo();

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

                    if (targetInfo.Target == db.Focus.Target[0] || targetInfo.Target ==  db.Focus.Target[1] || targetInfo.DistSqr < db.MaxTargetingRangeSqr && targetInfo.DistSqr < db.TargetingInfo.ThreatRangeSqr && targetInfo.OffenseRating > 0 && (targetInfo.EntInfo.Relationship != MyRelationsBetweenPlayerAndBlock.Friends || targetInfo.EntInfo.Relationship == MyRelationsBetweenPlayerAndBlock.FactionShare))
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

                if (db.PlanetSurfaceInRange) db.StaticsInRangeTmp.Add(db.MyPlanet);
                db.StaticsInRange.Clear();
                db.StaticsInRange.AddRange(db.StaticsInRangeTmp);
                db.StaticsInRangeTmp.Clear();
                db.StaticEntitiesInRange = db.StaticsInRange.Count > 0;

                db.DbReady = db.SortedTargets.Count > 0 || db.TargetAis.Count > 0 || Tick - db.LiveProjectileTick < 3600|| db.LiveProjectile.Count > 0 || db.FirstRun;
                db.MyShield = db.MyShieldTmp;
                db.NaturalGravity = db.FakeShipController.GetNaturalGravity();
                db.ShieldNear = db.ShieldNearTmp;
                db.BlockCount = db.MyGrid.BlocksCount;
                if (db.ScanBlockGroups || db.WeaponTerminalReleased()) db.ReScanBlockGroups();

                db.FirstRun = false;
            }
            DbsToUpdate.Clear();
            DsUtil.Complete("db", true);
            DbCallBackComplete = true;
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
            //Log.Line($"[UpdateGrids] DirtTmp:{DirtyGridsTmp.Count} - Dirt:{DirtyGrids.Count}");
            //DsUtil2.Start("UpdateGrids");
            DeferedUpBlockTypeCleanUp();

            DirtyGridsTmp.Clear();
            DirtyGridsTmp.AddRange(DirtyGrids);
            DirtyGrids.Clear();
            for (int i = 0; i < DirtyGridsTmp.Count; i++)
            {
                var grid = DirtyGridsTmp[i];
                var newTypeMap = BlockTypePool.Get();
                newTypeMap[Offense] = ConcurrentListPool.Get();
                newTypeMap[Utility] = ConcurrentListPool.Get();
                newTypeMap[Thrust] = ConcurrentListPool.Get();
                newTypeMap[Steering] = ConcurrentListPool.Get();
                newTypeMap[Jumping] = ConcurrentListPool.Get();
                newTypeMap[Power] = ConcurrentListPool.Get();
                newTypeMap[Production] = ConcurrentListPool.Get();

                ConcurrentDictionary<TargetingDefinition.BlockTypes, ConcurrentCachingList<MyCubeBlock>> noFatTypeMap;

                FatMap fatMap;
                if (GridToFatMap.TryGetValue(grid, out fatMap))
                {
                    var allFat = fatMap.MyCubeBocks;
                    var terminals = 0;
                    var tStatus = fatMap.Targeting == null || fatMap.Targeting.AllowScanning;
                    for (int j = 0; j < allFat.Count; j++)
                    {
                        var fat = allFat[j];
                        if (!(fat is IMyTerminalBlock)) continue;
                        terminals++;

                        using (fat.Pin())
                        {
                            if (fat.MarkedForClose) continue;
                            if (fat is IMyProductionBlock) newTypeMap[Production].Add(fat);
                            else if (fat is IMyPowerProducer) newTypeMap[Power].Add(fat);
                            else if (fat is IMyGunBaseUser || fat is IMyWarhead || fat is MyConveyorSorter && WeaponPlatforms.ContainsKey(fat.BlockDefinition.Id.SubtypeId))
                            {
                                if (!tStatus && fat is IMyGunBaseUser && !WeaponPlatforms.ContainsKey(fat.BlockDefinition.Id.SubtypeId))
                                    tStatus = fatMap.Targeting.AllowScanning = true;

                                newTypeMap[Offense].Add(fat);
                            }
                            else if (fat is IMyUpgradeModule || fat is IMyRadioAntenna) newTypeMap[Utility].Add(fat);
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
                    ConcurrentDictionary<TargetingDefinition.BlockTypes, ConcurrentCachingList<MyCubeBlock>> oldTypeMap; 
                    if (GridToBlockTypeMap.TryGetValue(grid, out oldTypeMap))
                    {
                        GridToBlockTypeMap[grid] = newTypeMap;
                        
                        BlockTypeCleanUp.Enqueue(new DeferedTypeCleaning {Collection = oldTypeMap, RequestTick = Tick});
                    }
                    else GridToBlockTypeMap[grid] = newTypeMap;
                }
                else if (GridToBlockTypeMap.TryRemove(grid, out noFatTypeMap))
                {
                    BlockTypeCleanUp.Enqueue(new DeferedTypeCleaning { Collection = noFatTypeMap, RequestTick = Tick });
                }
            }
            DirtyGridsTmp.Clear();
            //DsUtil2.Complete("UpdateGrids", false, true);
        }
    }
}
