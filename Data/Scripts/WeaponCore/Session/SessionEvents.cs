using System;
using Sandbox.Game.Entities;
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

                    var cube = (MyCubeBlock)myEntity;

                    if (cube.BlockDefinition.Id.SubtypeId == MyStringHash.NullOrEmpty)
                    {
                        Log.Line($"[OnEntityCreateEmptySubTypeId] typeId:{cube.BlockDefinition.Id.TypeId} - name:{cube.DebugName}");
                        PrefabCubesToStart.Enqueue(cube);
                        return;
                    }

                    InitComp(myEntity);
                }
            }
            catch (Exception ex) { Log.Line($"Exception in OnEntityCreate: {ex}"); }
        }

        private void InitComp(MyEntity myEntity)
        {
            var cube = (MyCubeBlock)myEntity;
            var weaponBase = myEntity as IMyLargeMissileTurret;
            //Log.Line("OnEntityCreate weapon");
            if (myEntity.IsPreview || cube.CubeGrid.IsPreview) return;
            Log.Line($"[InitComp] SubtypeId:{cube.BlockDefinition.Id.SubtypeId} - typeId:{cube.BlockDefinition.Id.TypeId} - name:{cube.DebugName}");
            if (!WeaponPlatforms.ContainsKey(cube.BlockDefinition.Id.SubtypeId)) return;

            //Log.Line("valid subtype");

            using (myEntity.Pin())
            {
                if (myEntity.MarkedForClose) return;
                //Log.Line("no marked close");
                GridAi gridAi;
                if (!GridTargetingAIs.TryGetValue(cube.CubeGrid, out gridAi))
                {
                    gridAi = new GridAi(cube.CubeGrid);
                    GridTargetingAIs.TryAdd(cube.CubeGrid, gridAi);
                    //Log.Line("new gridAi");
                }
                var weaponComp = new WeaponComponent(gridAi, cube, weaponBase);
                if (gridAi != null && gridAi.WeaponBase.TryAdd(cube, weaponComp))
                {
                    if (!gridAi.WeaponCounter.ContainsKey(cube.BlockDefinition.Id.SubtypeId))
                        gridAi.WeaponCounter.TryAdd(cube.BlockDefinition.Id.SubtypeId, new GridAi.WeaponCount());

                    CompsToStart.Enqueue(weaponComp);
                    //Log.Line($"CompsToStart: {cube.BlockDefinition.Id.TypeId} subtype: {cube.BlockDefinition.Id.SubtypeId}");
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

        private void OnPrefabSpawn(long entityId, string prefabName)
        {
            var grid = MyEntities.GetEntityById(entityId) as MyCubeGrid;

            if (grid == null) return;
            Log.Line($"OnPrefabSpawn: {entityId} - {grid.DebugName}");

            var cubes = grid.GetFatBlocks();

            foreach (var cube in cubes)
            {
                if (cube is IMyLargeMissileTurret || cube is IMyUpgradeModule)
                    PrefabCubesToStart.Enqueue(cube);
            }
        }


    }
}
