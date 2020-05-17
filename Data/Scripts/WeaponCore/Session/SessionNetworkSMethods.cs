using Sandbox.Game.Entities;
using WeaponCore.Control;
using WeaponCore.Platform;
using WeaponCore.Support;
using static WeaponCore.Platform.Weapon;
using static WeaponCore.Session;
using static WeaponCore.Support.GridAi;
using static WeaponCore.Support.WeaponDefinition;

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
                comp.State.Value.Sync(statePacket.Data);
                PacketsToClient.Add(new PacketInfo { Entity = ent, Packet = statePacket });

                data.Report.PacketValid = true;
            }
            else {
                SendMidResync(packet.PType, comp.MIds[(int)packet.PType], packet.SenderId, ent, comp);
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
                comp.Set.Value.Sync(comp, setPacket.Data);
                PacketsToClient.Add(new PacketInfo { Entity = ent, Packet = setPacket });

                data.Report.PacketValid = true;
            }
            else {
                SendMidResync(packet.PType, comp.MIds[(int)packet.PType], packet.SenderId, ent, comp);
                return Error(data, Msg("Mid is old, likely multiple clients attempting update"));
            }

            return true;
        }
        private bool ServerClientMouseEvent(PacketObj data)
        {
            var packet = data.Packet;
            return true;
        }
        private bool ServerActiveControlUpdate(PacketObj data)
        {
            var packet = data.Packet;
            return true;
        }
        private bool ServerFakeTargetUpdate(PacketObj data)
        {
            var packet = data.Packet;
            return true;
        }
        private bool ServerGridSyncRequestUpdate(PacketObj data)
        {
            var packet = data.Packet;
            return true;
        }
        private bool ServerReticleUpdate(PacketObj data)
        {
            var packet = data.Packet;
            return true;
        }
        private bool ServerOverRidesUpdate(PacketObj data)
        {
            var packet = data.Packet;
            return true;
        }
        private bool ServerPlayerControlUpdate(PacketObj data)
        {
            var packet = data.Packet;
            return true;
        }
        private bool ServerWeaponUpdateRequest(PacketObj data)
        {
            var packet = data.Packet;
            return true;
        }
        private bool ServerClientEntityClosed(PacketObj data)
        {
            var packet = data.Packet;
            return true;
        }
        private bool ServerRequestMouseStates(PacketObj data)
        {
            var packet = data.Packet;
            return true;
        }
        private bool ServerCompToolbarShootState(PacketObj data)
        {
            var packet = data.Packet;
            return true;
        }
        private bool ServerRangeUpdate(PacketObj data)
        {
            var packet = data.Packet;
            return true;
        }
        private bool ServerCycleAmmo(PacketObj data)
        {
            var packet = data.Packet;
            return true;
        }
        private bool ServerRescanGroupRequest(PacketObj data)
        {
            var packet = data.Packet;
            return true;
        }
        private bool ServerFixedWeaponHitEvent(PacketObj data)
        {
            var packet = data.Packet;
            return true;
        }
        private bool ServerCompSyncRequest(PacketObj data)
        {
            var packet = data.Packet;
            return true;
        }
        private bool ServerFocusUpdate(PacketObj data)
        {
            var packet = data.Packet;
            return true;
        }
    }
}
