using VRageMath;
using WeaponCore.Platform;
using WeaponCore.Projectiles;
using WeaponCore.Support;
using System.Collections.Generic;
using VRage.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.Weapons;
using static WeaponCore.Support.Target;
using static WeaponCore.Support.WeaponComponent.Start;
using static WeaponCore.Platform.Weapon.ManualShootActionState;
using static WeaponCore.Support.WeaponDefinition.HardPointDef.HardwareDef;
using static WeaponCore.Support.GridAi;

namespace WeaponCore
{
    public partial class Session
    {
        private void AiLoop()
        { //Fully Inlined due to keen's mod profiler
            foreach (var gridAi in GridTargetingAIs.Values)
            {
                ///
                /// GridAi update section
                ///
                gridAi.MyProjectiles = 0;
                gridAi.ProInMinCacheRange = 0;
                gridAi.AccelChecked = false;
                gridAi.Concealed = ((uint)gridAi.MyGrid.Flags & 4) > 0;

                if (Tick60) {
                    gridAi.SleepingComps = 0;
                    gridAi.TargetNonThreats = false;
                }

                if (!gridAi.GridInit || gridAi.MyGrid.MarkedForClose || gridAi.Concealed)
                    continue;

                if (DbTask.IsComplete && Tick - gridAi.TargetsUpdatedTick > 100)
                    gridAi.RequestDbUpdate();

                if (gridAi.DeadProjectiles.Count > 0) {
                    for (int i = 0; i < gridAi.DeadProjectiles.Count; i++) gridAi.LiveProjectile.Remove(gridAi.DeadProjectiles[i]);
                    gridAi.DeadProjectiles.Clear();
                    gridAi.LiveProjectileTick = Tick;
                }
                gridAi.CheckProjectiles = Tick - gridAi.NewProjectileTick <= 1;

                if (gridAi.UpdatePowerSources || !gridAi.HadPower && gridAi.MyGrid.IsPowered || gridAi.HasPower && !gridAi.MyGrid.IsPowered || Tick10)
                    gridAi.UpdateGridPower();

                if (!gridAi.HasPower || Tick - gridAi.LastWeaponTick > 600 && !gridAi.CheckProjectiles && (!gridAi.TargetingInfo.ThreatInRange && !gridAi.TargetingInfo.OtherInRange || !gridAi.TargetNonThreats && gridAi.TargetingInfo.OtherInRange) && gridAi.Construct.RootAi.ControllingPlayers.Keys.Count <= 0)
                    continue;

                ///
                /// Comp update section
                ///
                for (int i = 0; i < gridAi.Weapons.Count; i++) {

                    var comp = gridAi.Weapons[i];

                    if (Tick60 || !comp.UpdatedState) {
                        comp.DetectStateChanges();
                        if (InMenu && gridAi.LastTerminal == comp.MyCube)
                            comp.TerminalRefresh();
                    }

                    if (comp.Platform.State != MyWeaponPlatform.PlatformState.Ready || comp.IsAsleep || !comp.State.Value.Online || !comp.Set.Value.Overrides.Activate || comp.Status != Started || comp.MyCube.MarkedForClose) {
                        
                        if (comp.Status != Started) comp.HealthCheck();
                        continue;
                    }

                    if (HandlesInput) {
                        comp.WasTrackReticle = comp.TrackReticle;
                        var isControllingPlayer = comp.State.Value.CurrentPlayerControl.PlayerId == PlayerId;

                        comp.TrackReticle = comp.State.Value.OtherPlayerTrackingReticle || (isControllingPlayer && (comp.Set.Value.Overrides.TargetPainter || comp.Set.Value.Overrides.ManualControl) && TargetUi.DrawReticle && !InMenu && comp.Ai.Construct.RootAi.ControllingPlayers.ContainsKey(PlayerId));

                        if (MpActive && isControllingPlayer && comp.TrackReticle != comp.WasTrackReticle)
                            comp.Session.SendTrackReticleUpdate(comp);
                    }

                    comp.WasControlled = comp.UserControlled;
                    comp.UserControlled = comp.State.Value.CurrentPlayerControl.ControlType != ControlType.None;

                    if (!PlayerMouseStates.TryGetValue(comp.State.Value.CurrentPlayerControl.PlayerId, out comp.InputState)) 
                        comp.InputState = DefaultInputStateData;

                    var compManualMode = comp.State.Value.CurrentPlayerControl.ControlType == ControlType.Camera || (comp.Set.Value.Overrides.ManualControl && comp.TrackReticle);
                    var canManualShoot = !gridAi.SupressMouseShoot && !comp.InputState.InMenu;

                    ///
                    /// Weapon update section
                    ///
                    for (int j = 0; j < comp.Platform.Weapons.Length; j++) {

                        var w = comp.Platform.Weapons[j];

                        if (!w.Set.Enable) {

                            if (w.Target.HasTarget && !IsClient)
                                w.Target.Reset(comp.Session.Tick, States.Expired);
                            continue;
                        }

                        if (w.Timings.WeaponReadyTick > Tick) continue;

                        if (w.AvCapable && (!w.PlayTurretAv || Tick60)) {
                            var avWasEnabled = w.PlayTurretAv;
                            w.PlayTurretAv = Vector3D.DistanceSquared(CameraPos, w.MyPivotPos) < w.System.HardPointAvMaxDistSqr;
                            if (avWasEnabled != w.PlayTurretAv) w.StopBarrelAv = !w.PlayTurretAv;

                        }

                        if (!gridAi.HadPower && w.ActiveAmmoDef.AmmoDef.Const.MustCharge && w.State.ManualShoot != ShootOff) {
                            w.State.ManualShoot = ShootOff;
                            w.State.Sync.Reloading = false;
                            w.State.Sync.CurrentAmmo = 0;
                            w.FinishBurst = false;

                            if (w.IsShooting)
                                w.StopShooting();
                        }

                        ///
                        ///Check Reload
                        ///                        
                        
                        if (!w.OutOfAmmo && !w.State.Sync.Reloading && w.ActiveAmmoDef.AmmoDef.Const.Reloadable && w.State.Sync.CurrentAmmo <= 0 && w.CanReload)
                            w.StartReload();

                        ///
                        /// Update Weapon Hud Info
                        /// 

                        if (HandlesInput && !Session.Config.MinimalHud && ((w.State.Sync.Reloading && Tick - w.LastLoadedTick > 30) || (w.State.Sync.Heat > 0)) && ActiveControlBlock != null && gridAi.SubGrids.Contains(ActiveControlBlock.CubeGrid)) {
                            HudUi.TexturesToAdd++;
                            HudUi.WeaponsToDisplay.Add(w);
                        }

                        if (w.System.Armor != ArmorState.IsWeapon)
                            continue;

                        ///
                        /// Check target for expire states
                        /// 

                        if (w.Target.HasTarget && !(IsClient && w.Target.CurrentState == States.Invalid)) {

                            if (w.PosChangedTick != Tick) w.UpdatePivotPos();
                            if (!IsClient && w.Target.Entity == null && w.Target.Projectile == null && (!comp.TrackReticle || PlayerDummyTargets[comp.State.Value.CurrentPlayerControl.PlayerId].ClearTarget))
                                w.Target.Reset(Tick, States.Expired, !comp.TrackReticle);
                            else if (!IsClient && w.Target.Entity != null && (comp.UserControlled || w.Target.Entity.MarkedForClose))
                                w.Target.Reset(Tick, States.Expired);
                            else if (!IsClient && w.Target.Projectile != null && (!gridAi.LiveProjectile.Contains(w.Target.Projectile) || w.Target.IsProjectile && w.Target.Projectile.State != Projectile.ProjectileState.Alive))
                                w.Target.Reset(Tick, States.Expired);
                            else if (w.AiEnabled) {

                                if (!Weapon.TrackingTarget(w, w.Target) && !IsClient)
                                    w.Target.Reset(Tick, States.Expired, !comp.TrackReticle && (w.Target.CurrentState != States.RayCheckFailed && !w.Target.HasTarget));
                            }
                            else {

                                Vector3D targetPos;
                                if (w.IsTurret) {

                                    if (!w.TrackTarget && !IsClient) {

                                        if ((comp.TrackingWeapon.Target.Projectile != w.Target.Projectile || w.Target.IsProjectile && w.Target.Projectile.State != Projectile.ProjectileState.Alive || comp.TrackingWeapon.Target.Entity != w.Target.Entity || comp.TrackingWeapon.Target.IsFakeTarget != w.Target.IsFakeTarget))
                                            w.Target.Reset(Tick, States.Expired);
                                    }
                                    else if (!Weapon.TargetAligned(w, w.Target, out targetPos) && !IsClient)
                                        w.Target.Reset(Tick, States.Expired);
                                }
                                else if (w.TrackTarget && !Weapon.TargetAligned(w, w.Target, out targetPos) && !IsClient)
                                    w.Target.Reset(Tick, States.Expired);
                            }
                        }
                        else if (w.Target.HasTarget && MyEntities.EntityExists(comp.WeaponValues.Targets[w.WeaponId].EntityId)) {
                            w.Target.HasTarget = false;
                            ClientGridResyncRequests.Add(comp);
                        }

                        w.ProjectilesNear = w.TrackProjectiles && !w.Target.HasTarget && (w.Target.TargetChanged || SCount == w.ShortLoadId && gridAi.LiveProjectile.Count > 0);

                        if (comp.State.Value.CurrentPlayerControl.ControlType == ControlType.Camera && UiInput.MouseButtonPressed)
                            w.Target.TargetPos = Vector3D.Zero;

                        ///
                        /// Queue for target acquire or set to tracking weapon.
                        /// 
                        w.SeekTarget = (!IsClient && !w.Target.HasTarget && w.TrackTarget && (comp.TargetNonThreats && gridAi.TargetingInfo.OtherInRange || gridAi.TargetingInfo.ThreatInRange) && (!comp.UserControlled || w.State.ManualShoot == ShootClick)) || comp.TrackReticle && !w.Target.IsFakeTarget;
                        if (!IsClient && (w.SeekTarget || w.TrackTarget && gridAi.TargetResetTick == Tick && !comp.UserControlled) && !w.AcquiringTarget && (comp.State.Value.CurrentPlayerControl.ControlType == ControlType.None || comp.State.Value.CurrentPlayerControl.ControlType == ControlType.Ui))
                        {
                            w.AcquiringTarget = true;
                            AcquireTargets.Add(w);
                        }
                        else if (!IsClient && w.IsTurret && !w.TrackTarget && !w.Target.HasTarget && (comp.TargetNonThreats && gridAi.TargetingInfo.OtherInRange || gridAi.TargetingInfo.ThreatInRange)) {

                            if (w.Target != w.Comp.TrackingWeapon.Target) {
                                w.Target = w.Comp.TrackingWeapon.Target;
                                if (MpActive) w.Target.SyncTarget(comp.WeaponValues.Targets[w.WeaponId], w);
                            }
                        }

                        if (w.Target.TargetChanged) // Target changed
                            w.TargetChanged();

                        ///
                        /// Check weapon's turret to see if its time to go home
                        ///

                        if (w.TurretMode && !w.ReturingHome && !w.IsHome && !w.Target.HasTarget && !comp.UserControlled)
                            w.TurretHomePosition(true);

                        ///
                        /// Determine if its time to shoot
                        ///
                        ///
                        w.AiShooting = w.Target.TargetLock && !comp.UserControlled;
                        var reloading = w.ActiveAmmoDef.AmmoDef.Const.Reloadable && (w.State.Sync.Reloading || w.OutOfAmmo);
                        var canShoot = !w.State.Sync.Overheated && !reloading && !w.System.DesignatorWeapon && (!w.LastEventCanDelay || w.Timings.AnimationDelayTick <= Tick);
                        var fakeTarget = comp.Set.Value.Overrides.TargetPainter && comp.TrackReticle && w.Target.IsFakeTarget && w.Target.IsAligned;
                        var validShootStates = fakeTarget || w.State.ManualShoot == ShootOn || w.State.ManualShoot == ShootOnce || w.AiShooting && w.State.ManualShoot == ShootOff;
                        var manualShot = (compManualMode || w.State.ManualShoot == ShootClick) && canManualShoot && (comp.InputState.MouseButtonLeft && j % 2 == 0 || comp.InputState.MouseButtonRight && j == 1);
                        var delayedFire = w.System.DelayCeaseFire && !w.Target.IsAligned && Tick - w.CeaseFireDelayTick <= w.System.CeaseFireDelay;
                        var shoot = (validShootStates || manualShot || w.FinishBurst || delayedFire);
                        w.LockOnFireState = !shoot && w.System.LockOnFocus && gridAi.Focus.HasFocus && gridAi.Focus.FocusInRange(w);
                        var fire = canShoot && (shoot || w.LockOnFireState);

                        if (fire) {

                            if (w.System.DelayCeaseFire && (validShootStates || manualShot || w.FinishBurst))
                                w.CeaseFireDelayTick = Tick;

                            if ((gridAi.AvailablePowerChanged || gridAi.RequestedPowerChanged || (w.RecalcPower && Tick60)) && !w.ActiveAmmoDef.AmmoDef.Const.MustCharge) {

                                if ((!gridAi.RequestIncrease || gridAi.PowerIncrease) && !Tick60)
                                    w.RecalcPower = true;
                                else {
                                    w.RecalcPower = false;
                                    w.Timings.ChargeDelayTicks = 0;
                                }
                            }
                            if (w.Timings.ChargeDelayTicks == 0 || w.Timings.ChargeUntilTick <= Tick) {

                                if (!w.RequestedPower && !w.ActiveAmmoDef.AmmoDef.Const.MustCharge && !w.System.DesignatorWeapon) {
                                    if (!comp.UnlimitedPower)
                                        gridAi.RequestedWeaponsDraw += w.RequiredPower;
                                    w.RequestedPower = true;
                                }

                                ShootingWeapons.Add(w);
                            }
                            else if (w.Timings.ChargeUntilTick > Tick && !w.ActiveAmmoDef.AmmoDef.Const.MustCharge) {
                                w.State.Sync.Charging = true;
                                w.StopShooting(false, false);
                            }
                        }
                        else if (w.IsShooting)  
                            w.StopShooting();
                        else if (w.BarrelSpinning)
                            w.SpinBarrel(true);

                        if (comp.Debug && !DedicatedServer)
                            WeaponDebug(w);

                        if (fire || reloading || !canShoot || !w.IsHome || w.Target.HasTarget) {

                            //if (!canShoot)
                                //Log.Line($"AiShooting:{w.AiShooting} || ManualShoot:{w.State.ManualShoot == ShootOff} || TargetLock:{w.Target.TargetLock} || !comp.UserControlled:{!comp.UserControlled}");

                            w.LastWeaponEvent = Tick;
                            gridAi.LastWeaponTick = Tick;
                        }
                    }
                }
                gridAi.OverPowered = gridAi.RequestedWeaponsDraw > 0 && gridAi.RequestedWeaponsDraw > gridAi.GridMaxPower;
            }

            if (DbTask.IsComplete && DbsToUpdate.Count > 0)
                UpdateDbsInQueue();
        }

