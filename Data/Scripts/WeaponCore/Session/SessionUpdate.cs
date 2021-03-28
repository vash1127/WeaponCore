using VRageMath;
using WeaponCore.Platform;
using WeaponCore.Projectiles;
using WeaponCore.Support;
using System.Collections.Generic;
using VRage.Game;
using static WeaponCore.Support.Target;
using static WeaponCore.Support.WeaponComponent.Start;
using static WeaponCore.Support.WeaponComponent.ShootActions;
using static WeaponCore.Support.WeaponDefinition.HardPointDef.HardwareDef;
using static WeaponCore.CompStateValues;
namespace WeaponCore
{
    public partial class Session
    {
        private void AiLoop()
        { //Fully Inlined due to keen's mod profiler
            foreach (var ai in GridTargetingAIs.Values)
            {
                ///
                /// GridAi update section
                ///
                ai.MyProjectiles = 0;
                ai.ProInMinCacheRange = 0;
                ai.AccelChecked = false;
                
                if (ai.MarkedForClose || !ai.GridInit || ai.MyGrid == null || ai.Construct.RootAi == null || ai.MyGrid.MarkedForClose)
                    continue;
                
                ai.Concealed = ((uint)ai.MyGrid.Flags & 4) > 0;
                if (ai.Concealed)
                    continue;

                if (!ai.ScanInProgress && Tick - ai.TargetsUpdatedTick > 100 && DbTask.IsComplete)
                    ai.RequestDbUpdate();

                if (ai.DeadProjectiles.Count > 0) {
                    for (int i = 0; i < ai.DeadProjectiles.Count; i++) ai.LiveProjectile.Remove(ai.DeadProjectiles[i]);
                    ai.DeadProjectiles.Clear();
                    ai.LiveProjectileTick = Tick;
                }
                var enemyProjectiles = ai.LiveProjectile.Count > 0;
                ai.CheckProjectiles = Tick - ai.NewProjectileTick <= 1;

                if (ai.UpdatePowerSources || !ai.HadPower && ai.MyGrid.IsPowered || ai.HasPower && !ai.MyGrid.IsPowered || Tick10)
                    ai.UpdateGridPower();

                if (!ai.HasPower || Settings.Enforcement.ServerSleepSupport && IsServer && ai.AwakeComps == 0 && ai.WeaponsTracking == 0 && ai.SleepingComps > 0 && !ai.CheckProjectiles && ai.AiSleep && !ai.DbUpdated) 
                    continue;

                if (IsServer) {

                    if (ai.Construct.RootAi.Construct.NewInventoryDetected)
                        ai.Construct.RootAi.Construct.CheckForMissingAmmo();
                    else if (Tick60 && ai.Construct.RootAi.Construct.RecentItems.Count > 0)
                        ai.Construct.RootAi.Construct.CheckEmptyWeapons();
                }


                ///
                /// Comp update section
                ///
                for (int i = 0; i < ai.Weapons.Count; i++) {

                    var comp = ai.Weapons[i];
                    if (comp.Status != Started)
                        comp.HealthCheck();

                    if (ai.DbUpdated || !comp.UpdatedState) 
                        comp.DetectStateChanges();
                    if (comp.Platform.State != MyWeaponPlatform.PlatformState.Ready || comp.IsAsleep || !comp.IsWorking || comp.MyCube.MarkedForClose || comp.IsDisabled) 
                        continue;

                    if (IsServer && comp.Data.Repo.Base.State.PlayerId > 0 && !ai.Data.Repo.ControllingPlayers.ContainsKey(comp.Data.Repo.Base.State.PlayerId))
                        comp.ResetPlayerControl();

                    if (HandlesInput) {
                        var wasTrack = comp.Data.Repo.Base.State.TrackingReticle;

                        var isControllingPlayer = comp.Data.Repo.Base.State.PlayerId == PlayerId;
                        var track = (isControllingPlayer && (comp.Data.Repo.Base.Set.Overrides.Control != GroupOverrides.ControlModes.Auto) && TargetUi.DrawReticle && !InMenu && comp.Ai.Construct.RootAi.Data.Repo.ControllingPlayers.ContainsKey(PlayerId));
                        if (!MpActive && IsServer)
                            comp.Data.Repo.Base.State.TrackingReticle = track;

                        var needsUpdate = MpActive && track != wasTrack && (comp.Data.Repo.Base.State.PlayerId <= 0 || isControllingPlayer);
                        if (needsUpdate)
                            comp.Session.SendTrackReticleUpdate(comp, track);
                    }

                    var trackReticle = comp.Data.Repo.Base.State.TrackingReticle;
                    comp.WasControlled = comp.UserControlled;
                    comp.UserControlled = comp.Data.Repo.Base.State.Control != ControlMode.None;

                    if (!PlayerMouseStates.TryGetValue(comp.Data.Repo.Base.State.PlayerId, out comp.InputState)) 
                        comp.InputState = DefaultInputStateData;
                    var compManualMode = comp.Data.Repo.Base.State.Control == ControlMode.Camera || (comp.Data.Repo.Base.Set.Overrides.Control == GroupOverrides.ControlModes.Manual && trackReticle);
                    var canManualShoot = !ai.SuppressMouseShoot && !comp.InputState.InMenu;
                    ///
                    /// Weapon update section
                    ///
                    for (int j = 0; j < comp.Platform.Weapons.Length; j++) {

                        var w = comp.Platform.Weapons[j];
                        if (w.WeaponReadyTick > Tick) {

                            if (w.Target.HasTarget && !IsClient)
                                w.Target.Reset(comp.Session.Tick, States.WeaponNotReady);
                            continue;
                        }

                        if (w.AvCapable && Tick20) {
                            var avWasEnabled = w.PlayTurretAv;
                            double distSqr;
                            Vector3D.DistanceSquared(ref CameraPos, ref w.MyPivotPos, out distSqr);
                            w.PlayTurretAv = distSqr < w.System.HardPointAvMaxDistSqr;
                            if (avWasEnabled != w.PlayTurretAv) w.StopBarrelAvTick = Tick;
                        }

                        if (!ai.HadPower && w.ActiveAmmoDef.AmmoDef.Const.MustCharge && w.State.Action != ShootOff) {

                            if (IsServer) {
                                w.State.WeaponMode(comp, ShootOff);
                                w.Ammo.CurrentAmmo = 0;
                            }

                            w.Reloading = false;
                            w.FinishBurst = false;

                            if (w.IsShooting)
                                w.StopShooting();
                        }

                        ///
                        ///Check Reload
                        ///                        

                        if (w.ActiveAmmoDef.AmmoDef.Const.Reloadable && !w.System.DesignatorWeapon && !w.Reloading) {

                            if (IsServer && (w.Ammo.CurrentAmmo == 0 || w.CheckInventorySystem))
                                w.ComputeServerStorage();
                            else if (IsClient) {

                                if (w.ClientReloading && w.Reload.EndId > w.ClientEndId && w.Reload.StartId == w.ClientStartId)
                                    w.Reloaded();
                                else 
                                    w.ClientReload();
                            }
                        }


                        ///
                        /// Update Weapon Hud Info
                        /// 
                        var isWaitingForBurstDelay = w.ShowBurstDelayAsReload && !w.Reloading && w.ShootTick > Tick && w.ShootTick >= w.LastShootTick + w.System.Values.HardPoint.Loading.DelayAfterBurst;
                        if (HandlesInput && (w.Reloading || w.HeatPerc >= 0.01 || isWaitingForBurstDelay) && Tick - w.LastLoadedTick > 30 && !Session.Config.MinimalHud && ActiveControlBlock != null && ai.SubGrids.Contains(ActiveControlBlock.CubeGrid)) {
                            HudUi.TexturesToAdd++;
                            HudUi.WeaponsToDisplay.Add(w);
                        }

                        if (w.System.Armor != ArmorState.IsWeapon)
                            continue;

                        if (w.Target.ClientDirty)
                            w.Target.ClientUpdate(w, w.TargetData);

                        ///
                        /// Check target for expire states
                        /// 
                        bool targetLock = false;
                        var noAmmo =  w.NoMagsToLoad && w.Ammo.CurrentAmmo == 0 && w.ActiveAmmoDef.AmmoDef.Const.Reloadable && !w.System.DesignatorWeapon && Tick - w.LastMagSeenTick > 600;
                        if (w.Target.HasTarget) {

                            if (w.PosChangedTick != Tick) w.UpdatePivotPos();
                            if (!IsClient && noAmmo)
                                w.Target.Reset(Tick, States.Expired);
                            else if (!IsClient && w.Target.Entity == null && w.Target.Projectile == null && (!trackReticle || Tick - PlayerDummyTargets[comp.Data.Repo.Base.State.PlayerId].LastUpdateTick > 120))
                                w.Target.Reset(Tick, States.Expired, !trackReticle);
                            else if (!IsClient && w.Target.Entity != null && (comp.UserControlled && !w.System.SuppressFire || w.Target.Entity.MarkedForClose))
                                w.Target.Reset(Tick, States.Expired);
                            else if (!IsClient && w.Target.Projectile != null && (!ai.LiveProjectile.Contains(w.Target.Projectile) || w.Target.IsProjectile && w.Target.Projectile.State != Projectile.ProjectileState.Alive)) {
                                w.Target.Reset(Tick, States.Expired);
                                w.FastTargetResetTick = Tick + 6;
                            }
                            else if (w.AiEnabled) {

                                if (!Weapon.TrackingTarget(w, w.Target, out targetLock) && !IsClient && w.Target.ExpiredTick != Tick)
                                    w.Target.Reset(Tick, States.LostTracking, !trackReticle && (w.Target.CurrentState != States.RayCheckFailed && !w.Target.HasTarget));
                            }
                            else {

                                Vector3D targetPos;
                                if (w.IsTurret) {

                                    if (!w.TrackTarget && !IsClient) {

                                        if ((comp.TrackingWeapon.Target.Projectile != w.Target.Projectile || w.Target.IsProjectile && w.Target.Projectile.State != Projectile.ProjectileState.Alive || comp.TrackingWeapon.Target.Entity != w.Target.Entity || comp.TrackingWeapon.Target.IsFakeTarget != w.Target.IsFakeTarget))
                                            w.Target.Reset(Tick, States.Expired);
                                        else
                                            targetLock = true;
                                    }
                                    else if (!Weapon.TargetAligned(w, w.Target, out targetPos) && !IsClient)
                                        w.Target.Reset(Tick, States.Expired);
                                }
                                else if (w.TrackTarget && !Weapon.TargetAligned(w, w.Target, out targetPos) && !IsClient)
                                    w.Target.Reset(Tick, States.Expired);
                            }
                        }

                        w.ProjectilesNear = enemyProjectiles && w.System.TrackProjectile && w.Comp.Data.Repo.Base.Set.Overrides.Projectiles && !w.Target.HasTarget && (w.Target.TargetChanged || QCount == w.ShortLoadId );

                        if (comp.Data.Repo.Base.State.Control == ControlMode.Camera && UiInput.MouseButtonPressed)
                            w.Target.TargetPos = Vector3D.Zero;
                        
                        ///
                        /// Queue for target acquire or set to tracking weapon.
                        /// 
                        var seek = trackReticle && !w.Target.IsFakeTarget || (!noAmmo && !w.Target.HasTarget && w.TrackTarget && (comp.TargetNonThreats && ai.TargetingInfo.OtherInRange || ai.TargetingInfo.ThreatInRange) && (!comp.UserControlled && !Settings.Enforcement.DisableAi || w.State.Action == ShootClick));
                        if (!IsClient && (seek || w.TrackTarget && ai.TargetResetTick == Tick && !comp.UserControlled && !Settings.Enforcement.DisableAi) && !w.AcquiringTarget && (comp.Data.Repo.Base.State.Control == ControlMode.None || comp.Data.Repo.Base.State.Control== ControlMode.Ui)) {
                            w.AcquiringTarget = true;
                            AcquireTargets.Add(w);
                        }

                        if (w.Target.TargetChanged) // Target changed
                            w.TargetChanged();

                        ///
                        /// Check weapon's turret to see if its time to go home
                        ///

                        if (w.TurretMode && !w.IsHome && !w.ReturingHome && !w.Target.HasTarget && Tick - w.Target.ResetTick > 239 && !comp.UserControlled && w.State.Action == ShootOff)
                            w.ScheduleWeaponHome();

                        ///
                        /// Determine if its time to shoot
                        ///
                        ///
                        w.AiShooting = targetLock && !comp.UserControlled && !w.System.SuppressFire;
                        var reloading = w.ActiveAmmoDef.AmmoDef.Const.Reloadable && w.ClientMakeUpShots == 0 && (w.Reloading || w.Ammo.CurrentAmmo == 0);
                        var canShoot = !w.State.Overheated && !reloading && !w.System.DesignatorWeapon && (!w.LastEventCanDelay || w.AnimationDelayTick <= Tick || w.ClientMakeUpShots > 0);
                        var fakeTarget = comp.Data.Repo.Base.Set.Overrides.Control == GroupOverrides.ControlModes.Painter && trackReticle && w.Target.IsFakeTarget && w.Target.IsAligned;
                        var validShootStates = fakeTarget || w.State.Action == ShootOn || w.AiShooting && w.State.Action == ShootOff;
                        var manualShot = (compManualMode || w.State.Action == ShootClick) && canManualShoot && comp.InputState.MouseButtonLeft;
                        var delayedFire = w.System.DelayCeaseFire && !w.Target.IsAligned && Tick - w.CeaseFireDelayTick <= w.System.CeaseFireDelay;
                        var shoot = (validShootStates || manualShot || w.FinishBurst || delayedFire);
                        w.LockOnFireState = !shoot && w.System.LockOnFocus && ai.Construct.Data.Repo.FocusData.HasFocus && ai.Construct.Focus.FocusInRange(w);
                        var shotReady = canShoot && (shoot || w.LockOnFireState);

                        if ((shotReady || w.ShootOnce) && ai.CanShoot) {

                            if (w.ShootOnce && IsServer && (shotReady || w.State.Action != ShootOnce))
                                w.ShootOnce = false;

                            if (MpActive && HandlesInput && !ManualShot)
                                ManualShot = !validShootStates && !w.FinishBurst && !delayedFire;

                            if (w.System.DelayCeaseFire && (validShootStates || manualShot || w.FinishBurst))
                                w.CeaseFireDelayTick = Tick;

                            if ((ai.AvailablePowerChanged || ai.RequestedPowerChanged || (w.RecalcPower && Tick60)) && !w.ActiveAmmoDef.AmmoDef.Const.MustCharge) {

                                if ((!ai.RequestIncrease || ai.PowerIncrease) && !Tick60)
                                    w.RecalcPower = true;
                                else {
                                    w.RecalcPower = false;
                                    w.ChargeDelayTicks = 0;
                                }
                            }
                            if (w.ChargeDelayTicks == 0 || w.ChargeUntilTick <= Tick) {

                                if (!w.RequestedPower && !w.ActiveAmmoDef.AmmoDef.Const.MustCharge && !w.System.DesignatorWeapon) {
                                    if (!comp.UnlimitedPower)
                                        ai.RequestedWeaponsDraw += w.RequiredPower;
                                    w.RequestedPower = true;
                                }

                                ShootingWeapons.Add(w);
                            }
                            else if (w.ChargeUntilTick > Tick && !w.ActiveAmmoDef.AmmoDef.Const.MustCharge) {
                                w.Charging = true;
                                w.StopShooting(false);
                            }
                        }
                        else if (w.IsShooting)
                            w.StopShooting();
                        else if (w.BarrelSpinning)
                            w.SpinBarrel(true);

                        if (comp.Debug && !DedicatedServer)
                            WeaponDebug(w);
                    }
                }
                ai.OverPowered = ai.RequestedWeaponsDraw > 0 && ai.RequestedWeaponsDraw > ai.GridMaxPower;
                ai.DbUpdated = false;
            }

            if (DbTask.IsComplete && DbsToUpdate.Count > 0 && !DbUpdating)
                UpdateDbsInQueue();
        }

