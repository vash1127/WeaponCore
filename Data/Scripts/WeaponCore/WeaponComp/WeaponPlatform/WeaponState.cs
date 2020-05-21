using System;
using System.Collections.Generic;
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


        public void ChangeActiveAmmo(WeaponAmmoTypes ammoDef)
        {
            ActiveAmmoDef = ammoDef;
        }

        public void PositionChanged(MyPositionComponentBase pComp)
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
                Comp.WeaponValues.Targets[WeaponId].State = TransferTarget.TargetInfo.Expired;

                if (Comp.Session.MpActive && Comp.Session.IsServer && !Comp.TrackReticle)
                    Comp.Session.SendTargetExpiredUpdate(Comp, WeaponId);
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
            if (Comp?.State == null || Comp.MyCube == null || Comp.MyCube.MarkedForClose || Comp.Ai == null || Comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return;
            try
            {
                var session = Comp.Session;
                var distance = Vector3D.DistanceSquared(session.CameraPos, MyPivotPos);
                var canPlay = !session.DedicatedServer && 64000000 >= distance; //8km max range, will play regardless of range if it moves PivotPos and is loaded

                if (canPlay)
                    PlayParticleEvent(state, active, distance, muzzles);

                if (!AnimationsSet.ContainsKey(state)) return;
                if (Timings.AnimationDelayTick < Comp.Session.Tick)
                    Timings.AnimationDelayTick = Comp.Session.Tick;

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

                                    if (animation.Muzzle != "Any" && addToFiring) _muzzlesFiring.Add(animation.Muzzle);

                                    animation.StartTick = session.Tick + animation.MotionDelay;
                                    if(state == EventTriggers.StopFiring)
                                        animation.StartTick += (Timings.AnimationDelayTick - session.Tick);

                                    Comp.Session.AnimationsToProcess.Add(animation);
                                    animation.Running = true;
                                    //animation.Paused = Comp.ResettingSubparts;
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

                                    animation.Triggered = true;
                                    animation.Running = true;
                                    animation.CanPlay = canPlay;
                                    
                                    Comp.Session.AnimationsToProcess.Add(animation);
                                    animation.StartTick = session.Tick + animation.MotionDelay;

                                    if (LastEvent == EventTriggers.StopTracking || LastEvent == EventTriggers.Tracking)
                                        animation.StartTick += (Timings.AnimationDelayTick - session.Tick);

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

                                    animation.StartTick = session.Tick + animation.MotionDelay + (Timings.AnimationDelayTick - session.Tick);
                                    if (state == EventTriggers.TurnOff) animation.StartTick += Timings.OffDelay;

                                    session.ThreadedAnimations.Enqueue(animation);
                                }
                                else
                                    animation.Reverse = false;
                            }
                        }
                        break;
                    case EventTriggers.EmptyOnGameLoad:
                    case EventTriggers.Overheated:
                    case EventTriggers.OutOfAmmo:
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
                if (active)
                {
                    var animationLength = 0u;

                    LastEvent = state;
                    LastEventCanDelay = state == EventTriggers.Reloading || state == EventTriggers.StopFiring || state == EventTriggers.TurnOff || state == EventTriggers.TurnOn;

                    if (System.WeaponAnimationLengths.TryGetValue(state, out animationLength))
                        Timings.AnimationDelayTick += animationLength;
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
                State.SingleShotCounter = 0;
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

            Timings.ChargeDelayTicks = 0;
            if (Comp.SinkPower < Comp.IdlePower) Comp.SinkPower = Comp.IdlePower;
            Comp.MyCube.ResourceSink.Update();
        }

        public void StartReload()
        {
            if (State?.Sync == null || Timings == null || ActiveAmmoDef.AmmoDef?.Const == null || Comp?.MyCube == null || Comp.MyCube.MarkedForClose || State.Sync.Reloading || Comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return;

            var newAmmo = System.WeaponAmmoTypes[Set.AmmoTypeId];

            State.Sync.Reloading = true;
            FinishBurst = false;

            if (IsShooting)
                StopShooting();

            State.SingleShotCounter = 0;

            if (!ActiveAmmoDef.AmmoDef.Const.HasShotReloadDelay)
                State.ShotsFired = 0;

            if (!ActiveAmmoDef.Equals(newAmmo))
                ChangeAmmo(ref newAmmo);

            if ((State.Sync.CurrentMags == 0 && !ActiveAmmoDef.AmmoDef.Const.EnergyAmmo && !Comp.Session.IsCreative))
            {
                if (!OutOfAmmo)
                {
                    EventTriggerStateChanged(EventTriggers.OutOfAmmo, true);
                    OutOfAmmo = true;
                    Target.Reset(Comp.Session.Tick, Target.States.OutOfAmmo);
                }
                State.Sync.Reloading = false;
            }
            else
            {
                if (OutOfAmmo)
                {
                    EventTriggerStateChanged(EventTriggers.OutOfAmmo, false);
                    OutOfAmmo = false;
                }

                uint delay;
                if (System.WeaponAnimationLengths.TryGetValue(EventTriggers.Reloading, out delay))
                {
                    Timings.AnimationDelayTick = Comp.Session.Tick + delay;
                    EventTriggerStateChanged(EventTriggers.Reloading, true);
                }

                if (!Comp.Session.IsClient && !ActiveAmmoDef.AmmoDef.Const.EnergyAmmo)
                    Comp.BlockInventory.RemoveItemsOfType(1, ActiveAmmoDef.AmmoDefinitionId);

                if (ActiveAmmoDef.AmmoDef.Const.MustCharge && !Comp.Session.ChargingWeaponsCheck.ContainsKey(this))
                    ChargeReload();
                else if (!ActiveAmmoDef.AmmoDef.Const.MustCharge)
                {
                    CancelableReloadAction += Reloaded;
                    ReloadSubscribed = true;
                    Comp.Session.FutureEvents.Schedule(CancelableReloadAction, null, (uint)System.ReloadTime);
                    Timings.ReloadedTick = (uint)System.ReloadTime + Comp.Session.Tick;
                }

                if (ReloadEmitter == null || ReloadEmitter.IsPlaying) return;

                if (ReloadSound == null)
                {
                    Log.Line($"ReloadSound is null");
                    return;
                }
                ReloadEmitter.PlaySound(ReloadSound, true, false, false, false, false, false);
            }
        }

        public void ChangeAmmo(ref WeaponAmmoTypes newAmmo)
        {
            if (!ActiveAmmoDef.AmmoDef.Const.EnergyAmmo && State.Sync.CurrentMags > 0)
            {
                if (Comp.Session.IsServer && !Comp.Session.IsCreative)
                {
                    Comp.Session.WeaponsToRemoveAmmo.Add(this);
                    Comp.Session.WeaponsToRemoveAmmo.ApplyAdditions();
                }
                else
                {
                    if (Comp.Session.IsCreative)
                        ChangeActiveAmmo(newAmmo);

                    Session.ComputeStorage(this);
                }
                return;
            }

            if (!newAmmo.AmmoDef.Const.EnergyAmmo)
            {
                ChangeActiveAmmo(newAmmo);
                Session.ComputeStorage(this);
                return;
            }
            ChangeActiveAmmo(newAmmo);
            SetWeaponDps();
        }

        public void ChargeReload(bool syncCharge = false)
        {
            if (!syncCharge)
            {
                State.Sync.CurrentAmmo = 0;
                Comp.State.Value.CurrentCharge -= State.Sync.CurrentCharge;
                State.Sync.CurrentCharge = 0;
                Comp.TerminalRefresh();
            }

            Comp.Session.ChargingWeapons.Add(this);
            Comp.Session.ChargingWeaponsCheck.Add(this, Comp.Session.ChargingWeapons.Count - 1);

            if(!Comp.UnlimitedPower)
                Comp.Ai.RequestedWeaponsDraw += RequiredPower;

            Timings.ChargeUntilTick = syncCharge ? Timings.ChargeUntilTick : (uint)System.ReloadTime + Comp.Session.Tick;
            Comp.Ai.OverPowered = Comp.Ai.RequestedWeaponsDraw > 0 && Comp.Ai.RequestedWeaponsDraw > Comp.Ai.GridMaxPower;
        }

        internal void Reloaded(object o = null)
        {

            if (State?.Sync == null || Comp?.State?.Value == null  || Comp.Ai == null|| Timings == null|| !State.Sync.Reloading || State.Sync.CurrentAmmo > 0) return;

            LastLoadedTick = Comp.Session.Tick;

            using (Comp.MyCube?.Pin())
            {
                if (Comp.MyCube != null && Comp.MyCube.MarkedForClose) return;

                if (ActiveAmmoDef.AmmoDef.Const.MustCharge)
                {
                    if(ActiveAmmoDef.AmmoDef.Const.EnergyAmmo)
                        State.Sync.CurrentAmmo = ActiveAmmoDef.AmmoDef.Const.EnergyMagSize;

                    Comp.State.Value.CurrentCharge -= State.Sync.CurrentCharge;

                    State.Sync.CurrentCharge = MaxCharge;

                    Comp.State.Value.CurrentCharge += MaxCharge;

                    Timings.ChargeUntilTick = 0;
                    Timings.ChargeDelayTicks = 0;
                }
                else if (ReloadSubscribed)
                {
                    CancelableReloadAction -= Reloaded;
                    ReloadSubscribed = false;
                }

                EventTriggerStateChanged(EventTriggers.Reloading, false);

                if (!ActiveAmmoDef.AmmoDef.Const.EnergyAmmo)
                    State.Sync.CurrentAmmo = ActiveAmmoDef.AmmoDef.Const.MagazineDef.Capacity;

                State.Sync.Reloading = false;

                if (Comp.Session.IsServer && Comp.Session.MpActive && Comp.Session.Tick - LastSyncTick > Session.ResyncMinDelayTicks && Comp.Session.WeaponsSyncCheck.Add(this))
                {
                    Comp.Session.WeaponsToSync.Add(this);
                    Comp.Ai.NumSyncWeapons++;

                    SendTarget = false;
                    SendSync = true;

                    LastSyncTick = Comp.Session.Tick;
                }

                Comp.TerminalRefresh();
            }
        }

        internal void ForceSync()
        {
            if (Comp.Session.WeaponsSyncCheck.Add(this))
            {
                Comp.Session.WeaponsToSync.Add(this);
                Comp.Ai.NumSyncWeapons++;

                SendTarget = false;
                SendSync = true;

                LastSyncTick = Comp.Session.Tick;
            }
        }

        internal void CycleAmmo(object o = null)
        {
            if (State.Sync.CurrentAmmo == 0)
            {
                var newAmmo = System.WeaponAmmoTypes[Set.AmmoTypeId];
                ChangeAmmo(ref newAmmo);
            }
            else if (CanReload)
                StartReload();
            else if (!ActiveAmmoDef.AmmoDef.Const.Reloadable)
            {
                ChangeActiveAmmo(System.WeaponAmmoTypes[Set.AmmoTypeId]);
                SetWeaponDps();
                if (CanReload)
                    StartReload();
            }
        }

        internal double GetMaxWeaponRange()
        {
            var ammoMax = ActiveAmmoDef.AmmoDef.Const.MaxTrajectory;
            var hardPointMax = System.Values.Targeting.MaxTargetDistance > 0 ? System.Values.Targeting.MaxTargetDistance : double.MaxValue;
            return Math.Min(hardPointMax, ammoMax);
        }

        internal void UpdateWeaponRange()
        {
            var range = Comp.Set.Value.Range < 0 ? double.MaxValue : Comp.Set.Value.Range; 
            var ammoMax = ActiveAmmoDef.AmmoDef.Const.MaxTrajectory;
            var hardPointMax = System.Values.Targeting.MaxTargetDistance > 0 ? System.Values.Targeting.MaxTargetDistance : double.MaxValue;
            var weaponRange = Math.Min(hardPointMax, ammoMax);
            MaxTargetDistance = Math.Min(range, weaponRange);
            MaxTargetDistanceSqr = MaxTargetDistance * MaxTargetDistance;
            MinTargetDistance = System.Values.Targeting.MinTargetDistance;
            MinTargetDistanceSqr = MinTargetDistance * MinTargetDistance;
        }

        public void StartPreFiringSound()
        {
            if (PreFiringSound == null)
            {
                Log.Line($"PreFiringSound is null");
                return;
            }
            PreFiringEmitter?.PlaySound(PreFiringSound);
        }

        public void StopPreFiringSound(bool force)
        {
            PreFiringEmitter?.StopSound(force);
        }

        public void StartFiringSound()
        {
            if (FiringSound == null)
            {
                Log.Line($"FiringSound is null");
                return;
            }
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

        internal void WakeTargets()
        {
            LastTargetTick = Comp.Session.Tick;
            LoadId = Comp.Session.LoadAssigner();
            ShortLoadId = Comp.Session.ShortLoadAssigner();
        }

        public void PlayEmissives(PartAnimation animation, WeaponSystem system)
        {
            EmissiveState LastEmissive = new EmissiveState();
            for (int i = 0; i < animation.MoveToSetIndexer.Length; i++)
            {
                EmissiveState currentEmissive;
                if (system.WeaponEmissiveSet.TryGetValue(animation.EmissiveIds[animation.MoveToSetIndexer[i][(int)Indexer.EmissiveIndex]], out currentEmissive))
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
