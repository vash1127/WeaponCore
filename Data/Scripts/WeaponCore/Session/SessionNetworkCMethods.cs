using System.Collections.Generic;
using Sandbox.Game.Entities;
using WeaponCore.Control;
using WeaponCore.Platform;
using WeaponCore.Support;
using static WeaponCore.Platform.Weapon;
using static WeaponCore.Support.GridAi;
namespace WeaponCore
{
    public partial class Session
    {
        public void ReproccessClientErrorPackets()
        {
            foreach (var errorPacket in ClientSideErrorPkt)
            {
                if (errorPacket == null) {
                    Log.Line($"ClientSideErrorPktListNew errorPacket is null");
                    continue;
                }

                if (errorPacket.MaxAttempts == 0)
                {
                    //set packet retry variables, based on type
                    //erroredPacket.MaxAttempts = 3;
                    //erroredPacket.RetryAttempt = 0;                    

                    switch (errorPacket.PType)
                    {
                        case PacketType.WeaponSyncUpdate:
                            errorPacket.MaxAttempts = 7;
                            errorPacket.RetryDelayTicks = 15;
                            break;

                        default:
                            errorPacket.MaxAttempts = 7;
                            errorPacket.RetryDelayTicks = 15;
                            break;
                    }

                    errorPacket.RetryTick = Tick + errorPacket.RetryDelayTicks;
                }

                //proccess packet logic

                if (errorPacket.RetryTick > Tick) continue;
                errorPacket.RetryAttempt++;

                var success = false;

                switch (errorPacket.PType)
                {
                    case PacketType.WeaponSyncUpdate:
                        var ent = MyEntities.GetEntityByIdOrDefault(errorPacket.Packet.EntityId);
                        if (ent == null) break;

                        var packet = errorPacket.Packet as GridWeaponPacket;
                        if (packet?.Data == null) {
                            Log.Line($"GridWeaponPacket errorPacket is null");
                            errorPacket.MaxAttempts = 0;
                            break;
                        }

                        var compsToCheck = new HashSet<long>();
                        for (int j = 0; j < packet.Data.Count; j++) {
                            var weaponData = packet.Data[j];
                            if (weaponData != null)
                                compsToCheck.Add(weaponData.CompEntityId);
                        }

                        if (compsToCheck.Count > 0)
                        {
                            PacketsToServer.Add(new RequestTargetsPacket
                            {
                                EntityId = errorPacket.Packet.EntityId,
                                SenderId = MultiplayerId,
                                PType = PacketType.WeaponUpdateRequest,
                                Comps = new List<long>(compsToCheck),
                            });
                            success = true;
                        }
                        break;

                    default:
                        var report = Reporter.ReportPool.Get();
                        report.Receiver = NetworkReporter.Report.Received.Client;
                        report.PacketSize = 0;
                        Reporter.ReportData[errorPacket.Packet.PType].Add(report);
                        var packetObj = PacketObjPool.Get();
                        packetObj.Packet = errorPacket.Packet; packetObj.PacketSize = 0; packetObj.Report = report; packetObj.ErrorPacket = errorPacket;

                        success = ProccessClientPacket(packetObj);
                        break;
                }

                if (success || errorPacket.RetryAttempt > errorPacket.MaxAttempts)
                {
                    if (!success)
                        Log.LineShortDate($"        [BadReprocess] Entity:{errorPacket.Packet?.EntityId} Cause:{errorPacket.Error ?? string.Empty}", "net");

                    ClientSideErrorPkt.Remove(errorPacket);
                }
                else
                    errorPacket.RetryTick = Tick + errorPacket.RetryDelayTicks;

                if (errorPacket.MaxAttempts == 0)
                    ClientSideErrorPkt.Remove(errorPacket);
            }
            ClientSideErrorPkt.ApplyChanges();
        }

