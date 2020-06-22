using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using WeaponCore.Support;
using static WeaponCore.Session;

namespace WeaponCore
{
    public partial class Session
    {
        private void StringReceived(byte[] rawData)
        {
            try
            {
                var message = System.Text.Encoding.UTF8.GetString(rawData, 0, rawData.Length); 
                if (string.IsNullOrEmpty(message)) return;
                var firstChar = message[0];
                int logId;
                if (!int.TryParse(firstChar.ToString(), out logId))
                    return;
                message = message.Substring(1);

                switch (logId) {
                    case 0: {
                        Log.LineShortDate(message);
                        break;
                    }
                    case 1: {
                        Log.LineShortDate(message, "perf");
                        break;
                    }
                    case 2: {
                        Log.LineShortDate(message, "stats");
                        break;
                    }
                    case 3: { 
                        Log.LineShortDate(message, "net");
                        break;
                    }
                    case 4: {
                        Log.LineShortDate(message);
                        break;
                    }
                    default:
                        Log.LineShortDate(message);
                        break;
                }
            }
            catch (Exception ex) { Log.Line($"Exception in StringReceivedPacket: {ex}"); }
        }

        #region NewClientSwitch
        private void ClientReceivedPacket(byte[] rawData)
        {
            try
            {
                var packet = MyAPIGateway.Utilities.SerializeFromBinary<Packet>(rawData);
                if (packet == null) return;

                var packetSize = rawData.Length;
                var report = Reporter.ReportPool.Get();
                report.Receiver = NetworkReporter.Report.Received.Client;
                report.PacketSize = packetSize;
                Reporter.ReportData[packet.PType].Add(report);
                var errorPacket = new ErrorPacket {RecievedTick = Tick, Packet = packet};
                var packetObj = PacketObjPool.Get();
                packetObj.Packet = packet; packetObj.PacketSize = packetSize; packetObj.Report = report; packetObj.ErrorPacket = errorPacket;

                ProccessClientPacket(packetObj);
            }
            catch (Exception ex) { Log.Line($"Exception in ClientReceivedPacket: {ex}"); }
        }

