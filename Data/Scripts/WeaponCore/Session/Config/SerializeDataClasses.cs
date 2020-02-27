using ProtoBuf;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System.Collections.Generic;
using VRage;
using VRageMath;
using WeaponCore.Support;
using static WeaponCore.Support.GridAi;
using static WeaponCore.Support.Target;

namespace WeaponCore
{

    public enum PacketType
    {
        Invalid,
        ActiveControlRequestUpdate,
        CompStateUpdate,
        CompSettingsUpdate,
        TargetUpdate,
        WeaponPacket,
        FakeTargetUpdate,
        ClientMouseEvent,
        ActiveControlUpdate,
        PlayerIdUpdate,
        ActiveControlFullUpdate,
        FocusUpdate,
        MagUpdate,
    }

    [ProtoContract]
    [ProtoInclude(4, typeof(StatePacket))]
    [ProtoInclude(5, typeof(SettingPacket))]
    [ProtoInclude(6, typeof(GridWeaponPacket))]
    [ProtoInclude(7, typeof(MouseInputPacket))]
    [ProtoInclude(8, typeof(DictionaryUpdatePacket))]
    [ProtoInclude(9, typeof(FakeTargetPacket))]
    [ProtoInclude(10, typeof(ControllingPacket))]
    [ProtoInclude(11, typeof(FocusPacket))]
    [ProtoInclude(12, typeof(MagUpdatePacket))]
    public class Packet
    {
        [ProtoMember(1)] internal long EntityId;
        [ProtoMember(2)] internal ulong SenderId;
        [ProtoMember(3)] internal PacketType PType;

        public void CleanUp()
        {
            EntityId = 0;
            SenderId = 0;
            switch (PType)
            {
                case PacketType.Invalid:
                {
                    break;
                }
                case PacketType.ActiveControlRequestUpdate:
                {
                    break;
                }
                case PacketType.CompStateUpdate:
                {
                    ((StatePacket) this).Data = null;
                    break;
                }
                case PacketType.CompSettingsUpdate:
                {
                    ((SettingPacket) this).Data = null;
                    break;
                }
                case PacketType.TargetUpdate:
                {
                    var gridSync = (GridWeaponPacket) this;
                    for (int i = 0; i < gridSync.TargetData.Count; i++)
                    {
                        var weaponSync = gridSync.TargetData[i];
                        weaponSync.TargetData = null;
                        weaponSync.Timmings = null;
                        weaponSync.CompEntityId = 0;
                        weaponSync.WeaponData = new WeaponSyncValues();
                    }
                    gridSync.TargetData.Clear();
                    break;
                }
                case PacketType.FakeTargetUpdate:
                {
                    ((FakeTargetPacket)this).Data = null;
                    break;
                }
                case PacketType.ClientMouseEvent:
                {
                    ((MouseInputPacket)this).Data = null;
                    break;
                }
                case PacketType.ActiveControlUpdate:
                {
                    ((DictionaryUpdatePacket)this).Data = false;
                    break;
                }
                case PacketType.PlayerIdUpdate:
                {
                    ((DictionaryUpdatePacket) this).Data = false;
                    break;
                }
                case PacketType.ActiveControlFullUpdate:
                {
                    ((ControllingPacket)this).Data = new ControllingPlayersSync();
                    break;
                }
                case PacketType.FocusUpdate:
                {
                    ((FocusPacket) this).Data = 0;
                    break;
                }
                case PacketType.MagUpdate:
                {
                    var magPacket = (MagUpdatePacket)this;
                    magPacket.Mags = new MyFixedPoint();
                    magPacket.WeaponId = 0;
                    break;
                }
            }
            PType = PacketType.Invalid;
        }
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
    public class GridWeaponPacket : Packet
    {
        [ProtoMember(1)] internal List<WeaponPacket> TargetData = new List<WeaponPacket>();
        public GridWeaponPacket() { }
    }

    [ProtoContract]
    public class WeaponPacket
    {
        [ProtoMember(1)] internal TransferTargetPacket TargetData = null;
        [ProtoMember(2)] internal long CompEntityId;
        [ProtoMember(3)] internal WeaponSyncValues WeaponData;
        [ProtoMember(4)] internal WeaponTimings Timmings = null;

        public WeaponPacket() { }
    }

    [ProtoContract]
    public class FakeTargetPacket : Packet
    {
        [ProtoMember(1)] internal FakeTarget Data = null;
        public FakeTargetPacket() { }
    }


    [ProtoContract]
    public class MouseInputPacket : Packet
    {
        [ProtoMember(1)] internal MouseStatePacket Data = null;
        public MouseInputPacket() { }
    }

    [ProtoContract]
    public class ControllingPacket : Packet
    {
        [ProtoMember(1)] internal ControllingPlayersSync Data;
        public ControllingPacket() { }
    }

    [ProtoContract]
    public class MagUpdatePacket : Packet
    {
        [ProtoMember(1)] internal MyFixedPoint Mags;
        [ProtoMember(2)] internal int WeaponId;
        public MagUpdatePacket() { }
    }

    [ProtoContract]
    public class DictionaryUpdatePacket : Packet
    {
        [ProtoMember(1)] internal bool Data;
        public DictionaryUpdatePacket() { }
    }

    [ProtoContract]
    public class FocusPacket : Packet
    {
        [ProtoMember(1)] internal long Data;
        public FocusPacket() { }
    }

    [ProtoContract]
    internal class MouseStatePacket
    {
        [ProtoMember(1)] internal bool MouseButtonLeft;
        [ProtoMember(2)] internal bool MouseButtonMiddle;
        [ProtoMember(3)] internal bool MouseButtonRight;
    }

    [ProtoContract]
    public class TransferTargetPacket
    {
        [ProtoMember(1)] internal long EntityId;
        [ProtoMember(2)] internal bool IsProjectile;
        [ProtoMember(3)] internal bool IsFakeTarget;
        [ProtoMember(4)] internal Vector3D TargetPos;
        [ProtoMember(5)] internal double HitShortDist;
        [ProtoMember(6)] internal double OrigDistance;
        [ProtoMember(7)] internal long TopEntityId;
        [ProtoMember(8)] internal Targets State = Targets.Expired;
        [ProtoMember(9)] internal int WeaponId;

        internal void SyncTarget(Target target)
        {
            var entity = MyEntities.GetEntityByIdOrDefault(EntityId);
            target.Entity = entity;
            target.IsProjectile = IsProjectile;
            target.IsFakeTarget = IsFakeTarget;
            target.TargetPos = TargetPos;
            target.HitShortDist = HitShortDist;
            target.OrigDistance = OrigDistance;
            target.TopEntityId = TopEntityId;
            target.State = State;
        }

        public TransferTargetPacket()
        {
        }
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
