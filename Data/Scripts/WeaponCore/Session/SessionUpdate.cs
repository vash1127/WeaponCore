using VRageMath;
using WeaponCore.Platform;
using WeaponCore.Projectiles;
using WeaponCore.Support;
using System.Collections.Generic;
using VRage.Game;
using Sandbox.Game.Entities;
using static WeaponCore.Support.Target;
using static WeaponCore.Support.WeaponComponent.Start;
using static WeaponCore.Platform.Weapon.TerminalActionState;
using static WeaponCore.Support.WeaponDefinition.AnimationDef.PartAnimationSetDef;

namespace WeaponCore
{
    public partial class Session
    {
        private void AiLoop()
        { //Fully Inlined due to keen's mod profiler

            foreach (var aiPair in GridTargetingAIs)
            {
                ///
                /// GridAi update section
                ///
                var gridAi = aiPair.Value;

                using (gridAi.MyGrid.Pin())
                {

                    if (!gridAi.GridInit || gridAi.MyGrid.MarkedForClose || gridAi.Concealed)
                        continue;

                    var readyToUpdate = Tick - gridAi.TargetsUpdatedTick > 100 && DbCallBackComplete && DbTask.IsComplete;

                    if (readyToUpdate && gridAi.UpdateOwner())
                        gridAi.RequestDbUpdate();

                    if (gridAi.DeadProjectiles.Count > 0)
                    {
                        for (int i = 0; i < gridAi.DeadProjectiles.Count; i++) gridAi.LiveProjectile.Remove(gridAi.DeadProjectiles[i]);
                        gridAi.DeadProjectiles.Clear();
                        gridAi.LiveProjectileTick = Tick;
                    }
                    gridAi.CheckProjectiles = Tick - gridAi.NewProjectileTick <= 1;

                    if (!gridAi.HasPower && gridAi.HadPower || gridAi.UpdatePowerSources || !gridAi.WasPowered && gridAi.MyGrid.IsPowered || Tick10)
                        gridAi.UpdateGridPower();

                    if (!gridAi.HasPower)
                        continue;

                    ///
                    /// Comp update section
                    ///
                    for (int i = 0; i < gridAi.Weapons.Count; i++)
                    {
                        var comp = gridAi.Weapons[i];
                        using (comp.MyCube.Pin())
                        {
                            if (comp.MyCube.MarkedForClose || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready)
                                continue;

                            if (!comp.State.Value.Online || !comp.Set.Value.Overrides.Activate || comp.Status != Started) {
                                if (comp.Status != Started) comp.HealthCheck();
                                continue;
                            }

                            if (InMenu && Tick20 && gridAi.LastTerminal == comp.MyCube)
                                comp.TerminalRefresh();

                            var overRides = comp.Set.Value.Overrides;
                            var compCurPlayer = comp.State.Value.CurrentPlayerControl;

                            if (HandlesInput)
                                TargetUi.SetCompTrackReticle(comp);

                            comp.WasControlled = comp.UserControlled;
                            comp.UserControlled = compCurPlayer.ControlType != ControlType.None;

                            var leftClick = false;
                            var rightClick = false;

                            MouseStateData mouseState;
                            if (PlayerMouseStates.TryGetValue(compCurPlayer.PlayerId, out mouseState)) {
                                leftClick = mouseState.MouseButtonLeft;// && currentControl;
                                rightClick = mouseState.MouseButtonRight;// && currentControl;
                            }

                            if (overRides.ManualControl && IsClient && Tick10)
                                Log.Line($"leftClick: {leftClick} rightClick: {rightClick}  PlayerId: {compCurPlayer.PlayerId}");

                            ///
                            /// Weapon update section
                            ///
                            for (int j = 0; j < comp.Platform.Weapons.Length; j++) {

                                var w = comp.Platform.Weapons[j];
                                if (!w.Set.Enable) {

                                    if (w.Target.HasTarget && !IsClient)
                                        w.Target.Reset(comp.Session.Tick, States.Expired);
                                    continue;
                                }

                                if (w.Timings.WeaponReadyTick > Tick) continue;

                                if (w.AvCapable && (!w.PlayTurretAv || Tick60)) {
                                    var avWasEnabled = w.PlayTurretAv;
                                    w.PlayTurretAv = Vector3D.DistanceSquared(CameraPos, w.MyPivotPos) < w.System.HardPointAvMaxDistSqr;
                                    if (avWasEnabled != w.PlayTurretAv) w.StopBarrelAv = !w.PlayTurretAv;
                                }

                                ///
                                /// Check target for expire states
                                /// 

                                if (w.Target.HasTarget && !(IsClient && w.Target.CurrentState == States.Invalid))
                                {

                                    if (!IsClient && w.Target.Entity == null && w.Target.Projectile == null && (!comp.TrackReticle || gridAi.DummyTarget.ClearTarget))
                                        w.Target.Reset(Tick, States.Expired, !comp.TrackReticle);
                                    else if (!IsClient && w.Target.Entity != null && (comp.UserControlled || w.Target.Entity.MarkedForClose))
                                        w.Target.Reset(Tick, States.Expired);
                                    else if (!IsClient && w.Target.Projectile != null && (!gridAi.LiveProjectile.Contains(w.Target.Projectile) || w.Target.IsProjectile && w.Target.Projectile.State != Projectile.ProjectileState.Alive))
                                        w.Target.Reset(Tick, States.Expired);
                                    else if (w.AiEnabled)
                                    {

                                        w.UpdatePivotPos();
                                        if (!Weapon.TrackingTarget(w, w.Target) && !IsClient)
                                        {
                                            w.Target.Reset(Tick, States.Expired, !comp.TrackReticle);
                                        }
                                    }
                                    else
                                    {
                                        w.UpdatePivotPos();
                                        Vector3D targetPos;
                                        if (w.IsTurret)
                                        {

                                            if (!w.TrackTarget && !IsClient)
                                            {

                                                if ((comp.TrackingWeapon.Target.Projectile != w.Target.Projectile || w.Target.IsProjectile && w.Target.Projectile.State != Projectile.ProjectileState.Alive || comp.TrackingWeapon.Target.Entity != w.Target.Entity || comp.TrackingWeapon.Target.IsFakeTarget != w.Target.IsFakeTarget))
                                                    w.Target.Reset(Tick, States.Expired);
                                            }
                                            else if (!Weapon.TargetAligned(w, w.Target, out targetPos) && !IsClient)
                                                w.Target.Reset(Tick, States.Expired);
                                        }
                                        else if (w.TrackTarget && !Weapon.TargetAligned(w, w.Target, out targetPos) && !IsClient)
                                            w.Target.Reset(Tick, States.Expired);
                                    }
                                }
                                else if (w.Target.HasTarget && MyEntities.EntityExists(comp.WeaponValues.Targets[w.WeaponId].EntityId))
                                {
                                    w.Target.HasTarget = false;
                                    comp.Session.ClientGridResyncRequests.Add(comp);
                                }

                                w.ProjectilesNear = w.TrackProjectiles && !w.Target.HasTarget && (w.Target.TargetChanged || SCount == w.ShortLoadId && gridAi.LiveProjectile.Count > 0);

                                if (compCurPlayer.ControlType == ControlType.Camera && UiInput.MouseButtonPressed)
                                    w.Target.TargetPos = Vector3D.Zero;
                                
                                if (w.Target.TargetChanged)
                                {
                                    w.EventTriggerStateChanged(EventTriggers.Tracking, w.Target.HasTarget);
                                    w.EventTriggerStateChanged(EventTriggers.StopTracking, !w.Target.HasTarget);

                                    if (MpActive && IsServer && !w.Target.HasTarget) {
                                        w.Comp.WeaponValues.Targets[w.WeaponId].Info = TransferTarget.TargetInfo.Expired;

                                        PacketsToClient.Add(new PacketInfo {
                                            Entity = w.Comp.MyCube,
                                            Packet = new WeaponIdPacket {
                                                EntityId = comp.MyCube.EntityId,
                                                SenderId = 0,
                                                PType = PacketType.TargetExpireUpdate,
                                                WeaponId = w.WeaponId,
                                            }
                                        });
                                    }
                                }

                                ///
                                /// Queue for target acquire or set to tracking weapon.
                                /// 
                                w.SeekTarget = (!IsClient && !w.Target.HasTarget && w.TrackTarget && gridAi.TargetingInfo.TargetInRange && !comp.UserControlled) || comp.TrackReticle && !w.Target.IsFakeTarget;
                                if (!IsClient && (w.SeekTarget || w.TrackTarget && gridAi.TargetResetTick == Tick && !comp.UserControlled) && !w.AcquiringTarget && (compCurPlayer.ControlType == ControlType.None || compCurPlayer.ControlType == ControlType.Ui))
                                {
                                    w.AcquiringTarget = true;
                                    AcquireTargets.Add(w);
                                }
                                else if (w.IsTurret && !w.TrackTarget && !w.Target.HasTarget && gridAi.TargetingInfo.TargetInRange)
                                    w.Target = w.Comp.TrackingWeapon.Target;

                                ///
                                /// Check weapon's turret to see if its time to go home
                                /// 

                                if (w.TurretMode && (!w.Target.HasTarget && !w.ReturingHome && !w.IsHome && Tick - w.Target.ExpiredTick > 300) || comp.UserControlled != comp.WasControlled && !comp.UserControlled)
                                    w.TurretHomePosition(comp.WasControlled);

                                ///
                                /// Determine if its time to shoot
                                ///
                                /// 
                                w.AiShooting = (w.Target.TargetLock || w.System.DelayCeaseFire && !w.Target.IsAligned && Tick - w.CeaseFireDelayTick <= w.System.CeaseFireDelay) && !comp.UserControlled;
                                var reloading = (!w.ActiveAmmoDef.Const.EnergyAmmo || w.ActiveAmmoDef.Const.MustCharge) && (w.State.Sync.Reloading || w.OutOfAmmo);
                                var canShoot = !w.State.Sync.Overheated && !reloading && !w.System.DesignatorWeapon;
                                var fakeTarget = (overRides.TargetPainter || overRides.ManualControl) && comp.TrackReticle && w.Target.IsFakeTarget && w.Target.IsAligned;
                                var validShootStates = fakeTarget || w.State.ManualShoot == ShootOn || w.State.ManualShoot == ShootOnce || w.AiShooting && w.State.ManualShoot == ShootOff;

                                var manualShot = (compCurPlayer.ControlType == ControlType.Camera || overRides.ManualControl && comp.TrackReticle || w.State.ManualShoot == ShootClick) && !gridAi.SupressMouseShoot && (j % 2 == 0 && leftClick || j == 1 && rightClick);

                                if (canShoot && (validShootStates || manualShot || w.FinishBurst)) {

                                    if ((gridAi.AvailablePowerChanged || gridAi.RequestedPowerChanged || (w.RecalcPower && Tick60)) && !w.ActiveAmmoDef.Const.MustCharge) {

                                        if ((!gridAi.RequestIncrease || gridAi.PowerIncrease) && !Tick60)
                                            w.RecalcPower = true;
                                        else {
                                            w.RecalcPower = false;
                                            w.Timings.ChargeDelayTicks = 0;
                                        }
                                    }

                                    if (w.Timings.ChargeDelayTicks == 0 || w.Timings.ChargeUntilTick <= Tick) {

                                        if (!w.RequestedPower && !w.ActiveAmmoDef.Const.MustCharge) {
                                            gridAi.RequestedWeaponsDraw += w.RequiredPower;
                                            w.RequestedPower = true;
                                        }

                                        ShootingWeapons.Add(w);
                                    }
                                    else if (w.Timings.ChargeUntilTick > Tick && !w.ActiveAmmoDef.Const.MustCharge) {
                                        w.State.Sync.Charging = true;
                                        w.StopShooting(false, false);
                                    }
                                }
                                else if (w.IsShooting)  {

                                    if (!w.System.DelayCeaseFire || !w.Target.IsAligned && Tick - w.CeaseFireDelayTick > w.System.CeaseFireDelay) 
                                        w.StopShooting();
                                    else if (w.System.DelayCeaseFire && w.Target.IsAligned) 
                                        w.CeaseFireDelayTick = Tick;
                                }
                                else if (w.BarrelSpinning)
                                    w.SpinBarrel(true);

                                if (comp.Debug && !DedicatedServer)
                                    WeaponDebug(w);
                            }
                        }
                    }

                    gridAi.OverPowered = gridAi.RequestedWeaponsDraw > 0 && gridAi.RequestedWeaponsDraw > gridAi.GridMaxPower;
                }
            }

            if (DbCallBackComplete && DbsToUpdate.Count > 0 && DbTask.IsComplete)
                UpdateDbsInQueue();
        }

