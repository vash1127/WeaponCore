using System;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.Entity;
using VRageMath;
using WeaponCore.Support;

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
                    if (id != packet.SenderId && Vector3D.DistanceSquared(p.GetPosition(), block.PositionComp.WorldAABB.Center) <= SyncBufferedDistSqr)
                        MyAPIGateway.Multiplayer.SendMessageTo(ClientPacketId, bytes, p.SteamUserId);
                }
            }
            catch (Exception ex) { Log.Line($"Exception in PacketizeToClientsInRange: {ex}"); }
        }

        internal void SendPacketToServer(Packet packet)
        {
            if (!MpActive) return;

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
                                    comp.State.Value.Weapons[syncTarget.weaponId] = weaponData;
                                    comp.Platform.Weapons[syncTarget.weaponId].State = weaponData;
                                    syncTarget.SyncTarget(comp.Platform.Weapons[syncTarget.weaponId].Target);
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
                            PlayerMouseStates[playerId] = mousePacket.Data;

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

        #endregion
    }
}
