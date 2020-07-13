using Sandbox.Game.Entities;
using WeaponCore.Platform;
using WeaponCore.Support;

namespace WeaponCore
{
    public partial class Session
    {
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
                uint[] mIds;
                if (PlayerMIds.TryGetValue(packet.SenderId, out mIds) && mIds[(int)packet.PType] < packet.MId)  {
                    mIds[(int)packet.PType] = packet.MId;

                    if (PlayerMouseStates.ContainsKey(playerId))
                        PlayerMouseStates[playerId].Sync(inputPacket.Data);
                    else
                        PlayerMouseStates[playerId] = new InputStateData(inputPacket.Data);

                    PacketsToClient.Add(new PacketInfo { Entity = ent, Packet = inputPacket });

                    data.Report.PacketValid = true;
                }
                else Log.Line($"ServerClientMouseEvent: MidsHasSenderId:{PlayerMIds.ContainsKey(packet.SenderId)} - midsNull:{mIds == null} - senderId:{packet.SenderId}");
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

            GridAi ai;
            long playerId = 0;
            if (GridToMasterAi.TryGetValue(cube.CubeGrid, out ai) && SteamToPlayer.TryGetValue(packet.SenderId, out playerId))
            {
                uint[] mIds;
                if (PlayerMIds.TryGetValue(packet.SenderId, out mIds) && mIds[(int) packet.PType] < packet.MId)  {
                    mIds[(int)packet.PType] = packet.MId;

                    ai.Construct.UpdateConstructsPlayers(cube, playerId, dPacket.Data);
                    data.Report.PacketValid = true;
                }
                else Log.Line($"ServerActiveControlUpdate: MidsHasSenderId:{PlayerMIds.ContainsKey(packet.SenderId)} - midsNull:{mIds == null} - senderId:{packet.SenderId}");
            }
            else Log.Line($"ServerActiveControlUpdate: ai:{ai == null} - playerId:{playerId}");

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
                uint[] mIds;
                if (PlayerMIds.TryGetValue(packet.SenderId, out mIds) && mIds[(int) packet.PType] < packet.MId)  {
                    mIds[(int) packet.PType] = packet.MId;

                    PlayerDummyTargets[playerId].Update(targetPacket.Pos, ai, null, targetPacket.TargetId);
                    PacketsToClient.Add(new PacketInfo { Entity = myGrid, Packet = targetPacket });

                    data.Report.PacketValid = true;
                }
                else Log.Line($"ServerFakeTargetUpdate: MidsHasSenderId:{PlayerMIds.ContainsKey(packet.SenderId)} - midsNull:{mIds == null} - senderId:{packet.SenderId}");
            }
            else
                return Error(data, Msg($"GridAi not found, is marked:{myGrid.MarkedForClose}, has root:{GridToMasterAi.ContainsKey(myGrid)}"));

