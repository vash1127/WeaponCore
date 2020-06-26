using ProtoBuf;
using Sandbox.Game.Entities;
using System.Collections.Generic;
using System.ComponentModel;
using VRageMath;
using WeaponCore.Support;
using static WeaponCore.Support.Target;
using static WeaponCore.Support.WeaponComponent;

namespace WeaponCore
{

    public enum PacketType
    {
        Invalid,
        GridSyncRequestUpdate,
        CompStateUpdate,
        CompSettingsUpdate,
        WeaponSyncUpdate,
        FakeTargetUpdate,
        ClientMouseEvent,
        ActiveControlUpdate,
        PlayerIdUpdate,
        ActiveControlFullUpdate,
        FocusUpdate,
        ReticleUpdate,
        OverRidesUpdate,
        PlayerControlUpdate,
        TargetExpireUpdate,
        WeaponUpdateRequest,
        ClientEntityClosed,
        RequestMouseStates,
        FullMouseUpdate,
        CompToolbarShootState,
        CompData,
        CycleAmmo,
        ReassignTargetUpdate,
        NextActiveUpdate,
        ReleaseActiveUpdate,
        GridOverRidesSync,
        RescanGroupRequest,
        GridFocusListSync,
        FixedWeaponHitEvent,
        CompSyncRequest,
        ProblemReport,
        TerminalMonitor,
    }

    #region packets
    [ProtoContract]
    [ProtoInclude(5, typeof(GridWeaponPacket))]
    [ProtoInclude(6, typeof(InputPacket))]
    [ProtoInclude(7, typeof(BoolUpdatePacket))]
    [ProtoInclude(8, typeof(FakeTargetPacket))]
    [ProtoInclude(9, typeof(CurrentGridPlayersPacket))]
    [ProtoInclude(10, typeof(FocusPacket))]
    [ProtoInclude(11, typeof(WeaponIdPacket))]
    [ProtoInclude(12, typeof(RequestTargetsPacket))]
    [ProtoInclude(13, typeof(MouseInputSyncPacket))]
    [ProtoInclude(14, typeof(GridOverRidesSyncPacket))]
    [ProtoInclude(15, typeof(GridFocusListPacket))]
    [ProtoInclude(16, typeof(FixedWeaponHitPacket))]
    [ProtoInclude(17, typeof(ProblemReportPacket))]
    [ProtoInclude(18, typeof(CycleAmmoPacket))]
    [ProtoInclude(19, typeof(ShootStatePacket))]
    [ProtoInclude(20, typeof(OverRidesPacket))]
    [ProtoInclude(21, typeof(ControllingPlayerPacket))]
    [ProtoInclude(22, typeof(StatePacket))]
    [ProtoInclude(23, typeof(SettingPacket))]
    [ProtoInclude(24, typeof(TerminalMonitorPacket))]
    [ProtoInclude(25, typeof(CompDataPacket))]



    public class Packet
    {
        [ProtoMember(1)] internal uint MId;
        [ProtoMember(2)] internal long EntityId;
        [ProtoMember(3)] internal ulong SenderId;
        [ProtoMember(4)] internal PacketType PType;

        public virtual void CleanUp()
        {
            MId = 0;
            EntityId = 0;
            SenderId = 0;
            PType = PacketType.Invalid;
        }

        //can override in other packet
        protected bool Equals(Packet other)
        {
            return (EntityId.Equals(other.EntityId) && SenderId.Equals(other.SenderId) && PType.Equals(other.PType) && MId.Equals(other.MId));
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
    public class CompDataPacket : Packet
    {
        [ProtoMember(1)] internal CompDataValues Data;
        public CompDataPacket() { }

        public override void CleanUp()
        {
            base.CleanUp();
            Data = null;
        }
    }

    [ProtoContract]
    public class ProblemReportPacket : Packet
    {
        public enum RequestType
        {
            SendReport,
            RequestServerReport,
            RequestAllReport,
        }

        [ProtoMember(1)] internal RequestType Type;
        [ProtoMember(2)] internal DataReport Data;

        public ProblemReportPacket() { }

        public override void CleanUp()
        {
            base.CleanUp();
            Type = RequestType.RequestServerReport;
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


    [ProtoContract]
    public class TerminalMonitorPacket : Packet
    {
        public enum Change
        {
            Update,
            Clean,
        }

        [ProtoMember(1)] internal Change State;
        public TerminalMonitorPacket() { }

        public override void CleanUp()
        {
            base.CleanUp();
            State = Change.Update;
        }
    }

    [ProtoContract]
    public class CycleAmmoPacket : Packet
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
    public class ShootStatePacket : Packet
    {
        [ProtoMember(1)] internal ShootActions Action = ShootActions.ShootOff;
        [ProtoMember(2), DefaultValue(-1)] internal long PlayerId = -1;

        public ShootStatePacket() { }

        public override void CleanUp()
        {
            base.CleanUp();
            Action = ShootActions.ShootOff;
            PlayerId = -1;
        }
    }

    [ProtoContract]
    public class OverRidesPacket : Packet
    {
        [ProtoMember(1)] internal GroupOverrides Data;
        [ProtoMember(2), DefaultValue("")] internal string GroupName = "";
        [ProtoMember(3), DefaultValue("")] internal string Setting = "";
        [ProtoMember(4)] internal int Value;

        public OverRidesPacket() { }

        public override void CleanUp()
        {
            base.CleanUp();
            Data = null;
            GroupName = string.Empty;
            Setting = string.Empty;
            Value = 0;
        }
    }

    [ProtoContract]
    public class ControllingPlayerPacket : Packet
    {
        [ProtoMember(1)] internal long PlayerId;
        [ProtoMember(2)] internal CompStateValues.ControlMode Control;


        public ControllingPlayerPacket() { }

        public override void CleanUp()
        {
            base.CleanUp();
            PlayerId = -1;
            Control = CompStateValues.ControlMode.None;
        }
    }

    [ProtoContract]
    public class StatePacket : Packet
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
    public class SettingPacket : Packet
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
        [ProtoMember(3)] internal WeaponStateValues SyncData;
        [ProtoMember(4)] internal WeaponRandomGenerator WeaponRng;

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
