using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.Entity;
using VRageMath;
using WeaponCore.Platform;
using WeaponCore.Support;
using static WeaponCore.Platform.Weapon;
using static WeaponCore.Session;

namespace WeaponCore
{
    public partial class Session
    {
        #region Network sync
        internal void PacketizeToClientsInRange(MyEntity entity, Packet packet)
        {
            try
            {
                var bytes = MyAPIGateway.Utilities.SerializeToBinary(packet);
                foreach (var p in Players.Values)
                {
                    var id = p.SteamUserId;
                    if (id != packet.SenderId && (entity == null || Vector3D.DistanceSquared(p.GetPosition(), entity.PositionComp.WorldAABB.Center) <= SyncBufferedDistSqr))
                    {
                        MyAPIGateway.Multiplayer.SendMessageTo(ClientPacketId, bytes, p.SteamUserId);
                    }
                }
            }
            catch (Exception ex) { Log.Line($"Exception in PacketizeToClientsInRange: {ex}"); }
        }

        internal void SendPacketToServer(Packet packet)
        {
            if (!IsClient) return;

            byte[] bytes = MyAPIGateway.Utilities.SerializeToBinary(packet);

            MyAPIGateway.Multiplayer.SendMessageToServer(ServerPacketId, bytes);
        }

        private void ClientReceivedPacket(byte[] rawData)
        {
            try
            {
                var report = Reporter.ReportPool.Get();
                report.Receiver = NetworkReporter.Report.Received.Client;
                report.PacketSize = rawData.Length;

                var packet = MyAPIGateway.Utilities.SerializeFromBinary<Packet>(rawData);
                if (packet == null) return;
                var ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
                var comp = ent?.Components.Get<WeaponComponent>();

                Reporter.ReportData[packet.PType].Add(report);

                switch (packet.PType)
                {
                    case PacketType.CompStateUpdate:                        
                        var statePacket = packet as StatePacket;
                        if (statePacket?.Data == null || comp == null) return;

                        comp.State.Value.Sync(statePacket.Data);

                        report.PacketValid = true;
                        break;
                    case PacketType.CompSettingsUpdate:
                        var setPacket = packet as SettingPacket;
                        if (setPacket?.Data == null || comp == null) return;

                        comp.Set.Value.Sync(setPacket.Data);
                        
                        report.PacketValid = true;
                        break;
                    case PacketType.TargetUpdate:
                        {
                            var targetPacket = packet as GridWeaponPacket;
                            if (targetPacket?.Data == null || ent == null) return;
                            
                            for(int i = 0; i < targetPacket.Data.Count; i++)
                            {
                                var weaponData = targetPacket.Data[i];
                                var block = MyEntities.GetEntityByIdOrDefault(weaponData.CompEntityId) as MyCubeBlock;
                                comp = block?.Components.Get<WeaponComponent>();

                                if (comp == null) continue;

                                var weapon = comp.Platform.Weapons[weaponData.TargetData.WeaponId];
                                var timings = weaponData.Timmings.SyncOffsetClient(Tick);
                                var syncTarget = weaponData.TargetData;

                                SyncWeapon(weapon, timings, ref weaponData.SyncData);
                                syncTarget.SyncTarget(weapon.Target);
                            }

                            report.PacketValid = true;
                            break;
                        }
                    case PacketType.FocusUpdate:
                        {
                            var targetPacket = packet as FocusPacket;
                            if (targetPacket == null) return;

                            var myGrid = ent as MyCubeGrid;
                            GridAi ai;
                            if (myGrid != null && GridTargetingAIs.TryGetValue(myGrid, out ai))
                            {
                                var targetGrid = MyEntities.GetEntityByIdOrDefault(targetPacket.Data) as MyCubeGrid;

                                if (targetGrid != null)
                                {
                                    ai.Focus.AddFocus(targetGrid, ai, true);
                                    report.PacketValid = true;
                                }
                            }
                            break;
                        }
                    case PacketType.FakeTargetUpdate:
                        {
                            var targetPacket = packet as FakeTargetPacket;
                            if (targetPacket?.Data == null) return;

                            var myGrid = MyEntities.GetEntityByIdOrDefault(packet.EntityId) as MyCubeGrid;

                            GridAi ai;
                            if (myGrid != null && GridTargetingAIs.TryGetValue(myGrid, out ai))
                            {
                                ai.DummyTarget.TransferFrom(targetPacket.Data);

                                PacketsToClient.Add(new PacketInfo { Entity = myGrid, Packet = packet });

                                report.PacketValid = true;
                            }

                            break;
                        }

                    case PacketType.PlayerIdUpdate:
                        {
                            var updatePacket = packet as DictionaryUpdatePacket;
                            if (updatePacket == null) return;

                            if (updatePacket.Data) //update/add
                            {
                                SteamToPlayer[updatePacket.SenderId] = updatePacket.EntityId;
                                MouseStateData ms;
                                if (!PlayerMouseStates.TryGetValue(updatePacket.EntityId, out ms))
                                {
                                    PlayerMouseStates[updatePacket.EntityId] = new MouseStateData();

                                    report.PacketValid = true;
                                }
                            }
                            else //remove
                            {
                                long player;
                                SteamToPlayer.TryRemove(updatePacket.SenderId, out player);
                                PlayerMouseStates.Remove(player);

                                report.PacketValid = true;
                            }
                            break;
                        }
                    case PacketType.ClientMouseEvent:
                        var mousePacket = packet as MouseInputPacket;
                        if (mousePacket?.Data == null) return;

                        long playerId;
                        if (SteamToPlayer.TryGetValue(packet.SenderId, out playerId))
                        {
                            PlayerMouseStates[playerId] = mousePacket.Data;

                            report.PacketValid = true;
                        }

                        break;
                    case PacketType.ActiveControlUpdate:
                        {
                            var dPacket = packet as DictionaryUpdatePacket;
                            if (dPacket?.Data == null) return;

                            var block = MyEntities.GetEntityByIdOrDefault(packet.EntityId) as MyCubeBlock;
                            if (block == null) return;

                            SteamToPlayer.TryGetValue(packet.SenderId, out playerId);

                            UpdateActiveControlDictionary(block, playerId, dPacket.Data);

                            report.PacketValid = true;
                            break;
                        }
                    case PacketType.ActiveControlFullUpdate:
                        {
                            try
                            {
                                var csPacket = packet as ControllingPacket;
                                if (csPacket?.Data == null) return;

                                for (int i = 0; i < csPacket.Data.PlayersToControlledBlock.Length; i++)
                                {
                                    var playerBlock = csPacket.Data.PlayersToControlledBlock[i];

                                    var block = MyEntities.GetEntityByIdOrDefault(playerBlock.EntityId) as MyCubeBlock;

                                    UpdateActiveControlDictionary(block, playerBlock.PlayerId, true);
                                }
                            }
                            catch (Exception e) { Log.Line($"error in control update"); }

                            report.PacketValid = true;
                            break;
                        }
                    default:
                        Reporter.ReportData[PacketType.Invalid].Add(report);
                        report.PacketValid = false;

                        break;
                }
            }
            catch (Exception ex) { Log.Line($"Exception in ReceivedPacket: {ex}"); }
        }

