using System;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
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
        internal void OnEntityCreate(MyEntity myEntity)
        {
            try
            {
                var cube = myEntity as MyCubeBlock;

                var placer = myEntity as IMyBlockPlacerBase;
                if (placer != null && Placer == null) Placer = placer;

                if (myEntity.IsPreview || cube == null || cube.CubeGrid.IsPreview) return;

                //replace Targeting on all grids to improve lock speed, and handle grid locking
                var targeting = cube.CubeGrid?.Components?.Get<MyGridTargeting>() as CoreTargeting;

                if (targeting == null && cube.CubeGrid != null)
                {
                    targeting = new CoreTargeting();
                    cube.CubeGrid.Components.Remove<MyGridTargeting>();
                    cube.CubeGrid.Components.Add<MyGridTargeting>(targeting);
                }

                if (!Inited)
                    lock (InitObj)
                        Init();

                

                if (myEntity is IMyConveyorSorter || myEntity is IMyLargeMissileTurret)
                {
                    if (!UpgradeControls && myEntity is IMyConveyorSorter)
                    {
                        lock (InitObj)
                        {
                            //if (!UpgradeControls)
                                //MyAPIGateway.Utilities.InvokeOnGameThread(CreateTerminalUI<IMyConveyorSorter>);
                        }
                        UpgradeControls = true;
                    }
                    if (!TurretControls && myEntity is IMyLargeMissileTurret)
                    {
                        lock (InitObj)
                        {
                            if (!TurretControls)
                                MyAPIGateway.Utilities.InvokeOnGameThread(CreateTerminalUI<IMyLargeTurretBase>);
                        }
                        TurretControls = true;
                    }

                    if (!WeaponPlatforms.ContainsKey(cube.BlockDefinition.Id.SubtypeId)) return;
                    
                    InitComp(cube);
                }
            }
            catch (Exception ex) { Log.Line($"Exception in OnEntityCreate: {ex}"); }
        }

        private void InitComp(MyCubeBlock cube)
        {
            using (cube.Pin())
            {
                if (cube.MarkedForClose) return;
                GridAi gridAi;
                if (!GridTargetingAIs.TryGetValue(cube.CubeGrid, out gridAi))
                {
                    gridAi = new GridAi(cube.CubeGrid);
                    GridTargetingAIs.TryAdd(cube.CubeGrid, gridAi);
                }
                var weaponComp = new WeaponComponent(gridAi, cube);
                if (gridAi != null && gridAi.WeaponBase.TryAdd(cube, weaponComp))
                {
                    if (!gridAi.WeaponCounter.ContainsKey(cube.BlockDefinition.Id.SubtypeId))
                        gridAi.WeaponCounter.TryAdd(cube.BlockDefinition.Id.SubtypeId, new GridAi.WeaponCount());

                    CompsToStart.Add(weaponComp);
                    CompsToStart.ApplyAdditions();
                }
            }
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
