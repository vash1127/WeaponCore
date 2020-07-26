﻿using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using WeaponCore.Platform;
using WeaponCore.Support;
using static WeaponCore.Support.GridAi;
namespace WeaponCore
{
    public partial class Session
    {
        public void ReproccessClientErrorPackets()
        {
            foreach (var errorPacket in ClientSideErrorPkt)
            {
                if (errorPacket?.Packet == null) {
                    Log.Line($"ClientSideErrorPktList [{errorPacket?.PType}] is null, errorPacketItselfIsNull:{errorPacket == null}");
                    if (errorPacket != null) ClientSideErrorPkt.Remove(errorPacket);
                    continue;
                }

                if (errorPacket.MaxAttempts == 0)
                {
                    Log.Line($"MaxAttempts was 0");
                    //set packet retry variables, based on type
                    errorPacket.MaxAttempts = 7;
                    errorPacket.RetryDelayTicks = 15;
                    errorPacket.RetryTick = Tick + errorPacket.RetryDelayTicks;
                }

                //proccess packet logic

                if (errorPacket.RetryTick > Tick) continue;
                errorPacket.RetryAttempt++;

                var report = Reporter.ReportPool.Get();
                report.Receiver = NetworkReporter.Report.Received.Client;
                report.PacketSize = 0;
                Reporter.ReportData[errorPacket.Packet.PType].Add(report);
                var packetObj = PacketObjPool.Get();
                packetObj.Packet = errorPacket.Packet; packetObj.PacketSize = 0; packetObj.Report = report;

                var success = ProccessClientPacket(packetObj, false);

                if (success || errorPacket.RetryAttempt > errorPacket.MaxAttempts)
                {
                    if (!success)
                    {
                        Log.LineShortDate($"        [BadReprocess] Entity:{errorPacket.Packet?.EntityId} Cause:{errorPacket.Error ?? string.Empty} Type:{errorPacket.PType}", "net");
                        PacketObjPool.Return(packetObj);
                    }

                    ClientSideErrorPkt.Remove(errorPacket);
                }
                else
                    errorPacket.RetryTick = Tick + errorPacket.RetryDelayTicks;

                if (errorPacket.MaxAttempts == 0)
                {
                    ClientSideErrorPkt.Remove(errorPacket);
                    PacketObjPool.Return(packetObj);
                }
            }
            ClientSideErrorPkt.ApplyChanges();
        }

        private bool ClientConstructGroups(PacketObj data)
        {
            var packet = data.Packet;
            var myGrid = MyEntities.GetEntityByIdOrDefault(packet.EntityId) as MyCubeGrid;
            var cgPacket = (ConstructGroupsPacket)packet;
            if (myGrid == null) return Error(data, Msg($"Grid: {packet.EntityId}"));

            GridAi ai;
            if (GridToMasterAi.TryGetValue(myGrid, out ai))
            {
                if (ai.MIds[(int)packet.PType] < packet.MId)  {
                    ai.MIds[(int)packet.PType] = packet.MId;

                    var rootConstruct = ai.Construct.RootAi.Construct;

                    //Log.Line($"ConstructGroupUpdate: isRoot:{ai == ai.Construct.RootAi} - newGroups:{cgPacket.Data.BlockGroups.Count} - oldGroups:{rootConstruct.Data.Repo.BlockGroups.Count} - Rev:{cgPacket.Data.FocusData.Revision} > {rootConstruct.Data.Repo.FocusData.Revision}");
                    rootConstruct.Data.Repo.Sync(rootConstruct, cgPacket.Data);
                    rootConstruct.BuildMenuGroups();

                    Wheel.Dirty = true;
                    if (Wheel.WheelActive && string.IsNullOrEmpty(Wheel.ActiveGroupName))
                        Wheel.ForceUpdate();

                }
                else Log.Line($"ClientAiDataUpdate MID failure - mId:{packet.MId}");
            
                data.Report.PacketValid = true;
            }
            else
                return Error(data, Msg($"GridAi not found, is marked:{myGrid.MarkedForClose}, has root:{GridToMasterAi.ContainsKey(myGrid)}"));

            return true;
        }

