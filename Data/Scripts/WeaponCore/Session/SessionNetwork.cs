using System;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using WeaponCore.Support;

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
                if (packet == null)
                {
                    Log.Line($"ClientReceivedPacket null packet");
                    return;
                }

                var packetSize = rawData.Length;
                var report = Reporter.ReportPool.Get();
                report.Receiver = NetworkReporter.Report.Received.Client;
                report.PacketSize = packetSize;
                Reporter.ReportData[packet.PType].Add(report);
                var packetObj = PacketObjPool.Get();
                packetObj.ErrorPacket.RecievedTick = Tick;
                packetObj.Packet = packet; packetObj.PacketSize = packetSize; packetObj.Report = report;
                ProccessClientPacket(packetObj);
            }
            catch (Exception ex) { Log.Line($"Exception in ClientReceivedPacket: {ex}"); }
        }

        private bool ProccessClientPacket(PacketObj packetObj, bool firstRun = true)
        {
            try {
                var invalidType = false;
                switch (packetObj.Packet.PType) {

                    case PacketType.AimTargetUpdate: 
                    {
                        ClientFakeTargetUpdate(packetObj);
                        break;
                    }
                    case PacketType.PlayerIdUpdate: 
                    {
                        ClientPlayerIdUpdate(packetObj); 
                        break;
                    }
                    case PacketType.ServerData:
                    {
                        ClientServerData(packetObj);
                        break;
                    }
                    case PacketType.ClientMouseEvent: 
                    {
                        ClientClientMouseEvent(packetObj);
                        break;
                    }
                    case PacketType.Construct:
                    {
                        ClientConstruct(packetObj);
                        break;
                    }
                    case PacketType.ConstructFoci:
                    {
                        ClientConstructFoci(packetObj);
                        break;
                    }
                    case PacketType.AiData: 
                    {
                        ClientAiDataUpdate(packetObj);
                        break;
                    }
                    case PacketType.CompState:
                    {
                        ClientStateUpdate(packetObj);
                        break;
                    }
                    case PacketType.WeaponReload:
                    {
                        ClientWeaponReloadUpdate(packetObj);
                        break;
                    }
                    case PacketType.WeaponAmmo:
                    {
                        ClientWeaponAmmoUpdate(packetObj);
                        break;
                    }
                    case PacketType.CompBase:
                    {
                        ClientCompData(packetObj);
                        break;
                    }
                    case PacketType.TargetChange:
                    {
                        ClientTargetUpdate(packetObj);
                        break;
                    }
                    case PacketType.FullMouseUpdate: 
                    {
                        ClientFullMouseUpdate(packetObj);
                        break;
                    }
                    case PacketType.QueueShot: 
                    {
                        ClientQueueShot(packetObj);
                        break;
                    }
                    case PacketType.ProblemReport: 
                    {
                        ClientSentReport(packetObj);
                        break;
                    }
                    case PacketType.ClientNotify:
                    {
                        ClientNotify(packetObj);
                        break;
                    }
                    case PacketType.EwaredBlocks:
                    {
                        ClientEwarBlocks(packetObj);
                        break;
                    }
                    case PacketType.Invalid:
                    {
                        Log.Line($"invalid packet: {packetObj.PacketSize} - {packetObj.Packet.PType}");
                        invalidType = true;
                        packetObj.Report.PacketValid = false;
                        break;
                    }
                    default:
                        Log.LineShortDate($"        [BadClientPacket] Type:{packetObj.Packet.PType} - Size:{packetObj.PacketSize}", "net");
                        Reporter.ReportData[PacketType.Invalid].Add(packetObj.Report);
                        invalidType = true;
                        packetObj.Report.PacketValid = false;
                        break;
                }
                if (firstRun && !packetObj.Report.PacketValid && !invalidType && !packetObj.ErrorPacket.Retry && !packetObj.ErrorPacket.NoReprocess)
                {
                    if (!ClientSideErrorPkt.Contains(packetObj))
                        ClientSideErrorPkt.Add(packetObj);
                    else
                        Log.Line($"ClientSideErrorPkt: this should be impossible: {packetObj.Packet.PType}");
                }

                if (firstRun)  {

                    ClientSideErrorPkt.ApplyChanges();
                    
                    if (!ClientSideErrorPkt.Contains(packetObj))  {
                        ClientPacketsToClean.Add(packetObj);
                        return true;
                    }
                }
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
                foreach (var packets in PacketsToServer)
                {
                    Log.Line($"trying to process client packets on a non-client: {packets.PType}");
                }
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

            var packetObj = PacketObjPool.Get();
            packetObj.ErrorPacket.RecievedTick = Tick;
            packetObj.Packet = packet; packetObj.PacketSize = packetSize; packetObj.Report = report;

            switch (packetObj.Packet.PType) {

                case PacketType.ClientMouseEvent: {
                    ServerClientMouseEvent(packetObj);
                    break;
                }
                case PacketType.ActiveControlUpdate: {
                    ServerActiveControlUpdate(packetObj);
                    break;
                }
                case PacketType.AimTargetUpdate: {
                    ServerAimTargetUpdate(packetObj);
                    break;
                }
                case PacketType.PaintedTargetUpdate:
                {
                    ServerMarkedTargetUpdate(packetObj);
                    break;
                }
                case PacketType.AmmoCycleRequest: {
                    ServerAmmoCycleRequest(packetObj);
                    break;
                }
                case PacketType.ReticleUpdate: {
                    ServerReticleUpdate(packetObj);
                    break;
                }
                case PacketType.PlayerControlRequest:
                {
                    ServerPlayerControlRequest(packetObj);
                    break;
                }
                case PacketType.ClientAiAdd:
                case PacketType.ClientAiRemove: {
                    ServerClientAiExists(packetObj);
                    break;
                }
                case PacketType.OverRidesUpdate: {
                    ServerOverRidesUpdate(packetObj);
                    break;
                }
                case PacketType.RequestMouseStates: {
                    ServerRequestMouseStates(packetObj);
                    break;
                }
                case PacketType.RequestShootUpdate: {
                    ServerRequestShootUpdate(packetObj);
                    break;
                }
                case PacketType.FixedWeaponHitEvent: {
                    ServerFixedWeaponHitEvent(packetObj);
                    break;
                }
                case PacketType.RequestSetRof:
                case PacketType.RequestSetGuidance:
                case PacketType.RequestSetOverload:
                case PacketType.RequestSetRange:
                case PacketType.RequestSetDps:
                {
                    ServerUpdateSetting(packetObj);
                    break;
                }
                case PacketType.FocusUpdate:
                case PacketType.FocusLockUpdate:
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

            PacketsToClient.AddRange(PrunedPacketsToClient.Values);
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
                        var notSender = p.SteamUserId != packetInfo.Packet.SenderId;
                        var sendPacket = notSender && packetInfo.Entity == null;
                        if (!sendPacket && notSender)
                        {
                            if (PlayerEntityIdInRange.ContainsKey(p.SteamUserId))
                            {
                                if (PlayerEntityIdInRange[p.SteamUserId].Contains(entityId)) {
                                    sendPacket = true;
                                }
                                else  {
                                    GridAi rootAi;
                                    var grid = packetInfo.Entity.GetTopMostParent() as MyCubeGrid;
                                    if (grid != null && GridToMasterAi.TryGetValue(grid, out rootAi) && PlayerEntityIdInRange[p.SteamUserId].Contains(rootAi.MyGrid.EntityId))
                                        sendPacket = true;
                                }
                            }
                        }

                        if (sendPacket)
                            MyModAPIHelper.MyMultiplayer.Static.SendMessageTo(ClientPacketId, bytes, p.SteamUserId, true);
                    }
                }
            }

            ServerPacketsForClientsClean();
        }

        private void ServerPacketsForClientsClean()
        {
            PacketsToClient.Clear();
            foreach (var pInfo in PrunedPacketsToClient.Values)
            {
                switch (pInfo.Packet.PType)
                {
                    case PacketType.AiData:
                    {
                        var iPacket = (AiDataPacket)pInfo.Packet;
                        PacketAiPool.Return(iPacket);
                        break;
                    }
                    case PacketType.CompBase:
                    {
                        var iPacket = (CompBasePacket)pInfo.Packet;
                        PacketCompBasePool.Return(iPacket);
                        break;
                    }
                    case PacketType.CompState:
                    {
                        var iPacket = (CompStatePacket)pInfo.Packet;
                        PacketStatePool.Return(iPacket);
                        break;
                    }
                    case PacketType.TargetChange:
                    {
                        var iPacket = (TargetPacket)pInfo.Packet;
                        PacketTargetPool.Return(iPacket);
                        break;
                    }
                    case PacketType.WeaponReload:
                    {
                        var iPacket = (WeaponReloadPacket)pInfo.Packet;
                        PacketReloadPool.Return(iPacket);
                        break;
                    }
                    case PacketType.Construct:
                    {
                        var iPacket = (ConstructPacket)pInfo.Packet;
                        PacketConstructPool.Return(iPacket);
                        break;
                    }
                    case PacketType.ConstructFoci:
                    {
                        var iPacket = (ConstructFociPacket)pInfo.Packet;
                        PacketConstructFociPool.Return(iPacket);
                        break;
                    }
                    case PacketType.WeaponAmmo:
                    {
                        var iPacket = (WeaponAmmoPacket)pInfo.Packet;
                        PacketAmmoPool.Return(iPacket);
                        break;
                    }
                }
            }
            PrunedPacketsToClient.Clear();
        }

        #endregion
    }
}
