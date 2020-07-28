﻿using System;
using System.Collections.Generic;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRageMath;
using WeaponCore.Support;
using static WeaponCore.Support.PartAnimation;
using static WeaponCore.Support.WeaponDefinition.AnimationDef.PartAnimationSetDef;
namespace WeaponCore.Platform
{
    public partial class Weapon
    {

        internal void PositionChanged(MyPositionComponentBase pComp)
        {
            try
            {
                if (PosChangedTick != Comp.Session.Tick)
                    UpdatePivotPos();

                if (Comp.UserControlled)
                    IsHome = false;
            }
            catch (Exception ex) { Log.Line($"Exception in PositionChanged: {ex}"); }
        }

        internal void TargetChanged()
        {
            EventTriggerStateChanged(EventTriggers.Tracking, Target.HasTarget);
            EventTriggerStateChanged(EventTriggers.StopTracking, !Target.HasTarget);

            if (!Target.HasTarget)
            {
                if (!Acquire.Enabled)
                    System.Session.AcqManager.AddAwake(Acquire);

                if (Comp.Session.MpActive && Comp.Session.IsServer)  {
                    TargetData.ClearTarget();
                    if (!Comp.Data.Repo.State.TrackingReticle)
                        Comp.Session.SendTargetExpiredUpdate(Comp, WeaponId);
                } 
            }

            Target.TargetChanged = false;
        }

        internal void EntPartClose(MyEntity obj)
        {
            obj.PositionComp.OnPositionChanged -= PositionChanged;
            obj.OnMarkForClose -= EntPartClose;
        }

        internal void DelayedStart(object o)
        {
            EventTriggerStateChanged(EventTriggers.TurnOff, true);
        }