        private bool ClientConstructFoci(PacketObj data)
        {
            var packet = data.Packet;
            var myGrid = MyEntities.GetEntityByIdOrDefault(packet.EntityId) as MyCubeGrid;
            var fociPacket = (ConstructFociPacket)packet;
            if (myGrid == null) return Error(data, Msg($"Grid: {packet.EntityId}"));

            GridAi ai;
            if (GridToMasterAi.TryGetValue(myGrid, out ai))
            {
                if (ai.MIds[(int)packet.PType] < packet.MId)  {
                    ai.MIds[(int)packet.PType] = packet.MId;

                    var rootConstruct = ai.Construct.RootAi.Construct;
                    if (!rootConstruct.Data.Repo.FocusData.Sync(ai, fociPacket.Data))
                        Log.Line($"ClientConstructFoci old Revision: {fociPacket.Data.Revision} > {rootConstruct.Data.Repo.FocusData.Revision} - target:{fociPacket.Data.Target[0]}({rootConstruct.Data.Repo.FocusData.Target[0]})");
                }
                else Log.Line($"ClientAiDataUpdate MID failure - mId:{packet.MId}");

                data.Report.PacketValid = true;
            }
            else
                return Error(data, Msg($"GridAi not found, is marked:{myGrid.MarkedForClose}, has root:{GridToMasterAi.ContainsKey(myGrid)}"));

            return true;
        }

        private bool ClientAiDataUpdate(PacketObj data)
        {
            var packet = data.Packet;
            var myGrid = MyEntities.GetEntityByIdOrDefault(packet.EntityId) as MyCubeGrid;
            var aiSyncPacket = (AiDataPacket)packet;
            if (myGrid == null) return Error(data, Msg($"Grid: {packet.EntityId}"));

            GridAi ai;
            if (GridTargetingAIs.TryGetValue(myGrid, out ai))
            {

                if (ai.MIds[(int)packet.PType] < packet.MId)  {
                    ai.MIds[(int)packet.PType] = packet.MId;

                    ai.Data.Repo.Sync(aiSyncPacket.Data);
                    Wheel.Dirty = true;
                    if (Wheel.WheelActive && string.IsNullOrEmpty(Wheel.ActiveGroupName))
                        Wheel.ForceUpdate();
                }
                else Log.Line($"ClientAiDataUpdate MID failure - mId:{packet.MId}");

                data.Report.PacketValid = true;
            }
            else
                return Error(data, Msg($"GridAi not found, is marked:{myGrid.MarkedForClose}, has root:{GridToMasterAi.ContainsKey(myGrid)}"));

            return true;
        }