        private void ServerReceivedPacket(byte[] rawData)
        {
            try
            {
                var report = Reporter.ReportPool.Get();
                report.Receiver = NetworkReporter.Report.Received.Server;
                report.PacketSize = rawData.Length;

                var packet = MyAPIGateway.Utilities.SerializeFromBinary<Packet>(rawData);
                if (packet == null) return;

                Reporter.ReportData[packet.PType].Add(report);

                MyEntity ent; // not inited here to avoid extras calls unless needed
                WeaponComponent comp; // not inited here to avoid extras calls unless needed
                long playerId = 0;

                switch (packet.PType)
                {
                    case PacketType.CompStateUpdate:
                        var statePacket = packet as StatePacket;
                        if (statePacket?.Data == null) return;

                        ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
                        comp = ent?.Components.Get<WeaponComponent>();
                        if (comp == null) return;

                        if (statePacket.Data.MId > comp.State.Value.MId)
                        {
                            comp.State.Value.Sync(statePacket.Data);
                            PacketsToClient.Add(new PacketInfo { Entity = ent, Packet = packet });

                            report.PacketValid = true;
                        }
                        break;

                    case PacketType.CompSettingsUpdate:

                        var setPacket = packet as SettingPacket;
                        if (setPacket?.Data == null) return;

                        ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
                        comp = ent?.Components.Get<WeaponComponent>();
                        if (comp == null) return;

                        if (setPacket.Data.MId > comp.Set.Value.MId)
                        {
                            comp.Set.Value.Sync(setPacket.Data);
                            PacketsToClient.Add(new PacketInfo { Entity = ent, Packet = setPacket });

                            report.PacketValid = true;
                        }
                        break;

                    case PacketType.ClientMouseEvent:

                        var mousePacket = packet as MouseInputPacket;
                        if (mousePacket?.Data == null) return;

                        if (SteamToPlayer.TryGetValue(packet.SenderId, out playerId))
                        {
                            PlayerMouseStates[playerId] = mousePacket.Data;
                            PacketsToClient.Add(new PacketInfo { Entity = null, Packet = mousePacket });

                            report.PacketValid = true;
                        }

                        break;

                    case PacketType.ActiveControlUpdate:
                        {
                            var dPacket = packet as DictionaryUpdatePacket;
                            if (dPacket?.Data == null) return;

                            var block = MyEntities.GetEntityByIdOrDefault(packet.EntityId) as MyCubeBlock;
                            if (block == null) return;

                            SteamToPlayer.TryGetValue(packet.SenderId, out playerId);

                            UpdateActiveControlDictionary(block, playerId, dPacket.Data);

                            PacketsToClient.Add(new PacketInfo { Entity = block, Packet = dPacket });

                            report.PacketValid = true;
                            break;
                        }
                    case PacketType.FocusUpdate:
                        {
                            var targetPacket = packet as FocusPacket;
                            if (targetPacket == null) return;

                            var myGrid = MyEntities.GetEntityByIdOrDefault(packet.EntityId) as MyCubeGrid;
                            
                            GridAi ai;
                            if (myGrid != null && GridTargetingAIs.TryGetValue(myGrid, out ai))
                            {
                                var targetGrid = MyEntities.GetEntityByIdOrDefault(targetPacket.Data) as MyCubeGrid;

                                if (targetGrid != null)
                                {
                                    ai.Focus.AddFocus(targetGrid, ai, true);
                                    PacketsToClient.Add(new PacketInfo { Entity = myGrid, Packet = targetPacket });
                                    report.PacketValid = true;
                                }
                            }
                            break;
                        }
                    case PacketType.FakeTargetUpdate:
                        {
                            var targetPacket = packet as FakeTargetPacket;
                            if (targetPacket?.Data == null) return;

                            var myGrid = MyEntities.GetEntityByIdOrDefault(packet.EntityId) as MyCubeGrid;

                            GridAi ai;
                            if (myGrid != null && GridTargetingAIs.TryGetValue(myGrid, out ai))
                            {
                                ai.DummyTarget.TransferFrom(targetPacket.Data);
                                PacketsToClient.Add(new PacketInfo { Entity = myGrid, Packet = targetPacket });
                                report.PacketValid = true;
                            }

                            break;
                        }
                    case PacketType.ActiveControlRequestUpdate:
                        {
                            var myGrid = MyEntities.GetEntityByIdOrDefault(packet.EntityId) as MyCubeGrid;
                            GridAi ai;

                            if (myGrid != null && GridTargetingAIs.TryGetValue(myGrid, out ai))
                            {
                                var c = 0;
                                var playerToBlocks = new PlayerToBlock[ai.ControllingPlayers.Count];
                                foreach (var playerBlockPair in ai.ControllingPlayers)
                                {
                                    playerToBlocks[c] = new PlayerToBlock { PlayerId = playerBlockPair.Key, EntityId = playerBlockPair.Value.EntityId };
                                    c++;
                                }

                                var syncPacket = new ControllingPacket
                                {
                                    EntityId = -1,
                                    SenderId = 0,
                                    PType = PacketType.ActiveControlFullUpdate,
                                    Data = new ControllingPlayersSync
                                    {
                                        PlayersToControlledBlock = playerToBlocks
                                    }
                                };


                                var bytes = MyAPIGateway.Utilities.SerializeToBinary(syncPacket);
                                MyAPIGateway.Multiplayer.SendMessageTo(ClientPacketId, bytes, packet.SenderId);

                                report.PacketValid = true;
                            }
                            break;
                        }
                    default:
                        Reporter.ReportData[PacketType.Invalid].Add(report);
                        report.PacketValid = false;

                        break;
                }
            }
            catch (Exception ex) { Log.Line($"Exception in ReceivedPacket: {ex}"); }
        }

