using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRageMath;
using WeaponCore.Support;
using static WeaponCore.Support.PartAnimation;
using static WeaponCore.Support.WeaponDefinition;
using static WeaponCore.Support.WeaponDefinition.AnimationDef.PartAnimationSetDef;
namespace WeaponCore.Platform
{
    public partial class Weapon
    {
        public void PositionChanged(MyPositionComponentBase pComp)
        {
            try
            {
                if (PosChangedTick != Comp.Session.Tick)
                    UpdatePivotPos();
            }
            catch (Exception ex) { Log.Line($"Exception in PositionChanged: {ex}"); }
        }

        public void UpdateParts(MyPositionComponentBase pComp)
        {
            if (_azimuthSubpartUpdateTick == Comp.Session.Tick) return;
            _azimuthSubpartUpdateTick = Comp.Session.Tick;

            var matrix = AzimuthPart.Entity.WorldMatrix;
            foreach (var part in AzimuthPart.Entity.Subparts)
            {
                //if(!part.Key.Contains(System.AzimuthPartName.String))
                    //part.Value.PositionComp.UpdateWorldMatrix(ref matrix);
            }
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

            if (Comp.BaseType == WeaponComponent.BlockType.Turret && Comp.Session.VanillaSubpartNames.Contains(System.AzimuthPartName.String) && Comp.Session.VanillaSubpartNames.Contains(System.ElevationPartName.String))
                obj.PositionComp.OnPositionChanged -= UpdateParts;
        }

