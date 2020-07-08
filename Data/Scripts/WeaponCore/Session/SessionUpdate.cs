﻿using System;
using VRageMath;
using WeaponCore.Platform;
using WeaponCore.Projectiles;
using WeaponCore.Support;
using System.Collections.Generic;
using VRage.Game;
using Sandbox.Game.Entities;
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
                ai.Concealed = ((uint)ai.MyGrid.Flags & 4) > 0;

                if (!ai.GridInit || ai.MyGrid.MarkedForClose || ai.Concealed)
                    continue;

                if (DbTask.IsComplete && Tick - ai.TargetsUpdatedTick > 100 && !ai.ScanInProgress)
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

                if (!ai.HasPower || false && IsServer && ai.AwakeComps == 0 && ai.WeaponsTracking == 0 && ai.SleepingComps > 0 && !ai.CheckProjectiles && ai.AiSleep && !ai.DbUpdated) 
                    continue;

                ///
                /// Comp update section
                ///
                for (int i = 0; i < ai.Weapons.Count; i++) {

                    var comp = ai.Weapons[i];

                    if (ai.DbUpdated || !comp.UpdatedState) {
                        comp.DetectStateChanges();
                    }

                    if (comp.Platform.State != MyWeaponPlatform.PlatformState.Ready || comp.IsAsleep || !comp.IsWorking || !comp.Data.Repo.Set.Overrides.Activate || comp.Status != Started || comp.MyCube.MarkedForClose) {
                        
                        if (comp.Status != Started) comp.HealthCheck();
                        continue;
                    }

                    if (HandlesInput) {
                        var wasTrack = comp.Data.Repo.State.TrackingReticle;

                        var isControllingPlayer = comp.Data.Repo.State.PlayerId == PlayerId;
                        var track = (isControllingPlayer && (comp.Data.Repo.Set.Overrides.TargetPainter || comp.Data.Repo.Set.Overrides.ManualControl) && TargetUi.DrawReticle && !InMenu && comp.Ai.Construct.RootAi.Data.Repo.ControllingPlayers.ContainsKey(PlayerId));
                        
                        if (IsServer)
                            comp.Data.Repo.State.TrackingReticle = track;
                        
                        
                        if (MpActive && track != wasTrack)
                            comp.Session.SendTrackReticleUpdate(comp, track);
                    }

                    var trackReticle = comp.Data.Repo.State.TrackingReticle;
                    comp.WasControlled = comp.UserControlled;
                    comp.UserControlled = comp.Data.Repo.State.Control != ControlMode.None;

                    if (!PlayerMouseStates.TryGetValue(comp.Data.Repo.State.PlayerId, out comp.InputState)) 
                        comp.InputState = DefaultInputStateData;
                    var compManualMode = comp.Data.Repo.State.Control == ControlMode.Camera || (comp.Data.Repo.Set.Overrides.ManualControl && trackReticle);
                    var canManualShoot = !ai.SupressMouseShoot && !comp.InputState.InMenu;

                    ///
                    /// Weapon update section
                    ///
                    for (int j = 0; j < comp.Platform.Weapons.Length; j++) {

                        var w = comp.Platform.Weapons[j];

                        if (w.WeaponReadyTick > Tick) {

                            if (w.Target.HasTarget && !IsClient)
                                w.Target.Reset(comp.Session.Tick, States.Expired);
                            continue;
                        }

                        if (w.AvCapable && Tick20) {
                            var avWasEnabled = w.PlayTurretAv;
                            double distSqr;
                            Vector3D.DistanceSquared(ref CameraPos, ref w.MyPivotPos, out distSqr);
                            w.PlayTurretAv = distSqr < w.System.HardPointAvMaxDistSqr;
                            if (avWasEnabled != w.PlayTurretAv) w.StopBarrelAv = !w.PlayTurretAv;

                        }

                        if (!ai.HadPower && w.ActiveAmmoDef.AmmoDef.Const.MustCharge && w.State.Action != ShootOff) {
                            w.State.WeaponMode(comp, ShootOff);
                            w.Reloading = false;
                            w.State.CurrentAmmo = 0;
                            w.FinishBurst = false;

                            if (w.IsShooting)
                                w.StopShooting();
                        }

                        ///
                        ///Check Reload
                        ///                        

                        if (w.ActiveAmmoDef.AmmoDef.Const.Reloadable && !w.Reloading && w.State.CurrentAmmo <= 0)
                            w.Reload();

                        ///
                        /// Update Weapon Hud Info
                        /// 

                        if ((w.Reloading && Tick - w.LastLoadedTick > 30 || w.State.Heat > 0) && HandlesInput && !Session.Config.MinimalHud && ActiveControlBlock != null && ai.SubGrids.Contains(ActiveControlBlock.CubeGrid)) {
                            HudUi.TexturesToAdd++;
                            HudUi.WeaponsToDisplay.Add(w);
                        }

                        if (w.System.Armor != ArmorState.IsWeapon)
                            continue;

                        ///
                        /// Check target for expire states
                        /// 
                        bool targetLock = false;
                        if (w.Target.HasTarget && !(IsClient && w.Target.CurrentState == States.Invalid)) {

                            if (w.PosChangedTick != Tick) w.UpdatePivotPos();
                            if (!IsClient && w.Target.Entity == null && w.Target.Projectile == null && (!trackReticle || PlayerDummyTargets[comp.Data.Repo.State.PlayerId].ClearTarget))
                                w.Target.Reset(Tick, States.Expired, !trackReticle);
                            else if (!IsClient && w.Target.Entity != null && (comp.UserControlled || w.Target.Entity.MarkedForClose))
                                w.Target.Reset(Tick, States.Expired);
                            else if (!IsClient && w.Target.Projectile != null && (!ai.LiveProjectile.Contains(w.Target.Projectile) || w.Target.IsProjectile && w.Target.Projectile.State != Projectile.ProjectileState.Alive))
                                w.Target.Reset(Tick, States.Expired);
                            else if (w.AiEnabled) {

                                if (!Weapon.TrackingTarget(w, w.Target, out targetLock) && !IsClient)
                                    w.Target.Reset(Tick, States.Expired, !trackReticle && (w.Target.CurrentState != States.RayCheckFailed && !w.Target.HasTarget));
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
                        else if (w.Target.HasTarget && MyEntities.EntityExists(comp.Data.Repo.Targets[w.WeaponId].EntityId)) {
                            w.Target.HasTarget = false;
                            Log.Line($"wtf is this");
                        }

                        w.ProjectilesNear = enemyProjectiles && w.TrackProjectiles && !w.Target.HasTarget && (w.Target.TargetChanged || SCount == w.ShortLoadId );

                        if (comp.Data.Repo.State.Control == ControlMode.Camera && UiInput.MouseButtonPressed)
                            w.Target.TargetPos = Vector3D.Zero;

                        ///
                        /// Queue for target acquire or set to tracking weapon.
                        /// 
                        w.SeekTarget = (!IsClient && !w.Target.HasTarget && w.TrackTarget && (comp.TargetNonThreats && ai.TargetingInfo.OtherInRange || ai.TargetingInfo.ThreatInRange) && (!comp.UserControlled || w.State.Action == ShootClick)) || trackReticle && !w.Target.IsFakeTarget;
                        if (!IsClient && (w.SeekTarget || w.TrackTarget && ai.TargetResetTick == Tick && !comp.UserControlled) && !w.AcquiringTarget && (comp.Data.Repo.State.Control == ControlMode.None || comp.Data.Repo.State.Control== ControlMode.Ui))
                        {
                            w.AcquiringTarget = true;
                            AcquireTargets.Add(w);
                        }

                        if (w.Target.TargetChanged) // Target changed
                            w.TargetChanged();

                        ///
                        /// Check weapon's turret to see if its time to go home
                        ///

                        if (w.TurretMode && !w.IsHome && !w.ReturingHome  && !w.Target.HasTarget && !comp.UserControlled)
                            w.TurretHomePosition(true);

                        ///
                        /// Determine if its time to shoot
                        ///
                        ///
                        w.AiShooting = targetLock && !comp.UserControlled;
                        var reloading = w.ActiveAmmoDef.AmmoDef.Const.Reloadable && (w.Reloading || w.State.CurrentAmmo <= 0);
                        var canShoot = !w.State.Overheated && !reloading && !w.System.DesignatorWeapon && (!w.LastEventCanDelay || w.AnimationDelayTick <= Tick);
                        var fakeTarget = comp.Data.Repo.Set.Overrides.TargetPainter && trackReticle && w.Target.IsFakeTarget && w.Target.IsAligned;
                        var validShootStates = fakeTarget || w.State.Action == ShootOn || w.State.Action == ShootOnce || w.AiShooting && w.State.Action == ShootOff;
                        var manualShot = (compManualMode || w.State.Action == ShootClick) && canManualShoot && (comp.InputState.MouseButtonLeft && j % 2 == 0 || comp.InputState.MouseButtonRight && j == 1);
                        var delayedFire = w.System.DelayCeaseFire && !w.Target.IsAligned && Tick - w.CeaseFireDelayTick <= w.System.CeaseFireDelay;
                        var shoot = (validShootStates || manualShot || w.FinishBurst || delayedFire);
                        w.LockOnFireState = !shoot && w.System.LockOnFocus && ai.Data.Repo.Focus.HasFocus && ai.Data.Repo.Focus.FocusInRange(w);

                        if (canShoot && (shoot || w.LockOnFireState)) {

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
                                w.StopShooting(false, false);
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
                if (comp == null || w.Comp.Ai == null || gridAi.MyGrid.MarkedForClose || gridAi.Concealed || !gridAi.HasPower || comp.MyCube.MarkedForClose || !comp.IsWorking || !comp.Data.Repo.Set.Overrides.Activate)
                {
                    if (w.DrawingPower)
                        w.StopPowerDraw();

                    if (w.Comp?.Ai != null) 
                        w.Comp.Ai.OverPowered = w.Comp.Ai.RequestedWeaponsDraw > 0 && w.Comp.Ai.RequestedWeaponsDraw > w.Comp.Ai.GridMaxPower;
                    w.Reloading = false;

                    w.Reloading = false;

                    UniqueListRemove(w, ChargingWeaponsIndexer, ChargingWeapons);
                    continue;
                }

                if (comp.Platform.State != MyWeaponPlatform.PlatformState.Ready)
                    continue;


                if (Tick60 && w.DrawingPower)
                {
                    if ((w.State.CurrentCharge + w.UseablePower) < w.MaxCharge)
                    {
                        w.State.CurrentCharge += w.UseablePower;
                        comp.CurrentCharge += w.UseablePower;

                    }
                    else
                    {
                        comp.CurrentCharge -= (w.State.CurrentCharge - w.MaxCharge);
                        w.State.CurrentCharge = w.MaxCharge;
                    }
                }

                if (w.ChargeUntilTick <= Tick || !w.Reloading)
                {
                    if (w.Reloading)
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

                        w.ChargeDelayTicks = 0;
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

                    w.ChargeDelayTicks = (uint)(((w.ActiveAmmoDef.AmmoDef.Const.ChargSize - w.State.CurrentCharge) / w.UseablePower) * MyEngineConstants.UPDATE_STEPS_PER_SECOND);
                    w.ChargeUntilTick = w.ChargeDelayTicks + Tick;
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
                if (w.Comp.IsAsleep || w.Comp.Ai == null || comp.Ai.MyGrid.MarkedForClose || !comp.Ai.HasPower || comp.Ai.Concealed || comp.MyCube.MarkedForClose || !comp.Ai.DbReady || !comp.IsWorking || !comp.Data.Repo.Set.Overrides.Activate) {
                    
                    w.AcquiringTarget = false;
                    AcquireTargets.RemoveAtFast(i);
                    continue;
                }

                if (!w.Acquire.Enabled)
                    AcqManager.AddAwake(w.Acquire);

                var acquire = (w.Acquire.Asleep && AsleepCount == w.Acquire.SlotId || !w.Acquire.Asleep && AwakeCount == w.Acquire.SlotId);
                var seekProjectile = w.ProjectilesNear || w.TrackProjectiles && w.Comp.Ai.CheckProjectiles;
                var checkTime = w.Target.TargetChanged || acquire || seekProjectile;

                if (checkTime || w.Comp.Ai.TargetResetTick == Tick && w.Target.HasTarget) {

                    if (seekProjectile || comp.Data.Repo.State.TrackingReticle || (comp.TargetNonThreats && w.Comp.Ai.TargetingInfo.OtherInRange || w.Comp.Ai.TargetingInfo.ThreatInRange) && w.Comp.Ai.TargetingInfo.ValidTargetExists(w)) {
                        
                        AcquireChecks++;
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
                            w.Target.SyncTarget(w);
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

                if (MpActive && IsServer && Tick - w.LastSyncTick > ResyncMinDelayTicks)
                {
                   // w.SendTarget();
                }
            }
            ShootingWeapons.Clear();
        }
    }
}