        private void UpdateChargeWeapons() //Fully Inlined due to keen's mod profiler
        {
            for (int i = ChargingWeapons.Count - 1; i >= 0; i--)
            {
                var w = ChargingWeapons[i];
                using (w.Comp.MyCube.Pin())
                using (w.Comp.Ai?.MyGrid.Pin())
                {
                    var comp = w.Comp;
                    var gridAi = comp.Ai;
                    if (comp == null || w.Comp.Ai == null || gridAi.MyGrid.MarkedForClose || gridAi.Concealed || !gridAi.HasPower || comp.MyCube.MarkedForClose || !w.Set.Enable || !comp.State.Value.Online || !comp.Set.Value.Overrides.Activate)
                    {
                        ChargingWeapons.RemoveAtFast(i);
                        if (ChargingWeaponsCheck.Contains(w))
                            ChargingWeaponsCheck.Remove(w);
                        continue;
                    }

                    if (comp.Platform.State != MyWeaponPlatform.PlatformState.Ready)
                        continue;

                    var wState = w.Comp.State.Value.Weapons[w.WeaponId];
                    var cState = w.Comp.State.Value;

                    if (Tick60 && w.DrawingPower)
                    {
                        if ((cState.CurrentCharge + w.UseablePower) < w.ActiveAmmoDef.Const.EnergyMagSize)
                        {
                            wState.Sync.CurrentCharge += w.UseablePower;
                            cState.CurrentCharge += w.UseablePower;

                        }
                        else
                        {
                            w.Comp.State.Value.CurrentCharge += (w.ActiveAmmoDef.Const.EnergyMagSize - wState.Sync.CurrentCharge);
                            wState.Sync.CurrentCharge = w.ActiveAmmoDef.Const.EnergyMagSize;
                        }
                    }

                    if (w.Timings.ChargeUntilTick <= Tick || !w.State.Sync.Reloading)
                    {
                        if (w.State.Sync.Reloading)
                            w.Reloaded();

                        if (w.DrawingPower)
                            w.StopPowerDraw();

                        w.Comp.Ai.OverPowered = w.Comp.Ai.RequestedWeaponsDraw > 0 && w.Comp.Ai.RequestedWeaponsDraw > w.Comp.Ai.GridMaxPower;
                        ChargingWeapons.RemoveAtFast(i);
                        if (ChargingWeaponsCheck.Contains(w))
                            ChargingWeaponsCheck.Remove(w);
                        continue;
                    }

                    if (!w.Comp.Ai.OverPowered)
                    {
                        if (!w.DrawingPower)
                        {
                            w.OldUseablePower = w.UseablePower;
                            w.UseablePower = w.RequiredPower;
                            w.DrawPower();
                            w.Timings.ChargeDelayTicks = 0;
                        }

                        continue;
                    }



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

                        w.Timings.ChargeDelayTicks = (uint)(((w.ActiveAmmoDef.Const.EnergyMagSize - wState.Sync.CurrentCharge) / w.UseablePower) * MyEngineConstants.UPDATE_STEPS_PER_SECOND);
                        w.Timings.ChargeUntilTick = w.Timings.ChargeDelayTicks + Tick;

                        if (!w.DrawingPower)
                            w.DrawPower();
                        else
                            w.DrawPower(true);
                    }
                }
            }
        }