        internal void EventTriggerStateChanged(EventTriggers state, bool active, HashSet<string> muzzles = null)
        {
            if (Comp?.MyCube == null || Comp.MyCube.MarkedForClose || Comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return;

            var session = Comp.Session;
            var canPlay = !session.DedicatedServer && session.SyncBufferedDistSqr >= Vector3D.DistanceSquared(session.CameraPos, MyPivotPos);

            switch (state)
            {
                case EventTriggers.StopFiring:
                case EventTriggers.PreFire:
                case EventTriggers.Firing:
                    if (AnimationsSet.ContainsKey(state))
                    {
                        var addToFiring = AnimationsSet.ContainsKey(EventTriggers.StopFiring) && state == EventTriggers.Firing;
                        uint delay = 0;
                        if (active)
                        {
                            if (state == EventTriggers.StopFiring)
                            {
                                // Fix this properly
                                var stopLen = 0u;
                                System.WeaponAnimationLengths.TryGetValue(EventTriggers.StopFiring, out stopLen);
                                Timings.ShootDelayTick = stopLen + session.Tick;
                                if (LastEvent == EventTriggers.Firing || LastEvent == EventTriggers.PreFire)
                                {
                                    if (CurLgstAnimPlaying != null && CurLgstAnimPlaying.Running)
                                    {
                                        delay = CurLgstAnimPlaying.Reverse ? (uint)CurLgstAnimPlaying.CurrentMove : (uint)((CurLgstAnimPlaying.NumberOfMoves - 1) - CurLgstAnimPlaying.CurrentMove);
                                        Timings.ShootDelayTick += delay;
                                    }
                                }
                            }
                        }

                        for (int i = 0; i < AnimationsSet[state].Length; i++)
                        {
                            var animation = AnimationsSet[state][i];

                            if (active && !animation.Running && (animation.Muzzle == "Any" || (muzzles != null && muzzles.Contains(animation.Muzzle))))
                            {
                                if (animation.TriggerOnce && animation.Triggered) continue;
                                animation.Triggered = true;

                                if (CurLgstAnimPlaying == null || CurLgstAnimPlaying.EventTrigger != state || animation.NumberOfMoves > CurLgstAnimPlaying.NumberOfMoves)
                                    CurLgstAnimPlaying = animation;

                                if (animation.Muzzle != "Any" && addToFiring) _muzzlesFiring.Add(animation.Muzzle);

                                animation.StartTick = session.Tick + animation.MotionDelay + delay;
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
                    }
                    break;
                case EventTriggers.StopTracking:
                case EventTriggers.Tracking:
                    if (AnimationsSet.ContainsKey(state))
                    {
                        var oppositeEvnt = state == EventTriggers.Tracking ? EventTriggers.StopTracking : EventTriggers.Tracking;
                        //if (active) LastEvent = state;
                        for (int i = 0; i < AnimationsSet[state].Length; i++)
                        {
                            var animation = AnimationsSet[state][i];
                            if (active && !animation.Running)
                            {
                                if (animation.TriggerOnce && animation.Triggered) continue;
                                animation.Triggered = true;

                                if (CurLgstAnimPlaying == null || CurLgstAnimPlaying.EventTrigger != state || animation.NumberOfMoves > CurLgstAnimPlaying.NumberOfMoves)
                                    CurLgstAnimPlaying = animation;

                                PartAnimation animCheck;
                                animation.Running = true;
                                //animation.Paused = Comp.ResettingSubparts;
                                animation.CanPlay = canPlay;
                                string opEvent = "";
                                if (animation.EventIdLookup.TryGetValue(oppositeEvnt, out opEvent) && AnimationLookup.TryGetValue(opEvent, out animCheck) && animCheck.Running)
                                {
                                    animCheck.Reverse = true;

                                    if (!animation.DoesLoop)
                                        animation.Running = false;
                                    else
                                    {
                                        animation.StartTick = Comp.Session.Tick + (uint)animCheck.CurrentMove + animation.MotionDelay;
                                        Comp.Session.AnimationsToProcess.Add(animation);
                                    }
                                }
                                else
                                {
                                    Comp.Session.AnimationsToProcess.Add(animation);
                                    animation.StartTick = session.Tick + animation.MotionDelay;
                                }

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
                    }
                    break;
                case EventTriggers.TurnOn:
                case EventTriggers.TurnOff:
                    if (active && AnimationsSet.ContainsKey(state))
                    {
                        var oppositeEvnt = state == EventTriggers.TurnOff ? EventTriggers.TurnOn : EventTriggers.TurnOff;

                        if ((state == EventTriggers.TurnOn && !Comp.State.Value.Online) || state == EventTriggers.TurnOff && Comp.State.Value.Online) return;

                        //LastEvent = state;
                        for (int i = 0; i < AnimationsSet[state].Length; i++)
                        {
                            var animation = AnimationsSet[state][i];
                            if (!animation.Running)
                            {
                                if (CurLgstAnimPlaying == null || CurLgstAnimPlaying.EventTrigger != state || animation.NumberOfMoves > CurLgstAnimPlaying.NumberOfMoves)
                                    CurLgstAnimPlaying = animation;

                                PartAnimation animCheck;
                                animation.Running = true;
                                animation.CanPlay = true;
                                //animation.Paused = Comp.ResettingSubparts;
                                string eventName;
                                if (animation.EventIdLookup.TryGetValue(oppositeEvnt, out eventName) && AnimationLookup.TryGetValue(eventName, out animCheck))
                                {
                                    if (animCheck.Running)
                                    {
                                        animCheck.Reverse = true;
                                        animation.Running = false;
                                    }
                                    else
                                        session.ThreadedAnimations.Enqueue(animation);
                                }
                                else
                                    session.ThreadedAnimations.Enqueue(animation);

                                animation.StartTick = session.Tick + animation.MotionDelay;
                                if (state == EventTriggers.TurnOff) animation.StartTick += Timings.OffDelay;
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
                    if (AnimationsSet.ContainsKey(state))
                    {
                        //if (active) LastEvent = state;
                        for (int i = 0; i < AnimationsSet[state].Length; i++)
                        {
                            var animation = AnimationsSet[state][i];
                            if (active && !animation.Running)
                            {
                                if (animation.TriggerOnce && animation.Triggered) continue;
                                animation.Triggered = true;

                                if (CurLgstAnimPlaying == null || CurLgstAnimPlaying.EventTrigger != state || animation.NumberOfMoves > CurLgstAnimPlaying.NumberOfMoves)
                                    CurLgstAnimPlaying = animation;

                                animation.StartTick = session.Tick + animation.MotionDelay;
                                session.ThreadedAnimations.Enqueue(animation);

                                animation.Running = true;
                                animation.CanPlay = canPlay;
                                //animation.Paused = Comp.ResettingSubparts;

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
                    }
                    break;
            }
            if(active)
                LastEvent = state;
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
            StopFiringSound(false);
            StopPreFiringSound(false);
            if (AvCapable && RotateEmitter != null && RotateEmitter.IsPlaying) StopRotateSound();
            CeaseFireDelayTick = uint.MaxValue;
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

        public void StartReload(bool reset = false)
        {
            if (State?.Sync == null || Timings == null || ActiveAmmoDef.AmmoDef?.Const == null || Comp?.MyCube == null || Comp.MyCube.MarkedForClose || State.Sync.Reloading || Comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return;

            if (reset) State.Sync.Reloading = false;

            FinishBurst = false;
            State.Sync.Reloading = true;

            if (IsShooting)
                StopShooting();

            if (!ActiveAmmoDef.AmmoDef.Const.HasShotReloadDelay)
                State.ShotsFired = 0;

            var newAmmo = System.WeaponAmmoTypes[Set.AmmoTypeId];

            if (!ActiveAmmoDef.Equals(newAmmo))
                ChangeAmmo(ref newAmmo);

            if (Timings.AnimationDelayTick > Comp.Session.Tick && LastEvent != EventTriggers.Reloading)
            {
                Comp.Session.FutureEvents.Schedule(o => { StartReload(true); }, null, Timings.AnimationDelayTick - Comp.Session.Tick);
                return;
            }

            if ((State.Sync.CurrentMags == 0 && !ActiveAmmoDef.AmmoDef.Const.EnergyAmmo && !Comp.Session.IsCreative))
            {
                if (!OutOfAmmo)
                {
                    EventTriggerStateChanged(EventTriggers.OutOfAmmo, true);
                    OutOfAmmo = true;
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

                if (ActiveAmmoDef.AmmoDef.Const.MustCharge && (State.Sync.CurrentMags > 0 || ActiveAmmoDef.AmmoDef.Const.EnergyAmmo || Comp.Session.IsCreative) && !Comp.Session.ChargingWeaponsCheck.ContainsKey(this))
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
                    Comp.Session.WeaponAmmoRemoveQueue.Enqueue(this);
                else
                {
                    if (Comp.Session.IsCreative)
                        ActiveAmmoDef = newAmmo;
                    
                    Session.ComputeStorage(this);
                }
                return;
            }

            if (!newAmmo.AmmoDef.Const.EnergyAmmo)
            {
                ActiveAmmoDef = newAmmo;
                Session.ComputeStorage(this);
                return;
            }
            ActiveAmmoDef = newAmmo;
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

        internal void GetAmmoClient()
        {
            State.Sync.CurrentMags = Comp.BlockInventory.GetItemAmount(ActiveAmmoDef.AmmoDefinitionId);
        }

        internal void ReloadClient()
        {
            if (CanReload)
                StartReload();
        }

        internal void Reloaded(object o = null)
        {

            if (State?.Sync == null || Comp?.State?.Value == null  || Comp.Ai == null|| Timings == null|| !State.Sync.Reloading || State.Sync.CurrentAmmo > 0) return;

            using (Comp.MyCube?.Pin())
            {
                if (Comp.MyCube != null && Comp.MyCube.MarkedForClose) return;

                if (ActiveAmmoDef.AmmoDef.Const.MustCharge)
                {
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

                var hasMags = (State.Sync.CurrentMags > 0 || Comp.Session.IsCreative);
                
                if (!ActiveAmmoDef.AmmoDef.Const.EnergyAmmo && hasMags && (Comp.Session.IsClient || !Comp.Session.IsClient && (Comp.Session.IsCreative || Comp.BlockInventory.RemoveItemsOfType(1, ActiveAmmoDef.AmmoDef.Const.AmmoItem.Content))))
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
                ActiveAmmoDef = System.WeaponAmmoTypes[Set.AmmoTypeId];
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

        public void StopFiringSound(bool force)
        {
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
