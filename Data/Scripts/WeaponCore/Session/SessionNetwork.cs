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

namespace WeaponCore
{
    public partial class Session
    {
        #region Client Sync
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
                        {
                            var statePacket = packet as StatePacket;
                            if (statePacket?.Data == null || comp == null)
                            {
                                errorPacket.Error = $"Data was null: {statePacket?.Data == null} Comp was null: {comp == null}";
                                break;
                            }

                            comp.SyncIds.MIds[(int)packet.PType] = statePacket.MId;
                            comp.State.Value.Sync(statePacket.Data);

                            report.PacketValid = true;
                            break;
                        }
                    case PacketType.CompSettingsUpdate:
                        {
                            var setPacket = packet as SettingPacket;
                            if (setPacket?.Data == null || comp == null)
                            {
                                errorPacket.Error = $"Data was null: {setPacket?.Data == null} Comp was null: {comp == null}";
                                break;
                            }

                            comp.SyncIds.MIds[(int)packet.PType] = setPacket.MId;
                            comp.Set.Value.Sync(comp, setPacket.Data);

                            report.PacketValid = true;
                            break;
                        }
                    case PacketType.WeaponSyncUpdate:
                    {
                            var targetPacket = packet as GridWeaponPacket;
                            if (targetPacket?.Data == null || ent == null) {
                                errorPacket.Error = $"Data was null: {targetPacket?.Data == null} Grid was null: {ent == null}";

                                break;
                            }

                            for (int i = 0; i < targetPacket.Data.Count; i++)
                            {
                                var weaponData = targetPacket.Data[i];
                                var block = MyEntities.GetEntityByIdOrDefault(weaponData.CompEntityId) as MyCubeBlock;
                                comp = block?.Components.Get<WeaponComponent>();

                                if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready)
                                    continue;

                                Weapon weapon;

                                if (weaponData.Timmings != null && weaponData.SyncData != null)
                                {
                                    weapon = comp.Platform.Weapons[weaponData.SyncData.WeaponId];
                                    var timings = weaponData.Timmings.SyncOffsetClient(Tick);
                                    SyncWeapon(weapon, timings, ref weaponData.SyncData);
                                }

                                if (weaponData.TargetData != null)
                                {
                                    weapon = comp.Platform.Weapons[weaponData.TargetData.WeaponId];
                                    weaponData.TargetData.SyncTarget(weapon.Target);

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
                            }
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
                        {
                            var mousePacket = packet as InputPacket;
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
                        }
                    case PacketType.ActiveControlUpdate:
                        {
                            var dPacket = packet as BoolUpdatePacket;

                            var block = MyEntities.GetEntityByIdOrDefault(packet.EntityId) as MyCubeBlock;

                            if (block == null || dPacket?.Data == null)
                            {
                                errorPacket.Error = $"Data was null {dPacket?.Data == null} block was null {block == null}";
                                break;
                            }
                            long playerId;
                            SteamToPlayer.TryGetValue(packet.SenderId, out playerId);

                            UpdateActiveControlDictionary(block, playerId, dPacket.Data);

                            report.PacketValid = true;
                            break;
                        }
                    case PacketType.ActiveControlFullUpdate:
                        {
                            try
                            {
                                var csPacket = packet as CurrentGridPlayersPacket;
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
                        {
                            var reticlePacket = packet as BoolUpdatePacket;

                            if (reticlePacket == null || comp == null)
                            {
                                errorPacket.Error = $"reticlePacket was null {reticlePacket == null} Comp was null: {comp == null}";
                                break;
                            }

                            comp.State.Value.OtherPlayerTrackingReticle = reticlePacket.Data;

                            report.PacketValid = true;
                            break;
                        }
                    case PacketType.OverRidesUpdate:
                        {
                            var overRidesPacket = (OverRidesPacket)packet;
                            ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId, null, true);
                            comp = ent?.Components.Get<WeaponComponent>();

                            var myGrid = ent as MyCubeGrid;

                            if (ent == null || ent.MarkedForClose || comp == null && myGrid == null)
                            {
                                errorPacket.Error = $"comp is null: {comp == null} ent.MarkedForClose: {ent?.MarkedForClose} ent is null: {ent == null }";
                                break;
                            }

                            if (comp != null && comp.SyncIds.MIds[(int)packet.PType] < overRidesPacket.MId)
                            {
                                comp.Set.Value.Overrides.Sync(overRidesPacket.Data);
                                comp.SyncIds.MIds[(int)packet.PType] = overRidesPacket.MId;

                                GroupInfo group;
                                if (!string.IsNullOrEmpty(comp.State.Value.CurrentBlockGroup) && comp.Ai.BlockGroups.TryGetValue(comp.State.Value.CurrentBlockGroup, out group))
                                {
                                    comp.Ai.ScanBlockGroupSettings = true;
                                    comp.Ai.GroupsToCheck.Add(group);
                                }
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
                                        {
                                            component.State.Value.CurrentBlockGroup = overRidesPacket.GroupName;
                                            component.Set.Value.Overrides.Sync(o);
                                        }

                                        report.PacketValid = true;
                                    }
                                    else
                                        errorPacket.Error = "Block group not found";
                                }
                                else
                                    errorPacket.Error = "GridAi not found";
                            }
                            break;
                        }
                    case PacketType.PlayerControlUpdate:
                        {
                            ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
                            comp = ent?.Components.Get<WeaponComponent>();
                            var cPlayerPacket = (ControllingPlayerPacket)packet;

                            if (comp == null)
                            {
                                errorPacket.Error = $"[cPlayerPacket] Comp was null";
                                break;
                            }

                            comp.State.Value.CurrentPlayerControl.Sync(cPlayerPacket.Data);
                            comp.SyncIds.MIds[(int)packet.PType] = cPlayerPacket.MId;
                            report.PacketValid = true;

                            break;
                        }
                    case PacketType.TargetExpireUpdate:
                        {
                            noReproccess = true;
                            ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
                            comp = ent?.Components.Get<WeaponComponent>();

                            var idPacket = (WeaponIdPacket)packet;

                            if (comp == null)
                            {
                                errorPacket.Error = $"[idPacket] Comp was null";
                                break;
                            }
                            //saving on extra field with new packet type
                            comp.Platform.Weapons[idPacket.WeaponId].Target.Reset(Tick, Target.States.ServerReset);

                            report.PacketValid = true;
                            break;
                        }
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

