using System;
using Sandbox.Game.Entities;
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
                //Log.Line($"added grid");

                var allFat = ConcurrentListPool.Get();
                allFat.AddRange(grid.GetFatBlocks());
                var fatMap = FatMapPool.Get();

                if (grid.Components.TryGet(out fatMap.Targeting))
                    fatMap.Targeting.AllowScanning = false;
                fatMap.Trash = true;

                fatMap.MyCubeBocks = allFat;
                GridToFatMap.Add(grid, fatMap);
                grid.OnFatBlockAdded += ToFatMap;
                grid.OnFatBlockRemoved += FromFatMap;
                grid.OnClose += RemoveGridFromMap;
                DirtyGrids.Add(grid);
            }
        }

        private void RemoveGridFromMap(MyEntity myEntity)
        {
            var grid = (MyCubeGrid)myEntity;
            FatMap fatMap;
            if (GridToFatMap.TryRemove(grid, out fatMap))
            {
                fatMap.MyCubeBocks.Clear();
                ConcurrentListPool.Return(fatMap.MyCubeBocks);
                fatMap.Trash = true;
                FatMapPool.Return(fatMap);
                grid.OnFatBlockAdded -= ToFatMap;
                grid.OnFatBlockRemoved -= FromFatMap;
                grid.OnClose -= RemoveGridFromMap;
                DirtyGrids.Add(grid);
                //Log.Line("grid removed and list cleaned");
            }
            else Log.Line($"grid not removed and list not cleaned");
        }

        private void ToFatMap(MyCubeBlock myCubeBlock)
        {
            //Log.Line("added to fat map");
            GridToFatMap[myCubeBlock.CubeGrid].MyCubeBocks.Add(myCubeBlock);
            DirtyGrids.Add(myCubeBlock.CubeGrid);
        }

        private void FromFatMap(MyCubeBlock myCubeBlock)
        {
            //Log.Line("removed from fat map");
            GridToFatMap[myCubeBlock.CubeGrid].MyCubeBocks.Remove(myCubeBlock);
            DirtyGrids.Add(myCubeBlock.CubeGrid);
        }

        private void MenuOpened(object obj)
        {
            PlayerControlAcquired(ControlledEntity);
            InMenu = true;
        }

        private void MenuClosed(object obj)
        {
            InMenu = false;
        }

        private void PlayerControlAcquired(MyEntity lastEnt)
        {
            var cockpit = lastEnt as MyCockpit;
            var remote = lastEnt as MyRemoteControl;

            GridAi gridAi;
            if (cockpit != null && GridTargetingAIs.TryGetValue(cockpit.CubeGrid, out gridAi))
                FutureEvents.Schedule(TurnWeaponShootOff, gridAi, 1);
            else if (remote != null && GridTargetingAIs.TryGetValue(remote.CubeGrid, out gridAi))
                FutureEvents.Schedule(TurnWeaponShootOff, gridAi, 1);
        }

        private void PlayerConnected(long id)
        {
            try
            {
                if (Players.ContainsKey(id)) return;
                MyAPIGateway.Multiplayer.Players.GetPlayers(null, myPlayer => FindPlayer(myPlayer, id));
            }
            catch (Exception ex) { Log.Line($"Exception in PlayerConnected: {ex}"); }
        }

        private void PlayerDisconnected(long l)
        {
            try
            {
                IMyPlayer removedPlayer;
                Players.TryRemove(l, out removedPlayer);
                PlayerEventId++;
                if (removedPlayer.SteamUserId == AuthorSteamId)
                {
                    AuthorPlayerId = 0;
                }
            }
            catch (Exception ex) { Log.Line($"Exception in PlayerDisconnected: {ex}"); }
        }

        private bool FindPlayer(IMyPlayer player, long id)
        {
            if (player.IdentityId == id)
            {
                Players[id] = player;
                PlayerEventId++;
                if (player.SteamUserId == AuthorSteamId) AuthorPlayerId = player.IdentityId;
            }
            return false;
        }
    }
}
