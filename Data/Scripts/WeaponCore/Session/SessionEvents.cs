using System;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Weapons;
using SpaceEngineers.Game.ModAPI;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
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
                var cube = myEntity as MyCubeBlock;

                var placer = myEntity as IMyBlockPlacerBase;
                if (placer != null && Placer == null) Placer = placer;

                if (myEntity.IsPreview || cube == null || cube.CubeGrid.IsPreview) return;
               
                if (!Inited)
                    lock (InitObj)
                        Init();

                var isCore = false;
                if (myEntity is IMyUpgradeModule || myEntity is IMyLargeMissileTurret)
                {
                    if (!UpgradeControls && myEntity is IMyUpgradeModule)
                    {
                        lock (InitObj)
                        {
                            if (!UpgradeControls)
                                MyAPIGateway.Utilities.InvokeOnGameThread(CreateTerminalUI<IMyUpgradeModule>);
                        }
                        UpgradeControls = true;
                    }
                    if (!TurretControls && myEntity is IMyUpgradeModule)
                    {
                        lock (InitObj)
                        {
                            if (!TurretControls)
                                MyAPIGateway.Utilities.InvokeOnGameThread(CreateTerminalUI<IMyLargeMissileTurret>);
                        }
                        TurretControls = true;
                    }
                    if (!WeaponPlatforms.ContainsKey(cube.BlockDefinition.Id.SubtypeId)) return;
                    isCore = true;

                    //Log.Line("here");

                    using (myEntity.Pin())
                    {
                        if (myEntity.MarkedForClose) return;
                        GridAi gridAi;
                        if (!GridTargetingAIs.TryGetValue(cube.CubeGrid, out gridAi))
                        {
                            gridAi = new GridAi(cube.CubeGrid);
                            GridTargetingAIs.TryAdd(cube.CubeGrid, gridAi);
                        }
                        var weaponComp = new WeaponComponent(gridAi, cube);
                        if (gridAi != null && gridAi.WeaponBase.TryAdd(cube, weaponComp))
                        {
                            if(!gridAi.WeaponCounter.ContainsKey(cube.BlockDefinition.Id.SubtypeId))
                                gridAi.WeaponCounter.TryAdd(cube.BlockDefinition.Id.SubtypeId, new GridAi.WeaponCount());

                            CompsToStart.Enqueue(weaponComp);
                        }
                    }
                }

                //replace Targeting on all grids to improve lock speed, and handle grid locking
                var targeting = cube.CubeGrid?.Components?.Get<MyGridTargeting>() as CoreTargeting;

                if (!isCore && targeting == null && cube.CubeGrid != null)
                {
                    targeting = new CoreTargeting();
                    cube.CubeGrid.Components.Remove<MyGridTargeting>();
                    cube.CubeGrid.Components.Add<MyGridTargeting>(targeting);
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

        private void OnPrefabSpawn(long entityId, string prefabName)
        {
            var grid = MyEntities.GetEntityById(entityId) as MyCubeGrid;

            if (grid == null) return;

            var cubes = grid.GetFatBlocks();

            foreach (var cube in cubes)
            {

                if (cube is IMyLargeMissileTurret || cube is IMyUpgradeModule)
                    PrefabCubesToStart.Enqueue(cube);

            }
        }
    }
}