        private void UpdateChargeWeapons() //Fully Inlined due to keen's mod profiler
        {
            for (int i = ChargingWeapons.Count - 1; i >= 0; i--)
            {
                var w = ChargingWeapons[i];
                var comp = w.Comp;
                var ai = comp.Ai;
                if (ai == null || ai.MyGrid.MarkedForClose || ai.Concealed || !ai.HasPower || comp.MyCube.MarkedForClose || !comp.IsWorking  || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) {
                    
                    if (w.DrawingPower)
                        w.StopPowerDraw();

                    if (ai != null)
                        ai.OverPowered = ai.RequestedWeaponsDraw > 0 && ai.RequestedWeaponsDraw > ai.GridMaxPower;

                    w.Reloading = false;

                    UniqueListRemove(w, ChargingWeaponsIndexer, ChargingWeapons);
                    continue;
                }

                if (Tick60 && w.DrawingPower) {

                    if ((w.Ammo.CurrentCharge + w.UseablePower) < w.MaxCharge) {
                        w.Ammo.CurrentCharge += w.UseablePower;
                        comp.CurrentCharge += w.UseablePower;
                    }
                    else {
                        
                        comp.CurrentCharge -= (w.Ammo.CurrentCharge - w.MaxCharge);
                        
                        if (comp.CurrentCharge < 0)
                            comp.CurrentCharge = 0;

                        w.Ammo.CurrentCharge = w.MaxCharge;
                    }
                }

                if (!w.Reloading || IsServer && w.ChargeUntilTick <= Tick || IsClient && w.Reload.EndId > w.ClientEndId) {

                    if (w.Reloading)
                        w.Reloaded();

                    if (w.DrawingPower)
                        w.StopPowerDraw();

                    ai.OverPowered = ai.RequestedWeaponsDraw > 0 && ai.RequestedWeaponsDraw > ai.GridMaxPower;

                    UniqueListRemove(w, ChargingWeaponsIndexer, ChargingWeapons);
                    continue;
                }

                if (!ai.OverPowered) {

                    if (!w.DrawingPower) {

                        w.OldUseablePower = w.UseablePower;
                        w.UseablePower = w.RequiredPower;
                        if(!w.Comp.UnlimitedPower)
                            w.DrawPower();

                        w.ChargeDelayTicks = 0;
                    }
                    continue;
                }

                if (!w.DrawingPower || ai.RequestedPowerChanged || ai.AvailablePowerChanged || (w.RecalcPower && Tick60)) {

                    if ((!ai.RequestIncrease || ai.PowerIncrease) && !Tick60) {
                        w.RecalcPower = true;
                        continue;
                    }

                    w.RecalcPower = false;

                    var percUseable = w.RequiredPower / ai.RequestedWeaponsDraw;
                    w.OldUseablePower = w.UseablePower;
                    w.UseablePower = (ai.GridMaxPower * .98f) * percUseable;

                    w.ChargeDelayTicks = (uint)(((w.ActiveAmmoDef.AmmoDef.Const.ChargSize - w.Ammo.CurrentCharge) / w.UseablePower) * MyEngineConstants.UPDATE_STEPS_PER_SECOND);
                    w.ChargeUntilTick = w.ChargeDelayTicks + Tick;

                    if (!w.Comp.UnlimitedPower) {
                        if (!w.DrawingPower)
                            w.DrawPower();
                        else
                            w.DrawPower(true);
                    }
                }
            }
        }