        private bool ClientCompData(PacketObj data)
        {
            var packet = data.Packet;
            var compDataPacket = (CompDataPacket)packet;
            var ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
            var comp = ent?.Components.Get<WeaponComponent>();
            if (comp?.Ai == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return Error(data, Msg($"CompId: {packet.EntityId}", comp != null), Msg("Ai", comp?.Ai != null), Msg("Ai", comp?.Platform.State == MyWeaponPlatform.PlatformState.Ready));

            if (comp.MIds[(int)packet.PType] < packet.MId)
            {
                comp.MIds[(int)packet.PType] = packet.MId;

                if (comp.Data.Repo.Sync(comp, compDataPacket.Data))
                {
                    Wheel.Dirty = true;
                    if (Wheel.WheelActive && string.IsNullOrEmpty(Wheel.ActiveGroupName))
                        Wheel.ForceUpdate();
                }
                else Log.Line($"compDataSync failed: {packet.PType}");
            }
            else Log.Line($"compDataSync mId failed: {packet.PType} - mId:{packet.MId}");

            data.Report.PacketValid = true;

            return true;
        }

        private bool ClientStateUpdate(PacketObj data)
        {
            var packet = data.Packet;
            var compStatePacket = (CompStatePacket)packet;
            var ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
            var comp = ent?.Components.Get<WeaponComponent>();
            if (comp?.Ai == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return Error(data, Msg($"CompId: {packet.EntityId}", comp != null), Msg("Ai", comp?.Ai != null), Msg("Ai", comp?.Platform.State == MyWeaponPlatform.PlatformState.Ready));

            if (comp.MIds[(int)PacketType.CompState] < packet.MId)  {
                comp.MIds[(int)PacketType.CompState] = packet.MId;

                comp.Data.Repo.State.Sync(comp, compStatePacket.Data, packet.PType == PacketType.StateNoAmmo);
            }

            switch (packet.PType)
            {
                case PacketType.CompState:
                case PacketType.StateNoAmmo:
                    break;
                case PacketType.StateReload:
                    for (int i = 0; i < comp.Platform.Weapons.Length; i++)
                    {
                        var w = comp.Platform.Weapons[i];
                        if (!w.Reloading && w.ActiveAmmoDef.AmmoDef.Const.Reloadable && !w.System.DesignatorWeapon)
                            w.Reload();
                    }
                    break;
            }
            data.Report.PacketValid = true;

            return true;
        }

        private bool ClientTargetUpdate(PacketObj data)
        {
            var packet = data.Packet;
            var targetPacket = (TargetPacket)packet;
            var ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
            var comp = ent?.Components.Get<WeaponComponent>();
            if (comp?.Ai == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return Error(data, Msg($"CompId: {packet.EntityId}", comp != null), Msg("Ai", comp?.Ai != null), Msg("Ai", comp?.Platform.State == MyWeaponPlatform.PlatformState.Ready));

            var w = comp.Platform.Weapons[targetPacket.Target.WeaponId];
            if (w.MIds[(int)packet.PType] < packet.MId)  {
                w.MIds[(int)packet.PType] = packet.MId;

                w.State.WeaponRandom.ResetRandom();
                targetPacket.Target.SyncTarget(w);
            }

            data.Report.PacketValid = true;

            return true;
        }


        private bool ClientFakeTargetUpdate(PacketObj data)
        {
            var packet = data.Packet;
            data.ErrorPacket.NoReprocess = true;
            var targetPacket = (FakeTargetPacket)packet;
            var myGrid = MyEntities.GetEntityByIdOrDefault(packet.EntityId) as MyCubeGrid;

            GridAi ai;
            if (myGrid != null && GridTargetingAIs.TryGetValue(myGrid, out ai))
            {
                if (ai.MIds[(int)packet.PType] < packet.MId) {
                    ai.MIds[(int)packet.PType] = packet.MId;

                    long playerId;
                    if (SteamToPlayer.TryGetValue(packet.SenderId, out playerId))
                    {
                        FakeTarget dummyTarget;
                        if (PlayerDummyTargets.TryGetValue(playerId, out dummyTarget))
                        {
                            dummyTarget.Update(targetPacket.Pos, ai, null, targetPacket.TargetId);
                        }
                        else
                            return Error(data, Msg("Player dummy target not found"));
                    }
                    else
                        return Error(data, Msg("SteamToPlayer missing Player"));
                }
                else Log.Line($"ClientFakeTargetUpdate comp MID failure - mId:{packet.MId}");

                data.Report.PacketValid = true;
            }
            else
                return Error(data, Msg($"GridId: {packet.EntityId}", myGrid != null), Msg("Ai"));

            return true;
        }

        // no storge sync
        private bool ClientPlayerIdUpdate(PacketObj data)
        {
            var packet = data.Packet;
            var updatePacket = (BoolUpdatePacket)packet;

            if (updatePacket.Data)
                PlayerConnected(updatePacket.EntityId);
            else //remove
                PlayerDisconnected(updatePacket.EntityId);

            data.Report.PacketValid = true;
            return true;
        }

        private bool ClientClientMouseEvent(PacketObj data)
        {
            var packet = data.Packet;
            var mousePacket = (InputPacket)packet;
            if (mousePacket.Data == null) return Error(data, Msg("Data"));

            long playerId;
            if (SteamToPlayer.TryGetValue(packet.SenderId, out playerId))
            {
                uint[] mIds;
                if (PlayerMIds.TryGetValue(packet.SenderId, out mIds) && mIds[(int)packet.PType] < packet.MId)  {
                    mIds[(int)packet.PType] = packet.MId;

                    PlayerMouseStates[playerId] = mousePacket.Data;
                }
                else Log.Line($"ClientClientMouseEvent: MidsHasSenderId:{PlayerMIds.ContainsKey(packet.SenderId)} - midsNull:{mIds == null} - senderId:{packet.SenderId}");

                data.Report.PacketValid = true;
            }
            else
                return Error(data, Msg("No PlayerId Found"));

            return true;
        }

        private bool ClientTargetExpireUpdate(PacketObj data)
        {
            var packet = data.Packet;
            var idPacket = (WeaponIdPacket)packet;
            data.ErrorPacket.NoReprocess = true;
            var ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
            var comp = ent?.Components.Get<WeaponComponent>();
            if (comp?.Ai == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return Error(data, Msg($"CompId: {packet.EntityId}", comp != null), Msg("Ai", comp?.Ai != null), Msg("Ai", comp?.Platform.State == MyWeaponPlatform.PlatformState.Ready));

            //saving on extra field with new packet type
            if (comp.MIds[(int)packet.PType] < packet.MId) {
                comp.MIds[(int)packet.PType] = packet.MId;

                comp.Platform.Weapons[idPacket.WeaponId].Target.Reset(Tick, Target.States.ServerReset);
            }
            else Log.Line($"ClientTargetExpireUpdate mid failure");
            
            data.Report.PacketValid = true;

            return true;
        }

        private bool ClientFullMouseUpdate(PacketObj data)
        {
            var packet = data.Packet;
            var mouseUpdatePacket = (MouseInputSyncPacket)packet;

            if (mouseUpdatePacket.Data == null) return Error(data, Msg("Data"));

            uint[] mIds;
            if (PlayerMIds.TryGetValue(packet.SenderId, out mIds) && mIds[(int)packet.PType] < packet.MId)  {
                mIds[(int)packet.PType] = packet.MId;

                for (int i = 0; i < mouseUpdatePacket.Data.Length; i++)  {
                    var playerMousePackets = mouseUpdatePacket.Data[i];
                    if (playerMousePackets.PlayerId != PlayerId)
                        PlayerMouseStates[playerMousePackets.PlayerId] = playerMousePackets.MouseStateData;
                }
            }
            else Log.Line($"ClientClientMouseEvent: MidsHasSenderId:{PlayerMIds.ContainsKey(packet.SenderId)} - midsNull:{mIds == null} - senderId:{packet.SenderId}");

            data.Report.PacketValid = true;
            return true;
        }

        // Unmanaged state changes below this point

        private bool ClientSentReport(PacketObj data)
        {
            var packet = data.Packet;
            var sentReportPacket = (ProblemReportPacket)packet;
            if (sentReportPacket.Data == null) return Error(data, Msg("SentReport"));
            Log.Line($"remote data received");
            ProblemRep.RemoteData = sentReportPacket.Data;
            data.Report.PacketValid = true;

            return true;

        }

        private bool ClientSendSingleShot(PacketObj data)
        {
            var packet = data.Packet;
            var ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
            var comp = ent?.Components.Get<WeaponComponent>();
            if (comp?.Ai == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return Error(data, Msg($"CompId: {packet.EntityId}", comp != null), Msg("Ai", comp?.Ai != null), Msg("Ai", comp?.Platform.State == MyWeaponPlatform.PlatformState.Ready));

            for (int i = 0; i < comp.Platform.Weapons.Length; i++)
                comp.Platform.Weapons[i].SingleShotCounter++;

            data.Report.PacketValid = true;

            return true;
        }
    }
}
