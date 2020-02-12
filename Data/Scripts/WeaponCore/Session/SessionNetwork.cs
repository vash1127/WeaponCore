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
        internal void PacketizeToClientsInRange(IMyFunctionalBlock block, Packet packet)
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
            if (!IsMultiplayer) return;

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
                if (comp != null)
                {
                    switch (packet.PType)
                    {
                        case PacketType.CompStateUpdate:
                            var statePacket = packet as StatePacket;
                            comp.State.Value = statePacket.Data;

                            for (int i = 0; i < comp.Platform.Weapons.Length; i++)
                            {
                                var w = comp.Platform.Weapons[i];
                                w.State = comp.State.Value.Weapons[w.WeaponId];
                            }

                            break;
                        case PacketType.CompSettingsUpdate:
                            var setPacket = packet as SettingPacket;
                            comp.Set.Value = setPacket.Data;

                            for (int i = 0; i < comp.Platform.Weapons.Length; i++)
                            {
                                var w = comp.Platform.Weapons[i];
                                w.Set = comp.Set.Value.Weapons[w.WeaponId];
                            }

                            break;
                        case PacketType.TargetUpdate:
                            var targetPacket = packet as TargetPacket;
                            var targets = targetPacket.Data;
                            for(int i = 0; i < targets.Length; i++)
                                comp.Platform.Weapons[i].Target = targets[i];
                            break;
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
                IMyFunctionalBlock ent; // not inited here to avoid extras calls unless needed
                WeaponComponent comp; // not inited here to avoid extras calls unless needed
                long playerId = 0;

                switch (packet.PType)
                {
                    case PacketType.CompStateUpdate:
                        var statePacket = packet as StatePacket;
                        ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId) as IMyFunctionalBlock;
                        comp = ent?.Components.Get<WeaponComponent>();                        

                        if (comp == null || statePacket == null) return;

                        if (statePacket.Data.MId > comp.State.Value.MId)
                        {
                            comp.State.Value = statePacket.Data;
                            for(int i = 0; i < comp.Platform.Weapons.Length; i++)
                            {
                                var w = comp.Platform.Weapons[i];
                                w.State = comp.State.Value.Weapons[w.WeaponId];
                            }
                            PacketizeToClientsInRange(ent, packet);
                        }
                        break;

                    case PacketType.CompSettingsUpdate:
                        var setPacket = packet as SettingPacket;
                        ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId) as IMyFunctionalBlock;
                        comp = ent?.Components.Get<WeaponComponent>();

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

                        if (updatePacket.Data) //update/add
                        {
                            if (GridTargetingAIs.TryGetValue(grid, out TrackingAi) && SteamToPlayer.TryGetValue(packet.SenderId, out playerId))
                                TrackingAi.ControllingPlayers[playerId] = block;
                        }
                        else //remove
                        {
                            if (GridTargetingAIs.TryGetValue(grid, out TrackingAi) && SteamToPlayer.TryGetValue(packet.SenderId, out playerId))
                                TrackingAi.ControllingPlayers.TryGetValue(playerId, out block);
                        }
                        break;
                }
            }
            catch (Exception ex) { Log.Line($"Exception in ReceivedPacket: {ex}"); }
        }

        #endregion
    }
}
