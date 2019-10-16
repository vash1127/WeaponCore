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
                }
                if ((!gridAi.DbReady && !gridAi.ReturnHome && gridAi.ManualComps == 0 && !gridAi.Reloading && !ControlChanged) || !gridAi.MyGrid.InScene) continue;
                gridAi.Reloading = false;
                foreach (var basePair in gridAi.WeaponBase)
                {
                    var comp = basePair.Value;
                    var lastGunner = comp.Gunner;
                    var gunner = comp.Gunner = ControlledEntity == comp.MyCube;

                    if (!comp.MainInit || (!comp.State.Value.Online && !comp.ReturnHome) || comp.Status != Started)
                    {
                        if (comp.Status != Started) comp.HealthCheck();
                        continue;
                    }

                    comp.ReturnHome = gridAi.ReturnHome = false;

                    for (int j = 0; j < comp.Platform.Weapons.Length; j++)
                    {
                        var w = comp.Platform.Weapons[j];
                        w.TargetWasExpired = w.Target.Expired;
                        if (!comp.Set.Value.Weapons[w.WeaponId].Enable && !w.ReturnHome) continue;
                        if (w.Target.Entity == null && w.Target.Projectile == null) w.Target.Expired = true;
                        else if (w.Target.Entity != null && w.Target.Entity.MarkedForClose) w.Target.Reset();
                        else if (w.Target.Projectile != null && !gridAi.LiveProjectile.Contains(w.Target.Projectile)) w.Target.Reset();
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

                        if (gunner && Ui.MouseButtonPressed)
                        {
                            w.TargetPos = Vector3D.Zero;
                            var currentAmmo = comp.Gun.GunBase.CurrentAmmo;
                            if (currentAmmo <= 1) comp.Gun.GunBase.CurrentAmmo += 1;
                        }

                        if (w.DelayCeaseFire)
                        {
                            if (gunner || !w.AiReady || w.DelayFireCount++ > w.System.TimeToCeaseFire)
                            {
                                w.DelayFireCount = 0;
                                w.AiReady = gunner || !w.Target.Expired && ((w.TrackingAi || !w.TrackTarget) && w.Comp.TurretTargetLock) || !w.TrackingAi && w.TrackTarget && !w.Target.Expired;
                            }
                        }
                        else w.AiReady = gunner || !w.Target.Expired && ((w.TrackingAi || !w.TrackTarget) && w.Comp.TurretTargetLock) || !w.TrackingAi && w.TrackTarget && !w.Target.Expired;

                        w.SeekTarget = w.Target.Expired && w.TrackTarget || gridAi.TargetResetTick == Tick;

                        if (w.TargetWasExpired != w.Target.Expired)
                            w.EventTriggerStateChanged(Weapon.EventTriggers.Tracking, !w.Target.Expired);

                        if (w.TurretMode)
                        {
                            if (comp.State.Value.Online)
                            {
                                if (((w.TargetWasExpired != w.Target.Expired && w.Target.Expired) ||
                                     (gunner != lastGunner && !gunner)))
                                    _futureEvents.Schedule(ReturnHome, w, Tick + 240);

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
                            w.ReturnHome = w.ReturnHome && w.ManualShoot == ShootOff && !comp.Gunner;
                            if (w.ReturnHome)
                                comp.ReturnHome = gridAi.ReturnHome = true;
                        }

                        if (!w.System.EnergyAmmo && w.CurrentAmmo == 0 && w.CurrentMags > 0)
                            gridAi.Reloading = true;

                        if (w.AiReady || w.SeekTarget || gunner || w.ManualShoot != ShootOff || gridAi.Reloading || w.ReturnHome) gridAi.Ready = true;
                    }
                }
            }
        }

        private void UpdateWeaponPlatforms()
        {
            if (!GameLoaded) return;
            if (!DbsUpdating && DbsToUpdate.Count > 0) UpdateDbsInQueue();
            foreach (var aiPair in GridTargetingAIs)
            {
                var gridAi = aiPair.Value;
                gridAi.UpdateBlockGroups();
                if (!DbsUpdating && Tick - gridAi.TargetsUpdatedTick > 100) gridAi.RequestDbUpdate();

                if (!gridAi.Ready || !gridAi.MyGrid.InScene || !gridAi.GridInit) continue;

                if ((gridAi.SourceCount > 0 && (gridAi.UpdatePowerSources || Tick60)))
                    gridAi.UpdateGridPower(true);

                foreach (var basePair in gridAi.WeaponBase)
                {
                    var comp = basePair.Value;
                    var ammoCheck = comp.MultiInventory && !comp.FullInventory && Tick - comp.LastAmmoUnSuspendTick >= Weapon.SuspendAmmoCount;

                    if (gridAi.RecalcPowerPercent) comp.CompPowerPerc = comp.MaxRequiredPower / gridAi.TotalSinkPower;

                    if (!comp.MainInit || (!comp.State.Value.Online && !comp.ReturnHome) || !gridAi.Ready) continue;
                    if (comp.Debug)
                    {
                        DsDebugDraw.DrawLine(comp.MyPivotTestLine, Color.Green, 0.05f);
                        DsDebugDraw.DrawLine(comp.MyBarrelTestLine, Color.Red, 0.05f);
                        DsDebugDraw.DrawLine(comp.MyCenterTestLine, Color.Blue, 0.05f);
                        DsDebugDraw.DrawSingleVec(comp.MyPivotPos, 0.5f, Color.White);
                    }
                    if ((gridAi.RecalcLowPowerTick != 0 && gridAi.RecalcLowPowerTick <= Tick) || gridAi.AvailablePowerIncrease)
                        comp.UpdateCompPower();
                    for (int j = 0; j < comp.Platform.Weapons.Length; j++)
                    {
                        var w = comp.Platform.Weapons[j];

                        if (!comp.Set.Value.Weapons[w.WeaponId].Enable || comp.Overheated || (!gridAi.Ready && !w.Reloading))
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
                                    w.StopShooting(true);

                                comp.Charging = true;
                            }
                            comp.TerminalRefresh();
                        }
                        else comp.Charging = false;
                        
                        if (comp.Charging) continue;

                        if (ammoCheck)
                        {
                            if (w.AmmoSuspend && w.UnSuspendAmmoTick++ >= Weapon.UnSuspendAmmoCount)
                                AmmoPull(comp, w, false);
                            else if (!w.AmmoSuspend && comp.Gun.GunBase.CurrentAmmoMagazineId == w.System.AmmoDefId && w.SuspendAmmoTick++ >= Weapon.SuspendAmmoCount)
                                AmmoPull(comp, w, true);
                        }
                        if (!w.System.EnergyAmmo && w.CurrentAmmo == 0)
                        {
                            if (w.AmmoMagTimer == int.MaxValue)
                            {
                                if (!w.Reloading)
                                {
                                    w.EventTriggerStateChanged(state: Weapon.EventTriggers.Firing, active: true, pause: true);
                                    if (w.IsShooting)
                                        w.StopShooting(true);
                                }
                                if (w.CurrentMags != 0)
                                    w.StartReload();
                                else if(!w.Reloading)
                                    w.EventTriggerStateChanged(Weapon.EventTriggers.OutOfAmmo, true);

                                w.Reloading = true;
                                continue;
                            }
                            if (!w.AmmoMagLoaded) continue;
                            w.EventTriggerStateChanged(Weapon.EventTriggers.Reloading, false);

                            if (w.IsShooting)
                            {
                                if (w.FiringEmitter != null) w.StartFiringSound();
                                if (w.PlayTurretAv && w.RotateEmitter != null && !w.RotateEmitter.IsPlaying) w.StartRotateSound();
                                comp.CurrentDPS += w.DPS;
                            }
                            w.Reloading = false;
                        }
                        if (w.SeekTarget)
                        {
                            if (!w.SleepTargets || Tick - w.TargetCheckTick > 119 || gridAi.TargetResetTick == Tick) GridAi.AcquireTarget(w);
                        }
                        else if (w.IsTurret && !w.TrackTarget && w.Target.Expired)
                            w.Target = w.Comp.TrackingWeapon.Target;

                        if (!w.Target.Expired)
                            w.ReturnHome = false;
                        else if (w.ReturnHome)
                            w.TurretHomePosition();

                        if (w.TrackingAi && w.AvCapable && comp.RotationEmitter != null && Vector3D.DistanceSquared(CameraPos, comp.MyPivotPos) < 10000)
                        {
                            if (w.IsTracking && comp.AiMoving && !comp.RotationEmitter.IsPlaying)
                                comp.RotationEmitter.PlaySound(comp.RotationSound, true, false, false, false, false, false);
                            else if ((!w.IsTracking || !comp.AiMoving && Tick - comp.LastTrackedTick > 30) && comp.RotationEmitter.IsPlaying)
                                comp.StopRotSound(false);
                        }
                        if (w.ManualShoot == ShootOn || w.ManualShoot == ShootOnce || (w.ManualShoot == ShootOff && w.AiReady && !comp.Gunner) || ((w.ManualShoot == ShootClick ||comp.Gunner) && (j == 0 && Ui.MouseButtonLeft || j == 1 && Ui.MouseButtonRight)))
                        {
                            w.Shoot();
                            if (w.ManualShoot == ShootOnce) {
                                w.ManualShoot = ShootOff;
                                gridAi.ManualComps = gridAi.ManualComps - 1 > 0 ? gridAi.ManualComps - 1 : 0;
                            }
                        }
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
        }
    }
}