        internal void SyncWeapon(Weapon weapon, WeaponTimings timings, ref WeaponSyncValues weaponData, bool setState = true)
        {
            var comp = weapon.Comp;
            var cState = comp.State.Value;
            var wState = weapon.State;

            var wasReloading = wState.Sync.Reloading;

            if (setState)
            {
                comp.CurrentHeat -= weapon.State.Sync.Heat;
                cState.CurrentCharge -= weapon.State.Sync.CurrentCharge;


                weaponData.SetState(wState.Sync);

                comp.CurrentHeat += weapon.State.Sync.Heat;
                cState.CurrentCharge += weapon.State.Sync.CurrentCharge;
            }

            comp.WeaponValues.Timings[weapon.WeaponId] = timings;
            weapon.Timings = timings;

            var hasMags = weapon.State.Sync.CurrentMags > 0;
            var hasAmmo = weapon.State.Sync.CurrentAmmo > 0;

            var chargeFullReload = weapon.System.MustCharge && !wasReloading && !weapon.State.Sync.Reloading && !hasAmmo && (hasMags || !weapon.System.EnergyAmmo);
            var regularFullReload = !weapon.System.MustCharge && !wasReloading && !weapon.State.Sync.Reloading && !hasAmmo && hasMags;

            var chargeContinueReloading = weapon.System.MustCharge && !weapon.State.Sync.Reloading && wasReloading;
            var regularContinueReloading = !weapon.System.MustCharge && !hasAmmo && hasMags && ((!weapon.State.Sync.Reloading && wasReloading) || (weapon.State.Sync.Reloading && !wasReloading));

            if (chargeFullReload || regularFullReload)
                weapon.StartReload();

            else if (chargeContinueReloading || regularContinueReloading)
            {
                weapon.CancelableReloadAction += weapon.Reloaded;
                if (weapon.Timings.ReloadedTick > 0)
                    comp.Session.FutureEvents.Schedule(weapon.CancelableReloadAction, null, weapon.Timings.ReloadedTick);
                else
                    weapon.Reloaded();
            }
            else if (wasReloading && !weapon.State.Sync.Reloading && hasAmmo)
            {
                if (!weapon.System.MustCharge)
                    weapon.CancelableReloadAction -= weapon.Reloaded;

                weapon.EventTriggerStateChanged(EventTriggers.Reloading, false);
            }

            else if (weapon.System.MustCharge && weapon.State.Sync.Reloading && !weapon.Comp.Session.ChargingWeaponsCheck.Contains(weapon))
                weapon.ChargeReload();

            if (weapon.State.Sync.Heat > 0 && !weapon.HeatLoopRunning)
            {
                weapon.HeatLoopRunning = true;
                var delay = weapon.Timings.LastHeatUpdateTick > 0 ? weapon.Timings.LastHeatUpdateTick : 20;
                comp.Session.FutureEvents.Schedule(weapon.UpdateWeaponHeat, null, delay);
            }
        }

