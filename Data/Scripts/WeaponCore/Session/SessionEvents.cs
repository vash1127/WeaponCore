using System;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Weapons;
using SpaceEngineers.Game.ModAPI;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
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

                if (cube == null) return;

                var targeting = cube.CubeGrid?.Components?.Get<MyGridTargeting>() as CoreTargeting;

                if (targeting == null && cube.CubeGrid != null) {
                    targeting = new CoreTargeting();
                    cube.CubeGrid.Components.Remove<MyGridTargeting>();
                    cube.CubeGrid.Components.Add<MyGridTargeting>(targeting);
                }
               

                var weaponBase = myEntity as IMyUpgradeModule;
                var placer = myEntity as IMyBlockPlacerBase;
                if (placer != null && Placer == null) Placer = placer;


                if (!Inited)
                    lock (InitObj)
                        Init();

                if (weaponBase != null)
                {
                    if (!Controls)
                        lock(InitObj)
                            MyAPIGateway.Utilities.InvokeOnGameThread(CreateLogicElements);

                    
                    if (!WeaponPlatforms.ContainsKey(cube.BlockDefinition.Id.SubtypeId)) return;

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
                        var weaponComp = new WeaponComponent(gridAi, cube, weaponBase);
                        if (gridAi != null && gridAi.WeaponBase.TryAdd(cube, weaponComp))
                        {
                            if(!gridAi.WeaponCounter.ContainsKey(cube.BlockDefinition.Id.SubtypeId))
                                gridAi.WeaponCounter.TryAdd(cube.BlockDefinition.Id.SubtypeId, new GridAi.WeaponCount());

                            CompsToStart.Enqueue(weaponComp);
                        }
                    }
                }
            }
            catch (Exception ex) { Log.Line($"Exception in OnEntityCreate: {ex}"); }
        }

        private void MenuOpened(object obj)
        {
            var cockpit = ControlledEntity as MyCockpit;
            var remote = ControlledEntity as MyRemoteControl;

            if (cockpit != null && UpdateLocalAiAndCockpit())
                FutureEventsManager.Schedule(TurnWeaponShootOff, GridTargetingAIs[cockpit.CubeGrid], 1);

            if (remote != null)
                FutureEventsManager.Schedule(TurnWeaponShootOff, GridTargetingAIs[remote.CubeGrid], 1);
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
