using System;
using VRageMath;
using WeaponCore.Support;
using static WeaponCore.Support.WeaponDefinition.AnimationDef.PartAnimationSetDef;

namespace WeaponCore.Platform
{
    public partial class Weapon
    {
        public void AimBarrel(double azimuthChange, double elevationChange, bool moveAz = true, bool moveEl = true)
        {
            LastTrackedTick = Comp.Session.Tick;

            if (AiOnlyWeapon)
            {
                if (moveAz)
                {
                    bool rAz = false;
                    double absAzChange;
                    if (azimuthChange < 0)
                    {
                        absAzChange = azimuthChange * -1d;
                        rAz = true;
                    }
                    else
                        absAzChange = azimuthChange;

                    if (System.TurretMovement == WeaponSystem.TurretType.Full || System.TurretMovement == WeaponSystem.TurretType.AzimuthOnly)
                    {
                        var azMatrix = AzimuthPart.Entity.PositionComp.LocalMatrix;

                        if (absAzChange >= System.AzStep)
                        {
                            

                            if (rAz)
                                azMatrix *= AzimuthPart.RevFullRotationStep;
                            else
                                azMatrix *= AzimuthPart.FullRotationStep;
                        }
                        else
                        {
                            azMatrix *= (AzimuthPart.ToTransformation * Matrix.CreateFromAxisAngle(AzimuthPart.RotationAxis, (float)-azimuthChange) * AzimuthPart.FromTransformation);
                        }

                        AzimuthPart.Entity.PositionComp.SetLocalMatrix(ref azMatrix, null, true);
                    }
                }

                if (moveEl && (System.TurretMovement == WeaponSystem.TurretType.Full || System.TurretMovement == WeaponSystem.TurretType.ElevationOnly))
                {
                    bool rEl = false;
                    double absElChange;
                    if (elevationChange < 0)
                    {
                        absElChange = elevationChange * -1d;
                        rEl = true;
                    }
                    else
                        absElChange = elevationChange;

                    var elMatrix = ElevationPart.Entity.PositionComp.LocalMatrix;

                    if (absElChange >= System.ElStep)
                    {
                        if (rEl)
                            elMatrix *= ElevationPart.RevFullRotationStep;
                        else
                            elMatrix *= ElevationPart.FullRotationStep;
                    }
                    else
                    {
                        elMatrix *= (ElevationPart.ToTransformation * Matrix.CreateFromAxisAngle(ElevationPart.RotationAxis, (float)elevationChange) * ElevationPart.FromTransformation);
                    }

                    ElevationPart.Entity.PositionComp.SetLocalMatrix(ref elMatrix, null, true);
                }
            }
            else
            {   
                if (moveEl)
                    Comp.TurretBase.Elevation = (float)Elevation;

                if (moveAz)
                    Comp.TurretBase.Azimuth = (float)Azimuth;
            }

        }

