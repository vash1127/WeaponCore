using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Weapons;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using WeaponCore.Support;
using static WeaponCore.Support.Ai;

namespace WeaponCore
{
    public partial class Session
    {
        internal void OnEntityCreate(MyEntity entity)
        {
            try
            {
                if (!Inited) lock (InitObj) Init();
                var grid = entity as MyCubeGrid;
                if (grid != null) grid.AddedToScene += GridAddedToScene;
                if (!PbApiInited && entity is IMyProgrammableBlock) PbActivate = true;

                var placer = entity as IMyBlockPlacerBase;
                if (placer != null && Placer == null) Placer = placer;

                var cube = entity as MyCubeBlock;
                var sorter = entity as MyConveyorSorter;
                var turret = entity as IMyLargeTurretBase;
                var controllableGun = entity as IMyUserControllableGun;
                var rifle = entity as IMyAutomaticRifleGun;
                var upgrade = entity as IMyUpgradeModule;
                if (sorter != null || turret != null || controllableGun != null || rifle != null || upgrade != null)
                {
                    var cubeType = cube != null && (ReplaceVanilla && VanillaIds.ContainsKey(cube.BlockDefinition.Id) || PartPlatforms.ContainsKey(cube.BlockDefinition.Id));
                    var rifleType = !cubeType && rifle != null && PartPlatforms.ContainsKey(rifle.DefinitionId);
                    var validType = cubeType || rifleType;
                    if (!validType) return;

                    lock (InitObj)
                    {
                        if (!SorterControls && entity is MyConveyorSorter) {
                            MyAPIGateway.Utilities.InvokeOnGameThread(() => CreateTerminalUi<IMyConveyorSorter>(this));
                            SorterControls = true;
                        }
                        else if (!TurretControls && turret != null) {
                            MyAPIGateway.Utilities.InvokeOnGameThread(() => CreateTerminalUi<IMyLargeTurretBase>(this));
                            TurretControls = true;
                        }
                        else if (!FixedMissileReloadControls && controllableGun is IMySmallMissileLauncherReload) {
                            MyAPIGateway.Utilities.InvokeOnGameThread(() => CreateTerminalUi<IMySmallMissileLauncherReload>(this));
                            FixedMissileReloadControls = true;
                        }
                        else if (!FixedMissileControls && controllableGun is IMySmallMissileLauncher) {
                            MyAPIGateway.Utilities.InvokeOnGameThread(() => CreateTerminalUi<IMySmallMissileLauncher>(this));
                            FixedMissileControls = true;
                        }
                        else if (!FixedGunControls && controllableGun is IMySmallGatlingGun) {
                            MyAPIGateway.Utilities.InvokeOnGameThread(() => CreateTerminalUi<IMySmallGatlingGun>(this));
                            FixedGunControls = true;
                        }
                        else if (!ArmorControls && sorter != null && WeaponCoreArmorBlockDefs.Contains(cube.BlockDefinition.Id))
                        {
                            MyAPIGateway.Utilities.InvokeOnGameThread(() => CreateTerminalUi<IMyConveyorSorter>(this));
                            ArmorControls = true;
                        }
                        else  if (!UpgradeControls && sorter != null && WeaponCoreUpgradeBlockDefs.Contains(cube.BlockDefinition.Id))
                        {
                            MyAPIGateway.Utilities.InvokeOnGameThread(() => CreateTerminalUi<IMyUpgradeModule>(this));
                            UpgradeControls = true;
                        }
                    }
                    InitComp(entity);
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
                GridToInfoMap[myCubeBlock.CubeGrid].MyCubeBocks.Add(myCubeBlock);
                GridToInfoMap[myCubeBlock.CubeGrid].MyCubeBocks.ApplyAdditions();
                DirtyGridInfos.Add(myCubeBlock.CubeGrid);
            }
            catch (Exception ex) { Log.Line($"Exception in ToGridMap: {ex} - marked:{myCubeBlock.MarkedForClose}"); }
        }

        private void FromGridMap(MyCubeBlock myCubeBlock)
        {
            try
            {
                GridToInfoMap[myCubeBlock.CubeGrid].MyCubeBocks.Remove(myCubeBlock, true);
                DirtyGridInfos.Add(myCubeBlock.CubeGrid);
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

                CoreComponent comp;
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
                Ai ai;
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
                Ai ai;
                if (ActiveControlBlock != null && GridToMasterAi.TryGetValue(ActiveControlBlock.CubeGrid, out ai))  {
                    //Send updates?
                }
            }
            catch (Exception ex) { Log.Line($"Exception in MenuClosed: {ex}"); }
        }

        private void PlayerControlAcquired(MyEntity lastEnt)
        {
            var topMost = lastEnt.GetTopMostParent();
            Ai ai;
            if (topMost != null && GridTargetingAIs.TryGetValue(topMost, out ai)) {

                CoreComponent comp;
                if (ai.PartBase.TryGetValue(lastEnt, out comp))
                    comp.RequestShootUpdate(CoreComponent.TriggerActions.TriggerOff, comp.Session.DedicatedServer ? 0 : -1);
            }
        }

        private void PlayerControlNotify(MyEntity entity)
        {
            var topMost = entity.GetTopMostParent();
            Ai ai;
            if (topMost != null && GridTargetingAIs.TryGetValue(topMost, out ai))
            {
                if (HandlesInput && ai.AiOwner == 0)
                {
                    MyAPIGateway.Utilities.ShowNotification($"Ai computer is not owned, take ownership of grid weapons! - current ownerId is: {ai.AiOwner}", 10000);
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
                PlayerEntityIdInRange[player.SteamUserId] = new HashSet<long>();
                PlayerMIds[player.SteamUserId] = new uint[Enum.GetValues(typeof(PacketType)).Length];

                PlayerEventId++;
                if (AuthorIds.Contains(player.SteamUserId)) 
                    ConnectedAuthors.Add(id, player.SteamUserId);

                if (IsServer && MpActive)  {
                    SendPlayerConnectionUpdate(id, true);
                    SendServerStartup(player.SteamUserId);
                }
            }
            return false;
        }

        private void WApiReceiveData()
        {
            if (WApi.Registered) {
                WaterMap.Clear();
                MaxWaterHeightSqr.Clear();
                for (int i = 0; i < WApi.Waters.Count; i++) {
                    
                    var water = WApi.Waters[i];
                    if (water.planet != null) {

                        WaterMap[water.planet] = water;
                        var maxWaterHeight = water.radius;
                        var maxWaterHeightSqr = maxWaterHeight * maxWaterHeight;
                        MaxWaterHeightSqr[water.planet] = maxWaterHeightSqr;
                    }
                }
            }
        }
    }
}
