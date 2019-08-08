using Sandbox.ModAPI;
using VRageMath;
using WeaponCore.Platform;
using WeaponCore.Support;
using static WeaponCore.Support.WeaponComponent.EmissiveStates;

namespace WeaponCore
{
    public partial class Session
    {
        private void UpdateWeaponPlatforms()
        {
            if (!GameLoaded) return;
            if (!DbUpdating && DbsToUpdate.Count > 0) UpdateDbsInQueue();
            foreach (var aiPair in GridTargetingAIs)
            {
                var gridAi = aiPair.Value;
                if (Tick - gridAi.TargetsUpdatedTick > 100) gridAi.RequestDbUpdate();
                if (!gridAi.Ready || !gridAi.DbReady || !gridAi.MyGrid.InScene) continue;
                if (gridAi.GridMaxPower <= 0 || Tick60)
                {
                    gridAi.UpdateGridPower();
                    Log.Line($"MaxPower:{gridAi.GridMaxPower} - AvailPower:{gridAi.GridAvailablePower} - CurrentPower:{gridAi.GridCurrentPower}");
                }
                foreach (var basePair in gridAi.WeaponBase)
                {
                    var comp = basePair.Value;
                    var ammoCheck = comp.MultiInventory && !comp.FullInventory && Tick - comp.LastAmmoUnSuspendTick >= Weapon.SuspendAmmoCount;
                    var gun = comp.Gun.GunBase;

                    if (!comp.MainInit || !comp.State.Value.Online) continue;
                    for (int j = 0; j < comp.Platform.Weapons.Length; j++)
                    {
                        var w = comp.Platform.Weapons[j];
                        if (!w.Enabled || (!Tick60 && comp.Overheated)) continue;

                        if (Tick60)
                        {
                            w.CurrentHeat = w.CurrentHeat >= w.HSRate ? w.CurrentHeat -= w.HSRate : 0;
                            if (comp.Overheated && w.CurrentHeat <= (w.System.MaxHeat * w.System.WepCooldown))
                            {
                                //comp.ChangeStateEmissive(w, Overheat, 0);
                                comp.Overheated = false;
                            }
                        }

                        if (Tick60 && comp.Charging) {
                            comp.ChargeAmtLeft -= gridAi.GridAvailablePower;
                            
                            if (comp.ChargeAmtLeft <= 0) {
                                comp.ChargeAmtLeft = 0;
                                comp.Charging = false;
                            }
                            comp.TerminalRefresh();
                        }

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
                                //comp.ChangeStateEmissive(w, Reload, 0);
                                if (w.CurrentMags != 0)
                                {
                                    w.LoadAmmoMag = true;
                                    w.StartReloadSound();
                                }
                                continue;
                            }
                            if (!w.AmmoMagLoaded) continue;
                            //comp.ChangeStateEmissive(w, Reload, 1);
                        }
                        if (w.SeekTarget)
                        {
                            if (w.LastTargetCheck++ == 0 || w.LastTargetCheck == 60) GridAi.AcquireTarget(w);
                            w.TargetExpired = w.NewTarget.Entity == null || w.NewTarget.Entity.MarkedForClose;
                            w.NewTarget.TransferTo(w.Target);
                        }
                        else if (!w.TrackTarget && w.TargetExpired)
                        {
                            w.Target = w.Comp.TrackingWeapon.Target;
                            w.TargetExpired = false;
                        }

                        if (w.TrackingAi && w.AvCapable && comp.RotationEmitter != null && Vector3D.DistanceSquared(Session.Camera.Position, comp.MyPivotPos) < 10000)
                        {
                            if (w.IsTracking && comp.AiMoving && !comp.RotationEmitter.IsPlaying)
                                comp.RotationEmitter.PlaySound(comp.RotationSound, true, false, false, false, false, false);
                            else if ((!w.IsTracking || !comp.AiMoving && Tick - comp.LastTrackedTick > 30) && comp.RotationEmitter.IsPlaying)
                                comp.StopRotSound(false);
                        }

                        
                        if (!comp.Overheated && (w.AiReady || comp.Gunner && (j == 0 && MouseButtonLeft || j == 1 && MouseButtonRight))) w.Shoot();
                        else if (w.IsShooting) w.StopShooting();
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
                if (!gridAi.DbReady || !gridAi.MyGrid.InScene) continue;
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
                                if (w.Target.Entity == null || w.Target.Entity.MarkedForClose || !Weapon.TrackingTarget(w, w.Target.Entity, true))
                                    w.TargetExpired = true;
                            }
                            else
                            {
                                if (w.IsTurret)
                                {
                                    if (!w.TrackTarget)
                                    {
                                        if (comp.TrackingWeapon.Target.Entity != w.Target.Entity)
                                            w.TargetExpired = true;
                                        else if (w.Target.Entity == null)
                                            w.TargetExpired = true;
                                    }
                                    else if (!w.TargetExpired && (w.Target.Entity == null || w.Target.Entity.MarkedForClose || !Weapon.ValidTarget(w, w.Target.Entity)))
                                        w.TargetExpired = true;
                                }
                                else
                                {
                                    if (w.TrackTarget && !w.TargetExpired && (w.Target.Entity == null || w.Target.Entity.MarkedForClose || !Weapon.ValidTarget(w, w.Target.Entity)))
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
                        //var wasExpired = w.TargetExpired;
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
                        /*
                        if (wasExpired != w.TargetExpired)
                        {
                            comp.ChangeStateEmissive(w, Lock, wasExpired ? 1 : 0);
                        }
                        */
                    }
                }
            }
        }
    }
}