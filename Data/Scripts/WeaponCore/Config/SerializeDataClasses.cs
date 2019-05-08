using ProtoBuf;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.Entity;
using WeaponCore.Support;

namespace WeaponCore
{
    [ProtoInclude(3, typeof(DataLogicState))]
    [ProtoInclude(4, typeof(DataWeaponHit))]
    [ProtoInclude(5, typeof(DataEnforce))]

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
                if (Entity?.GameLogic == null) return false;
                var logic = Entity.GameLogic.GetAs<Logic>();
                logic?.UpdateState(State);
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
            if (Entity?.GameLogic == null) return false;
            var logic = Entity.GameLogic.GetAs<Logic>();
            logic?.UpdateSettings(Settings);
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
            var logic = Entity.GameLogic.GetAs<Logic>();
            if (logic == null) return false;

            Session.Instance.WeaponHits.Add(new WeaponHit(logic, State.HitPos, State.Size, State.Effect));
            return false;
        }
    }

    [ProtoContract]
    public class DataEnforce : PacketBase
    {
        public DataEnforce()
        {
        } // Empty constructor required for deserialization

        [ProtoMember(1)] public WeaponEnforcement State = null;

        public DataEnforce(long entityId, WeaponEnforcement state) : base(entityId)
        {
            State = state;
        }

        public override bool Received(bool isServer)
        {
            if (!isServer)
            {
                Session.Enforced = State;
                Session.EnforceInit = true;
                return false;
            }
            var data = new DataEnforce(0, Session.Enforced);
            var bytes = MyAPIGateway.Utilities.SerializeToBinary(data);
            MyAPIGateway.Multiplayer.SendMessageTo(Session.PACKET_ID, bytes, State.SenderId);
            return false;
        }
    }
}
