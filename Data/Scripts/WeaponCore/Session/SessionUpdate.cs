using WeaponCore.Platform;

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
                    var ammoCheck = comp.MultiInventory && !comp.FullInventory;
                    var gun = comp.Gun.GunBase;

                    if (!comp.MainInit || !comp.State.Value.Online) continue;
                    for (int j = 0; j < comp.Platform.Weapons.Length; j++)
                    {
                        var w = comp.Platform.Weapons[j];
                        if (ammoCheck)
                        {
                            if (w.AmmoSuspend && w.UnSuspendAmmoTick++ >= Weapon.UnSuspendAmmoCount)
                                AmmoPull(comp, w, false);
                            else if (!w.AmmoSuspend && gun.CurrentAmmoMagazineId == w.WeaponSystem.AmmoDefId && w.SuspendAmmoTick++ >= Weapon.SuspendAmmoCount)
                                AmmoPull(comp, w, true);
                        }

                        if (w.SeekTarget && w.TrackTarget) gridAi.SelectTarget(ref w.Target, w);

                        if (w.AiReady || comp.Gunner && (j == 0 && MouseButtonLeft || j == 1 && MouseButtonRight)) w.Shoot();
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
                    if (!comp.MainInit || !comp.State.Value.Online) continue;

                    for (int j = 0; j < comp.Platform.Weapons.Length; j++)
                    {
                        var w = comp.Platform.Weapons[j];
                        if (!gunner)
                        {
                            if (w.TrackingAi)
                            {
                                if (w.Target != null && !Weapon.TrackingTarget(w, w.Target, true))
                                    w.Target = null;
                            }
                            else
                            {
                                if (!w.TrackTarget) w.Target = comp.TrackingWeapon.Target;
                                if (w.Target != null && !Weapon.CheckTarget(w, w.Target)) w.Target = null;
                            }

                            if (w != comp.TrackingWeapon && comp.TrackingWeapon.Target == null) w.Target = null;
                        }
                        else
                        {
                            InTurret = true;
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