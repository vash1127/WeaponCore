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

                if ((!gridAi.DbReady && !gridAi.ReturnHome && gridAi.ManualComps == 0 && !gridAi.Reloading) || !gridAi.MyGrid.InScene) continue;
                gridAi.Reloading = false;
                foreach (var basePair in gridAi.WeaponBase)
                {
                    var comp = basePair.Value;
                    var LastGunner = comp.Gunner;
                    var gunner = comp.Gunner = ControlledEntity == comp.MyCube;
                    if (!comp.MainInit || (!comp.State.Value.Online && !comp.ReturnHome) || comp.Status != Started)
                    {
                        if (comp.Status != Started) comp.HealthCheck();
                        continue;
                    }

                    for (int j = 0; j < comp.Platform.Weapons.Length; j++)
                    {
                        var w = comp.Platform.Weapons[j];
                        w.TargetWasExpired = w.Target.Expired;
                        if (!comp.Set.Value.Weapons[w.WeaponId].Enable && !w.ReturnHome) continue;
                        if (w.Target.Entity == null && w.Target.Projectile == null) w.Target.Expired = true;
                        else if (w.Target.Entity != null && w.Target.Entity.MarkedForClose) w.Target.Reset();
                        else if (w.Target.Projectile != null && !gridAi.LiveProjectile.Contains(w.Target.Projectile)) w.Target.Reset();
                        else if (w.TrackingAi)
                            if (!Weapon.TrackingTarget(w, w.Target, !gunner)) w.Target.Expired = true;
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

                        w.SeekTarget = w.Target.Expired && w.TrackTarget;
                        
                        if (w.TargetWasExpired != w.Target.Expired)
                            w.EventTriggerStateChanged(Weapon.EventTriggers.Tracking, !w.Target.Expired);

                        if (w.TurretMode && comp.State.Value.Online)
                        {
                            if (((w.TargetWasExpired != w.Target.Expired && w.Target.Expired) ||
                                 (gunner != LastGunner && !gunner)))
                                w.LastTargetLock = Tick;

                            comp.ReturnHome = gridAi.ReturnHome = false;

                            if (w.LastTargetLock > 0)
                                comp.ReturnHome = gridAi.ReturnHome = true;

                            w.ReturnHome = (w.LastTargetLock + 240 < Tick && w.LastTargetLock > 0 || w.ReturnHome) && w.ManualShoot == ShootOff;
                        }

                        if (!w.System.EnergyAmmo && w.CurrentAmmo == 0 && w.CurrentMags > 0)
                            gridAi.Reloading = true;


                        if (w.AiReady || w.SeekTarget || gunner || w.ManualShoot != ShootOff || gridAi.Reloading) gridAi.Ready = true;
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
                if (!DbsUpdating && Tick - gridAi.TargetsUpdatedTick > 100) gridAi.RequestDbUpdate();

                if ((!gridAi.Ready && !gridAi.ReturnHome) || !gridAi.MyGrid.InScene || !gridAi.GridInit) continue;

                if ((gridAi.SourceCount > 0 && (gridAi.UpdatePowerSources || Tick60)))
                    gridAi.UpdateGridPower(true);

                foreach (var basePair in gridAi.WeaponBase)
                {
                    var comp = basePair.Value;
                    var ammoCheck = comp.MultiInventory && !comp.FullInventory && Tick - comp.LastAmmoUnSuspendTick >= Weapon.SuspendAmmoCount;
                    var gun = comp.Gun.GunBase;

                    if (gridAi.RecalcPowerPercent) comp.CompPowerPerc = comp.MaxRequiredPower / gridAi.TotalSinkPower;

                    if (!comp.MainInit || (!comp.State.Value.Online && !comp.ReturnHome) || (!gridAi.Ready && !comp.ReturnHome)) continue;

                    if ((gridAi.RecalcLowPowerTick != 0 && gridAi.RecalcLowPowerTick <= Tick) || gridAi.AvailablePowerIncrease)
                        comp.UpdateCompPower();

                    for (int j = 0; j < comp.Platform.Weapons.Length; j++)
                    {
                        var w = comp.Platform.Weapons[j];

                        if (gridAi.turnWeaponShootOff)
                        {
                            if (w.ManualShoot == ShootClick)
                            {
                                w.ManualShoot = ShootOff;
                                gridAi.ManualComps--;
                            }
                        }

                        if (!comp.Set.Value.Weapons[w.WeaponId].Enable || (!Tick60 && comp.Overheated) || (!gridAi.Ready && !w.Reloading))
                        {
                            if (w.ReturnHome)
                                w.ReturnHome = w.TurretHomePosition();

                            if (w.ReturnHome)
                            {
                                comp.ReturnHome = true;
                                gridAi.ReturnHome = true;
                            }

                            continue;
                        }
                        if (Tick60)
                        {
                            var weaponValue = comp.State.Value.Weapons[w.WeaponId];
                            comp.CurrentHeat = comp.CurrentHeat >= w.HsRate ? comp.CurrentHeat - w.HsRate : 0;
                            weaponValue.Heat = weaponValue.Heat >= w.HsRate ? weaponValue.Heat - w.HsRate : 0;
                            
                            comp.TerminalRefresh();
                            if (comp.Overheated && weaponValue.Heat <= (w.System.MaxHeat * w.System.WepCooldown))
                            {
                                w.EventTriggerStateChanged(Weapon.EventTriggers.Overheated, false);
                                comp.Overheated = false;
                            }
                        }

                        var energyAmmo = w.System.EnergyAmmo;

                        if ((energyAmmo || w.System.IsHybrid) && comp.DelayTicks > 0)
                        {
                            if (comp.ShootTick <= Tick)
                            {
                                comp.Charging = false;
                                comp.ShootTick = Tick + comp.DelayTicks;
                                comp.TerminalRefresh();
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
                                comp.TerminalRefresh();
                            }
                        }
                        else comp.Charging = false;
                        
                        if (comp.Overheated || comp.Charging) continue;

                        if (ammoCheck)
                        {
                            if (w.AmmoSuspend && w.UnSuspendAmmoTick++ >= Weapon.UnSuspendAmmoCount)
                                AmmoPull(comp, w, false);
                            else if (!w.AmmoSuspend && gun.CurrentAmmoMagazineId == w.System.AmmoDefId && w.SuspendAmmoTick++ >= Weapon.SuspendAmmoCount)
                                AmmoPull(comp, w, true);
                        }
                        if (!energyAmmo && w.CurrentAmmo == 0)
                        {
                            if (w.AmmoMagTimer == int.MaxValue)
                            {
                                if (!w.Reloading)
                                {
                                    w.EventTriggerStateChanged(state: Weapon.EventTriggers.Firing, active: true, pause: true);

                                    if (w.IsShooting)
                                    {
                                        w.StopShooting(true);
                                        comp.CurrentDPS -= w.DPS;
                                    }
                                }
                                if (w.CurrentMags != 0)
                                {
                                    w.EventTriggerStateChanged(Weapon.EventTriggers.Reloading, true);
                                    w.EventTriggerStateChanged(Weapon.EventTriggers.OutOfAmmo, false);
                                    w.LoadAmmoMag = true;
                                    w.StartReloadSound();
                                }
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
                            if (w.LastTargetCheck++ == 0 || w.LastTargetCheck == 60) GridAi.AcquireTarget(w);
                        }
                        else if (w.IsTurret && !w.TrackTarget && w.Target.Expired)
                            w.Target = w.Comp.TrackingWeapon.Target;

                        if (!w.Target.Expired)
                        {
                            w.LastTargetLock = 0;
                            w.ReturnHome = false;
                        }
                        else if (w.ReturnHome)
                        {
                            if (!(w.ReturnHome = w.TurretHomePosition()))
                                w.LastTargetLock = 0;
                        }

                        if (w.TrackingAi && w.AvCapable && comp.RotationEmitter != null && Vector3D.DistanceSquared(CameraPos, comp.MyPivotPos) < 10000)
                        {
                            if (w.IsTracking && comp.AiMoving && !comp.RotationEmitter.IsPlaying)
                                comp.RotationEmitter.PlaySound(comp.RotationSound, true, false, false, false, false, false);
                            else if ((!w.IsTracking || !comp.AiMoving && Tick - comp.LastTrackedTick > 30) && comp.RotationEmitter.IsPlaying)
                                comp.StopRotSound(false);
                        }
                        var manualShoot = w.ManualShoot;
                        if (manualShoot == ShootOn || manualShoot == ShootOnce || (manualShoot == ShootOff && w.AiReady && !comp.Gunner) || ((manualShoot == ShootClick ||comp.Gunner) && (j == 0 && Ui.MouseButtonLeft || j == 1 && Ui.MouseButtonRight)))
                        {
                            w.Shoot();
                            if (w.ManualShoot == ShootOnce) {
                                w.ManualShoot = ShootOff;
                                gridAi.ManualComps--;
                            }
                        }
                        else if (w.IsShooting)
                        {
                            w.EventTriggerStateChanged(Weapon.EventTriggers.Firing, false);
                            w.StopShooting();
                        }
                        if (w.AvCapable && w.BarrelAvUpdater.Reader.Count > 0) w.ShootGraphics();
                    }
                }
                gridAi.Ready = false;
                gridAi.AvailablePowerIncrease = false;
                gridAi.RecalcPowerPercent = false;
                gridAi.turnWeaponShootOff = false;

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