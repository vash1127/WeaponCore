using VRageMath;
using WeaponCore.Platform;
using WeaponCore.Projectiles;
using WeaponCore.Support;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using VRage.Game;
using static WeaponCore.Support.Target;
using static WeaponCore.Support.WeaponComponent.Start;
using static WeaponCore.Platform.Weapon.TerminalActionState;
using static WeaponCore.Support.WeaponComponent.TerminalControl;

namespace WeaponCore
{
    public partial class Session
    {
        private void AiLoop() { //Fully Inlined due to keen's mod profiler

            foreach (var aiPair in GridTargetingAIs) {

                ///
                /// GridAi update section
                ///
                var gridAi = aiPair.Value;
                using (gridAi.MyGrid.Pin()) {

                    if (!gridAi.GridInit || gridAi.MyGrid.MarkedForClose)
                        continue;

                    var dbIsStale = Tick - gridAi.TargetsUpdatedTick > 100;
                    var readyToUpdate = dbIsStale && DbCallBackComplete && DbTask.IsComplete;

                    if (readyToUpdate && gridAi.UpdateOwner())
                        gridAi.RequestDbUpdate();

                    if (gridAi.DeadProjectiles.Count > 0) {
                        for (int i = 0; i < gridAi.DeadProjectiles.Count; i++) gridAi.LiveProjectile.Remove(gridAi.DeadProjectiles[i]);
                        gridAi.DeadProjectiles.Clear();
                        gridAi.LiveProjectileTick = Tick;
                    }
                    gridAi.CheckProjectiles = Tick - gridAi.NewProjectileTick <= 1;

                    if (!gridAi.HasPower && gridAi.HadPower || gridAi.UpdatePowerSources || !gridAi.WasPowered && gridAi.MyGrid.IsPowered || Tick10 )
                        gridAi.UpdateGridPower();

                    if (!gridAi.HasPower)
                        continue;

                    var uiTargeting = TargetUi.DrawReticle && !InMenu && gridAi.ControllingPlayers.ContainsKey(Session.Player.IdentityId);

                    ///
                    /// Comp update section
                    ///
                    for (int i = 0; i < gridAi.Weapons.Count; i++) {

                        var comp = gridAi.Weapons[i];
                        using (comp.MyCube.Pin()) {

                            if (comp.MyCube.MarkedForClose || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready)
                                continue;

                            if (!comp.State.Value.Online || !comp.Set.Value.Overrides.Activate || comp.Status != Started) {

                                if (comp.Status != Started) comp.HealthCheck();
                                continue;
                            }
                            if (InMenu && Tick20 && gridAi.LastTerminal == comp.MyCube)
                                comp.TerminalRefresh();

                            var overRides = comp.Set.Value.Overrides;
                            comp.WasControlled = comp.UserControlled;
                            comp.ManualControl = overRides.ManaulControl;
                            comp.TargetPainter = !comp.ManualControl && overRides.TargetPainter;

                            comp.TrackReticle = (comp.TargetPainter || comp.ManualControl) && uiTargeting;
                            
                            var id = comp.State.Value.PlayerIdInTerminal;
                            comp.TerminalControlled = id == -1 ? None : 
                                id == -2 ? ApiControl : 
                                    id == -3 ? CameraControl : ToolBarControl;

                            comp.UserControlled = comp.TrackReticle || comp.TerminalControlled == CameraControl;

                            ///
                            /// Weapon update section
                            ///
                            for (int j = 0; j < comp.Platform.Weapons.Length; j++) {

                                var w = comp.Platform.Weapons[j];
                                if (!w.Set.Enable) {
                                    if (w.Target.State == Targets.Acquired)
                                        w.Target.Reset();
                                    continue;
                                }

                                if (w.AvCapable && (!w.PlayTurretAv || Tick60)) {
                                    var avWasEnabled = w.PlayTurretAv;
                                    w.PlayTurretAv = Vector3D.DistanceSquared(CameraPos, w.MyPivotPos) < w.System.HardPointAvMaxDistSqr;
                                    if (avWasEnabled != w.PlayTurretAv) w.StopBarrelAv = !w.PlayTurretAv;
                                }

                                ///
                                /// Check target for expire states
                                /// 
                                var targetAcquired = w.TargetState != Targets.Acquired && w.Target.State == Targets.Acquired;
                                var targetLost = w.TargetState == Targets.Acquired && w.Target.State != Targets.Acquired;
                                w.TargetState = w.Target.State;
                                if (w.Target.State == Targets.Acquired) {

                                    if (w.Target.Entity == null && w.Target.Projectile == null && (!comp.TrackReticle || gridAi.DummyTarget.ClearTarget)) {
                                        w.Target.Reset(!comp.TrackReticle);

                                    }
                                    else if (w.Target.Entity != null && (comp.UserControlled || w.Target.Entity.MarkedForClose)) {
                                        w.Target.Reset();

                                    }
                                    else if (w.Target.Projectile != null && (!gridAi.LiveProjectile.Contains(w.Target.Projectile) || w.Target.IsProjectile && w.Target.Projectile.State != Projectile.ProjectileState.Alive)) {
                                        w.Target.Reset();

                                    }
                                    else if (w.TrackingAi) {

                                        if (!Weapon.TrackingTarget(w, w.Target)) {
                                            w.Target.Reset(!comp.TrackReticle);

                                        }
                                    }
                                    else {

                                        Vector3D targetPos;
                                        if (w.IsTurret) {

                                            if (!w.TrackTarget) {

                                                if ((comp.TrackingWeapon.Target.Projectile != w.Target.Projectile || w.Target.IsProjectile && w.Target.Projectile.State != Projectile.ProjectileState.Alive || comp.TrackingWeapon.Target.Entity != w.Target.Entity || comp.TrackingWeapon.Target.IsFakeTarget != w.Target.IsFakeTarget)) {
                                                    w.Target.Reset();

                                                }
                                            }
                                            else if (!Weapon.TargetAligned(w, w.Target, out targetPos)) {
                                                w.Target.Reset();

                                            }
                                        }
                                        else if (w.TrackTarget && !Weapon.TargetAligned(w, w.Target, out targetPos)) {
                                            w.Target.Reset();

                                        }
                                    }
                                }

                                w.AiShooting = w.Target.State == Targets.Acquired && !comp.UserControlled && (w.Target.TargetLock && (w.TrackingAi || !w.TrackTarget) || !w.TrackingAi && w.TrackTarget);

                                w.TargetChanged = targetAcquired || targetLost;
                                w.ProjectilesNear = w.TrackProjectiles && w.Target.State != Targets.Acquired && (w.TargetChanged || SCount == w.ShortLoadId && gridAi.LiveProjectile.Count > 0);

                                if (comp.TerminalControlled == CameraControl && UiInput.MouseButtonPressed)
                                    w.Target.TargetPos = Vector3D.Zero;

                                if (w.DelayCeaseFire && (comp.TerminalControlled != CameraControl || !w.AiShooting || w.DelayFireCount++ > w.System.TimeToCeaseFire))
                                    w.DelayFireCount = 0;

                                if (w.TargetChanged) {
                                    w.EventTriggerStateChanged(Weapon.EventTriggers.Tracking, w.Target.State == Targets.Acquired);
                                    w.EventTriggerStateChanged(Weapon.EventTriggers.StopTracking, w.Target.State != Targets.Acquired);
                                }

                                ///
                                /// Queue for target acquire or set to tracking weapon.
                                /// 
                                w.SeekTarget = (w.Target.State == Targets.Expired && w.TrackTarget && gridAi.TargetingInfo.TargetInRange && !comp.UserControlled) || comp.TrackReticle && !w.Target.IsFakeTarget;
                                if ((w.SeekTarget || w.TrackTarget && gridAi.TargetResetTick == Tick && !comp.UserControlled) && !w.AcquiringTarget && comp.TerminalControlled == None)
                                {
                                    w.AcquiringTarget = true;
                                    AcquireTargets.Add(w);
                                }
                                else if (w.IsTurret && !w.TrackTarget && w.Target.State != Targets.Acquired && gridAi.TargetingInfo.TargetInRange)
                                    w.Target = w.Comp.TrackingWeapon.Target;

                                ///
                                /// Check weapon's turret to see if its time to go home
                                /// 
                                if (w.TurretMode && comp.State.Value.Online) {

                                    if (w.TargetChanged && w.Target.State != Targets.Acquired || comp.UserControlled != comp.WasControlled && !comp.UserControlled)
                                        FutureEvents.Schedule(w.TurretHomePosition, null, 240);
                                }

                                if (gridAi.CheckReload && w.System.AmmoDefId == gridAi.NewAmmoType && !w.System.EnergyAmmo)
                                    ComputeStorage(w);

                                ///
                                /// Determine if its time to shoot
                                ///
                                var reloading = (!w.System.EnergyAmmo || w.System.MustCharge) && (w.Reloading || w.OutOfAmmo);
                                var canShoot = !comp.Overheated && !reloading && !w.System.DesignatorWeapon;
                                var fakeTarget = comp.TargetPainter && comp.TrackReticle && w.Target.IsFakeTarget && w.Target.IsAligned;
                                var validShootStates = fakeTarget || w.State.ManualShoot == ShootOn || w.State.ManualShoot == ShootOnce || w.AiShooting && w.State.ManualShoot == ShootOff;

                                var manualShot = (comp.TerminalControlled == CameraControl || comp.ManualControl && comp.TrackReticle || w.State.ManualShoot == ShootClick) && !gridAi.SupressMouseShoot && (j % 2 == 0 && UiInput.MouseButtonLeft || j == 1 && UiInput.MouseButtonRight);

                                //Log.Line($"reloading: {reloading} canShoot: {canShoot} fakeTarget: {fakeTarget} validShootStates: {validShootStates} manualShot: {manualShot} heat: {w.State.Heat}");

                                if (canShoot && (validShootStates || manualShot)) {

                                    if ((gridAi.AvailablePowerChanged || gridAi.RequestedPowerChanged || (w.RecalcPower && Tick60)) && !w.System.MustCharge) {

                                        if ((!gridAi.RequestIncrease || gridAi.PowerIncrease) && !Tick60) {
                                            w.RecalcPower = true;
                                        }
                                        else {
                                            w.RecalcPower = false;
                                            w.ChargeDelayTicks = 0;
                                        }
                                    }

                                    if (w.ChargeDelayTicks == 0 || w.ChargeUntilTick <= Tick) {

                                        if (!w.RequestedPower && !w.System.MustCharge) {
                                            gridAi.RequestedWeaponsDraw += w.RequiredPower;
                                            w.RequestedPower = true;
                                        }

                                        ShootingWeapons.Add(w);
                                    }
                                    else if (w.ChargeUntilTick > Tick && !w.System.MustCharge) {
                                        w.Charging = true;
                                        w.StopShooting(false, false);
                                    }
                                }
                                else if (w.IsShooting)
                                    w.StopShooting();
                                else if (w.BarrelSpinning)
                                    w.SpinBarrel(true);

                                if (comp.Debug)
                                    WeaponDebug(w);
                            }
                        }
                    }

                    gridAi.OverPowered = gridAi.RequestedWeaponsDraw > 0 && gridAi.RequestedWeaponsDraw > gridAi.GridMaxPower;
                    gridAi.CheckReload = false;
                }
            }

            if (DbCallBackComplete && DbsToUpdate.Count > 0 && DbTask.IsComplete) 
                UpdateDbsInQueue();
        }

