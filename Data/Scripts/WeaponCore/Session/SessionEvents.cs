using System;
using Sandbox.Game.Entities;
using Sandbox.Game.Screens.Models;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Weapons;
using SpaceEngineers.Game.ModAPI;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageRender;
using WeaponCore.Support;

namespace WeaponCore
{
    public partial class Session
    {
        private void OnEntityCreate(MyEntity myEntity)
        {
            try
            {            
                var weaponBase = myEntity as IMyLargeMissileTurret;
                var placer = myEntity as IMyBlockPlacerBase;
                if (placer != null && Placer == null) Placer = placer;
                if (!Inited)
                    lock (InitObj)
                        Init();

                if (weaponBase != null)
                {
                    if (!Controls)
                    {
                        Controls = true;
                        lock (InitObj)
                            MyAPIGateway.Utilities.InvokeOnGameThread(CreateLogicElements);
                    }
                    InitComp(myEntity);
                }
            }
            catch (Exception ex) { Log.Line($"Exception in OnEntityCreate: {ex}"); }
        }

        private void MenuOpened(object obj)
        {
            var cockpit = ControlledEntity as MyCockpit;
            var remote = ControlledEntity as MyRemoteControl;

            if (cockpit != null && UpdateLocalAiAndCockpit())
                _futureEvents.Schedule(TurnWeaponShootOff, GridTargetingAIs[cockpit.CubeGrid], 1);

            if (remote != null)
                _futureEvents.Schedule(TurnWeaponShootOff, GridTargetingAIs[remote.CubeGrid], 1);
        }
    }
}