        private bool ProccessClientPacket(PacketObj packetObj, bool firstRun = true)
        {
            try {
                var invalidType = false;
                switch (packetObj.Packet.PType) {
                    case PacketType.CompStateUpdate: {
                            ClientCompStateUpdate(packetObj);
                            break;
                        }
                    case PacketType.CompSettingsUpdate: {
                            ClientCompSettingsUpdate(packetObj);
                            break;
                        }
                    case PacketType.WeaponSyncUpdate: {
                            ClientWeaponSyncUpdate(packetObj);
                            break;
                        }
                    case PacketType.FakeTargetUpdate: {
                            ClientFakeTargetUpdate(packetObj);
                            break;
                        }
                    case PacketType.PlayerIdUpdate: {
                            ClientPlayerIdUpdate(packetObj); 
                            break;
                        }
                    case PacketType.ClientMouseEvent: {
                            ClientClientMouseEvent(packetObj);
                            break;
                        }
                    case PacketType.ActiveControlUpdate: {
                            ClientActiveControlUpdate(packetObj);
                            break;
                        }
                    case PacketType.ActiveControlFullUpdate: {
                            ClientActiveControlFullUpdate(packetObj);
                            break;
                        }
                    case PacketType.ReticleUpdate: {
                            ClientReticleUpdate(packetObj);
                            break;
                        }
                    case PacketType.OverRidesUpdate: {
                            ClientOverRidesUpdate(packetObj);
                            break;
                        }
                    case PacketType.PlayerControlUpdate: {
                            ClientPlayerControlUpdate(packetObj);
                            break;
                        }
                    case PacketType.TargetExpireUpdate: {
                            ClientTargetExpireUpdate(packetObj);
                            break;
                        }
                    case PacketType.FullMouseUpdate: {
                            ClientFullMouseUpdate(packetObj);
                            break;
                        }
                    case PacketType.CompToolbarShootState: {
                            ClientCompToolbarShootState(packetObj);
                            break;
                        }
                    case PacketType.RangeUpdate: {
                            ClientRangeUpdate(packetObj);
                            break;
                        }
                    case PacketType.GridAiUiMidUpdate: {
                            ClientGridAiUiMidUpdate(packetObj);
                            break;
                        }
                    case PacketType.CycleAmmo: {
                            ClientCycleAmmo(packetObj);
                            break;
                        }
                    case PacketType.GridOverRidesSync: {
                            ClientGridOverRidesSync(packetObj);
                            break;
                        }
                    case PacketType.RescanGroupRequest: {
                            ClientRescanGroupRequest(packetObj);
                            break;
                        }
                    case PacketType.GridFocusListSync: {
                            ClientGridFocusListSync(packetObj);
                            break;
                        }
                    case PacketType.ClientMidUpdate: {
                            ClientClientMidUpdate(packetObj);
                            break;
                        }
                    case PacketType.FocusUpdate:
                    case PacketType.ReassignTargetUpdate:
                    case PacketType.NextActiveUpdate:
                    case PacketType.ReleaseActiveUpdate: {
                            ClientFocusStates(packetObj);
                            break;
                        }
                    case PacketType.ProblemReport: {
                        ClientSentReport(packetObj);
                        break;
                    }
                    default:
                        if (!packetObj.ErrorPacket.Retry) Reporter.ReportData[PacketType.Invalid].Add(packetObj.Report);
                        Log.LineShortDate($"        [BadClientPacket] Type:{packetObj.Packet.PType} - Size:{packetObj.PacketSize}", "net");
                        invalidType = true;
                        packetObj.Report.PacketValid = false;
                        break;
                }
                if (!packetObj.Report.PacketValid && !invalidType && !packetObj.ErrorPacket.Retry && !packetObj.ErrorPacket.NoReprocess)
                {
                    if (!ClientSideErrorPkt.Contains(packetObj.ErrorPacket))
                        ClientSideErrorPkt.Add(packetObj.ErrorPacket);
                    else {
                        //this only works because hashcode override in ErrorPacket
                        ClientSideErrorPkt.Remove(packetObj.ErrorPacket);
                        ClientSideErrorPkt.Add(packetObj.ErrorPacket);
                    }
                }
                else if (packetObj.Report.PacketValid && ClientSideErrorPkt.Contains(packetObj.ErrorPacket))
                    ClientSideErrorPkt.Remove(packetObj.ErrorPacket);

                if (firstRun) 
                    ClientSideErrorPkt.ApplyChanges();

                if (packetObj.Report.PacketValid) {
                    PacketObjPool.Return(packetObj);
                    return true;
                }
                PacketObjPool.Return(packetObj);
            }
            catch (Exception ex) { Log.Line($"Exception in ProccessClientPacket: {ex} - packetSize:{packetObj?.PacketSize} - pObjNull:{packetObj == null} - packetNull:{packetObj?.Packet == null} - error:{packetObj?.ErrorPacket == null} - report:{packetObj?.Report == null}"); }
            return false;
        }
        #endregion

        #region NewServerSwitch
        internal void ProccessClientPacketsForServer()
        {
            if (!IsClient || !MpActive)
            {
                Log.Line($"trying to process client packets on a non-client");
                PacketsToServer.Clear();
                return;
            }

            for (int i = 0; i < PacketsToServer.Count; i++)
                MyModAPIHelper.MyMultiplayer.Static.SendMessageToServer(ServerPacketId, MyAPIGateway.Utilities.SerializeToBinary(PacketsToServer[i]), true);

            PacketsToServer.Clear();
        }

