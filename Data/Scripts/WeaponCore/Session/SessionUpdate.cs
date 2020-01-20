using SpaceEngineers.Game.ModAPI;
using VRageMath;
using WeaponCore.Platform;
using WeaponCore.Projectiles;
using WeaponCore.Support;
using static WeaponCore.Support.WeaponComponent.Start;
using static WeaponCore.Platform.Weapon.TerminalActionState;
using static WeaponCore.Support.WeaponComponent.Control;
using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage.Game;
using static WeaponCore.Support.Target;

namespace WeaponCore
{
    public partial class Session
    {
        private void AiLoop() //Fully Inlined due to keen's mod profiler
        {
            foreach (var aiPair in GridTargetingAIs)
            {
                ///
                ///
                /// GridAi update section
                ///
                ///
                
                var gridAi = aiPair.Value;
                using (gridAi.MyGrid.Pin())
                {
                    if (gridAi.MyGrid.MarkedForClose || !gridAi.GridInit)
                        continue;

                    var dbIsStale = Tick - gridAi.TargetsUpdatedTick > 100;
                    var readyToUpdate = dbIsStale && DbCallBackComplete && DbTask.IsComplete;

                    if (readyToUpdate && gridAi.UpdateOwner())
                        gridAi.RequestDbUpdate();

                    if (gridAi.DeadProjectiles.Count > 0)
                    {
                        for (int i = 0; i < gridAi.DeadProjectiles.Count; i++) gridAi.LiveProjectile.Remove(gridAi.DeadProjectiles[i]);
                        gridAi.DeadProjectiles.Clear();
                        gridAi.LiveProjectileTick = Tick;
                    }

                    gridAi.CheckProjectiles = Tick - gridAi.NewProjectileTick <= 1;

                    var weaponsInStandby = gridAi.ManualComps == 0 && !gridAi.CheckReload && gridAi.Gunners.Count == 0;
                    if (!gridAi.DbReady && weaponsInStandby)
                    {
                        //if (Tick1800) Log.Line($"gridAi Disabled: {gridAi.MyGrid.DebugName} - weaponStandby:{weaponsInStandby} - dbReady:{gridAi.DbReady}");
                        continue;
                    }

                    if (gridAi.HasPower || gridAi.HadPower || gridAi.UpdatePowerSources || Tick180)
                        gridAi.UpdateGridPower();

                    if (!gridAi.HasPower) continue;
                    ///
                    ///
                    /// Comp update section
                    ///
                    ///

                    for (int i = 0; i < gridAi.Weapons.Count; i++)
                    {
                        var comp = gridAi.Weapons[i];
                        using (comp.MyCube.Pin())
                        {
                            if (comp.MyCube.MarkedForClose || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready)
                                continue;

                            if (!comp.State.Value.Online || comp.Status != Started)
                            {

                                if (comp.Status != Started)
                                    comp.HealthCheck();

                                continue;
                            }

                            if (Tick60) comp.UpdateGunnerState();
                            var directControl = comp.Gunner == Direct;
                            var gunControl = comp.Gunner != None;

                            ///
                            ///
                            /// Weapon update section
                            ///
                            ///

                            for (int j = 0; j < comp.Platform.Weapons.Length; j++)
                            {

                                var w = comp.Platform.Weapons[j];

                                if (!w.Set.Enable)
                                    continue;

                                ///
                                /// Check target for expire states
                                /// 

                                var targetAcquired = w.TargetState != Targets.Acquired && w.Target.State == Targets.Acquired;
                                var targetLost = w.TargetState == Targets.Acquired && w.Target.State != Targets.Acquired;

                                w.TargetState = w.Target.State;

                                if (w.Target.State == Targets.Acquired)
                                {

                                    if (w.Target.Entity == null && w.Target.Projectile == null && (!w.Target.IsFakeTarget || w.Target.IsFakeTarget && gridAi.DummyTarget.ClearTarget))
                                    {

                                        w.Target.Reset(true, !w.Target.IsFakeTarget);
                                    }
                                    else if (w.Target.Entity != null && w.Target.Entity.MarkedForClose)
                                    {

                                        w.Target.Reset();
                                    }
                                    else if (w.Target.Projectile != null && (!gridAi.LiveProjectile.Contains(w.Target.Projectile) || w.Target.IsProjectile && w.Target.Projectile.State != Projectile.ProjectileState.Alive))
                                    {

                                        w.Target.Reset();
                                    }
                                    else if (w.TrackingAi)
                                    {

                                        if (!Weapon.TrackingTarget(w, w.Target))
                                        {

                                            w.Target.Reset();
                                        }
                                    }
                                    else
                                    {

                                        Vector3D targetPos;
                                        if (w.IsTurret)
                                        {

                                            if (!w.TrackTarget)
                                            {

                                                if ((comp.TrackingWeapon.Target.Projectile != w.Target.Projectile || w.Target.IsProjectile && w.Target.Projectile.State != Projectile.ProjectileState.Alive || comp.TrackingWeapon.Target.Entity != w.Target.Entity || comp.TrackingWeapon.Target.IsFakeTarget != w.Target.IsFakeTarget))
                                                {

                                                    w.Target.Reset();
                                                }
                                            }
                                            else if (!Weapon.TargetAligned(w, w.Target, out targetPos))
                                            {

                                                w.Target.Reset();
                                            }
                                        }
                                        else if (w.TrackTarget && !Weapon.TargetAligned(w, w.Target, out targetPos))
                                        {

                                            w.Target.Reset();
                                        }
                                    }
                                }
                                var targetChanged = targetAcquired || targetLost;
                                if (directControl && UiInput.MouseButtonPressed)
                                    w.Target.TargetPos = Vector3D.Zero;
                                ///
                                /// Set weapon Ai state
                                /// 

                                if (w.DelayCeaseFire)
                                {

                                    if (directControl || !w.AiReady || w.DelayFireCount++ > w.System.TimeToCeaseFire)
                                    {

                                        w.DelayFireCount = 0;
                                        w.AiReady = directControl || (w.Target.State == Targets.Acquired && (w.TrackingAi || !w.TrackTarget) && w.Target.TargetLock) || (!w.TrackingAi && w.TrackTarget && w.Target.State == Targets.Acquired);
                                    }
                                }
                                else
                                {

                                    w.AiReady = gunControl || (w.Target.State == Targets.Acquired && (w.TrackingAi || !w.TrackTarget) && w.Target.TargetLock) || (!w.TrackingAi && w.TrackTarget && w.Target.State == Targets.Acquired);
                                }

                                if (targetChanged)
                                {

                                    w.EventTriggerStateChanged(Weapon.EventTriggers.Tracking, w.Target.State == Targets.Acquired);
                                    w.EventTriggerStateChanged(Weapon.EventTriggers.StopTracking, w.Target.State != Targets.Acquired);
                                }

                                ///
                                /// Queue for target acquire or set to tracking weapon.
                                /// 

                                w.SeekTarget = w.TrackTarget && w.Target.State == Targets.Expired && (gridAi.TargetingInfo.TargetInRange || comp.Gunner == Manual);
                                if ((w.SeekTarget || w.TrackTarget && gridAi.TargetResetTick == Tick) && w.Target.State != Targets.StillSeeking && comp.Gunner != Direct)
                                {
                                    w.Target.State = Targets.StillSeeking;
                                    AcquireTargets.Add(w);
                                }
                                else if (w.IsTurret && !w.TrackTarget && w.Target.State != Targets.Acquired && gridAi.TargetingInfo.TargetInRange)
                                    w.Target = w.Comp.TrackingWeapon.Target;

                                ///
                                /// Check weapon's turret to see if its time to go home
                                /// 

                                if (w.TurretMode && comp.State.Value.Online)
                                {
                                    if ((targetChanged && w.Target.State != Targets.Acquired) || (comp.Gunner != comp.LastGunner && !gunControl))
                                        FutureEvents.Schedule(w.TurretHomePosition, null, 240);

                                    if (comp.Gunner != comp.LastGunner && gunControl)
                                    {

                                        gridAi.ManualComps++;
                                    }
                                    else if (comp.Gunner != comp.LastGunner && !gunControl)
                                    {

                                        gridAi.ManualComps = gridAi.ManualComps - 1 > 0 ? gridAi.ManualComps - 1 : 0;
                                    }
                                }

                                if (gridAi.CheckReload && w.System.AmmoDefId == gridAi.NewAmmoType && !w.System.EnergyAmmo)
                                    ComputeStorage(w);

                                if (comp.Debug)
                                    WeaponDebug(w);

                                ///
                                /// Determine if its time to shoot
                                ///

                                var reloading = (!w.System.EnergyAmmo || w.System.MustCharge) && (w.Reloading || w.OutOfAmmo);
                                if (w.AvCapable && (!w.PlayTurretAv || Tick60))
                                    w.PlayTurretAv = Vector3D.DistanceSquared(CameraPos, w.MyPivotPos) < w.System.HardPointAvMaxDistSqr;

                                if (!comp.Overheated && !reloading && !w.System.DesignatorWeapon && (w.State.ManualShoot == ShootOn || w.State.ManualShoot == ShootOnce || (w.State.ManualShoot == ShootOff && w.AiReady && !directControl) || ((w.State.ManualShoot == ShootClick || directControl) && !gridAi.SupressMouseShoot && (j == 0 && UiInput.MouseButtonLeft || j == 1 && UiInput.MouseButtonRight))))
                                {
                                    if ((gridAi.AvailablePowerChanged || gridAi.RequestedPowerChanged || (w.RecalcPower && Tick60)) && !w.System.MustCharge)
                                    {
                                        if ((!gridAi.RequestIncrease || gridAi.PowerIncrease) && !Tick60)
                                        {
                                            w.RecalcPower = true;
                                        }
                                        else
                                        {
                                            w.RecalcPower = false;
                                            w.ChargeDelayTicks = 0;
                                        }
                                    }

                                    var targetRequested = w.SeekTarget && targetChanged;
                                    if (!targetRequested && (w.Target.IsAligned || w.State.ManualShoot != ShootOff) && (w.ChargeDelayTicks == 0 || w.ChargeUntilTick <= Tick))
                                    {
                                        if (!w.RequestedPower && !w.System.MustCharge)
                                        {
                                            gridAi.RequestedWeaponsDraw += w.RequiredPower;
                                            w.RequestedPower = true;
                                        }

                                        ShootingWeapons.Add(w);
                                    }
                                    else if (w.ChargeUntilTick > Tick && !w.System.MustCharge)
                                    {
                                        w.Charging = true;
                                        w.StopShooting(false, false);
                                    }
                                }
                                else if (w.IsShooting)
                                    w.StopShooting();

                                if (w.PlayTurretAv && w.BarrelAvUpdater.Reader.Count > 0)
                                    w.ShootGraphics();
                                else if (w.BarrelAvUpdater.Reader.Count > 0)
                                    w.ShootGraphics(true);
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
                    if (w.Comp.MyCube.MarkedForClose || w.Comp.Ai == null || w.Comp.Ai.MyGrid.MarkedForClose)
                    {
                        ChargingWeapons.RemoveAtFast(i);
                        continue;
                    }

                    var comp = w.Comp;
                    if (comp.Platform.State != MyWeaponPlatform.PlatformState.Ready)
                        continue;

                    var gridAi = w.Comp.Ai;

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

                        if (!w.Comp.Session.DedicatedServer)
                            w.Comp.TerminalRefresh();
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

                    if (gridAi.LastPowerUpdateTick != Tick && (gridAi.HasPower || gridAi.HadPower || gridAi.UpdatePowerSources || Tick180))
                        gridAi.UpdateGridPower();

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
            for (int i = AcquireTargets.Count - 1; i >= 0; i--)
            {
                var w = AcquireTargets[i];

                using (w.Comp.MyCube.Pin())
                using (w.Comp.Ai.MyGrid.Pin())
                {
                    if (w.Comp.MyCube.MarkedForClose || w.Comp.Ai == null || w.Comp.Ai.MyGrid.MarkedForClose || w.Comp.Gunner == Direct)
                    {
                        AcquireTargets.RemoveAtFast(i);
                        continue;
                    }

                    var gridAi = w.Comp.Ai;
                    var sinceCheck = Tick - w.Target.CheckTick;
                    var checkTime = sinceCheck > 239 || sinceCheck > 60 && Count == w.LoadId || w.TrackProjectiles && gridAi.CheckProjectiles;

                    if (checkTime || gridAi.TargetResetTick == Tick && w.Target.State == Targets.Acquired)
                    {
                        var comp = w.Comp;
                        var weaponEnabled = !comp.State.Value.Online || w.Set.Enable;

                        if (!weaponEnabled)
                            continue;

                        if (w.TrackProjectiles && gridAi.CheckProjectiles || w.Comp.Gunner == Manual || gridAi.TargetingInfo.TargetInRange && gridAi.TargetingInfo.ValidTargetExists(w))
                        {
                            if (comp.TrackingWeapon != null && comp.TrackingWeapon.System.DesignatorWeapon && comp.TrackingWeapon != w && comp.TrackingWeapon.Target.State == Targets.Acquired)
                                GridAi.AcquireTarget(w, false, comp.TrackingWeapon.Target.Entity.GetTopMostParent());
                            else
                                GridAi.AcquireTarget(w, gridAi.TargetResetTick == Tick);
                        }

                        if (w.Target.State == Targets.Acquired || !gridAi.TargetingInfo.TargetInRange || !gridAi.DbReady)
                        {
                            if (w.Target.State == Targets.StillSeeking) w.Target.State = Targets.Expired;
                            AcquireTargets.RemoveAtFast(i);
                        }
                    }
                }
            }
        }

        private void ShootWeapons() 
        {
            for (int i = ShootingWeapons.Count - 1; i >= 0; i--)
            {
                var w = ShootingWeapons[i];

                using (w.Comp.MyCube.Pin())
                using (w.Comp.Ai.MyGrid.Pin())
                {
                    if (w.Comp.MyCube.MarkedForClose || w.Comp.Ai == null || w.Comp.Ai.MyGrid.MarkedForClose || w.Comp.Platform.State != MyWeaponPlatform.PlatformState.Ready || !w.Target.IsAligned && w.State.ManualShoot == ShootOff)
                    {
                        ShootingWeapons.RemoveAtFast(i);
                        continue;
                    }

                    //TODO add logic for power priority
                    if (w.Comp.Ai.OverPowered && (w.System.EnergyAmmo || w.System.IsHybrid) && !w.System.MustCharge)
                    {

                        if (w.ChargeDelayTicks == 0)
                        {
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
                        else if (w.ChargeUntilTick <= Tick)
                        {
                            w.Charging = false;
                            w.ChargeUntilTick = Tick + w.ChargeDelayTicks;
                        }
                    }
                    //w.System.MustCharge must be first to prevent setting w.ResetPower
                    else if (!w.System.MustCharge && (w.Charging || w.ChargeDelayTicks > 0 || w.ResetPower))
                    {
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

                    if (!w.System.MustCharge && w.State.ManualShoot == ShootOnce)
                    {
                        w.State.ManualShoot = ShootOff;
                        w.StopShooting();
                        w.Comp.Ai.ManualComps = w.Comp.Ai.ManualComps - 1 > 0 ? w.Comp.Ai.ManualComps - 1 : 0;
                    }
                }
            }
            ShootingWeapons.Clear();
        }
    }
}