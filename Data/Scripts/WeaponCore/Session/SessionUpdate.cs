using VRageMath;
using WeaponCore.Platform;
using WeaponCore.Projectiles;
using WeaponCore.Support;

namespace WeaponCore
{
    public partial class Session
    {
        private void UpdateWeaponPlatforms()
        {
            if (!GameLoaded) return;
            if (!DbsUpdating && DbsToUpdate.Count > 0) UpdateDbsInQueue();
            foreach (var aiPair in GridTargetingAIs)
            {
                var gridAi = aiPair.Value;

                if (!DbsUpdating && Tick - gridAi.TargetsUpdatedTick > 100) gridAi.RequestDbUpdate();

                if (!gridAi.Ready || !gridAi.DbReady || !gridAi.MyGrid.InScene) continue;
                if (!gridAi.DeadProjectiles.IsEmpty)
                {
                    Projectile p;
                    while (gridAi.DeadProjectiles.TryDequeue(out p)) gridAi.LiveProjectile.Remove(p);
                }

                if ((gridAi.SourceCount > 0 && (gridAi.GridMaxPower <= 0 || gridAi.UpdatePowerSources || Tick60)))
                {
                    gridAi.UpdateGridPower();
                    if (gridAi.GridMaxPower - gridAi.CurrentWeaponsDraw > gridAi.LastAvailablePower && gridAi.LastAvailablePower != 0) {
                        gridAi.AvailablePowerIncrease = true;
                        //Log.Line($"Increase");
                    }
                    gridAi.LastAvailablePower = gridAi.GridMaxPower - gridAi.CurrentWeaponsDraw;
                    //Log.Line($"avail power: {gridAi.GridMaxPower - gridAi.CurrentWeaponsDraw}  Last Power: {gridAi.LastAvailablePower} Max: {gridAi.GridMaxPower}  Weapon Draw: {gridAi.CurrentWeaponsDraw} Current Power: {gridAi.GridCurrentPower}");
                }

                foreach (var basePair in gridAi.WeaponBase)
                {
                    var cube = basePair.Key;
                    var comp = basePair.Value;
                    var ammoCheck = comp.MultiInventory && !comp.FullInventory && Tick - comp.LastAmmoUnSuspendTick >= Weapon.SuspendAmmoCount;
                    var gun = comp.Gun.GunBase;

                    if (gridAi.RecalcPowerPercent) comp.CompPowerPerc = comp.MaxRequiredPower / gridAi.TotalSinkPower;

                    if (!comp.MainInit || !comp.State.Value.Online) continue;

                    if ((gridAi.RecalcLowPowerTick > 0 && gridAi.RecalcLowPowerTick <= Tick) || gridAi.AvailablePowerIncrease)
                        comp.UpdateCompPower();

                    for (int j = 0; j < comp.Platform.Weapons.Length; j++)
                    {
                        var w = comp.Platform.Weapons[j];
                        if (!w.Enabled || (!Tick60 && comp.Overheated)) continue;

                        if (Tick60)
                        {
                            w.CurrentHeat = w.CurrentHeat >= w.HSRate ? w.CurrentHeat -= w.HSRate : 0;
                            if (comp.Overheated && w.CurrentHeat <= (w.System.MaxHeat * w.System.WepCooldown))
                            {
                                if (w.AvCapable) w.ChangeEmissiveState(Weapon.Emissives.Heating, false);
                                comp.Overheated = false;
                            }
                        }

                        if (comp.DelayTicks != 0 && w.IsShooting)
                        {
                            if (comp.ShootTick <= Tick)
                            {
                                comp.Charging = false;
                                comp.ShootTick = Tick + comp.DelayTicks;
                            }
                            else comp.Charging = true;
                        }
                        else comp.Charging = false;
                        
                        if (comp.Overheated || comp.Charging) continue;

                        var energyAmmo = w.System.EnergyAmmo;
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
                                if (w.AvCapable) w.ChangeEmissiveState(Weapon.Emissives.Reloading, true);
                                if (w.CurrentMags != 0)
                                {
                                    w.LoadAmmoMag = true;
                                    w.StartReloadSound();
                                }
                                continue;
                            }
                            if (!w.AmmoMagLoaded) continue;
                            if (w.AvCapable) w.ChangeEmissiveState(Weapon.Emissives.Reloading, false);
                        }
                        if (w.SeekTarget)
                        {
                            if (w.LastTargetCheck++ == 0 || w.LastTargetCheck == 60) GridAi.AcquireTarget(w);
                        }
                        else if (!w.TrackTarget && w.Target.Expired)
                            w.Target = w.Comp.TrackingWeapon.Target;

                        if (w.TrackingAi && w.AvCapable && comp.RotationEmitter != null && Vector3D.DistanceSquared(Session.Camera.Position, comp.MyPivotPos) < 10000)
                        {
                            if (w.IsTracking && comp.AiMoving && !comp.RotationEmitter.IsPlaying)
                                comp.RotationEmitter.PlaySound(comp.RotationSound, true, false, false, false, false, false);
                            else if ((!w.IsTracking || !comp.AiMoving && Tick - comp.LastTrackedTick > 30) && comp.RotationEmitter.IsPlaying)
                                comp.StopRotSound(false);
                        }

                        if (!comp.Overheated && (w.AiReady || comp.Gunner && (j == 0 && MouseButtonLeft || j == 1 && MouseButtonRight))) w.Shoot();
                        else if (w.IsShooting)
                        {
                            if (w.AvCapable) w.ChangeEmissiveState(Weapon.Emissives.Firing, false);
                            w.StopShooting();
                        }
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

        private void AiLoop()
        {
            if (!GameLoaded) return;
            foreach (var aiPair in GridTargetingAIs)
            {
                var gridAi = aiPair.Value;
                if (!gridAi.DbReady || !gridAi.MyGrid.InScene) continue;

                foreach (var basePair in gridAi.WeaponBase)
                {
                    var comp = basePair.Value;
                    var gunner = comp.Gunner = ControlledEntity == comp.MyCube;
                    InTurret = gunner;
                    if (!comp.MainInit) continue;
                    for (int j = 0; j < comp.Platform.Weapons.Length; j++)
                    {
                        var w = comp.Platform.Weapons[j];
                        if ((!comp.State.Value.Online || !comp.IsFunctional) && w.IsShooting) w.StopShooting();
                        if (!w.Enabled || !comp.State.Value.Online) continue;
                        if (!gunner)
                        {
                            if (w.Target.Entity == null && w.Target.Projectile == null) w.Target.Expired = true;
                            else if (w.Target.Entity != null && w.Target.Entity.MarkedForClose) w.Target.Reset();
                            else if (w.Target.Projectile != null && !gridAi.LiveProjectile.Contains(w.Target.Projectile)) w.Target.Reset();
                            else if (w.TrackingAi)
                            {
                                if (!Weapon.TrackingTarget(w, w.Target, true)) w.Target.Expired = true;
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
                        }
                        else
                        {
                            if (MouseButtonPressed)
                            {
                                var currentAmmo = comp.Gun.GunBase.CurrentAmmo;
                                if (currentAmmo <= 1) comp.Gun.GunBase.CurrentAmmo += 1;
                            }
                        }
                        if (w.DelayCeaseFire)
                        {
                            if (gunner || !w.AiReady || w.DelayFireCount++ > w.System.TimeToCeaseFire)
                            {
                                w.DelayFireCount = 0;
                                w.AiReady = (!w.Target.Expired && !InTurret) && ((w.TrackingAi || !w.TrackTarget) && w.Comp.TurretTargetLock) || !w.TrackingAi && w.TrackTarget && !w.Target.Expired;
                            }
                        }
                        else w.AiReady = (!w.Target.Expired && !gunner) && ((w.TrackingAi || !w.TrackTarget) && w.Comp.TurretTargetLock) || !w.TrackingAi && w.TrackTarget && !w.Target.Expired;

                        w.SeekTarget = !gunner && w.Target.Expired && w.TrackTarget;

                        if (w.AiReady || w.SeekTarget || gunner) gridAi.Ready = true;
                        //if (gridAi.MyGrid.CubeBlocks.Count < 1000) Log.Line($"{w.System.WeaponName} - shooting:{w.IsShooting} - wAi:{w.AiReady} - Expired:{w.Target.Expired} - gAi:{gridAi.Ready} - Seek:{w.SeekTarget}");

                        if (w.TargetWasExpired != w.Target.Expired)
                        {
                            w.ChangeEmissiveState(Weapon.Emissives.Tracking, !w.Target.Expired);
                        }
                    }
                }
            }
        }
    }
}