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
                                MyAPIGateway.Utilities.InvokeOnGameThread(() => CreateTerminalUI<IMyConveyorSorter>(this));
                            SorterControls = true;
                        }
                    }
                    if (!TurretControls && missileTurret != null)
                    {
                        lock (InitObj)
                        {
                            if (!TurretControls)
                                MyAPIGateway.Utilities.InvokeOnGameThread(() => CreateTerminalUI<IMyLargeTurretBase>(this));
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
                var allFat = ConcurrentListPool.Get();
                var gridFat = grid.GetFatBlocks();
                for (int i = 0; i < gridFat.Count; i++) allFat.Add(gridFat[i]);
                allFat.ApplyAdditions();

                var fatMap = FatMapPool.Get();

                if (grid.Components.TryGet(out fatMap.Targeting))
                    fatMap.Targeting.AllowScanning = false;
                fatMap.Trash = true;

                fatMap.MyCubeBocks = allFat;
                GridToFatMap.TryAdd(grid, fatMap);
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
                fatMap.MyCubeBocks.ClearImmediate();
                ConcurrentListPool.Return(fatMap.MyCubeBocks);
                fatMap.Trash = true;
                FatMapPool.Return(fatMap);
                grid.OnFatBlockAdded -= ToFatMap;
                grid.OnFatBlockRemoved -= FromFatMap;
                grid.OnClose -= RemoveGridFromMap;
                grid.AddedToScene -= GridAddedToScene;
                DirtyGrids.Add(grid);
            }
            else Log.Line($"grid not removed and list not cleaned");
            //FutureEvents.Schedule(DeferedFatMapRemoval, (MyCubeGrid)myEntity, 120);
        }

        private void ToFatMap(MyCubeBlock myCubeBlock)
        {
            GridToFatMap[myCubeBlock.CubeGrid].MyCubeBocks.Add(myCubeBlock);
            GridToFatMap[myCubeBlock.CubeGrid].MyCubeBocks.ApplyAdditions();
            DirtyGrids.Add(myCubeBlock.CubeGrid);
        }

        private void FromFatMap(MyCubeBlock myCubeBlock)
        {
            GridToFatMap[myCubeBlock.CubeGrid].MyCubeBocks.Remove(myCubeBlock, true);
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
            var cube = lastEnt as MyCubeBlock;
            GridAi gridAi;
            if (cube!= null && GridTargetingAIs.TryGetValue(cube.CubeGrid, out gridAi))
                gridAi.TurnMouseShootOff();
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
                PlayerEventId++;
                IMyPlayer removedPlayer;
                if (Players.TryRemove(l, out removedPlayer) && removedPlayer.SteamUserId == AuthorSteamId)
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
