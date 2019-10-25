using Sandbox.Common.ObjectBuilders;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using SpaceEngineers.Game.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Input;
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
            }
            else {
                Azimuth -= azimuthChange;
                Elevation -= elevationChange;
                Comp.ControllableTurret.Azimuth = (float)Azimuth;
                Comp.ControllableTurret.Elevation = (float)Elevation;
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
            var elevationComp = ElevationPart.Item1 != null ? ElevationPart.Item1.PositionComp : null;
            var azimuthComp = AzimuthPart.Item1 != null ? AzimuthPart.Item1.PositionComp : null;
            var weaponMatrix = elevationComp != null ? elevationComp.WorldMatrix : azimuthComp.WorldMatrix;

            var center = azimuthComp != null ? azimuthComp.WorldAABB.Center : weaponMatrix.Translation;
            var weaponCenter = weaponMatrix.Translation;

            //single axis weapons may have Elevation or Azimuth parts only, this always get weapon constant up
            MyPivotUp = weaponCenter - Comp.MyCube.PositionComp.WorldMatrix.Translation;
            MyPivotUp.X = 0;
            MyPivotUp.Z = 0;

            MyPivotDir = weaponMatrix.Forward;
            MyPivotLeft = weaponMatrix.Left;
            MyPivotMatrix = new MatrixD { Forward = MyPivotDir, Left = MyPivotLeft, Up = weaponMatrix.Up };
            MyWeaponConstMatrix = new MatrixD { Forward = MyPivotDir, Left = MyPivotLeft, Up = MyPivotUp };
            MyPivotPos = UtilsStatic.GetClosestPointOnLine1(center, MyPivotUp, weaponCenter, MyPivotDir) + Vector3D.Rotate(AimOffset, MyPivotMatrix);

            if (Comp.Debug)
            {
                MyCenterTestLine = new LineD(center, center + (MyPivotUp * 20));
                MyBarrelTestLine = new LineD(weaponCenter, weaponCenter + (MyPivotDir * 18));
                MyPivotTestLine = new LineD(MyPivotPos + (MyPivotLeft * 10), MyPivotPos - (MyPivotLeft * 10));
                MyAimTestLine = new LineD(MyPivotPos, MyPivotPos + (MyPivotDir * 20));
                MyPivotDirLine = new LineD(MyPivotPos, MyPivotPos + (MyPivotDir * 19));
                if (!Target.Expired)
                    MyShootAlignmentLine = new LineD(MyPivotPos, TargetPos);
            }
        }
    }
}
