using ProtoBuf;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.Entity;
using WeaponCore.Support;

namespace WeaponCore
{
    [ProtoInclude(3, typeof(DataLogicState))]
    [ProtoInclude(4, typeof(DataWeaponHit))]

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
    public class DataLogicState : PacketBase
    {
        public DataLogicState()
        {
        } // Empty constructor required for deserialization

        [ProtoMember(1)] public LogicStateValues State = null;

        public DataLogicState(long entityId, LogicStateValues state) : base(entityId)
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
    public class DataLogicSettings : PacketBase
    {
        public DataLogicSettings()
        {
        } // Empty constructor required for deserialization

        [ProtoMember(1)] public LogicSettingsValues Settings = null;

        public DataLogicSettings(long entityId, LogicSettingsValues settings) : base(entityId)
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

    [ProtoContract]
    public class DataWeaponHit : PacketBase
    {
        public DataWeaponHit()
        {
        } // Empty constructor required for deserialization

        [ProtoMember(1)] public WeaponHitValues State = null;

        public DataWeaponHit(long entityId, WeaponHitValues state) : base(entityId)
        {
            State = state;
        }

        public override bool Received(bool isServer)
        {
            if (isServer || Entity?.GameLogic == null) return false;
            var logic = Entity.GameLogic.GetAs<WeaponComponent>();
            if (logic == null) return false;

            Session.Instance.WeaponHits.Add(new WeaponHit(logic, State.HitPos, State.Size, State.Effect));
            return false;
        }
    }
}