                            comp.SyncIds.MIds[(int)packet.PType] = shootStatePacket.MId;

                            for (int i = 0; i < comp.Platform.Weapons.Length; i++)
                            {
                                var w = comp.Platform.Weapons[i];

                                if (shootStatePacket.Data == ManualShootActionState.ShootOnce)
                                    w.State.SingleShotCounter++;

                                w.State.ManualShoot = shootStatePacket.Data;
                            }

                            report.PacketValid = true;
                            break;
                        }
                    case PacketType.WeaponToolbarShootState:
                        {
                            var shootStatePacket = (ShootStatePacket) packet;
                            ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
                            comp = ent?.Components.Get<WeaponComponent>();

                            if (comp == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) break;

                            if (shootStatePacket.WeaponId == -1)
                            {
                                errorPacket.Error = $"[WeaponToolbarShootState] weapon Id: {shootStatePacket.WeaponId}";
                                break;
                            }

                            comp.SyncIds.MIds[(int)packet.PType] = shootStatePacket.MId;
                            var w = comp.Platform.Weapons[shootStatePacket.WeaponId];

                            if (shootStatePacket.Data == ManualShootActionState.ShootOnce)
                                w.State.SingleShotCounter++;

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

                            comp.SyncIds.MIds[(int)packet.PType] = rangePacket.MId;
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
                                ai.UiMId = midPacket.MId;

                                report.PacketValid = true;
                            }
                            break;
                        }
                    case PacketType.CycleAmmo:
                        {
                            var cyclePacket = (CycleAmmoPacket)packet;

                            if (comp == null || ent.MarkedForClose || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready)
                            {
                                errorPacket.Error = $"[CycleAmmo]  ent.MarkedForClose: {ent?.MarkedForClose} ent is null: {ent == null } comp.Platform.State: {comp?.Platform?.State}";
                                break;
                            }

                            if (cyclePacket.WeaponId == -1)
                            {
                                errorPacket.Error = $"[CycleAmmo] weapon Id: {cyclePacket.WeaponId}";
                                break;
                            }

                            comp.SyncIds.MIds[(int)packet.PType] = cyclePacket.MId;
                            var weapon = comp.Platform.Weapons[cyclePacket.WeaponId];
                            weapon.Set.AmmoTypeId = cyclePacket.AmmoId;

                            if (weapon.State.Sync.CurrentAmmo == 0)
                                weapon.StartReload();

                            break;
                        }
                    case PacketType.GridOverRidesSync:
                        {
                            var gridOverRidePacket = (GridOverRidesSyncPacket)packet;

                            var myGrid = MyEntities.GetEntityByIdOrDefault(packet.EntityId) as MyCubeGrid;

                            if (myGrid == null || myGrid.MarkedForClose)
                            {
                                errorPacket.Error = $"targetPacket is null ent.MarkedForClose: {myGrid?.MarkedForClose} myGrid is null";
                                break;
                            }

                            GridAi ai;
                            if (GridTargetingAIs.TryGetValue(myGrid, out ai))
                            {
                                ai.ReScanBlockGroups();

                                for (int i = 0; i < gridOverRidePacket.Data.Length; i++)
                                {
                                    var groupName = gridOverRidePacket.Data[i].GroupName;
                                    var overRides = gridOverRidePacket.Data[i].Overrides;
                                    if (ai.BlockGroups.ContainsKey(groupName))
                                    {
                                        SyncGridOverrides(ai, groupName, overRides);
                                        report.PacketValid = true;
                                    }
                                    else
                                        errorPacket.Error = "Group did not exist yet.";
                                }
                            }
                                break;
                        }
                    case PacketType.RescanGroupRequest:
                        {
                            var myGrid = MyEntities.GetEntityByIdOrDefault(packet.EntityId) as MyCubeGrid;

                            if (myGrid == null || myGrid.MarkedForClose)
                            {
                                errorPacket.Error = $"myGrid is null: {myGrid == null} ent.MarkedForClose: {myGrid?.MarkedForClose}";
                                break;
                            }

                            GridAi ai;
                            if (GridTargetingAIs.TryGetValue(myGrid, out ai))
                                ai.ReScanBlockGroups(true);

                            report.PacketValid = true;
                            break;
                        }
                    case PacketType.GridFocusListSync:
                        {
                            var myGrid = MyEntities.GetEntityByIdOrDefault(packet.EntityId) as MyCubeGrid;
                            var focusPacket = (GridFocusListPacket)packet;

                            if (myGrid == null || myGrid.MarkedForClose)
                            {
                                errorPacket.Error = $"myGrid is null: {myGrid == null} ent.MarkedForClose: {myGrid?.MarkedForClose}";
                                break;
                            }

                            GridAi ai;
                            if (GridTargetingAIs.TryGetValue(myGrid, out ai))
                            {
                                for(int i = 0; i < focusPacket.EntityIds.Length; i++)
                                {
                                    ai.Focus.Target[i] = MyEntities.GetEntityByIdOrDefault(focusPacket.EntityIds[i]);
                                }
                                report.PacketValid = true;
                            }
                            
                            break;
                        }
                    case PacketType.FocusUpdate:
                    case PacketType.ReassignTargetUpdate:
                    case PacketType.NextActiveUpdate:
                    case PacketType.ReleaseActiveUpdate:
                        {
                            var focusPacket = (FocusPacket) packet;
                            var myGrid = MyEntities.GetEntityByIdOrDefault(packet.EntityId) as MyCubeGrid;

                            if (myGrid == null || myGrid.MarkedForClose)
                            {
                                errorPacket.Error = $"targetPacket is null ent.MarkedForClose: {myGrid?.MarkedForClose} myGrid is null";
                                break;
                            }

                            GridAi ai;
                            if (GridTargetingAIs.TryGetValue(myGrid, out ai))
                            {
                                var targetGrid = MyEntities.GetEntityByIdOrDefault(focusPacket.TargetId) as MyCubeGrid;

                                switch (packet.PType)
                                {
                                    case PacketType.FocusUpdate:
                                        if (targetGrid != null)
                                            ai.Focus.AddFocus(targetGrid, ai, true);
                                        break;
                                    case PacketType.ReassignTargetUpdate:
                                        if (targetGrid != null)
                                            ai.Focus.ReassignTarget(targetGrid, focusPacket.FocusId, ai, true);
                                        break;
                                    case PacketType.NextActiveUpdate:
                                        ai.Focus.NextActive(focusPacket.AddSecondary, ai, true);
                                        break;
                                    case PacketType.ReleaseActiveUpdate:
                                        ai.Focus.ReleaseActive(ai, true);
                                        break;
                                }
                                report.PacketValid = true;
                            }
                            else
                                errorPacket.Error = "GridAi not found";

                            break;
                        }

                    default:
                        if(!retry) Reporter.ReportData[PacketType.Invalid].Add(report);
                        Log.Line($"Invalid Packet Type: {packet.PType} packet type: {packet.GetType()}");
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
                        case PacketType.WeaponSyncUpdate:
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
                    case PacketType.WeaponSyncUpdate:
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

                        PacketsToServer.Add(new RequestTargetsPacket {
                            EntityId = erroredPacket.Packet.EntityId,
                            SenderId = MultiplayerId,
                            PType = PacketType.WeaponUpdateRequest,
                            Comps = new List<long>(compsToCheck),
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
        #endregion

        #region Server Sync

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
                long playerId;

                switch (packet.PType)
                {
                    case PacketType.CompStateUpdate:
                        {
                            var statePacket = packet as StatePacket;
                            ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
                            comp = ent?.Components.Get<WeaponComponent>();

                            if (statePacket?.Data == null || comp == null || ent.MarkedForClose || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready)
                            {
                                errorPacket.Error = $"statePacket?.Data is null: {statePacket?.Data == null} comp is null: {comp == null} ent.MarkedForClose: {ent?.MarkedForClose} ent is null: {ent == null}";
                                break;
                            }

                            if (statePacket.MId > comp.SyncIds.MIds[(int)packet.PType])
                            {
                                comp.SyncIds.MIds[(int)packet.PType] = statePacket.MId;
                                comp.State.Value.Sync(statePacket.Data);
                                PacketsToClient.Add(new PacketInfo { Entity = ent, Packet = statePacket });

                                report.PacketValid = true;
                            }
                            else
                                errorPacket.Error = "Mid is old, likely multiple clients attempting update";

                            break;
                        }
                    case PacketType.CompSettingsUpdate:
                        {
                            var setPacket = packet as SettingPacket;
                            ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
                            comp = ent?.Components.Get<WeaponComponent>();

                            if (setPacket?.Data == null || comp == null || ent.MarkedForClose || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready)
                            {
                                errorPacket.Error = $"setPacket?.Data is null: {setPacket?.Data == null} comp is null: {comp == null} ent.MarkedForClose: {ent?.MarkedForClose} ent is null: {ent == null}";
                                break;
                            }

                            if (setPacket.MId > comp.SyncIds.MIds[(int)packet.PType])
                            {
                                comp.SyncIds.MIds[(int)packet.PType] = setPacket.MId;
                                comp.Set.Value.Sync(comp, setPacket.Data);
                                PacketsToClient.Add(new PacketInfo { Entity = ent, Packet = setPacket });

                                report.PacketValid = true;
                            }
                            else
                                errorPacket.Error = "Mid is old, likely multiple clients attempting update";

                            break;
                        }
                    case PacketType.ClientMouseEvent:
                        {
                            var inputPacket = packet as InputPacket;
                            ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);

                            if (inputPacket?.Data == null || ent == null || ent.MarkedForClose)
                            {
                                errorPacket.Error = $"mousePacket?.Data is null: {inputPacket?.Data == null} ent.MarkedForClose: {ent?.MarkedForClose} ent is null: {ent}";
                                break;
                            }

                            if (SteamToPlayer.TryGetValue(packet.SenderId, out playerId))
                            {
                                PlayerMouseStates[playerId] = inputPacket.Data;
                                PacketsToClient.Add(new PacketInfo { Entity = ent, Packet = inputPacket });

                                report.PacketValid = true;
                            }
                            else
                                errorPacket.Error = "Player Not Found";

                            break;
                        }
                    case PacketType.ActiveControlUpdate:
                        {
                            var dPacket = packet as BoolUpdatePacket;
                            var block = MyEntities.GetEntityByIdOrDefault(packet.EntityId) as MyCubeBlock;

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
                    case PacketType.FakeTargetUpdate:
                        {
                            var targetPacket = packet as FakeTargetPacket;
                            var myGrid = MyEntities.GetEntityByIdOrDefault(packet.EntityId) as MyCubeGrid;

                            if (targetPacket?.Data == null || myGrid == null || myGrid.MarkedForClose)
                            {
                                errorPacket.Error = $"targetPacket?.Data is null: {targetPacket?.Data == null} ent.MarkedForClose: {myGrid?.MarkedForClose} myGrid is null";
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
                    case PacketType.GridSyncRequestUpdate:
                        {
                            var myGrid = MyEntities.GetEntityByIdOrDefault(packet.EntityId) as MyCubeGrid;

                            if (myGrid == null || myGrid.MarkedForClose)
                            {
                                errorPacket.Error = $"ent.MarkedForClose: {myGrid?.MarkedForClose} myGrid is null: {myGrid == null }";
                                break;
                            }

                            GridAi ai;
                            if (GridTargetingAIs.TryGetValue(myGrid, out ai))
                            {
                                var c = 0;
                                var playerToBlocks = new PlayerToBlock[ai.ControllingPlayers.Keys.Count];
                                foreach (var playerBlockPair in ai.ControllingPlayers)
                                {
                                    if (playerBlockPair.Value != null)
                                    {
                                        playerToBlocks[c] = new PlayerToBlock
                                        {
                                            PlayerId = playerBlockPair.Key,
                                            EntityId = playerBlockPair.Value.EntityId
                                        };

                                        c++;
                                    }
                                    else
                                        ai.ControllingPlayers.Remove(playerBlockPair.Key);
                                }

                                ai.ControllingPlayers.ApplyRemovals();

                                Array.Resize(ref playerToBlocks, c + 1);

                                PacketsToClient.Add(new PacketInfo {
                                    Entity = myGrid,
                                    Packet = new CurrentGridPlayersPacket
                                    {
                                        EntityId = packet.EntityId,
                                        SenderId = packet.SenderId,
                                        PType = PacketType.ActiveControlFullUpdate,
                                        Data = new ControllingPlayersSync
                                        {
                                            PlayersToControlledBlock = playerToBlocks
                                        }
                                    },
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
                                        MId = ai.UiMId,
                                    },
                                    SingleClient = true,
                                });

                                var gridPacket = new GridWeaponPacket
                                {
                                    EntityId = packet.EntityId,
                                    SenderId = packet.SenderId,
                                    PType = PacketType.WeaponSyncUpdate,
                                    Data = new List<WeaponData>()
                                };
                                
                                foreach (var cubeComp in ai.WeaponBase)
                                {
                                    comp = cubeComp.Value;

                                    if (comp.MyCube == null || comp.MyCube.MarkedForClose || comp.MyCube.Closed || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) continue;

                                    for (int j = 0; j < comp.Platform.Weapons.Length; j++)
                                    {
                                        var w = comp.Platform.Weapons[j];

                                        if (comp.WeaponValues.Targets == null || comp.WeaponValues.Targets[j].State == TransferTarget.TargetInfo.Expired)
                                            continue;

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

                                var overrides = new OverRidesData[ai.BlockGroups.Values.Count];

                                c = 0;
                                foreach (var group in ai.BlockGroups)
                                {
                                    overrides[c].Overrides = GetOverrides(ai, group.Key);
                                    overrides[c].GroupName = group.Key;
                                    c++;
                                }

                                if(overrides.Length > 0)
                                {
                                    PacketsToClient.Add(new PacketInfo
                                    {
                                        Entity = myGrid, 
                                        Packet = new GridOverRidesSyncPacket
                                        {
                                            EntityId = myGrid.EntityId,
                                            SenderId = packet.SenderId,
                                            PType = PacketType.GridOverRidesSync,
                                            Data = overrides,
                                        },
                                        SingleClient = true,
                                    });
                                }

                                var focusPacket = new GridFocusListPacket
                                {
                                    EntityId = myGrid.EntityId,
                                    SenderId = packet.SenderId,
                                    PType = PacketType.GridFocusListSync,
                                    EntityIds = new long[ai.Focus.Target.Length]
                                };

                                for(int i = 0; i < ai.Focus.Target.Length; i++)
                                    focusPacket.EntityIds[i] = ai.Focus.Target[i]?.EntityId ?? -1;

                                PacketsToClient.Add(new PacketInfo
                                {
                                    Entity = myGrid,
                                    Packet = focusPacket,
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
                            ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
                            comp = ent?.Components.Get<WeaponComponent>();

                            if (reticlePacket == null || comp == null || ent.MarkedForClose || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready)
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

                            if (comp != null && comp.SyncIds.MIds[(int)packet.PType] < overRidesPacket.MId)
                            {
                                comp.Set.Value.Overrides.Sync(overRidesPacket.Data);
                                comp.SyncIds.MIds[(int)packet.PType] = overRidesPacket.MId;

                                GroupInfo group;
                                if (!string.IsNullOrEmpty(comp.State.Value.CurrentBlockGroup) && comp.Ai.BlockGroups.TryGetValue(comp.State.Value.CurrentBlockGroup, out group))
                                {
                                    comp.Ai.ScanBlockGroupSettings = true;
                                    comp.Ai.GroupsToCheck.Add(group);
                                }
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
                                        {
                                            component.State.Value.CurrentBlockGroup = overRidesPacket.GroupName;
                                            component.Set.Value.Overrides.Sync(o);
                                        }

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
                        {
                            ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
                            comp = ent?.Components.Get<WeaponComponent>();
                            var cPlayerPacket = (ControllingPlayerPacket)packet;

                            if (comp == null || ent.MarkedForClose || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready)
                            {
                                errorPacket.Error = $"[overRidesPacket] comp is null: {comp == null} ent.MarkedForClose: {ent?.MarkedForClose}";
                                break;
                            }

                            if (comp.SyncIds.MIds[(int)packet.PType] < cPlayerPacket.MId)
                            {
                                comp.State.Value.CurrentPlayerControl.Sync(cPlayerPacket.Data);
                                comp.SyncIds.MIds[(int)packet.PType] = cPlayerPacket.MId;
                                report.PacketValid = true;
                                PacketsToClient.Add(new PacketInfo { Entity = comp.MyCube, Packet = cPlayerPacket });
                            }
                            else
                                errorPacket.Error = "Mid is old, likely multiple clients attempting update";
                            break;
                        }                        
                    case PacketType.WeaponUpdateRequest:
                        {
                            var targetRequestPacket = (RequestTargetsPacket) packet;
                            var myGrid = MyEntities.GetEntityByIdOrDefault(packet.EntityId) as MyCubeGrid;

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
                                    PType = PacketType.WeaponSyncUpdate,
                                    Data = new List<WeaponData>()
                                };

                                for (int i = 0; i < targetRequestPacket.Comps.Count; i++)
                                {
                                    var compId = targetRequestPacket.Comps[i];
                                    var compCube = MyEntities.GetEntityByIdOrDefault(compId) as MyCubeBlock;

                                    if (compCube == null || !ai.WeaponBase.TryGetValue(compCube, out comp) || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready)
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
                            break;
                        }                        
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
                            break;
                        }
                    case PacketType.CompToolbarShootState:
                        {
                            var shootStatePacket = (ShootStatePacket) packet;
                            ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
                            comp = ent?.Components.Get<WeaponComponent>();

                            if (ent == null || comp == null || ent.MarkedForClose || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready)
                            {
                                errorPacket.Error = $"[shootStatePacket] ent.MarkedForClose: {ent?.MarkedForClose} ent is null:";
                                break;
                            }

                            if (comp.SyncIds.MIds[(int)packet.PType] < shootStatePacket.MId)
                            {
                                comp.SyncIds.MIds[(int)packet.PType] = shootStatePacket.MId;
                                for (int i = 0; i < comp.Platform.Weapons.Length; i++)
                                {
                                    var w = comp.Platform.Weapons[i];

                                    if (shootStatePacket.Data == ManualShootActionState.ShootOnce)
                                        w.State.SingleShotCounter++;

                                    w.State.ManualShoot = shootStatePacket.Data;
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
                            var shootStatePacket = (ShootStatePacket) packet;
                            ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
                            comp = ent?.Components.Get<WeaponComponent>();

                            if (comp == null || ent.MarkedForClose || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready)
                            {
                                errorPacket.Error = $"[shootStatePacket] ent.MarkedForClose: {ent?.MarkedForClose} ent is null: {ent == null }";
                                break;
                            }

                            if (comp.SyncIds.MIds[(int)packet.PType] < shootStatePacket.MId)
                            {
                                comp.SyncIds.MIds[(int)packet.PType] = shootStatePacket.MId;
                                var weaponId = 0;
                                if (shootStatePacket.WeaponId < 0 || shootStatePacket.WeaponId > comp.Platform.Weapons.Length - 1)
                                    Log.Line( $"invalid weaponId sync in WeaponToolbarShootState, defaulting to 0: {shootStatePacket.WeaponId}");
                                else weaponId = shootStatePacket.WeaponId;

                                var w = comp.Platform.Weapons[weaponId];

                                if (shootStatePacket.Data == ManualShootActionState.ShootOnce)
                                    w.State.SingleShotCounter++;

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
                            ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
                            comp = ent?.Components.Get<WeaponComponent>();

                            if (comp == null || ent.MarkedForClose || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready)
                            {
                                errorPacket.Error = $"[shootStatePacket]  ent.MarkedForClose: {ent?.MarkedForClose} ent is null: {ent == null }";
                                break;
                            }

                            if (comp.SyncIds.MIds[(int)packet.PType] < rangePacket.MId)
                            {
                                comp.SyncIds.MIds[(int)packet.PType] = rangePacket.MId;
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
                    case PacketType.CycleAmmo:
                        {
                            var cyclePacket = (CycleAmmoPacket)packet;
                            ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
                            comp = ent?.Components.Get<WeaponComponent>();

                            if (comp == null || ent.MarkedForClose || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready)
                            {
                                errorPacket.Error = $"[shootStatePacket]  ent.MarkedForClose: {ent?.MarkedForClose} ent is null: {ent == null } comp.Platform.State: {comp?.Platform?.State}";
                                break;
                            }

                            if (cyclePacket.MId > comp.SyncIds.MIds[(int)packet.PType])
                            {
                                comp.SyncIds.MIds[(int)packet.PType] = cyclePacket.MId;

                                var weapon = comp.Platform.Weapons[cyclePacket.WeaponId];

                                weapon.Set.AmmoTypeId = cyclePacket.AmmoId;

                                if (weapon.State.Sync.CurrentAmmo == 0)
                                    weapon.StartReload();

                                PacketsToClient.Add(new PacketInfo
                                {
                                    Entity = ent,
                                    Packet = cyclePacket,
                                });

                                report.PacketValid = true;
                            }
                            else
                                errorPacket.Error = "Mid is old, likely multiple clients attempting update";

                            break;
                        }
                    case PacketType.RescanGroupRequest:
                        {
                            var myGrid = MyEntities.GetEntityByIdOrDefault(packet.EntityId) as MyCubeGrid;

                            if (myGrid == null || myGrid.MarkedForClose)
                            {
                                errorPacket.Error = $"myGrid is null: {myGrid == null} ent.MarkedForClose: {myGrid?.MarkedForClose}";
                                break;
                            }

                            GridAi ai;
                            if (GridTargetingAIs.TryGetValue(myGrid, out ai))
                                ai.ReScanBlockGroups(true);

                            PacketsToClient.Add(new PacketInfo { Entity = myGrid, Packet = packet });

                            report.PacketValid = true;
                            break;
                        }
                    case PacketType.FixedWeaponHitEvent:
                        {
                            var hitPacket = (FixedWeaponHitPacket)packet;

                            ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
                            comp = ent?.Components.Get<WeaponComponent>();

                            Log.Line($"packet.EntityId: {packet.EntityId} hitPacket: {hitPacket.HitEnt}");

                            if (comp == null || comp.MyCube.MarkedForClose || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready)
                            {
                                errorPacket.Error = $"comp is null: {comp == null} ent is null: {ent == null} ent.MarkedForClose: {ent?.MarkedForClose}";
                                break;
                            }

                            

                            var weapon = comp.Platform.Weapons[hitPacket.WeaponId];
                            var targetEnt = MyEntities.GetEntityByIdOrDefault(hitPacket.HitEnt);
                            
                            var hitPos = targetEnt.PositionComp.WorldMatrixRef.Translation - hitPacket.HitOffset;
                            var origin = hitPos - hitPacket.HitDirection;

                            CreateFixedWeaponProjectile(weapon, targetEnt, origin, hitPacket.HitDirection, hitPacket.Up, hitPacket.MuzzleId);

                            report.PacketValid = true;
                            break;
                        }
                    case PacketType.FocusUpdate:
                    case PacketType.ReassignTargetUpdate:
                    case PacketType.NextActiveUpdate:
                    case PacketType.ReleaseActiveUpdate:
                        {
                            var focusPacket = (FocusPacket) packet;
                            var myGrid = MyEntities.GetEntityByIdOrDefault(packet.EntityId) as MyCubeGrid;

                            if (myGrid == null || myGrid.MarkedForClose)
                            {
                                errorPacket.Error = $"targetPacket is null: ent.MarkedForClose: {myGrid?.MarkedForClose} myGrid is null";
                                break;
                            }

                            GridAi ai;
                            if (GridTargetingAIs.TryGetValue(myGrid, out ai))
                            {
                                var targetGrid = MyEntities.GetEntityByIdOrDefault(focusPacket.TargetId) as MyCubeGrid;

                                switch (packet.PType)
                                {
                                    case PacketType.FocusUpdate:
                                        if (targetGrid != null)
                                            ai.Focus.AddFocus(targetGrid, ai, true);
                                        break;
                                    case PacketType.ReassignTargetUpdate:
                                        if (targetGrid != null)
                                            ai.Focus.ReassignTarget(targetGrid, focusPacket.FocusId, ai, true);
                                        break;
                                    case PacketType.NextActiveUpdate:
                                        ai.Focus.NextActive(focusPacket.AddSecondary, ai, true);
                                        break;
                                    case PacketType.ReleaseActiveUpdate:
                                        ai.Focus.ReleaseActive(ai, true);
                                        break;
                                }

                                PacketsToClient.Add(new PacketInfo { Entity = myGrid, Packet = focusPacket });
                                report.PacketValid = true;
                            }
                            else
                                errorPacket.Error = "GridAi not found";

                            break;
                        }
                    default:
                        Reporter.ReportData[PacketType.Invalid].Add(report);
                        report.PacketValid = false;

                        break;
                }

                if (!report.PacketValid)
                    Log.Line(errorPacket.Error);
            }
            catch (Exception ex) { Log.Line($"Exception in ReceivedPacket: PacketType:{ptype} Exception: {ex}"); }
        }

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
                    Timmings = null,
                    SyncData = null
                };

                if (w.SendTarget && w.Comp.WeaponValues.Targets != null)
                    weaponSync.TargetData = w.Comp.WeaponValues.Targets[w.WeaponId];
                else if (w.SendTarget)
                    continue;

                if (w.SendSync && w.Timings != null && w.State.Sync != null)
                {
                    weaponSync.Timmings = w.Timings.SyncOffsetServer(_session.Tick);
                    weaponSync.SyncData = w.State.Sync;
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