            return true;
        }

        private bool ServerAmmoCycleRequest(PacketObj data)
        {
            var packet = data.Packet;
            var cyclePacket = (AmmoCycleRequestPacket)packet;
            var ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
            var comp = ent?.Components.Get<WeaponComponent>();

            if (comp?.Ai == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return Error(data, Msg("Comp", comp != null), Msg("Ai", comp?.Ai != null), Msg("Ai", comp?.Platform.State == MyWeaponPlatform.PlatformState.Ready));

            uint[] mIds;
            if (PlayerMIds.TryGetValue(packet.SenderId, out mIds) && mIds[(int) packet.PType] < packet.MId)  {
                mIds[(int) packet.PType] = packet.MId;
                comp.Platform.Weapons[cyclePacket.WeaponId].ChangeAmmo(cyclePacket.NewAmmoId);
                data.Report.PacketValid = true;
            }
            else Log.Line($"ServerAmmoCycleRequest: MidsHasSenderId:{PlayerMIds.ContainsKey(packet.SenderId)} - midsNull:{mIds == null} - senderId:{packet.SenderId}");

            return true;
        }

        private bool ServerPlayerControlRequest(PacketObj data)
        {
            var packet = data.Packet;
            var controlPacket = (PlayerControlRequestPacket)packet;
            var ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
            var comp = ent?.Components.Get<WeaponComponent>();

            if (comp?.Ai == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return Error(data, Msg("Comp", comp != null), Msg("Ai", comp?.Ai != null), Msg("Ai", comp?.Platform.State == MyWeaponPlatform.PlatformState.Ready));

            uint[] mIds;
            if (PlayerMIds.TryGetValue(packet.SenderId, out mIds) && mIds[(int)packet.PType] < packet.MId)  {
                mIds[(int)packet.PType] = packet.MId;

                comp.Data.Repo.State.PlayerId = controlPacket.PlayerId;
                comp.Data.Repo.State.Control = controlPacket.Mode;

                SendCompData(comp);
                data.Report.PacketValid = true;
            }
            else Log.Line($"ServerPlayerControlRequest: MidsHasSenderId:{PlayerMIds.ContainsKey(packet.SenderId)} - midsNull:{mIds == null} - senderId:{packet.SenderId}");

            return true;
        }

        private bool ServerReticleUpdate(PacketObj data)
        {
            var packet = data.Packet;
            var reticlePacket = (BoolUpdatePacket)packet;
            var ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
            var comp = ent?.Components.Get<WeaponComponent>();

            if (comp?.Ai == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return Error(data, Msg("Comp", comp != null), Msg("Ai", comp?.Ai != null), Msg("Ai", comp?.Platform.State == MyWeaponPlatform.PlatformState.Ready));

            uint[] mIds;
            if (PlayerMIds.TryGetValue(packet.SenderId, out mIds) && mIds[(int) packet.PType] < packet.MId)  {
                mIds[(int) packet.PType] = packet.MId;

                comp.Data.Repo.State.TrackingReticle = reticlePacket.Data;
                SendCompState(comp, PacketType.CompState);

                data.Report.PacketValid = true;
            }
            else Log.Line($"ServerReticleUpdate: MidsHasSenderId:{PlayerMIds.ContainsKey(packet.SenderId)} - midsNull:{mIds == null} - senderId:{packet.SenderId}");

            return true;
        }

        private bool ServerOverRidesUpdate(PacketObj data)
        {
            var packet = data.Packet;
            var overRidesPacket = (OverRidesPacket)packet;
            var ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId, null, true);
            var comp = ent?.Components.Get<WeaponComponent>();
            var myGrid = ent as MyCubeGrid;

            if (comp?.Ai == null && myGrid == null) return Error(data, Msg("Comp", comp != null), Msg("Ai+Grid"));

            if (comp?.Ai != null)
            {
                uint[] mIds;
                if (PlayerMIds.TryGetValue(packet.SenderId, out mIds) && mIds[(int)packet.PType] < packet.MId)  {
                    mIds[(int)packet.PType] = packet.MId;

                    var rootConstruct = comp.Ai.Construct.RootAi.Construct;
                    rootConstruct.UpdateConstruct(GridAi.Constructs.UpdateType.BlockScan);

                    GroupInfo group;
                    if (rootConstruct.Data.Repo.BlockGroups.TryGetValue(overRidesPacket.GroupName, out group))
                    {
                        Log.Line($"ServerOverRidesUpdate Comp2");

                        group.RequestSetValue(comp, overRidesPacket.Setting, overRidesPacket.Value, SteamToPlayer[overRidesPacket.SenderId]);
                        data.Report.PacketValid = true;
                    }
                    else Log.Line($"ServerOverRidesUpdate couldn't find group: {overRidesPacket.GroupName}");
                }
                else Log.Line($"ServerOverRidesUpdate: MidsHasSenderId:{PlayerMIds.ContainsKey(packet.SenderId)} - midsNull:{mIds == null} - senderId:{packet.SenderId}");
            }
            else if (myGrid != null)
            {
                GridAi ai;
                if (GridTargetingAIs.TryGetValue(myGrid, out ai))  {

                    uint[] mIds;
                    if (PlayerMIds.TryGetValue(packet.SenderId, out mIds) && mIds[(int)packet.PType] < packet.MId)  {
                        mIds[(int)packet.PType] = packet.MId;

                        var rootConstruct = ai.Construct.RootAi.Construct;

                        rootConstruct.UpdateConstruct(GridAi.Constructs.UpdateType.BlockScan);

                        GroupInfo groups;
                        if (rootConstruct.Data.Repo.BlockGroups.TryGetValue(overRidesPacket.GroupName, out groups))
                        {
                            Log.Line($"ServerOverRidesUpdate myGrid3");
                            groups.RequestApplySettings(ai, overRidesPacket.Setting, overRidesPacket.Value, ai.Session, SteamToPlayer[overRidesPacket.SenderId]);
                            data.Report.PacketValid = true;
                        }
                        else
                            return Error(data, Msg("Block group not found"));
                    }
                    else Log.Line($"ServerOverRidesUpdate: MidsHasSenderId:{PlayerMIds.ContainsKey(packet.SenderId)} - midsNull:{mIds == null} - senderId:{packet.SenderId}");
                }
                else
                    return Error(data, Msg($"GridAi not found, is marked:{myGrid.MarkedForClose}, has root:{GridToMasterAi.ContainsKey(myGrid)}"));
            }
            return true;
        }

        private bool ServerClientAiExists(PacketObj data)
        {
            var packet = data.Packet;
            uint[] mIds;
            if (PlayerMIds.TryGetValue(packet.SenderId, out mIds) && mIds[(int) packet.PType] < packet.MId) {
                mIds[(int)packet.PType] = packet.MId;

                if (packet.PType == PacketType.ClientAiRemove && PlayerEntityIdInRange.ContainsKey(packet.SenderId))
                    PlayerEntityIdInRange[packet.SenderId].Remove(packet.EntityId);
                else if ((packet.PType == PacketType.ClientAiAdd))
                {
                    PlayerEntityIdInRange[packet.SenderId].Add(packet.EntityId);
                }
                else return Error(data, Msg("SenderId not found"));
                
                data.Report.PacketValid = true;
            }
            else Log.Line($"ServerClientAiExists: MidsHasSenderId:{PlayerMIds.ContainsKey(packet.SenderId)} - midsNull:{mIds == null} - senderId:{packet.SenderId}");


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

        private bool ServerRequestShootUpdate(PacketObj data)
        {
            var packet = data.Packet;
            var shootStatePacket = (ShootStatePacket)packet;
            var ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
            var comp = ent?.Components.Get<WeaponComponent>();

            if (comp?.Ai == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return Error(data, Msg("Comp", comp != null), Msg("Ai", comp?.Ai != null), Msg("Ai", comp?.Platform.State == MyWeaponPlatform.PlatformState.Ready));

            uint[] mIds;
            if (PlayerMIds.TryGetValue(packet.SenderId, out mIds) && mIds[(int)packet.PType] < packet.MId)  {
                mIds[(int)packet.PType] = packet.MId;

                comp.RequestShootUpdate(shootStatePacket.Action, shootStatePacket.PlayerId);
                data.Report.PacketValid = true;
            }
            else Log.Line($"ServerRequestShootUpdate: MidsHasSenderId:{PlayerMIds.ContainsKey(packet.SenderId)} - midsNull:{mIds == null} - senderId:{packet.SenderId}");


            return true;
        }

        private bool ServerRescanGroupRequest(PacketObj data)
        {
            var packet = data.Packet;
            var myGrid = MyEntities.GetEntityByIdOrDefault(packet.EntityId) as MyCubeGrid;

            if (myGrid == null) return Error(data, Msg("Grid"));

            GridAi ai;
            if (GridToMasterAi.TryGetValue(myGrid, out ai)) {

                uint[] mIds;
                if (PlayerMIds.TryGetValue(packet.SenderId, out mIds) && mIds[(int)packet.PType] < packet.MId)  {
                    mIds[(int)packet.PType] = packet.MId;

                    ai.Construct.GroupRefresh(ai);
                    data.Report.PacketValid = true;
                }
                else Log.Line($"ServerRescanGroupRequest: MidsHasSenderId:{PlayerMIds.ContainsKey(packet.SenderId)} - midsNull:{mIds == null} - senderId:{packet.SenderId}");

            }

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

        private bool ServerFocusUpdate(PacketObj data)
        {
            var packet = data.Packet;
            var focusPacket = (FocusPacket)packet;
            var myGrid = MyEntities.GetEntityByIdOrDefault(packet.EntityId) as MyCubeGrid;

            if (myGrid == null) return Error(data, Msg("Grid"));

            GridAi ai;
            if (GridToMasterAi.TryGetValue(myGrid, out ai)) {

                var targetGrid = MyEntities.GetEntityByIdOrDefault(focusPacket.TargetId) as MyCubeGrid;

                switch (packet.PType) {
                    case PacketType.FocusUpdate:
                        if (targetGrid != null)
                            ai.Construct.Focus.ServerAddFocus(targetGrid, ai);
                        break;
                    case PacketType.NextActiveUpdate:
                        ai.Construct.Focus.ServerNextActive(focusPacket.AddSecondary, ai);
                        break;
                    case PacketType.ReleaseActiveUpdate:
                        ai.Construct.Focus.RequestReleaseActive(ai);
                        break;
                }

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

            uint[] mIds;
            if (PlayerMIds.TryGetValue(packet.SenderId, out mIds) && mIds[(int)packet.PType] < packet.MId) {
                mIds[(int)packet.PType] = packet.MId;

                if (terminalMonPacket.State == TerminalMonitorPacket.Change.Update)
                    TerminalMon.ServerUpdate(comp);
                else if (terminalMonPacket.State == TerminalMonitorPacket.Change.Clean)
                    TerminalMon.ServerClean(comp);

                data.Report.PacketValid = true;
            }
            else Log.Line($"ServerTerminalMonitor: MidsHasSenderId:{PlayerMIds.ContainsKey(packet.SenderId)} - midsNull:{mIds == null} - senderId:{packet.SenderId}");

            return true;
        }

        private bool ServerSendSingleShot(PacketObj data)
        {
            var packet = data.Packet;
            var ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
            var comp = ent?.Components.Get<WeaponComponent>();

            if (comp?.Ai == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return Error(data, Msg("Comp", comp != null), Msg("Ai", comp?.Ai != null), Msg("Ai", comp?.Platform.State == MyWeaponPlatform.PlatformState.Ready));

            PacketsToClient.Add(new PacketInfo { Packet = packet });

            data.Report.PacketValid = true;
            return true;
        }
    }
}
