using System;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.Entity;
using VRageMath;
using WeaponCore.Platform;
using WeaponCore.Support;
using static WeaponCore.Platform.Weapon;

namespace WeaponCore
{
    public partial class Session
    {
        #region Network sync
        internal void PacketizeToClientsInRange(MyEntity block, Packet packet)
        {
            try
            {
                var bytes = MyAPIGateway.Utilities.SerializeToBinary(packet);
                foreach (var p in Players.Values)
                {
                    var id = p.SteamUserId;
                    if (id != packet.SenderId && (Vector3D.DistanceSquared(p.GetPosition(), block.PositionComp.WorldAABB.Center) <= SyncBufferedDistSqr) || block == null)
                        MyAPIGateway.Multiplayer.SendMessageTo(ClientPacketId, bytes, p.SteamUserId);
                }
            }
            catch (Exception ex) { Log.Line($"Exception in PacketizeToClientsInRange: {ex}"); }
        }

        internal void SendPacketToServer(Packet packet)
        {
            if (!IsClient) return;

            byte[] bytes = MyAPIGateway.Utilities.SerializeToBinary(packet);

            MyAPIGateway.Multiplayer.SendMessageToServer(ServerPacketId, bytes);
        }

        private void ClientReceivedPacket(byte[] rawData)
        {
            try
            {
                var packet = MyAPIGateway.Utilities.SerializeFromBinary<Packet>(rawData);
                var ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
                var comp = ent?.Components.Get<WeaponComponent>();
                if (ent != null)
                {
                    switch (packet.PType)
                    {
                        case PacketType.CompStateUpdate:
                            if (comp == null) return;

                            var statePacket = packet as StatePacket;
                            comp.State.Value = statePacket.Data;

                            for (int i = 0; i < comp.Platform.Weapons.Length; i++)
                            {
                                var w = comp.Platform.Weapons[i];
                                w.State = comp.State.Value.Weapons[w.WeaponId];
                            }

                            break;
                        case PacketType.CompSettingsUpdate:
                            if (comp == null) return;

                            var setPacket = packet as SettingPacket;
                            comp.Set.Value = setPacket.Data;

                            for (int i = 0; i < comp.Platform.Weapons.Length; i++)
                            {
                                var w = comp.Platform.Weapons[i];
                                w.Set = comp.Set.Value.Weapons[w.WeaponId];
                            }

                            break;
                        case PacketType.TargetUpdate:
                            {
                                var targetPacket = packet as TargetPacket;

                                if (comp != null && targetPacket != null && targetPacket.TargetData != null)
                                {
                                    var syncTarget = targetPacket.TargetData;
                                    var weaponData = targetPacket.WeaponData;
                                    var wid = syncTarget.WeaponId;
                                    var weapon = comp.Platform.Weapons[wid];
                                    var timings = targetPacket.Timmings.SyncOffsetClient(Tick);

                                    SyncWeapon(weapon, timings, ref weaponData);
                                    syncTarget.SyncTarget(weapon.Target);
                                }
                                else
                                {
                                    var myGrid = ent as MyCubeGrid;
                                    GridAi ai;
                                    if (myGrid != null && GridTargetingAIs.TryGetValue(myGrid, out ai))
                                    {
                                        var target = targetPacket.TargetData;
                                        var targetGrid = MyEntities.GetEntityByIdOrDefault(target.EntityId) as MyCubeGrid;

                                        if (targetGrid != null)
                                        {
                                            ai.Focus.AddFocus(targetGrid, ai);
                                            PacketizeToClientsInRange(myGrid, packet);
                                        }
                                    }
                                }

                                break;
                            }
                        case PacketType.FakeTargetUpdate:
                            {

                                var myGrid = MyEntities.GetEntityByIdOrDefault(packet.EntityId) as MyCubeGrid;
                                var targetPacket = packet as FakeTargetPacket;

                                GridAi ai;

                                if (myGrid != null && GridTargetingAIs.TryGetValue(myGrid, out ai))
                                {
                                    ai.DummyTarget.TransferFrom(targetPacket.Data);
                                    PacketizeToClientsInRange(myGrid, packet);
                                }

                                break;
                            }

                        case PacketType.WeaponSync:
                            var syncPacket = packet as WeaponSyncPacket;

                            if (comp != null && syncPacket != null)
                            {
                                var weaponData = syncPacket.WeaponData;
                                var wid = weaponData.WeaponId;
                                var weapon = comp.Platform.Weapons[wid];
                                var timings = syncPacket.Timmings.SyncOffsetClient(Tick);

                                SyncWeapon(weapon, timings, ref weaponData);
                            }
                                break;

                        case PacketType.PlayerIdUpdate:
                            {
                                var updatePacket = packet as LookupUpdatePacket;

                                if (updatePacket == null) return;
                                if (updatePacket.Data) //update/add
                                {
                                    SteamToPlayer[updatePacket.SenderId] = updatePacket.EntityId;
                                    MouseState ms;
                                    if (!PlayerMouseStates.TryGetValue(updatePacket.EntityId, out ms))
                                        PlayerMouseStates[updatePacket.EntityId] = new MouseState();
                                }
                                else //remove
                                {
                                    long player;
                                    SteamToPlayer.TryRemove(updatePacket.SenderId, out player);
                                    PlayerMouseStates.Remove(player);
                                }
                                break;
                            }
                        case PacketType.ClientMouseEvent:
                            var mousePacket = packet as MouseInputPacket;
                            long playerId;
                            if (SteamToPlayer.TryGetValue(packet.SenderId, out playerId))
                                PlayerMouseStates[playerId] = mousePacket.Data;

                            break;
                        case PacketType.ActiveControlUpdate:
                            {
                                var block = MyEntities.GetEntityByIdOrDefault(packet.EntityId) as MyCubeBlock;
                                var grid = block?.CubeGrid as MyCubeGrid;
                                var updatePacket = packet as LookupUpdatePacket;

                                if (block == null || grid == null || updatePacket == null) return;
                                GridAi trackingAi;
                                if (updatePacket.Data) //update/add
                                {
                                    if (GridTargetingAIs.TryGetValue(grid, out trackingAi) && SteamToPlayer.TryGetValue(packet.SenderId, out playerId))
                                        trackingAi.ControllingPlayers[playerId] = block;
                                }
                                else //remove
                                {
                                    if (GridTargetingAIs.TryGetValue(grid, out trackingAi) && SteamToPlayer.TryGetValue(packet.SenderId, out playerId))
                                        trackingAi.ControllingPlayers.TryGetValue(playerId, out block);
                                }
                                break;
                            }
                    }
                }
            }
            catch (Exception ex) { Log.Line($"Exception in ReceivedPacket: {ex}"); }
        }

