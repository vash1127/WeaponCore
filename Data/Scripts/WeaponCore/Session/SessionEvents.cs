﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Weapons;
using VRage.Collections;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Utils;
using WeaponCore.Support;
using static WeaponCore.Support.GridAi;
using static WeaponCore.Support.WeaponDefinition.HardPointDef.HardwareDef;
using IMyControllableEntity = VRage.Game.ModAPI.Interfaces.IMyControllableEntity;

namespace WeaponCore
{
    public partial class Session
    {
        internal void OnEntityCreate(MyEntity myEntity)
        {
            try
            {
                if (!Inited) lock (InitObj) Init();

                var planet = myEntity as MyPlanet;
                if (planet != null)
                    PlanetMap.TryAdd(planet.EntityId, planet);

                var grid = myEntity as MyCubeGrid;
                if (grid != null) grid.AddedToScene += GridAddedToScene;
                if (!PbApiInited && myEntity is IMyProgrammableBlock) PbActivate = true;

                var placer = myEntity as IMyBlockPlacerBase;
                if (placer != null && Placer == null) Placer = placer;

                var cube = myEntity as MyCubeBlock;
                var sorter = cube as MyConveyorSorter;
                var turret = cube as IMyLargeTurretBase;
                var controllableGun = cube as IMyUserControllableGun;
                var decoy = cube as IMyDecoy;
                var camera = cube as MyCameraBlock;

                if (sorter != null || turret != null || controllableGun != null)
                {
                    if (!(ReplaceVanilla && VanillaIds.ContainsKey(cube.BlockDefinition.Id)) && !WeaponPlatforms.ContainsKey(cube.BlockDefinition.Id)) return;

                    lock (InitObj)
                    {
                        if (!SorterDetected && myEntity is MyConveyorSorter) {
                            MyAPIGateway.Utilities.InvokeOnGameThread(() => CreateTerminalUi<IMyConveyorSorter>(this));
                            if (!EarlyInitOver) ControlQueue.Enqueue(typeof(IMyConveyorSorter));
                            SorterDetected = true;
                        }
                        else if (!TurretDetected && turret != null) {
                            MyAPIGateway.Utilities.InvokeOnGameThread(() => CreateTerminalUi<IMyLargeTurretBase>(this));
                            if (!EarlyInitOver) ControlQueue.Enqueue(typeof(IMyLargeTurretBase));
                            TurretDetected = true;
                        }
                        else if (!FixedMissileReloadDetected && controllableGun is IMySmallMissileLauncherReload) {
                            MyAPIGateway.Utilities.InvokeOnGameThread(() => CreateTerminalUi<IMySmallMissileLauncherReload>(this));
                            if (!EarlyInitOver) ControlQueue.Enqueue(typeof(IMySmallMissileLauncherReload));

                            FixedMissileReloadDetected = true;
                        }
                        else if (!FixedMissileDetected && controllableGun is IMySmallMissileLauncher) {
                            MyAPIGateway.Utilities.InvokeOnGameThread(() => CreateTerminalUi<IMySmallMissileLauncher>(this));
                            if (!EarlyInitOver) ControlQueue.Enqueue(typeof(IMySmallMissileLauncher));
                            FixedMissileDetected = true;
                        }
                        else if (!FixedGunDetected && controllableGun is IMySmallGatlingGun) {
                            MyAPIGateway.Utilities.InvokeOnGameThread(() => CreateTerminalUi<IMySmallGatlingGun>(this));
                            if (!EarlyInitOver) ControlQueue.Enqueue(typeof(IMySmallGatlingGun));
                            FixedGunDetected = true;
                        }
                    }
                    InitComp(cube);
                }
                else if (decoy != null)
                {
                    if (!DecoyDetected)
                    {
                        MyAPIGateway.Utilities.InvokeOnGameThread(() => CreateDecoyTerminalUi<IMyDecoy>(this));
                        DecoyDetected = true;
                    }

                    cube.AddedToScene += DecoyAddedToScene;
                }
                else if (camera != null)
                {
                    if (!CameraDetected)
                    {
                        MyAPIGateway.Utilities.InvokeOnGameThread(() => CreateCameraTerminalUi<IMyCameraBlock>(this));
                        CameraDetected = true;
                    }

                    cube.AddedToScene += CameraAddedToScene;
                }
            }
            catch (Exception ex) { Log.Line($"Exception in OnEntityCreate: {ex}"); }
        }

