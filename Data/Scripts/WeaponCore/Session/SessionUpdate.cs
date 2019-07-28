using WeaponCore.Platform;
using WeaponCore.Support;
namespace WeaponCore
{
    public partial class Session
    {
        private void UpdateWeaponPlatforms()
        {
            if (!GameLoaded) return;
            if (!DbUpdating && DbsToUpdate.Count > 0) StartUpdatingDbs();
            foreach (var aiPair in GridTargetingAIs)
            {
                var gridAi = aiPair.Value;
                if (gridAi.Stale && Tick - gridAi.TargetsUpdatedTick > 100) gridAi.TimeToUpdateDb();
                if (!gridAi.Ready || !gridAi.DbReady) continue;
                foreach (var basePair in gridAi.WeaponBase)
                {
                    var comp = basePair.Value;
                    var ammoCheck = comp.MultiInventory && !comp.FullInventory && Tick - comp.LastAmmoUnSuspendTick >= Weapon.SuspendAmmoCount;
                    var gun = comp.Gun.GunBase;

                    if (!comp.MainInit || !comp.State.Value.Online) continue;
                    for (int j = 0; j < comp.Platform.Weapons.Length; j++)
                    {
                        var w = comp.Platform.Weapons[j];
                        if (!w.Enabled) continue;
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
                                if (w.CurrentMags != 0)
                                {
                                    w.LoadAmmoMag = true;
                                    w.StartReloadSound();
                                }
                                continue;
                            }
                            if (!w.AmmoMagLoaded) continue;
                        }
                        if (w.SeekTarget)
                        {
                            if (w.LastTargetCheck++ == 0 || w.LastTargetCheck == 30) gridAi.SelectTarget(ref w.NewTarget, w);
                            w.TargetExpired = w.NewTarget == null || w.NewTarget.MarkedForClose;
                            w.Target = w.NewTarget;

                        }
                        else if (!w.TrackTarget && w.TargetExpired)
                        {
                            w.Target = w.Comp.TrackingWeapon.Target;
                            w.TargetExpired = false;
                        }

                        if (w.TrackingAi && w.AvCapable && comp.RotationEmitter != null)
                        {
                            if (w.IsTracking && comp.AiMoving && !comp.RotationEmitter.IsPlaying)
                                comp.RotationEmitter.PlaySound(comp.RotationSound, true, false, false, false, false, false);
                            else if ((!w.IsTracking || !comp.AiMoving && Tick - comp.LastTrackedTick > 30) && comp.RotationEmitter.IsPlaying)
                                comp.StopRotSound(false);
                        }

                        if (w.AiReady || comp.Gunner && (j == 0 && MouseButtonLeft || j == 1 && MouseButtonRight)) w.Shoot();
                        else if (w.IsShooting)
                        {
                            //Log.Line($"ai not ready");
                            w.StopShooting();
                        }
                        if (w.AvCapable && w.BarrelAvUpdater.Reader.Count > 0) w.ShootGraphics();
                    }
                }
                gridAi.Ready = false;
            }
        }

        private void AiLoop()
        {
            if (!GameLoaded) return;
            foreach (var aiPair in GridTargetingAIs)
            {
                var gridAi = aiPair.Value;
                if (!gridAi.DbReady) continue;
                foreach (var basePair in gridAi.WeaponBase)
                {
                    var comp = basePair.Value;
                    var gunner = comp.Gunner = ControlledEntity == comp.MyCube;
                    InTurret = gunner;
                    if (!comp.MainInit || !comp.State.Value.Online) continue;
                    for (int j = 0; j < comp.Platform.Weapons.Length; j++)
                    {
                        var w = comp.Platform.Weapons[j];
                        if (!w.Enabled && comp.TrackingWeapon != w) continue;
                        if (!gunner)
                        {
                            if (w.TrackingAi)
                            {
                                if (w.Target == null || w.Target.MarkedForClose || !Weapon.TrackingTarget(w, w.Target, true))
                                    w.TargetExpired = true;
                            }
                            else
                            {
                                if (w.IsTurret)
                                {
                                    if (!w.TrackTarget)
                                    {
                                        if (comp.TrackingWeapon.Target != w.Target)
                                            w.TargetExpired = true;
                                        else if (w.Target == null)
                                            w.TargetExpired = true;
                                    }
                                    else if (w.Target == null || w.Target.MarkedForClose || !Weapon.ValidTarget(w, w.Target))
                                        w.TargetExpired = true;
                                }
                                else
                                {
                                    if (w.TrackTarget && w.Target == null || w.Target.MarkedForClose || !Weapon.ValidTarget(w, w.Target))
                                        w.TargetExpired = true;
                                }
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
                                w.AiReady = (!w.TargetExpired && !gunner) && ((w.TrackingAi || !w.TrackTarget) && w.Comp.TurretTargetLock) || !w.TrackingAi && w.TrackTarget && !w.TargetExpired;
                            }
                        }
                        else w.AiReady = (!w.TargetExpired && !gunner) && ((w.TrackingAi || !w.TrackTarget) && w.Comp.TurretTargetLock) || !w.TrackingAi && w.TrackTarget && !w.TargetExpired;

                        w.SeekTarget = !gunner && w.TargetExpired && w.TrackTarget;
                        if (w.AiReady || w.SeekTarget || gunner) gridAi.Ready = true;
                    }
                }
            }
        }
    }
}