        public void UpdateActiveControlDictionary(MyCubeBlock block, long playerId, bool updateAdd)
        {
            var grid = block?.CubeGrid;

            if (block == null || grid == null) return;
            GridAi trackingAi;
            if (updateAdd) //update/add
            {
                if (GridTargetingAIs.TryGetValue(grid, out trackingAi) && Players.ContainsKey(playerId))
                    trackingAi.ControllingPlayers[playerId] = block;
            }
            else //remove
            {
                if (GridTargetingAIs.TryGetValue(grid, out trackingAi) && Players.ContainsKey(playerId))
                    trackingAi.ControllingPlayers.TryGetValue(playerId, out block);
            }
        }

        internal void MouseNetworkEvent()
        {
            if (IsClient)
            {
                PacketsToServer.Add(new MouseInputPacket
                {
                    EntityId = -1,
                    SenderId = MultiplayerId,
                    PType = PacketType.ClientMouseEvent,
                    Data = UiInput.ClientMouseState
                });
            }
            else if (MpActive && IsServer)
            {
                PacketsToClient.Add(new PacketInfo
                {
                    Entity = null,
                    Packet = new MouseInputPacket
                    {
                        EntityId = -1,
                        SenderId = MultiplayerId,
                        PType = PacketType.ClientMouseEvent,
                        Data = UiInput.ClientMouseState
                    }
                });
            }
        }

