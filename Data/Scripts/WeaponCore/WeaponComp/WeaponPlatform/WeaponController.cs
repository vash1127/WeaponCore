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
            float absAzChange;
            float absElChange;

            Azimuth -= azimuthChange;
            Elevation -= elevationChange;

            AzimuthPart.Item1.PositionComp.LocalMatrix *= (Matrix.CreateTranslation(-AzimuthPart.Item2) * Matrix.CreateRotationY((float)-azimuthChange) * Matrix.CreateTranslation(AzimuthPart.Item2));

            ElevationPart.Item1.PositionComp.LocalMatrix *= (Matrix.CreateTranslation(-ElevationPart.Item2) * Matrix.CreateRotationX((float)-elevationChange) * Matrix.CreateTranslation(ElevationPart.Item2));

            bool rAz = false;
            bool rEl = false;

            /*
            if (azimuthChange < 0)
            {
                absAzChange = (float)azimuthChange * -1f;
                rAz = true;
            }
            else
                absAzChange = (float)azimuthChange;

            if (elevationChange < 0)
            {
                absElChange = (float)elevationChange * -1f;
                rEl = true;
            }
            else
                absElChange = (float)elevationChange;


            if (absAzChange >= System.AzStep)
            {
                if (rAz)
                    AzimuthPart.Item1.PositionComp.LocalMatrix *= AzimuthPart.Item4;
                else
                    AzimuthPart.Item1.PositionComp.LocalMatrix *= AzimuthPart.Item3;
            }
            else
            {
                AzimuthPart.Item1.PositionComp.LocalMatrix *= (Matrix.CreateTranslation(-AzimuthPart.Item2) * Matrix.CreateRotationY((float)-azimuthChange) * Matrix.CreateTranslation(AzimuthPart.Item2));
            }

            if (absElChange >= System.ElStep)
            {
                if (rEl)
                    ElevationPart.Item1.PositionComp.LocalMatrix *= ElevationPart.Item4;
                else
                    ElevationPart.Item1.PositionComp.LocalMatrix *= ElevationPart.Item3;
            }
            else
            {
                ElevationPart.Item1.PositionComp.LocalMatrix *= (Matrix.CreateTranslation(-ElevationPart.Item2) * Matrix.CreateRotationX((float)-elevationChange) * Matrix.CreateTranslation(ElevationPart.Item2));
            }*/

        }

        public bool TurretHomePosition()
        {
            var turret = Comp.MyCube as IMyUpgradeModule;
            if (turret == null) return false;

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
    }
}