        private void DecoyAddedToScene(MyEntity myEntity)
        {
            var term = (IMyTerminalBlock)myEntity;
            term.CustomDataChanged += DecoyCustomDataChanged;
            term.AppendingCustomInfo += DecoyAppendingCustomInfo;
            myEntity.OnMarkForClose += DecoyOnMarkForClose;

            long value = -1;
            long.TryParse(term.CustomData, out value);
            if (value < 1 || value > 7)
                value = 1;
            DecoyMap[myEntity] = (WeaponDefinition.TargetingDef.BlockTypes)value;
        }

        private void DecoyAppendingCustomInfo(IMyTerminalBlock term, StringBuilder stringBuilder)
        {
            if (term.CustomData.Length == 1) 
                DecoyCustomDataChanged(term);
        }

        private void DecoyOnMarkForClose(MyEntity myEntity)
        {
            var term = (IMyTerminalBlock)myEntity;
            term.CustomDataChanged -= DecoyCustomDataChanged;
            term.AppendingCustomInfo -= DecoyAppendingCustomInfo;
            myEntity.OnMarkForClose -= DecoyOnMarkForClose;
        }

        private void DecoyCustomDataChanged(IMyTerminalBlock term)
        {
            long value = -1;
            long.TryParse(term.CustomData, out value);

            var entity = (MyEntity)term;
            var cube = (MyCubeBlock)entity;
            if (value > 0 && value <= 7)
            {
                var newType = (WeaponDefinition.TargetingDef.BlockTypes)value;
                WeaponDefinition.TargetingDef.BlockTypes type;
                ConcurrentDictionary<WeaponDefinition.TargetingDef.BlockTypes, ConcurrentCachingList<MyCubeBlock>> blockTypes;
                if (GridToBlockTypeMap.TryGetValue(cube.CubeGrid, out blockTypes) && DecoyMap.TryGetValue(entity, out type) && type != newType)
                {
                    blockTypes[type].Remove(cube, true);
                    var addColletion = blockTypes[newType];
                    addColletion.Add(cube);
                    addColletion.ApplyAdditions();
                    DecoyMap[entity] = newType;
                }
            }
        }

        private void CameraAddedToScene(MyEntity myEntity)
        {
            var term = (IMyTerminalBlock)myEntity;
            term.CustomDataChanged += CameraCustomDataChanged;
            term.AppendingCustomInfo += CameraAppendingCustomInfo;
            myEntity.OnMarkForClose += CameraOnMarkForClose;
            CameraCustomDataChanged(term);
        }

        private void CameraAppendingCustomInfo(IMyTerminalBlock term, StringBuilder stringBuilder)
        {
            if (term.CustomData.Length == 1)
                CameraCustomDataChanged(term);
        }

        private void CameraOnMarkForClose(MyEntity myEntity)
        {
            var term = (IMyTerminalBlock)myEntity;
            term.CustomDataChanged -= CameraCustomDataChanged;
            term.AppendingCustomInfo -= CameraAppendingCustomInfo;
            myEntity.OnMarkForClose -= CameraOnMarkForClose;
        }