        private void UpdateChargeWeapons() //Fully Inlined due to keen's mod profiler
        {
            for (int i = ChargingWeapons.Count - 1; i >= 0; i--)
            {
                var w = ChargingWeapons[i];
                var comp = w.Comp;
                var gridAi = comp.Ai;
                if (comp == null || w.Comp.Ai == null || gridAi.MyGrid.MarkedForClose || gridAi.Concealed || !gridAi.HasPower || comp.MyCube.MarkedForClose || !w.Set.Enable || !comp.State.Value.Online || !comp.Set.Value.Overrides.Activate)
                {
                    if (w.DrawingPower)
                        w.StopPowerDraw();

                    if (w.Comp?.Ai != null) 
                        w.Comp.Ai.OverPowered = w.Comp.Ai.RequestedWeaponsDraw > 0 && w.Comp.Ai.RequestedWeaponsDraw > w.Comp.Ai.GridMaxPower;
                    w.State.Sync.Reloading = false;

                    w.State.Sync.Reloading = false;

                    UniqueListRemove(w, ChargingWeaponsIndexer, ChargingWeapons);
                    continue;
                }

                if (comp.Platform.State != MyWeaponPlatform.PlatformState.Ready)
                    continue;

                var wState = w.Comp.State.Value.Weapons[w.WeaponId];
                var cState = w.Comp.State.Value;

                if (Tick60 && w.DrawingPower)
                {
                    if ((wState.Sync.CurrentCharge + w.UseablePower) < w.MaxCharge)
                    {
                        wState.Sync.CurrentCharge += w.UseablePower;
                        cState.CurrentCharge += w.UseablePower;

                    }
                    else
                    {
                        w.Comp.State.Value.CurrentCharge -= (wState.Sync.CurrentCharge - w.MaxCharge);
                        wState.Sync.CurrentCharge = w.MaxCharge;
                    }
                }

                if (w.Timings.ChargeUntilTick <= Tick || !w.State.Sync.Reloading)
                {
                    if (w.State.Sync.Reloading)
                        w.Reloaded();

                    if (w.DrawingPower)
                        w.StopPowerDraw();

                    w.Comp.Ai.OverPowered = w.Comp.Ai.RequestedWeaponsDraw > 0 && w.Comp.Ai.RequestedWeaponsDraw > w.Comp.Ai.GridMaxPower;

                    UniqueListRemove(w, ChargingWeaponsIndexer, ChargingWeapons);
                    continue;
                }

                if (!w.Comp.Ai.OverPowered)
                {
                    if (!w.DrawingPower)
                    {
                        w.OldUseablePower = w.UseablePower;
                        w.UseablePower = w.RequiredPower;
                        if(!w.Comp.UnlimitedPower)
                            w.DrawPower();

                        w.Timings.ChargeDelayTicks = 0;
                    }

                    continue;
                }

                if (!w.DrawingPower || gridAi.RequestedPowerChanged || gridAi.AvailablePowerChanged || (w.RecalcPower && Tick60))
                {
                    if ((!gridAi.RequestIncrease || gridAi.PowerIncrease) && !Tick60)
                    {
                        w.RecalcPower = true;
                        continue;
                    }

                    w.RecalcPower = false;

                    var percUseable = w.RequiredPower / w.Comp.Ai.RequestedWeaponsDraw;
                    w.OldUseablePower = w.UseablePower;
                    w.UseablePower = (w.Comp.Ai.GridMaxPower * .98f) * percUseable;

                    w.Timings.ChargeDelayTicks = (uint)(((w.ActiveAmmoDef.AmmoDef.Const.ChargSize - wState.Sync.CurrentCharge) / w.UseablePower) * MyEngineConstants.UPDATE_STEPS_PER_SECOND);
                    w.Timings.ChargeUntilTick = w.Timings.ChargeDelayTicks + Tick;
                    if (!w.Comp.UnlimitedPower)
                    {
                        if (!w.DrawingPower)
                            w.DrawPower();
                        else
                            w.DrawPower(true);
                    }
                }
            }
        }

