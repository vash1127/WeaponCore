using ProtoBuf;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.Entity;
using WeaponCore.Support;

namespace WeaponCore
{
    [ProtoInclude(3, typeof(DataCompState))]
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
    }
}
