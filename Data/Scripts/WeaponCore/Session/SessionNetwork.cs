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
        internal void PacketizeToClientsInRange(MyEntity block, Packet packet)
        {
            try
            {
                var bytes = MyAPIGateway.Utilities.SerializeToBinary(packet);
                foreach (var p in Players.Values)
                {
                    var id = p.SteamUserId;
                    if (id != packet.SenderId && (block == null || Vector3D.DistanceSquared(p.GetPosition(), block.PositionComp.WorldAABB.Center) <= SyncBufferedDistSqr))
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

                        comp.State.Value = statePacket.Data;

                        for (int i = 0; i < comp.Platform.Weapons.Length; i++)
                        {
                            var w = comp.Platform.Weapons[i];
                            w.State = comp.State.Value.Weapons[w.WeaponId];
                        }

                        report.PacketValid = true;
                        break;
                    case PacketType.CompSettingsUpdate:
                        var setPacket = packet as SettingPacket;
                        if (setPacket?.Data == null || comp == null) return;

                        comp.Set.Value = setPacket.Data;

                        for (int i = 0; i < comp.Platform.Weapons.Length; i++)
                        {
                            var w = comp.Platform.Weapons[i];
                            w.Set = comp.Set.Value.Weapons[w.WeaponId];
                        }

                        report.PacketValid = true;
                        break;
                    case PacketType.TargetUpdate:
                        {
                            var targetPacket = packet as GridWeaponSyncPacket;
                            if (targetPacket?.TargetData == null || ent == null) return;
                            
                            for(int i = 0; i < targetPacket.TargetData.Length; i++)
                            {
                                var weaponData = targetPacket.TargetData[i];
                                var block = MyEntities.GetEntityByIdOrDefault(weaponData.CompEntityId) as MyCubeBlock;
                                comp = block?.Components.Get<WeaponComponent>();

                                if (comp == null) continue;

                                var weapon = comp.Platform.Weapons[weaponData.TargetData.WeaponId];
                                var timings = weaponData.Timmings.SyncOffsetClient(Tick);
                                var syncTarget = weaponData.TargetData;

                                SyncWeapon(weapon, timings, ref weaponData.WeaponData);
                                syncTarget.SyncTarget(weapon.Target);
                            }

                                report.PacketValid = true;
                            break;
                        }
                    case PacketType.FocusUpdate:
                        {
                            var myGrid = ent as MyCubeGrid;
                            GridAi ai;
                            if (myGrid != null && GridTargetingAIs.TryGetValue(myGrid, out ai))
                            {
                                var targetPacket = packet as FocusSyncPacket;
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
                                //PacketizeToClientsInRange(myGrid, packet);

                                PacketsToClient.Add(new PacketInfo { Entity = myGrid, Packet = packet });

                                report.PacketValid = true;
                            }

                            break;
                        }

                    /*case PacketType.WeaponSync:
                        Reporter.ReportData[packet.PType].Add(report);

                        var syncPacket = packet as WeaponSyncPacket;

                        if (comp != null && syncPacket != null)
                        {
                            var weaponData = syncPacket.WeaponData;
                            var wid = weaponData.WeaponId;
                            var weapon = comp.Platform.Weapons[wid];
                            var timings = syncPacket.Timmings.SyncOffsetClient(Tick);

                            SyncWeapon(weapon, timings, ref weaponData);

                            report.PacketValid = true;
                        }
                        break;*/

                    case PacketType.PlayerIdUpdate:
                        {
                            var updatePacket = packet as DictionaryUpdatePacket;
                            if (updatePacket == null) return;

                            if (updatePacket.Data) //update/add
                            {
                                SteamToPlayer[updatePacket.SenderId] = updatePacket.EntityId;
                                MouseState ms;
                                if (!PlayerMouseStates.TryGetValue(updatePacket.EntityId, out ms))
                                {
                                    PlayerMouseStates[updatePacket.EntityId] = new MouseState();

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
                                var csPacket = packet as ControllingSyncPacket;
                                if (csPacket?.Data == null) return;

                                for (int i = 0; i < csPacket.Data.PlayersToControlledBlock.Length; i++)
                                {
                                    var playerBlock = csPacket.Data.PlayersToControlledBlock[i];

                                    var block = MyEntities.GetEntityByIdOrDefault(playerBlock.EntityId) as MyCubeBlock;

                                    UpdateActiveControlDictionary(block, playerBlock.playerId, true);
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
                            comp.State.Value = statePacket.Data;

                            for (int i = 0; i < comp.Platform.Weapons.Length; i++)
                            {
                                var w = comp.Platform.Weapons[i];
                                w.State = comp.State.Value.Weapons[w.WeaponId];
                            }
                            //PacketizeToClientsInRange(ent, packet);
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
                            comp.Set.Value = setPacket.Data;
                            for (int i = 0; i < comp.Platform.Weapons.Length; i++)
                            {
                                var w = comp.Platform.Weapons[i];
                                w.Set = comp.Set.Value.Weapons[w.WeaponId];
                            }
                            //PacketizeToClientsInRange(ent, packet);
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
                            //PacketizeToClientsInRange(null, mousePacket);
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

                            //PacketizeToClientsInRange(block, dPacket);
                            PacketsToClient.Add(new PacketInfo { Entity = block, Packet = dPacket });

                            report.PacketValid = true;
                            break;
                        }
                    case PacketType.FocusUpdate:
                        {
                            var targetPacket = packet as FocusSyncPacket;
                            if (targetPacket == null) return;

                            var myGrid = MyEntities.GetEntityByIdOrDefault(packet.EntityId) as MyCubeGrid;
                            
                            GridAi ai;
                            if (myGrid != null && GridTargetingAIs.TryGetValue(myGrid, out ai))
                            {
                                var targetGrid = MyEntities.GetEntityByIdOrDefault(targetPacket.Data) as MyCubeGrid;

                                if (targetGrid != null)
                                {
                                    ai.Focus.AddFocus(targetGrid, ai, true);
                                    //PacketizeToClientsInRange(myGrid, targetPacket);
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
                                //PacketizeToClientsInRange(myGrid, packet);
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
                                    playerToBlocks[c] = new PlayerToBlock { playerId = playerBlockPair.Key, EntityId = playerBlockPair.Value.EntityId };
                                    c++;
                                }

                                var syncPacket = new ControllingSyncPacket
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

            var wasReloading = wState.Reloading;

            if (setState)
            {
                comp.CurrentHeat -= weapon.State.Heat;
                cState.CurrentCharge -= weapon.State.CurrentCharge;


                weaponData.SetState(wState);

                comp.CurrentHeat += weapon.State.Heat;
                cState.CurrentCharge += weapon.State.CurrentCharge;
            }

            comp.WeaponValues.Timings[weapon.WeaponId] = timings;
            weapon.Timings = timings;

            var hasMags = weapon.State.CurrentMags > 0;
            var hasAmmo = weapon.State.CurrentAmmo > 0;

            var chargeFullReload = weapon.System.MustCharge && !wasReloading && !weapon.State.Reloading && !hasAmmo && (hasMags || !weapon.System.EnergyAmmo);
            var regularFullReload = !weapon.System.MustCharge && !wasReloading && !weapon.State.Reloading && !hasAmmo && hasMags;

            var chargeContinueReloading = weapon.System.MustCharge && !weapon.State.Reloading && wasReloading;
            var regularContinueReloading = !weapon.System.MustCharge && !hasAmmo && hasMags && ((!weapon.State.Reloading && wasReloading) || (weapon.State.Reloading && !wasReloading));

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
            else if (wasReloading && !weapon.State.Reloading && hasAmmo)
            {
                if (!weapon.System.MustCharge)
                    weapon.CancelableReloadAction -= weapon.Reloaded;

                weapon.EventTriggerStateChanged(EventTriggers.Reloading, false);
            }

            else if (weapon.System.MustCharge && weapon.State.Reloading && !weapon.Comp.Session.ChargingWeaponsCheck.Contains(weapon))
                weapon.ChargeReload();

            if (weapon.State.Heat > 0 && !weapon.HeatLoopRunning)
            {
                weapon.HeatLoopRunning = true;
                var delay = weapon.Timings.LastHeatUpdateTick > 0 ? weapon.Timings.LastHeatUpdateTick : 20;
                comp.Session.FutureEvents.Schedule(weapon.UpdateWeaponHeat, null, delay);
            }
        }

        public void UpdateActiveControlDictionary(MyCubeBlock block, long playerId, bool updateAdd)
        {
            var grid = block?.CubeGrid as MyCubeGrid;

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

        internal void ProccessClientPackets()
        {
            for (int i = PacketsToClient.Count - 1; i >= 0; i--)
            {
                var packetInfo = PacketsToClient[i];
                PacketizeToClientsInRange(packetInfo.Entity, packetInfo.Packet);
                PacketsToClient.RemoveAtFast(i);
            }
        }

        internal void ProccessServerPackets()
        {
            for (int i = PacketsToServer.Count - 1; i >= 0; i--)
            {
                SendPacketToServer(PacketsToServer[i]);
                PacketsToServer.RemoveAtFast(i);
            }
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
        private readonly Dictionary<GridAi, GridWeaponSyncPacket> _gridsToSync = new Dictionary<GridAi, GridWeaponSyncPacket>();
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
            for(int i = 0; i < _packets.Count; i++)
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

                if (_gridsToSync[ai] == null)
                {
                    _gridsToSync[ai] = new GridWeaponSyncPacket
                    {
                        EntityId = ai.MyGrid.EntityId,
                        SenderId = 0,
                        PType = PacketType.TargetUpdate,
                        TargetData = new WeaponSync[ai.NumSyncWeapons],
                    };
                }

                _gridsToSync[ai].TargetData[ai.CurrWeapon] = new WeaponSync
                {
                    CompEntityId = w.Comp.MyCube.EntityId,
                    TargetData = w.Comp.WeaponValues.Targets[w.WeaponId],
                    Timmings = w.Timings.SyncOffsetServer(_session.Tick),
                    WeaponData = new WeaponSyncValues
                    {
                        Charging = w.State.Charging,
                        CurrentAmmo = w.State.CurrentAmmo,
                        currentMags = w.State.CurrentMags,
                        CurrentCharge = w.State.CurrentCharge,
                        Heat = w.State.Heat,
                        Overheated = w.State.Overheated,
                        Reloading = w.State.Reloading,
                        WeaponId = w.WeaponId,
                    }
                };
                ai.CurrWeapon++;
            }
        }

        private void ProccessGridWeaponPackets()
        {
            foreach (var gridPacket in _gridsToSync)
            {
                var ai = gridPacket.Key;
                ai.CurrWeapon = 0;
                ai.NumSyncWeapons = 0;
                _packets.Add(new PacketInfo { Entity = ai.MyGrid, Packet = gridPacket.Value });
                //_session.PacketizeToClientsInRange(ai.MyGrid, gridPacket.Value);                
            }
            _gridsToSync.Clear();
        } 
    }
}
