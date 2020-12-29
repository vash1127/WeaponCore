using System;
using VRage.Utils;
using VRageMath;
using WeaponCore.Support;
using static WeaponCore.Support.WeaponDefinition.AnimationDef.PartAnimationSetDef;
using static WeaponCore.Support.WeaponComponent;

namespace WeaponCore.Platform
{
    public partial class Weapon
    {
        public void AimBarrel()
        {
            LastTrackedTick = Comp.Session.Tick;
            IsHome = false;

            if (AiOnlyWeapon) {

                if (AzimuthTick == Comp.Session.Tick && System.TurretMovement == WeaponSystem.TurretType.Full || System.TurretMovement == WeaponSystem.TurretType.AzimuthOnly) {
                    Matrix azRotMatrix;
                    Matrix.CreateFromAxisAngle(ref AzimuthPart.RotationAxis, (float)Azimuth, out azRotMatrix);
                    var localMatrix = AzimuthPart.OriginalPosition * azRotMatrix;
                    localMatrix.Translation = AzimuthPart.Entity.PositionComp.LocalMatrix.Translation;
                    AzimuthPart.Entity.PositionComp.SetLocalMatrix(ref localMatrix, null, true);
                }

                if (ElevationTick == Comp.Session.Tick && (System.TurretMovement == WeaponSystem.TurretType.Full || System.TurretMovement == WeaponSystem.TurretType.ElevationOnly)) {
                    Matrix elRotMatrix;
                    Matrix.CreateFromAxisAngle(ref ElevationPart.RotationAxis, -(float)Elevation, out elRotMatrix);
                    var localMatrix = ElevationPart.OriginalPosition * elRotMatrix;
                    localMatrix.Translation = ElevationPart.Entity.PositionComp.LocalMatrix.Translation;
                    ElevationPart.Entity.PositionComp.SetLocalMatrix(ref localMatrix, null, true);
                }
            }
            else {
                if (ElevationTick == Comp.Session.Tick)
                {
                    Comp.TurretBase.Elevation = (float)Elevation;
                }

                if (AzimuthTick == Comp.Session.Tick)
                {
                    Comp.TurretBase.Azimuth = (float)Azimuth;
                }
            }
        }

        public void ScheduleWeaponHome(bool sendNow = false)
        {
            if (ReturingHome)
                return;

            ReturingHome = true;
            if (sendNow)
                SendTurretHome();
            else 
                System.Session.FutureEvents.Schedule(SendTurretHome, null, 300u);
        }

        public void SendTurretHome(object o = null)
        {
            System.Session.HomingWeapons.Add(this);
        }

