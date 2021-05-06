using System.Collections.Generic;
using CoreSystems.Platform;
using CoreSystems.Projectiles;
using CoreSystems.Support;
using VRage.Game;
using VRageMath;
using static CoreSystems.Support.Target;
using static CoreSystems.Support.CoreComponent.Start;
using static CoreSystems.Support.CoreComponent.TriggerActions;
using static CoreSystems.Support.WeaponDefinition.HardPointDef.HardwareDef;
using static CoreSystems.ProtoWeaponState;
namespace CoreSystems
{
    public partial class Session
    {
        private void AiLoop()
        { //Fully Inlined due to keen's mod profiler
            foreach (var ai in GridAIs.Values)
            {
                ///
                /// GridAi update section
                ///
                ai.MyProjectiles = 0;
                ai.ProInMinCacheRange = 0;
                ai.AccelChecked = false;

                if (ai.MarkedForClose || !ai.GridInit || ai.TopEntity == null || ai.Construct.RootAi == null || ai.TopEntity.MarkedForClose)
                    continue;

                ai.Concealed = ((uint)ai.TopEntity.Flags & 4) > 0;
                if (ai.Concealed)
                    continue;

                if (!ai.ScanInProgress && Tick - ai.TargetsUpdatedTick > 100 && DbTask.IsComplete)
                    ai.RequestDbUpdate();

                if (ai.DeadProjectiles.Count > 0) {
                    for (int i = 0; i < ai.DeadProjectiles.Count; i++) ai.LiveProjectile.Remove(ai.DeadProjectiles[i]);
                    ai.DeadProjectiles.Clear();
                    ai.LiveProjectileTick = Tick;
                }
                var enemyProjectiles = ai.LiveProjectile.Count > 0;
                ai.CheckProjectiles = Tick - ai.NewProjectileTick <= 1;

                if (ai.IsGrid && (ai.UpdatePowerSources || !ai.HadPower && ai.GridEntity.IsPowered || ai.HasPower && !ai.GridEntity.IsPowered || Tick10))
                    ai.UpdateGridPower();

                if (!ai.HasPower || Settings.Enforcement.ServerSleepSupport && IsServer && ai.AwakeComps == 0 && ai.WeaponsTracking == 0 && ai.SleepingComps > 0 && !ai.CheckProjectiles && ai.AiSleep && !ai.DbUpdated) 
                    continue;
                
                if (Tick60 && ai.IsGrid && ai.BlockChangeArea != BoundingBox.Invalid)
                {
                    ai.BlockChangeArea.Min *= ai.GridEntity.GridSize;
                    ai.BlockChangeArea.Max *= ai.GridEntity.GridSize;
                }

                if (IsServer) {

                    if (ai.Construct.RootAi.Construct.NewInventoryDetected)
                        ai.Construct.RootAi.Construct.CheckForMissingAmmo();
                    else if (Tick60 && ai.Construct.RootAi.Construct.RecentItems.Count > 0)
                        ai.Construct.RootAi.Construct.CheckEmptyWeapons();
                }

                ///
                /// Upgrade update section
                ///
                for (int i = 0; i < ai.UpgradeComps.Count; i++)
                {
                    var uComp = ai.UpgradeComps[i];
                    if (uComp.Status != Started)
                        uComp.HealthCheck();

                    if (ai.DbUpdated || !uComp.UpdatedState)
                    {
                        uComp.DetectStateChanges();
                    }

                    if (uComp.Platform.State != CorePlatform.PlatformState.Ready || uComp.IsAsleep || !uComp.IsWorking || uComp.CoreEntity.MarkedForClose || uComp.IsDisabled || uComp.LazyUpdate && !ai.DbUpdated && Tick > uComp.NextLazyUpdateStart)
                        continue;

                    for (int j = 0; j < uComp.Platform.Upgrades.Count; j++)
                    {
                        var u = uComp.Platform.Upgrades[j];
                    }
                }
                ///
                /// Support update section
                ///
                for (int i = 0; i < ai.SupportComps.Count; i++)
                {
                    var sComp = ai.SupportComps[i];
                    if (sComp.Status != Started)
                        sComp.HealthCheck();

                    if (ai.DbUpdated || !sComp.UpdatedState)
                    {
                        sComp.DetectStateChanges();
                    }

                    if (sComp.Platform.State != CorePlatform.PlatformState.Ready || sComp.IsAsleep || !sComp.IsWorking || sComp.CoreEntity.MarkedForClose || sComp.IsDisabled || !Tick60)
                        continue;

                    for (int j = 0; j < sComp.Platform.Support.Count; j++)
                    {
                        var s = sComp.Platform.Support[j];
                        if (s.LastBlockRefreshTick < ai.LastBlockChangeTick && s.IsPrime || s.LastBlockRefreshTick < ai.LastBlockChangeTick && !sComp.Structure.CommonBlockRange)
                            s.RefreshBlocks();

                        if (s.ShowAffectedBlocks != sComp.Data.Repo.Values.Set.Overrides.ArmorShowArea)
                            s.ToggleAreaEffectDisplay();

                        if (s.Active)
                            s.Charge();
                    }
                }

                ///
                /// Phantom update section
                ///
                for (int i = 0; i < ai.PhantomComps.Count; i++)
                {
                    var pComp = ai.PhantomComps[i];
                    if (pComp.Status != Started)
                        pComp.HealthCheck();

                    if (pComp.Platform.State != CorePlatform.PlatformState.Ready || pComp.IsAsleep || !pComp.IsWorking || pComp.CoreEntity.MarkedForClose || pComp.IsDisabled)
                        continue;

                    if (ai.DbUpdated || !pComp.UpdatedState)
                    {
                        //pComp.DetectStateChanges();
                    }

                    ///
                    /// Upgrade update section
                    ///
                    for (int j = 0; j < pComp.Platform.Phantoms.Count; j++)
                    {
                        var u = pComp.Platform.Phantoms[j];
                    }
                }

                ///
                /// WeaponComp update section
                ///
                for (int i = 0; i < ai.WeaponComps.Count; i++) {

                    var wComp = ai.WeaponComps[i];
                    if (wComp.Status != Started)
                        wComp.HealthCheck();

                    if (ai.DbUpdated || !wComp.UpdatedState) {

                        wComp.DetectStateChanges();
                    }

                    if (wComp.Platform.State != CorePlatform.PlatformState.Ready || wComp.IsAsleep || !wComp.IsWorking || wComp.CoreEntity.MarkedForClose || wComp.IsDisabled || wComp.LazyUpdate && !ai.DbUpdated && Tick > wComp.NextLazyUpdateStart) 
                        continue;

                    if (IsServer && wComp.Data.Repo.Values.State.PlayerId > 0 && !ai.Data.Repo.ControllingPlayers.ContainsKey(wComp.Data.Repo.Values.State.PlayerId))
                        wComp.ResetPlayerControl();

                    if (HandlesInput) {
                        var wasTrack = wComp.Data.Repo.Values.State.TrackingReticle;

                        var isControllingPlayer = wComp.Data.Repo.Values.State.PlayerId == PlayerId;
                        var track = (isControllingPlayer && (wComp.Data.Repo.Values.Set.Overrides.Control != ProtoWeaponOverrides.ControlModes.Auto) && TargetUi.DrawReticle && !InMenu && wComp.Ai.Construct.RootAi.Data.Repo.ControllingPlayers.ContainsKey(PlayerId));
                        if (!MpActive && IsServer)
                            wComp.Data.Repo.Values.State.TrackingReticle = track;

                        var needsUpdate = MpActive && track != wasTrack && (wComp.Data.Repo.Values.State.PlayerId <= 0 || isControllingPlayer);
                        if (needsUpdate)
                            wComp.Session.SendTrackReticleUpdate(wComp, track);
                    }
                    var trackReticle = wComp.Data.Repo.Values.State.TrackingReticle;
                    wComp.WasControlled = wComp.UserControlled;
                    wComp.UserControlled = wComp.Data.Repo.Values.State.Control != ControlMode.None;

                    if (!PlayerMouseStates.TryGetValue(wComp.Data.Repo.Values.State.PlayerId, out wComp.InputState))
                        wComp.InputState = DefaultInputStateData;
                    var compManualMode = wComp.Data.Repo.Values.State.Control == ControlMode.Camera || (wComp.Data.Repo.Values.Set.Overrides.Control == ProtoWeaponOverrides.ControlModes.Manual && trackReticle);
                    var canManualShoot = !ai.SuppressMouseShoot && !wComp.InputState.InMenu;

                    ///
                    /// Weapon update section
                    ///
                    for (int j = 0; j < wComp.Platform.Weapons.Count; j++) {

                        var w = wComp.Platform.Weapons[j];
                        var comp = w.Comp;

                        if (w.PartReadyTick > Tick) {

                            if (w.Target.HasTarget && !IsClient)
                                w.Target.Reset(comp.Session.Tick, States.WeaponNotReady);
                            continue;
                        }

                        if (w.AvCapable && Tick20) {
                            var avWasEnabled = w.PlayTurretAv;
                            double distSqr;
                            Vector3D.DistanceSquared(ref CameraPos, ref w.MyPivotPos, out distSqr);
                            w.PlayTurretAv = distSqr < w.System.HardPointAvMaxDistSqr;
                            if (avWasEnabled != w.PlayTurretAv) w.StopBarrelAvTick = Tick;
                        }

                        if (!ai.HadPower && w.ActiveAmmoDef.AmmoDef.Const.MustCharge && w.PartState.Action != TriggerOff) {

                            if (IsServer) {
                                w.PartState.WeaponMode(comp, TriggerOff);
                                w.ProtoWeaponAmmo.CurrentAmmo = 0;
                            }

                            w.FinishBurst = false;

                            if (w.IsShooting)
                                w.StopShooting();
                        }

                        ///
                        ///Check Reload
                        ///                        

                        if (w.ActiveAmmoDef.AmmoDef.Const.Reloadable && !w.System.DesignatorWeapon && !w.Loading) { // does this need StayCharged?

                            if (IsServer && (w.ProtoWeaponAmmo.CurrentAmmo == 0 || w.CheckInventorySystem))
                                w.ComputeServerStorage();
                            else if (IsClient) {

                                if (w.ClientReloading && w.Reload.EndId > w.ClientEndId && w.Reload.StartId == w.ClientStartId)
                                    w.Reloaded();
                                else 
                                    w.ClientReload();
                            }
                        }


                        ///
                        /// Update Weapon Hud Info
                        /// 
                        var isWaitingForBurstDelay = w.ShowBurstDelayAsReload && !w.Loading && w.ShootTick > Tick && w.ShootTick >= w.LastShootTick + w.System.Values.HardPoint.Loading.DelayAfterBurst;
                        if (HandlesInput && (w.Loading || w.HeatPerc >= 0.01 || isWaitingForBurstDelay) && Tick - w.LastLoadedTick > 30 && !Session.Config.MinimalHud && ActiveControlBlock != null && ai.SubGrids.Contains(ActiveControlBlock.CubeGrid)) {
                            HudUi.TexturesToAdd++;
                            HudUi.WeaponsToDisplay.Add(w);
                        }

                        if (w.System.PartType != HardwareType.BlockWeapon)
                            continue;

                        if (w.Target.ClientDirty)
                            w.Target.ClientUpdate(w, w.TargetData);

                        ///
                        /// Check target for expire states
                        /// 
                        bool targetLock = false;
                        var noAmmo =  w.NoMagsToLoad && w.ProtoWeaponAmmo.CurrentAmmo == 0 && w.ActiveAmmoDef.AmmoDef.Const.Reloadable && !w.System.DesignatorWeapon && Tick - w.LastMagSeenTick > 600;
                        if (w.Target.HasTarget) {

                            if (w.PosChangedTick != Tick) w.UpdatePivotPos();
                            if (!IsClient && noAmmo)
                                w.Target.Reset(Tick, States.Expired);
                            else if (!IsClient && w.Target.TargetEntity == null && w.Target.Projectile == null && (!trackReticle || Tick - PlayerDummyTargets[comp.Data.Repo.Values.State.PlayerId].LastUpdateTick > 120))
                                w.Target.Reset(Tick, States.Expired, !trackReticle);
                            else if (!IsClient && w.Target.TargetEntity != null && (comp.UserControlled && !w.System.SuppressFire || w.Target.TargetEntity.MarkedForClose))
                                w.Target.Reset(Tick, States.Expired);
                            else if (!IsClient && w.Target.Projectile != null && (!ai.LiveProjectile.Contains(w.Target.Projectile) || w.Target.IsProjectile && w.Target.Projectile.State != Projectile.ProjectileState.Alive))
                            {
                                w.Target.Reset(Tick, States.Expired);
                                w.FastTargetResetTick = Tick + 6;
                            }
                            else if (w.AiEnabled) {

                                if (!Weapon.TrackingTarget(w, w.Target, out targetLock) && !IsClient && w.Target.ExpiredTick != Tick)
                                    w.Target.Reset(Tick, States.LostTracking, !trackReticle && (w.Target.CurrentState != States.RayCheckFailed && !w.Target.HasTarget));
                            }
                            else {

                                Vector3D targetPos;
                                if (w.IsTurret) {

                                    if (!w.TrackTarget && !IsClient) {

                                        if ((comp.TrackingWeapon.Target.Projectile != w.Target.Projectile || w.Target.IsProjectile && w.Target.Projectile.State != Projectile.ProjectileState.Alive || comp.TrackingWeapon.Target.TargetEntity != w.Target.TargetEntity || comp.TrackingWeapon.Target.IsFakeTarget != w.Target.IsFakeTarget))
                                            w.Target.Reset(Tick, States.Expired);
                                        else
                                            targetLock = true;
                                    }
                                    else if (!Weapon.TargetAligned(w, w.Target, out targetPos) && !IsClient)
                                        w.Target.Reset(Tick, States.Expired);
                                }
                                else if (w.TrackTarget && !Weapon.TargetAligned(w, w.Target, out targetPos) && !IsClient)
                                    w.Target.Reset(Tick, States.Expired);
                            }
                        }

                        w.ProjectilesNear = enemyProjectiles && w.System.TrackProjectile && w.Comp.Data.Repo.Values.Set.Overrides.Projectiles && !w.Target.HasTarget && (w.Target.TargetChanged || QCount == w.ShortLoadId );

                        if (comp.Data.Repo.Values.State.Control == ControlMode.Camera && UiInput.MouseButtonPressed)
                            w.Target.TargetPos = Vector3D.Zero;

                        ///
                        /// Queue for target acquire or set to tracking weapon.
                        /// 

                        var seek = trackReticle && !w.Target.IsFakeTarget || (!noAmmo && !w.Target.HasTarget && w.TrackTarget && (comp.DetectOtherSignals && ai.DetectionInfo.OtherInRange || ai.DetectionInfo.PriorityInRange) && (!comp.UserControlled && !Settings.Enforcement.DisableAi || w.PartState.Action == TriggerClick));
                        if (!IsClient && (seek || w.TrackTarget && ai.TargetResetTick == Tick && !comp.UserControlled && !Settings.Enforcement.DisableAi) && !w.AcquiringTarget && (comp.Data.Repo.Values.State.Control == ControlMode.None || comp.Data.Repo.Values.State.Control == ControlMode.Ui))
                        {
                            w.AcquiringTarget = true;
                            AcquireTargets.Add(w);
                        }

                        if (w.Target.TargetChanged) // Target changed
                            w.TargetChanged();

                        ///
                        /// Check weapon's turret to see if its time to go home
                        ///

                        if (w.TurretMode && !w.IsHome && !w.ReturingHome && !w.Target.HasTarget && Tick - w.Target.ResetTick > 239 && !comp.UserControlled && w.PartState.Action == TriggerOff)
                            w.ScheduleWeaponHome();

                        ///
                        /// Determine if its time to shoot
                        ///
                        ///
                        w.AiShooting = targetLock && !comp.UserControlled && !w.System.SuppressFire;
                        var ammoEmpty = w.ActiveAmmoDef.AmmoDef.Const.Reloadable && w.ClientMakeUpShots == 0 && w.ProtoWeaponAmmo.CurrentAmmo == 0 && !(w.Loading && w.System.ReloadTime == 0);
                        var canShoot = !w.PartState.Overheated  && !w.System.DesignatorWeapon && (!w.LastEventCanDelay || w.AnimationDelayTick <= Tick || w.ClientMakeUpShots > 0);
                        var fakeTarget = comp.Data.Repo.Values.Set.Overrides.Control == ProtoWeaponOverrides.ControlModes.Painter && trackReticle && w.Target.IsFakeTarget && w.Target.IsAligned;
                        var validShootStates = fakeTarget || w.PartState.Action == TriggerOn || w.AiShooting && w.PartState.Action == TriggerOff;
                        var manualShot = (compManualMode || w.PartState.Action == TriggerClick) && canManualShoot && comp.InputState.MouseButtonLeft;
                        var delayedFire = w.System.DelayCeaseFire && !w.Target.IsAligned && Tick - w.CeaseFireDelayTick <= w.System.CeaseFireDelay;
                        var shoot = (validShootStates || manualShot || w.FinishBurst || delayedFire);
                        w.LockOnFireState = !shoot && w.System.LockOnFocus && ai.Construct.Data.Repo.FocusData.HasFocus && ai.Construct.Focus.FocusInRange(w);
                        var weaponPrimed  = canShoot && (shoot || w.LockOnFireState);
                        
                        var shotReady = weaponPrimed && !ammoEmpty;

                        if ((shotReady || w.ShootOnce) && ai.CanShoot) {

                            if (w.ShootOnce && IsServer && (shotReady || w.PartState.Action != TriggerOnce))
                                w.ShootOnce = false;

                            if (MpActive && HandlesInput && !ManualShot)
                                ManualShot = !validShootStates && !w.FinishBurst && !delayedFire;

                            if (w.System.DelayCeaseFire && (validShootStates || manualShot || w.FinishBurst))
                                w.CeaseFireDelayTick = Tick;

                            ShootingWeapons.Add(w);
                        }
                        else {

                            if (w.IsShooting)
                                w.StopShooting();

                            if (w.BarrelSpinning) {
                                var spinDown = !(weaponPrimed && ai.CanShoot && w.System.Values.HardPoint.Loading.SpinFree);
                                w.SpinBarrel(spinDown);
                            }
                        }

                        if (comp.Debug && !DedicatedServer)
                            WeaponDebug(w);
                    }
                }
                
                if (ai.IsGrid && Tick60 && ai.BlockChangeArea != BoundingBox.Invalid) {
                    ai.BlockChangeArea = BoundingBox.CreateInvalid();
                    ai.AddedBlockPositions.Clear();
                    ai.RemovedBlockPositions.Clear();
                }
                ai.DbUpdated = false;
            }

            if (DbTask.IsComplete && DbsToUpdate.Count > 0 && !DbUpdating)
                UpdateDbsInQueue();
        }