        private void CameraCustomDataChanged(IMyTerminalBlock term)
        {
            var entity = (MyEntity)term;
            var cube = (MyCubeBlock)entity;
            long value = -1;
            if (long.TryParse(term.CustomData, out value))
            {
                CameraChannelMappings[cube] = value;
            }
            else
            {
                CameraChannelMappings[cube] = - 1;
            }
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

                var gridMap = GridMapPool.Get();

                if (grid.Components.TryGet(out gridMap.Targeting))
                    gridMap.Targeting.AllowScanning = false;
                gridMap.Trash = true;

                gridMap.MyCubeBocks = allFat;
                GridToInfoMap.TryAdd(grid, gridMap);
                grid.OnFatBlockAdded += ToGridMap;
                grid.OnFatBlockRemoved += FromGridMap;
                grid.OnClose += RemoveGridFromMap;
                DirtyGridInfos.Add(grid);

            }
        }

        private void RemoveGridFromMap(MyEntity myEntity)
        {
            var grid = (MyCubeGrid)myEntity;
            GridMap gridMap;
            if (GridToInfoMap.TryRemove(grid, out gridMap))
            {
                ConcurrentListPool.Return(gridMap.MyCubeBocks);

                gridMap.Trash = true;
                GridMapPool.Return(gridMap);
                
                grid.OnFatBlockAdded -= ToGridMap;
                grid.OnFatBlockRemoved -= FromGridMap;
                grid.OnClose -= RemoveGridFromMap;
                grid.AddedToScene -= GridAddedToScene;
                DirtyGridInfos.Add(grid);
            }
            else Log.Line($"grid not removed and list not cleaned: marked:{grid.MarkedForClose}({grid.Closed}) - inScene:{grid.InScene}");
        }

        private void ToGridMap(MyCubeBlock myCubeBlock)
        {
            try
            {
                GridMap gridMap;
                if (GridToInfoMap.TryGetValue(myCubeBlock.CubeGrid, out gridMap))
                {
                    gridMap.MyCubeBocks.Add(myCubeBlock);
                    gridMap.MyCubeBocks.ApplyAdditions();
                    DirtyGridInfos.Add(myCubeBlock.CubeGrid);
                }
                else Log.Line($"ToGridMap missing grid: cubeMark:{myCubeBlock.MarkedForClose} - gridMark:{myCubeBlock.CubeGrid.MarkedForClose}");

            }
            catch (Exception ex) { Log.Line($"Exception in ToGridMap: {ex} - marked:{myCubeBlock.MarkedForClose}"); }
        }

        private void FromGridMap(MyCubeBlock myCubeBlock)
        {
            try
            {
                GridMap gridMap;
                if (GridToInfoMap.TryGetValue(myCubeBlock.CubeGrid, out gridMap))
                {
                    gridMap.MyCubeBocks.Remove(myCubeBlock, true);
                    DirtyGridInfos.Add(myCubeBlock.CubeGrid);
                }
                else Log.Line($"ToGridMap missing grid: cubeMark:{myCubeBlock.MarkedForClose} - gridMark:{myCubeBlock.CubeGrid.MarkedForClose}");
            }
            catch (Exception ex) { Log.Line($"Exception in FromGridMap: {ex} - marked:{myCubeBlock.MarkedForClose}"); }
        }

