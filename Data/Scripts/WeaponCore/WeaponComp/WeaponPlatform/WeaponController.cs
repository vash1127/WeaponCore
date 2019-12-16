using VRageMath;
using WeaponCore.Support;

namespace WeaponCore.Platform
{
    public partial class Weapon
    {
        public void AimBarrel(double azimuthChange, double elevationChange)
        {
            LastTrackedTick = Comp.Ai.Session.Tick;

            if (AiOnlyWeapon)
            {
                Log.Line($"Azimuth: {Azimuth} Elevation: {Elevation} azimuthChange: {azimuthChange} elevationChange: {elevationChange}");
                double absAzChange;
                double absElChange;

                bool rAz = false;
                bool rEl = false;

                if (azimuthChange < 0)
                {
                    absAzChange = azimuthChange * -1d;
                    rAz = true;
                }
                else
                    absAzChange = azimuthChange;

                if (elevationChange < 0)
                {
                    absElChange = elevationChange * -1d;
                    rEl = true;
                }
                else
                    absElChange = elevationChange;

                if (System.TurretMovement == WeaponSystem.TurretType.Full || System.TurretMovement == WeaponSystem.TurretType.AzimuthOnly)
                {
                    if (absAzChange >= System.AzStep)
                    {
                        if (rAz)
                            AzimuthPart.Item1.PositionComp.LocalMatrix *= AzimuthPart.Item5;
                        else
                            AzimuthPart.Item1.PositionComp.LocalMatrix *= AzimuthPart.Item4;
                    }
                    else
                    {
                        AzimuthPart.Item1.PositionComp.LocalMatrix *= (AzimuthPart.Item2 * Matrix.CreateRotationY((float)-azimuthChange) * AzimuthPart.Item3);
                    }
                }

                if (System.TurretMovement == WeaponSystem.TurretType.Full || System.TurretMovement == WeaponSystem.TurretType.ElevationOnly)
                {
                    if (absElChange >= System.ElStep)
                    {
                        if (rEl)
                            ElevationPart.Item1.PositionComp.LocalMatrix *= ElevationPart.Item5;
                        else
                            ElevationPart.Item1.PositionComp.LocalMatrix *= ElevationPart.Item4;
                    }
                    else
                    {
                        ElevationPart.Item1.PositionComp.LocalMatrix *= (ElevationPart.Item2 * Matrix.CreateRotationX((float)-elevationChange) * ElevationPart.Item3);
                    }
                }
            }
            else
            {
                Comp.MissileBase.Elevation = (float)Elevation;
                Comp.MissileBase.Azimuth = (float)Azimuth;
            }

        }

        public void TurretHomePosition()
        {
            ReturnHome = false;
            if (Comp.SorterBase == null && Comp.MissileBase == null) return;

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


            if (Azimuth > 0 || Azimuth < 0 || Elevation > 0 || Elevation < 0) ReturnHome = true;

        }

        internal void HomeTurret(object o)
        {
            TurretHomePosition();

            //Log.Line($"{weapon.System.WeaponName}: homing");
            ReturnHome = ReturnHome && Comp.State.Value.Weapons[WeaponId].ManualShoot == Weapon.TerminalActionState.ShootOff && !Comp.Gunner && Target.Expired;

            if (ReturnHome)
                Comp.Ai.Session.FutureEvents.Schedule(HomeTurret, null, 1);

            //weapon.ReturnHome = weapon.Comp.ReturnHome = weapon.Comp.Ai.ReturnHome = true;
        }

        internal void UpdatePivotPos()
        {
            if (Comp.MatrixUpdateTick < Comp.Ai.Session.Tick)
            {
                Comp.MatrixUpdateTick = Comp.Ai.Session.Tick;
                Comp.CubeMatrix = Comp.MyCube.PositionComp.WorldMatrix;
            }

            var azimuthMatrix = AzimuthPart.Item1.PositionComp.WorldMatrix;
            var elevationMatrix = ElevationPart.Item1.PositionComp.WorldMatrix;
            var weaponCenter = MuzzlePart.Item1.PositionComp.WorldMatrix.Translation;
            var centerTestPos = azimuthMatrix.Translation + (azimuthMatrix.Down * 1);


            MyPivotUp = azimuthMatrix.Up;
            MyPivotDir = elevationMatrix.Forward;
            var forward = Comp.CubeMatrix.Forward;
            var left = Vector3D.Cross(MyPivotUp, forward);

            if (System.ElevationOnly)//turrets limited to elevation only, makes constraints check whats in front of weapon not cube forward within elevation limits
            {
                forward = Vector3D.Cross(elevationMatrix.Left, MyPivotUp);
                WeaponConstMatrix = new MatrixD { Forward = forward, Up = MyPivotUp, Left = elevationMatrix.Left };
            }
            else // azimuth only and full turret already have the right matrix
                WeaponConstMatrix = new MatrixD { Forward = forward, Up = MyPivotUp, Left = left };

            MyPivotPos = UtilsStatic.GetClosestPointOnLine1(centerTestPos, MyPivotUp, weaponCenter, MyPivotDir);
            if (!Vector3D.IsZero(AimOffset))
                MyPivotPos += Vector3D.Rotate(AimOffset, new MatrixD { Forward = MyPivotDir, Left = elevationMatrix.Left, Up = elevationMatrix.Up });

            if (!Comp.Debug) return;
            MyCenterTestLine = new LineD(centerTestPos, centerTestPos + (MyPivotUp * 20));
            MyBarrelTestLine = new LineD(weaponCenter, weaponCenter + (MyPivotDir * 18));
            MyPivotTestLine = new LineD(MyPivotPos + (left * 10), MyPivotPos - (left * 10));
            MyAimTestLine = new LineD(MyPivotPos, MyPivotPos + (MyPivotDir * 20));
            if (!Target.Expired)
                MyShootAlignmentLine = new LineD(MyPivotPos, TargetPos);
        }
    }
}
