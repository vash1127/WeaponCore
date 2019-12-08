using Sandbox.ModAPI;
using VRageMath;
using WeaponCore.Platform;
using WeaponCore.Projectiles;
using WeaponCore.Support;
using static WeaponCore.Support.WeaponComponent.Start;
using static WeaponCore.Platform.Weapon.TerminalActionState;

namespace WeaponCore
{
    public partial class Session
    {
        private void AiLoop()
        {
            if (!GameLoaded) return;
            foreach (var aiPair in GridTargetingAIs)
            {
                var gridAi = aiPair.Value;
                if (!gridAi.DeadProjectiles.IsEmpty)
                {
                    Projectile p;
                    while (gridAi.DeadProjectiles.TryDequeue(out p)) gridAi.LiveProjectile.Remove(p);
                    gridAi.LiveProjectileTick = Tick;
                }
                if (!gridAi.HasGunner && !gridAi.DbReady && !gridAi.ReturnHome && gridAi.ManualComps == 0 && !gridAi.Reloading && !gridAi.CheckReload || !gridAi.MyGrid.InScene || gridAi.MyGrid.MarkedForClose) continue;

                gridAi.ReturnHome = false;
                foreach (var basePair in gridAi.WeaponBase)
                {
                    var comp = basePair.Value;
                    if (comp.Platform.State != MyWeaponPlatform.PlatformState.Ready)
                        continue;

                    if (!comp.State.Value.Online && !comp.ReturnHome || comp.Status != Started)
                    {
                        if (comp.Status != Started) comp.HealthCheck();
                        continue;
                    }

                    comp.ReturnHome = false;
                    for (int j = 0; j < comp.Platform.Weapons.Length; j++)
                    {
                        var w = comp.Platform.Weapons[j];
                        var lastGunner = comp.Gunner;
                        var gunner = comp.Gunner = comp.MyCube == ControlledEntity;

                        w.TargetWasExpired = w.Target.Expired;

                        if (!comp.Set.Value.Weapons[w.WeaponId].Enable && !w.ReturnHome) continue;
                        if (w.Target.Entity == null && w.Target.Projectile == null)
                            w.Target.Expired = true;
                        else if (w.Target.Entity != null && w.Target.Entity.MarkedForClose)
                            w.Target.Reset();
                        else if (w.Target.Projectile != null && !gridAi.LiveProjectile.Contains(w.Target.Projectile))
                            w.Target.Reset();

                        else if (w.TrackingAi && comp.Set.Value.Weapons[w.WeaponId].Enable)
                        {
                            if (!Weapon.TrackingTarget(w, w.Target, !gunner))
                                w.Target.Expired = true;
                        }
                        else
                        {
                            if (w.IsTurret)
                            {
                                if (!w.TrackTarget)
                                {
                                    if ((comp.TrackingWeapon.Target.Projectile != w.Target.Projectile || comp.TrackingWeapon.Target.Entity != w.Target.Entity))
                                        w.Target.Reset();
                                }
                                else if (!w.Target.Expired && !Weapon.TargetAligned(w, w.Target))
                                    w.Target.Reset();
                            }
                            else if (w.TrackTarget && !Weapon.TargetAligned(w, w.Target))
                                w.Target.Expired = true;
                        }

                        if (gunner && UiInput.MouseButtonPressed)
                            w.TargetPos = Vector3D.Zero;

                        if (w.DelayCeaseFire)
                        {
                            if (gunner || !w.AiReady || w.DelayFireCount++ > w.System.TimeToCeaseFire)
                            {
                                w.DelayFireCount = 0;
                                w.AiReady = gunner || !w.Target.Expired && ((w.TrackingAi || !w.TrackTarget) && w.TurretTargetLock) || !w.TrackingAi && w.TrackTarget && !w.Target.Expired;
                            }
                        }
                        else w.AiReady = gunner || !w.Target.Expired && ((w.TrackingAi || !w.TrackTarget) && w.TurretTargetLock) || !w.TrackingAi && w.TrackTarget && !w.Target.Expired;

                        w.SeekTarget = w.Target.Expired && w.TrackTarget;

                        if (w.TargetWasExpired != w.Target.Expired)
                        {
                            w.EventTriggerStateChanged(Weapon.EventTriggers.Tracking, !w.Target.Expired);
                            if (w.Target.Expired)
                                w.TargetReset = true;
                        }

                        var wState = comp.State.Value.Weapons[w.WeaponId];

                        if (w.TurretMode)
                        {
                            if (comp.State.Value.Online)
                            {
                                if (((w.TargetWasExpired != w.Target.Expired && w.Target.Expired) ||
                                     (gunner != lastGunner && !gunner)))
                                    FutureEvents.Schedule(ReturnHome, w, Tick + 240);

                                if (gunner != lastGunner && gunner)
                                {
                                    gridAi.ManualComps++;
                                    comp.Shooting++;
                                }
                                else if (gunner != lastGunner && !gunner)
                                {
                                    gridAi.ManualComps = gridAi.ManualComps - 1 > 0 ? gridAi.ManualComps - 1 : 0;
                                    comp.Shooting = comp.Shooting - 1 > 0 ? comp.Shooting - 1 : 0;
                                }
                            }
                            w.ReturnHome = w.ReturnHome && wState.ManualShoot == ShootOff && !comp.Gunner && w.Target.Expired;
                            if (w.ReturnHome)
                                comp.ReturnHome = gridAi.ReturnHome = true;
                        }

                        if (gridAi.CheckReload && w.System.AmmoDefId == gridAi.NewAmmoType) ComputeStorage(w);

                        gridAi.Reloading = !w.System.EnergyAmmo && comp.State.Value.Weapons[w.WeaponId].CurrentAmmo == 0 && (comp.State.Value.Weapons[w.WeaponId].CurrentMags > 0 || IsCreative);

                        if (comp.Debug) WeaponDebug(w);

                        if (w.AiReady || w.SeekTarget || gunner || wState.ManualShoot != ShootOff || gridAi.Reloading || w.ReturnHome) gridAi.Ready = true;
                    }
                }
                gridAi.CheckReload = false;
            }
        }

