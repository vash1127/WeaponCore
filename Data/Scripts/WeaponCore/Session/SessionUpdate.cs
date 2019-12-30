using SpaceEngineers.Game.ModAPI;
using VRageMath;
using WeaponCore.Platform;
using WeaponCore.Projectiles;
using WeaponCore.Support;
using static WeaponCore.Support.WeaponComponent.Start;
using static WeaponCore.Platform.Weapon.TerminalActionState;
using System.Collections.Generic;
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
                if (!gridAi.GridInit || !gridAi.MyGrid.InScene || gridAi.MyGrid.MarkedForClose) 
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
                    continue;

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
                    if (comp.Platform.State != MyWeaponPlatform.PlatformState.Ready)
                        continue;

                    if (!comp.State.Value.Online || comp.Status != Started) {

                        if (comp.Status != Started) 
                            comp.HealthCheck();

                        continue;
                    }
                    ///
                    ///
                    /// Weapon update section
                    ///
                    ///

                    for (int j = 0; j < comp.Platform.Weapons.Length; j++) {

                        var w = comp.Platform.Weapons[j];
                        var lastGunner = comp.Gunner;
                        var gunner = comp.Gunner = comp.MyCube == ControlledEntity;

                        if (!comp.Set.Value.Weapons[w.WeaponId].Enable) 
                            continue;

                        ///
                        /// Check target for expire states
                        /// 
                        
                        w.TargetState = w.Target.State;
                        if (w.Target.State == Targets.Acquired) {

                            if (w.Target.Entity == null && w.Target.Projectile == null) {

                                w.Target.Reset();
                            }
                            else if (w.Target.Entity != null && w.Target.Entity.MarkedForClose) {

                                w.Target.Reset();
                            }
                            else if (w.Target.Projectile != null && (!gridAi.LiveProjectile.Contains(w.Target.Projectile) || w.Target.IsProjectile && w.Target.Projectile.State != Projectile.ProjectileState.Alive)) {

                                w.Target.Reset();
                            }
                            else if (w.TrackingAi) {

                                //DsUtil2.Start("");
                                if (!Weapon.TrackingTarget(w, w.Target, !gunner)) {

                                    w.Target.Reset();
                                }
                                //DsUtil2.Complete("", false, true);
                            }
                            else {

                                Vector3D targetPos;
                                if (w.IsTurret) {

                                    if (!w.TrackTarget) {

                                        if ((comp.TrackingWeapon.Target.Projectile != w.Target.Projectile || w.Target.IsProjectile && w.Target.Projectile.State != Projectile.ProjectileState.Alive || comp.TrackingWeapon.Target.Entity != w.Target.Entity)) {

                                            w.Target.Reset();
                                        }
                                    }
                                    else if (!Weapon.TargetAligned(w, w.Target, out targetPos)) {

                                        w.Target.Reset();
                                    }
                                }
                                else if (w.TrackTarget && !Weapon.TargetAligned(w, w.Target, out targetPos)) {

                                    w.Target.Reset();
                                }
                            }
                        }

                        var targetAcquired = w.TargetState != Targets.Acquired && w.Target.State == Targets.Acquired;
                        var targetLost = w.TargetState == Targets.Acquired && w.Target.State != Targets.Acquired;
                        var targetChanged = targetAcquired || targetLost;
                        if (gunner && UiInput.MouseButtonPressed)
                            w.TargetPos = Vector3D.Zero;

                        ///
                        /// Set weapon Ai state
                        /// 

                        if (w.DelayCeaseFire) {

                            if (gunner || !w.AiReady || w.DelayFireCount++ > w.System.TimeToCeaseFire) {

                                w.DelayFireCount = 0;
                                w.AiReady = gunner || (w.Target.State == Targets.Acquired && (w.TrackingAi || !w.TrackTarget) && w.Target.TargetLock) || (!w.TrackingAi && w.TrackTarget && w.Target.State == Targets.Acquired);
                            }
                        }
                        else {

                            w.AiReady = gunner || (w.Target.State == Targets.Acquired && (w.TrackingAi || !w.TrackTarget) && w.Target.TargetLock) || (!w.TrackingAi && w.TrackTarget && w.Target.State == Targets.Acquired);
                        }

                        if (targetChanged) {

                            w.EventTriggerStateChanged(Weapon.EventTriggers.Tracking, w.Target.State == Targets.Acquired);
                            w.EventTriggerStateChanged(Weapon.EventTriggers.StopTracking, w.Target.State != Targets.Acquired);
                        }

                        ///
                        /// Queue for target acquire or set to tracking weapon.
                        /// 
                        
                        w.SeekTarget = w.TrackTarget && w.Target.State == Targets.Expired;
                        //Log.Line($"w.Target.State: {w.Target.State}");
                        if ((w.SeekTarget || w.TrackTarget && gridAi.TargetResetTick == Tick) && w.Target.State != Targets.StillSeeking && !gunner)
                        {
                            w.Target.State = Targets.StillSeeking;
                            AcquireTargets.Add(w);
                        }
                        else if (w.IsTurret && !w.TrackTarget && w.Target.State != Targets.Acquired)
                            w.Target = w.Comp.TrackingWeapon.Target;

                        ///
                        /// Check weapon's turret to see if its time to go home
                        /// 

                        var wState = comp.State.Value.Weapons[w.WeaponId];
                        if (w.TurretMode) {

                            if (comp.State.Value.Online) {
                                
                                if (targetChanged && w.Target.State != Targets.Acquired || gunner != lastGunner && !gunner) 
                                    FutureEvents.Schedule(w.HomeTurret, null, 240);

                                if (gunner != lastGunner && gunner) {

                                    gridAi.ManualComps++;
                                    comp.Shooting++;
                                }
                                else if (gunner != lastGunner && !gunner) {

                                    gridAi.ManualComps = gridAi.ManualComps - 1 > 0 ? gridAi.ManualComps - 1 : 0;
                                    comp.Shooting = comp.Shooting - 1 > 0 ? comp.Shooting - 1 : 0;
                                }
                            }
                        }

                        // reload if needed
                        if (gridAi.CheckReload && w.System.AmmoDefId == gridAi.NewAmmoType && !w.System.EnergyAmmo) 
                            ComputeStorage(w);

                        if (comp.Debug) 
                            WeaponDebug(w);

                        ///
                        /// Determine if its time to shoot
                        ///
                        
                        var reloading = (!w.System.EnergyAmmo || w.System.MustCharge) && w.Reloading;

                        if (!comp.Overheated && !reloading && !w.System.DesignatorWeapon && (wState.ManualShoot == ShootOn || wState.ManualShoot == ShootOnce || (wState.ManualShoot == ShootOff && w.AiReady && !comp.Gunner) || ((wState.ManualShoot == ShootClick || comp.Gunner) && !gridAi.SupressMouseShoot && (j == 0 && UiInput.MouseButtonLeft || j == 1 && UiInput.MouseButtonRight))))
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
                                    w.DelayTicks = 0;
                                }
                            }

                            var targetRequested = w.SeekTarget && targetChanged;
                            if (!targetRequested && (w.DelayTicks == 0 || w.ChargeUntilTick <= Tick))
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
                    }
                }

                gridAi.OverPowered = gridAi.RequestedWeaponsDraw > 0 && gridAi.RequestedWeaponsDraw > gridAi.GridMaxPower;
                gridAi.CheckReload = false;
            }

            if (DbCallBackComplete && DbsToUpdate.Count > 0 && DbTask.IsComplete) 
                UpdateDbsInQueue();
        }

        private void UpdateChargeWeapons() //Fully Inlined due to keen's mod profiler
        {
            for (int i = ChargingWeapons.Count - 1; i >= 0; i--)
            {
                var w = ChargingWeapons[i];
                if (!w.Comp.MyCube.InScene || w.Comp.MyCube.MarkedForClose)
                {
                    ChargingWeapons.RemoveAtFast(i);
                    continue;
                }

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

                    if (!w.Comp.Ai.Session.DedicatedServer)
                        w.Comp.TerminalRefresh();
                }

                if (w.ChargeUntilTick <= Tick)
                {
                    //Log.Line("Reloaded");
                    Weapon.Reloaded(w);

                    if (w.DrawingPower)
                        w.StopPowerDraw();

                    w.Comp.Ai.OverPowered = w.Comp.Ai.RequestedWeaponsDraw > 0 && w.Comp.Ai.RequestedWeaponsDraw > w.Comp.Ai.GridMaxPower;
                    ChargingWeapons.RemoveAtFast(i);
                    continue;
                }

                if (!w.Comp.Ai.OverPowered)
                {
                    //Log.Line($"DrawingPower: {w.DrawingPower}");
                    if (!w.DrawingPower)
                    {
                        //Log.Line("Reset Power");
                        w.OldUseablePower = w.UseablePower;
                        w.UseablePower = w.RequiredPower;
                        w.DrawPower();
                        w.DelayTicks = 0;
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
                        
                    w.DelayTicks = (uint)(((w.System.EnergyMagSize - w.CurrentCharge) / w.UseablePower) * MyEngineConstants.UPDATE_STEPS_PER_SECOND);
                    w.ChargeUntilTick = w.DelayTicks + Tick;

                    if (!w.DrawingPower)
                        w.DrawPower();
                    else
                        w.DrawPower(true);
                }
            }
        }

        private void CheckAcquire()
        {
            for (int i = AcquireTargets.Count - 1; i >= 0; i--)
            {
                var w = AcquireTargets[i];
                var gridAi = w.Comp.Ai;

                var sinceCheck = Tick - w.Target.CheckTick;
                var reacquire = gridAi.TargetResetTick == Tick || w.TrackProjectiles && gridAi.CheckProjectiles;

                if (sinceCheck > 239 || reacquire && w.Target.State == Targets.Acquired || sinceCheck > 60 && _count == w.LoadId) 
                {
                    var comp = w.Comp;
                    var weaponsInStandby = gridAi.ManualComps == 0 && !gridAi.CheckReload && gridAi.Gunners.Count == 0;
                    var weaponEnabled = !comp.State.Value.Online || comp.Set.Value.Weapons[w.WeaponId].Enable;

                    if (!weaponEnabled || !gridAi.DbReady && weaponsInStandby || w.Comp.Gunner || !gridAi.MyGrid.InScene || gridAi.MyGrid.MarkedForClose || !comp.MyCube.InScene)
                        continue;
                    
                    if (comp.TrackingWeapon != null && comp.TrackingWeapon.System.DesignatorWeapon && comp.TrackingWeapon != w && comp.TrackingWeapon.Target.State == Targets.Acquired)
                        GridAi.AcquireTarget(w, false, comp.TrackingWeapon.Target.Entity.GetTopMostParent());
                    else 
                        GridAi.AcquireTarget(w, gridAi.TargetResetTick == Tick);
                    
                    if (w.Target.State == Targets.Acquired) AcquireTargets.RemoveAtFast(i);
                }
            }
        }

        private void ShootWeapons() 
        {
            for (int i = 0; i < ShootingWeapons.Count; i++)
            {
                var w = ShootingWeapons[i];
                //TODO add logic for power priority
                if (w.Comp.Ai.OverPowered && (w.System.EnergyAmmo || w.System.IsHybrid) && !w.System.MustCharge) {

                    if (w.DelayTicks == 0) {
                        //Log.Line($"Adapting Current Requested: {w.Comp.Ai.RequestedWeaponsDraw}");
                        var percUseable = w.RequiredPower / w.Comp.Ai.RequestedWeaponsDraw;
                        w.OldUseablePower = w.UseablePower;
                        w.UseablePower = (w.Comp.Ai.GridMaxPower * .98f) * percUseable;

                        if (w.DrawingPower)
                            w.DrawPower(true);
                        else
                            w.DrawPower();

                        
                        w.DelayTicks = (uint)(((w.RequiredPower - w.UseablePower)  / w.UseablePower) * MyEngineConstants.UPDATE_STEPS_PER_SECOND);
                        w.ChargeUntilTick = Tick + w.DelayTicks;
                        w.Charging = true;
                    }
                    else if (w.ChargeUntilTick <= Tick) {
                        w.Charging = false;
                        w.ChargeUntilTick = Tick + w.DelayTicks;
                    }
                }
                //w.System.MustCharge must be first to prevent setting w.ResetPower
                else if (!w.System.MustCharge && (w.Charging || w.DelayTicks > 0 || w.ResetPower))
                {
                    w.OldUseablePower = w.UseablePower;
                    w.UseablePower = w.RequiredPower;
                    w.DrawPower(true);
                    w.DelayTicks = 0;
                    w.Charging = false;
                    w.ResetPower = false;
                }

                if (w.Charging)
                    continue;

                w.Shoot();

                if (!w.System.MustCharge && w.Comp.State.Value.Weapons[w.WeaponId].ManualShoot == ShootOnce)
                {
                    w.Comp.State.Value.Weapons[w.WeaponId].ManualShoot = ShootOff;
                    w.StopShooting();
                    w.Comp.Ai.ManualComps = w.Comp.Ai.ManualComps - 1 > 0 ? w.Comp.Ai.ManualComps - 1 : 0;
                    w.Comp.Shooting = w.Comp.Shooting - 1 > 0 ? w.Comp.Shooting - 1 : 0;
                }

                if (w.AvCapable && w.BarrelAvUpdater.Reader.Count > 0) 
                    w.ShootGraphics();
            }
            ShootingWeapons.Clear();
        }
    }
}