        public void TurretHomePosition(object o = null)
        {
            if (Comp == null || State == null || Target == null || Comp.MyCube == null) return;
            using (Comp.MyCube.Pin())
            {
                if (Comp.MyCube.MarkedForClose || Comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return;

                if (State.ManualShoot != TerminalActionState.ShootOff || Comp.UserControlled || Target.HasTarget)
                {
                    ReturingHome = false;
                    return;
                }

                var userControlled = o != null && (bool)o;
                if (userControlled && Comp.BaseType == WeaponComponent.BlockType.Turret && Comp.TurretBase != null)
                {
                    Azimuth = Comp.TurretBase.Azimuth;
                    Elevation = Comp.TurretBase.Elevation;
                }
                else if (!userControlled)
                {
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


                    AimBarrel(oldAz - Azimuth, oldEl - Elevation);
                }

                if (Azimuth > 0 || Azimuth < 0 || Elevation > 0 || Elevation < 0)
                {
                    ReturingHome = true;
                    IsHome = false;
                    Comp.Session.FutureEvents.Schedule(TurretHomePosition, null, (userControlled ? 300u : 1u));
                }
                else
                {
                    IsHome = true;
                    ReturingHome = false;
                }
            }
        }

        internal void UpdatePivotPos()
        {
            if (PosChangedTick == Comp.Session.Tick || AzimuthPart?.Entity?.Parent == null || ElevationPart?.Entity == null || MuzzlePart?.Entity == null || Comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return;
            PosChangedTick = Comp.Session.Tick;

            if (AzimuthOnBase)
                Comp.CubeMatrix = Comp.MyCube.PositionComp.WorldMatrix;

            var azimuthMatrix = AzimuthPart.Entity.PositionComp.WorldMatrix;
            var elevationMatrix = ElevationPart.Entity.PositionComp.WorldMatrix;
            var weaponCenter = MuzzlePart.Entity.PositionComp.WorldMatrix.Translation;
            var centerTestPos = azimuthMatrix.Translation + (azimuthMatrix.Down * 1);

            MyPivotUp = azimuthMatrix.Up;
            MyPivotDir = elevationMatrix.Forward;

            if (System.TurretMovement == WeaponSystem.TurretType.ElevationOnly)
            {
                var forward = Vector3D.Cross(elevationMatrix.Left, MyPivotUp);
                WeaponConstMatrix = new MatrixD { Forward = forward, Up = MyPivotUp, Left = elevationMatrix.Left };
            }
            else
            {
                var forward = AzimuthOnBase ? Comp.CubeMatrix.Forward : AzimuthPart.Entity.Parent.WorldMatrix.Forward;
                var left = Vector3D.Cross(MyPivotUp, forward);
                WeaponConstMatrix = new MatrixD { Forward = forward, Up = MyPivotUp, Left = left };
            }

            var axis = Vector3D.Cross(MyPivotUp, MyPivotDir);
            if (Vector3D.IsZero(axis))
                MyPivotPos = centerTestPos;
            else
            {
                var perpDir2 = Vector3D.Cross(MyPivotDir, axis);
                var point1To2 = weaponCenter - centerTestPos;
                MyPivotPos = centerTestPos + Vector3D.Dot(point1To2, perpDir2) / Vector3D.Dot(MyPivotUp, perpDir2) * MyPivotUp;
            }


            if (!Vector3D.IsZero(AimOffset))
                MyPivotPos += Vector3D.Rotate(AimOffset, new MatrixD { Forward = MyPivotDir, Left = elevationMatrix.Left, Up = elevationMatrix.Up });

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
                var currentHeat = State.Sync.Heat;
                currentHeat = currentHeat - ((float)HsRate / 3) > 0 ? currentHeat - ((float)HsRate / 3) : 0;
                var set = currentHeat - LastHeat > 0.001 || currentHeat - LastHeat < 0.001;

                Timings.LastHeatUpdateTick = Comp.Session.Tick;

                if (!Comp.Session.DedicatedServer)
                {
                    var heatPercent = currentHeat / System.MaxHeat;

                    if (set && heatPercent > .33)
                    {
                        if (heatPercent > 1) heatPercent = 1;

                        heatPercent -= .33f;

                        var intensity = .7f * heatPercent;

                        var color = Comp.Session.HeatEmissives[(int)(heatPercent * 100)];

                        for(int i = 0; i < HeatingParts.Count; i++)
                            HeatingParts[i]?.SetEmissiveParts("Heating", color, intensity);
                    }
                    else if (set)
                        for(int i = 0; i < HeatingParts.Count; i++)
                            HeatingParts[i]?.SetEmissiveParts("Heating", Color.Transparent, 0);

                    LastHeat = currentHeat;
                }

                if (set && System.DegRof && State.Sync.Heat >= (System.MaxHeat * .8))
                {
                    var systemRate = System.RateOfFire * Comp.Set.Value.RofModifier;
                    var barrelRate = System.BarrelSpinRate * Comp.Set.Value.RofModifier;
                    var heatModifier = MathHelper.Lerp(1f, .25f, State.Sync.Heat / System.MaxHeat);

                    systemRate *= heatModifier;

                    if (systemRate < 1)
                        systemRate = 1;

                    RateOfFire = (int)systemRate;
                    BarrelSpinRate = (int)barrelRate;
                    TicksPerShot = (uint)(3600f / RateOfFire);

                    if (System.HasBarrelRotation) UpdateBarrelRotation();
                    CurrentlyDegrading = true;
                }
                else if (set && CurrentlyDegrading)
                {
                    CurrentlyDegrading = false;
                    RateOfFire = (int)(System.RateOfFire * Comp.Set.Value.RofModifier);
                    BarrelSpinRate = (int)(System.BarrelSpinRate * Comp.Set.Value.RofModifier);
                    TicksPerShot = (uint)(3600f / RateOfFire);

                    if (System.HasBarrelRotation) UpdateBarrelRotation();
                }

                if (_fakeHeatTick * 30 == 60)
                {
                    Comp.CurrentHeat = Comp.CurrentHeat >= HsRate ? Comp.CurrentHeat - HsRate : 0;
                    State.Sync.Heat = State.Sync.Heat >= HsRate ? State.Sync.Heat - HsRate : 0;

                    if (State.Sync.Overheated && State.Sync.Heat <= (System.MaxHeat * System.WepCoolDown))
                    {
                        //ShootDelayTick = CurLgstAnimPlaying.Reverse ? (uint)CurLgstAnimPlaying.CurrentMove : (uint)((CurLgstAnimPlaying.NumberOfMoves - 1) - CurLgstAnimPlaying.CurrentMove);
                        if (CurLgstAnimPlaying != null)
                            Timings.ShootDelayTick = Comp.Session.Tick + (CurLgstAnimPlaying.Reverse ? (uint)CurLgstAnimPlaying.CurrentMove : (uint)((CurLgstAnimPlaying.NumberOfMoves - 1) - CurLgstAnimPlaying.CurrentMove));
                        
                        EventTriggerStateChanged(EventTriggers.Overheated, false);
                        State.Sync.Overheated = false;
                    }

                    _fakeHeatTick = -1;
                }

                //Log.Line($"currentHeat :{currentHeat} _fakeHeatTick: {_fakeHeatTick}");

                if (State.Sync.Heat > 0)
                {
                    _fakeHeatTick++;
                    Comp.Session.FutureEvents.Schedule(UpdateWeaponHeat, null, 20);
                }
                else
                {
                    _fakeHeatTick = 0;
                    HeatLoopRunning = false;
                    Timings.LastHeatUpdateTick = 0;
                }
            }
            catch (Exception ex) { Log.Line($"Exception in UpdateWeaponHeat: {ex} - {System == null}- Comp:{Comp == null} - State:{Comp?.State == null} - Set:{Comp?.Set == null} - Session:{Comp?.Session == null} - Value:{Comp?.State?.Value == null} - Weapons:{Comp?.State?.Value?.Weapons[WeaponId] == null}"); }
        }

        internal void SetWeaponDps(object o = null)
        {
            var newBase = 0f;

            if (ActiveAmmoDef.AmmoDef.Const.EnergyAmmo)
                newBase = ActiveAmmoDef.AmmoDef.Const.BaseDamage * Comp.Set.Value.DpsModifier;
            else
                newBase = ActiveAmmoDef.AmmoDef.Const.BaseDamage;

            if (ActiveAmmoDef.AmmoDef.Const.IsBeamWeapon)
                newBase *= Comp.Set.Value.Overload;

            if (newBase < 0)
                newBase = 0;

            BaseDamage = newBase;
            var oldRequired = RequiredPower;
            var oldHeatPSec = (60f / TicksPerShot) * HeatPShot * System.BarrelsPerShot;

            UpdateShotEnergy();
            UpdateRequiredPower();

            var mulitplier = (ActiveAmmoDef.AmmoDef.Const.EnergyAmmo && ActiveAmmoDef.AmmoDef.Const.BaseDamage > 0) ? BaseDamage / ActiveAmmoDef.AmmoDef.Const.BaseDamage : 1;

            var dpsMulti = mulitplier;

            if (BaseDamage > ActiveAmmoDef.AmmoDef.Const.BaseDamage)
                mulitplier *= mulitplier;

            HeatPShot = System.HeatPerShot * mulitplier;

            RequiredPower *= mulitplier;

            TicksPerShot = (uint)(3600f / RateOfFire);
            TimePerShot = (3600d / RateOfFire);

            var oldDps = Dps;
            var oldMaxCharge = MaxCharge;

            if (ActiveAmmoDef.AmmoDef.Const.MustCharge)
                MaxCharge = ActiveAmmoDef.AmmoDef.Const.EnergyMagSize * mulitplier;

            Dps = ActiveAmmoDef.AmmoDef.Const.PeakDps * dpsMulti;

            var heatPShot = (60f / TicksPerShot) * HeatPShot * System.BarrelsPerShot;
            var heatDif = oldHeatPSec - heatPShot;
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
                    Timings.ChargeDelayTicks = 0;
                }
            }
            else
                UseablePower = RequiredPower;

            Comp.HeatPerSecond -= heatDif;
            Comp.MaxRequiredPower -= ActiveAmmoDef.AmmoDef.Const.MustCharge ? chargeDif : powerDif;

        }

        internal void SpinBarrel(bool spinDown = false)
        {
            var matrix = MuzzlePart.Entity.PositionComp.LocalMatrix * BarrelRotationPerShot[BarrelRate];
            MuzzlePart.Entity.PositionComp.SetLocalMatrix(ref matrix, null, true);

            if (PlayTurretAv && RotateEmitter != null && !RotateEmitter.IsPlaying)
                RotateEmitter.PlaySound(RotateSound, true, false, false, false, false, false);

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
        }
    }
}