        private void UpdateWeaponPlatforms()
        {
            if (!GameLoaded) return;
            foreach (var aiPair in GridTargetingAIs)
            {
                var gridAi = aiPair.Value;
                if (Tick - gridAi.TargetsUpdatedTick > 100 && DbCallBackComplete && DbTask.IsComplete && gridAi.UpdateOwner())
                    gridAi.RequestDbUpdate();

                if (!gridAi.Ready || !gridAi.MyGrid.InScene || !gridAi.GridInit || gridAi.MyGrid.MarkedForClose) continue;

                if (gridAi.HasPower || gridAi.HadPower || gridAi.UpdatePowerSources || Tick180) gridAi.UpdateGridPower();
                if (!gridAi.HasPower) continue;

                foreach (var basePair in gridAi.WeaponBase)
                {
                    var comp = basePair.Value;
                    if (gridAi.RecalcPowerPercent) comp.CompPowerPerc = comp.MaxRequiredPower / gridAi.TotalSinkPower;

                    if (comp.Platform.State != MyWeaponPlatform.PlatformState.Ready || (!comp.State.Value.Online && !comp.ReturnHome) || !gridAi.Ready || comp.MyCube.MarkedForClose) continue;

                    if ((gridAi.RecalcLowPowerTick != 0 && gridAi.RecalcLowPowerTick <= Tick) || gridAi.AvailablePowerIncrease)
                        comp.UpdateCompPower();
                    for (int j = 0; j < comp.Platform.Weapons.Length; j++)
                    {
                        var w = comp.Platform.Weapons[j];

                        if (!comp.Set.Value.Weapons[w.WeaponId].Enable || comp.Overheated || !gridAi.Ready)
                        {
                            if (w.ReturnHome)
                                w.TurretHomePosition();

                            continue;
                        }

                        if ((w.System.EnergyAmmo || w.System.IsHybrid) && comp.DelayTicks > 0)
                        {
                            if (comp.ShootTick <= Tick)
                            {
                                comp.Charging = false;
                                comp.ShootTick = Tick + comp.DelayTicks;
                                if (w.IsShooting)
                                {
                                    if (w.FiringEmitter != null) w.StartFiringSound();
                                    if (w.PlayTurretAv && w.RotateEmitter != null && !w.RotateEmitter.IsPlaying)
                                        w.StartRotateSound();
                                }
                            }
                            else
                            {
                                if (w.IsShooting)
                                    w.StopShooting();

                                comp.Charging = true;
                            }
                            comp.TerminalRefresh();
                        }
                        else comp.Charging = false;
                        
                        if (comp.Charging) continue;

                        if (!w.System.EnergyAmmo && comp.State.Value.Weapons[w.WeaponId].CurrentAmmo == 0)
                        {
                            if (w.AmmoMagTimer == int.MaxValue)
                            {
                                if (!w.Reloading)
                                {
                                    w.EventTriggerStateChanged(state: Weapon.EventTriggers.Firing, active: true, pause: true);
                                    if (w.IsShooting)
                                        w.StopShooting();
                                }
                                if (comp.State.Value.Weapons[w.WeaponId].CurrentMags != 0 || IsCreative)
                                    w.StartReload();
                                else if(!w.Reloading)
                                    w.EventTriggerStateChanged(Weapon.EventTriggers.OutOfAmmo, true);
                                continue;
                            }
                            if (!w.AmmoMagLoaded) continue;
                            w.EventTriggerStateChanged(Weapon.EventTriggers.Reloading, false);

                            if (w.IsShooting)
                            {
                                if (w.FiringEmitter != null) w.StartFiringSound();
                                if (w.PlayTurretAv && w.RotateEmitter != null && !w.RotateEmitter.IsPlaying) w.StartRotateSound();
                                comp.CurrentDps += w.Dps;
                            }
                            w.Reloading = false;
                        }

                        if (w.SeekTarget || gridAi.TargetResetTick == Tick)
                        {
                            if (!w.SleepTargets || Tick - w.TargetCheckTick > 119 || gridAi.TargetResetTick == Tick || w.TargetReset)
                            {
                                w.TargetReset = false;
                                if (comp.TrackingWeapon != null && comp.TrackingWeapon.System.DesignatorWeapon && comp.TrackingWeapon != w && !comp.TrackingWeapon.Target.Expired)
                                    GridAi.AcquireTarget(w, false, comp.TrackingWeapon.Target.Entity.GetTopMostParent());
                                else GridAi.AcquireTarget(w, gridAi.TargetResetTick == Tick);
                            }
                        }
                        else if (w.IsTurret && !w.TrackTarget && w.Target.Expired)
                            w.Target = w.Comp.TrackingWeapon.Target;

                        if (!w.Target.Expired)
                            w.ReturnHome = false;
                        else if (w.ReturnHome)
                            w.TurretHomePosition();

                        if (w.TrackingAi && w.AvCapable && comp.RotationEmitter != null && Vector3D.DistanceSquared(CameraPos, w.MyPivotPos) < 10000)
                        {
                            if (w.IsTracking && comp.AiMoving && !comp.RotationEmitter.IsPlaying)
                                comp.RotationEmitter.PlaySound(comp.RotationSound, true, false, false, false, false, false);
                            else if ((!w.IsTracking || !comp.AiMoving && Tick - w.LastTrackedTick > 30) && comp.RotationEmitter.IsPlaying)
                                comp.StopRotSound(false);
                        }

                        var wState = comp.State.Value.Weapons[w.WeaponId];

                        if (!w.System.DesignatorWeapon && (wState.ManualShoot == ShootOn || wState.ManualShoot == ShootOnce || (wState.ManualShoot == ShootOff && w.AiReady && !comp.Gunner) || ((wState.ManualShoot == ShootClick ||comp.Gunner) && (j == 0 && UiInput.MouseButtonLeft || j == 1 && UiInput.MouseButtonRight))))
                            w.Shoot();
                        else if (w.IsShooting)
                            w.StopShooting();

                        if (w.AvCapable && w.BarrelAvUpdater.Reader.Count > 0) w.ShootGraphics();
                    }
                }
                gridAi.Ready = false;
                gridAi.AvailablePowerIncrease = false;
                gridAi.RecalcPowerPercent = false;

                if (gridAi.RecalcDone)
                {
                    gridAi.RecalcLowPowerTick = 0;
                    gridAi.ResetPower = true;
                    gridAi.RecalcDone = false;
                }
            }

            if (DbCallBackComplete && DbsToUpdate.Count > 0 && DbTask.IsComplete) UpdateDbsInQueue();
        }
    }
}