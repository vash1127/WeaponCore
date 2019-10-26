using System;
using System.Collections.Concurrent;
using System.Threading;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;
using WeaponCore.Support;
using VRage.Utils;
using WeaponCore.Platform;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using VRage.Collections;
using VRage.ObjectBuilders;
using static WeaponCore.Support.TargetingDefinition.BlockTypes;

namespace WeaponCore
{
    public partial class Session
    {
        public void UpdateDbsInQueue()
        {
            DbsUpdating = true;
            MyAPIGateway.Parallel.Start(ProcessDbs, ProcessDbsCallBack);
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
                if (db.MyPlanetTmp != null)
                {
                    var gridBox = db.MyGrid.PositionComp.WorldAABB;
                    if (db.MyPlanetTmp.IntersectsWithGravityFast(ref gridBox)) db.MyPlanetInfo();
                    else if (db.MyPlanet != null) db.MyPlanetInfo(clear: true);
                }

                for (int i = 0; i < db.SubGridsTmp.Count; i++) db.SubGrids.Add(db.SubGridsTmp[i]);
                db.SubGridsTmp.Clear();

                for (int i = 0; i < db.SortedTargets.Count; i++) db.TargetInfoPool.Return(db.SortedTargets[i]);
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
                    var targetInfo = db.TargetInfoPool.Get();
                    if (grid == null)
                        targetInfo.Init(detectInfo.EntInfo, ent, false, 1, db.MyGrid, db, null);
                    else
                    {
                        GridAi targetAi;
                        GridTargetingAIs.TryGetValue(grid, out targetAi);
                        targetInfo.Init(detectInfo.EntInfo, grid, true, GridToFatMap[grid].Count, db.MyGrid, db, targetAi);
                    }

                    db.SortedTargets.Add(targetInfo);
                    db.Targets[ent] = targetInfo;
                }
                db.NewEntities.Clear();
                db.SortedTargets.Sort(db.TargetCompare1);

                db.Threats.Clear();
                db.Threats.AddRange(db.TargetAisTmp);
                db.ThreatsTmp.Clear();

                db.TargetAis.Clear();
                db.TargetAis.AddRange(db.TargetAisTmp);
                db.TargetAisTmp.Clear();

                db.Obstructions.Clear();
                db.Obstructions.AddRange(db.ObstructionsTmp);
                db.ObstructionsTmp.Clear();

                db.StaticsInRange.Clear();
                if (db.PlanetSurfaceInRange) db.StaticsInRangeTmp.Add(db.MyPlanet);
                var staticCount = db.StaticsInRangeTmp.Count;
                db.StaticsInRange.AddRange(db.StaticsInRangeTmp);
                db.StaticEntitiesInRange = staticCount > 0;
                db.StaticsInRangeTmp.Clear();

                db.DbReady = db.SortedTargets.Count > 0 || db.Threats.Count > 0 || db.FirstRun;
                db.MyShield = db.MyShieldTmp;
                db.ShieldNear = db.ShieldNearTmp;
                db.BlockCount = db.MyGrid.BlocksCount;

                if (db.FirstRun)
                    db.UpdateBlockGroups();

