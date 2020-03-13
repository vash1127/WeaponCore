using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Weapons;
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
                if (!Inited) lock (InitObj) Init();
                var grid = myEntity as MyCubeGrid;
                if (grid != null) grid.AddedToScene += GridAddedToScene;

                var placer = myEntity as IMyBlockPlacerBase;
                if (placer != null && Placer == null) Placer = placer;

                var cube = myEntity as MyCubeBlock;
                var sorter = cube as MyConveyorSorter;
                var turret = cube as IMyLargeTurretBase;
                var controllableGun = cube as IMyUserControllableGun;
                if (sorter != null || turret != null || controllableGun != null)
                {
                    if (!(ReplaceVanilla && VanillaIds.ContainsKey(cube.BlockDefinition.Id)) && !WeaponPlatforms.ContainsKey(cube.BlockDefinition.Id.SubtypeId)) return;

                    lock (InitObj)
                    {
                        if (!SorterControls && myEntity is MyConveyorSorter)
                        {
                            MyAPIGateway.Utilities.InvokeOnGameThread(() => CreateTerminalUi<IMyConveyorSorter>(this));
                            SorterControls = true;
                        }
                        else if (!TurretControls && turret != null)
                        {
                            MyAPIGateway.Utilities.InvokeOnGameThread(() => CreateTerminalUi<IMyLargeTurretBase>(this));
                            TurretControls = true;

                        }
                        else if (!FixedMissileControls && controllableGun is IMySmallMissileLauncher)
                        {
                            MyAPIGateway.Utilities.InvokeOnGameThread(() => CreateTerminalUi<IMySmallMissileLauncher>(this));
                            FixedMissileControls = true;
                        }
                        else if (!FixedGunControls && controllableGun is IMySmallGatlingGun)
                        {
                            MyAPIGateway.Utilities.InvokeOnGameThread(() => CreateTerminalUi<IMySmallGatlingGun>(this));
                            FixedGunControls = true;
                        }
                    }
                    InitComp(cube);
                }
            }
            catch (Exception ex) { Log.Line($"Exception in OnEntityCreate: {ex}"); }
        }

        private void GridAddedToScene(MyEntity myEntity)
        {
            try
            {
                NewGrids.Enqueue(myEntity as MyCubeGrid);
            }
            catch (Exception ex) { Log.Line($"Exception in GridAddedToScene: {ex}"); }
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
        }

        private void ToFatMap(MyCubeBlock myCubeBlock)
        {
            try
            {
                GridToFatMap[myCubeBlock.CubeGrid].MyCubeBocks.Add(myCubeBlock);
                GridToFatMap[myCubeBlock.CubeGrid].MyCubeBocks.ApplyAdditions();
                DirtyGrids.Add(myCubeBlock.CubeGrid);
            }
            catch (Exception ex) { Log.Line($"Exception in ToFatMap: {ex}"); }
        }

        private void FromFatMap(MyCubeBlock myCubeBlock)
        {
            try
            {
                GridToFatMap[myCubeBlock.CubeGrid].MyCubeBocks.Remove(myCubeBlock, true);
                DirtyGrids.Add(myCubeBlock.CubeGrid);
            }
            catch (Exception ex) { Log.Line($"Exception in ToFatMap: {ex}"); }
        }

        private void MenuOpened(object obj)
        {
            try
            {
                PlayerControlAcquired(ControlledEntity);
                InMenu = true;
            }
            catch (Exception ex) { Log.Line($"Exception in MenuOpened: {ex}"); }
        }

        private void MenuClosed(object obj)
        {
            try
            {
                InMenu = false;
            }
            catch (Exception ex) { Log.Line($"Exception in MenuClosed: {ex}"); }
        }

        private void PlayerControlAcquired(MyEntity lastEnt)
        {
            var cube = lastEnt as MyCubeBlock;
            GridAi gridAi;
            if (cube!= null && GridTargetingAIs.TryGetValue(cube.CubeGrid, out gridAi))
                gridAi.TurnManualShootOff();
        }

        private void PlayerConnected(long id)
        {
            try
            {
                if (Players.ContainsKey(id)) return;
                MyAPIGateway.Multiplayer.Players.GetPlayers(null, myPlayer => FindPlayer(myPlayer, id));

                if (IsServer && MpActive)
                {
                    PacketsToClient.Add(new PacketInfo
                    {
                        Entity = null,
                        Packet = new BoolUpdatePacket
                        {
                            EntityId = id,
                            SenderId = Players[id].SteamUserId,
                            PType = PacketType.PlayerIdUpdate,
                            Data = true
                        }
                    });
                }
            }
            catch (Exception ex) { Log.Line($"Exception in PlayerConnected: {ex}"); }
        }

        private void PlayerDisconnected(long l)
        {
            try
            {
                PlayerEventId++;
                IMyPlayer removedPlayer;
                if (Players.TryRemove(l, out removedPlayer))
                {
                    long playerId;
                    SteamToPlayer.TryRemove(removedPlayer.SteamUserId, out playerId);
                    PlayerMouseStates.Remove(playerId);

                    if (IsServer && MpActive)
                    {
                        PacketsToClient.Add(new PacketInfo
                        {
                            Entity = null,
                            Packet = new BoolUpdatePacket
                            {
                                EntityId = playerId,
                                SenderId = removedPlayer.SteamUserId,
                                PType = PacketType.PlayerIdUpdate,
                                Data = false
                            }
                        });
                    }

                    if (removedPlayer.SteamUserId == AuthorSteamId)
                    {
                        AuthorPlayerId = 0;
                    }
                }
            }
            catch (Exception ex) { Log.Line($"Exception in PlayerDisconnected: {ex}"); }
        }
    }
}