        private void ProccessServerPacket(byte[] rawData)
        {
            var packet = MyAPIGateway.Utilities.SerializeFromBinary<Packet>(rawData);
            if (packet == null) return;

            var packetSize = rawData.Length;

            var report = Reporter.ReportPool.Get();
            report.Receiver = NetworkReporter.Report.Received.Server;
            report.PacketSize = packetSize;
            Reporter.ReportData[packet.PType].Add(report);
            var errorPacket = new ErrorPacket { RecievedTick = Tick, Packet = packet, PType = packet.PType };

            var packetObj = PacketObjPool.Get();
            packetObj.Packet = packet; packetObj.PacketSize = packetSize; packetObj.Report = report; packetObj.ErrorPacket = errorPacket;

            switch (packetObj.Packet.PType) {

                case PacketType.CompStateUpdate: {
                    ServerCompStateUpdate(packetObj);
                    break;
                }
                case PacketType.CompSettingsUpdate: {
                    ServerCompSettingsUpdate(packetObj);
                    break;
                }
                case PacketType.ClientMouseEvent: {
                    ServerClientMouseEvent(packetObj);
                    break;
                }
                case PacketType.ActiveControlUpdate: {
                    ServerActiveControlUpdate(packetObj);
                    break;
                }
                case PacketType.FakeTargetUpdate: {
                    ServerFakeTargetUpdate(packetObj);
                    break;
                }
                case PacketType.GridSyncRequestUpdate: {
                    ServerGridSyncRequestUpdate(packetObj);
                    break;
                }
                case PacketType.ReticleUpdate: {
                    ServerReticleUpdate(packetObj);
                    break;
                }
                case PacketType.OverRidesUpdate: {
                    ServerOverRidesUpdate(packetObj);
                    break;
                }
                case PacketType.PlayerControlUpdate: {
                    ServerPlayerControlUpdate(packetObj);
                    break;
                }
                case PacketType.WeaponUpdateRequest: {
                    ServerWeaponUpdateRequest(packetObj);
                    break;
                }
                case PacketType.ClientEntityClosed: {
                    ServerClientEntityClosed(packetObj);
                    break;
                }
                case PacketType.RequestMouseStates: {
                    ServerRequestMouseStates(packetObj);
                    break;
                }
                case PacketType.CompToolbarShootState: {
                    ServerCompToolbarShootState(packetObj);
                    break;
                }
                case PacketType.RangeUpdate: {
                    ServerRangeUpdate(packetObj);
                    break;
                }
                case PacketType.CycleAmmo: {
                    ServerCycleAmmo(packetObj);
                    break;
                }
                case PacketType.RescanGroupRequest: {
                    ServerRescanGroupRequest(packetObj);
                    break;
                }
                case PacketType.FixedWeaponHitEvent: {
                    ServerFixedWeaponHitEvent(packetObj);
                    break;
                }
                case PacketType.CompSyncRequest: {
                    ServerCompSyncRequest(packetObj);
                    break;
                }
                case PacketType.FocusUpdate:
                case PacketType.ReassignTargetUpdate:
                case PacketType.NextActiveUpdate:
                case PacketType.ReleaseActiveUpdate: {
                    ServerFocusUpdate(packetObj);
                    break;
                }
                case PacketType.ProblemReport: {
                    ServerRequestReport(packetObj);
                    break;
                }
                case PacketType.TerminalMonitor: {
                    ServerTerminalMonitor(packetObj);
                    break;
                }
                default:
                    packetObj.Report.PacketValid = false;
                    Reporter.ReportData[PacketType.Invalid].Add(packetObj.Report);
                    break;
            }

            if (!packetObj.Report.PacketValid)
                Log.LineShortDate(packetObj.ErrorPacket.Error, "net");

            PacketObjPool.Return(packetObj);
        }
        #endregion

        #region ProcessRequests
        internal void ProccessServerPacketsForClients()
        {

            if ((!IsServer || !MpActive))
            {
                Log.Line($"trying to process server packets on a non-server");
                return;
            }

            for (int i = 0; i < PacketsToClient.Count; i++)
            {
                var packetInfo = PacketsToClient[i];

                var bytes = MyAPIGateway.Utilities.SerializeToBinary(packetInfo.Packet);

                if (packetInfo.SingleClient)
                    MyModAPIHelper.MyMultiplayer.Static.SendMessageTo(ClientPacketId, bytes, packetInfo.Packet.SenderId, true);
                else
                {
                    long entityId = packetInfo.Entity?.GetTopMostParent().EntityId ?? -1;

                    foreach (var p in Players.Values)
                    {
                        if (p.SteamUserId != packetInfo.Packet.SenderId && (packetInfo.Entity == null || (PlayerEntityIdInRange.ContainsKey(p.SteamUserId) && PlayerEntityIdInRange[p.SteamUserId].Contains(entityId))))
                            MyModAPIHelper.MyMultiplayer.Static.SendMessageTo(ClientPacketId, bytes, p.SteamUserId, true);
                    }
                }
            }
            PacketsToClient.Clear();
        }

