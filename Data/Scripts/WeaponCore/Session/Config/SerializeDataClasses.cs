using ProtoBuf;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System.Collections.Generic;
using System.ComponentModel;
using VRage;
using VRageMath;
using WeaponCore.Support;
using static WeaponCore.Platform.Weapon;
using static WeaponCore.Support.Target;

namespace WeaponCore
{

    public enum PacketType
    {
        Invalid,
        GridSyncRequestUpdate,
        CompStateUpdate,
        CompSettingsUpdate,
        WeaponSyncUpdate,
        WeaponPacket,
        FakeTargetUpdate,
        ClientMouseEvent,
        ActiveControlUpdate,
        PlayerIdUpdate,
        ActiveControlFullUpdate,
        FocusUpdate,
        MagUpdate,
        ReticleUpdate,
        OverRidesUpdate,
        PlayerControlUpdate,
        TargetExpireUpdate,
        WeaponUpdateRequest,
        ClientEntityClosed,
        RequestMouseStates,
        FullMouseUpdate,
        CompToolbarShootState,
        RangeUpdate,
        GridAiUiMidUpdate,
        CycleAmmo,
        ReassignTargetUpdate,
        NextActiveUpdate,
        ReleaseActiveUpdate,
        GridOverRidesSync,
        RescanGroupRequest,
        GridFocusListSync,
        FixedWeaponHitEvent,
        ClientMidUpdate,
        CompSyncRequest,
        RequestReport,
        SentReport,
    }

    #region packets
    [ProtoContract]
    [ProtoInclude(4, typeof(GridWeaponPacket))]
    [ProtoInclude(5, typeof(InputPacket))]
    [ProtoInclude(6, typeof(BoolUpdatePacket))]
    [ProtoInclude(7, typeof(FakeTargetPacket))]
    [ProtoInclude(8, typeof(CurrentGridPlayersPacket))]
    [ProtoInclude(9, typeof(FocusPacket))]
    [ProtoInclude(10, typeof(WeaponIdPacket))]
    [ProtoInclude(11, typeof(RequestTargetsPacket))]
    [ProtoInclude(12, typeof(MouseInputSyncPacket))]
    [ProtoInclude(13, typeof(GridOverRidesSyncPacket))]
    [ProtoInclude(14, typeof(GridFocusListPacket))]
    [ProtoInclude(15, typeof(FixedWeaponHitPacket))]
    [ProtoInclude(16, typeof(ClientMIdUpdatePacket))]
    [ProtoInclude(17, typeof(MIdPacket))]
    [ProtoInclude(18, typeof(RequestDataReportPacket))]
    [ProtoInclude(19, typeof(SendDataReportPacket))]


    public class Packet
    {
        [ProtoMember(1)] internal long EntityId;
        [ProtoMember(2)] internal ulong SenderId;
        [ProtoMember(3)] internal PacketType PType;

        public virtual void CleanUp()
        {
            EntityId = 0;
            SenderId = 0;
            PType = PacketType.Invalid;
        }

        //can override in other packet
        protected bool Equals(Packet other)
        {
            return (EntityId.Equals(other.EntityId) && SenderId.Equals(other.SenderId) && PType.Equals(other.PType));
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((Packet)obj);
        }

        public override int GetHashCode()
        {
            return (EntityId.GetHashCode() + PType.GetHashCode() + SenderId.GetHashCode());
        }
    }

    [ProtoContract]
    public class RequestDataReportPacket : Packet
    {
        [ProtoMember(1)] internal bool AllClients;
        public RequestDataReportPacket() { }

        public override void CleanUp()
        {
            base.CleanUp();
            AllClients = false;
        }
    }

    [ProtoContract]
    public class SendDataReportPacket : Packet
    {
        [ProtoMember(1)] internal DataReport Data;
        public SendDataReportPacket() { }

        public override void CleanUp()
        {
            base.CleanUp();
            Data = null;
        }
    }

    [ProtoContract]
    public class GridWeaponPacket : Packet
    {
        [ProtoMember(1)] internal List<WeaponData> Data;
        public GridWeaponPacket() { }

        public override void CleanUp()
        {
            base.CleanUp();
            Data = null;
        }
    }

    [ProtoContract]
    public class InputPacket : Packet
    {
        [ProtoMember(1)] internal InputStateData Data;
        public InputPacket() { }

        public override void CleanUp()
        {
            base.CleanUp();
            Data = null;
        }
    }

    [ProtoContract]
    public class BoolUpdatePacket : Packet
    {
        [ProtoMember(1)] internal bool Data;
        public BoolUpdatePacket() { }

        public override void CleanUp()
        {
            base.CleanUp();
            Data = false;
        }
    }

    [ProtoContract]
    public class FakeTargetPacket : Packet
    {
        [ProtoMember(1)] internal Vector3 Data;
        public FakeTargetPacket() { }

        public override void CleanUp()
        {
            base.CleanUp();
            Data = new Vector3();
        }
    }

    [ProtoContract]
    public class CurrentGridPlayersPacket : Packet
    {
        [ProtoMember(1)] internal ControllingPlayersSync Data;
        public CurrentGridPlayersPacket() { }

