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

        public void UpdateTurretInput() {

            List<MyKeys> pressedKeys = new List<MyKeys>();
            MyAPIGateway.Input.GetPressedKeys(pressedKeys);

            var currentEnt = Session.Instance.ControlledEntity as MyCockpit;

            var grid = currentEnt.CubeGrid;
            
            for (int i = 0; i < pressedKeys.Count; i++) {
                if (pressedKeys[i].ToString() == "F")
                {
                    Session.Instance.ControlingWeaponCam = false;
                    Session.Instance.ControlledWeapon = null;
                    Comp.Controlling = false;

                    MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(MyControlsSpace.ROTATION_DOWN.String, MyAPIGateway.Session.Player.IdentityId, true);

                    MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(MyControlsSpace.ROTATION_LEFT.String, MyAPIGateway.Session.Player.IdentityId, true);

                    MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(MyControlsSpace.ROTATION_RIGHT.String, MyAPIGateway.Session.Player.IdentityId, true);

                    MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(MyControlsSpace.ROTATION_UP.String, MyAPIGateway.Session.Player.IdentityId, true);
                }
            }

            //MyAPIGateway.Input.GetMouseX;
            //MyAPIGateway.Input.GetMouseXForGamePlay;
        }

        public void AimBarrel(double azimuthChange, double elevationChange)
        {
            float absAzChange;
            float absElChange;

            bool rAz = false;
            bool rEl = false;

            //AzimuthPart.Item1.Render.RemoveRenderObjects();

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
                AzimuthPart.Item1.PositionComp.LocalMatrix *= (Matrix.CreateTranslation(-AzimuthPart.Item2) * Matrix.CreateRotationY(MathHelper.ToRadians((float)azimuthChange)) * Matrix.CreateTranslation(AzimuthPart.Item2));

            if (absElChange >= System.ElStep)
            {
                if (rEl)
                    ElevationPart.Item1.PositionComp.LocalMatrix *= ElevationPart.Item4;
                else
                    ElevationPart.Item1.PositionComp.LocalMatrix *= ElevationPart.Item3;
            }
            else
                ElevationPart.Item1.PositionComp.LocalMatrix *= (Matrix.CreateTranslation(-ElevationPart.Item2) * Matrix.CreateRotationY(MathHelper.ToRadians((float)elevationChange)) * Matrix.CreateTranslation(ElevationPart.Item2));


        }

        public bool TurretHomePosition()
        {
            var turret = Comp.MyCube as IMyUpgradeModule;
            if (turret == null) return false;

            var azStep = System.AzStep;
            var elStep = System.ElStep;

            var az = Azimuth;
            var el = Elevation;

            if (az > 0)
                Azimuth = az - azStep > 0 ? az - azStep : 0;
            else if (az < 0)
                Azimuth = az + azStep < 0 ? az + azStep : 0;

            if (el > 0)
                Elevation = el - elStep > 0 ? el - elStep : 0;
            else if (el < 0)
                Elevation = el + elStep < 0 ? el + elStep : 0;


            AimBarrel(az - Azimuth, el - Elevation);


            if (Azimuth > 0 || Azimuth < 0 || Elevation > 0 || Elevation < 0) return false;

            return false;
        }
    }
}