        private void CheckAcquire()
        {
            for (int i = AcquireTargets.Count - 1; i >= 0; i--)
            {
                var w = AcquireTargets[i];
                var comp = w.Comp;
                if (w.Comp.IsAsleep || w.Comp.Ai == null || comp.Ai.MyGrid.MarkedForClose || !comp.Ai.HasPower || comp.Ai.Concealed || comp.MyCube.MarkedForClose || !comp.Ai.DbReady || !comp.IsWorking  || w.NoMagsToLoad && w.Ammo.CurrentAmmo == 0 && Tick - w.LastMagSeenTick > 600) {
                    
                    w.AcquiringTarget = false;
                    AcquireTargets.RemoveAtFast(i);
                    continue;
                }

                if (!w.Acquire.Monitoring && IsServer && w.TrackTarget)
                    AcqManager.Monitor(w.Acquire);

                var acquire = (w.Acquire.IsSleeping && AsleepCount == w.Acquire.SlotId || !w.Acquire.IsSleeping && AwakeCount == w.Acquire.SlotId);

                var seekProjectile = w.ProjectilesNear || w.System.TrackProjectile && w.Comp.Data.Repo.Base.Set.Overrides.Projectiles && w.Comp.Ai.CheckProjectiles;
                var checkTime = w.Target.TargetChanged || acquire || seekProjectile || w.FastTargetResetTick == Tick;

                if (checkTime || w.Comp.Ai.TargetResetTick == Tick && w.Target.HasTarget) {

                    if (seekProjectile || comp.Data.Repo.Base.State.TrackingReticle || (comp.TargetNonThreats && w.Comp.Ai.TargetingInfo.OtherInRange || w.Comp.Ai.TargetingInfo.ThreatInRange) && w.Comp.Ai.TargetingInfo.ValidTargetExists(w)) {

                        if (comp.TrackingWeapon != null && comp.TrackingWeapon.System.DesignatorWeapon && comp.TrackingWeapon != w && comp.TrackingWeapon.Target.HasTarget) {

                            var topMost = comp.TrackingWeapon.Target.Entity?.GetTopMostParent();
                            GridAi.AcquireTarget(w, false, topMost);
                        }
                        else
                            GridAi.AcquireTarget(w, w.Comp.Ai.TargetResetTick == Tick);
                    }

                    if (w.Target.HasTarget || !(comp.TargetNonThreats && w.Comp.Ai.TargetingInfo.OtherInRange || w.Comp.Ai.TargetingInfo.ThreatInRange)) {

                        w.AcquiringTarget = false;
                        AcquireTargets.RemoveAtFast(i);
                        if (w.Target.HasTarget && MpActive) {
                            w.Target.PushTargetToClient(w);
                        }
                    }
                }
            }
        }