        public override void CleanUp()
        {
            base.CleanUp();
            Data = new ControllingPlayersSync();
        }
    }

    [ProtoContract]
    public class FocusPacket : Packet
    {
        [ProtoMember(1)] internal long TargetId;
        [ProtoMember(2)] internal int FocusId;
        [ProtoMember(3)] internal bool AddSecondary;
        public FocusPacket() { }

        public override void CleanUp()
        {
            base.CleanUp();
            TargetId = 0;
            FocusId = 0;
            AddSecondary = false;
        }
    }

    [ProtoContract]
    public class WeaponIdPacket : Packet
    {
        [ProtoMember(1)] internal int WeaponId;

        public WeaponIdPacket() { }

        public override void CleanUp()
        {
            base.CleanUp();
            WeaponId = 0;
        }
    }

    [ProtoContract]
    public class RequestTargetsPacket : Packet
    {
        [ProtoMember(1)] internal List<long> Comps;

        public RequestTargetsPacket() { }

        public override void CleanUp()
        {
            base.CleanUp();
            Comps.Clear();
        }
    }

    [ProtoContract]
    public class MouseInputSyncPacket : Packet
    {
        [ProtoMember(1)] internal PlayerMouseData[] Data = new PlayerMouseData[0];
        public MouseInputSyncPacket() { }

        public override void CleanUp()
        {
            base.CleanUp();
            Data = new PlayerMouseData[0];
        }
    }

    [ProtoContract]
    public class GridOverRidesSyncPacket : Packet
    {
        [ProtoMember(1)] internal OverRidesData[] Data = new OverRidesData[0];
        public GridOverRidesSyncPacket() { }

        public override void CleanUp()
        {
            base.CleanUp();
            Data = new OverRidesData[0];
        }
    }

    [ProtoContract]
    public class GridFocusListPacket : Packet
    {
        [ProtoMember(1)] internal long[] EntityIds;
        public GridFocusListPacket() { }

        public override void CleanUp()
        {
            base.CleanUp();
            EntityIds = null;
        }
    }

    [ProtoContract]
    public class FixedWeaponHitPacket : Packet
    {
        [ProtoMember(1)] internal long HitEnt;
        [ProtoMember(2)] internal Vector3 HitOffset;
        [ProtoMember(3)] internal Vector3 Up;
        [ProtoMember(4)] internal Vector3 Velocity;
        [ProtoMember(5)] internal int MuzzleId;
        [ProtoMember(6)] internal int WeaponId;
        [ProtoMember(7)] internal int AmmoIndex;
        [ProtoMember(8)] internal float MaxTrajectory;

        public FixedWeaponHitPacket() { }

        public override void CleanUp()
        {
            base.CleanUp();
            HitEnt = 0;
            HitOffset = Vector3.Zero;
            Up = Vector3.Zero;
            MuzzleId = 0;
            WeaponId = 0;
            AmmoIndex = 0;
            MaxTrajectory = 0;
        }
    }

    public class ClientMIdUpdatePacket : Packet
    {
        [ProtoMember(1)] internal uint MId;
        [ProtoMember(2)] internal PacketType MidType;
        [ProtoMember(2)] internal int HashCheck;

        public ClientMIdUpdatePacket() { }

        public override void CleanUp()
        {
            base.CleanUp();
            MId = 0;
            MidType = PacketType.Invalid;
        }
    }
    #endregion

    #region MId Based Packets
    [ProtoContract]
    [ProtoInclude(22, typeof(RangePacket))]
    [ProtoInclude(23, typeof(CycleAmmoPacket))]
    [ProtoInclude(24, typeof(ShootStatePacket))]
    [ProtoInclude(25, typeof(OverRidesPacket))]
    [ProtoInclude(26, typeof(ControllingPlayerPacket))]
    [ProtoInclude(27, typeof(StatePacket))]
    [ProtoInclude(28, typeof(SettingPacket))]
    public class MIdPacket : Packet
    {
        [ProtoMember(1)] internal uint MId;

        public MIdPacket() { }

        public override void CleanUp()
        {
            base.CleanUp();
            MId = 0;
        }
    }

    [ProtoContract]
    public class RangePacket : MIdPacket
    {
        [ProtoMember(1)] internal float Data;
        public RangePacket() { }

        public override void CleanUp()
        {
            base.CleanUp();
            Data = 0f;
        }
    }

    [ProtoContract]
    public class CycleAmmoPacket : MIdPacket
    {
        [ProtoMember(1)] internal int AmmoId;
        [ProtoMember(2)] internal int WeaponId;
        public CycleAmmoPacket() { }

        public override void CleanUp()
        {
            base.CleanUp();
            AmmoId = 0;
            WeaponId = 0;
        }
    }

    [ProtoContract]
    public class ShootStatePacket : MIdPacket
    {
        [ProtoMember(1)] internal ManualShootActionState Data = ManualShootActionState.ShootOff;
        public ShootStatePacket() { }

        public override void CleanUp()
        {
            base.CleanUp();
            Data = ManualShootActionState.ShootOff;
        }
    }

