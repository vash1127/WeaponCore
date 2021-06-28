﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using CoreSystems.Support;
using Jakaria;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;
using static CoreSystems.Support.WeaponDefinition.TargetingDef.BlockTypes;
namespace CoreSystems
{
    public class GridMap
    {
        public readonly MyShipController FakeController = new MyShipController();
        public ConcurrentCachingList<MyCubeBlock> MyCubeBocks;
        public MyGridTargeting Targeting;
        public volatile bool Trash;
        public int MostBlocks;
        public uint PowerCheckTick;
        public bool SuspectedDrone;
        public bool Powered;

        internal void Clean()
        {
            Targeting = null;
            FakeController.SlimBlock = null;
            MyCubeBocks.ClearImmediate();
            MostBlocks = 0;
            PowerCheckTick = 0;
            SuspectedDrone = false;
            Powered = false;
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
            for (int i = 0; i < DbsToUpdate.Count; i++)
            {

                var db = DbsToUpdate[i];
                using (db.Ai.DbLock.AcquireExclusiveUsing())
                {

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
                        if (ai.TopEntity.MarkedForClose || ai.MarkedForClose || db.Version != ai.Version)
                        {
                            ai.ScanInProgress = false;
                            continue;
                        }


                        if (ai.MyPlanetTmp != null)
                            ai.MyPlanetInfo();

                        foreach (var sub in ai.PrevSubGrids) ai.SubGrids.Add((MyCubeGrid)sub);
                        if (ai.SubGridsChanged) ai.SubGridChanges(false, true);

                        ai.DetectionInfo.Clean(ai);
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
                                EntityAIs.TryGetValue(grid, out targetAi);

                            var targetInfo = TargetInfoPool.Get();
                            targetInfo.Init(ref detectInfo, ai, targetAi);

                            ai.SortedTargets.Add(targetInfo);
                            ai.Targets[ent] = targetInfo;

                            var checkFocus = ai.Construct.Data.Repo.FocusData.HasFocus && targetInfo.Target?.EntityId == ai.Construct.Data.Repo.FocusData.Target[0] || targetInfo.Target?.EntityId == ai.Construct.Data.Repo.FocusData.Target[1];

                            if (targetInfo.Drone)
                                ai.DetectionInfo.DroneAdd(ai, targetInfo);

                            if (ai.RamProtection && targetInfo.DistSqr < 136900 && targetInfo.IsGrid)
                                ai.DetectionInfo.RamProximity = true;

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

                                if (targetInfo.Drone && targetInfo.DistSqr < ai.DetectionInfo.DroneRangeSqr)
                                {
                                    ai.DetectionInfo.DroneInRange = true;
                                    ai.DetectionInfo.DroneRangeSqr = targetInfo.DistSqr;
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
                        ai.BlockCount = ai.AiType == Ai.AiTypes.Grid ? ai.GridEntity.BlocksCount : 0;
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

        private void UpdateWaters()
        {
            foreach (var planet in PlanetMap.Values)
            {
                WaterData data;
                if (WaterApi.HasWater(planet.EntityId))
                {
                    if (!WaterMap.TryGetValue(planet.EntityId, out data))
                    {
                        data = new WaterData(planet);
                        WaterMap[planet.EntityId] = data;
                    }

                    var radiusInfo = WaterApi.GetPhysical(data.WaterId);
                    data.Center = radiusInfo.Item1;
                    data.Radius = radiusInfo.Item2;
                    data.MinRadius = radiusInfo.Item3;
                    data.MaxRadius = radiusInfo.Item3;
                    var waveInfo = WaterApi.GetWaveData(data.WaterId);
                    data.WaveHeight = waveInfo.Item1;
                    data.WaveSpeed = waveInfo.Item2;
                }
                else WaterMap.TryRemove(planet.EntityId, out data);
            }
        }

        private void UpdatePlayerPainters()
        {
            ActiveMarks.Clear();
            foreach (var pair in PlayerDummyTargets)
            {

                IMyPlayer player;
                if (Players.TryGetValue(pair.Key, out player))
                {

                    var painted = pair.Value.PaintedTarget;
                    MyEntity target;
                    if (!painted.Dirty && painted.EntityId != 0 && Tick - painted.LastInfoTick < 300 && !MyUtils.IsZero(painted.LocalPosition) && MyEntities.TryGetEntityById(painted.EntityId, out target))
                    {

                        var grid = target as MyCubeGrid;
                        if (player.IdentityId == PlayerId && grid != null)
                        {

                            var v3 = grid.LocalToGridInteger(painted.LocalPosition);
                            MyCube cube;

                            if (!grid.TryGetCube(v3, out cube))
                            {

                                var startPos = grid.GridIntegerToWorld(v3);
                                var endPos = startPos + (TargetUi.AimDirection * grid.PositionComp.LocalVolume.Radius);

                                if (grid.RayCastBlocks(startPos, endPos) == null)
                                {
                                    if (++painted.MissCount > 2)
                                        painted.ClearMark(Tick);
                                }
                            }
                        }

                        var rep = MyIDModule.GetRelationPlayerPlayer(PlayerId, player.IdentityId);
                        var self = rep == MyRelationsBetweenPlayers.Self;
                        var friend = rep == MyRelationsBetweenPlayers.Allies;
                        var neut = rep == MyRelationsBetweenPlayers.Neutral;
                        var color = neut ? new Vector4(1, 1, 1, 1) : self ? new Vector4(0.025f, 1f, 0.25f, 2) : friend ? new Vector4(0.025f, 0.025f, 1, 2) : new Vector4(1, 0.025f, 0.025f, 2);
                        ActiveMarks.Add(new MyTuple<IMyPlayer, Vector4, Ai.FakeTarget>(player, color, painted));
                    }
                }
            }
        }


        private bool GetClosestLocalPos(MyCubeGrid grid, Vector3I center, double areaRadius, out Vector3D newWorldPos)
        {
            if (areaRadius < 3 && grid.GridSizeEnum == MyCubeSize.Large) areaRadius = 3;

            List<Vector3I> tmpSphereOfV3S;
            areaRadius = Math.Ceiling(areaRadius);
            if (grid.GridSizeEnum == MyCubeSize.Large && LargeBlockSphereDb.TryGetValue(areaRadius, out tmpSphereOfV3S) || SmallBlockSphereDb.TryGetValue(areaRadius, out tmpSphereOfV3S))
            {
                var gMinX = grid.Min.X;
                var gMinY = grid.Min.Y;
                var gMinZ = grid.Min.Z;
                var gMaxX = grid.Max.X;
                var gMaxY = grid.Max.Y;
                var gMaxZ = grid.Max.Z;

                for (int i = 0; i < tmpSphereOfV3S.Count; i++)
                {
                    var v3ICheck = center + tmpSphereOfV3S[i];
                    var contained = gMinX <= v3ICheck.X && v3ICheck.X <= gMaxX && (gMinY <= v3ICheck.Y && v3ICheck.Y <= gMaxY) && (gMinZ <= v3ICheck.Z && v3ICheck.Z <= gMaxZ);
                    if (!contained) continue;

                    MyCube cube;
                    if (grid.TryGetCube(v3ICheck, out cube))
                    {
                        IMySlimBlock slim = cube.CubeBlock;
                        if (slim.Position == v3ICheck)
                        {
                            newWorldPos = grid.GridIntegerToWorld(slim.Position);
                            return true;
                        }
                    }
                }
            }
            newWorldPos = Vector3D.Zero;
            return false;
        }
        /*
        IEnumerable<Vector3I> NearLine(Vector3I start, LineD line)
        {
            MinHeap blocks;
            HashSet<Vector3I> seen = new HashSet<Vector3I> {start};
            blocks.Add(dist(line, start), start);
            while (!blocks.Empty)
            {
                var next = blocks.RemoveMin();
                yield return next;
                foreach (var neighbor in Neighbors(next))
                {
                    if (seen.add(neighbor))
                        blocks.Add(dist(line, neighbor), neighbor);
                }
            }
        }
        */

        private void UpdateGrids()
        {
            DeferedUpBlockTypeCleanUp();

            DirtyGridsTmp.Clear();
            DirtyGridsTmp.AddRange(DirtyGridInfos);
            DirtyGridInfos.Clear();
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

                ConcurrentDictionary<WeaponDefinition.TargetingDef.BlockTypes, ConcurrentCachingList<MyCubeBlock>> noFatTypeMap;

                GridMap gridMap;
                if (GridToInfoMap.TryGetValue(grid, out gridMap))
                {
                    var allFat = gridMap.MyCubeBocks;
                    var terminals = 0;
                    var tStatus = gridMap.Targeting == null || gridMap.Targeting.AllowScanning;
                    var thrusters = 0;
                    var gyros = 0;
                    var powerProducers = 0;
                    var warHead = 0;
                    var working = 0;
                    for (int j = 0; j < allFat.Count; j++)
                    {
                        var fat = allFat[j];
                        if (!(fat is IMyTerminalBlock)) continue;
                        terminals++;
                        using (fat.Pin())
                        {

                            if (fat.MarkedForClose) continue;
                            if (fat.IsWorking && ++working == 1)
                            {

                                var oldCube = (gridMap.FakeController.SlimBlock as IMySlimBlock)?.FatBlock as MyCubeBlock;
                                if (oldCube == null || oldCube.MarkedForClose || oldCube.CubeGrid != grid)
                                {
                                    gridMap.FakeController.SlimBlock = fat.SlimBlock;
                                    GridDistributors[grid] = gridMap;
                                }
                            }

                            var cockpit = fat as MyCockpit;
                            var decoy = fat as IMyDecoy;
                            var bomb = fat as IMyWarhead;

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
                            else if (fat is IMyPowerProducer)
                            {
                                newTypeMap[Power].Add(fat);
                                powerProducers++;
                            }
                            else if (fat is IMyGunBaseUser || bomb != null || fat is MyConveyorSorter && PartPlatforms.ContainsKey(fat.BlockDefinition.Id))
                            {
                                if (bomb != null)
                                    warHead++;

                                if (!tStatus && fat is IMyGunBaseUser && !PartPlatforms.ContainsKey(fat.BlockDefinition.Id))
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

                    GridMap oldMap;
                    if (terminals == 0 && !gridMap.Trash && GridDistributors.TryRemove(grid, out oldMap))
                        oldMap.FakeController.SlimBlock = null;

                    gridMap.MyCubeBocks.ApplyAdditions();
                    gridMap.SuspectedDrone = warHead > 0 || powerProducers > 0 && thrusters > 0 && gyros > 0;
                    gridMap.Trash = terminals == 0;
                    gridMap.Powered = working > 0;
                    gridMap.PowerCheckTick = Tick;

                    var gridBlocks = grid.BlocksCount;

                    if (gridBlocks > gridMap.MostBlocks) gridMap.MostBlocks = gridBlocks;

                    ConcurrentDictionary<WeaponDefinition.TargetingDef.BlockTypes, ConcurrentCachingList<MyCubeBlock>> oldTypeMap;
                    if (GridToBlockTypeMap.TryGetValue(grid, out oldTypeMap))
                    {
                        GridToBlockTypeMap[grid] = newTypeMap;
                        BlockTypeCleanUp.Enqueue(new DeferedTypeCleaning { Collection = oldTypeMap, RequestTick = Tick });
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
