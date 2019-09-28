using System;
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
using VRage.ObjectBuilders;

namespace WeaponCore
{
    public partial class Session
    {
        public void UpdateDbsInQueue()
        {
            DbsUpdating = true;
            MyAPIGateway.Parallel.Start(ProcessDbs, ProcessDbsCallBack);
            //ProcessDbs();
            //ProcessDbsCallBack();
        }

        private void ProcessDbs()
        {
            //MyAPIGateway.Parallel.For(0, DbsToUpdate.Count, x => DbsToUpdate[x].UpdateTargetDb(), 6);

            foreach (var db in DbsToUpdate)
            {
                db.UpdateTargetDb();
            }

            foreach (var db in DbsToUpdate)
            {
                db.FinalizeTargetDb();
            }
        }

        private void ProcessDbsCallBack()
        {
            foreach (var db in DbsToUpdate)
            {
                if (db.MyPlanetTmp != null)
                {
                    var gridBox = db.MyGrid.PositionComp.WorldAABB;
                    if (db.MyPlanetTmp.IntersectsWithGravityFast(ref gridBox)) db.MyPlanetInfo();
                    else if (db.MyPlanet != null) db.MyPlanetInfo(clear: true);
                }

                for (int i = 0; i < db.SubGridsTmp.Count; i++) db.SubGrids.Add(db.SubGridsTmp[i]);
                db.SubGridsTmp.Clear();

                for (int i = 0; i < db.SortedTargets.Count; i++) db.SortedTargets[i].Clean();
                db.SortedTargets.Clear();
                db.Targets.Clear();

                for (int i = 0; i < db.NewEntities.Count; i++)
                {
                    var detectInfo = db.NewEntities[i];
                    var ent = detectInfo.Parent;
                    var dictTypes = detectInfo.DictTypes;
                    var grid = ent as MyCubeGrid;
                    GridAi.TargetInfo targetInfo;
                    var protectedByShield = ShieldApiLoaded && SApi.ProtectedByShield(ent);
                    if (grid == null)
                        targetInfo = new GridAi.TargetInfo(detectInfo.EntInfo, ent, false, protectedByShield, null, 1, db.MyGrid, db);
                    else
                        targetInfo = new GridAi.TargetInfo(detectInfo.EntInfo, grid, true, protectedByShield, dictTypes, grid.GetFatBlocks().Count, db.MyGrid, db) { TypeDict = dictTypes };

                    db.SortedTargets.Add(targetInfo);
                    db.Targets.Add(ent, targetInfo);
                }
                db.NewEntities.Clear();
                db.SortedTargets.Sort(db.TargetCompare1);

                db.Threats.Clear();
                db.Threats.Capacity = db.ThreatsTmp.Count;
                for (var i = 0; i < db.ThreatsTmp.Count; i++) db.Threats.Add(db.ThreatsTmp[i]);
                db.ThreatsTmp.Clear();

                db.TargetAis.Clear();
                db.TargetAis.Capacity = db.TargetAisTmp.Count;
                for (var i = 0; i < db.TargetAisTmp.Count; i++) db.TargetAis.Add(db.TargetAisTmp[i]);
                db.TargetAisTmp.Clear();

                db.Obstructions.Clear();
                for (int i = 0; i < db.ObstructionsTmp.Count; i++) db.Obstructions.Add(db.ObstructionsTmp[i]);
                db.ObstructionsTmp.Clear();

                db.StaticsInRange.Clear();
                if (db.PlanetSurfaceInRange) db.StaticsInRangeTmp.Add(db.MyPlanet);
                var staticCount = db.StaticsInRangeTmp.Count;

                for (int i = 0; i < staticCount; i++) db.StaticsInRange.Add(db.StaticsInRangeTmp[i]);
                db.StaticsInRangeTmp.Clear();
                db.StaticEntitiesInRange = staticCount > 0;

                db.DbReady = db.SortedTargets.Count > 0 || db.Threats.Count > 0 || db.FirstRun;
                db.FirstRun = false;
                //Log.Line($"[DB] - dbReady:{db.DbReady} - liveProjectiles:{db.LiveProjectile.Count} - armedGrids:{db.Threats.Count} - obstructions:{db.Obstructions.Count} - targets:{db.SortedTargets.Count} - checkedTargets:{db.NewEntities.Count} - targetRoots:{db.Targeting.TargetRoots.Count} - forGrid:{db.MyGrid.DebugName}");
                db.MyShield = db.MyShieldTmp;
                db.ShieldNear = db.ShieldNearTmp;
                Interlocked.Exchange(ref db.DbUpdating, 0);
            }
            DbsToUpdate.Clear();
            DbsUpdating = false;
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
            if (Target == null)
                return false;

            if (Target.MarkedForClose || ai != TrackingAi)
            {
                Log.Line("resetting target");
                Target = null;
                TrackingAi = null;
                RemoveGps();
                return false;
            }

            return true;
        }