    [ProtoContract]
    public class OverRidesPacket : MIdPacket
    {
        [ProtoMember(1)] internal GroupOverrides Data;
        [ProtoMember(2), DefaultValue("")] internal string GroupName = "";

        public OverRidesPacket() { }

        public override void CleanUp()
        {
            base.CleanUp();
            Data = null;
            GroupName = "";
        }
    }

    [ProtoContract]
    public class ControllingPlayerPacket : MIdPacket
    {
        [ProtoMember(1)] internal PlayerControl Data;

        public ControllingPlayerPacket() { }

        public override void CleanUp()
        {
            base.CleanUp();
            Data = null;
        }
    }

    [ProtoContract]
    public class StatePacket : MIdPacket
    {
        [ProtoMember(1)] internal CompStateValues Data;

        public StatePacket() { }

        public override void CleanUp()
        {
            base.CleanUp();
            Data = null;
        }
    }

    [ProtoContract]
    public class SettingPacket : MIdPacket
    {
        [ProtoMember(1)] internal CompSettingsValues Data;
        public SettingPacket() { }

        public override void CleanUp()
        {
            base.CleanUp();
            Data = null;
        }
    }

    #endregion

    #region packet Data


    [ProtoContract]
    internal class DataReport
    {
        [ProtoMember(1)] internal Dictionary<string, string> Session = new Dictionary<string, string>();
        [ProtoMember(2)] internal Dictionary<string, string> Ai = new Dictionary<string, string>();
        [ProtoMember(3)] internal Dictionary<string, string> Comp = new Dictionary<string, string>();
        [ProtoMember(4)] internal Dictionary<string, string> Platform = new Dictionary<string, string>();
        [ProtoMember(5)] internal Dictionary<string, string> Weapon = new Dictionary<string, string>();

        public DataReport() { }
    }

    [ProtoContract]
    public class WeaponData
    {
        [ProtoMember(1)] internal TransferTarget TargetData;
        [ProtoMember(2)] internal long CompEntityId;
        [ProtoMember(3)] internal WeaponSyncValues SyncData;
        [ProtoMember(4)] internal WeaponTimings Timmings;
        [ProtoMember(5)] internal WeaponRandomGenerator WeaponRng;

        public WeaponData() { }
    }

    [ProtoContract]
    internal class InputStateData
    {
        [ProtoMember(1)] internal bool MouseButtonLeft;
        [ProtoMember(2)] internal bool MouseButtonMiddle;
        [ProtoMember(3)] internal bool MouseButtonRight;
        [ProtoMember(4)] internal bool InMenu;

        internal InputStateData() { }

        internal InputStateData(InputStateData createFrom)
        {
            Sync(createFrom);
        }

        internal void Sync(InputStateData syncFrom)
        {
            MouseButtonLeft = syncFrom.MouseButtonLeft;
            MouseButtonMiddle = syncFrom.MouseButtonMiddle;
            MouseButtonRight = syncFrom.MouseButtonRight;
            InMenu = syncFrom.InMenu;
        }
    }

    [ProtoContract]
    internal class PlayerMouseData
    {
        [ProtoMember(1)] internal long PlayerId;
        [ProtoMember(2)] internal InputStateData MouseStateData;
    }

    [ProtoContract]
    internal class GroupSettingsData
    {
        [ProtoMember(1)] internal string SettingName;
        [ProtoMember(2)] internal bool Value;
    }

    [ProtoContract]
    public class TransferTarget
    {
        [ProtoMember(1)] public long EntityId;
        [ProtoMember(2)] public Vector3 TargetPos;
        [ProtoMember(3)] public float HitShortDist;
        [ProtoMember(4)] public float OrigDistance;
        [ProtoMember(5)] public long TopEntityId;
        [ProtoMember(6)] public TargetInfo State = TargetInfo.Expired;
        [ProtoMember(7)] public int WeaponId;

        public enum TargetInfo
        {
            IsEntity,
            IsProjectile,
            IsFakeTarget,
            Expired
        }

        internal void SyncTarget(Target target, bool allowChange = true)
        {
            var entity = MyEntities.GetEntityByIdOrDefault(EntityId);
            target.Entity = entity;
            target.TargetPos = TargetPos;
            target.HitShortDist = HitShortDist;
            target.OrigDistance = OrigDistance;
            target.TopEntityId = TopEntityId;

            target.IsProjectile = false;
            target.IsFakeTarget = false;

            if (State == TargetInfo.IsProjectile)
                target.IsProjectile = true;

            else if (State == TargetInfo.IsFakeTarget)
                target.IsFakeTarget = true;

            var state = State != TargetInfo.Expired ? States.Acquired : States.Expired;

            
            target.StateChange(State != TargetInfo.Expired, state);

            if (!allowChange)
                target.TargetChanged = false;
        }

        public TransferTarget()
        {
        }
    }

    [ProtoContract]
    public struct OverRidesData
    {
        [ProtoMember(1)] public string GroupName;
        [ProtoMember(2)] public GroupOverrides Overrides;
    }
    #endregion
}