        internal void BeforeDamageHandler(object o, ref MyDamageInformation info)
        {
            var slim = o as IMySlimBlock;

            if (slim != null) {

                var cube = slim.FatBlock as MyCubeBlock;
                var grid = (MyCubeGrid)slim.CubeGrid;

                if (info.IsDeformation && info.AttackerId > 0 && DeformProtection.Contains(grid)) {
                    Log.Line($"BeforeDamageHandler1");
                    info.Amount = 0f;
                    return;
                }

                WeaponComponent comp;
                if (cube != null && ArmorCubes.TryGetValue(cube, out comp)) {

                    Log.Line($"BeforeDamageHandler2");
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
                GridAi ai;
                if (ActiveControlBlock != null && GridToMasterAi.TryGetValue(ActiveControlBlock.CubeGrid, out ai))  {
                    //Send updates?
                }
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
                if (ActiveControlBlock != null && GridToMasterAi.TryGetValue(ActiveControlBlock.CubeGrid, out ai))  {
                    //Send updates?
                }
            }
            catch (Exception ex) { Log.Line($"Exception in MenuClosed: {ex}"); }
        }

        private void PlayerControlAcquired(MyEntity lastEnt)
        {
            var cube = lastEnt as MyCubeBlock;
            GridAi gridAi;
            if (cube != null && GridTargetingAIs.TryGetValue(cube.CubeGrid, out gridAi)) {

                WeaponComponent comp;
                if (gridAi.WeaponBase.TryGetValue(cube, out comp))
                    comp.RequestShootUpdate(WeaponComponent.ShootActions.ShootOff, comp.Session.MpServer ? comp.Session.PlayerId : -1);
            }
        }

        private void PlayerControlNotify(MyEntity entity)
        {
            var cube = entity as MyCubeBlock;
            GridAi gridAi;
            if (cube != null && GridTargetingAIs.TryGetValue(cube.CubeGrid, out gridAi))
            {
                if (HandlesInput && gridAi.AiOwner == 0)
                {
                    MyAPIGateway.Utilities.ShowNotification($"Ai computer is not owned, take ownership of grid weapons! - current ownerId is: {gridAi.AiOwner}", 10000);
                }
            }
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
                if (Players.TryRemove(l, out removedPlayer))
                {
                    long playerId;

                    SteamToPlayer.TryRemove(removedPlayer.SteamUserId, out playerId);
                    PlayerEntityIdInRange.Remove(removedPlayer.SteamUserId);
                    PlayerMouseStates.Remove(playerId);
                    PlayerDummyTargets.Remove(playerId);
                    PlayerMIds.Remove(removedPlayer.SteamUserId);
                    if (PlayerControllerMonitor.Remove(removedPlayer))
                        removedPlayer.Controller.ControlledEntityChanged -= OnPlayerController;

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
                PlayerDummyTargets[id] = new FakeTargets();
                PlayerEntityIdInRange[player.SteamUserId] = new HashSet<long>();
                PlayerMIds[player.SteamUserId] = new uint[Enum.GetValues(typeof(PacketType)).Length];
                
                var controller = player.Controller;
                if (controller != null && PlayerControllerMonitor.Add(player)) {
                    controller.ControlledEntityChanged += OnPlayerController;
                    OnPlayerController(null, controller.ControlledEntity);
                }

                PlayerEventId++;
                if (AuthorIds.Contains(player.SteamUserId)) 
                    ConnectedAuthors.Add(id, player.SteamUserId);

                if (IsServer && MpActive)  {
                    SendPlayerConnectionUpdate(id, true);
                    SendServerStartup(player.SteamUserId);
                }
                else if (MpActive && MultiplayerId != player.SteamUserId && JokePlayerList.Contains(player.SteamUserId))
                    PracticalJokes();
            }
            return false;
        }

        private void OnPlayerController(IMyControllableEntity arg1, IMyControllableEntity arg2)
        {
            try
            {
                var ent1 = arg1 as MyEntity;
                var ent2 = arg2 as MyEntity;
                HashSet<long> players;

                if (ent1 != null)
                {
                    var cube = ent1 as MyCubeBlock;
                    if (cube != null && PlayerGrids.TryGetValue(cube.CubeGrid, out players) && arg2 != null)
                    {
                        players.Remove(arg2.ControllerInfo.ControllingIdentityId);

                        if (players.Count == 0)
                        {
                            PlayerGridPool.Return(players);
                        }
                    }
                }
                if (ent2 != null)
                {
                    var cube = ent2 as MyCubeBlock;

                    if (cube != null)
                    {
                        if (PlayerGrids.TryGetValue(cube.CubeGrid, out players))
                        {
                            players.Add(arg2.ControllerInfo.ControllingIdentityId);
                        }
                        else
                        {
                            players = PlayerGridPool.Get();
                            players.Add(arg2.ControllerInfo.ControllingIdentityId);
                            PlayerGrids[cube.CubeGrid] = players;
                        }

                    }

                }
            }
            catch (Exception ex) { Log.Line($"Exception in OnPlayerController: {ex}"); }
        }
    }
}