        private void CheckAcquire()
        {
            for (int i = AcquireTargets.Count - 1; i >= 0; i--)
            {
                var w = AcquireTargets[i];
                var comp = w.Comp;
                if (w.BaseComp.IsAsleep || w.BaseComp.Ai == null || comp.Ai.TopEntity.MarkedForClose || !comp.Ai.HasPower || comp.Ai.Concealed || comp.CoreEntity.MarkedForClose || !comp.Ai.DbReady || !comp.IsWorking  || w.NoMagsToLoad && w.ProtoWeaponAmmo.CurrentAmmo == 0 && Tick - w.LastMagSeenTick > 600) {
                    
                    w.AcquiringTarget = false;
                    AcquireTargets.RemoveAtFast(i);
                    continue;
                }

                if (!w.Acquire.Monitoring && IsServer && w.TrackTarget)
                    AcqManager.Monitor(w.Acquire);

                var acquire = (w.Acquire.IsSleeping && AsleepCount == w.Acquire.SlotId || !w.Acquire.IsSleeping && AwakeCount == w.Acquire.SlotId);

                var seekProjectile = w.ProjectilesNear || w.System.TrackProjectile && w.Comp.Data.Repo.Values.Set.Overrides.Projectiles && w.BaseComp.Ai.CheckProjectiles;
                var checkTime = w.Target.TargetChanged || acquire || seekProjectile || w.FastTargetResetTick == Tick;

                if (checkTime || w.BaseComp.Ai.TargetResetTick == Tick && w.Target.HasTarget) {

                    if (seekProjectile || comp.Data.Repo.Values.State.TrackingReticle || (comp.DetectOtherSignals && w.BaseComp.Ai.DetectionInfo.OtherInRange || w.BaseComp.Ai.DetectionInfo.PriorityInRange) && w.BaseComp.Ai.DetectionInfo.ValidSignalExists(w)) {
                        if (comp.TrackingWeapon != null && comp.TrackingWeapon.System.DesignatorWeapon && comp.TrackingWeapon != w && comp.TrackingWeapon.Target.HasTarget) {

                            var topMost = comp.TrackingWeapon.Target.TargetEntity?.GetTopMostParent();
                            Ai.AcquireTarget(w, false, topMost);
                        }
                        else
                        {
                            Ai.AcquireTarget(w, w.BaseComp.Ai.TargetResetTick == Tick);
                        }
                    }

                    if (w.Target.HasTarget || !(comp.DetectOtherSignals && w.BaseComp.Ai.DetectionInfo.OtherInRange || w.BaseComp.Ai.DetectionInfo.PriorityInRange)) {

                        w.AcquiringTarget = false;
                        AcquireTargets.RemoveAtFast(i);
                        if (w.Target.HasTarget && MpActive) {
                            w.Target.PushTargetToClient(w);
                        }
                    }
                }
            }
        }

        private void ShootWeapons()
        {
            for (int i = ShootingWeapons.Count - 1; i >= 0; i--) {

                var w = ShootingWeapons[i];
                var invalidWeapon = w.Comp.CoreEntity.MarkedForClose || w.Comp.Ai == null || w.Comp.Ai.Concealed || w.Comp.Ai.TopEntity.MarkedForClose || w.Comp.Platform.State != CorePlatform.PlatformState.Ready;
                var smartTimer = !w.AiEnabled && w.ActiveAmmoDef.AmmoDef.Trajectory.Guidance == WeaponDefinition.AmmoDef.TrajectoryDef.GuidanceType.Smart && Tick - w.LastSmartLosCheck > 180;
                var quickSkip = invalidWeapon || w.Comp.IsBlock && smartTimer && !w.SmartLos() || w.PauseShoot || w.ProtoWeaponAmmo.CurrentAmmo == 0;
                if (quickSkip) continue;

                w.Shoot();
            }
            ShootingWeapons.Clear();
        }
    }
}