        private void CheckAcquire()
        {
            for (int i = AcquireTargets.Count - 1; i >= 0; i--)
            {
                var w = AcquireTargets[i];
                using (w.Comp.MyCube.Pin())
                using (w.Comp.Ai?.MyGrid.Pin())
                {

                    var comp = w.Comp;
                    if (w.Comp.Ai == null || comp.Ai.MyGrid.MarkedForClose || !comp.Ai.HasPower || comp.Ai.Concealed || comp.MyCube.MarkedForClose || !comp.Ai.DbReady || !w.Set.Enable || !comp.State.Value.Online || !comp.Set.Value.Overrides.Activate) {
                        
                        w.AcquiringTarget = false;
                        AcquireTargets.RemoveAtFast(i);
                        continue;
                    }

                    var gridAi = w.Comp.Ai;
                    var sinceCheck = Tick - w.Target.CheckTick;
                    var seekProjectile = w.ProjectilesNear || w.TrackProjectiles && gridAi.CheckProjectiles;

                    var checkTime = w.Target.TargetChanged || sinceCheck > 239 || sinceCheck > 60 && Count == w.LoadId || seekProjectile;

                    if (checkTime || gridAi.TargetResetTick == Tick && w.Target.HasTarget)
                    {

                        if (seekProjectile || comp.TrackReticle || gridAi.TargetingInfo.TargetInRange && gridAi.TargetingInfo.ValidTargetExists(w))
                        {

                            if (comp.TrackingWeapon != null && comp.TrackingWeapon.System.DesignatorWeapon && comp.TrackingWeapon != w && comp.TrackingWeapon.Target.HasTarget)
                            {
                                var topMost = comp.TrackingWeapon.Target.Entity?.GetTopMostParent();
                                GridAi.AcquireTarget(w, false, topMost);
                            }
                            else
                                GridAi.AcquireTarget(w, gridAi.TargetResetTick == Tick);
                        }


                        if (w.Target.HasTarget || !gridAi.TargetingInfo.TargetInRange)
                        {

                            w.AcquiringTarget = false;
                            AcquireTargets.RemoveAtFast(i);
                            if (w.Target.HasTarget && MpActive)
                            {
                                w.Target.SyncTarget(comp.WeaponValues.Targets[w.WeaponId], w.WeaponId);
                                WeaponsToSync.Add(w);
                                w.Comp.Ai.NumSyncWeapons++;
                            }
                        }
                    }
                }
            }
        }

