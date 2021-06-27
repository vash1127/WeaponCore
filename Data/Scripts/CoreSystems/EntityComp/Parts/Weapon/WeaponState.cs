﻿using System;
using CoreSystems.Support;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRageMath;
using static CoreSystems.Support.WeaponDefinition.AnimationDef.PartAnimationSetDef;
namespace CoreSystems.Platform
{
    public partial class Weapon 
    {

        internal void PositionChanged(MyPositionComponentBase pComp)
        {
            try
            {
                if (PosChangedTick != Comp.Session.Tick)
                    UpdatePivotPos();

                if (Comp.UserControlled) {
                    ReturingHome = false;
                    IsHome = false;
                }
            }
            catch (Exception ex) { Log.Line($"Exception in PositionChanged: {ex}", null, true); }
        }

        internal void TargetChanged()
        {
            EventTriggerStateChanged(EventTriggers.Tracking, Target.HasTarget);
            EventTriggerStateChanged(EventTriggers.StopTracking, !Target.HasTarget);
            WeaponCache.MissDistance = 0;
            if (!Target.HasTarget)
            {
                if (InCharger) 
                    ExitCharger = true;

                if (Comp.Session.MpActive && Comp.Session.IsServer)  {
                    TargetData.ClearTarget();
                    if (!Comp.FakeMode)
                        Target.PushTargetToClient(this);
                } 
            }

            Target.TargetChanged = false;
        }

        internal bool ValidFakeTargetInfo(long playerId, out Ai.FakeTarget.FakeWorldTargetInfo fakeTargetInfo, bool preferPainted = true)
        {
            fakeTargetInfo = null;
            Ai.FakeTargets fakeTargets;
            if (Comp.Session.PlayerDummyTargets.TryGetValue(playerId, out fakeTargets))
            {
                var validManual = Comp.Data.Repo.Values.Set.Overrides.Control == ProtoWeaponOverrides.ControlModes.Manual && Comp.Data.Repo.Values.State.TrackingReticle && fakeTargets.ManualTarget.FakeInfo.WorldPosition != Vector3D.Zero;
                var validPainter = Comp.Data.Repo.Values.Set.Overrides.Control == ProtoWeaponOverrides.ControlModes.Painter && !fakeTargets.PaintedTarget.Dirty && fakeTargets.PaintedTarget.LocalPosition != Vector3D.Zero;
                var fakeTarget = validPainter && preferPainted ? fakeTargets.PaintedTarget : validManual ? fakeTargets.ManualTarget : null;
                if (fakeTarget == null || fakeTarget.Dirty)
                    return false;

                fakeTargetInfo = fakeTarget.LastInfoTick != System.Session.Tick ? fakeTarget.GetFakeTargetInfo(Comp.Ai) : fakeTarget.FakeInfo;
            }

            return fakeTargetInfo != null;
        }

        internal void EntPartClose(MyEntity obj)
        {
            obj.PositionComp.OnPositionChanged -= PositionChanged;
            obj.OnMarkForClose -= EntPartClose;
            if (Comp.FakeIsWorking)
                Comp.Status = CoreComponent.Start.ReInit;
        }

        internal void UpdateDesiredPower()
        {
            if (ActiveAmmoDef.AmmoDef.Const.MustCharge)
            {
                var rofPerSecond = RateOfFire / MyEngineConstants.UPDATE_STEPS_PER_SECOND;
                DesiredPower = ((ShotEnergyCost * (rofPerSecond * MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS)) * System.Values.HardPoint.Loading.BarrelsPerShot) * System.Values.HardPoint.Loading.TrajectilesPerBarrel;
            }
            else
                DesiredPower = Comp.IdlePower;
        }

        internal void UpdateShotEnergy()
        {
            var ewar = (int)ActiveAmmoDef.AmmoDef.AreaEffect.AreaEffect > 3;
            ShotEnergyCost = ewar ? ActiveAmmoDef.AmmoDef.EnergyCost * ActiveAmmoDef.AmmoDef.Override.AreaEffectDamage : ActiveAmmoDef.AmmoDef.EnergyCost * BaseDamage;
        }