        private void ServerReceivedPacket(byte[] rawData)
        {
            try
            {
                var packet = MyAPIGateway.Utilities.SerializeFromBinary<Packet>(rawData);
                MyEntity ent; // not inited here to avoid extras calls unless needed
                WeaponComponent comp; // not inited here to avoid extras calls unless needed
                long playerId = 0;

                switch (packet.PType)
                {
                    case PacketType.CompStateUpdate:
                        var statePacket = packet as StatePacket;
                        ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
                        comp = ent?.Components.Get<WeaponComponent>();

                        if (comp == null || statePacket == null) return;

                        if (statePacket.Data.MId > comp.State.Value.MId)
                        {
                            comp.State.Value = statePacket.Data;

                            for (int i = 0; i < comp.Platform.Weapons.Length; i++)
                            {
                                var w = comp.Platform.Weapons[i];
                                w.State = comp.State.Value.Weapons[w.WeaponId];
                            }
                            PacketizeToClientsInRange(ent, packet);
                        }
                        break;

                    case PacketType.CompSettingsUpdate:
                        var setPacket = packet as SettingPacket;
                        ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
                        comp = ent?.Components.Get<WeaponComponent>();

                        //Log.Line($"comp not null: {comp != null} setPacket not null: {setPacket != null}");
                        if (comp == null || setPacket == null) return;

                        if (setPacket.Data.MId > comp.Set.Value.MId)
                        {
                            comp.Set.Value = setPacket.Data;
                            for (int i = 0; i < comp.Platform.Weapons.Length; i++)
                            {
                                var w = comp.Platform.Weapons[i];
                                w.Set = comp.Set.Value.Weapons[w.WeaponId];
                            }
                            PacketizeToClientsInRange(ent, packet);
                        }
                        break;

                    case PacketType.ClientMouseEvent:
                        var mousePacket = packet as MouseInputPacket;
                        if (SteamToPlayer.TryGetValue(packet.SenderId, out playerId))
                        {
                            PlayerMouseStates[playerId] = mousePacket.Data;
                            PacketizeToClientsInRange(null, mousePacket);
                        }

                        break;

                    case PacketType.ActiveControlUpdate:
                        var block = MyEntities.GetEntityByIdOrDefault(packet.EntityId) as MyCubeBlock;
                        var grid = block?.CubeGrid as MyCubeGrid;
                        var updatePacket = packet as LookupUpdatePacket;

                        if (block == null || grid == null || updatePacket == null) return;
                        GridAi trackingAi;
                        if (updatePacket.Data) //update/add
                        {
                            if (GridTargetingAIs.TryGetValue(grid, out trackingAi) && SteamToPlayer.TryGetValue(packet.SenderId, out playerId))
                                trackingAi.ControllingPlayers[playerId] = block;
                        }
                        else //remove
                        {
                            if (GridTargetingAIs.TryGetValue(grid, out trackingAi) && SteamToPlayer.TryGetValue(packet.SenderId, out playerId))
                                trackingAi.ControllingPlayers.TryGetValue(playerId, out block);
                        }

                        PacketizeToClientsInRange(block, updatePacket);
                        break;
                    case PacketType.TargetUpdate:
                        {

                            var myGrid = MyEntities.GetEntityByIdOrDefault(packet.EntityId) as MyCubeGrid;
                            var targetPacket = packet as TargetPacket;

                            GridAi ai;

                            if (myGrid != null && GridTargetingAIs.TryGetValue(myGrid, out ai))
                            {
                                var target = targetPacket.TargetData;
                                var targetGrid = MyEntities.GetEntityByIdOrDefault(target.EntityId) as MyCubeGrid;

                                if (targetGrid != null)
                                {
                                    ai.Focus.AddFocus(targetGrid, ai);
                                    PacketizeToClientsInRange(myGrid, packet);
                                }
                            }

                            break;
                        }
                    case PacketType.FakeTargetUpdate:
                        {

                            var myGrid = MyEntities.GetEntityByIdOrDefault(packet.EntityId) as MyCubeGrid;
                            var targetPacket = packet as FakeTargetPacket;

                            GridAi ai;

                            if (myGrid != null && GridTargetingAIs.TryGetValue(myGrid, out ai))
                            {
                                ai.DummyTarget.TransferFrom(targetPacket.Data);
                                PacketizeToClientsInRange(myGrid, packet);
                            }

                            break;
                        }
                }
            }
            catch (Exception ex) { Log.Line($"Exception in ReceivedPacket: {ex}"); }
        }