                db.FirstRun = false;
                Interlocked.Exchange(ref db.DbUpdating, 0);
            }
            DbsToUpdate.Clear();
            DbsUpdating = false;
            DsUtil.Complete("db", true);
        }

        public void Handler(object o)
        {
            try
            {
                var message = o as byte[];
                if (message == null) return;
                var slaveDefArray = MyAPIGateway.Utilities.SerializeFromBinary<WeaponDefinition[]>(message);
                foreach (var wepDef in slaveDefArray)
                    _weaponDefinitions.Add(wepDef);
            }
            catch (Exception ex) { Log.Line($"Exception in Handler: {ex}"); }
        }

        public void UpgradeHandler(object o)
        {
            try
            {
                var message = o as byte[];
                if (message == null) return;
                var slaveDefArray = MyAPIGateway.Utilities.SerializeFromBinary<UpgradeDefinition[]>(message);
                foreach (var upgDef in slaveDefArray)
                    _upgradeDefinitions.Add(upgDef);
            }
            catch (Exception ex) { Log.Line($"Exception in Handler: {ex}"); }
        }

        internal bool PlayerInAiCockPit()
        {
            if (ActiveCockPit == null || ActiveCockPit.MarkedForClose || ((IMyControllerInfo)ActiveCockPit.ControllerInfo)?.ControllingIdentityId != MyAPIGateway.Session.Player.IdentityId) return false;
            return true;
        }

        internal void ResetGps()
        {
            if (TargetGps == null)
            {
                Log.Line("resetgps");
                MyVisualScriptLogicProvider.AddGPS("WEAPONCORE", "", Vector3D.Zero, Color.Red);
                var gpsList = MyAPIGateway.Session.GPS.GetGpsList(MyAPIGateway.Session.Player.IdentityId);
                foreach (var t in gpsList)
                {
                    if (t.Name == "WEAPONCORE")
                    {
                        TargetGps = t;
                        break;
                    }
                }
                //TargetGps = MyAPIGateway.Session.GPS.Create("", "", Vector3D.MaxValue, true, true);
                MyAPIGateway.Session.GPS.AddLocalGps(TargetGps);
                MyVisualScriptLogicProvider.SetGPSColor(TargetGps?.Name, Color.Yellow);
            }
        }

        internal void RemoveGps()
        {
            if (TargetGps != null)
            {
                Log.Line("remove gps");
                MyAPIGateway.Session.GPS.RemoveLocalGps(TargetGps);
                TargetGps = null;
            }
        }

        internal void SetGpsInfo(Vector3D pos, string name, double dist = 0)
        {
            if (TargetGps != null)
            {
                var newPos = dist > 0 ? pos + (Camera.WorldMatrix.Up * dist) : pos;
                TargetGps.Coords = newPos;
                TargetGps.Name = name;
            }
        }

        internal bool CheckTarget(GridAi ai)
        {
            if (ai.PrimeTarget == null)
                return false;

            if (ai.PrimeTarget.MarkedForClose || ai != TrackingAi)
            {
                Log.Line("resetting target");
                ai.PrimeTarget = null;
                TrackingAi = null;
                RemoveGps();
                return false;
            }

            return true;
        }

        internal void SetTarget(MyEntity entity, GridAi ai)
        {
            ai.PrimeTarget = entity;
            TrackingAi = ai;
            ai.TargetResetTick = Tick + 1;
            GridAi gridAi;
            TargetArmed = false;
            if (GridTargetingAIs.TryGetValue((MyCubeGrid)entity, out gridAi))
            {
                TargetArmed = true;
            }
            else
            {
                foreach (var info in ai.SortedTargets)
                {
                    if (info.Target != entity) continue;
                    ConcurrentDictionary<TargetingDefinition.BlockTypes, MyConcurrentList<MyCubeBlock>> typeDict;
                    if (info.IsGrid && ai.Session.GridToBlockTypeMap.TryGetValue((MyCubeGrid)info.Target, out typeDict))
                    {
                        MyConcurrentList<MyCubeBlock> fatList;
                        if (typeDict.TryGetValue(Offense, out fatList))
                            TargetArmed = fatList.Count > 0;
                        else TargetArmed = false;
                    }
                    else TargetArmed = false;
                    break;
                }
            }
        }

        internal void GetTargetInfo(GridAi ai, out double speed, out string armedStr, out string interceptStr, out string shieldedStr, out string threatStr)
        {
            var target = ai.PrimeTarget;
            var targetVel = target.Physics?.LinearVelocity ?? Vector3.Zero;
            if (MyUtils.IsZero(targetVel, 1E-02F)) targetVel = Vector3.Zero;
            var targetDir = Vector3D.Normalize(targetVel);
            var targetPos = target.PositionComp.WorldAABB.Center;
            var myPos = ai.MyGrid.PositionComp.WorldAABB.Center;
            var myHeading = Vector3D.Normalize(myPos - targetPos);
            var intercept = MathFuncs.IsDotProductWithinTolerance(ref targetDir, ref myHeading, ApproachDegrees);
            var shielded = ShieldApiLoaded && SApi.ProtectedByShield(target);
            var grid = target as MyCubeGrid;
            var friend = false;
            if (grid != null && grid.BigOwners.Count != 0)
            {
                var relation = MyIDModule.GetRelationPlayerBlock(ai.MyOwner, grid.BigOwners[0], MyOwnershipShareModeEnum.Faction);
                if (relation == MyRelationsBetweenPlayerAndBlock.FactionShare || relation == MyRelationsBetweenPlayerAndBlock.Owner || relation == MyRelationsBetweenPlayerAndBlock.Friends) friend = true;
            }
            var threat = friend ? 0 : 1;
            shieldedStr = shielded ? "S" : "_";
            armedStr = TargetArmed ? "A" : "_";
            interceptStr = intercept ? "I" : "_";
            threatStr = threat > 0 ? "T" + threat : "__";
            speed = Math.Round(target.Physics?.Speed ?? 0, 1);
        }

        internal static double ModRadius(double radius, bool largeBlock)
        {
            if (largeBlock && radius < 3) radius = 3;
            else if (largeBlock && radius > 25) radius = 25;
            else if (!largeBlock && radius > 5) radius = 5;

            radius = Math.Ceiling(radius);
            return radius;
        }

        internal void Timings()
        {
            Tick = (uint)(Session.ElapsedPlayTime.TotalMilliseconds * TickTimeDiv);
            Tick10 = Tick % 10 == 0;
            Tick20 = Tick % 20 == 0;
            Tick60 = Tick % 60 == 0;
            Tick180 = Tick % 180 == 0;
            Tick300 = Tick % 300 == 0;
            Tick600 = Tick % 600 == 0;
            Tick1800 = Tick % 1800 == 0;
            if (Tick60) ExplosionCounter = 0;
            if (_count++ == 59)
            {
                _count = 0;
            }
            _lCount++;
            if (_lCount == 129)
            {
                _lCount = 0;
                _eCount++;
                if (_eCount == 10) _eCount = 0;
            }
            if (!GameLoaded)
            {
                if (FirstLoop)
                {
                    if (!MiscLoaded)
                    {
                        MiscLoaded = true;
                        if (!IsServer) PlayerConnected(MyAPIGateway.Session.Player.IdentityId);

                    }
                    GameLoaded = true;
                }
                else if (!FirstLoop)
                {
                    FirstLoop = true;
                    foreach (var t in AllDefinitions)
                    {
                        var name = t.Id.SubtypeName;
                        var contains = name.Contains("BlockArmor");
                        if (contains)
                        {
                            AllArmorBaseDefinitions.Add(t);
                            if (name.Contains("HeavyBlockArmor")) HeavyArmorBaseDefinitions.Add(t);
                        }
                    }
                }
            }

            if (ShieldMod && !ShieldApiLoaded && SApi.Load())
                ShieldApiLoaded = true;
        }

        internal bool UpdateLocalAiAndCockpit()
        {
            ActiveCockPit = ControlledEntity as MyCockpit;
            InGridAiCockPit = false;
            if (ActiveCockPit != null && GridTargetingAIs.TryGetValue(ActiveCockPit.CubeGrid, out TrackingAi))
            {
                InGridAiCockPit = true;
                return true;
            }

            if (TrackingAi != null) TrackingAi.PrimeTarget = null;
            TrackingAi = null;
            ActiveCockPit = null;
            RemoveGps();
            return false;
        }

        private void PlayerControlAcquired(IMyEntityController myEntityController)
        {
            var cockpit = ControlledEntity as MyCockpit;
            var remote = ControlledEntity as MyRemoteControl;

            if (cockpit != null && UpdateLocalAiAndCockpit())
                _futureEvents.Schedule(TurnWeaponShootOff,GridTargetingAIs[cockpit.CubeGrid], 1);

            if (remote != null)
                _futureEvents.Schedule(TurnWeaponShootOff, GridTargetingAIs[remote.CubeGrid], 1);

            MyAPIGateway.Utilities.InvokeOnGameThread(PlayerAcquiredControl);
        }

        private void PlayerControlReleased(IMyEntityController myEntityController)
        {
            MyAPIGateway.Utilities.InvokeOnGameThread(PlayerReleasedControl);
        }

        private void PlayerReleasedControl()
        {
            UpdateLocalAiAndCockpit();
        }

        private void PlayerAcquiredControl()
        {
            UpdateLocalAiAndCockpit();
        }

        private void Paused()
        {
            Log.Line($"Stopping all AV due to pause");
            foreach (var aiPair in GridTargetingAIs)
            {
                var gridAi = aiPair.Value;
                foreach (var comp in gridAi.WeaponBase.Values)
                    comp.StopAllAv();
            }

            for (int i = 0; i < Projectiles.Wait.Length; i++)
                foreach (var p in Projectiles.ProjectilePool[i].Active)
                    p.PauseAv();
        }

        private void StartComps()
        {
            var reassign = false;
            for (int i = 0; i < CompsToStart.Count; i++)
            {
                var weaponComp = CompsToStart[i];
                if (weaponComp.MyCube.CubeGrid.IsPreview)
                {
                    //Log.Line($"[IsPreview] MyCubeId:{weaponComp.MyCube.EntityId} - Grid:{weaponComp.MyCube.CubeGrid.DebugName} - !Marked:{!weaponComp.MyCube.MarkedForClose} - inScene:{weaponComp.MyCube.InScene} - gridMatch:{weaponComp.MyCube.CubeGrid == weaponComp.Ai.MyGrid}");
                    weaponComp.RemoveComp();
                    CompsToStart.Remove(weaponComp);
                    continue;
                }
                if (weaponComp.MyCube.CubeGrid.Physics == null && !weaponComp.MyCube.CubeGrid.MarkedForClose && weaponComp.MyCube.BlockDefinition.HasPhysics)
                    continue;
                if (weaponComp.Ai.MyGrid != weaponComp.MyCube.CubeGrid)
                {
                    if (!GridToFatMap.ContainsKey(weaponComp.MyCube.CubeGrid))
                    {
                        Log.Line($"grid not yet in map1");
                        continue;
                    }

                    Log.Line($"[gridMisMatch] MyCubeId:{weaponComp.MyCube.EntityId} - Grid:{weaponComp.MyCube.CubeGrid.DebugName} - WeaponName:{weaponComp.MyCube.BlockDefinition.Id.SubtypeId.String} - !Marked:{!weaponComp.MyCube.MarkedForClose} - inScene:{weaponComp.MyCube.InScene} - gridMatch:{weaponComp.MyCube.CubeGrid == weaponComp.Ai.MyGrid} - {weaponComp.Ai.MyGrid.MarkedForClose}");
                    weaponComp.RemoveComp();
                    InitComp(weaponComp.MyCube, false);
                    reassign = true;
                    CompsToStart.Remove(weaponComp);
                }
                else if (weaponComp.Platform == null)
                {
                    if (!GridToFatMap.ContainsKey(weaponComp.MyCube.CubeGrid))
                    {
                        Log.Line($"grid not yet in map2");
                        continue;
                    }
                    //Log.Line($"[Init] MyCubeId:{weaponComp.MyCube.EntityId} - Grid:{weaponComp.MyCube.CubeGrid.DebugName} - !Marked:{!weaponComp.MyCube.MarkedForClose} - inScene:{weaponComp.MyCube.InScene} - gridMatch:{weaponComp.MyCube.CubeGrid == weaponComp.Ai.MyGrid}");
                    weaponComp.MyCube.Components.Add(weaponComp);
                    CompsToStart.Remove(weaponComp);
                }
                else
                {
                    //Log.Line($"[Other] MyCubeId:{weaponComp.MyCube.EntityId} - Grid:{weaponComp.MyCube.CubeGrid.DebugName} - WeaponName:{weaponComp.Ob.SubtypeId.String} - !Marked:{!weaponComp.MyCube.MarkedForClose} - inScene:{weaponComp.MyCube.InScene} - gridMatch:{weaponComp.MyCube.CubeGrid == weaponComp.Ai.MyGrid}");
                    CompsToStart.Remove(weaponComp);
                }
            }
            CompsToStart.ApplyRemovals();
            if (reassign)
            {
                CompsToStart.ApplyAdditions();
                StartComps();
            }
        }

        private void InitComp(MyCubeBlock cube, bool apply = true)
        {
            if (!WeaponPlatforms.ContainsKey(cube.BlockDefinition.Id.SubtypeId)) return;

            using (cube.Pin())
            {
                if (cube.MarkedForClose) return;
                GridAi gridAi;
                if (!GridTargetingAIs.TryGetValue(cube.CubeGrid, out gridAi))
                {
                    gridAi = new GridAi(cube.CubeGrid, this, Tick);
                    GridTargetingAIs.TryAdd(cube.CubeGrid, gridAi);
                }
                var weaponComp = new WeaponComponent(gridAi, cube);
                if (gridAi != null && gridAi.WeaponBase.TryAdd(cube, weaponComp))
                {
                    if (!gridAi.WeaponCounter.ContainsKey(cube.BlockDefinition.Id.SubtypeId))
                        gridAi.WeaponCounter.TryAdd(cube.BlockDefinition.Id.SubtypeId, new GridAi.WeaponCount());

                    CompsToStart.Add(weaponComp);
                    if (apply) CompsToStart.ApplyAdditions();
                }
            }
        }

        private void UpdatePlacer()
        {
            if (!Placer.Visible) Placer = null;
            if (!MyCubeBuilder.Static.DynamicMode && MyCubeBuilder.Static.HitInfo.HasValue)
            {
                var hit = MyCubeBuilder.Static.HitInfo.Value as IHitInfo;
                var grid = hit.HitEntity as MyCubeGrid;
                GridAi gridAi;
                if (grid != null && GridTargetingAIs.TryGetValue(grid, out gridAi))
                {
                    if (MyCubeBuilder.Static.CurrentBlockDefinition != null)
                    {
                        var subtypeIdHash = MyCubeBuilder.Static.CurrentBlockDefinition.Id.SubtypeId;
                        GridAi.WeaponCount weaponCount;
                        if (gridAi.WeaponCounter.TryGetValue(subtypeIdHash, out weaponCount))
                        {
                            if (weaponCount.Current >= weaponCount.Max && weaponCount.Max > 0)
                            {
                                MyCubeBuilder.Static.NotifyPlacementUnable();
                                MyCubeBuilder.Static.Deactivate();
                            }
                        }
                    }
                }
            }
        }

        private void UpdateGrids()
        {
            //Log.Line($"[UpdateGrids] DirtTmp:{DirtyGridsTmp.Count} - Dirt:{DirtyGrids.Count}");
            //DsUtil2.Start("UpdateGrids");

            DirtyGridsTmp.Clear();
            DirtyGridsTmp.AddRange(DirtyGrids);
            DirtyGrids.Clear();

            for (int i = 0; i < DirtyGridsTmp.Count; i++)
            {
                var grid = DirtyGridsTmp[i];
                MyConcurrentList<MyCubeBlock> allFat;
                ConcurrentDictionary<TargetingDefinition.BlockTypes, MyConcurrentList<MyCubeBlock>> collection;
                if (GridToFatMap.TryGetValue(grid, out allFat))
                {
                    if (GridToBlockTypeMap.TryRemove(grid, out collection))
                    {
                        foreach (var item in collection)
                            item.Value.Clear();

                        for (int j = 0; j < allFat.Count; j++)
                        {
                            var fat = allFat[j];
                            if (fat == null) continue;

                            using (fat.Pin())
                            {
                                if (fat.MarkedForClose) continue;
                                if (fat is IMyProductionBlock) collection[Production].Add(fat);
                                else if (fat is IMyPowerProducer) collection[Power].Add(fat);
                                else if (fat is IMyGunBaseUser || fat is IMyWarhead) collection[Offense].Add(fat);
                                else if (fat is IMyUpgradeModule || fat is IMyRadioAntenna) collection[Utility].Add(fat);
                                else if (fat is MyThrust) collection[Thrust].Add(fat);
                                else if (fat is MyGyro) collection[Steering].Add(fat);
                                else if (fat is MyJumpDrive) collection[Jumping].Add(fat);
                            }
                        }
                    }
                    else
                    {
                        collection = BlockTypePool.Get();

                        collection[Offense] = ConcurrentListPool.Get();
                        collection[Utility] = ConcurrentListPool.Get();
                        collection[Thrust] = ConcurrentListPool.Get();
                        collection[Steering] = ConcurrentListPool.Get();
                        collection[Jumping] = ConcurrentListPool.Get();
                        collection[Power] = ConcurrentListPool.Get();
                        collection[Production] = ConcurrentListPool.Get();

                        for (int j = 0; j < allFat.Count; j++)
                        {
                            var fat = allFat[j];
                            if (fat == null) continue;

                            using (fat.Pin())
                            {
                                if (fat.MarkedForClose) continue;
                                if (fat is IMyProductionBlock) collection[Production].Add(fat);
                                else if (fat is IMyPowerProducer) collection[Power].Add(fat);
                                else if (fat is IMyGunBaseUser || fat is IMyWarhead) collection[Offense].Add(fat);
                                else if (fat is IMyUpgradeModule || fat is IMyRadioAntenna) collection[Utility].Add(fat);
                                else if (fat is MyThrust) collection[Thrust].Add(fat);
                                else if (fat is MyGyro) collection[Steering].Add(fat);
                                else if (fat is MyJumpDrive) collection[Jumping].Add(fat);
                            }
                        }
                        GridToBlockTypeMap.Add(grid, collection);
                    }
                }
                else if (GridToBlockTypeMap.TryRemove(grid, out collection))
                {
                    foreach (var item in collection)
                        item.Value.Clear();

                    BlockTypePool.Return(collection);
                }
            }
            DirtyGridsTmp.Clear();
            //DsUtil2.Complete("UpdateGrids", false, true);
        }

        private void UpdateGridsCallBack()
        {
            GridsUpdated = true;
        }

        #region Events
        private void PlayerConnected(long id)
        {
            try
            {
                if (Players.ContainsKey(id)) return;
                MyAPIGateway.Multiplayer.Players.GetPlayers(null, myPlayer => FindPlayer(myPlayer, id));
            }
            catch (Exception ex) { Log.Line($"Exception in PlayerConnected: {ex}"); }
        }

        internal void CheckDirtyGrids()
        {
            if (!NewGrids.IsEmpty)
                AddGridToMap();

            if ((!GameLoaded || Tick20) && DirtyGrids.Count > 0)
                MyAPIGateway.Parallel.StartBackground(UpdateGrids, UpdateGridsCallBack);
        }

        private void PlayerDisconnected(long l)
        {
            try
            {
                IMyPlayer removedPlayer;
                Players.TryRemove(l, out removedPlayer);
                PlayerEventId++;
                if (removedPlayer.SteamUserId == AuthorSteamId)
                {
                    AuthorPlayerId = 0;
                }
            }
            catch (Exception ex) { Log.Line($"Exception in PlayerDisconnected: {ex}"); }
        }

        private bool FindPlayer(IMyPlayer player, long id)
        {
            if (player.IdentityId == id)
            {
                Players[id] = player;
                PlayerEventId++;
                if (player.SteamUserId == AuthorSteamId) AuthorPlayerId = player.IdentityId;
            }
            return false;
        }
        #endregion

        #region Misc
        public string ModPath()
        {
            var modPath = ModContext.ModPath;
            return modPath;
        }

      
        internal void UpdateWeaponHeat(object heatTracker)
        {

            var ht = heatTracker as MyTuple<Weapon, int, bool>?;
            if (ht != null)
            {
                var w = ht.Value.Item1;
                var currentHeat = w.Comp.State.Value.Weapons[w.WeaponId].Heat;
                currentHeat = currentHeat - ((float)w.HsRate / 3) > 0 ? currentHeat - ((float)w.HsRate / 3) : 0;
                var set = currentHeat - w.LastHeat > 0.001 || (currentHeat - w.LastHeat) * -1 > 0.001;

                if (!DedicatedServer)
                {
                    
                    var heatPercent = currentHeat / w.System.MaxHeat;

                    if (set && heatPercent > .33)
                    {
                        if (heatPercent > 1) heatPercent = 1;

                        heatPercent -= .33f;

                        var intensity = .7f * heatPercent;

                        var color = HeatEmissives[(int)(heatPercent * 100)];

                        w.MuzzlePart.Item1.SetEmissiveParts("Heating", color, intensity);
                    }
                    else if (set)
                        w.MuzzlePart.Item1.SetEmissiveParts("Heating", Color.Transparent, 0);

                    w.LastHeat = currentHeat;
                }


                if (set && w.System.DegRof && w.Comp.State.Value.Weapons[w.WeaponId].Heat >= (w.System.MaxHeat * .8))
                {
                    var systemRate = w.System.RateOfFire * w.Comp.Set.Value.ROFModifier;
                    var newRate = (int)MathHelper.Lerp(systemRate, systemRate/4, w.Comp.State.Value.Weapons[w.WeaponId].Heat/ w.System.MaxHeat);

                    if (newRate < 1)
                        newRate = 1;

                    w.RateOfFire = newRate;
                    w.TicksPerShot = (uint)(3600f / w.RateOfFire);
                    w.UpdateBarrelRotation();
                    w.CurrentlyDegrading = true;
                }
                else if (set && w.CurrentlyDegrading)
                {
                    w.CurrentlyDegrading = false;
                    w.RateOfFire = (int)(w.System.RateOfFire * w.Comp.Set.Value.ROFModifier);
                    w.TicksPerShot = (uint)(3600f / w.RateOfFire);
                    w.UpdateBarrelRotation();
                }

                var resetFakeTick = false;

                if (ht.Value.Item2 * 30 == 60)
                {
                    var weaponValue = w.Comp.State.Value.Weapons[w.WeaponId];
                    w.Comp.CurrentHeat = w.Comp.CurrentHeat >= w.HsRate ? w.Comp.CurrentHeat - w.HsRate : 0;
                    weaponValue.Heat = weaponValue.Heat >= w.HsRate ? weaponValue.Heat - w.HsRate : 0;

                    w.Comp.TerminalRefresh();
                    if (w.Comp.Overheated && weaponValue.Heat <= (w.System.MaxHeat * w.System.WepCooldown))
                    {
                        w.EventTriggerStateChanged(Weapon.EventTriggers.Overheated, false);
                        w.Comp.Overheated = false;
                    }

                    resetFakeTick = true;
                }

                if (w.Comp.State.Value.Weapons[w.WeaponId].Heat > 0 || ht.Value.Item3)
                {
                    int fakeTick;
                    if (resetFakeTick)
                        fakeTick = 0;
                    else
                        fakeTick = ht.Value.Item2 + 1;

                    _futureEvents.Schedule(UpdateWeaponHeat, MyTuple.Create(w, fakeTick, false), 20);
                }
            }
        }

        internal void FixPrefabs()
        {
            var sMissileBuilder = (MyObjectBuilder_LargeMissileTurret)MyObjectBuilderSerializer.CreateNewObject(new MyDefinitionId(typeof(MyObjectBuilder_LargeMissileTurret), "SmallMissileLauncher"));
            var rMissileBuilder = (MyObjectBuilder_LargeMissileTurret)MyObjectBuilderSerializer.CreateNewObject(new MyDefinitionId(typeof(MyObjectBuilder_LargeMissileTurret), "SmallRocketLauncherReload"));
            var sGatBuilder = (MyObjectBuilder_LargeMissileTurret)MyObjectBuilderSerializer.CreateNewObject(new MyDefinitionId(typeof(MyObjectBuilder_LargeMissileTurret), "SmallGatlingGun"));
            var gatBuilder = (MyObjectBuilder_LargeMissileTurret)MyObjectBuilderSerializer.CreateNewObject(new MyDefinitionId(typeof(MyObjectBuilder_LargeMissileTurret), "LargeGatlingTurret"));
            var lSGatBuilder = (MyObjectBuilder_LargeMissileTurret)MyObjectBuilderSerializer.CreateNewObject(new MyDefinitionId(typeof(MyObjectBuilder_LargeMissileTurret), "SmallGatlingTurret"));
            var missileBuilder = (MyObjectBuilder_LargeMissileTurret)MyObjectBuilderSerializer.CreateNewObject(new MyDefinitionId(typeof(MyObjectBuilder_LargeMissileTurret), "LargeMissileTurret"));
            var interBuilder = (MyObjectBuilder_LargeMissileTurret)MyObjectBuilderSerializer.CreateNewObject(new MyDefinitionId(typeof(MyObjectBuilder_LargeMissileTurret), "LargeInteriorTurret"));

            foreach (var definition in MyDefinitionManager.Static.GetPrefabDefinitions())
            {
                for (int j = 0; j < definition.Value.CubeGrids.Length; j++)
                {
                    for (int i = 0; i < definition.Value.CubeGrids[j].CubeBlocks.Count; i++)
                    {
                        try
                        {
                            switch (definition.Value.CubeGrids[j].CubeBlocks[i].TypeId.ToString())
                            {
                                case "MyObjectBuilder_SmallMissileLauncher":
                                    if (string.IsNullOrEmpty(definition.Value.CubeGrids[j].CubeBlocks[i].SubtypeId
                                        .String))
                                    {
                                        var origOB = definition.Value.CubeGrids[j].CubeBlocks[i];
                                        var newSMissileOB = (MyObjectBuilder_CubeBlock)sMissileBuilder.Clone();
                                        newSMissileOB.EntityId = 0;
                                        newSMissileOB.BlockOrientation = origOB.BlockOrientation;
                                        newSMissileOB.Min = origOB.Min;
                                        newSMissileOB.ColorMaskHSV = origOB.ColorMaskHSV;
                                        newSMissileOB.Owner = origOB.Owner;

                                        definition.Value.CubeGrids[j].CubeBlocks[i] = newSMissileOB;
                                    }
                                    break;

                                case "MyObjectBuilder_SmallMissileLauncherReload":
                                    if (definition.Value.CubeGrids[j].CubeBlocks[i].SubtypeId.String == "SmallRocketLauncherReload")
                                    {
                                        var origOB = definition.Value.CubeGrids[j].CubeBlocks[i];
                                        var newSMissileOB = (MyObjectBuilder_CubeBlock)rMissileBuilder.Clone();
                                        newSMissileOB.EntityId = 0;
                                        newSMissileOB.BlockOrientation = origOB.BlockOrientation;
                                        newSMissileOB.Min = origOB.Min;
                                        newSMissileOB.ColorMaskHSV = origOB.ColorMaskHSV;
                                        newSMissileOB.Owner = origOB.Owner;

                                        definition.Value.CubeGrids[j].CubeBlocks[i] = newSMissileOB;
                                    }
                                    break;

                                case "MyObjectBuilder_SmallGatlingGun":
                                    if (string.IsNullOrEmpty(definition.Value.CubeGrids[j].CubeBlocks[i].SubtypeId
                                        .String))
                                    {
                                        var origOB = definition.Value.CubeGrids[j].CubeBlocks[i];
                                        var newSGatOB = (MyObjectBuilder_CubeBlock)sGatBuilder.Clone();
                                        newSGatOB.EntityId = 0;
                                        newSGatOB.BlockOrientation = origOB.BlockOrientation;
                                        newSGatOB.Min = origOB.Min;
                                        newSGatOB.ColorMaskHSV = origOB.ColorMaskHSV;
                                        newSGatOB.Owner = origOB.Owner;

                                        definition.Value.CubeGrids[j].CubeBlocks[i] = newSGatOB;
                                    }

                                    break;

                                case "MyObjectBuilder_LargeGatlingTurret":
                                    if (string.IsNullOrEmpty(definition.Value.CubeGrids[j].CubeBlocks[i].SubtypeId
                                        .String))
                                    {
                                        var origOB = definition.Value.CubeGrids[j].CubeBlocks[i];
                                        var newGatOB = (MyObjectBuilder_CubeBlock)gatBuilder.Clone();
                                        newGatOB.EntityId = 0;
                                        newGatOB.BlockOrientation = origOB.BlockOrientation;
                                        newGatOB.Min = origOB.Min;
                                        newGatOB.ColorMaskHSV = origOB.ColorMaskHSV;
                                        newGatOB.Owner = origOB.Owner;

                                        definition.Value.CubeGrids[j].CubeBlocks[i] = newGatOB;
                                    }
                                    else if (definition.Value.CubeGrids[j].CubeBlocks[i].SubtypeId.String ==
                                             "SmallGatlingTurret")
                                    {
                                        var origOB = definition.Value.CubeGrids[j].CubeBlocks[i];
                                        var newGatOB = (MyObjectBuilder_CubeBlock)lSGatBuilder.Clone();
                                        newGatOB.EntityId = 0;
                                        newGatOB.BlockOrientation = origOB.BlockOrientation;
                                        newGatOB.Min = origOB.Min;
                                        newGatOB.ColorMaskHSV = origOB.ColorMaskHSV;
                                        newGatOB.Owner = origOB.Owner;

                                        definition.Value.CubeGrids[j].CubeBlocks[i] = newGatOB;
                                    }

                                    break;

                                case "MyObjectBuilder_LargeMissileTurret":
                                    if (string.IsNullOrEmpty(definition.Value.CubeGrids[j].CubeBlocks[i].SubtypeId
                                        .String))
                                    {
                                        var origOB = definition.Value.CubeGrids[j].CubeBlocks[i];
                                        var newMissileOB = (MyObjectBuilder_CubeBlock)missileBuilder.Clone();
                                        newMissileOB.EntityId = 0;
                                        newMissileOB.BlockOrientation = origOB.BlockOrientation;
                                        newMissileOB.Min = origOB.Min;
                                        newMissileOB.ColorMaskHSV = origOB.ColorMaskHSV;
                                        newMissileOB.Owner = origOB.Owner;

                                        definition.Value.CubeGrids[j].CubeBlocks[i] = newMissileOB;
                                    }

                                    break;

                                case "MyObjectBuilder_InteriorTurret":
                                    if (definition.Value.CubeGrids[j].CubeBlocks[i].SubtypeId.String ==
                                        "LargeInteriorTurret")
                                    {
                                        var origOB = definition.Value.CubeGrids[j].CubeBlocks[i];
                                        var newInteriorOB = (MyObjectBuilder_CubeBlock)interBuilder.Clone();
                                        newInteriorOB.EntityId = 0;
                                        newInteriorOB.BlockOrientation = origOB.BlockOrientation;
                                        newInteriorOB.Min = origOB.Min;
                                        newInteriorOB.ColorMaskHSV = origOB.ColorMaskHSV;
                                        newInteriorOB.Owner = origOB.Owner;

                                        definition.Value.CubeGrids[j].CubeBlocks[i] = newInteriorOB;
                                    }

                                    break;
                            }
                        }
                        catch (Exception e)
                        {
                            //bad prefab xml
                        }
                    }
                }
            }
            foreach (var definition in MyDefinitionManager.Static.GetSpawnGroupDefinitions())
            {
                try
                {
                    definition.ReloadPrefabs();
                }
                catch (Exception e)
                {
                    //bad prefab xml
                }
            }
        }

        private void WeaponShootOff(object obj)
        {
            var gridAi = obj as GridAi;
            if (gridAi == null) return;

            foreach (var baseValuePair in gridAi.WeaponBase)
            {
                var comp = baseValuePair.Value;
                for (int i = 0; i < comp.Platform.Weapons.Length; i++)
                {
                    var w = comp.Platform.Weapons[i];
                    w.StopReloadSound();
                    w.StopShooting();
                }
            }
        }

        internal void TurnWeaponShootOff(object ai)
        {
            var gridAi = ai as GridAi;
            if(gridAi == null) return;

            foreach (var basePair in gridAi.WeaponBase)
            {
                var comp = basePair.Value;
                if (comp?.Platform == null) return;

                for (int i = 0; i < basePair.Value.Platform.Weapons.Length; i++)
                {
                    var w = basePair.Value.Platform.Weapons[i];
                    if (w == null) return;

                    if (w.ManualShoot == Weapon.TerminalActionState.ShootClick)
                    {
                        w.ManualShoot = Weapon.TerminalActionState.ShootOff;
                        gridAi.ManualComps = gridAi.ManualComps - 1 > 0 ? gridAi.ManualComps - 1 : 0;
                        comp.Shooting = comp.Shooting - 1 > 0 ? comp.Shooting - 1 : 0;
                    }

                }
            }
        }

        internal void ReturnHome(object o)
        {
            var weapon = o as Weapon;
            if (weapon == null) return;

            weapon.ReturnHome = weapon.Comp.ReturnHome = weapon.Comp.Ai.ReturnHome = true;
        }

        internal void PurgeAll()
        {
            _futureEvents.Purge();
            DsUtil.Purge();
            DsUtil2.Purge();

            foreach (var item in _effectedCubes)
            {
                var cubeid = item.Key;
                var blockInfo = item.Value;
                var functBlock = blockInfo.FunctBlock;
                var cube = blockInfo.CubeBlock;

                if (cube == null || cube.MarkedForClose)
                {
                    _effectPurge.Enqueue(cubeid);
                    continue;
                }

                functBlock.EnabledChanged -= ForceDisable;
                functBlock.Enabled = blockInfo.FirstState;
                cube.SetDamageEffect(false);
                _effectPurge.Enqueue(cubeid);
            }

            while (_effectPurge.Count != 0)
            {
                _effectedCubes.Remove(_effectPurge.Dequeue());
            }

            _effectActive = false;
            RemoveEffectsFromGrid.Clear();
            WeaponAmmoPullQueue.Clear();
            DbsToUpdate.Clear();
            AmmoToPullQueue.Clear();
            Hits.Clear();
            AllArmorBaseDefinitions.Clear();
            HeavyArmorBaseDefinitions.Clear();
            AllArmorBaseDefinitions.Clear();
            LargeBlockSphereDb.Clear();
            SmallBlockSphereDb.Clear();
            GridToBlockTypeMap.Clear();
            AnimationsToProcess.Clear();
            AnimationsToQueue.Clear();
            _shrinking.ClearImmediate();
            _afterGlow.ClearImmediate();
            _shrinkPool.Clean();
            _subTypeIdToWeaponDefs.Clear();
            _weaponDefinitions.Clear();
            _slimsSortedList.Clear();
            _destroyedSlims.Clear();
            _slimsSet.Clear();
            _turretDefinitions.Clear();
            CompsToStart.ClearImmediate();
            GridEffectPool.Clean();
            GridEffectsPool.Clean();
            BlockTypePool.Clean();
            ConcurrentListPool.Clean();
            GridToFatMap.Clear();

            foreach (var structure in WeaponPlatforms.Values)
            {
                structure.WeaponSystems.Clear();
                structure.AmmoToWeaponIds.Clear();
            }
            WeaponPlatforms.Clear();

            for (int i = 0; i < Projectiles.Wait.Length; i++)
            {
                Projectiles.CheckPool[i].Clean();
                Projectiles.ShrapnelToSpawn[i].Clear();
                Projectiles.ShrapnelPool[i].Clean();
                Projectiles.FragmentPool[i].Clean();
                Projectiles.CheckPool[i].Clean();
                Projectiles.ProjectilePool[i].DeallocateAll();
                Projectiles.HitEntityPool[i].Clean();
                Projectiles.DrawProjectiles[i].Clear();
                Projectiles.CleanUp[i].Clear();
                Projectiles.TrajectilePool[i].DeallocateAll();
            }

            _weaponDefinitions = null;
            Projectiles = null;
            TrackingAi = null;
            Pointer = null;
            Placer = null;
            Ui = null;
            DbsToUpdate = null;
            TargetGps = null;
            SApi.Unload();
            SApi = null;
            AnimationsToProcess = null;
            AnimationsToQueue = null;

            ProjectileTree.Clear();
            ProjectileTree = null;
            GridTargetingAIs.Clear();
            AllDefinitions = null;
            SoundDefinitions = null;
            ActiveCockPit = null;
            ControlledEntity = null;
        }
        #endregion
    }
}