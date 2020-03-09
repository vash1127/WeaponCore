using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.Entity;
using VRageMath;
using WeaponCore.Platform;
using WeaponCore.Support;
using static WeaponCore.Session;
using static WeaponCore.Support.WeaponDefinition.AnimationDef.PartAnimationSetDef;

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
                    if (p.SteamUserId != packet.SenderId && (entity == null || Vector3D.DistanceSquared(p.GetPosition(), entity.PositionComp.WorldAABB.Center) <= SyncBufferedDistSqr))
                        MyModAPIHelper.MyMultiplayer.Static.SendMessageTo(ClientPacketId, bytes, p.SteamUserId, true);
                }
            }
            catch (Exception ex) { Log.Line($"Exception in PacketizeToClientsInRange: {ex}"); }
        }

        private void ClientReceivedPacket(byte[] rawData)
        {
            try
            {
                var packet = MyAPIGateway.Utilities.SerializeFromBinary<Packet>(rawData);
                if (packet == null) return;

                ProccessClientPacket(packet, rawData.Length);
            }
            catch (Exception ex) { Log.Line($"Exception in ClientReceivedPacket: {ex}"); }
        }

        private bool ProccessClientPacket(Packet packet, int packetSize, bool retry = false)
        {
            try
            {
                var report = Reporter.ReportPool.Get();
                report.Receiver = NetworkReporter.Report.Received.Client;
                report.PacketSize = packetSize;

                var ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
                var comp = ent?.Components.Get<WeaponComponent>();

                if (!retry) Reporter.ReportData[packet.PType].Add(report);

                var invalidType = false;

                //TODO pool error packets for quicker checks if a valid packet should remove from list
                var errorPacket = new ErrorPacket { RecievedTick = Tick, Packet = packet };

                switch (packet.PType)
                {
                    case PacketType.CompStateUpdate:                        
                        var statePacket = packet as StatePacket;
                        if (statePacket?.Data == null || comp == null)
                        {
                            errorPacket.Error = $"Data was null: {statePacket?.Data == null} Comp was null: {comp == null}";
                            break;
                        }

                        comp.State.Value.Sync(statePacket.Data);

                        report.PacketValid = true;
                        break;
                    case PacketType.CompSettingsUpdate:
                        var setPacket = packet as SettingPacket;
                        if (setPacket?.Data == null || comp == null)
                        {
                            errorPacket.Error = $"Data was null: {setPacket?.Data == null} Comp was null: {comp == null}";
                            break;
                        }

                        comp.Set.Value.Sync(comp, setPacket.Data);
                        
                        report.PacketValid = true;
                        break;
                    case PacketType.TargetUpdate:
                    {
                            var targetPacket = packet as GridWeaponPacket;
                            if (targetPacket?.Data == null || ent == null) {
                                errorPacket.Error = $"Data was null: {targetPacket?.Data == null} Grid was null: {ent == null}";

                                break;
                            }
                            
                            for(int i = 0; i < targetPacket.Data.Count; i++)
                            {
                                var weaponData = targetPacket.Data[i];
                                var block = MyEntities.GetEntityByIdOrDefault(weaponData.CompEntityId) as MyCubeBlock;
                                comp = block?.Components.Get<WeaponComponent>();

                                if (comp == null) continue;//possible retry condition

                                var weapon = comp.Platform.Weapons[weaponData.TargetData.WeaponId];
                                
                                var syncTarget = weaponData.TargetData;

                                if (weaponData.Timmings != null && weaponData.SyncData != null)
                                {
                                    var timings = weaponData.Timmings.SyncOffsetClient(Tick);
                                    SyncWeapon(weapon, timings, ref weaponData.SyncData);
                                }

                                syncTarget.SyncTarget(weapon.Target);
                            }

                            report.PacketValid = true;
                            break;
                        }
                    case PacketType.FocusUpdate:
                        {
                            var targetPacket = packet as FocusPacket;
                            if (targetPacket == null)
                            {
                                errorPacket.Error = $"Packet was null: {targetPacket == null}";
                                break;
                            }

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
                                else
                                    errorPacket.Error = $"targetGrid was null";
                            }
                            else
                                errorPacket.Error = $"myGrid was null {myGrid == null} GridTargetingAIs Not Found";
                            break;
                        }
                    case PacketType.FakeTargetUpdate:
                        {
                            var targetPacket = packet as FakeTargetPacket;
                            if (targetPacket?.Data == null)
                            {
                                errorPacket.Error = $"Data was null: {targetPacket?.Data == null}";
                                break;
                            }

                            var myGrid = MyEntities.GetEntityByIdOrDefault(packet.EntityId) as MyCubeGrid;

                            GridAi ai;
                            if (myGrid != null && GridTargetingAIs.TryGetValue(myGrid, out ai))
                            {
                                ai.DummyTarget.Update(targetPacket.Data, ai, null, true);
                                report.PacketValid = true;
                            }
                            else
                                errorPacket.Error = $"myGrid was null {myGrid == null} GridTargetingAIs Not Found";

                            break;
                        }

                    case PacketType.PlayerIdUpdate:
                        {
                            var updatePacket = packet as BoolUpdatePacket;
                            if (updatePacket == null)
                            {
                                errorPacket.Error = $"updatePacket was null {updatePacket == null}";
                                break;
                            }

                            if (updatePacket.Data)
                                PlayerConnected(updatePacket.EntityId);
                            else //remove
                                PlayerDisconnected(updatePacket.EntityId);

                            report.PacketValid = true;
                            break;
                        }
                    case PacketType.ClientMouseEvent:
                        var mousePacket = packet as MouseInputPacket;
                        if (mousePacket?.Data == null)
                        {
                            errorPacket.Error = $"Data was null {mousePacket?.Data == null}";
                            break;
                        }

                        long playerId;
                        if (SteamToPlayer.TryGetValue(packet.SenderId, out playerId))
                        {
                            PlayerMouseStates[playerId] = mousePacket.Data;

                            report.PacketValid = true;
                        }
                        else
                            errorPacket.Error = "No Player Mouse State Found";

                        break;
                    case PacketType.ActiveControlUpdate:
                        {
                            var dPacket = packet as BoolUpdatePacket;

                            var block = MyEntities.GetEntityByIdOrDefault(packet.EntityId) as MyCubeBlock;

                            if (block == null || dPacket?.Data == null)
                            {
                                errorPacket.Error = $"Data was null {dPacket?.Data == null} block was null {block == null}";
                                break;
                            }

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
                                if (csPacket?.Data == null)
                                {
                                    errorPacket.Error = $"Data was null {csPacket?.Data == null}";
                                    break;
                                }

                                for (int i = 0; i < csPacket.Data.PlayersToControlledBlock.Length; i++)
                                {
                                    var playerBlock = csPacket.Data.PlayersToControlledBlock[i];

                                    var block = MyEntities.GetEntityByIdOrDefault(playerBlock.EntityId) as MyCubeBlock;

                                    UpdateActiveControlDictionary(block, playerBlock.PlayerId, true);
                                }
                            }
                            catch (Exception e) {
                                errorPacket.Error = $" Error in Full Update {e}";
                            }

                            report.PacketValid = true;
                            break;
                        }
                    case PacketType.ReticleUpdate:

                        var reticlePacket = packet as BoolUpdatePacket;

                        if (reticlePacket == null || comp == null)
                        {
                            errorPacket.Error = $"reticlePacket was null {reticlePacket == null} Comp was null: {comp == null}";
                            break;
                        }

                        if (reticlePacket.Data)
                            comp.OtherPlayerTrackingReticle = true;
                        else
                            comp.OtherPlayerTrackingReticle = false;

                        report.PacketValid = true;
                        break;

                    case PacketType.OverRidesUpdate:
                        var overRidesPacket = packet as OverRidesPacket;

                        if (comp == null || overRidesPacket == null)
                        {
                            errorPacket.Error = $"overRidesPacket was null {overRidesPacket == null} Comp was null: {comp == null}";
                            break;
                        }

                        comp.Set.Value.Overrides.Sync(overRidesPacket.Data);
                        comp.Set.Value.MId = overRidesPacket.MId;
                        report.PacketValid = true;
                        break;

                    case PacketType.PlayerControlUpdate:
                        ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
                        comp = ent?.Components.Get<WeaponComponent>();
                        var cPlayerPacket = packet as ControllingPlayerPacket;

                        if (comp == null || cPlayerPacket == null)
                        {
                            errorPacket.Error = $"cPlayerPacket was null {cPlayerPacket == null} Comp was null: {comp == null}";
                            break;
                        }

                        comp.State.Value.CurrentPlayerControl.Sync(cPlayerPacket.Data);
                        comp.Set.Value.MId = cPlayerPacket.MId;
                        report.PacketValid = true;

                        break;

                    case PacketType.TargetExpireUpdate:
                        ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
                        comp = ent?.Components.Get<WeaponComponent>();

                        var idPacket = packet as WeaponIdPacket;

                        if (comp == null || idPacket == null)
                        {
                            errorPacket.Error = $"idPacket was null {idPacket == null} Comp was null: {comp == null}";
                            break;
                        }
                        //saving on extra field with new packet type
                        comp.Platform.Weapons[idPacket.WeaponId].Target.Reset(Tick);

                        report.PacketValid = true;
                        break;

                    default:
                        if(!retry) Reporter.ReportData[PacketType.Invalid].Add(report);
                        invalidType = true;
                        report.PacketValid = false;

                        break;
                }

                if (!report.PacketValid && !invalidType && !retry)
                {
                    Log.Line($"Invalid Packet: {packet.PType} Occured");
                    if (!ClientSideErrorPktList.Contains(errorPacket))
                    {
                        ClientSideErrorPktList.Add(errorPacket);
                        Log.Line($"Invalid Packet: {packet.PType} Entity: {packet.EntityId} Added");
                    }
                    else
                    {
                        Log.Line($"Invalid Packet: {packet.PType} Entity: {packet.EntityId} Replaced");
                        //this only works because hashcode override in ErrorPacket
                        ClientSideErrorPktList.Remove(errorPacket);
                        ClientSideErrorPktList.Add(errorPacket);
                    }
                }
                else if(report.PacketValid && ClientSideErrorPktList.Contains(errorPacket))
                    ClientSideErrorPktList.Remove(errorPacket);

                return report.PacketValid;
            }
            catch (Exception ex) { Log.Line($"Exception in ReceivedPacket: {ex}"); return false; }
        }

        public void ReproccessClientErrorPackets()
        {
            for(int i = ClientSideErrorPktList.Count - 1; i >= 0; i--)
            {
                var erroredPacket = ClientSideErrorPktList[i];
                if (erroredPacket.MaxAttempts == 0)
                {
                    //set packet retry variables, based on type
                    //erroredPacket.MaxAttempts = 3;
                    //erroredPacket.RetryAttempt = 0;                    

                    switch (erroredPacket.PType)
                    {
                        case PacketType.TargetUpdate:
                            erroredPacket.MaxAttempts = 7;
                            erroredPacket.RetryDelayTicks = 15;                            
                            break;

                        default:
                            erroredPacket.MaxAttempts = 7;
                            erroredPacket.RetryDelayTicks = 15;
                            break;
                    }

                    erroredPacket.RetryTick = Tick + erroredPacket.RetryDelayTicks;
                }

                //proccess packet logic

                if (erroredPacket.RetryTick > Tick) continue;
                erroredPacket.RetryAttempt++;

                var success = false;

                switch (erroredPacket.PType)
                {
                    case PacketType.TargetUpdate:
                        var ent = MyEntities.GetEntityByIdOrDefault(erroredPacket.Packet.EntityId);
                        if (ent == null) break;

                        var packet = erroredPacket.Packet as GridWeaponPacket;
                        if (packet == null)
                        {
                            erroredPacket.MaxAttempts = 0;
                            break;
                        }

                        var compsToCheck = new HashSet<long>();
                        for(int j = 0; j < packet.Data.Count; j++)
                        {
                            var block = MyEntities.GetEntityByIdOrDefault(packet.Data[j].CompEntityId);
                            if (!compsToCheck.Contains(packet.Data[j].CompEntityId))
                                compsToCheck.Add(packet.Data[j].CompEntityId);
                        }
                        var compsArr = new long[compsToCheck.Count];
                        compsToCheck.CopyTo(compsArr);

                        PacketsToServer.Add(new RequestTargetsPacket {
                            EntityId = erroredPacket.Packet.EntityId,
                            SenderId = MultiplayerId,
                            PType = PacketType.TargetUpdateRequest,
                            Comps = compsArr
                        });

                        success = true;
                        break;

                    default:
                        success = ProccessClientPacket(erroredPacket.Packet, 0, true);                        
                        break;
                }

                if (success || erroredPacket.RetryAttempt > erroredPacket.MaxAttempts)
                {
                    if (!success)
                        Log.Line($"Invalid Packet: {erroredPacket.PType} Entity: {erroredPacket.Packet.EntityId} Failed to reproccess, Error Cause: {erroredPacket.Error}");
                    else
                        Log.Line($"Invalid Packet: {erroredPacket.PType} Entity: {erroredPacket.Packet.EntityId} Reproccessed Successfully, Error Cause: {erroredPacket.Error}");

                    ClientSideErrorPktList.Remove(erroredPacket);
                }
                else
                    erroredPacket.RetryTick = Tick + erroredPacket.RetryDelayTicks;

                if (erroredPacket.MaxAttempts == 0)
                    ClientSideErrorPktList.Remove(erroredPacket);
            }
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

                        ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId, null, true);
                        comp = ent?.Components.Get<WeaponComponent>();
                        if (comp == null) return;
                        if (ent.MarkedForClose)
                        {
                            report.EntityClosed = true;
                            return;
                        }

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

                        ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId, null, true);
                        comp = ent?.Components.Get<WeaponComponent>();
                        if (comp == null) return;
                        if (ent.MarkedForClose)
                        {
                            report.EntityClosed = true;
                            return;
                        }

                        if (setPacket.Data.MId > comp.Set.Value.MId)
                        {
                            comp.Set.Value.Sync(comp, setPacket.Data);
                            PacketsToClient.Add(new PacketInfo { Entity = ent, Packet = setPacket });

                            report.PacketValid = true;
                        }
                        break;

                    case PacketType.ClientMouseEvent:

                        var mousePacket = packet as MouseInputPacket;
                        ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId, null, true);

                        if (mousePacket?.Data == null || ent == null) return;
                        if (ent.MarkedForClose)
                        {
                            report.EntityClosed = true;
                            return;
                        }

                        if (SteamToPlayer.TryGetValue(packet.SenderId, out playerId))
                        {
                            PlayerMouseStates[playerId] = mousePacket.Data;
                            PacketsToClient.Add(new PacketInfo { Entity = ent, Packet = mousePacket });

                            report.PacketValid = true;
                        }

                        break;

                    case PacketType.ActiveControlUpdate:
                        {
                            var dPacket = packet as BoolUpdatePacket;
                            if (dPacket?.Data == null) return;

                            var block = MyEntities.GetEntityByIdOrDefault(packet.EntityId, null, true) as MyCubeBlock;
                            if (block == null) return;
                            if (block.MarkedForClose)
                            {
                                report.EntityClosed = true;
                                return;
                            }

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

                            var myGrid = MyEntities.GetEntityByIdOrDefault(packet.EntityId, null, true) as MyCubeGrid;
                            if (myGrid == null) return;
                            if (myGrid.MarkedForClose)
                            {
                                report.EntityClosed = true;
                                return;
                            }

                            GridAi ai;
                            if (GridTargetingAIs.TryGetValue(myGrid, out ai))
                            {
                                var targetGrid = MyEntities.GetEntityByIdOrDefault(targetPacket.Data, null, true) as MyCubeGrid;

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

                            var myGrid = MyEntities.GetEntityByIdOrDefault(packet.EntityId, null, true) as MyCubeGrid;
                            if (myGrid == null) return;
                            if (myGrid.MarkedForClose)
                            {
                                report.EntityClosed = true;
                                return;
                            }

                            GridAi ai;
                            if (GridTargetingAIs.TryGetValue(myGrid, out ai))
                            {
                                ai.DummyTarget.Update(targetPacket.Data, ai, null, true);
                                PacketsToClient.Add(new PacketInfo { Entity = myGrid, Packet = targetPacket });
                                report.PacketValid = true;
                            }

                            break;
                        }
                    case PacketType.GridSyncRequestUpdate:
                        {
                            var myGrid = MyEntities.GetEntityByIdOrDefault(packet.EntityId, null, true) as MyCubeGrid;
                            if (myGrid == null) return;
                            if (myGrid.MarkedForClose)
                            {
                                report.EntityClosed = true;
                                return;
                            }

                            GridAi ai;
                            if (GridTargetingAIs.TryGetValue(myGrid, out ai))
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
                                    EntityId = packet.EntityId,
                                    SenderId = packet.SenderId,
                                    PType = PacketType.ActiveControlFullUpdate,
                                    Data = new ControllingPlayersSync
                                    {
                                        PlayersToControlledBlock = playerToBlocks
                                    }
                                };

                                PacketsToClient.Add(new PacketInfo {
                                    Entity = myGrid,
                                    Packet = syncPacket,
                                    SingleClient = true,
                                });

                                var gridPacket = new GridWeaponPacket
                                {
                                    EntityId = packet.EntityId,
                                    SenderId = packet.SenderId,
                                    PType = PacketType.TargetUpdate,
                                    Data = new List<WeaponData>()
                                };

                                List<WeaponComponent> comps = new List<WeaponComponent>(ai.WeaponBase.Values);

                                for (int i = 0; i < comps.Count; i++)
                                {
                                    comp = comps[i];
                                    if (comp.MyCube == null || comp.MyCube.MarkedForClose || comp.MyCube.Closed) continue;

                                    for (int j = 0; j < comp.Platform.Weapons.Length; j++)
                                    {
                                        var w = comp.Platform.Weapons[j];
                                        var weaponData = new WeaponData
                                        {
                                            CompEntityId = comp.MyCube.EntityId,
                                            SyncData = w.State.Sync,
                                            Timmings = w.Timings.SyncOffsetServer(Tick),
                                            TargetData = comp.WeaponValues.Targets[j],
                                        };

                                        gridPacket.Data.Add(weaponData);
                                    }
                                }

                                if (gridPacket.Data.Count > 0)
                                    PacketsToClient.Add(new PacketInfo
                                    {
                                        Entity = myGrid,
                                        Packet = gridPacket,
                                        SingleClient = true,
                                    });

                                report.PacketValid = true;
                            }
                            break;
                        }
                    case PacketType.ReticleUpdate:
                        {
                            var reticlePacket = packet as BoolUpdatePacket;
                            ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId, null, true);
                            comp = ent?.Components.Get<WeaponComponent>();

                            if (reticlePacket == null || comp == null) return;
                            if (ent.MarkedForClose)
                            {
                                report.EntityClosed = true;
                                return;
                            }

                            if (reticlePacket.Data)
                                comp.TrackReticle = true;
                            else
                                comp.TrackReticle = false;

                            PacketsToClient.Add(new PacketInfo { Entity = ent, Packet = reticlePacket });

                            report.PacketValid = true;
                            break;
                        }
                    case PacketType.OverRidesUpdate:
                        ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId, null, true);
                        comp = ent?.Components.Get<WeaponComponent>();
                        var overRidesPacket = packet as OverRidesPacket;

                        if (comp == null || overRidesPacket == null || comp.Set.Value.MId >= overRidesPacket.MId) return;
                        if (ent.MarkedForClose)
                        {
                            report.EntityClosed = true;
                            return;
                        }

                        comp.Set.Value.Overrides.Sync(overRidesPacket.Data);
                        comp.Set.Value.MId = overRidesPacket.MId;
                        report.PacketValid = true;

                        PacketsToClient.Add(new PacketInfo {Entity = comp.MyCube, Packet = overRidesPacket });
                        break;

                    case PacketType.PlayerControlUpdate:
                        ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId, null, true);
                        comp = ent?.Components.Get<WeaponComponent>();
                        var cPlayerPacket = packet as ControllingPlayerPacket;

                        if (comp == null || cPlayerPacket == null || comp.Set.Value.MId >= cPlayerPacket.MId) return;
                        if (ent.MarkedForClose)
                        {
                            report.EntityClosed = true;
                            return;
                        }

                        comp.State.Value.CurrentPlayerControl.Sync(cPlayerPacket.Data);
                        comp.Set.Value.MId = cPlayerPacket.MId;
                        report.PacketValid = true;

                        PacketsToClient.Add(new PacketInfo { Entity = comp.MyCube, Packet = cPlayerPacket });
                        break;

                    case PacketType.TargetUpdateRequest:
                        {
                            var targetRequestPacket = packet as RequestTargetsPacket;
                            var myGrid = MyEntities.GetEntityByIdOrDefault(packet.EntityId, null, true) as MyCubeGrid;

                            if (myGrid == null || targetRequestPacket == null) break;
                            GridAi ai;
                            if(GridTargetingAIs.TryGetValue(myGrid, out ai))
                            {
                                var gridPacket = new GridWeaponPacket {
                                    EntityId = packet.EntityId,
                                    SenderId = packet.SenderId,
                                    PType = PacketType.TargetUpdate,
                                    Data = new List<WeaponData>()
                                };

                                for(int i = 0; i < targetRequestPacket.Comps.Length; i++)
                                {
                                    var compId = targetRequestPacket.Comps[i];
                                    var compCube = MyEntities.GetEntityByIdOrDefault(compId, null, true) as MyCubeBlock;

                                    if (compCube == null || !ai.WeaponBase.TryGetValue(compCube, out comp)) continue;

                                    for (int j = 0; j < comp.Platform.Weapons.Length; j++)
                                    {
                                        var w = comp.Platform.Weapons[j];
                                        var weaponData = new WeaponData
                                        {
                                            CompEntityId = compId,
                                            SyncData = w.State.Sync,
                                            Timmings = w.Timings.SyncOffsetServer(Tick),
                                            TargetData = comp.WeaponValues.Targets[j],
                                        };

                                        gridPacket.Data.Add(weaponData);
                                    }                                        
                                }

                                if (gridPacket.Data.Count > 0)
                                    PacketsToClient.Add(new PacketInfo {
                                        Entity = myGrid,
                                        Packet = gridPacket,
                                        SingleClient = true,
                                    });

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

            var chargeFullReload = weapon.ActiveAmmoDef.Const.MustCharge && !wasReloading && !weapon.State.Sync.Reloading && !hasAmmo && (hasMags || !weapon.ActiveAmmoDef.Const.EnergyAmmo);
            var regularFullReload = !weapon.ActiveAmmoDef.Const.MustCharge && !wasReloading && !weapon.State.Sync.Reloading && !hasAmmo && hasMags;

            var chargeContinueReloading = weapon.ActiveAmmoDef.Const.MustCharge && !weapon.State.Sync.Reloading && wasReloading;
            var regularContinueReloading = !weapon.ActiveAmmoDef.Const.MustCharge && !hasAmmo && hasMags && ((!weapon.State.Sync.Reloading && wasReloading) || (weapon.State.Sync.Reloading && !wasReloading));

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
                if (!weapon.ActiveAmmoDef.Const.MustCharge)
                    weapon.CancelableReloadAction -= weapon.Reloaded;

                weapon.EventTriggerStateChanged(EventTriggers.Reloading, false);
            }

            else if (weapon.ActiveAmmoDef.Const.MustCharge && weapon.State.Sync.Reloading && !weapon.Comp.Session.ChargingWeaponsCheck.Contains(weapon))
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

        internal void MouseNetworkEvent(MyEntity entity)
        {
            if (IsClient)
            {
                PacketsToServer.Add(new MouseInputPacket
                {
                    EntityId = entity.EntityId,
                    SenderId = MultiplayerId,
                    PType = PacketType.ClientMouseEvent,
                    Data = UiInput.ClientMouseState
                });
            }
            else if (MpActive && IsServer)
            {
                PacketsToClient.Add(new PacketInfo
                {
                    Entity = entity,
                    Packet = new MouseInputPacket
                    {
                        EntityId = entity.EntityId,
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
                PacketsToServer.Add(new BoolUpdatePacket
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
                    Packet = new BoolUpdatePacket
                    {
                        EntityId = controlBlock.EntityId,
                        SenderId = 0,
                        PType = PacketType.ActiveControlUpdate,
                        Data = active
                    }
                });
            }
        }
        internal void ProccessServerPacketsForClients()
        {
            if (!IsServer)
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
                    foreach (var p in Players.Values)
                    {

                        if (p.SteamUserId != packetInfo.Packet.SenderId && (packetInfo.Entity == null || Vector3D.DistanceSquared(p.GetPosition(), packetInfo.Entity.PositionComp.WorldAABB.Center) <= SyncBufferedDistSqr))
                            MyModAPIHelper.MyMultiplayer.Static.SendMessageTo(ClientPacketId, bytes, p.SteamUserId, true);
                    }
                }
            }
            PacketsToClient.Clear();
        }

        internal void ProccessClientPacketsForServer()
        {
            if (!IsClient)
            {
                Log.Line($"trying to process client packets on a non-client");
                return;
            }

            for (int i = 0; i < PacketsToServer.Count; i++)
                MyModAPIHelper.MyMultiplayer.Static.SendMessageToServer(ServerPacketId, MyAPIGateway.Utilities.SerializeToBinary(PacketsToServer[i]), true);

            PacketsToServer.Clear();
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

            foreach (var GridComps in gridCompsToCheck)
            { 

                var packet = new RequestTargetsPacket
                {
                    EntityId = GridComps.Key.MyGrid.EntityId,
                    SenderId = MultiplayerId,
                    PType = PacketType.TargetUpdateRequest,
                };

                packet.Comps = new long[GridComps.Value.Count];
                GridComps.Value.CopyTo(packet.Comps);

                PacketsToServer.Add(packet);
            }

            gridCompsToCheck.Clear();
        }

        internal struct PacketInfo
        {
            internal MyEntity Entity;
            internal Packet Packet;
            internal bool SingleClient;
        }

        internal class ErrorPacket
        {
            internal uint RecievedTick;
            internal uint RetryTick;
            internal uint RetryDelayTicks;
            internal int RetryAttempt;
            internal int MaxAttempts;
            internal string Error;
            internal PacketType PType;
            internal Packet Packet;

            public virtual bool Equals(ErrorPacket other)
            {
                if (Packet == null) return false;

                return Packet.Equals(other.Packet);
            }

            public override bool Equals(object obj)
            {
                if (Packet == null) return false;
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != GetType()) return false;
                return Equals((ErrorPacket)obj);
            }

            public override int GetHashCode()
            {
                if (Packet == null) return 0;

                return Packet.GetHashCode();
            }
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
                    Timmings = null,
                    SyncData = null
                };
                
                if (_session.Tick - w.LastSyncTick > 20)
                {
                    weaponSync.Timmings = w.Timings.SyncOffsetServer(_session.Tick);
                    weaponSync.SyncData = w.State.Sync;
                    w.LastSyncTick = _session.Tick;
                }

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
