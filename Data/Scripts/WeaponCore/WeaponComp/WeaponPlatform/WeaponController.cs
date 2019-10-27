using VRageMath;
using WeaponCore.Support;

namespace WeaponCore.Platform
{
    public partial class Weapon
    {
        public void AimBarrel(double azimuthChange, double elevationChange)
        {

            LastTrackedTick = Comp.Ai.Session.Tick;

            if (Comp.IsAiOnlyTurret)
            {
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
                    Elevation -= elevationChange;
                }

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
                    Azimuth -= azimuthChange;
                }
            }
            else
            {
                Comp.ControllableTurret.Elevation = (float)Elevation;
                Comp.ControllableTurret.Azimuth = (float)Azimuth;
            }

        }

        public bool TurretHomePosition()
        {
            if (Comp.AiOnlyTurret == null && Comp.ControllableTurret == null) return false;

            var azStep = System.AzStep;
            var elStep = System.ElStep;

            var oldAz = Azimuth;
            var oldEl = Elevation;

            double newAz = 0;
            double newEl = 0;

            if (oldAz > 0)
                newAz = oldAz - azStep > 0 ? oldAz - azStep : 0;
            else if (oldAz < 0)
                newAz = oldAz + azStep < 0 ? oldAz + azStep : 0;

            if (oldEl > 0)
                newEl = oldEl - elStep > 0 ? oldEl - elStep : 0;
            else if (oldEl < 0)
                newEl = oldEl + elStep < 0 ? oldEl + elStep : 0;


            AimBarrel(oldAz - newAz, oldEl - newEl);


            if (Azimuth > 0 || Azimuth < 0 || Elevation > 0 || Elevation < 0) return true;

            return false;
        }

        internal void UpdatePivotPos()
        {
            var azimuthMatrix = AzimuthPart.Item1.PositionComp.WorldMatrix;
            var elevationMatrix = ElevationPart.Item1.PositionComp.WorldMatrix;
            var weaponCenter = MuzzlePart.Item1.PositionComp.WorldMatrix.Translation;
            var centerTestPos = azimuthMatrix.Translation + (azimuthMatrix.Down * 1);

            MyPivotUp = azimuthMatrix.Up;
            MyPivotDir = elevationMatrix.Forward;
            MyPivotPos = UtilsStatic.GetClosestPointOnLine1(centerTestPos, MyPivotUp, weaponCenter, MyPivotDir);
            if (!Vector3D.IsZero(AimOffset))
                MyPivotPos += Vector3D.Rotate(AimOffset, new MatrixD { Forward = MyPivotDir, Left = elevationMatrix.Left, Up = elevationMatrix.Up });

            if (!Comp.Debug) return;
            MyCenterTestLine = new LineD(centerTestPos, centerTestPos + (MyPivotUp * 20));
            MyBarrelTestLine = new LineD(weaponCenter, weaponCenter + (MyPivotDir * 18));
            MyPivotTestLine = new LineD(MyPivotPos + (elevationMatrix.Left * 10), MyPivotPos - (elevationMatrix.Left * 10));
            MyAimTestLine = new LineD(MyPivotPos, MyPivotPos + (MyPivotDir * 20));
            MyPivotDirLine = new LineD(MyPivotPos, MyPivotPos + (MyPivotDir * 19));
            if (!Target.Expired)
                MyShootAlignmentLine = new LineD(MyPivotPos, TargetPos);
        }
    }
}
