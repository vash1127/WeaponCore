using System;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Weapons;
using SpaceEngineers.Game.ModAPI;
using VRage.Collections;
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
                var grid = myEntity as MyCubeGrid;

                var placer = myEntity as IMyBlockPlacerBase;
                if (placer != null && Placer == null) Placer = placer;

                if (grid != null)
                    grid.AddedToScene += GridAddedToScene;

                if (cube == null) return;

                if (!Inited)
                    lock (InitObj)
                        Init();

                var sorter = myEntity as MyConveyorSorter;
                var missileTurret = myEntity as IMyLargeMissileTurret;
                if (sorter != null || missileTurret != null)
                {
                    if (!SorterControls && sorter != null)
                    {
                        lock (InitObj)
                        {
                            if (!SorterControls)
                                MyAPIGateway.Utilities.InvokeOnGameThread(CreateTerminalUI<IMyConveyorSorter>);
                            SorterControls = true;
                        }
                    }
                    if (!TurretControls && missileTurret != null)
                    {
                        lock (InitObj)
                        {
                            if (!TurretControls)
                                MyAPIGateway.Utilities.InvokeOnGameThread(CreateTerminalUI<IMyLargeTurretBase>);
                            TurretControls = true;
                        }
                    }
                    InitComp(cube);
                }
            }
            catch (Exception ex) { Log.Line($"Exception in OnEntityCreate: {ex}"); }
        }


        private void GridAddedToScene(MyEntity myEntity)
        {
            NewGrids.Enqueue(myEntity as MyCubeGrid);
        }

        private void AddGridToMap()
        {
            MyCubeGrid grid;
            while (NewGrids.TryDequeue(out grid))
            {
                //Log.Line($"added to grid");
                grid.OnFatBlockAdded += ToFatMap;
                grid.OnFatBlockRemoved += FromFatMap;
                grid.OnClose += RemoveGridFromMap;
                var fatMap = ConcurrentListPool.Get();
                fatMap.AddRange(grid.GetFatBlocks());
                GridToFatMap.Add(grid, fatMap);
            }
        }

        private void RemoveGridFromMap(MyEntity myEntity)
        {
            var grid = (MyCubeGrid)myEntity;
            MyConcurrentList<MyCubeBlock> list;
            if (GridToFatMap.TryRemove(grid, out list))
            {
                grid.OnFatBlockAdded -= ToFatMap;
                grid.OnFatBlockRemoved -= FromFatMap;
                grid.OnClose -= RemoveGridFromMap;
                list.Clear();
                ConcurrentListPool.Return(list);
                //Log.Line("grid removed and list cleaned");
            }
            else Log.Line($"grid not removed and list not cleaned");
        }

        private void ToFatMap(MyCubeBlock myCubeBlock)
        {
            //Log.Line("added to fat map");
            GridToFatMap[myCubeBlock.CubeGrid].Add(myCubeBlock);
        }

        private void FromFatMap(MyCubeBlock myCubeBlock)
        {
            //Log.Line("removed from fat map");
            GridToFatMap[myCubeBlock.CubeGrid].Remove(myCubeBlock);
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
