using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using WeaponCore.Platform;
using WeaponCore.Support;
using static WeaponCore.Support.Ai;
namespace WeaponCore
{
    public partial class Session
    {
        public void CleanClientPackets()
        {
            foreach (var packet in ClientPacketsToClean)
                PacketObjPool.Return(packet);

            ClientPacketsToClean.Clear();
        }

        public void ReproccessClientErrorPackets()
        {
            foreach (var packetObj in ClientSideErrorPkt)
            {
                var errorPacket = packetObj.ErrorPacket;
                var packet = packetObj.Packet;

                if (errorPacket.MaxAttempts == 0)  {
                    Log.LineShortDate($"        [ClientReprocessing] Entity:{packet.EntityId} - Type:{packet.PType}", "net");
                    //set packet retry variables, based on type
                    errorPacket.MaxAttempts = 7;
                    errorPacket.RetryDelayTicks = 15;
                    errorPacket.RetryTick = Tick + errorPacket.RetryDelayTicks;
                }

                if (errorPacket.RetryTick > Tick) continue;
                errorPacket.RetryAttempt++;
                var success = ProccessClientPacket(packetObj, false) || packetObj.Report.PacketValid;

                if (success || errorPacket.RetryAttempt > errorPacket.MaxAttempts)  {

                    if (!success)  
                        Log.LineShortDate($"        [BadReprocess] Entity:{packet.EntityId} Cause:{errorPacket.Error ?? string.Empty} Type:{packet.PType}", "net");
                    else Log.LineShortDate($"        [ReprocessSuccess] Entity:{packet.EntityId} - Type:{packet.PType} - Retries:{errorPacket.RetryAttempt}", "net");

                    ClientSideErrorPkt.Remove(packetObj);
                    ClientPacketsToClean.Add(packetObj);
                }
                else
                    errorPacket.RetryTick = Tick + errorPacket.RetryDelayTicks;
            }
            ClientSideErrorPkt.ApplyChanges();
        }

