using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Cube;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Weapons;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using WeaponCore.Support;
using static WeaponCore.Support.GridAi;
using static WeaponCore.Support.WeaponDefinition.HardPointDef.HardwareDef;

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
                if (!PbApiInited) PbActivate = true;

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
                            if (IsServer && MpActive && !DedicatedServer)
                                CreateTerminalUi<IMyConveyorSorter>(this);
                            else
                                MyAPIGateway.Utilities.InvokeOnGameThread(() => CreateTerminalUi<IMyConveyorSorter>(this));

                            SorterControls = true;
                        }
                        else if (!TurretControls && turret != null)
                        {
                            if (IsServer && MpActive && !DedicatedServer)
                                CreateTerminalUi<IMyLargeTurretBase>(this);
                            else
                                MyAPIGateway.Utilities.InvokeOnGameThread(() => CreateTerminalUi<IMyLargeTurretBase>(this));

                            TurretControls = true;
                        }
                        else if (!FixedMissileControls && controllableGun is IMySmallMissileLauncher)
                        {
                            if (IsServer && MpActive && !DedicatedServer)
                                CreateTerminalUi<IMySmallMissileLauncher>(this);
                            else
                                MyAPIGateway.Utilities.InvokeOnGameThread(() => CreateTerminalUi<IMySmallMissileLauncher>(this));

                            FixedMissileControls = true;
                        }
                        else if (!FixedGunControls && controllableGun is IMySmallGatlingGun)
                        {
                            if (IsServer && MpActive && !DedicatedServer)
                                CreateTerminalUi<IMySmallGatlingGun>(this);
                            else
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
                ConcurrentListPool.Return(fatMap.MyCubeBocks);
                fatMap.Trash = true;
                FatMapPool.Return(fatMap);
                grid.OnFatBlockAdded -= ToFatMap;
                grid.OnFatBlockRemoved -= FromFatMap;
                grid.OnClose -= RemoveGridFromMap;
                grid.AddedToScene -= GridAddedToScene;
                DirtyGrids.Add(grid);
            }
            else Log.Line($"grid not removed and list not cleaned: marked:{grid.MarkedForClose}({grid.Closed}) - inScene:{grid.InScene}");
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

        internal void BeforeDamageHandler(object o, ref MyDamageInformation info)
        {
            var slim = o as IMySlimBlock;

            if (slim != null) {

                var cube = slim.FatBlock as MyCubeBlock;
                var grid = (MyCubeGrid)slim.CubeGrid;

                if (info.IsDeformation && info.AttackerId > 0 && DeformProtection.Contains(grid)) {
                    info.Amount = 0f;
                    return;
                }

                WeaponComponent comp;
                if (cube != null && ArmorCubes.TryGetValue(cube, out comp)) {

                    info.Amount = 0f;
                    if (info.IsDeformation && info.AttackerId > 0) {
                        DeformProtection.Add(cube.CubeGrid);
                        LastDeform = Tick;
                    }
                }
            }
        }

        private void MenuOpened(object obj)
        {
            try
            {
                InMenu = true;
            }
            catch (Exception ex) { Log.Line($"Exception in MenuOpened: {ex}"); }
        }

        private void MenuClosed(object obj)
        {
            try
            {
                InMenu = false;
                HudUi.NeedsUpdate = true;
                GridAi ai;
                if(ActiveControlBlock != null && GridToMasterAi.TryGetValue(ActiveControlBlock.CubeGrid, out ai))
                    ai.ScanBlockGroups = true;
            }
            catch (Exception ex) { Log.Line($"Exception in MenuClosed: {ex}"); }
        }

        private void PlayerControlAcquired(MyEntity lastEnt)
        {
            var cube = lastEnt as MyCubeBlock;
            GridAi rootAi;
            if (cube!= null && GridToMasterAi.TryGetValue(cube.CubeGrid, out rootAi))
                rootAi.Construct.UpdateConstruct(GridAi.Constructs.UpdateType.ManualShootingOff);
        }

        private void PlayerConnected(long id)
        {
            try
            {
                if (Players.ContainsKey(id)) return;
                MyAPIGateway.Multiplayer.Players.GetPlayers(null, myPlayer => FindPlayer(myPlayer, id));

                if (IsServer && MpActive)
                    SendPlayerConnectionUpdate(id, true);
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
                    PlayerDummyTargets.Remove(playerId);

                    if (IsServer && MpActive)
                        SendPlayerConnectionUpdate(l, false);

                    if (AuthorIds.Contains(removedPlayer.SteamUserId))
                        ConnectedAuthors.Remove(playerId);
                }
            }
            catch (Exception ex) { Log.Line($"Exception in PlayerDisconnected: {ex}"); }
        }


        private bool FindPlayer(IMyPlayer player, long id)
        {
            if (player.IdentityId == id)
            {
                Players[id] = player;
                SteamToPlayer[player.SteamUserId] = id;
                PlayerMouseStates[id] = new InputStateData();
                PlayerDummyTargets[id] = new FakeTarget();


                PlayerEventId++;
                if (AuthorIds.Contains(player.SteamUserId)) 
                    ConnectedAuthors.Add(id, player.SteamUserId);
            }
            return false;
        }
    }
}