        public void TurretHomePosition()
        {
            using (Comp.MyCube.Pin()) {

                if (Comp.MyCube.MarkedForClose || Comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return;

                if (State.Action != ShootActions.ShootOff || Comp.UserControlled || Target.HasTarget || !ReturingHome) {
                    ReturingHome = false;
                    return;
                }

                if (Comp.BaseType == BlockType.Turret && Comp.TurretBase != null) {
                    Azimuth = Comp.TurretBase.Azimuth;
                    Elevation = Comp.TurretBase.Elevation;
                }

                var azStep = System.AzStep;
                var elStep = System.ElStep;

                var oldAz = Azimuth;
                var oldEl = Elevation;

                if (oldAz > 0)
                    Azimuth = oldAz - azStep > 0 ? oldAz - azStep : 0;
                else if (oldAz < 0)
                    Azimuth = oldAz + azStep < 0 ? oldAz + azStep : 0;

                if (oldEl > 0)
                    Elevation = oldEl - elStep > 0 ? oldEl - elStep : 0;
                else if (oldEl < 0)
                    Elevation = oldEl + elStep < 0 ? oldEl + elStep : 0;

                if (!MyUtils.IsEqual((float)oldAz, (float)Azimuth))
                    AzimuthTick = Comp.Session.Tick;

                if (!MyUtils.IsEqual((float)oldEl, (float)Elevation))
                    ElevationTick = Comp.Session.Tick;

                AimBarrel();

                if (Azimuth > 0 || Azimuth < 0 || Elevation > 0 || Elevation < 0) {
                    IsHome = false;
                }
                else {
                    IsHome = true;
                    ReturingHome = false;
                }
            }
        }

        internal void UpdatePivotPos()
        {
            if (PosChangedTick == Comp.Session.Tick || AzimuthPart?.Parent == null || ElevationPart?.Entity == null || MuzzlePart?.Entity == null || Comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return;

            PosChangedTick = Comp.Session.Tick;
            var azimuthMatrix = AzimuthPart.Entity.PositionComp.WorldMatrixRef;
            var elevationMatrix = ElevationPart.Entity.PositionComp.WorldMatrixRef;
            var weaponCenter = MuzzlePart.Entity.PositionComp.WorldMatrixRef.Translation;
            var centerTestPos = azimuthMatrix.Translation + (azimuthMatrix.Down * 1);

            MyPivotUp = azimuthMatrix.Up;
            MyPivotDir = elevationMatrix.Forward;

            if (System.TurretMovement == WeaponSystem.TurretType.ElevationOnly)
            {
                Vector3D forward;
                var eLeft = elevationMatrix.Left;
                Vector3D.Cross(ref eLeft, ref MyPivotUp, out forward);
                WeaponConstMatrix = new MatrixD { Forward = forward, Up = MyPivotUp, Left = elevationMatrix.Left };
            }
            else
            {
                var forward = Vector3D.TransformNormal((AzimuthPart.Parent.PositionComp.LocalMatrix * AzimuthRotation).Forward, AzimuthPart.Parent.WorldMatrix);
                Vector3D left;
                Vector3D.Cross(ref MyPivotUp, ref forward, out left);
                WeaponConstMatrix = new MatrixD { Forward = forward, Up = MyPivotUp, Left = left };
            }

            Vector3D axis;
            Vector3D.Cross(ref MyPivotUp ,ref MyPivotDir, out axis);
            if (Vector3D.IsZero(axis))
                MyPivotPos = centerTestPos;
            else
            {
                Vector3D perpDir2;
;               Vector3D.Cross(ref MyPivotDir, ref axis, out perpDir2);
                var point1To2 = weaponCenter - centerTestPos;
                
                double point1Dot;
                Vector3D.Dot(ref point1To2, ref perpDir2, out point1Dot);

                double myPivotUpDot;
                Vector3D.Dot(ref MyPivotUp, ref perpDir2, out myPivotUpDot);

                MyPivotPos = centerTestPos + point1Dot / myPivotUpDot * MyPivotUp;
            }


            if (!Vector3D.IsZero(AimOffset))
            {
                var pivotRotMatrix = new MatrixD { Forward = MyPivotDir, Left = elevationMatrix.Left, Up = elevationMatrix.Up };
                Vector3D offSet;
                Vector3D.Rotate(ref AimOffset, ref pivotRotMatrix, out offSet);

                MyPivotPos += offSet;
            }

            MyRayCheckPos = MyPivotPos + (MyPivotDir * Comp.MyCube.CubeGrid.GridSizeHalf);
                
            if (!Comp.Debug) return;
            MyCenterTestLine = new LineD(centerTestPos, centerTestPos + (MyPivotUp * 20));
            MyBarrelTestLine = new LineD(weaponCenter, weaponCenter + (MyPivotDir * 18));
            MyPivotTestLine = new LineD(MyPivotPos, MyPivotPos - (WeaponConstMatrix.Left * 10));
            MyAimTestLine = new LineD(MyPivotPos, MyPivotPos + (MyPivotDir * 20));
            if (Target.HasTarget)
                MyShootAlignmentLine = new LineD(MyPivotPos, Target.TargetPos);
        }

        internal void UpdateWeaponHeat(object o = null)
        {
            try
            {
                Comp.CurrentHeat = Comp.CurrentHeat >= HsRate ? Comp.CurrentHeat - HsRate : 0;
                State.Heat = State.Heat >= HsRate ? State.Heat - HsRate : 0;

                var set = State.Heat - LastHeat > 0.001 || State.Heat - LastHeat < 0.001;

                LastHeatUpdateTick = Comp.Session.Tick;

                if (!Comp.Session.DedicatedServer)
                {
                    var heatOffset = HeatPerc = State.Heat / System.MaxHeat;

                    if (set && heatOffset > .33)
                    {
                        if (heatOffset > 1) heatOffset = 1;

                        heatOffset -= .33f;

                        var intensity = .7f * heatOffset;

                        var color = Comp.Session.HeatEmissives[(int)(heatOffset * 100)];

                        for(int i = 0; i < HeatingParts.Count; i++)
                            HeatingParts[i]?.SetEmissiveParts("Heating", color, intensity);
                    }
                    else if (set)
                        for(int i = 0; i < HeatingParts.Count; i++)
                            HeatingParts[i]?.SetEmissiveParts("Heating", Color.Transparent, 0);

                    LastHeat = State.Heat;
                }

                if (set && System.DegRof && State.Heat >= (System.MaxHeat * .8))
                {
                    CurrentlyDegrading = true;
                    UpdateRof();
                }
                else if (set && CurrentlyDegrading)
                {
                    if (State.Heat <= (System.MaxHeat * .4)) 
                        CurrentlyDegrading = false;

                    UpdateRof();
                }

                if (State.Overheated && State.Heat <= (System.MaxHeat * System.WepCoolDown))
                {
                    EventTriggerStateChanged(EventTriggers.Overheated, false);
                    State.Overheated = false;
                    if (System.Session.MpActive && System.Session.IsServer)
                        System.Session.SendCompState(Comp);
                }

                if (State.Heat > 0)
                    Comp.Session.FutureEvents.Schedule(UpdateWeaponHeat, null, 20);
                else
                {
                    HeatLoopRunning = false;
                    LastHeatUpdateTick = 0;
                }
            }
            catch (Exception ex) { Log.Line($"Exception in UpdateWeaponHeat: {ex} - {System == null}- Comp:{Comp == null} - State:{Comp?.Data.Repo == null}  - Session:{Comp?.Session == null} - Value:{Comp.Data.Repo == null} - Weapons:{Comp.Data.Repo?.Base.State.Weapons[WeaponId] == null}"); }
        }

        internal void UpdateRof()
        {
            var systemRate = System.RateOfFire * Comp.Data.Repo.Base.Set.RofModifier;
            var barrelRate = System.BarrelSpinRate * Comp.Data.Repo.Base.Set.RofModifier;
            var heatModifier = MathHelper.Lerp(1f, .25f, State.Heat / System.MaxHeat);

            systemRate *= CurrentlyDegrading ? heatModifier : 1;

            if (systemRate < 1)
                systemRate = 1;

            RateOfFire = (int)systemRate;
            BarrelSpinRate = (int)barrelRate;
            TicksPerShot = (uint)(3600f / RateOfFire);
            if (System.HasBarrelRotation) UpdateBarrelRotation();
        }

        internal void TurnOnAV(object o)
        {
            if (Comp.MyCube == null || Comp.MyCube.MarkedForClose || Comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return;

            for (int j = 0; j < AnimationsSet[EventTriggers.TurnOn].Length; j++)
                PlayEmissives(AnimationsSet[EventTriggers.TurnOn][j]);

            PlayParticleEvent(EventTriggers.TurnOn, true, Vector3D.DistanceSquared(Comp.Session.CameraPos, MyPivotPos), null);
        }

        internal void TurnOffAv(object o)
        {
            if (Comp.MyCube == null || Comp.MyCube.MarkedForClose || Comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return;

            for (int j = 0; j < AnimationsSet[EventTriggers.TurnOff].Length; j++)
                PlayEmissives(AnimationsSet[EventTriggers.TurnOff][j]);

            PlayParticleEvent(EventTriggers.TurnOff, true, Vector3D.DistanceSquared(Comp.Session.CameraPos, MyPivotPos), null);
        }

        internal void SetWeaponDps(object o = null) // Need to test client sends MP request and receives response
        {
            if (System.DesignatorWeapon) return;

            var newBase = 0f;

            if (ActiveAmmoDef.AmmoDef.Const.EnergyAmmo)
                newBase = ActiveAmmoDef.AmmoDef.Const.BaseDamage * Comp.Data.Repo.Base.Set.DpsModifier;
            else
                newBase = ActiveAmmoDef.AmmoDef.Const.BaseDamage;

            if (ActiveAmmoDef.AmmoDef.Const.IsBeamWeapon)
                newBase *= Comp.Data.Repo.Base.Set.Overload;

            if (newBase < 0)
                newBase = 0;

            BaseDamage = newBase;
            var oldRequired = RequiredPower;
            var oldHeatPSec = Comp.HeatPerSecond;

            UpdateShotEnergy();
            UpdateRequiredPower();

            var multiplier = (ActiveAmmoDef.AmmoDef.Const.EnergyAmmo && ActiveAmmoDef.AmmoDef.Const.BaseDamage > 0) ? BaseDamage / ActiveAmmoDef.AmmoDef.Const.BaseDamage : 1;

            var dpsMulti = multiplier;

            if (BaseDamage > ActiveAmmoDef.AmmoDef.Const.BaseDamage)
                multiplier *= multiplier;

            HeatPShot = System.HeatPerShot * multiplier;

            RequiredPower *= multiplier;

            TicksPerShot = (uint)(3600f / RateOfFire);

            var oldDps = Dps;
            var oldMaxCharge = MaxCharge;

            if (ActiveAmmoDef.AmmoDef.Const.MustCharge)
                MaxCharge = ActiveAmmoDef.AmmoDef.Const.ChargSize * multiplier;

            Dps = ActiveAmmoDef.AmmoDef.Const.PeakDps * dpsMulti;

            var newHeatPSec = (60f / TicksPerShot) * HeatPShot * System.BarrelsPerShot;

            var heatDif = oldHeatPSec - newHeatPSec;
            var dpsDif = oldDps - Dps;
            var powerDif = oldRequired - RequiredPower;
            var chargeDif = oldMaxCharge - MaxCharge;

            if (IsShooting)
                Comp.CurrentDps -= dpsDif;

            if (DrawingPower)
            {
                Comp.Ai.RequestedWeaponsDraw -= powerDif;
                OldUseablePower = UseablePower;

                Comp.Ai.OverPowered = Comp.Ai.RequestedWeaponsDraw > 0 && Comp.Ai.RequestedWeaponsDraw > Comp.Ai.GridMaxPower;

                if (!Comp.Ai.OverPowered)
                {
                    UseablePower = RequiredPower;
                    DrawPower(true);
                }
                else
                {
                    RecalcPower = true;
                    ResetPower = true;
                    ChargeDelayTicks = 0;
                }
            }
            else
                UseablePower = RequiredPower;

            Comp.HeatPerSecond -= heatDif;
            Comp.MaxRequiredPower -= ActiveAmmoDef.AmmoDef.Const.MustCharge ? chargeDif : powerDif;
            Comp.Ai.UpdatePowerSources = true;
        }

        internal bool SpinBarrel(bool spinDown = false)
        {
            var matrix = MuzzlePart.Entity.PositionComp.LocalMatrixRef * BarrelRotationPerShot[BarrelRate];
            MuzzlePart.Entity.PositionComp.SetLocalMatrix(ref matrix, null, true);

            if (PlayTurretAv && RotateEmitter != null && !RotateEmitter.IsPlaying)
            { 
                RotateEmitter?.PlaySound(RotateSound, true, false, false, false, false, false);
            }

            if (_spinUpTick <= Comp.Session.Tick && spinDown)
            {
                _spinUpTick = Comp.Session.Tick + _ticksBeforeSpinUp;
                BarrelRate--;
            }

            if (BarrelRate < 0)
            {
                BarrelRate = 0;
                BarrelSpinning = false;

                if (PlayTurretAv && RotateEmitter != null && RotateEmitter.IsPlaying)
                    RotateEmitter.StopSound(true);
            }
            else BarrelSpinning = true;

            if (!spinDown)
            {
                if (BarrelRate < 9)
                {
                    if (_spinUpTick <= Comp.Session.Tick)
                    {
                        BarrelRate++;
                        _spinUpTick = Comp.Session.Tick + _ticksBeforeSpinUp;
                    }
                    return false;
                }
            }

            return true;
        }
    }
}
