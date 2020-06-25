using System.Collections.Generic;
using Sandbox.Game.Entities;
using WeaponCore.Platform;
using WeaponCore.Support;
using static WeaponCore.Platform.Weapon;

namespace WeaponCore
{
    public partial class Session
    {
        private bool ServerCompStateUpdate(PacketObj data)
        {
            var packet = data.Packet;
            var statePacket = (StatePacket)packet;
            var ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
            var comp = ent?.Components.Get<WeaponComponent>();

            if (comp?.Ai == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return Error(data, Msg("Comp", comp != null), Msg("Ai", comp?.Ai != null), Msg("Ai", comp?.Platform.State == MyWeaponPlatform.PlatformState.Ready));
            if (statePacket.Data == null) return Error(data, Msg("Data"));

            if (statePacket.MId > comp.MIds[(int)packet.PType]) {
                comp.MIds[(int)packet.PType] = statePacket.MId;
                comp.Data.Repo.State.Sync(comp, statePacket.Data);
                PacketsToClient.Add(new PacketInfo { Entity = ent, Packet = statePacket });

                data.Report.PacketValid = true;
            }
            else {
                //SendMidResync(packet.PType, comp.MIds[(int)packet.PType], packet.SenderId, ent, comp);
                return Error(data, Msg($"Mid is old({statePacket.MId}[{comp.MIds[(int)packet.PType]}]), likely multiple clients attempting update"));
            }

            return true;
        }

        private bool ServerCompSettingsUpdate(PacketObj data)
        {
            var packet = data.Packet;
            var setPacket = (SettingPacket)packet;
            var ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
            var comp = ent?.Components.Get<WeaponComponent>();

            if (comp?.Ai == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return Error(data, Msg("Comp", comp != null), Msg("Ai", comp?.Ai != null), Msg("Ai", comp?.Platform.State == MyWeaponPlatform.PlatformState.Ready));
            if (setPacket.Data == null) return Error(data, Msg("Data"));

            if (setPacket.MId > comp.MIds[(int)packet.PType]) {
                comp.MIds[(int)packet.PType] = setPacket.MId;
                comp.Data.Repo.Set.Sync(comp, setPacket.Data);
                PacketsToClient.Add(new PacketInfo { Entity = ent, Packet = setPacket });

                data.Report.PacketValid = true;
            }
            else {
                //SendMidResync(packet.PType, comp.MIds[(int)packet.PType], packet.SenderId, ent, comp); // is this really required?
                return Error(data, Msg("Mid is old, likely multiple clients attempting update"));
            }

            return true;
        }

        private bool ServerClientMouseEvent(PacketObj data)
        {
            var packet = data.Packet;
            var inputPacket = (InputPacket)packet;
            var ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);

            if (ent == null) return Error(data, Msg("Entity"));
            if (inputPacket.Data == null) return Error(data, Msg("Data"));

            long playerId;
            if (SteamToPlayer.TryGetValue(packet.SenderId, out playerId))
            {
                if (PlayerMouseStates.ContainsKey(playerId))
                    PlayerMouseStates[playerId].Sync(inputPacket.Data);
                else
                    PlayerMouseStates[playerId] = new InputStateData(inputPacket.Data);

                PacketsToClient.Add(new PacketInfo { Entity = ent, Packet = inputPacket });

                data.Report.PacketValid = true;
            }
            else
                return Error(data, Msg("Player Not Found"));

            return true;
        }

        private bool ServerActiveControlUpdate(PacketObj data)
        {
            var packet = data.Packet;
            var dPacket = (BoolUpdatePacket)packet;
            var cube = MyEntities.GetEntityByIdOrDefault(packet.EntityId) as MyCubeBlock;

            if (cube == null) return Error(data, Msg("Cube"));

            long playerId;
            SteamToPlayer.TryGetValue(packet.SenderId, out playerId);

            UpdateActiveControlDictionary(cube, playerId, dPacket.Data);
            PacketsToClient.Add(new PacketInfo { Entity = cube, Packet = dPacket });
            data.Report.PacketValid = true;

            return true;
        }

        private bool ServerFakeTargetUpdate(PacketObj data)
        {
            var packet = data.Packet;
            var targetPacket = (FakeTargetPacket)packet;
            var myGrid = MyEntities.GetEntityByIdOrDefault(packet.EntityId) as MyCubeGrid;

            if (myGrid == null) return Error(data, Msg("Grid"));

            GridAi ai;
            long playerId;
            if (GridTargetingAIs.TryGetValue(myGrid, out ai) && SteamToPlayer.TryGetValue(packet.SenderId, out playerId))
            {
                PlayerDummyTargets[playerId].Update(targetPacket.Data, ai, null, true);
                data.Report.PacketValid = true;
            }
            else
                return Error(data, Msg($"GridAi not found, is marked:{myGrid.MarkedForClose}, has root:{GridToMasterAi.ContainsKey(myGrid)}"));

            return true;
        }

        private bool ServerGridSyncRequestUpdate(PacketObj data)
        {
            var packet = data.Packet;
            var myGrid = MyEntities.GetEntityByIdOrDefault(packet.EntityId) as MyCubeGrid;

            if (myGrid == null) return Error(data, Msg("Grid"));

            GridAi ai;
            if (GridTargetingAIs.TryGetValue(myGrid, out ai)) {

                if (ai.ControllingPlayers.Keys.Count > 0)
                {
                    var i = 0;
                    var playerToBlocks = new PlayerToBlock[ai.ControllingPlayers.Keys.Count];

                    foreach (var playerBlockPair in ai.ControllingPlayers)
                    {
                        playerToBlocks[i] = new PlayerToBlock
                        {
                            PlayerId = playerBlockPair.Key,
                            EntityId = playerBlockPair.Value.EntityId
                        };
                        i++;
                    }

                    PacketsToClient.Add(new PacketInfo
                    {

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
                }

                PacketsToClient.Add(new PacketInfo {

                    Entity = myGrid,
                    Packet = new MIdPacket {
                        EntityId = myGrid.EntityId,
                        SenderId = packet.SenderId,
                        PType = PacketType.GridAiUiMidUpdate,
                        MId = ai.UiMId,
                    },
                    SingleClient = true,
                });

                var gridPacket = new GridWeaponPacket {
                    EntityId = packet.EntityId,
                    SenderId = packet.SenderId,
                    PType = PacketType.WeaponSyncUpdate,
                    Data = new List<WeaponData>()
                };

                foreach (var cubeComp in ai.WeaponBase) {

                    var comp = cubeComp.Value;
                    if (comp.MyCube == null || comp.MyCube.MarkedForClose || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) continue;

                    for (int j = 0; j < comp.Platform.Weapons.Length; j++) {

                        var w = comp.Platform.Weapons[j];
                        if (comp.Data.Repo.WepVal.Targets == null || comp.Data.Repo.WepVal.Targets[j].State == TransferTarget.TargetInfo.Expired)
                            continue;

                        var weaponData = new WeaponData {
                            CompEntityId = comp.MyCube.EntityId,
                            SyncData = w.State.Sync,
                            //Timmings = w.Timings.SyncOffsetServer(Tick),
                            TargetData = comp.Data.Repo.WepVal.Targets[j],
                            WeaponRng = comp.Data.Repo.WepVal.WeaponRandom[j]
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

                var overrides = new OverRidesData[ai.BlockGroups.Values.Count];

                var c = 0;
                foreach (var group in ai.BlockGroups) {
                    overrides[c].Overrides = GetOverrides(ai, group.Key);
                    overrides[c].GroupName = group.Key;
                    c++;
                }

                if (overrides.Length > 0)
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

                long[] ids = new long[ai.Focus.Target.Length];
                var validFocus = false;
                for (int i = 0; i < ai.Focus.Target.Length; i++)
                {
                    ids[i] = ai.Focus.Target[i]?.EntityId ?? -1;
                    if (ids[i] != -1)
                        validFocus = true;
                }

                if (validFocus)
                {
                    var focusPacket = new GridFocusListPacket
                    {
                        EntityId = myGrid.EntityId,
                        SenderId = packet.SenderId,
                        PType = PacketType.GridFocusListSync,
                        EntityIds = ids,
                    };

                    PacketsToClient.Add(new PacketInfo
                    {
                        Entity = myGrid,
                        Packet = focusPacket,
                        SingleClient = true,
                    });
                }

                if (!PlayerEntityIdInRange.ContainsKey(packet.SenderId))
                    PlayerEntityIdInRange[packet.SenderId] = new HashSet<long>();

                PlayerEntityIdInRange[packet.SenderId].Add(packet.EntityId);

                data.Report.PacketValid = true;
            }
            else
                return Error(data, Msg($"GridAi not found, is marked:{myGrid.MarkedForClose}, has root:{GridToMasterAi.ContainsKey(myGrid)}"));

            return true;
        }

        private bool ServerReticleUpdate(PacketObj data)
        {
            var packet = data.Packet;
            var reticlePacket = (BoolUpdatePacket)packet;
            var ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
            var comp = ent?.Components.Get<WeaponComponent>();

            if (comp?.Ai == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return Error(data, Msg("Comp", comp != null), Msg("Ai", comp?.Ai != null), Msg("Ai", comp?.Platform.State == MyWeaponPlatform.PlatformState.Ready));

            if (DedicatedServer)
                comp.TrackReticle = reticlePacket.Data;

            comp.Data.Repo.State.OtherPlayerTrackingReticle = reticlePacket.Data;

            PacketsToClient.Add(new PacketInfo { Entity = ent, Packet = reticlePacket });

            data.Report.PacketValid = true;
            return true;
        }

        private bool ServerOverRidesUpdate(PacketObj data)
        {
            Log.Line($"ServerOverRidesUpdate0");
            var packet = data.Packet;
            var overRidesPacket = (OverRidesPacket)packet;
            var ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId, null, true);
            var comp = ent?.Components.Get<WeaponComponent>();
            var myGrid = ent as MyCubeGrid;

            if (comp?.Ai == null && myGrid == null) return Error(data, Msg("Comp", comp != null), Msg("Ai+Grid"));
            Log.Line($"ServerOverRidesUpdate1");

            if (comp?.Ai != null) {
                Log.Line($"ServerOverRidesUpdate Comp0");

                if (comp.MIds[(int)packet.PType] < overRidesPacket.MId) {
                    comp.MIds[(int)packet.PType] = overRidesPacket.MId;
                    Log.Line($"ServerOverRidesUpdate Comp1");

                    comp.Ai.ReScanBlockGroups();

                    GroupInfo group;
                    if (comp.Ai.BlockGroups.TryGetValue(overRidesPacket.GroupName, out group))
                    {
                        Log.Line($"ServerOverRidesUpdate Comp2");

                        group.RequestSetValue(comp, overRidesPacket.Setting, overRidesPacket.Value);
                        data.Report.PacketValid = true;
                    }
                    else Log.Line($"ServerOverRidesUpdate couldn't find group: {overRidesPacket.GroupName}");
                }
                else
                    return Error(data, Msg("Mid is old, likely multiple clients attempting update"));
            }
            else if (myGrid != null)
            {
                Log.Line($"ServerOverRidesUpdate myGrid0");
                GridAi ai;
                if (GridTargetingAIs.TryGetValue(myGrid, out ai)) {

                    Log.Line($"ServerOverRidesUpdate myGrid1");
                    if (ai.UiMId < overRidesPacket.MId) {
                        ai.UiMId = overRidesPacket.MId;
                        Log.Line($"ServerOverRidesUpdate myGrid2");

                        ai.ReScanBlockGroups();

                        GroupInfo groups;
                        if (ai.BlockGroups.TryGetValue(overRidesPacket.GroupName, out groups))
                        {
                            Log.Line($"ServerOverRidesUpdate myGrid3");
                            groups.RequestApplySettings(ai, overRidesPacket.Setting, overRidesPacket.Value, ai.Session);
                            data.Report.PacketValid = true;
                        }
                        else
                            return Error(data, Msg("Block group not found"));
                    }
                    else
                        return Error(data, Msg("Mid is old, likely multiple clients attempting update"));
                }
                else
                    return Error(data, Msg($"GridAi not found, is marked:{myGrid.MarkedForClose}, has root:{GridToMasterAi.ContainsKey(myGrid)}"));
            }
            /*
            if (comp != null) {

                if (comp.MIds[(int)packet.PType] < overRidesPacket.MId) {

                    comp.Set.Value.Overrides.Sync(overRidesPacket.Data);
                    comp.MIds[(int)packet.PType] = overRidesPacket.MId;

                    GroupInfo group;
                    if (!string.IsNullOrEmpty(comp.State.Value.CurrentBlockGroup) && comp.Ai.BlockGroups.TryGetValue(comp.State.Value.CurrentBlockGroup, out group)) {
                        comp.Ai.ScanBlockGroupSettings = true;
                        comp.Ai.GroupsToCheck.Add(group);
                    }
                    data.Report.PacketValid = true;
                }
                else {
                    SendMidResync(packet.PType, comp.MIds[(int)packet.PType], packet.SenderId, ent, comp); // is this really required?
                    return Error(data, Msg("Mid is old, likely multiple clients attempting update"));
                }
            }
            else {
                GridAi ai;
                if (GridTargetingAIs.TryGetValue(myGrid, out ai)) {

                    if (ai.UiMId < overRidesPacket.MId) {

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
                    else {
                        SendMidResync(packet.PType, ai.UiMId, packet.SenderId, myGrid, null); // is this really required?
                        return Error(data, Msg("Mid is old, likely multiple clients attempting update"));
                    }
                }
                else
                    return Error(data, Msg($"GridAi not found, is marked:{myGrid.MarkedForClose}, has root:{GridToMasterAi.ContainsKey(myGrid)}"));
            }

            if (data.Report.PacketValid) {
                PacketsToClient.Add(new PacketInfo {
                    Entity = ent,
                    Packet = overRidesPacket,
                });
            }
            */
            return true;
        }

        private bool ServerPlayerControlUpdate(PacketObj data)
        {
            var packet = data.Packet;
            var cPlayerPacket = (ControllingPlayerPacket)packet;
            var ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
            var comp = ent?.Components.Get<WeaponComponent>();

            if (comp?.Ai == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return Error(data, Msg("Comp", comp != null), Msg("Ai", comp?.Ai != null), Msg("Ai", comp?.Platform.State == MyWeaponPlatform.PlatformState.Ready));

            if (comp.MIds[(int)packet.PType] < cPlayerPacket.MId) {
                comp.Data.Repo.State.CurrentPlayerControl.Sync(cPlayerPacket.Data);
                comp.MIds[(int)packet.PType] = cPlayerPacket.MId;
                data.Report.PacketValid = true;
                PacketsToClient.Add(new PacketInfo { Entity = comp.MyCube, Packet = cPlayerPacket });
            }
            else {
                //SendMidResync(packet.PType, comp.MIds[(int)packet.PType], packet.SenderId, ent, comp);
                return Error(data, Msg("Mid is old, likely multiple clients attempting update"));
            }

            return true;
        }

        private bool ServerWeaponUpdateRequest(PacketObj data)
        {
            var packet = data.Packet;
            var targetRequestPacket = (RequestTargetsPacket)packet;
            var myGrid = MyEntities.GetEntityByIdOrDefault(packet.EntityId) as MyCubeGrid;

            if (myGrid == null) return Error(data, Msg("Grid"));

            GridAi ai;
            if (GridTargetingAIs.TryGetValue(myGrid, out ai)) {

                var gridPacket = new GridWeaponPacket {
                    EntityId = packet.EntityId,
                    SenderId = packet.SenderId,
                    PType = PacketType.WeaponSyncUpdate,
                    Data = new List<WeaponData>()
                };

                for (int i = 0; i < targetRequestPacket.Comps.Count; i++) {

                    var compId = targetRequestPacket.Comps[i];
                    var compCube = MyEntities.GetEntityByIdOrDefault(compId) as MyCubeBlock;
                    if (compCube == null) return Error(data, Msg("compCube"));

                    WeaponComponent comp;
                    if (!ai.WeaponBase.TryGetValue(compCube, out comp) || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready)
                        continue;

                    for (int j = 0; j < comp.Platform.Weapons.Length; j++) {

                        var w = comp.Platform.Weapons[j];
                        var weaponData = new WeaponData {
                            CompEntityId = compId,
                            SyncData = w.State.Sync,
                            //Timmings = w.Timings.SyncOffsetServer(Tick),
                            TargetData = comp.Data.Repo.WepVal.Targets[j],
                            WeaponRng = comp.Data.Repo.WepVal.WeaponRandom[j]
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

                data.Report.PacketValid = true;
            }
            else
                return Error(data, Msg($"GridAi not found, is marked:{myGrid.MarkedForClose}, has root:{GridToMasterAi.ContainsKey(myGrid)}"));

            return true;
        }

        private bool ServerClientEntityClosed(PacketObj data)
        {
            var packet = data.Packet;
            if (PlayerEntityIdInRange.ContainsKey(packet.SenderId))
                PlayerEntityIdInRange[packet.SenderId].Remove(packet.EntityId);
            else
                return Error(data, Msg("SenderId not found"));

            data.Report.PacketValid = true;
            return true;
        }

        private bool ServerRequestMouseStates(PacketObj data)
        {
            var packet = data.Packet;
            var mouseUpdatePacket = new MouseInputSyncPacket {
                EntityId = -1,
                SenderId = packet.SenderId,
                PType = PacketType.FullMouseUpdate,
                Data = new PlayerMouseData[PlayerMouseStates.Count],
            };

            var c = 0;
            foreach (var playerMouse in PlayerMouseStates) {
                mouseUpdatePacket.Data[c] = new PlayerMouseData {
                    PlayerId = playerMouse.Key,
                    MouseStateData = playerMouse.Value
                };
            }

            if (PlayerMouseStates.Count > 0)
                PacketsToClient.Add(new PacketInfo {
                    Entity = null,
                    Packet = mouseUpdatePacket,
                    SingleClient = true,
                });

            data.Report.PacketValid = true;

            return true;
        }

        private bool ServerCompToolbarShootState(PacketObj data)
        {
            var packet = data.Packet;
            var shootStatePacket = (ShootStatePacket)packet;
            var ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
            var comp = ent?.Components.Get<WeaponComponent>();

            if (comp?.Ai == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return Error(data, Msg("Comp", comp != null), Msg("Ai", comp?.Ai != null), Msg("Ai", comp?.Platform.State == MyWeaponPlatform.PlatformState.Ready));

            if (comp.MIds[(int)packet.PType] < shootStatePacket.MId) {

                comp.MIds[(int)packet.PType] = shootStatePacket.MId;

                comp.RequestShootUpdate(shootStatePacket.Action, shootStatePacket.PlayerId);
                data.Report.PacketValid = true;
            }
            else {
                return Error(data, Msg("Mid is old, likely multiple clients attempting update"));
            }
            return true;
        }

        private bool ServerCycleAmmo(PacketObj data)
        {
            var packet = data.Packet;
            var cyclePacket = (CycleAmmoPacket)packet;
            var ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
            var comp = ent?.Components.Get<WeaponComponent>();

            if (comp?.Ai == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return Error(data, Msg("Comp", comp != null), Msg("Ai", comp?.Ai != null), Msg("Ai", comp?.Platform.State == MyWeaponPlatform.PlatformState.Ready));

            if (cyclePacket.MId > comp.MIds[(int)packet.PType]) {

                comp.MIds[(int)packet.PType] = cyclePacket.MId;

                var weapon = comp.Platform.Weapons[cyclePacket.WeaponId];

                weapon.Set.AmmoTypeId = cyclePacket.AmmoId;
                weapon.ChangeAmmo(weapon.System.AmmoTypes[cyclePacket.AmmoId]);

                PacketsToClient.Add(new PacketInfo {
                    Entity = ent,
                    Packet = cyclePacket,
                });

                data.Report.PacketValid = true;
            }
            else {
                //SendMidResync(packet.PType, comp.MIds[(int)packet.PType], packet.SenderId, ent, comp);
                return Error(data, Msg("Mid is old, likely multiple clients attempting update"));
            }

            return true;
        }

        private bool ServerRescanGroupRequest(PacketObj data)
        {
            var packet = data.Packet;
            var myGrid = MyEntities.GetEntityByIdOrDefault(packet.EntityId) as MyCubeGrid;

            if (myGrid == null) return Error(data, Msg("Grid"));

            GridAi ai;
            if (GridTargetingAIs.TryGetValue(myGrid, out ai))
                ai.ReScanBlockGroups(true);

            PacketsToClient.Add(new PacketInfo { Entity = myGrid, Packet = packet });

            data.Report.PacketValid = true;

            return true;
        }

        private bool ServerFixedWeaponHitEvent(PacketObj data)
        {
            var packet = data.Packet;
            var hitPacket = (FixedWeaponHitPacket)packet;

            var ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
            var comp = ent?.Components.Get<WeaponComponent>();

            if (comp?.Ai == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return Error(data, Msg("Comp", comp != null), Msg("Ai", comp?.Ai != null), Msg("Ai", comp?.Platform.State == MyWeaponPlatform.PlatformState.Ready));

            var weapon = comp.Platform.Weapons[hitPacket.WeaponId];
            var targetEnt = MyEntities.GetEntityByIdOrDefault(hitPacket.HitEnt);

            if (targetEnt == null) return Error(data, Msg("TargetEnt"));

            var origin = targetEnt.PositionComp.WorldMatrixRef.Translation - hitPacket.HitOffset;
            var direction = hitPacket.Velocity;
            direction.Normalize();

            Projectiles.NewProjectiles.Add(new NewProjectile
            {
                AmmoDef = weapon.System.AmmoTypes[hitPacket.AmmoIndex].AmmoDef, Muzzle = weapon.Muzzles[hitPacket.MuzzleId], Weapon = weapon, TargetEnt = targetEnt, Origin = origin, OriginUp = hitPacket.Up, Direction = direction, Velocity = hitPacket.Velocity, MaxTrajectory = hitPacket.MaxTrajectory, Type = NewProjectile.Kind.Client
            });
            //CreateFixedWeaponProjectile(weapon, targetEnt, origin, direction, hitPacket.Velocity, hitPacket.Up, hitPacket.MuzzleId, weapon.System.AmmoTypes[hitPacket.AmmoIndex].AmmoDef, hitPacket.MaxTrajectory);

            data.Report.PacketValid = true;
            return true;
        }

        private bool ServerCompSyncRequest(PacketObj data)
        {
            var packet = data.Packet;
            var ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
            var comp = ent?.Components.Get<WeaponComponent>();

            if (comp?.Ai == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return Error(data, Msg("Comp", comp != null), Msg("Ai", comp?.Ai != null), Msg("Ai", comp?.Platform.State == MyWeaponPlatform.PlatformState.Ready));

            SendCompSettingUpdate(comp);
            SendCompStateUpdate(comp);
            data.Report.PacketValid = true;
            return true;
        }

        private bool ServerFocusUpdate(PacketObj data)
        {
            var packet = data.Packet;
            var focusPacket = (FocusPacket)packet;
            var myGrid = MyEntities.GetEntityByIdOrDefault(packet.EntityId) as MyCubeGrid;

            if (myGrid == null) return Error(data, Msg("Grid"));

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

                PacketsToClient.Add(new PacketInfo { Entity = myGrid, Packet = focusPacket });
                data.Report.PacketValid = true;
            }
            else
                return Error(data, Msg($"GridAi not found, is marked:{myGrid.MarkedForClose}, has root:{GridToMasterAi.ContainsKey(myGrid)}"));

            return true;
        }
        private bool ServerRequestReport(PacketObj data)
        {
            var packet = data.Packet;
            
            var cube = MyEntities.GetEntityByIdOrDefault(packet.EntityId) as MyCubeBlock;
            if (cube == null) return Error(data, Msg("Cube"));
            
            var reportData = ProblemRep.PullData(cube);
            if (reportData == null) return Error(data, Msg("RequestReport"));
            
            ProblemRep.NetworkTransfer(false, packet.SenderId, reportData);
            data.Report.PacketValid = true;

            return true;
        }

        private bool ServerTerminalMonitor(PacketObj data)
        {
            var packet = data.Packet;
            var terminalMonPacket = (TerminalMonitorPacket)packet;
            var ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
            var comp = ent?.Components.Get<WeaponComponent>();

            if (comp?.Ai == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return Error(data, Msg("Comp", comp != null), Msg("Ai", comp?.Ai != null), Msg("Ai", comp?.Platform.State == MyWeaponPlatform.PlatformState.Ready));

            if (terminalMonPacket.MId > comp.MIds[(int) packet.PType]) {

                if (terminalMonPacket.State == TerminalMonitorPacket.Change.Update) {
                    comp.MIds[(int)packet.PType] = terminalMonPacket.MId;
                    TerminalMon.ServerUpdate(comp);
                    //Log.Line("Terminal Update");
                }
                else if (terminalMonPacket.State == TerminalMonitorPacket.Change.Clean) {
                    TerminalMon.ServerClean(comp);
                    //Log.Line("Terminal Clean");
                }

            }

            data.Report.PacketValid = true;

            return true;
        }
    }
}
