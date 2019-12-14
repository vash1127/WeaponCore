using Sandbox.Game;
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

                //moved from update
                if (Tick - gridAi.TargetsUpdatedTick > 100 && DbCallBackComplete && DbTask.IsComplete && gridAi.UpdateOwner())
                    gridAi.RequestDbUpdate();

                if (!gridAi.DeadProjectiles.IsEmpty)
                {
                    Projectile p;
                    while (gridAi.DeadProjectiles.TryDequeue(out p)) gridAi.LiveProjectile.Remove(p);
                    gridAi.LiveProjectileTick = Tick;
                }

                if (!gridAi.HasGunner && !gridAi.DbReady && gridAi.ManualComps == 0 && !gridAi.CheckReload || !gridAi.MyGrid.InScene || gridAi.MyGrid.MarkedForClose) continue;

                if (gridAi.HasPower || gridAi.HadPower || gridAi.UpdatePowerSources || Tick180) gridAi.UpdateGridPower();
                if (!gridAi.HasPower) continue;         

                foreach (var basePair in gridAi.WeaponBase)
                {
                    var comp = basePair.Value;
                    if (comp.Platform.State != MyWeaponPlatform.PlatformState.Ready)
                        continue;

                    //if (gridAi.RecalcPowerPercent) comp.CompPowerPerc = comp.MaxRequiredPower / gridAi.TotalSinkPower;

                    if (!comp.State.Value.Online || comp.Status != Started)
                    {
                        if (comp.Status != Started) comp.HealthCheck();
                        continue;
                    }

                    for (int j = 0; j < comp.Platform.Weapons.Length; j++)
                    {
                        var w = comp.Platform.Weapons[j];
                        var lastGunner = comp.Gunner;
                        var gunner = comp.Gunner = comp.MyCube == ControlledEntity;
                        w.TargetWasExpired = w.Target.Expired;

                        if (!comp.Set.Value.Weapons[w.WeaponId].Enable) continue;
                        if (w.Target.Entity == null && w.Target.Projectile == null)
                            w.Target.Expired = true;
                        else if (w.Target.Entity != null && w.Target.Entity.MarkedForClose)
                            w.Target.Reset();
                        else if (w.Target.Projectile != null && (!gridAi.LiveProjectile.Contains(w.Target.Projectile) || w.Target.IsProjectile && w.Target.Projectile.State != Projectile.ProjectileState.Alive))
                            w.Target.Reset();

                        else if (w.TrackingAi && comp.Set.Value.Weapons[w.WeaponId].Enable)
                        {
                            if (!Weapon.TrackingTarget(w, w.Target, !gunner))
                                w.Target.Expired = true;
                        }
                        else
                        {
                            Vector3D targetPos;
                            if (w.IsTurret)
                            {
                                if (!w.TrackTarget)
                                {
                                    if ((comp.TrackingWeapon.Target.Projectile != w.Target.Projectile || w.Target.IsProjectile && w.Target.Projectile.State != Projectile.ProjectileState.Alive || comp.TrackingWeapon.Target.Entity != w.Target.Entity))
                                        w.Target.Reset();
                                }
                                else if (!w.Target.Expired && !Weapon.TargetAligned(w, w.Target, out targetPos))
                                    w.Target.Reset();
                            }
                            else if (w.TrackTarget && !Weapon.TargetAligned(w, w.Target, out targetPos))
                                w.Target.Expired = true;
                        }

                        if (gunner && UiInput.MouseButtonPressed)
                            w.TargetPos = Vector3D.Zero;

                        if ((w.Target.Expired && w.TrackTarget) || gridAi.TargetResetTick == Tick)
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

                        if (w.DelayCeaseFire)
                        {
                            if (gunner || !w.AiReady || w.DelayFireCount++ > w.System.TimeToCeaseFire)
                            {
                                w.DelayFireCount = 0;
                                w.AiReady = gunner || !w.Target.Expired && ((w.TrackingAi || !w.TrackTarget) && w.TurretTargetLock) || !w.TrackingAi && w.TrackTarget && !w.Target.Expired;
                            }
                        }
                        else w.AiReady = gunner || !w.Target.Expired && ((w.TrackingAi || !w.TrackTarget) && w.TurretTargetLock) || !w.TrackingAi && w.TrackTarget && !w.Target.Expired;

                        if (w.TargetWasExpired != w.Target.Expired)
                        {
                            w.EventTriggerStateChanged(Weapon.EventTriggers.Tracking, !w.Target.Expired);
                            w.EventTriggerStateChanged(Weapon.EventTriggers.StopTracking, w.Target.Expired);
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
                                    FutureEvents.Schedule(ReturnHome, w, 240);

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
                        }

                        if (gridAi.CheckReload && w.System.AmmoDefId == gridAi.NewAmmoType) ComputeStorage(w);

                        if (comp.Debug) WeaponDebug(w);

                        var reloading = !w.System.EnergyAmmo && w.Reloading;

                        if (!comp.Overheated && !reloading && !w.System.DesignatorWeapon && (wState.ManualShoot == ShootOn || wState.ManualShoot == ShootOnce || (wState.ManualShoot == ShootOff && w.AiReady && !comp.Gunner) || ((wState.ManualShoot == ShootClick || comp.Gunner) && (j == 0 && UiInput.MouseButtonLeft || j == 1 && UiInput.MouseButtonRight))))
                        {
                            if (gridAi.AvailablePowerChange)
                                w.DelayTicks = 0;

                            if (w.DelayTicks == 0 || w.ChargeUntilTick <= Tick)
                                ShootingWeapons.Enqueue(w);
                            else if (w.ChargeUntilTick > Tick)
                                w.Charging = true;
                        }
                        else if (w.IsShooting)
                            w.StopShooting();


                    }
                }
                gridAi.OverPowered = gridAi.RequestedWeaponsDraw > 0 && gridAi.RequestedWeaponsDraw > gridAi.GridMaxPower;
                gridAi.CheckReload = false;
                gridAi.AvailablePowerChange = false;
            }
        }

        private void UpdateWeaponPlatforms()
        {
            if (!GameLoaded) return;

            while (ShootingWeapons.Count > 0)
            {
                var w = ShootingWeapons.Dequeue();
                var comp = w.Comp;
                var ai = w.Comp.Ai;

                //TODO add logic for power priority
                if (ai.OverPowered && (w.System.EnergyAmmo || w.System.IsHybrid))
                {
                    
                    if (w.DelayTicks == 0)
                    {
                        Log.Line($"Recalc");
                        var percUseable = w.RequiredPower / ai.RequestedWeaponsDraw;
                        var oldUseable = w.UseablePower;
                        w.UseablePower = (ai.GridMaxPower * .98f) * percUseable;

                        if (w.IsShooting)
                        {
                            comp.SinkPower = (comp.SinkPower - oldUseable) + w.UseablePower;
                            comp.MyCube.ResourceSink.Update();
                        }

                        w.DelayTicks = 1 + ((uint)(w.RequiredPower - w.UseablePower) * 20); //arbitrary charge rate ticks/watt should be config

                        w.ChargeUntilTick = Tick + w.DelayTicks;
                        w.Charging = true;
                    }
                    else if (w.ChargeUntilTick <= Tick)
                    {
                        Log.Line($"Charged");
                        w.Charging = false;
                        w.ChargeUntilTick = Tick + w.DelayTicks;
                    }
                    comp.TerminalRefresh();
                }
                else if(w.RequiredPower - w.UseablePower > 0.0001)
                {
                    Log.Line($"Full Power");
                    var oldUseable = w.UseablePower;
                    w.UseablePower = w.RequiredPower;
                    comp.SinkPower = (comp.SinkPower - oldUseable) + w.UseablePower;
                    w.DelayTicks = 0;
                    w.Charging = false;
                }

                if (!comp.Set.Value.Weapons[w.WeaponId].Enable || w.Charging)
                    continue;

                w.Shoot();
                if (w.AvCapable && w.BarrelAvUpdater.Reader.Count > 0) w.ShootGraphics();
            }

            if (DbCallBackComplete && DbsToUpdate.Count > 0 && DbTask.IsComplete) UpdateDbsInQueue();
        }
    }
}