        private bool ClientConstruct(PacketObj data)
        {
            var packet = data.Packet;
            var myGrid = MyEntities.GetEntityByIdOrDefault(packet.EntityId) as MyCubeGrid;
            var cgPacket = (ConstructPacket)packet;
            if (myGrid == null) return Error(data, Msg($"Grid: {packet.EntityId}"));

            Ai ai;
            if (GridToMasterAi.TryGetValue(myGrid, out ai))
            {
                if (ai.MIds[(int)packet.PType] < packet.MId)  {
                    ai.MIds[(int)packet.PType] = packet.MId;

                    var rootConstruct = ai.Construct.RootAi.Construct;

                    rootConstruct.Data.Repo.Sync(rootConstruct, cgPacket.Data);
                    rootConstruct.UpdateLeafs();
                }
                else Log.Line($"ClientConstructGroups MID failure - mId:{packet.MId}");
            
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

            Ai ai;
            if (GridToMasterAi.TryGetValue(myGrid, out ai))
            {
                if (ai.MIds[(int)packet.PType] < packet.MId)  {
                    ai.MIds[(int)packet.PType] = packet.MId;

                    var rootConstruct = ai.Construct.RootAi.Construct;
                    rootConstruct.Data.Repo.FocusData.Sync(ai, fociPacket.Data);
                }
                else Log.Line($"ClientConstructFoci MID failure - mId:{packet.MId}");

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

            Ai ai;
            if (GridTargetingAIs.TryGetValue(myGrid, out ai)) {

                if (ai.MIds[(int)packet.PType] < packet.MId)  {
                    ai.MIds[(int)packet.PType] = packet.MId;

                    ai.Data.Repo.Sync(aiSyncPacket.Data);
                }
                else Log.Line($"ClientAiDataUpdate: mid fail - senderId:{packet.SenderId} - mId:{ai.MIds[(int)packet.PType]} >= {packet.MId}");

                data.Report.PacketValid = true;
            }
            else
                return Error(data, Msg($"GridAi not found, is marked:{myGrid.MarkedForClose}, has root:{GridToMasterAi.ContainsKey(myGrid)}"));

            return true;
        }

        private bool ClientCompData(PacketObj data)
        {
            var packet = data.Packet;
            var compDataPacket = (CompBasePacket)packet;
            var ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
            var comp = ent?.Components.Get<CoreComponent>();
            if (comp?.Ai == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return Error(data, Msg($"CompId: {packet.EntityId}", comp != null), Msg("Ai", comp?.Ai != null), Msg("Ai", comp?.Platform.State == CorePlatform.PlatformState.Ready));

            if (comp.MIds[(int)packet.PType] < packet.MId) {
                comp.MIds[(int)packet.PType] = packet.MId;

                comp.Data.Repo.Base.Sync(comp, compDataPacket.Data);
            }
            else Log.Line($"compDataSync: mid fail - senderId:{packet.SenderId} - mId:{comp.MIds[(int)packet.PType]} >= {packet.MId}");

            data.Report.PacketValid = true;

            return true;
        }

        private bool ClientStateUpdate(PacketObj data)
        {
            var packet = data.Packet;
            var compStatePacket = (CompStatePacket)packet;
            var ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
            var comp = ent?.Components.Get<CoreComponent>();
            if (comp?.Ai == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return Error(data, Msg($"CompId: {packet.EntityId}", comp != null), Msg("Ai", comp?.Ai != null), Msg("Ai", comp?.Platform.State == CorePlatform.PlatformState.Ready));

            if (comp.MIds[(int)packet.PType] < packet.MId)  {
                comp.MIds[(int)packet.PType] = packet.MId;

                comp.Data.Repo.Base.State.Sync(comp, compStatePacket.Data, CompStateValues.Caller.Direct);
            }

            data.Report.PacketValid = true;

            return true;
        }

        private bool ClientWeaponReloadUpdate(PacketObj data)
        {
            var packet = data.Packet;
            var weaponReloadPacket = (WeaponReloadPacket)packet;
            var ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
            var comp = ent?.Components.Get<CoreComponent>();
            if (comp?.Ai == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return Error(data, Msg($"CompId: {packet.EntityId}", comp != null), Msg("Ai", comp?.Ai != null), Msg("Ai", comp?.Platform.State == CorePlatform.PlatformState.Ready));
            
            var w = comp.Platform.Weapons[weaponReloadPacket.WeaponId];
            if (w.MIds[(int)packet.PType] < packet.MId)  {
                w.MIds[(int)packet.PType] = packet.MId;

                w.Reload.Sync(w, weaponReloadPacket.Data);
            }

            data.Report.PacketValid = true;

            return true;
        }

        private bool ClientTargetUpdate(PacketObj data)
        {
            var packet = data.Packet;
            var targetPacket = (TargetPacket)packet;
            var ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
            var comp = ent?.Components.Get<CoreComponent>();

            if (comp?.Ai == null || comp.Platform.State != CorePlatform.PlatformState.Ready ) return Error(data, Msg($"CompId: {packet.EntityId}", comp != null), Msg("Ai", comp?.Ai != null), Msg("Ai", comp?.Platform.State == CorePlatform.PlatformState.Ready));

            var w = comp.Platform.Weapons[targetPacket.Target.WeaponId];
            if (w.MIds[(int)packet.PType] < packet.MId)  {
                w.MIds[(int)packet.PType] = packet.MId;

                targetPacket.Target.SyncTarget(w);
            }

            data.Report.PacketValid = true;

            return true;
        }

        private bool ClientWeaponAmmoUpdate(PacketObj data)
        {
            var packet = data.Packet;
            var ammoPacket = (WeaponAmmoPacket)packet;
            var ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
            var comp = ent?.Components.Get<CoreComponent>();

            if (comp?.Ai == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return Error(data, Msg($"CompId: {packet.EntityId}", comp != null), Msg("Ai", comp?.Ai != null), Msg("Ai", comp?.Platform.State == CorePlatform.PlatformState.Ready));

            var w = comp.Platform.Weapons[ammoPacket.WeaponId];
            if (w.MIds[(int)packet.PType] < packet.MId) {
                w.MIds[(int)packet.PType] = packet.MId;

                w.Ammo.Sync(w, ammoPacket.Data);
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

            Ai ai;
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
                else Log.Line($"ClientFakeTargetUpdate: mid fail - senderId:{packet.SenderId} - mId:{ai.MIds[(int)packet.PType]} >= {packet.MId}");

                data.Report.PacketValid = true;
            }
            else
                return Error(data, Msg($"GridId: {packet.EntityId}", myGrid != null), Msg("Ai"));

            return true;
        }

        // no storge sync

        private bool ClientClientMouseEvent(PacketObj data)
        {
            var packet = data.Packet;
            var mousePacket = (InputPacket)packet;
            if (mousePacket.Data == null) return Error(data, Msg("Data"));
            var entity = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
            var topEntity = entity?.GetTopMostParent();
            if (topEntity == null) return Error(data, Msg($"entityId: {packet.EntityId}"));

            Ai ai;
            long playerId;
            if (GridToMasterAi.TryGetValue(topEntity, out ai) && SteamToPlayer.TryGetValue(packet.SenderId, out playerId))
            {
                if (ai.MIds[(int)packet.PType] < packet.MId)  {
                    ai.MIds[(int)packet.PType] = packet.MId;

                    PlayerMouseStates[playerId] = mousePacket.Data;
                }
                else Log.Line($"ClientClientMouseEvent: mid fail - senderId:{packet.SenderId} - mId:{ai.MIds[(int)packet.PType]} >= {packet.MId}");

                data.Report.PacketValid = true;
            }
            else
                return Error(data, Msg("No PlayerId Found"));

            return true;
        }

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

        private bool ClientServerData(PacketObj data)
        {
            var packet = data.Packet;
            var updatePacket = (ServerPacket)packet;

            ServerVersion = updatePacket.VersionString;
            Settings.VersionControl.UpdateClientEnforcements(updatePacket.Data);
            data.Report.PacketValid = true;
            Log.Line($"Server enforcement received");
            return true;
        }

        private bool ClientFullMouseUpdate(PacketObj data)
        {
            var packet = data.Packet;
            var mouseUpdatePacket = (MouseInputSyncPacket)packet;

            if (mouseUpdatePacket.Data == null) return Error(data, Msg("Data"));

            for (int i = 0; i < mouseUpdatePacket.Data.Length; i++)  {
                var playerMousePackets = mouseUpdatePacket.Data[i];
                if (playerMousePackets.PlayerId != PlayerId)
                    PlayerMouseStates[playerMousePackets.PlayerId] = playerMousePackets.MouseStateData;
            }

            data.Report.PacketValid = true;
            return true;
        }

        private bool ClientNotify(PacketObj data)
        {
            var packet = data.Packet;
            var clientNotifyPacket = (ClientNotifyPacket)packet;

            if (clientNotifyPacket.Message == string.Empty || clientNotifyPacket.Color == string.Empty) return Error(data, Msg("Data"));

            ShowClientNotify(clientNotifyPacket);
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

        private bool ClientQueueShot(PacketObj data)
        {
            var packet = data.Packet;
            var ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
            var queueShot = (QueuedShotPacket) packet;
            var comp = ent?.Components.Get<CoreComponent>();
            if (comp?.Ai == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return Error(data, Msg($"CompId: {packet.EntityId}", comp != null), Msg("Ai", comp?.Ai != null), Msg("Ai", comp?.Platform.State == CorePlatform.PlatformState.Ready));

            var w = comp.Platform.Weapons[queueShot.WeaponId];
            w.ShootOnce = true;
            if (PlayerId == queueShot.PlayerId)
                w.ClientStaticShot = true;

            data.Report.PacketValid = true;

            return true;
        }
    }
}