        internal void UpdateLocalAiNetworkEvent(MyCubeBlock controlBlock, bool active)
        {
            if (IsClient)
            {
                PacketsToServer.Add(new DictionaryUpdatePacket
                {
                    EntityId = controlBlock.EntityId,
                    SenderId = MultiplayerId,
                    PType = PacketType.ActiveControlUpdate,
                    Data = active
                });
            }
            else
            {
                PacketsToClient.Add(new PacketInfo
                {
                    Entity = controlBlock,
                    Packet = new DictionaryUpdatePacket
                    {
                        EntityId = controlBlock.EntityId,
                        SenderId = 0,
                        PType = PacketType.ActiveControlUpdate,
                        Data = active
                    }
                });
            }
        }

        internal void ProccessClientPackets()
        {
            for (int i = 0; i < PacketsToClient.Count; i++)
            {
                var packetInfo = PacketsToClient[i];
                PacketizeToClientsInRange(packetInfo.Entity, packetInfo.Packet);
            }
            PacketsToClient.Clear();
        }

        internal void ProccessServerPackets()
        {
            for (int i = 0; i < PacketsToServer.Count; i++)
                SendPacketToServer(PacketsToServer[i]);

            PacketsToServer.Clear();
        }

        internal struct PacketInfo
        {
            internal MyEntity Entity;
            internal Packet Packet;
        }
        #endregion
    }

    public class NetworkProccessor
    {
        private readonly Session _session;
        private readonly Dictionary<GridAi, GridWeaponPacket> _gridsToSync = new Dictionary<GridAi, GridWeaponPacket>();
        private readonly List<PacketInfo> _packets = new List<PacketInfo>();
        
        public NetworkProccessor(Session session)
        {
            _session = session;
        }

        internal void Proccess()
        {
            ProccessTargetUpdates();
            ProccessGridWeaponPackets();
        }

        internal void AddPackets()
        {
            for (int i = 0; i < _packets.Count; i++)
                _session.PacketsToClient.Add(_packets[i]);

            _packets.Clear();
        }

        private void ProccessTargetUpdates()
        {
            for (int i = 0; i < _session.WeaponsToSync.Count; i++)
            {
                var w = _session.WeaponsToSync[i];
                var ai = w.Comp.Ai;
                //need to pool to reduce allocations
                GridWeaponPacket gridSync;
                if (!_gridsToSync.ContainsKey(ai))
                {
                    gridSync = new GridWeaponPacket
                    {
                        EntityId = ai.MyGrid.EntityId,
                        SenderId = 0,
                        PType = PacketType.TargetUpdate,
                        Data = {Capacity = ai.NumSyncWeapons},
                    };
                    _gridsToSync[ai] = gridSync;
                }
                else gridSync = _gridsToSync[ai];

                var weaponSync = new WeaponData
                {
                    CompEntityId = w.Comp.MyCube.EntityId,
                    TargetData = w.Comp.WeaponValues.Targets[w.WeaponId],
                    Timmings = w.Timings.SyncOffsetServer(_session.Tick),
                    SyncData = w.State.Sync,
                };
                gridSync.Data.Add(weaponSync);
                ai.CurrWeapon++;
            }
            _session.WeaponsToSync.Clear();
        }

        private void ProccessGridWeaponPackets()
        {
            foreach (var gridPacket in _gridsToSync)
            {
                var ai = gridPacket.Key;
                ai.CurrWeapon = 0;
                ai.NumSyncWeapons = 0;
                _packets.Add(new PacketInfo { Entity = ai.MyGrid, Packet = gridPacket.Value });
            }
            _gridsToSync.Clear();
        } 
    }
}