        private void ShootWeapons()
        {
            for (int i = ShootingWeapons.Count - 1; i >= 0; i--)
            {

                var w = ShootingWeapons[i];
                using (w.Comp.MyCube.Pin())
                using (w.Comp.Ai?.MyGrid.Pin())
                {
                    if (w.Comp.MyCube.MarkedForClose || w.Comp.Ai == null || w.Comp.Ai.Concealed || w.Comp.Ai.MyGrid.MarkedForClose || w.Comp.Platform.State != MyWeaponPlatform.PlatformState.Ready)
                    {

                        ShootingWeapons.RemoveAtFast(i);
                        continue;
                    }
                    //TODO add logic for power priority
                    if (w.Comp.Ai.OverPowered && (w.ActiveAmmoDef.Const.EnergyAmmo || w.ActiveAmmoDef.Const.IsHybrid) && !w.ActiveAmmoDef.Const.MustCharge)
                    {

                        if (w.Timings.ChargeDelayTicks == 0)
                        {
                            var percUseable = w.RequiredPower / w.Comp.Ai.RequestedWeaponsDraw;
                            w.OldUseablePower = w.UseablePower;
                            w.UseablePower = (w.Comp.Ai.GridMaxPower * .98f) * percUseable;

                            if (w.DrawingPower)
                                w.DrawPower(true);
                            else
                                w.DrawPower();

                            w.Timings.ChargeDelayTicks = (uint)(((w.RequiredPower - w.UseablePower) / w.UseablePower) * MyEngineConstants.UPDATE_STEPS_PER_SECOND);
                            w.Timings.ChargeUntilTick = Tick + w.Timings.ChargeDelayTicks;
                            w.State.Sync.Charging = true;
                        }
                        else if (w.Timings.ChargeUntilTick <= Tick)
                        {

                            w.State.Sync.Charging = false;
                            w.Timings.ChargeUntilTick = Tick + w.Timings.ChargeDelayTicks;
                        }
                    }
                    else if (!w.ActiveAmmoDef.Const.MustCharge && (w.State.Sync.Charging || w.Timings.ChargeDelayTicks > 0 || w.ResetPower))
                    {

                        w.OldUseablePower = w.UseablePower;
                        w.UseablePower = w.RequiredPower;
                        w.DrawPower(true);
                        w.Timings.ChargeDelayTicks = 0;
                        w.State.Sync.Charging = false;
                        w.ResetPower = false;
                    }

                    if (w.State.Sync.Charging)
                        continue;

                    if (w.Timings.ShootDelayTick <= Tick) w.Shoot();
                }
            }
            ShootingWeapons.Clear();
        }
    }
}