        internal void SetTarget(MyEntity entity, GridAi ai)
        {
            Target = entity;
            TrackingAi = ai;

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
                    TargetArmed = info.TypeDict[TargetingDefinition.BlockTypes.Offense].Count > 0;
                    break;
                }
            }
        }

        internal void GetTargetInfo(GridAi ai, out double speed, out string armedStr, out string interceptStr, out string shieldedStr, out string threatStr)
        {
            var targetVel = Target.Physics?.LinearVelocity ?? Vector3.Zero;
            if (MyUtils.IsZero(targetVel, 1E-02F)) targetVel = Vector3.Zero;
            var targetDir = Vector3D.Normalize(targetVel);
            var targetPos = Target.PositionComp.WorldAABB.Center;
            var myPos = ai.MyGrid.PositionComp.WorldAABB.Center;
            var myHeading = Vector3D.Normalize(myPos - targetPos);
            var degrees = Math.Cos(MathHelper.ToRadians(25));
            var intercept = MathFuncs.IsDotProductWithinTolerance(ref targetDir, ref myHeading, degrees);
            var shielded = ShieldApiLoaded && SApi.ProtectedByShield(Target);
            var grid = Target as MyCubeGrid;
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
            speed = Math.Round(Target.Physics?.Speed ?? 0, 1);
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

            if (!CompsToStart.IsEmpty)
            {
                WeaponComponent weaponComp;
                CompsToStart.TryDequeue(out weaponComp);
                weaponComp.MyCube.Components.Add(weaponComp);
                weaponComp.OnAddedToScene();
                weaponComp.Ai.FirstRun = true;
                Log.Line($"added to comp");
            }
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

                //todo client side only
                var currentHeat = w.Comp.State.Value.Weapons[w.WeaponId].Heat;
                currentHeat = currentHeat - ((float)w.HsRate / 3) > 0 ? currentHeat - ((float)w.HsRate / 3) : 0;
                var heatPercent = currentHeat / w.System.MaxHeat;

                var set = currentHeat - w.LastHeat > 0.001 || (currentHeat - w.LastHeat) * -1 > 0.001;

                if (set && heatPercent > .33)
                {
                    if (heatPercent > 1) heatPercent = 1;

                    heatPercent -= .33f;

                    var intensity = .7f * heatPercent;

                    var color = HeatEmissives[(int)(heatPercent * 100)];

                    w.BarrelPart.SetEmissiveParts("Heating", color, intensity);
                }
                else if (set)
                    w.BarrelPart.SetEmissiveParts("Heating", Color.Transparent, 0);

                w.LastHeat = currentHeat;
                //end client side code


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

        internal void TurnWeaponShootOff(Object ai)
        {
            var gridAi = ai as GridAi;
            if(gridAi == null) return;

            foreach (var basePair in gridAi.WeaponBase)
            {
                for (int i = 0; i < basePair.Value.Platform.Weapons.Length; i++)
                {
                    var comp = basePair.Value;
                    var w = basePair.Value.Platform.Weapons[i];
                    if (w.ManualShoot == Weapon.TerminalActionState.ShootClick)
                    {
                        w.ManualShoot = Weapon.TerminalActionState.ShootOff;
                        gridAi.ManualComps = gridAi.ManualComps - 1 > 0 ? gridAi.ManualComps - 1 : 0;
                        comp.Shooting = comp.Shooting - 1 > 0 ? comp.Shooting - 1 : 0;
                    }

                }
            }
        }

        #endregion
    }
}