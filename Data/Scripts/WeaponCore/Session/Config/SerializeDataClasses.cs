using ProtoBuf;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.Entity;
using WeaponCore.Support;

namespace WeaponCore
{

    public enum PacketType
    {
        CompStateUpdate,
        CompSettingsUpdate,
        TargetUpdate,
        ClientMouseEvent,
    }

    [ProtoContract]
    [ProtoInclude(4, typeof(StatePacket))]
    [ProtoInclude(5, typeof(SettingPacket))]
    [ProtoInclude(6, typeof(TargetPacket))]
    [ProtoInclude(7, typeof(MouseInputPacket))]
    public abstract class Packet
    {
        [ProtoMember(1)] internal long EntityId;
        [ProtoMember(2)] internal ulong SenderId;
        [ProtoMember(3)] internal PacketType PType;        
    }

    [ProtoContract]
    public class StatePacket : Packet
    {
        [ProtoMember(1)] internal CompStateValues Data = null;

        public StatePacket() { }
    }

    [ProtoContract]
    public class SettingPacket : Packet
    {
        [ProtoMember(1)] internal CompSettingsValues Data = null;
        public SettingPacket() { }
    }

    [ProtoContract]
    public class TargetPacket : Packet
    {
        [ProtoMember(1)] internal Target[] Data = null;
        public TargetPacket() { }
    }

    [ProtoContract]
    public class MouseInputPacket : Packet
    {
        [ProtoMember(1)] internal ServerMouseState Data = null;
        public MouseInputPacket() { }
    }

    [ProtoContract]
    internal class ServerMouseState
    {
        internal bool MouseButtonLeft;
        internal bool MouseButtonMiddle;
        internal bool MouseButtonRight;
    }

    /*[ProtoInclude(3, typeof(DataCompState))]
    [ProtoContract]
    public abstract class PacketBase
    {
        [ProtoMember(1)] public ulong SenderId;

        [ProtoMember(2)] public long EntityId;

        private MyEntity _ent;

        public MyEntity Entity
        {
            get
            {
                if (EntityId == 0) return null;

                if (_ent == null) _ent = MyEntities.GetEntityById(EntityId, true);

                if (_ent == null || _ent.MarkedForClose) return null;
                return _ent;
            }
        }

        public PacketBase(long entityId = 0)
        {
            SenderId = MyAPIGateway.Multiplayer.MyId;
            EntityId = entityId;
        }

        public abstract bool Received(bool isServer);
    }
    
    [ProtoContract]
    public class DataCompState : PacketBase
    {
        public DataCompState()
        {
        } // Empty constructor required for deserialization

        [ProtoMember(1)] public CompStateValues State = null;

        public DataCompState(long entityId, CompStateValues state) : base(entityId)
        {
            State = state;
        }

        public override bool Received(bool isServer)
        {
            if (!isServer)
            {
                if (Entity == null) return false;
                var comp = Entity.Components.Get<WeaponComponent>();
                comp?.UpdateState(State);
                return false;
            }
            return true;
        }
    }

    [ProtoContract]
    public class DataCompSettings : PacketBase
    {
        public DataCompSettings()
        {
        } // Empty constructor required for deserialization

        [ProtoMember(1)] public CompSettingsValues Settings = null;

        public DataCompSettings(long entityId, CompSettingsValues settings) : base(entityId)
        {
            Settings = settings;
        }

        public override bool Received(bool isServer)
        {
            if (Entity == null) return false;
            var comp = Entity.Components.Get<WeaponComponent>();
            comp?.UpdateSettings(Settings);
            return isServer;
        }
    }*/
}