        private void ShootWeapons()
        {
            for (int i = ShootingWeapons.Count - 1; i >= 0; i--) {

                var w = ShootingWeapons[i];
                var invalidWeapon = w.Comp.MyCube.MarkedForClose || w.Comp.Ai == null || w.Comp.Ai.Concealed || w.Comp.Ai.MyGrid.MarkedForClose || w.Comp.Platform.State != MyWeaponPlatform.PlatformState.Ready;
                var smartTimer = !w.AiEnabled && w.ActiveAmmoDef.AmmoDef.Trajectory.Guidance == WeaponDefinition.AmmoDef.TrajectoryDef.GuidanceType.Smart && Tick - w.LastSmartLosCheck > 180;
                var quickSkip = invalidWeapon || smartTimer && !w.SmartLos() || w.PauseShoot;
                if (quickSkip) continue;

                if (!w.Comp.UnlimitedPower) {

                    //TODO add logic for power priority
                    if (!w.System.DesignatorWeapon && w.Comp.Ai.OverPowered && (w.ActiveAmmoDef.AmmoDef.Const.EnergyAmmo || w.ActiveAmmoDef.AmmoDef.Const.IsHybrid) && !w.ActiveAmmoDef.AmmoDef.Const.MustCharge) {

                        if (w.ChargeDelayTicks == 0) {
                            var percUseable = w.RequiredPower / w.Comp.Ai.RequestedWeaponsDraw;
                            w.OldUseablePower = w.UseablePower;
                            w.UseablePower = (w.Comp.Ai.GridMaxPower * .98f) * percUseable;

                            if (w.DrawingPower)
                                w.DrawPower(true);
                            else
                                w.DrawPower();

                            w.ChargeDelayTicks = (uint)(((w.RequiredPower - w.UseablePower) / w.UseablePower) * MyEngineConstants.UPDATE_STEPS_PER_SECOND);
                            w.ChargeUntilTick = Tick + w.ChargeDelayTicks;
                            w.Charging = true;
                        }
                        else if (w.ChargeUntilTick <= Tick) {
                            w.Charging = false;
                            w.ChargeUntilTick = Tick + w.ChargeDelayTicks;
                        }
                    }
                    else if (!w.ActiveAmmoDef.AmmoDef.Const.MustCharge && (w.Charging || w.ChargeDelayTicks > 0 || w.ResetPower)) {
                        w.OldUseablePower = w.UseablePower;
                        w.UseablePower = w.RequiredPower;
                        w.DrawPower(true);
                        w.ChargeDelayTicks = 0;
                        w.Charging = false;
                        w.ResetPower = false;
                    }

                    if (w.Charging)
                        continue;

                }

                w.Shoot();
            }
            ShootingWeapons.Clear();
        }
    }
}