        internal void UpdateBarrelRotation()
        {
            const int loopCnt = 10;
            var interval = System.Values.HardPoint.Loading.DeterministicSpin ? (3600f / System.BarrelSpinRate) * (1f / _numModelBarrels) : (3600f / System.BarrelSpinRate) * ((float)Math.PI / _numModelBarrels);
            var steps = (360f / _numModelBarrels) / interval;
            _ticksBeforeSpinUp = (uint)interval / loopCnt;
            for (int i = 0; i < loopCnt; i++) {

                var multi = (float)(i + 1) / loopCnt;
                var angle = MathHelper.ToRadians(steps * multi);
                switch (System.Values.HardPoint.Other.RotateBarrelAxis) {

                    case 1:
                        BarrelRotationPerShot[i] = SpinPart.ToTransformation * Matrix.CreateRotationX(angle) * SpinPart.FromTransformation;
                        break;
                    case 2:
                        BarrelRotationPerShot[i] = SpinPart.ToTransformation * Matrix.CreateRotationY(angle) * SpinPart.FromTransformation;
                        break;
                    case 3:
                        BarrelRotationPerShot[i] = SpinPart.ToTransformation * Matrix.CreateRotationZ(angle) * SpinPart.FromTransformation;
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
                //if ((ActiveAmmoDef.AmmoDef.Const.EnergyAmmo || ActiveAmmoDef.AmmoDef.Const.IsHybrid) && !ActiveAmmoDef.AmmoDef.Const.MustCharge && !Comp.UnlimitedPower && !ExitCharger)
                    //DrawPower();
            }
            IsShooting = true;
        }

        public void StopShooting(bool power = true)
        {
            FireCounter = 0;
            CeaseFireDelayTick = uint.MaxValue / 2;
            _ticksUntilShoot = 0;
            FinishBurst = false;

            if (PreFired)
                UnSetPreFire();

            if (IsShooting && !System.DesignatorWeapon)
            {
                EventTriggerStateChanged(EventTriggers.Firing, false);
                EventTriggerStateChanged(EventTriggers.StopFiring, true, _muzzlesFiring);
                Comp.CurrentDps = Comp.CurrentDps - Dps > 0 ? Comp.CurrentDps - Dps : 0;
            }

            if (System.Session.HandlesInput)
                StopShootingAv(power);
            else IsShooting = false;
        }

        internal void LostPowerIsThisEverUsed()
        {
            if (System.Session.IsServer)
            {
                PartState.WeaponMode(Comp, CoreComponent.TriggerActions.TriggerOff);
                //w.Ammo.CurrentAmmo = 0;
                Log.Line($"power off set ammo to 0");
            }

            Loading = false;
            FinishBurst = false;

            if (IsShooting)
                StopShooting();
        }

        internal double GetMaxWeaponRange()
        {
            var ammoMax = ActiveAmmoDef.AmmoDef.Override.MaxTrajectory;
            var hardPointMax = System.Values.Targeting.MaxTargetDistance > 0 ? System.Values.Targeting.MaxTargetDistance : double.MaxValue;
            return Math.Min(hardPointMax, ammoMax);
        }

        internal void UpdateWeaponRange()
        {
            var hardPointMax = System.Values.Targeting.MaxTargetDistance > 0 ? System.Values.Targeting.MaxTargetDistance : double.MaxValue;
            var range = Comp.Data.Repo.Values.Set.Range < 0 ? hardPointMax : Comp.Data.Repo.Values.Set.Range;
            var ammoMax = ActiveAmmoDef.AmmoDef.Override.MaxTrajectory;
            var weaponRange = Math.Min(hardPointMax, ammoMax);
            MaxTargetDistance = Math.Min(range, weaponRange);
            MaxTargetDistanceSqr = MaxTargetDistance * MaxTargetDistance;
            MinTargetDistance = System.Values.Targeting.MinTargetDistance;
            MinTargetDistanceSqr = MinTargetDistance * MinTargetDistance;

            var minBuffer = MinTargetDistance * 0.50;
            var minBufferSqr = (MinTargetDistance + minBuffer) * (MinTargetDistance + minBuffer);
            MinTargetDistanceBufferSqr = minBufferSqr;

            if (Comp.MaxDetectDistance < MaxTargetDistance)
            {
                Comp.MaxDetectDistance = MaxTargetDistance;
                Comp.MaxDetectDistanceSqr = MaxTargetDistanceSqr;
            }

            if (Comp.MinDetectDistance > MinTargetDistance)
            {
                Comp.MinDetectDistance = MinTargetDistance;
                Comp.MinDetectDistanceSqr = MinTargetDistanceSqr;
            }
        }

        internal void RayCallBackClean()
        {
            RayCallBack.Weapon = null;
            RayCallBack = null;
        }

        internal void WakeTargets()
        {
            LastTargetTick = Comp.Session.Tick;
            if (System.Session.IsServer && TrackTarget)
            {
                if (Acquire.Monitoring)
                    System.Session.AcqManager.Refresh(Acquire);
                else
                    System.Session.AcqManager.Monitor(Acquire);
            }

            ShortLoadId = Comp.Session.ShortLoadAssigner();
        }

        public void CriticalMonitor()
        {
            var cState = Comp.Data.Repo.Values.State;
            var cSet = Comp.Data.Repo.Values.Set;

            if (cState.CriticalReaction && !Comp.CloseCondition)
                CriticalOnDestruction(true);
            else if (cState.CountingDown && cSet.Overrides.ArmedTimer - 1 >= 0)
            {
                if (--cSet.Overrides.ArmedTimer == 0)
                {
                    CriticalOnDestruction();
                }
            }
        }

        public void CriticalOnDestruction(bool force = false)
        {
            if ((force || Comp.Data.Repo.Values.Set.Overrides.Armed) && !Comp.CloseCondition)
            {
                Comp.CloseCondition = true;
                Comp.Session.CreatePhantomEntity(Comp.SubtypeName, 3600, true, 1, System.Values.HardPoint.HardWare.CriticalReaction.AmmoRound, CoreComponent.TriggerActions.TriggerOnce, null, Comp.CoreEntity);
            }
        }
    }
}