        private bool ClientCompStateUpdate(PacketObj data)
        {
            var packet = data.Packet;
            var ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
            var comp = ent?.Components.Get<WeaponComponent>();
            var statePacket = (StatePacket)packet;
            if (comp?.Ai == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return Error(data, Msg($"CompId: {packet.EntityId}", comp != null), Msg("Ai", comp?.Ai != null), Msg("Ai", comp?.Platform.State == MyWeaponPlatform.PlatformState.Ready));
            if (statePacket.Data == null) return Error(data,  Msg("Data"));

            comp.MIds[(int)packet.PType] = statePacket.MId;
            comp.State.Value.Sync(statePacket.Data, comp);
            data.Report.PacketValid = true;

            return true;
        }

        private bool ClientCompSettingsUpdate(PacketObj data)
        {
            var packet = data.Packet;
            var ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
            var comp = ent?.Components.Get<WeaponComponent>();
            var setPacket = (SettingPacket)packet;
            if (comp?.Ai == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return Error(data, Msg($"CompId: {packet.EntityId}", comp != null), Msg("Ai", comp?.Ai != null), Msg("Ai", comp?.Platform.State == MyWeaponPlatform.PlatformState.Ready));
            if (setPacket.Data == null) return Error(data, Msg("Data"));

            comp.MIds[(int)packet.PType] = setPacket.MId;
            comp.Set.Value.Sync(comp, setPacket.Data);
            data.Report.PacketValid = true;
            return true;
        }

        private bool ClientWeaponSyncUpdate(PacketObj data)
        {
            var packet = data.Packet;
            var targetPacket = (GridWeaponPacket)packet;
            if (targetPacket.Data == null) return Error(data, Msg("Data"));

            for (int j = 0; j < targetPacket.Data.Count; j++) {

                var weaponData = targetPacket.Data[j];
                var block = MyEntities.GetEntityByIdOrDefault(weaponData.CompEntityId) as MyCubeBlock;
                var comp = block?.Components.Get<WeaponComponent>();

                if (comp?.Ai == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return Error(data, Msg("Comp", comp != null), Msg("Ai", comp?.Platform.State == MyWeaponPlatform.PlatformState.Ready));

                var validData= weaponData.SyncData != null && weaponData.WeaponRng != null;
                var validTargetData = weaponData.TargetData != null;
                
                if (!validTargetData && !validData)
                    Error(data, Msg($"No valid weapon Data or Timings for {comp.MyCube.BlockDefinition.Id.SubtypeName} - aiAge:{Tick - comp.Ai.AiSpawnTick}"));

                Weapon weapon;
                if (validData) {
                    weapon = comp.Platform.Weapons[weaponData.SyncData.WeaponId];
                    SyncWeapon(weapon, ref weaponData.SyncData);

                    weapon.Comp.WeaponValues.WeaponRandom[weapon.WeaponId].Sync(weaponData.WeaponRng);
                }

                if (validTargetData) {

                    weapon = comp.Platform.Weapons[weaponData.TargetData.WeaponId];
                    weaponData.TargetData.SyncTarget(weapon.Target);

                    if (weapon.Target.HasTarget) {

                        if (!weapon.Target.IsProjectile && !weapon.Target.IsFakeTarget && weapon.Target.Entity == null) {
                            var oldChange = weapon.Target.TargetChanged;
                            weapon.Target.StateChange(true, Target.States.Invalid);
                            weapon.Target.TargetChanged = !weapon.FirstSync && oldChange;
                            weapon.FirstSync = false;
                        }
                        else if (weapon.Target.IsProjectile) {

                            TargetType targetType;
                            AcquireProjectile(weapon, out targetType);

                            if (targetType == TargetType.None) {
                                if (weapon.NewTarget.CurrentState != Target.States.NoTargetsSeen)
                                    weapon.NewTarget.Reset(weapon.Comp.Session.Tick, Target.States.NoTargetsSeen);
                                if (weapon.Target.CurrentState != Target.States.NoTargetsSeen) weapon.Target.Reset(weapon.Comp.Session.Tick, Target.States.NoTargetsSeen, !weapon.Comp.TrackReticle);
                            }
                        }
                    }
                }

                data.Report.PacketValid = true;
            }

            return true;
        }

        private bool ClientFakeTargetUpdate(PacketObj data)
        {
            var packet = data.Packet;
            data.ErrorPacket.NoReprocess = true; 
            var targetPacket = (FakeTargetPacket)packet;
            var myGrid = MyEntities.GetEntityByIdOrDefault(packet.EntityId) as MyCubeGrid;

            GridAi ai;
            //TODO client uses try get in case packets are out of order, no need to reprocess as fake targets are sent very often
            if (myGrid != null && GridTargetingAIs.TryGetValue(myGrid, out ai))
            {
                long playerId;
                if (SteamToPlayer.TryGetValue(packet.SenderId, out playerId))
                {
                    FakeTarget dummyTarget;
                    if (PlayerDummyTargets.TryGetValue(playerId, out dummyTarget))
                    {
                        dummyTarget.Update(targetPacket.Data, ai, null, true);
                        data.Report.PacketValid = true;
                    }
                    else
                        return Error(data, Msg("Player dummy target not found"));
                }
                else
                    return Error(data, Msg("SteamToPlayer missing Player"));
            }
            else
                return Error(data, Msg($"GridId: {packet.EntityId}", myGrid != null), Msg("Ai"));

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

        private bool ClientClientMouseEvent(PacketObj data)
        {
            var packet = data.Packet;
            var mousePacket = (InputPacket)packet;
            if (mousePacket.Data == null) return Error(data, Msg("Data"));

            long playerId;
            if (SteamToPlayer.TryGetValue(packet.SenderId, out playerId)) {
                
                PlayerMouseStates[playerId] = mousePacket.Data;
                data.Report.PacketValid = true;
            }
            else
                return Error(data, Msg("No Player Mouse State Found"));

            return true;
        }

        private bool ClientActiveControlUpdate(PacketObj data)
        {
            var packet = data.Packet;
            var dPacket = (BoolUpdatePacket)packet;
            var cube = MyEntities.GetEntityByIdOrDefault(packet.EntityId) as MyCubeBlock;
            if (cube == null) return Error(data, Msg($"CubeId: {packet.EntityId}"));

            long playerId;
            SteamToPlayer.TryGetValue(packet.SenderId, out playerId);

            UpdateActiveControlDictionary(cube, playerId, dPacket.Data);

            data.Report.PacketValid = true;

            return true;
        }

        private bool ClientActiveControlFullUpdate(PacketObj data)
        {
            var packet = data.Packet;
            var csPacket = (CurrentGridPlayersPacket)packet;

            //null = 0 players in grid on stream/load
            if (csPacket.Data.PlayersToControlledBlock != null && csPacket.Data.PlayersToControlledBlock.Length > 0)
            {
                for (int i = 0; i < csPacket.Data.PlayersToControlledBlock.Length; i++)
                {

                    var playerBlock = csPacket.Data.PlayersToControlledBlock[i];
                    var cube = MyEntities.GetEntityByIdOrDefault(playerBlock.EntityId) as MyCubeBlock;
                    if (cube?.CubeGrid == null) return Error(data, Msg($"CubeId:{playerBlock.EntityId} - pId:{playerBlock.PlayerId}", cube != null), Msg("Grid"));

                    UpdateActiveControlDictionary(cube, playerBlock.PlayerId, true);
                }
            }

            data.Report.PacketValid = true;

            return true;

        }

        private bool ClientReticleUpdate(PacketObj data)
        {
            var packet = data.Packet;
            var reticlePacket = (BoolUpdatePacket)packet;

            var ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
            var comp = ent?.Components.Get<WeaponComponent>();
            if (comp?.Ai == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return Error(data, Msg($"CompId: {packet.EntityId}", comp != null), Msg("Ai", comp?.Ai != null), Msg("Ai", comp?.Platform.State == MyWeaponPlatform.PlatformState.Ready));

            comp.State.Value.OtherPlayerTrackingReticle = reticlePacket.Data;
            data.Report.PacketValid = true;
            return true;

        }

        private bool ClientOverRidesUpdate(PacketObj data)
        {
            var packet = data.Packet;
            var overRidesPacket = (OverRidesPacket)packet;
            var ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
            var comp = ent?.Components.Get<WeaponComponent>();
            var myGrid = ent as MyCubeGrid;

            if (comp?.Ai == null && myGrid == null) return Error(data, Msg($"CompId: {packet.EntityId}", comp != null), Msg("Ai+Grid"));

            if (overRidesPacket.Data == null) return Error(data, Msg("Data"));

            if (comp?.Ai != null && comp.MIds[(int)packet.PType] < overRidesPacket.MId) {

                comp.Set.Value.Overrides.Sync(overRidesPacket.Data);
                comp.MIds[(int)packet.PType] = overRidesPacket.MId;

                GroupInfo group;
                if (!string.IsNullOrEmpty(comp.State.Value.CurrentBlockGroup) && comp.Ai.BlockGroups.TryGetValue(comp.State.Value.CurrentBlockGroup, out group)) {
                    comp.Ai.ScanBlockGroupSettings = true;
                    comp.Ai.GroupsToCheck.Add(group);
                }
                data.Report.PacketValid = true;
            }
            else if (myGrid != null)
            {
                GridAi ai;
                if (GridTargetingAIs.TryGetValue(myGrid, out ai) && ai.UiMId < overRidesPacket.MId) {
                    var o = overRidesPacket.Data;
                    ai.UiMId = overRidesPacket.MId;

                    ai.ReScanBlockGroups();

                    SyncGridOverrides(ai, overRidesPacket.GroupName, o);

                    GroupInfo groups;
                    if (ai.BlockGroups.TryGetValue(overRidesPacket.GroupName, out groups)) {

                        foreach (var component in groups.Comps) {
                            component.State.Value.CurrentBlockGroup = overRidesPacket.GroupName;
                            component.Set.Value.Overrides.Sync(o);
                        }

                        data.Report.PacketValid = true;
                    }
                    else
                        return Error(data, Msg("Block group not found"));
                }
                else
                    return Error(data, Msg($"GridAi not found, is marked:{myGrid.MarkedForClose}, has root:{GridToMasterAi.ContainsKey(myGrid)}"));
            }

            return true;
        }

        private bool ClientPlayerControlUpdate(PacketObj data)
        {
            var packet = data.Packet;
            var cPlayerPacket = (ControllingPlayerPacket)packet;
            var ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
            var comp = ent?.Components.Get<WeaponComponent>();
            if (comp?.Ai == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return Error(data, Msg($"CompId: {packet.EntityId}", comp != null), Msg("Ai", comp?.Ai != null), Msg("Ai", comp?.Platform.State == MyWeaponPlatform.PlatformState.Ready));

            comp.State.Value.CurrentPlayerControl.Sync(cPlayerPacket.Data);
            comp.MIds[(int)packet.PType] = cPlayerPacket.MId;
            data.Report.PacketValid = true;

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
            comp.Platform.Weapons[idPacket.WeaponId].Target.Reset(Tick, Target.States.ServerReset);

            data.Report.PacketValid = true;

            return true;

        }

        private bool ClientFullMouseUpdate(PacketObj data)
        {
            var packet = data.Packet;
            var mouseUpdatePacket = (MouseInputSyncPacket)packet;

            if (mouseUpdatePacket.Data == null) return Error(data, Msg("Data"));

            for (int i = 0; i < mouseUpdatePacket.Data.Length; i++) {
                var playerMousePackets = mouseUpdatePacket.Data[i];
                if (playerMousePackets.PlayerId != PlayerId)
                    PlayerMouseStates[playerMousePackets.PlayerId] = playerMousePackets.MouseStateData;
            }

            data.Report.PacketValid = true;
            return true;

        }

        private bool ClientCompToolbarShootState(PacketObj data)
        {
            var packet = data.Packet;
            var shootStatePacket = (ShootStatePacket)packet;
            var ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
            var comp = ent?.Components.Get<WeaponComponent>();

            if (comp?.Ai == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return Error(data, Msg($"CompId: {packet.EntityId}", comp != null), Msg("Ai", comp?.Ai != null), Msg("Ai", comp?.Platform.State == MyWeaponPlatform.PlatformState.Ready));

            comp.MIds[(int)packet.PType] = shootStatePacket.MId;

            switch (shootStatePacket.Data)
            {
                case ManualShootActionState.ShootClick:
                    TerminalHelpers.WcShootClickAction(comp, true, comp.HasTurret, true);
                    break;
                case ManualShootActionState.ShootOff:
                    TerminalHelpers.WcShootOffAction(comp, true);
                    break;
                case ManualShootActionState.ShootOn:
                    TerminalHelpers.WcShootOnAction(comp, true);
                    break;
                case ManualShootActionState.ShootOnce:
                    TerminalHelpers.WcShootOnceAction(comp, true);
                    break;
            }

            data.Report.PacketValid = true;
            return true;
        }

        private bool ClientRangeUpdate(PacketObj data)
        {
            var packet = data.Packet;
            var rangePacket = (RangePacket)packet;
            var ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
            var comp = ent?.Components.Get<WeaponComponent>();
            if (comp?.Ai == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return Error(data, Msg($"CompId: {packet.EntityId}", comp != null), Msg("Ai", comp?.Ai != null), Msg("Ai", comp?.Platform.State == MyWeaponPlatform.PlatformState.Ready));

            comp.MIds[(int)packet.PType] = rangePacket.MId;
            comp.Set.Value.Range = rangePacket.Data;

            data.Report.PacketValid = true;
            return true;
        }

        private bool ClientGridAiUiMidUpdate(PacketObj data)
        {
            var packet = data.Packet;
            var myGrid = MyEntities.GetEntityByIdOrDefault(packet.EntityId) as MyCubeGrid;
            var midPacket = (MIdPacket)packet;

            if (myGrid == null) return Error(data, Msg("Grid"));

            GridAi ai;
            if (GridTargetingAIs.TryGetValue(myGrid, out ai)) {
                ai.UiMId = midPacket.MId;
                data.Report.PacketValid = true;
            }
            else
                return Error(data, Msg($"GridAi not found, is marked:{myGrid.MarkedForClose}, has root:{GridToMasterAi.ContainsKey(myGrid)}"));

            return true;
        }

        private bool ClientCycleAmmo(PacketObj data)
        {
            var packet = data.Packet;
            var cyclePacket = (CycleAmmoPacket)packet;
            var ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
            var comp = ent?.Components.Get<WeaponComponent>();
            if (comp?.Ai == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return Error(data, Msg($"CompId: {packet.EntityId}", comp != null), Msg("Ai", comp?.Ai != null), Msg("Ai", comp?.Platform.State == MyWeaponPlatform.PlatformState.Ready));

            comp.MIds[(int)packet.PType] = cyclePacket.MId;
            var weapon = comp.Platform.Weapons[cyclePacket.WeaponId];
            weapon.Set.AmmoTypeId = cyclePacket.AmmoId;
            if (IsCreative || !weapon.ActiveAmmoDef.AmmoDef.Const.Reloadable)
                weapon.ChangeActiveAmmo(weapon.System.AmmoTypes[cyclePacket.AmmoId]);

            data.Report.PacketValid = true;

            return true;
        }

        private bool ClientGridOverRidesSync(PacketObj data)
        {
            var packet = data.Packet;
            var gridOverRidePacket = (GridOverRidesSyncPacket)packet;

            var myGrid = MyEntities.GetEntityByIdOrDefault(packet.EntityId) as MyCubeGrid;
            if (myGrid == null) return Error(data, Msg($"GridId: {packet.EntityId}"));

            GridAi ai;
            if (GridTargetingAIs.TryGetValue(myGrid, out ai))
            {
                ai.ReScanBlockGroups();

                for (int i = 0; i < gridOverRidePacket.Data.Length; i++) {

                    var groupName = gridOverRidePacket.Data[i].GroupName;
                    var overRides = gridOverRidePacket.Data[i].Overrides;

                    if (ai.BlockGroups.ContainsKey(groupName)) {
                        SyncGridOverrides(ai, groupName, overRides);
                        data.Report.PacketValid = true;
                    }
                    else
                        return Error(data, Msg($"group did not exist: {groupName} - gridMarked:{myGrid.MarkedForClose} - aiMarked:{ai.MarkedForClose}({ai.Version})"));
                }
            }
            else
                return Error(data, Msg($"GridAi not found, is marked:{myGrid.MarkedForClose}, has root:{GridToMasterAi.ContainsKey(myGrid)}"));

            return true;

        }

        private bool ClientRescanGroupRequest(PacketObj data)
        {
            var packet = data.Packet;
            var myGrid = MyEntities.GetEntityByIdOrDefault(packet.EntityId) as MyCubeGrid;
            if (myGrid == null) return Error(data, Msg($"GridId: {packet.EntityId}"));

            GridAi ai;
            if (GridTargetingAIs.TryGetValue(myGrid, out ai))
                ai.ReScanBlockGroups(true);
            else
                return Error(data, Msg($"GridAi not found, is marked:{myGrid.MarkedForClose}, has root:{GridToMasterAi.ContainsKey(myGrid)}"));

            data.Report.PacketValid = true;
            return true;

        }

        private bool ClientGridFocusListSync(PacketObj data)
        {
            var packet = data.Packet;
            var myGrid = MyEntities.GetEntityByIdOrDefault(packet.EntityId) as MyCubeGrid;
            var focusPacket = (GridFocusListPacket)packet;
            if (myGrid == null) return Error(data, Msg($"Grid: {packet.EntityId}"));

            GridAi ai;
            if (GridTargetingAIs.TryGetValue(myGrid, out ai)) {

                for (int i = 0; i < focusPacket.EntityIds.Length; i++)
                {
                    var eId = focusPacket.EntityIds[i];
                    var focusTarget = MyEntities.GetEntityByIdOrDefault(eId);
                    if (focusTarget == null) return Error(data, Msg($"FocusTargetId: {eId}"));

                    ai.Focus.Target[i] = focusTarget;
                }
                data.Report.PacketValid = true;
            }
            else
                return Error(data, Msg($"GridAi not found, is marked:{myGrid.MarkedForClose}, has root:{GridToMasterAi.ContainsKey(myGrid)}"));

            return true;
        }

        private bool ClientClientMidUpdate(PacketObj data)
        {
            var packet = data.Packet;
            var midPacket = (ClientMIdUpdatePacket)packet;
            var ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
            var comp = ent?.Components.Get<WeaponComponent>();
            var myGrid = ent as MyCubeGrid;
            if (comp?.Ai == null && myGrid == null) return Error(data, Msg($"CompId: {packet.EntityId}", comp != null), Msg("Ai+Grid"));

            if (comp != null) {
                comp.MIds[(int)midPacket.MidType] = midPacket.MId;
                if (comp.GetSyncHash() != midPacket.HashCheck)
                    RequestCompSync(comp);

                data.Report.PacketValid = true;
            }
            else  {
                GridAi ai;
                if (GridTargetingAIs.TryGetValue(myGrid, out ai)) {
                    ai.UiMId = midPacket.MId;
                    data.Report.PacketValid = true;
                }
                else
                    return Error(data, Msg($"GridAi not found, is marked:{myGrid.MarkedForClose}, has root:{GridToMasterAi.ContainsKey(myGrid)}"));
            }

            return true;

        }
        private bool ClientFocusStates(PacketObj data)
        {
            var packet = data.Packet;
            var focusPacket = (FocusPacket)packet;
            var myGrid = MyEntities.GetEntityByIdOrDefault(packet.EntityId) as MyCubeGrid;
            if (myGrid == null) return Error(data, Msg($"GridId: {packet.EntityId}"));

            GridAi ai;
            if (GridTargetingAIs.TryGetValue(myGrid, out ai)) {

                var targetGrid = MyEntities.GetEntityByIdOrDefault(focusPacket.TargetId) as MyCubeGrid;
                switch (packet.PType) {

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
                data.Report.PacketValid = true;
            }
            else
                return Error(data, Msg($"GridAi not found, is marked:{myGrid.MarkedForClose}, has root:{GridToMasterAi.ContainsKey(myGrid)}"));

            return true;

        }
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
    }
}