        internal int LowAcquireChecks = int.MaxValue;
        internal int HighAcquireChecks = int.MinValue;
        internal int AverageAcquireChecks;
        internal int TotalAcquireChecks;
        internal int AcquireChecks;

        private void CheckAcquire()
        {
            for (int i = AcquireTargets.Count - 1; i >= 0; i--)
            {
                var w = AcquireTargets[i];
                var comp = w.Comp;
                if (w.Comp.Ai == null || comp.Ai.MyGrid.MarkedForClose || !comp.Ai.HasPower || comp.Ai.Concealed || comp.MyCube.MarkedForClose || !comp.Ai.DbReady || !w.Set.Enable || !comp.State.Value.Online || !comp.Set.Value.Overrides.Activate) {
                    
                    w.AcquiringTarget = false;
                    AcquireTargets.RemoveAtFast(i);
                    continue;
                }

                var gridAi = w.Comp.Ai;
                //var sinceCheck = Tick - w.Target.CheckTick;

                var wa = w.WeaponAcquire;

                if (!wa.Enabled)
                    AcquireManager.AddAwake(wa);

                var acquire = (wa.Asleep && AsleepCount == wa.SlotId || !wa.Asleep && AwakeCount == wa.SlotId);

                var seekProjectile = w.ProjectilesNear || w.TrackProjectiles && gridAi.CheckProjectiles;

                var checkTime = w.Target.TargetChanged || acquire || seekProjectile;

                if (checkTime || gridAi.TargetResetTick == Tick && w.Target.HasTarget) {

                    if (seekProjectile || comp.TrackReticle || (comp.TargetNonThreats && gridAi.TargetingInfo.OtherInRange || gridAi.TargetingInfo.ThreatInRange) && gridAi.TargetingInfo.ValidTargetExists(w)) {
                        
                        AcquireChecks++;
                        if (comp.TrackingWeapon != null && comp.TrackingWeapon.System.DesignatorWeapon && comp.TrackingWeapon != w && comp.TrackingWeapon.Target.HasTarget) {
                            var topMost = comp.TrackingWeapon.Target.Entity?.GetTopMostParent();
                            GridAi.AcquireTarget(w, false, topMost);
                        }
                        else
                            GridAi.AcquireTarget(w, gridAi.TargetResetTick == Tick);
                    }

                    if (w.Target.HasTarget || !(comp.TargetNonThreats && gridAi.TargetingInfo.OtherInRange || gridAi.TargetingInfo.ThreatInRange)) {

                        w.AcquiringTarget = false;
                        AcquireTargets.RemoveAtFast(i);
                        if (w.Target.HasTarget && MpActive) {
                            w.Target.SyncTarget(comp.WeaponValues.Targets[w.WeaponId], w);
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

                        if (w.Timings.ChargeDelayTicks == 0) {
                            var percUseable = w.RequiredPower / w.Comp.Ai.RequestedWeaponsDraw;
                            w.OldUseablePower = w.UseablePower;
                            w.UseablePower = (w.Comp.Ai.GridMaxPower * .98f) * percUseable;

                            if (w.DrawingPower)
                                w.DrawPower(true);
                            else
                                w.DrawPower();

                            w.Timings.ChargeDelayTicks = (uint)(((w.RequiredPower - w.UseablePower) / w.UseablePower) * MyEngineConstants.UPDATE_STEPS_PER_SECOND);
                            w.Timings.ChargeUntilTick = Tick + w.Timings.ChargeDelayTicks;
                            w.State.Sync.Charging = true;
                        }
                        else if (w.Timings.ChargeUntilTick <= Tick) {
                            w.State.Sync.Charging = false;
                            w.Timings.ChargeUntilTick = Tick + w.Timings.ChargeDelayTicks;
                        }
                    }
                    else if (!w.ActiveAmmoDef.AmmoDef.Const.MustCharge && (w.State.Sync.Charging || w.Timings.ChargeDelayTicks > 0 || w.ResetPower)) {
                        w.OldUseablePower = w.UseablePower;
                        w.UseablePower = w.RequiredPower;
                        w.DrawPower(true);
                        w.Timings.ChargeDelayTicks = 0;
                        w.State.Sync.Charging = false;
                        w.ResetPower = false;
                    }

                    if (w.State.Sync.Charging)
                        continue;

                }

                w.Shoot();

                if (MpActive && IsServer && !w.IsTurret && w.ActiveAmmoDef.AmmoDef.Const.EnergyAmmo && Tick - w.LastSyncTick > ResyncMinDelayTicks) w.ForceSync();
            }
            ShootingWeapons.Clear();
        }
    }
}