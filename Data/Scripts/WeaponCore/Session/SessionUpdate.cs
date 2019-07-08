using WeaponCore.Platform;
using WeaponCore.Support;
namespace WeaponCore
{
    public partial class Session
    {
        private void UpdateWeaponPlatforms()
        {
            if (!GameLoaded) return;
            foreach (var aiPair in GridTargetingAIs)
            {
                //var grid = aiPair.Key;
                var gridAi = aiPair.Value;
                if (!gridAi.Ready) continue;
                foreach (var basePair in gridAi.WeaponBase)
                {
                    //var myCube = basePair.Key;
                    var comp = basePair.Value;
                    //DsDebugDraw.DrawSingleVec(comp.MyPivotPos, 0.5f, Color.Red);
                    //DsDebugDraw.DrawLine(comp.MyPivotTestLine.From, comp.MyPivotTestLine.To, Color.Blue, 0.1f);
                    var ammoCheck = comp.MultiInventory && !comp.FullInventory && Tick - comp.LastAmmoUnSuspendTick >= Weapon.SuspendAmmoCount;
                    var gun = comp.Gun.GunBase;
                    if (!comp.MainInit || !comp.State.Value.Online) continue;
                    for (int j = 0; j < comp.Platform.Weapons.Length; j++)
                    {
                        var w = comp.Platform.Weapons[j];
                        var energyAmmo = w.System.EnergyAmmo;
                        if (ammoCheck)
                        {
                            if (w.AmmoSuspend && w.UnSuspendAmmoTick++ >= Weapon.UnSuspendAmmoCount)
                                AmmoPull(comp, w, false);
                            else if (!w.AmmoSuspend && gun.CurrentAmmoMagazineId == w.System.AmmoDefId && w.SuspendAmmoTick++ >= Weapon.SuspendAmmoCount)
                                AmmoPull(comp, w, true);
                        }
                        //Log.Line($"Turret: AiReady:{w.AiReady}({w.Comp.TurretTargetLock} - ValidTarget:{w.Target != null} - TrackingAi:{w.TrackingAi}  - Multi:{comp.MultiInventory} - FullInv:{comp.FullInventory}) - AmmoMa: AmmoSuspend:{w.AmmoSuspend} - AmmoFull:{w.AmmoFull} - AmmoCheck:{ammoCheck} - CurrAmmo:{w.CurrentAmmo} - CurrentMags:{w.CurrentMags} - Energy:{energyAmmo}");
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

                        if (w.SeekTarget && w.TrackTarget) gridAi.SelectTarget(ref w.Target, w);

                        if (w.TrackingAi && w.AvCapable && comp.RotationEmitter != null)
                        {
                            if (w.IsTracking && comp.AiMoving && !comp.RotationEmitter.IsPlaying)
                                comp.RotationEmitter.PlaySound(comp.RotationSound, true, false, false, false, false, false);
                            else if ((!w.IsTracking || !comp.AiMoving && Tick - comp.LastTrackedTick > 30) && comp.RotationEmitter.IsPlaying)
                                comp.StopRotSound(false);
                        }

                        if (w.AiReady || comp.Gunner && (j == 0 && MouseButtonLeft || j == 1 && MouseButtonRight)) w.Shoot();
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
                //var grid = aiPair.Key;
                var ai = aiPair.Value;
                foreach (var basePair in ai.WeaponBase)
                {
                    //var myCube = basePair.Key;
                    var comp = basePair.Value;
                    var gunner = comp.Gunner = ControlledEntity == comp.MyCube;
                    InTurret = gunner;
                    if (!comp.MainInit || !comp.State.Value.Online) continue;
                    for (int j = 0; j < comp.Platform.Weapons.Length; j++)
                    {
                        var w = comp.Platform.Weapons[j];
                        if (!gunner)
                        {
                            if (w.TrackingAi && w.Target != null)
                                Weapon.TrackingTarget(w, w.Target, true);
                            else 
                            {
                                if (w.IsTurret && !w.TrackTarget) w.Target = comp.TrackingWeapon.Target;
                                else if (w.Target != null && !Weapon.ValidTarget(w, w.Target)) w.Target = null;
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
                        w.AiReady = w.Target != null && !gunner && w.Comp.TurretTargetLock && !w.Target.MarkedForClose;
                        w.SeekTarget = Tick20 && !gunner && (w.Target == null || w.Target != null && w.Target.MarkedForClose) && w.TrackTarget;
                        if (w.AiReady || w.SeekTarget || gunner) ai.Ready = true;
                    }
                }
            }
        }
    }
}