        internal void ProccessGridResyncRequests()
        {
            var gridCompsToCheck = new Dictionary<GridAi, HashSet<long>>();
            
            for (int i = 0; i < ClientGridResyncRequests.Count; i++)
            {
                var comp = ClientGridResyncRequests[i];

                if (!gridCompsToCheck.ContainsKey(comp.Ai))
                    gridCompsToCheck[comp.Ai] = new HashSet<long>();

                gridCompsToCheck[comp.Ai].Add(comp.MyCube.EntityId);
            }

            ClientGridResyncRequests.Clear();

            foreach (var gridComps in gridCompsToCheck)
            {

                var packet = new RequestTargetsPacket
                {
                    EntityId = gridComps.Key.MyGrid.EntityId,
                    SenderId = MultiplayerId,
                    PType = PacketType.WeaponUpdateRequest,
                    Comps = new List<long>(gridComps.Value),
                };

                PacketsToServer.Add(packet);
            }

            gridCompsToCheck.Clear();
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
            for (int i = _session.WeaponsToSync.Count -1 ; i >= 0; i--)
            {
                var w = _session.WeaponsToSync[i];
                var ai = w.Comp.Ai;

                if (ai == null || w.Comp.MyCube == null || w.Comp.MyCube.MarkedForClose || w.Comp.MyCube.Closed)
                {
                    _session.WeaponsToSync.RemoveAtFast(i);
                    _session.WeaponsSyncCheck.Remove(w);
                    continue;
                }

                //need to pool to reduce allocations
                GridWeaponPacket gridSync;
                if (!_gridsToSync.ContainsKey(ai))
                {
                    gridSync = new GridWeaponPacket
                    {
                        EntityId = ai.MyGrid.EntityId,
                        SenderId = 0,
                        PType = PacketType.WeaponSyncUpdate,
                        Data = new List<WeaponData>(ai.NumSyncWeapons),
                    };
                    _gridsToSync[ai] = gridSync;
                }
                else gridSync = _gridsToSync[ai];

                var weaponSync = new WeaponData
                {
                    CompEntityId = w.Comp.MyCube.EntityId,
                    TargetData = null,
                    //Timmings = null,
                    SyncData = null,
                    WeaponRng = null,
                };

                if (w.SendTarget && w.Comp.WeaponValues.Targets != null)
                    weaponSync.TargetData = w.Comp.WeaponValues.Targets[w.WeaponId];
                else if (w.SendTarget)
                    continue;

                if (w.SendSync && w.State.Sync != null && w.Comp.WeaponValues.WeaponRandom != null)
                {
                    weaponSync.SyncData = w.State.Sync;

                    var rand = w.Comp.WeaponValues.WeaponRandom[w.WeaponId];
                    rand.TurretCurrentCounter = 0;
                    rand.ClientProjectileCurrentCounter = 0;
                    rand.CurrentSeed = w.UniqueId;
                    rand.TurretRandom = new Random(rand.CurrentSeed);
                    rand.ClientProjectileRandom = new Random(rand.CurrentSeed);
                    rand.AcquireRandom = new Random(rand.CurrentSeed);

                    weaponSync.WeaponRng = rand;
                }
                else if(w.SendSync)
                    continue;

                w.SendTarget = false;
                w.SendSync = false;

                gridSync.Data.Add(weaponSync);
                _session.WeaponsToSync.RemoveAtFast(i);
                _session.WeaponsSyncCheck.Remove(w);
            }
        }

        private void ProccessGridWeaponPackets()
        {
            foreach (var gridPacket in _gridsToSync)
            {
                var ai = gridPacket.Key;
                ai.NumSyncWeapons = 0;
                _packets.Add(new PacketInfo { Entity = ai.MyGrid, Packet = gridPacket.Value });
            }
            _gridsToSync.Clear();
        } 
    }
}