        internal void EventTriggerStateChanged(EventTriggers state, bool active, HashSet<string> muzzles = null)
        {
            if (Comp?.Data.Repo == null || Comp.MyCube == null || Comp.MyCube.MarkedForClose || Comp.Ai == null || Comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return;
            try
            {
                var session = Comp.Session;
                var distance = Vector3D.DistanceSquared(session.CameraPos, MyPivotPos);
                var canPlay = !session.DedicatedServer && 64000000 >= distance; //8km max range, will play regardless of range if it moves PivotPos and is loaded

                if (canPlay)
                    PlayParticleEvent(state, active, distance, muzzles);

                if (!AnimationsSet.ContainsKey(state)) return;
                if (AnimationDelayTick < Comp.Session.Tick)
                    AnimationDelayTick = Comp.Session.Tick;

                var set = false;

                switch (state)
                {
                    case EventTriggers.StopFiring:
                    case EventTriggers.PreFire:
                    case EventTriggers.Firing:
                        {
                            var addToFiring = AnimationsSet.ContainsKey(EventTriggers.StopFiring) && state == EventTriggers.Firing;

                            for (int i = 0; i < AnimationsSet[state].Length; i++)
                            {
                                var animation = AnimationsSet[state][i];

                                if (active && !animation.Running && (animation.Muzzle == "Any" || (muzzles != null && muzzles.Contains(animation.Muzzle))))
                                {
                                    if (animation.TriggerOnce && animation.Triggered) continue;
                                    animation.Triggered = true;

                                    set = true;

                                    if (animation.Muzzle != "Any" && addToFiring) _muzzlesFiring.Add(animation.Muzzle);

                                    animation.StartTick = session.Tick + animation.MotionDelay;
                                    if(state == EventTriggers.StopFiring)
                                        animation.StartTick += (AnimationDelayTick - session.Tick);

                                    Comp.Session.AnimationsToProcess.Add(animation);
                                    animation.Running = true;
                                    animation.CanPlay = canPlay;

                                    if (animation.DoesLoop)
                                        animation.Looping = true;
                                }
                                else if (active && animation.DoesLoop)
                                    animation.Looping = true;
                                else if (!active)
                                {
                                    animation.Looping = false;
                                    animation.Triggered = false;
                                }
                            }
                            if (active && state == EventTriggers.StopFiring)
                                _muzzlesFiring.Clear();
                            break;
                        }
                    case EventTriggers.StopTracking:
                    case EventTriggers.Tracking:
                        {
                            for (int i = 0; i < AnimationsSet[state].Length; i++)
                            {
                                var animation = AnimationsSet[state][i];
                                if (active && !animation.Running)
                                {
                                    if (animation.TriggerOnce && animation.Triggered) continue;

                                    set = true;
                                    animation.Triggered = true;
                                    animation.Running = true;
                                    animation.CanPlay = canPlay;
                                    
                                    Comp.Session.AnimationsToProcess.Add(animation);
                                    animation.StartTick = session.Tick + animation.MotionDelay;

                                    if (LastEvent == EventTriggers.StopTracking || LastEvent == EventTriggers.Tracking)
                                        animation.StartTick += (AnimationDelayTick - session.Tick);

                                    if (animation.DoesLoop)
                                        animation.Looping = true;
                                }
                                else if (active && animation.DoesLoop)
                                    animation.Looping = true;
                                else if (!active)
                                {
                                    animation.Looping = false;
                                    animation.Triggered = false;
                                }
                            }
                            break;
                        }
                    case EventTriggers.TurnOn:
                    case EventTriggers.TurnOff:
                        if (active)
                        {
                            for (int i = 0; i < AnimationsSet[state].Length; i++)
                            {
                                var animation = AnimationsSet[state][i];
                                if (!animation.Running)
                                {
                                    animation.Running = true;
                                    animation.CanPlay = true;

                                    animation.StartTick = session.Tick + animation.MotionDelay + (AnimationDelayTick - session.Tick);
                                    if (state == EventTriggers.TurnOff) animation.StartTick += OffDelay;

                                    session.ThreadedAnimations.Enqueue(animation);
                                }
                                else
                                    animation.Reverse = false;
                            }
                        }
                        break;
                    case EventTriggers.EmptyOnGameLoad:
                    case EventTriggers.Overheated:
                    case EventTriggers.NoMagsToLoad:
                    case EventTriggers.BurstReload:
                    case EventTriggers.Reloading:
                        {
                            for (int i = 0; i < AnimationsSet[state].Length; i++)
                            {
                                var animation = AnimationsSet[state][i];
                                if (animation == null) continue;

                                if (active && !animation.Running)
                                {
                                    if (animation.TriggerOnce && animation.Triggered) continue;
                                    animation.Triggered = true;

                                    set = true;

                                    animation.StartTick = session.Tick + animation.MotionDelay;
                                    session?.ThreadedAnimations?.Enqueue(animation);

                                    animation.Running = true;
                                    animation.CanPlay = canPlay;

                                    if (animation.DoesLoop)
                                        animation.Looping = true;
                                }
                                else if (active && animation.DoesLoop)
                                    animation.Looping = true;
                                else if (!active)
                                {
                                    animation.Looping = false;
                                    animation.Triggered = false;
                                }
                            }

                            break;
                        }
                }
                if (active && set)
                {
                    var animationLength = 0u;

                    LastEvent = state;
                    LastEventCanDelay = state == EventTriggers.Reloading || state == EventTriggers.StopFiring || state == EventTriggers.TurnOff || state == EventTriggers.TurnOn;

                    if (System.WeaponAnimationLengths.TryGetValue(state, out animationLength))
                        AnimationDelayTick += animationLength;
                }
            }
            catch (Exception e)
            {
                Log.Line($"Exception in Event Triggered: {e}");
            }
        }

        internal void PlayParticleEvent(EventTriggers eventTrigger, bool active, double distance, HashSet<string> muzzles)
        {
            if (ParticleEvents.ContainsKey(eventTrigger))
            {
                for (int i = 0; i < ParticleEvents[eventTrigger].Length; i++)
                {
                    var particle = ParticleEvents[eventTrigger][i];

                    if(active && particle.Restart && particle.Triggered) continue;

                    var obb = particle.MyDummy.Entity.PositionComp.WorldAABB;
                    //var inView = Comp.Session.Camera.IsInFrustum(ref obb);

                    var canPlay = true;
                    if (muzzles != null)
                    {
                        for (int j = 0; j < particle.MuzzleNames.Length; j++)
                        {
                            if (particle.MuzzleNames[j] == "Any" || muzzles.Contains(particle.MuzzleNames[j]))
                            {
                                canPlay = true;
                                break;
                            }
                        }
                    }
                    else
                        canPlay = true;

                    if (!canPlay) return;

                    if (active && !particle.Playing && distance <= particle.Distance)
                    {
                        particle.PlayTick = Comp.Session.Tick + particle.StartDelay;
                        Comp.Session.Av.ParticlesToProcess.Add(particle);
                        particle.Playing = true;
                        particle.Triggered = true;
                    }
                    else if (!active)
                    {
                        if(particle.Playing)
                            particle.Stop = true;

                        particle.Triggered = false;
                    }
                }
            }
        }

        internal void UpdateRequiredPower()
        {
            if (ActiveAmmoDef.AmmoDef.Const.EnergyAmmo || ActiveAmmoDef.AmmoDef.Const.IsHybrid)
            {
                var rofPerSecond = RateOfFire / MyEngineConstants.UPDATE_STEPS_PER_SECOND;
                RequiredPower = ((ShotEnergyCost * (rofPerSecond * MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS)) * System.Values.HardPoint.Loading.BarrelsPerShot) * System.Values.HardPoint.Loading.TrajectilesPerBarrel;
            }
            else
                RequiredPower = Comp.IdlePower;
        }

        internal void UpdateShotEnergy()
        {
            var ewar = (int)ActiveAmmoDef.AmmoDef.AreaEffect.AreaEffect > 3;
            ShotEnergyCost = ewar ? ActiveAmmoDef.AmmoDef.EnergyCost * ActiveAmmoDef.AmmoDef.Const.AreaEffectDamage : ActiveAmmoDef.AmmoDef.EnergyCost * BaseDamage;
        }

        internal void UpdateBarrelRotation()
        {
            const int loopCnt = 10;
            var interval = (3600f / System.BarrelSpinRate) * ((float)Math.PI / _numModelBarrels);
            var steps = (360f / _numModelBarrels) / interval;

            _ticksBeforeSpinUp = (uint)interval / loopCnt;
            for (int i = 0; i < loopCnt; i++)
            {

                var multi = (float)(i + 1) / loopCnt;
                var angle = MathHelper.ToRadians(steps * multi);

                switch (System.Values.HardPoint.Other.RotateBarrelAxis)
                {

                    case 1:
                        BarrelRotationPerShot[i] = MuzzlePart.ToTransformation * Matrix.CreateRotationX(angle) * MuzzlePart.FromTransformation;
                        break;
                    case 2:
                        BarrelRotationPerShot[i] = MuzzlePart.ToTransformation * Matrix.CreateRotationY(angle) * MuzzlePart.FromTransformation;
                        break;
                    case 3:
                        BarrelRotationPerShot[i] = MuzzlePart.ToTransformation * Matrix.CreateRotationZ(angle) * MuzzlePart.FromTransformation;
                        break;
                }
            }
        }

        public void StartShooting()
        {
            if (FiringEmitter != null) StartFiringSound();
            if (!IsShooting && !System.DesignatorWeapon)
            {
                EventTriggerStateChanged(EventTriggers.StopFiring, false);
                Comp.CurrentDps += Dps;
                if ((ActiveAmmoDef.AmmoDef.Const.EnergyAmmo || ActiveAmmoDef.AmmoDef.Const.IsHybrid) && !ActiveAmmoDef.AmmoDef.Const.MustCharge && !Comp.UnlimitedPower && !DrawingPower)
                    DrawPower();

            }
            IsShooting = true;
        }

        public void StopShooting(bool avOnly = false, bool power = true)
        {
            if (System.Values.HardPoint.Audio.FireSoundEndDelay > 0)
                Comp.Session.FutureEvents.Schedule(StopFiringSound, false, System.Values.HardPoint.Audio.FireSoundEndDelay);
            else StopFiringSound(false);

            StopPreFiringSound(false);
            if (AvCapable && RotateEmitter != null && RotateEmitter.IsPlaying) StopRotateSound();
            CeaseFireDelayTick = uint.MaxValue / 2;
            FireCounter = 0;
            if (!power || avOnly) StopRotateSound();
            for (int i = 0; i < Muzzles.Length; i++)
            {
                var muzzle = Muzzles[i];
                muzzle.Av1Looping = false;
                muzzle.LastAv1Tick = Comp.Session.Tick;
                muzzle.Av2Looping = false;
                muzzle.LastAv2Tick = Comp.Session.Tick;

            }
            if (!avOnly)
            {
                _ticksUntilShoot = 0;
                SingleShotCounter = 0;
                PreFired = false;
                if (IsShooting && !System.DesignatorWeapon)
                {
                    EventTriggerStateChanged(EventTriggers.Firing, false);
                    EventTriggerStateChanged(EventTriggers.StopFiring, true, _muzzlesFiring);
                    Comp.CurrentDps = Comp.CurrentDps - Dps > 0 ? Comp.CurrentDps - Dps : 0;

                    if (!ActiveAmmoDef.AmmoDef.Const.MustCharge && (ActiveAmmoDef.AmmoDef.Const.EnergyAmmo || ActiveAmmoDef.AmmoDef.Const.IsHybrid) && !Comp.UnlimitedPower && power && DrawingPower)
                        StopPowerDraw();

                }
                IsShooting = false;
            }
        }

        public void DrawPower(bool adapt = false)
        {
            if (DrawingPower && !adapt) return;

            var useableDif = adapt ? OldUseablePower - UseablePower : -UseablePower;
            DrawingPower = true;
            //yes they are the right signs, weird math at play :P
            Comp.Ai.CurrentWeaponsDraw -= useableDif;
            Comp.SinkPower -= useableDif;
            Comp.Ai.GridAvailablePower += useableDif;
            Comp.MyCube.ResourceSink.Update();
        }

        public void StopPowerDraw()
        {
            if (!DrawingPower) return;
            DrawingPower = false;
            RequestedPower = false;
            Comp.Ai.RequestedWeaponsDraw -= RequiredPower;
            Comp.Ai.CurrentWeaponsDraw -= UseablePower;
            Comp.SinkPower -= UseablePower;
            Comp.Ai.GridAvailablePower += UseablePower;

            ChargeDelayTicks = 0;
            if (Comp.SinkPower < Comp.IdlePower) Comp.SinkPower = Comp.IdlePower;
            Comp.MyCube.ResourceSink.Update();
        }

        public void ChargeReload(bool syncCharge = false)
        {
            if (!syncCharge)
            {
                State.CurrentAmmo = 0;
                Comp.CurrentCharge -= State.CurrentCharge;
                State.CurrentCharge = 0;
            }

            Comp.Session.UniqueListAdd(this, Comp.Session.ChargingWeaponsIndexer, Comp.Session.ChargingWeapons);

            if(!Comp.UnlimitedPower)
                Comp.Ai.RequestedWeaponsDraw += RequiredPower;

            ChargeUntilTick = syncCharge ? ChargeUntilTick : (uint)System.ReloadTime + Comp.Session.Tick;
            Comp.Ai.OverPowered = Comp.Ai.RequestedWeaponsDraw > 0 && Comp.Ai.RequestedWeaponsDraw > Comp.Ai.GridMaxPower;
        }

        internal double GetMaxWeaponRange()
        {
            var ammoMax = ActiveAmmoDef.AmmoDef.Const.MaxTrajectory;
            var hardPointMax = System.Values.Targeting.MaxTargetDistance > 0 ? System.Values.Targeting.MaxTargetDistance : double.MaxValue;
            return Math.Min(hardPointMax, ammoMax);
        }

        internal void UpdateWeaponRange()
        {
            var range = Comp.Data.Repo.Set.Range < 0 ? double.MaxValue : Comp.Data.Repo.Set.Range; 
            var ammoMax = ActiveAmmoDef.AmmoDef.Const.MaxTrajectory;
            var hardPointMax = System.Values.Targeting.MaxTargetDistance > 0 ? System.Values.Targeting.MaxTargetDistance : double.MaxValue;
            var weaponRange = Math.Min(hardPointMax, ammoMax);
            MaxTargetDistance = Math.Min(range, weaponRange);
            MaxTargetDistanceSqr = MaxTargetDistance * MaxTargetDistance;
            MinTargetDistance = System.Values.Targeting.MinTargetDistance;
            MinTargetDistanceSqr = MinTargetDistance * MinTargetDistance;

            if (Comp.MaxTargetDistance < MaxTargetDistance) {
                Comp.MaxTargetDistance = MaxTargetDistance;
                Comp.MaxTargetDistanceSqr = MaxTargetDistanceSqr;
            }

            if (Comp.MinTargetDistance > MinTargetDistance) {
                Comp.MinTargetDistance = MinTargetDistance;
                Comp.MinTargetDistanceSqr = MinTargetDistanceSqr;
            }
        }

        public void StartPreFiringSound()
        {
            if (PreFiringSound == null)
                return;
            PreFiringEmitter?.PlaySound(PreFiringSound);
        }

        public void StopPreFiringSound(bool force)
        {
            PreFiringEmitter?.StopSound(force);
        }

        public void StartFiringSound()
        {
            if (FiringSound == null)
                return;
            FiringEmitter?.PlaySound(FiringSound);
        }

        public void StopFiringSound(object o = null)
        {
            var force = o as bool? ?? false;
            FiringEmitter?.StopSound(force);
        }

        public void StopReloadSound()
        {
            ReloadEmitter?.StopSound(true);
        }

        public void StopRotateSound()
        {
            RotateEmitter?.StopSound(true);
        }

        internal void RayCallBackClean()
        {
            RayCallBack.Weapon = null;
            RayCallBack = null;
        }

        internal void WakeTargets()
        {
            LastTargetTick = Comp.Session.Tick;
            if (Acquire.Enabled)
                System.Session.AcqManager.Awaken(Acquire);
            else
                System.Session.AcqManager.AddAwake(Acquire);

            ShortLoadId = Comp.Session.ShortLoadAssigner();
        }

        internal void SendTarget(int weaponId)
        {
            State.WeaponRandom.ResetRandom();
            System.Session.SendTargetChange(Comp, weaponId);
            //System.Session.SendCompState(Comp, PacketType.CompState);

        }
        internal void ChangeActiveAmmo()
        {
            var proposed = ProposedAmmoId != -1;
            var ammoType = proposed ? System.AmmoTypes[ProposedAmmoId] : System.AmmoTypes[State.AmmoTypeId];
            CanHoldMultMags = ((float)Comp.BlockInventory.MaxVolume * .75) > (ammoType.AmmoDef.Const.MagVolume * 2);
            ScheduleAmmoChange = false;

            if (ActiveAmmoDef == ammoType)
                return;

            if (proposed)  {
                State.AmmoTypeId = ProposedAmmoId;
                ProposedAmmoId = -1;
            }

            ActiveAmmoDef = System.AmmoTypes[State.AmmoTypeId];
            SetWeaponDps();
            UpdateWeaponRange();

            if (System.Session.MpActive && System.Session.IsServer)
                System.Session.SendCompData(Comp);
        }

        internal void AmmoChange(object o)
        {
            try
            {
                var ammoChange = (AmmoLoad)o;
                if (ammoChange.Change == AmmoLoad.ChangeType.Add)
                {
                    var oldType = System.AmmoTypes[ammoChange.OldId];
                    if (Comp.BlockInventory.CanItemsBeAdded(ammoChange.Amount, oldType.AmmoDefinitionId))
                        Comp.BlockInventory.AddItems(ammoChange.Amount, ammoChange.Item.Content);
                    else 
                    {
                        if (!Comp.Session.MpActive)
                            MyAPIGateway.Utilities.ShowNotification($"Weapon inventory full, ejecting {ammoChange.Item.Content.SubtypeName} magazine", 3000, "Red");
                        else if (Comp.Data.Repo.State.PlayerId > 0)
                        {
                            var message = $"Weapon inventory full, ejecting {ammoChange.Item.Content.SubtypeName} magazine";
                            Comp.Session.SendClientNotify(Comp.Data.Repo.State.PlayerId, message, true, "Red", 3000);
                        }
                        MyFloatingObjects.Spawn(ammoChange.Item, Dummies[0].Info.Position, MyPivotDir, MyPivotUp);
                    }
                }
            }
            catch (Exception ex) { Log.Line($"Exception in AmmoChange: {ex} - {((AmmoLoad)o).Amount} - {((AmmoLoad)o).Item.Content.SubtypeName}"); }
        }

        internal void ChangeAmmo(int newAmmoId)
        {

            if (System.Session.IsServer)
            {
                ProposedAmmoId = newAmmoId;

                var instantChange = System.Session.IsCreative || !ActiveAmmoDef.AmmoDef.Const.Reloadable;
                var canReload = State.CurrentAmmo == 0 && ActiveAmmoDef.AmmoDef.Const.Reloadable;
                var proposedAmmo = System.AmmoTypes[ProposedAmmoId];

                var unloadMag = !canReload && !instantChange && !Reloading && State.CurrentAmmo == ActiveAmmoDef.AmmoDef.Const.MagazineSize;

                if (unloadMag && proposedAmmo.AmmoDef.Const.Reloadable)
                {
                    State.CurrentAmmo = 0;
                    canReload = true;
                    System.Session.FutureEvents.Schedule(AmmoChange, new AmmoLoad {Amount = 1, Change = AmmoLoad.ChangeType.Add, OldId = State.AmmoTypeId, Item = ActiveAmmoDef.AmmoDef.Const.AmmoItem }, 1);
                }

                if (instantChange)
                    ChangeActiveAmmo();
                else if (canReload)
                    ScheduleAmmoChange = true;

                if (proposedAmmo.AmmoDef.Const.Reloadable && canReload)
                    Session.ComputeStorage(this);
            }
            else if (System.Session.MpActive)
                System.Session.SendAmmoCycleRequest(Comp, WeaponId, newAmmoId);
        }

        internal bool HasAmmo()
        {
            if (Comp.Session.IsCreative || !ActiveAmmoDef.AmmoDef.Const.Reloadable || System.DesignatorWeapon) {
                NoMagsToLoad = false;
                return true;
            }

            State.CurrentMags = Comp.BlockInventory.GetItemAmount(ActiveAmmoDef.AmmoDefinitionId);

            var energyDrainable = ActiveAmmoDef.AmmoDef.Const.EnergyAmmo && Comp.Ai.HasPower;
            var nothingToLoad = System.Session.IsServer ? State.CurrentMags <= 0 && !energyDrainable : !State.HasInventory && !energyDrainable;

            if (NoMagsToLoad)
            {
                if (nothingToLoad)
                    return false;

                //Log.Line($"[Found MagstoLoad] currentMags: {State.Sync.CurrentMags} - currentAmmo: {State.Sync.CurrentAmmo}");
                EventTriggerStateChanged(EventTriggers.NoMagsToLoad, false);
                Target.Reset(Comp.Session.Tick, Target.States.NoMagsToLoad);

                Comp.Ai.OutOfAmmoWeapons.Remove(this);

                if (System.Session.IsServer) 
                    State.HasInventory = true;

                NoMagsToLoad = false;
            }
            else if (nothingToLoad)
            {
                //Log.Line($"[No MagstoLoad] currentMags: {State.Sync.CurrentMags} - currentAmmo: {State.Sync.CurrentAmmo}");
                EventTriggerStateChanged(EventTriggers.NoMagsToLoad, true);
                Comp.Ai.OutOfAmmoWeapons.Add(this);

                if (System.Session.IsServer) 
                    State.HasInventory = false;

                NoMagsToLoad = true;

                Session.ComputeStorage(this);
            }

            return !NoMagsToLoad;
        }


        internal bool Reload()
        {
            var invalidState = State == null || ActiveAmmoDef.AmmoDef?.Const == null || Comp.MyCube.MarkedForClose || Comp.Platform.State != MyWeaponPlatform.PlatformState.Ready;
            if (invalidState || !Comp.IsWorking || !ActiveAmmoDef.AmmoDef.Const.Reloadable || System.DesignatorWeapon  || Reloading || PullingAmmo) 
                return false;

            if (State.CurrentAmmo > 0 || AnimationDelayTick > Comp.Session.Tick && (LastEventCanDelay || LastEvent == EventTriggers.Firing))
                return false;

            var hadNoMags = NoMagsToLoad;
            var scheduledChange = ScheduleAmmoChange;
            
            if (scheduledChange) 
                ChangeActiveAmmo();

            var hasAmmo = HasAmmo();
            var magStateChange = hadNoMags != NoMagsToLoad;

            if (IsShooting)
                StopShooting();
            FinishBurst = false;
            SingleShotCounter = 0;

            if (!hasAmmo) {

                if (magStateChange && System.Session.IsServer && System.Session.MpActive && !System.Session.PrunedPacketsToClient.ContainsKey(Comp.Data.Repo))
                    System.Session.SendCompState(Comp, PacketType.CompState);

                return false;
            }

            if (!ActiveAmmoDef.AmmoDef.Const.HasShotReloadDelay) ShotsFired = 0;

            Reloading = true;

            uint delay;
            if (System.WeaponAnimationLengths.TryGetValue(EventTriggers.Reloading, out delay)) {
                AnimationDelayTick = Comp.Session.Tick + delay;
                EventTriggerStateChanged(EventTriggers.Reloading, true);
            }

            if (System.Session.IsServer && !ActiveAmmoDef.AmmoDef.Const.EnergyAmmo)
                Comp.BlockInventory.RemoveItemsOfType(1, ActiveAmmoDef.AmmoDefinitionId);

            if (ActiveAmmoDef.AmmoDef.Const.MustCharge && !Comp.Session.ChargingWeaponsIndexer.ContainsKey(this))
                ChargeReload();
            else if (!ActiveAmmoDef.AmmoDef.Const.MustCharge) {

                if (System.ReloadTime > 0) {
                    CancelableReloadAction += Reloaded;
                    ReloadSubscribed = true;
                    Comp.Session.FutureEvents.Schedule(CancelableReloadAction, null, (uint) System.ReloadTime);
                }
                else Reloaded();
            }

            if (magStateChange && System.ReloadTime > 0 && System.Session.IsServer && System.Session.MpActive)
                System.Session.SendCompState(Comp, PacketType.CompState);

            if (ReloadEmitter == null || ReloadSound == null || ReloadEmitter.IsPlaying) return true;
            ReloadEmitter.PlaySound(ReloadSound, true, false, false, false, false, false);

            return true;
        }

        internal void Reloaded(object o = null)
        {
            using (Comp.MyCube.Pin()) {

                if (State == null || Comp.Data.Repo == null || Comp.Ai == null || Comp.MyCube.MarkedForClose) return;

                LastLoadedTick = Comp.Session.Tick;

                if (ActiveAmmoDef.AmmoDef.Const.MustCharge) {

                    if (ActiveAmmoDef.AmmoDef.Const.EnergyAmmo)
                        State.CurrentAmmo = ActiveAmmoDef.AmmoDef.Const.EnergyMagSize;

                    Comp.CurrentCharge -= State.CurrentCharge;
                    State.CurrentCharge = MaxCharge;
                    Comp.CurrentCharge += MaxCharge;

                    ChargeUntilTick = 0;
                    ChargeDelayTicks = 0;
                }
                else if (ReloadSubscribed) {
                    CancelableReloadAction -= Reloaded;
                    ReloadSubscribed = false;
                }

                EventTriggerStateChanged(EventTriggers.Reloading, false);

                if (!ActiveAmmoDef.AmmoDef.Const.EnergyAmmo && (System.Session.IsServer || State.HasInventory)) {
                    
                    //Log.Line($"[Reloaded] currentAmmo: {State.Sync.CurrentAmmo} - hasInventory: {State.Sync.HasInventory}");
                    if (State.CurrentAmmo <= 0)
                        State.CurrentAmmo = ActiveAmmoDef.AmmoDef.Const.MagazineDef.Capacity;

                }
                else if (System.Session.IsClient && !ActiveAmmoDef.AmmoDef.Const.EnergyAmmo && !HasAmmo()) {
                    EventTriggerStateChanged(EventTriggers.NoMagsToLoad, true);
                    //Log.Line($"[Reloaded failed] hasAmmo: {hasAmmo} - currentAmmo: {State.Sync.CurrentAmmo} - hasInventory: {State.Sync.HasInventory}");
                }

                if (Comp.Session.MpActive && Comp.Session.IsServer)
                    System.Session.SendCompState(Comp, PacketType.StateReload);

                SingleShotCounter = 0;
                Reloading = false;
            }
        }

        public void PlayEmissives(PartAnimation animation)
        {
            EmissiveState LastEmissive = new EmissiveState();
            for (int i = 0; i < animation.MoveToSetIndexer.Length; i++)
            {
                EmissiveState currentEmissive;
                if (System.WeaponEmissiveSet.TryGetValue(animation.EmissiveIds[animation.MoveToSetIndexer[i][(int)Indexer.EmissiveIndex]], out currentEmissive))
                {
                    currentEmissive.CurrentPart = animation.CurrentEmissivePart[animation.MoveToSetIndexer[i][(int)Indexer.EmissivePartIndex]];

                    if (currentEmissive.EmissiveParts != null && LastEmissive.EmissiveParts != null && currentEmissive.CurrentPart == LastEmissive.CurrentPart && currentEmissive.CurrentColor == LastEmissive.CurrentColor && Math.Abs(currentEmissive.CurrentIntensity - LastEmissive.CurrentIntensity) < 0.001)
                        currentEmissive = new EmissiveState();

                    LastEmissive = currentEmissive;


                    if (currentEmissive.EmissiveParts != null && currentEmissive.EmissiveParts.Length > 0)
                    {
                        if (currentEmissive.CycleParts)
                        {
                            animation.Part.SetEmissiveParts(currentEmissive.EmissiveParts[currentEmissive.CurrentPart], currentEmissive.CurrentColor,
                                currentEmissive.CurrentIntensity);
                            if (!currentEmissive.LeavePreviousOn)
                            {
                                var prev = currentEmissive.CurrentPart - 1 >= 0 ? currentEmissive.CurrentPart - 1 : currentEmissive.EmissiveParts
                                    .Length - 1;
                                animation.Part.SetEmissiveParts(currentEmissive.EmissiveParts[prev],
                                    Color.Transparent,
                                    0);
                            }
                        }
                        else
                        {
                            for (int j = 0; j < currentEmissive.EmissiveParts.Length; j++)
                                animation.Part.SetEmissiveParts(currentEmissive.EmissiveParts[j], currentEmissive.CurrentColor, currentEmissive.CurrentIntensity);
                        }
                    }
                }
            }
        }
    }
}
