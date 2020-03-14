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
using static WeaponCore.Support.GridAi;
using static WeaponCore.Support.WeaponDefinition.AnimationDef.PartAnimationSetDef;

namespace WeaponCore
{
    public partial class Session
    {
        #region Network sync
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
                var noReproccess = false;

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

                                if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) continue;

                                var weapon = comp.Platform.Weapons[weaponData.TargetData.WeaponId];
                                
                                var syncTarget = weaponData.TargetData;

                                if (weaponData.Timmings != null && weaponData.SyncData != null)
                                {
                                    var timings = weaponData.Timmings.SyncOffsetClient(Tick);
                                    SyncWeapon(weapon, timings, ref weaponData.SyncData);
                                }

                                syncTarget.SyncTarget(weapon.Target);

                                if (weapon.Target.HasTarget)
                                {
                                    if (!weapon.Target.IsProjectile && !weapon.Target.IsFakeTarget && weapon.Target.Entity == null)
                                    {
                                        var oldChange = weapon.Target.TargetChanged;
                                        weapon.Target.StateChange(true, Target.States.Invalid);
                                        weapon.Target.TargetChanged = !weapon.FirstSync && oldChange;
                                        weapon.FirstSync = false;
                                    }
                                    else if (weapon.Target.IsProjectile)
                                    {
                                        TargetType targetType;
                                        AcquireProjectile(weapon, out targetType);

                                        if (targetType == TargetType.None)
                                        {
                                            if (weapon.NewTarget.CurrentState != Target.States.NoTargetsSeen)
                                                weapon.NewTarget.Reset(weapon.Comp.Session.Tick, Target.States.NoTargetsSeen);                                            
                                            if (weapon.Target.CurrentState != Target.States.NoTargetsSeen) weapon.Target.Reset(weapon.Comp.Session.Tick, Target.States.NoTargetsSeen, !weapon.Comp.TrackReticle);
                                        }
                                    }
                                }
                            }

                            report.PacketValid = true;
                            break;
                        }
                    case PacketType.FocusUpdate:
                        {
                            var targetPacket = packet as FocusPacket;
                            if (targetPacket == null)
                            {
                                errorPacket.Error = $"Packet was null";
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
                            noReproccess = true;
                            var targetPacket = packet as FakeTargetPacket;
                            if (targetPacket?.Data == null)
                            {
                                errorPacket.Error = $"Data was null:";
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
                                errorPacket.Error = $"updatePacket was null";
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
                                    errorPacket.Error = $"Data was null";
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

                        comp.OtherPlayerTrackingReticle = reticlePacket.Data;

                        report.PacketValid = true;
                        break;

                    case PacketType.OverRidesUpdate:
                        {
                            var overRidesPacket = packet as OverRidesPacket;

                            if (overRidesPacket == null)
                            {
                                errorPacket.Error = $"overRidesPacket was null";
                                break;
                            }
                            var myGrid = ent as MyCubeGrid;

                            if (comp != null) {
                                comp.Set.Value.Overrides.Sync(overRidesPacket.Data);
                                comp.Set.Value.MId = overRidesPacket.MId;
                                report.PacketValid = true;
                            }
                            else if (myGrid != null)
                            {
                                GridAi ai;
                                if(GridTargetingAIs.TryGetValue(myGrid, out ai))
                                {
                                    var o = overRidesPacket.Data;
                                    ai.UiMId = overRidesPacket.MId;

                                    ai.ReScanBlockGroups();

                                    SyncGridOverrides(ai, overRidesPacket.GroupName, o);

                                    foreach (var component in ai.BlockGroups[overRidesPacket.GroupName].Comps)
                                        component.Set.Value.Overrides.Sync(o);

                                    report.PacketValid = true;
                                }
                            }

                            break;
                        }
                    case PacketType.PlayerControlUpdate:
                        ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
                        comp = ent?.Components.Get<WeaponComponent>();
                        var cPlayerPacket = (ControllingPlayerPacket) packet;

                        if (comp == null)
                        {
                            errorPacket.Error = $"[cPlayerPacket] Comp was null";
                            break;
                        }

                        comp.State.Value.CurrentPlayerControl.Sync(cPlayerPacket.Data);
                        comp.Set.Value.MId = cPlayerPacket.MId;
                        report.PacketValid = true;

                        break;

                    case PacketType.TargetExpireUpdate:
                        noReproccess = true;
                        ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
                        comp = ent?.Components.Get<WeaponComponent>();

                        var idPacket = (WeaponIdPacket) packet;

                        if (comp == null)
                        {
                            errorPacket.Error = $"[idPacket] Comp was null";
                            break;
                        }
                        //saving on extra field with new packet type
                        comp.Platform.Weapons[idPacket.WeaponId].Target.Reset(Tick, Target.States.ServerReset);

                        report.PacketValid = true;
                        break;

                    case PacketType.FullMouseUpdate:
                        {
                            var mouseUpdatePacket = (MouseInputSyncPacket) packet;

                            if (mouseUpdatePacket.Data == null) break;

                            for(int i = 0; i < mouseUpdatePacket.Data.Length; i++)
                            {
                                var playerMousePackets = mouseUpdatePacket.Data[i];
                                if(playerMousePackets.PlayerId != PlayerId)
                                    PlayerMouseStates[playerMousePackets.PlayerId] = playerMousePackets.MouseStateData;
                            }

                            report.PacketValid = true;
                            break;
                        }

                    case PacketType.CompToolbarShootState:
                        {
                            var shootStatePacket = (ShootStatePacket) packet;
                            ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
                            comp = ent?.Components.Get<WeaponComponent>();

                            if (comp == null) break;

                            comp.State.Value.MId = shootStatePacket.MId;

                            for (int i = 0; i < comp.Platform.Weapons.Length; i++)
                            {
                                var w = comp.Platform.Weapons[i];

                                if (shootStatePacket.Data == TerminalActionState.ShootOnce)
                                    w.State.SingleShotCounter++;

                                w.State.ManualShoot = shootStatePacket.Data;
                            }

                            if(shootStatePacket.Data != TerminalActionState.ShootOff && shootStatePacket.Data != TerminalActionState.ShootOnce)
                            {
                                comp.Set.Value.Overrides.ManualControl = false;
                                comp.Set.Value.Overrides.TargetPainter = false;

                                if (shootStatePacket.Data != TerminalActionState.ShootClick)
                                {
                                    comp.State.Value.CurrentPlayerControl.PlayerId = -1;
                                    comp.State.Value.CurrentPlayerControl.ControlType = ControlType.None;
                                }
                            }

                            report.PacketValid = true;
                            break;
                        }

                    case PacketType.WeaponToolbarShootState:
                        {
                            var shootStatePacket = (WeaponShootStatePacket) packet;
                            ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
                            comp = ent?.Components.Get<WeaponComponent>();

                            if (comp == null) break;
                            
                            comp.State.Value.MId = shootStatePacket.MId;
                            var w = comp.Platform.Weapons[shootStatePacket.WeaponId];

                            if (shootStatePacket.Data == TerminalActionState.ShootOnce)
                                w.State.SingleShotCounter++;
                            else if (shootStatePacket.Data != TerminalActionState.ShootOff)
                            {
                                comp.Set.Value.Overrides.ManualControl = false;
                                comp.Set.Value.Overrides.TargetPainter = false;

                                if (shootStatePacket.Data != TerminalActionState.ShootClick)
                                {
                                    comp.State.Value.CurrentPlayerControl.PlayerId = -1;
                                    comp.State.Value.CurrentPlayerControl.ControlType = ControlType.None;
                                }
                            }

                            w.State.ManualShoot = shootStatePacket.Data;

                            report.PacketValid = true;
                            break;
                        }

                    case PacketType.RangeUpdate:
                        {
                            var rangePacket = (RangePacket) packet;
                            ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
                            comp = ent?.Components.Get<WeaponComponent>();

                            if (comp == null) break;

                            comp.Set.Value.MId = rangePacket.MId;
                            comp.Set.Value.Range = rangePacket.Data;

                            report.PacketValid = true;
                            break;
                        }

                    case PacketType.GridAiUiMidUpdate:
                        {
                            var myGrid = MyEntities.GetEntityByIdOrDefault(packet.EntityId) as MyCubeGrid;
                            var midPacket = (MIdPacket) packet;

                            if (myGrid == null) break;

                            GridAi ai;
                            if (GridTargetingAIs.TryGetValue(myGrid, out ai))
                            {
                                ai.UiMId = midPacket.Id;

                                report.PacketValid = true;
                            }
                            break;
                        }

                    default:
                        if(!retry) Reporter.ReportData[PacketType.Invalid].Add(report);
                        invalidType = true;
                        report.PacketValid = false;

                        break;
                }

                if (!report.PacketValid && !invalidType && !retry && !noReproccess)
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
            PacketType ptype = PacketType.Invalid;
            try
            {
                var report = Reporter.ReportPool.Get();
                report.Receiver = NetworkReporter.Report.Received.Server;
                report.PacketSize = rawData.Length;

                var packet = MyAPIGateway.Utilities.SerializeFromBinary<Packet>(rawData);
                if (packet == null) return;

                Reporter.ReportData[packet.PType].Add(report);
                ptype = packet.PType;

                var errorPacket = new ErrorPacket { RecievedTick = Tick, Packet = packet, PType = ptype };

                MyEntity ent; // not inited here to avoid extras calls unless needed
                WeaponComponent comp; // not inited here to avoid extras calls unless needed
                long playerId = 0;

                switch (packet.PType)
                {
                    case PacketType.CompStateUpdate:
                        var statePacket = packet as StatePacket;
                        ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId, null, true);
                        comp = ent?.Components.Get<WeaponComponent>();

                        if (statePacket?.Data == null || comp == null || ent.MarkedForClose)
                        {
                            errorPacket.Error = $"statePacket?.Data is null: {statePacket?.Data == null} comp is null: {comp == null} ent.MarkedForClose: {ent?.MarkedForClose} ent is null: {ent == null}";
                            break;
                        }

                        if (statePacket.Data.MId > comp.State.Value.MId)
                        {
                            comp.State.Value.Sync(statePacket.Data);
                            PacketsToClient.Add(new PacketInfo { Entity = ent, Packet = packet });

                            report.PacketValid = true;
                        }
                        else
                            errorPacket.Error = "Mid is old, likely multiple clients attempting update";

                        break;

                    case PacketType.CompSettingsUpdate:

                        var setPacket = packet as SettingPacket;
                        ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId, null, true);
                        comp = ent?.Components.Get<WeaponComponent>();

                        if (setPacket?.Data == null || comp == null || ent.MarkedForClose)
                        {
                            errorPacket.Error = $"setPacket?.Data is null: {setPacket?.Data == null} comp is null: {comp == null} ent.MarkedForClose: {ent?.MarkedForClose} ent is null: {ent == null}";
                            break;
                        }

                        if (setPacket.Data.MId > comp.Set.Value.MId)
                        {
                            comp.Set.Value.Sync(comp, setPacket.Data);
                            PacketsToClient.Add(new PacketInfo { Entity = ent, Packet = setPacket });

                            report.PacketValid = true;
                        }
                        else
                            errorPacket.Error = "Mid is old, likely multiple clients attempting update";

                        break;

                    case PacketType.ClientMouseEvent:

                        var mousePacket = packet as MouseInputPacket;
                        ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId, null, true);

                        if (mousePacket?.Data == null || ent == null || ent.MarkedForClose)
                        {
                            errorPacket.Error = $"mousePacket?.Data is null: {mousePacket?.Data == null} ent.MarkedForClose: {ent?.MarkedForClose} ent is null: {ent}";
                            break;
                        }

                        if (SteamToPlayer.TryGetValue(packet.SenderId, out playerId))
                        {
                            PlayerMouseStates[playerId] = mousePacket.Data;
                            PacketsToClient.Add(new PacketInfo { Entity = ent, Packet = mousePacket });

                            report.PacketValid = true;
                        }
                        else
                            errorPacket.Error = "Player Not Found";

                        break;

                    case PacketType.ActiveControlUpdate:
                        {
                            var dPacket = packet as BoolUpdatePacket;
                            var block = MyEntities.GetEntityByIdOrDefault(packet.EntityId, null, true) as MyCubeBlock;

                            if (dPacket?.Data == null || block == null || block.MarkedForClose)
                            {
                                errorPacket.Error = $"dPacket?.Data is null: {dPacket?.Data == null} block is null: {block == null} ent.MarkedForClose: {block?.MarkedForClose}";
                                break;
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
                            var myGrid = MyEntities.GetEntityByIdOrDefault(packet.EntityId, null, true) as MyCubeGrid;

                            if (targetPacket?.Data == null || myGrid == null || myGrid.MarkedForClose)
                            {
                                errorPacket.Error = $"targetPacket?.Data is null: {targetPacket?.Data == null} ent.MarkedForClose: {myGrid.MarkedForClose} myGrid is null: {myGrid == null}";
                                break;
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
                            else
                                errorPacket.Error = "GridAi not found";

                            break;
                        }
                    case PacketType.FakeTargetUpdate:
                        {
                            var targetPacket = packet as FakeTargetPacket;
                            var myGrid = MyEntities.GetEntityByIdOrDefault(packet.EntityId, null, true) as MyCubeGrid;

                            if (targetPacket?.Data == null || myGrid == null || myGrid.MarkedForClose)
                            {
                                errorPacket.Error = $"targetPacket?.Data is null: {targetPacket?.Data == null} ent.MarkedForClose: {myGrid.MarkedForClose} myGrid is null: {myGrid == null }";
                                break;
                            }

                            GridAi ai;
                            if (GridTargetingAIs.TryGetValue(myGrid, out ai))
                            {
                                ai.DummyTarget.Update(targetPacket.Data, ai, null, true);
                                PacketsToClient.Add(new PacketInfo { Entity = myGrid, Packet = targetPacket });
                                report.PacketValid = true;
                            }
                            else
                                errorPacket.Error = "GridAi not found";

                            break;
                        }
                    case PacketType.GridSyncRequestUpdate://can be a large update, only call on stream sync
                        {
                            var myGrid = MyEntities.GetEntityByIdOrDefault(packet.EntityId, null, true) as MyCubeGrid;

                            if (myGrid == null || myGrid.MarkedForClose)
                            {
                                errorPacket.Error = $"ent.MarkedForClose: {myGrid?.MarkedForClose} myGrid is null: {myGrid == null }";
                                break;
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

                                for (int i = 0; i < _gridSyncCompTmpList.Count; i++)
                                {
                                    comp = _gridSyncCompTmpList[i];
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
                                _gridSyncCompTmpList.Clear();

                                if (gridPacket.Data.Count > 0)
                                    PacketsToClient.Add(new PacketInfo
                                    {
                                        Entity = myGrid,
                                        Packet = gridPacket,
                                        SingleClient = true,
                                    });

                                PacketsToClient.Add(new PacketInfo
                                {
                                    Entity = myGrid,
                                    Packet = new MIdPacket
                                    {
                                        EntityId = myGrid.EntityId,
                                        SenderId = packet.SenderId,
                                        PType = PacketType.GridAiUiMidUpdate,
                                        Id = ai.UiMId,
                                    },
                                    SingleClient = true,
                                });

                                if (!PlayerEntityIdInRange.ContainsKey(packet.SenderId))
                                    PlayerEntityIdInRange[packet.SenderId] = new HashSet<long>();

                                PlayerEntityIdInRange[packet.SenderId].Add(packet.EntityId);

                                report.PacketValid = true;
                            }
                            else
                                errorPacket.Error = "GridAi not found";

                            break;
                        }
                    case PacketType.ReticleUpdate:
                        {
                            var reticlePacket = packet as BoolUpdatePacket;
                            ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId, null, true);
                            comp = ent?.Components.Get<WeaponComponent>();

                            if (reticlePacket == null || comp == null || ent.MarkedForClose)
                            {
                                errorPacket.Error = $"reticlePacket is null: {reticlePacket == null} comp is null: {comp == null} ent.MarkedForClose: {ent?.MarkedForClose} ent is null: {ent == null }";
                                break;
                            }

                            comp.TrackReticle = reticlePacket.Data;

                            PacketsToClient.Add(new PacketInfo { Entity = ent, Packet = reticlePacket });

                            report.PacketValid = true;
                            break;
                        }
                    case PacketType.OverRidesUpdate:
                        {
                            var overRidesPacket = (OverRidesPacket) packet;
                            ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId, null, true);
                            comp = ent?.Components.Get<WeaponComponent>();

                            var myGrid = ent as MyCubeGrid;

                            if (ent == null || ent.MarkedForClose || comp == null && myGrid == null)
                            {
                                errorPacket.Error = $"comp is null: {comp == null} ent.MarkedForClose: {ent?.MarkedForClose} ent is null: {ent == null }";
                                break;
                            }

                            if (comp != null && comp.Set.Value.MId < overRidesPacket.MId)
                            {
                                comp.Set.Value.Overrides.Sync(overRidesPacket.Data);
                                comp.Set.Value.MId = overRidesPacket.MId;
                                report.PacketValid = true;
                            }
                            else if (myGrid != null)
                            {
                                GridAi ai;
                                if (GridTargetingAIs.TryGetValue(myGrid, out ai) && ai.UiMId < overRidesPacket.MId)
                                {
                                    var o = overRidesPacket.Data;
                                    ai.UiMId = overRidesPacket.MId;

                                    ai.ReScanBlockGroups();

                                    SyncGridOverrides(ai, overRidesPacket.GroupName, o);

                                    GroupInfo groups;
                                    if (ai.BlockGroups.TryGetValue(overRidesPacket.GroupName, out groups))
                                    {
                                        foreach (var component in groups.Comps)
                                            component.Set.Value.Overrides.Sync(o);

                                        report.PacketValid = true;
                                    }
                                    else
                                        errorPacket.Error = "Block group not found";
                                }
                                else
                                    errorPacket.Error = "GridAi not found";
                            }

                            if (report.PacketValid)
                            {
                                PacketsToClient.Add(new PacketInfo
                                {
                                    Entity = ent,
                                    Packet = overRidesPacket,
                                });
                            }

                            break;
                        }
                    case PacketType.PlayerControlUpdate:
                        ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId, null, true);
                        comp = ent?.Components.Get<WeaponComponent>();
                        var cPlayerPacket = (ControllingPlayerPacket) packet;

                        if (comp == null  || ent.MarkedForClose)
                        {
                            errorPacket.Error = $"[overRidesPacket] comp is null: {comp == null} ent.MarkedForClose: {ent?.MarkedForClose}";
                            break;
                        }

                        if (comp.Set.Value.MId < cPlayerPacket.MId)
                        {
                            comp.State.Value.CurrentPlayerControl.Sync(cPlayerPacket.Data);
                            comp.Set.Value.MId = cPlayerPacket.MId;
                            report.PacketValid = true;
                            PacketsToClient.Add(new PacketInfo { Entity = comp.MyCube, Packet = cPlayerPacket });
                        }
                        else
                            errorPacket.Error = "Mid is old, likely multiple clients attempting update";
                        
                        break;

                    case PacketType.TargetUpdateRequest:
                        {
                            var targetRequestPacket = (RequestTargetsPacket) packet;
                            var myGrid = MyEntities.GetEntityByIdOrDefault(packet.EntityId, null, true) as MyCubeGrid;

                            if (myGrid == null || myGrid.MarkedForClose)
                            {
                                errorPacket.Error = $"[overRidesPacket] ent.MarkedForClose: {myGrid?.MarkedForClose} myGrid is null: {myGrid == null }";
                                break;
                            }

                            GridAi ai;
                            if (GridTargetingAIs.TryGetValue(myGrid, out ai))
                            {
                                var gridPacket = new GridWeaponPacket
                                {
                                    EntityId = packet.EntityId,
                                    SenderId = packet.SenderId,
                                    PType = PacketType.TargetUpdate,
                                    Data = new List<WeaponData>()
                                };

                                for (int i = 0; i < targetRequestPacket.Comps.Length; i++)
                                {
                                    var compId = targetRequestPacket.Comps[i];
                                    var compCube = MyEntities.GetEntityByIdOrDefault(compId, null, true) as MyCubeBlock;

                                    if (compCube == null || !ai.WeaponBase.TryGetValue(compCube, out comp))
                                        continue;

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
                                    PacketsToClient.Add(new PacketInfo
                                    {
                                        Entity = myGrid,
                                        Packet = gridPacket,
                                        SingleClient = true,
                                    });

                                report.PacketValid = true;
                            }
                            else
                                errorPacket.Error = "GridAi not found";

                            break;
                        }

                    case PacketType.ClientEntityClosed:
                        {
                            if (PlayerEntityIdInRange.ContainsKey(packet.SenderId))
                                PlayerEntityIdInRange[packet.SenderId].Remove(packet.EntityId);
                        }
                        break;

                    case PacketType.RequestMouseStates:
                        {
                            var mouseUpdatePacket = new MouseInputSyncPacket
                            {
                                EntityId = -1,
                                SenderId = packet.SenderId,
                                PType = PacketType.FullMouseUpdate,
                                Data = new PlayerMouseData[PlayerMouseStates.Count],
                            };

                            var c = 0;
                            foreach (var playerMouse in PlayerMouseStates)
                            {
                                mouseUpdatePacket.Data[c] = new PlayerMouseData
                                {
                                    PlayerId = playerMouse.Key,
                                    MouseStateData = playerMouse.Value
                                };
                            }

                            if (PlayerMouseStates.Count > 0)
                                PacketsToClient.Add(new PacketInfo
                                {
                                    Entity = null,
                                    Packet = mouseUpdatePacket,
                                    SingleClient = true,
                                });

                            report.PacketValid = true;
                        }
                        break;

                    case PacketType.CompToolbarShootState:
                        {
                            var shootStatePacket = (ShootStatePacket) packet;
                            ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId, null, true);
                            comp = ent?.Components.Get<WeaponComponent>();

                            if (shootStatePacket?.Data == null || ent == null || comp == null || ent.MarkedForClose)
                            {
                                errorPacket.Error = $"[shootStatePacket] ent.MarkedForClose: {ent?.MarkedForClose} ent is null:";
                                break;
                            }

                            if (comp.State.Value.MId < shootStatePacket.MId)
                            {
                                comp.State.Value.MId = shootStatePacket.MId;
                                for (int i = 0; i < comp.Platform.Weapons.Length; i++)
                                {
                                    var w = comp.Platform.Weapons[i];

                                    if (shootStatePacket.Data == TerminalActionState.ShootOnce)
                                        w.State.SingleShotCounter++;

                                    w.State.ManualShoot = shootStatePacket.Data;
                                }

                                if (shootStatePacket.Data != TerminalActionState.ShootOff && shootStatePacket.Data != TerminalActionState.ShootOnce)
                                {
                                    comp.Set.Value.Overrides.ManualControl = false;
                                    comp.Set.Value.Overrides.TargetPainter = false;

                                    if (shootStatePacket.Data != TerminalActionState.ShootClick)
                                    {
                                        comp.State.Value.CurrentPlayerControl.PlayerId = -1;
                                        comp.State.Value.CurrentPlayerControl.ControlType = ControlType.None;
                                    }
                                }

                                PacketsToClient.Add(new PacketInfo
                                {
                                    Entity = ent,
                                    Packet = shootStatePacket,
                                });

                                report.PacketValid = true;
                            }
                            else
                                errorPacket.Error = "Mid is old, likely multiple clients attempting update";

                            
                            break;
                        }

                    case PacketType.WeaponToolbarShootState:
                        {
                            var shootStatePacket = (WeaponShootStatePacket) packet;
                            ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId, null, true);
                            comp = ent?.Components.Get<WeaponComponent>();

                            if (comp == null || ent.MarkedForClose)
                            {
                                errorPacket.Error = $"[shootStatePacket] ent.MarkedForClose: {ent?.MarkedForClose} ent is null: {ent == null }";
                                break;
                            }

                            if (comp.State.Value.MId < shootStatePacket.MId)
                            {
                                comp.State.Value.MId = shootStatePacket.MId;
                                var w = comp.Platform.Weapons[shootStatePacket.WeaponId];

                                if (shootStatePacket.Data == TerminalActionState.ShootOnce)
                                    w.State.SingleShotCounter++;
                                else if (shootStatePacket.Data != TerminalActionState.ShootOff)
                                {
                                    comp.Set.Value.Overrides.ManualControl = false;
                                    comp.Set.Value.Overrides.TargetPainter = false;

                                    if (shootStatePacket.Data != TerminalActionState.ShootClick)
                                    {
                                        comp.State.Value.CurrentPlayerControl.PlayerId = -1;
                                        comp.State.Value.CurrentPlayerControl.ControlType = ControlType.None;
                                    }
                                }

                                w.State.ManualShoot = shootStatePacket.Data;

                                PacketsToClient.Add(new PacketInfo
                                {
                                    Entity = ent,
                                    Packet = shootStatePacket,
                                });

                                report.PacketValid = true;
                            }
                            else
                                errorPacket.Error = "Mid is old, likely multiple clients attempting update";

                            
                            break;
                        }

                    case PacketType.RangeUpdate:
                        {
                            var rangePacket = (RangePacket) packet;
                            ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId, null, true);
                            comp = ent?.Components.Get<WeaponComponent>();

                            if (comp == null || ent.MarkedForClose)
                            {
                                errorPacket.Error = $"[shootStatePacket]  ent.MarkedForClose: {ent?.MarkedForClose} ent is null: {ent == null }";
                                break;
                            }

                            if (comp.Set.Value.MId < rangePacket.MId)
                            {
                                comp.Set.Value.MId = rangePacket.MId;
                                comp.Set.Value.Range = rangePacket.Data;

                                PacketsToClient.Add(new PacketInfo
                                {
                                    Entity = ent,
                                    Packet = rangePacket,
                                });

                                report.PacketValid = true;
                            }
                            else
                                errorPacket.Error = "Mid is old, likely multiple clients attempting update";
                            
                            break;
                        }

                    default:
                        Reporter.ReportData[PacketType.Invalid].Add(report);
                        report.PacketValid = false;

                        break;
                }
            }
            catch (Exception ex) { Log.Line($"Exception in ReceivedPacket: PacketType:{ptype} Exception: {ex}"); }
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

            var hasMags = weapon.State.Sync.CurrentMags > 0 || IsCreative;
            var hasAmmo = weapon.State.Sync.CurrentAmmo > 0;

            var chargeFullReload = weapon.ActiveAmmoDef.AmmoDef.Const.MustCharge && !wasReloading && !weapon.State.Sync.Reloading && !hasAmmo && (hasMags || weapon.ActiveAmmoDef.AmmoDef.Const.EnergyAmmo);
            var regularFullReload = !weapon.ActiveAmmoDef.AmmoDef.Const.MustCharge && !wasReloading && !weapon.State.Sync.Reloading && !hasAmmo && hasMags;

            var chargeFinishReloading = weapon.ActiveAmmoDef.AmmoDef.Const.MustCharge && !weapon.State.Sync.Reloading && wasReloading;
            var regularFinishedReloading = !weapon.ActiveAmmoDef.AmmoDef.Const.MustCharge && !hasAmmo && hasMags && ((!weapon.State.Sync.Reloading && wasReloading) || (weapon.State.Sync.Reloading && !wasReloading));

            if (chargeFullReload || regularFullReload)
                weapon.StartReload();

            else if (chargeFinishReloading || regularFinishedReloading)
            {
                weapon.CancelableReloadAction += weapon.Reloaded;
                if (weapon.Timings.ReloadedTick > 0)
                    comp.Session.FutureEvents.Schedule(weapon.CancelableReloadAction, null, weapon.Timings.ReloadedTick);
                else
                    weapon.Reloaded();
            }
            else if (wasReloading && !weapon.State.Sync.Reloading && hasAmmo)
            {
                if (!weapon.ActiveAmmoDef.AmmoDef.Const.MustCharge)
                    weapon.CancelableReloadAction -= weapon.Reloaded;

                weapon.EventTriggerStateChanged(EventTriggers.Reloading, false);
            }

            else if (weapon.ActiveAmmoDef.AmmoDef.Const.MustCharge && weapon.State.Sync.Reloading && !weapon.Comp.Session.ChargingWeaponsCheck.Contains(weapon))
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
                if (GridTargetingAIs.TryGetValue(grid, out trackingAi))
                    trackingAi.ControllingPlayers[playerId] = block;
            }
            else //remove
            {
                if (GridTargetingAIs.TryGetValue(grid, out trackingAi))
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

            foreach (var gridComps in gridCompsToCheck)
            {

                var packet = new RequestTargetsPacket
                {
                    EntityId = gridComps.Key.MyGrid.EntityId,
                    SenderId = MultiplayerId,
                    PType = PacketType.TargetUpdateRequest,
                    Comps = new long[gridComps.Value.Count],
                };

                gridComps.Value.CopyTo(packet.Comps);

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

        internal static void SyncGridOverrides(GridAi ai, string groupName, GroupOverrides o)
        {
            ai.BlockGroups[groupName].Settings["Active"] = o.Activate ? 1 : 0;
            ai.BlockGroups[groupName].Settings["Neutrals"] = o.Neutrals ? 1 : 0;
            ai.BlockGroups[groupName].Settings["Projectiles"] = o.Projectiles ? 1 : 0;
            ai.BlockGroups[groupName].Settings["Biologicals"] = o.Biologicals ? 1 : 0;
            ai.BlockGroups[groupName].Settings["Meteors"] = o.Meteors ? 1 : 0;
            ai.BlockGroups[groupName].Settings["Friendly"] = o.Friendly ? 1 : 0;
            ai.BlockGroups[groupName].Settings["Unowned"] = o.Unowned ? 1 : 0;
            ai.BlockGroups[groupName].Settings["TargetPainter"] = o.TargetPainter ? 1 : 0;
            ai.BlockGroups[groupName].Settings["ManualControl"] = o.ManualControl ? 1 : 0;
            ai.BlockGroups[groupName].Settings["FocusTargets"] = o.FocusTargets ? 1 : 0;
            ai.BlockGroups[groupName].Settings["FocusSubSystem"] = o.FocusSubSystem ? 1 : 0;
            ai.BlockGroups[groupName].Settings["SubSystems"] = (int)o.SubSystem;
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