        internal void SyncWeapon(Weapon weapon, WeaponTimings timings, ref WeaponSyncValues weaponData, bool setState = true)
        {
            var comp = weapon.Comp;
            var cState = comp.State.Value;
            var wState = weapon.State;


            var wasReloading = wState.Reloading;

            if (setState)
            {
                comp.CurrentHeat -= weapon.State.Heat;
                cState.CurrentCharge -= weapon.State.CurrentCharge;


                weaponData.SetState(wState);

                comp.CurrentHeat += weapon.State.Heat;
                cState.CurrentCharge += weapon.State.CurrentCharge;                
            }

            comp.WeaponValues.Timings[weapon.WeaponId] = timings;
            weapon.Timings = timings;

            var hasMags = weapon.State.CurrentMags > 0;
            var hasAmmo = weapon.State.CurrentAmmo > 0;

            var chargeFullReload = weapon.System.MustCharge && !wasReloading && !weapon.State.Reloading && !hasAmmo && (hasMags || !weapon.System.EnergyAmmo);
            var regularFullReload = !weapon.System.MustCharge && !wasReloading && !weapon.State.Reloading && !hasAmmo && hasMags;

            var chargeContinueReloading = weapon.System.MustCharge && !weapon.State.Reloading && wasReloading;
            var regularContinueReloading = !weapon.System.MustCharge && !hasAmmo && hasMags && ((!weapon.State.Reloading && wasReloading) || (weapon.State.Reloading && !wasReloading)) ;

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
            else if (wasReloading && !weapon.State.Reloading && hasAmmo)
            {
                if(!weapon.System.MustCharge)
                    weapon.CancelableReloadAction -= weapon.Reloaded;

                weapon.EventTriggerStateChanged(EventTriggers.Reloading, false);
            }

            else if (weapon.System.MustCharge && weapon.State.Reloading && !weapon.Comp.Session.ChargingWeaponsCheck.Contains(weapon))
                weapon.ChargeReload();

            if (weapon.State.Heat > 0 && !weapon.HeatLoopRunning)
            {
                weapon.HeatLoopRunning = true;
                var delay = weapon.Timings.LastHeatUpdateTick > 0 ? weapon.Timings.LastHeatUpdateTick : 20;
                comp.Session.FutureEvents.Schedule(weapon.UpdateWeaponHeat, null, delay);
            }
        }

        #endregion
    }
}