        private void UpdateChargeWeapons() //Fully Inlined due to keen's mod profiler
        {
            for (int i = ChargingWeapons.Count - 1; i >= 0; i--)
            {
                var w = ChargingWeapons[i];
                using (w.Comp.MyCube.Pin())
                using (w.Comp.Ai.MyGrid.Pin())
                {
                    var comp = w.Comp;
                    var gridAi = comp.Ai;

                    //if (gridAi.LastPowerUpdateTick != Tick)
                        //gridAi.UpdateGridPower();

                    if (comp.Ai == null || comp.Ai.MyGrid.MarkedForClose || !comp.Ai.HasPower || comp.MyCube.MarkedForClose || !w.Set.Enable || !comp.State.Value.Online || !comp.Set.Value.Overrides.Activate)
                    {
                        ChargingWeapons.RemoveAtFast(i);
                        continue;
                    }

                    if (comp.Platform.State != MyWeaponPlatform.PlatformState.Ready)
                        continue;                    

                    if (Tick60 && w.DrawingPower)
                    {
                        if ((w.Comp.CurrentCharge + w.UseablePower) < w.System.EnergyMagSize)
                        {
                            w.CurrentCharge += w.UseablePower;
                            w.Comp.CurrentCharge += w.UseablePower;

                        }
                        else
                        {
                            w.Comp.CurrentCharge += (w.System.EnergyMagSize - w.CurrentCharge);
                            w.CurrentCharge = w.System.EnergyMagSize;
                        }
                    }

                    if (w.ChargeUntilTick <= Tick || !w.Reloading)
                    {
                        if (w.Reloading)
                            Weapon.Reloaded(w);

                        if (w.DrawingPower)
                            w.StopPowerDraw();

                        w.Comp.Ai.OverPowered = w.Comp.Ai.RequestedWeaponsDraw > 0 && w.Comp.Ai.RequestedWeaponsDraw > w.Comp.Ai.GridMaxPower;
                        ChargingWeapons.RemoveAtFast(i);
                        continue;
                    }

                    if (!w.Comp.Ai.OverPowered)
                    {
                        if (!w.DrawingPower)
                        {
                            w.OldUseablePower = w.UseablePower;
                            w.UseablePower = w.RequiredPower;
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

                        w.ChargeDelayTicks = (uint)(((w.System.EnergyMagSize - w.CurrentCharge) / w.UseablePower) * MyEngineConstants.UPDATE_STEPS_PER_SECOND);
                        w.ChargeUntilTick = w.ChargeDelayTicks + Tick;

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
            for (int i = AcquireTargets.Count - 1; i >= 0; i--) {

                var w = AcquireTargets[i];
                using (w.Comp.MyCube.Pin())
                using (w.Comp.Ai.MyGrid.Pin()) {

                    var comp = w.Comp;
                    if (comp.Ai == null || comp.Ai.MyGrid.MarkedForClose || !comp.Ai.HasPower || comp.MyCube.MarkedForClose || !comp.Ai.DbReady || !w.Set.Enable || !comp.State.Value.Online || !comp.Set.Value.Overrides.Activate) {
                        
                        w.AcquiringTarget = false;
                        AcquireTargets.RemoveAtFast(i);
                        continue;
                    }

                    var gridAi = w.Comp.Ai;
                    var sinceCheck = Tick - w.Target.CheckTick;
                    var seekProjectile = w.ProjectilesNear || w.TrackProjectiles && gridAi.CheckProjectiles;

                    var checkTime = w.TargetChanged || sinceCheck > 239 || sinceCheck > 60 && Count == w.LoadId || seekProjectile;

                    if (checkTime || gridAi.TargetResetTick == Tick && w.Target.State == Targets.Acquired) {

                        if (seekProjectile || comp.TrackReticle || gridAi.TargetingInfo.TargetInRange && gridAi.TargetingInfo.ValidTargetExists(w)) {

                            if (comp.TrackingWeapon != null && comp.TrackingWeapon.System.DesignatorWeapon && comp.TrackingWeapon != w && comp.TrackingWeapon.Target.State == Targets.Acquired)
                                GridAi.AcquireTarget(w, false, comp.TrackingWeapon.Target.Entity.GetTopMostParent());
                            else
                                GridAi.AcquireTarget(w, gridAi.TargetResetTick == Tick);
                        }

                        if (w.Target.State == Targets.Acquired || !gridAi.TargetingInfo.TargetInRange) {

                            w.AcquiringTarget = false;
                            AcquireTargets.RemoveAtFast(i);
                        }
                    }
                }
            }
        }

        private void ShootWeapons()  {
            for (int i = ShootingWeapons.Count - 1; i >= 0; i--) {

                var w = ShootingWeapons[i];
                using (w.Comp.MyCube.Pin())
                using (w.Comp.Ai.MyGrid.Pin()) {

                    if (w.Comp.MyCube.MarkedForClose || w.Comp.Ai == null || w.Comp.Ai.MyGrid.MarkedForClose || w.Comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) {

                        ShootingWeapons.RemoveAtFast(i);
                        continue;
                    }
                    //TODO add logic for power priority
                    if (w.Comp.Ai.OverPowered && (w.System.EnergyAmmo || w.System.IsHybrid) && !w.System.MustCharge) {

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
                    else if (!w.System.MustCharge && (w.Charging || w.ChargeDelayTicks > 0 || w.ResetPower)) {

                        w.OldUseablePower = w.UseablePower;
                        w.UseablePower = w.RequiredPower;
                        w.DrawPower(true);
                        w.ChargeDelayTicks = 0;
                        w.Charging = false;
                        w.ResetPower = false;
                    }

                    if (w.Charging)
                        continue;

                    if (w.ShootDelayTick <= Tick) w.Shoot();

                    /*if (!w.System.MustCharge && w.State.ManualShoot == ShootOnce) {

                        w.State.ManualShoot = ShootOff;
                        w.StopShooting();
                    }*/
                }
            }
            ShootingWeapons.Clear();
        }
    }
}