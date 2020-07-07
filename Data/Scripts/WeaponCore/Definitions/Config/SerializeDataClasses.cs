using System;
using ProtoBuf;
using Sandbox.Game.Entities;
using System.Collections.Generic;
using System.ComponentModel;
using VRageMath;
using WeaponCore.Platform;
using WeaponCore.Support;
using static WeaponCore.Support.Target;
using static WeaponCore.Support.WeaponComponent;
using static WeaponCore.WeaponStateValues;

namespace WeaponCore
{

    public enum PacketType
    {
        Invalid,
        AiData,
        CompData,
        CompState,
        StateReload,
        TargetChange,
        OverRidesUpdate,
        FakeTargetUpdate,
        ClientMouseEvent,
        ActiveControlUpdate,
        PlayerIdUpdate,
        FocusUpdate,
        ReticleUpdate,
        TargetExpireUpdate,
        ClientAiAdd,
        ClientAiRemove,
        RequestMouseStates,
        FullMouseUpdate,
        RequestShootUpdate,
        ReassignTargetUpdate,
        NextActiveUpdate,
        ReleaseActiveUpdate,
        AmmoCycleRequest,
        PlayerControlRequest,
        RescanGroupRequest,
        GridFocusListSync,
        FixedWeaponHitEvent,
        ProblemReport,
        TerminalMonitor,
        SendSingleShot,
    }

    #region packets
    [ProtoContract]
    [ProtoInclude(5, typeof(GridWeaponPacket))]
    [ProtoInclude(6, typeof(InputPacket))]
    [ProtoInclude(7, typeof(BoolUpdatePacket))]
    [ProtoInclude(8, typeof(FakeTargetPacket))]
    [ProtoInclude(10, typeof(FocusPacket))]
    [ProtoInclude(11, typeof(WeaponIdPacket))]
    [ProtoInclude(12, typeof(RequestTargetsPacket))]
    [ProtoInclude(13, typeof(MouseInputSyncPacket))]
    [ProtoInclude(14, typeof(AiDataPacket))]
    [ProtoInclude(15, typeof(GridFocusListPacket))]
    [ProtoInclude(16, typeof(FixedWeaponHitPacket))]
    [ProtoInclude(17, typeof(ProblemReportPacket))]
    [ProtoInclude(18, typeof(AmmoCycleRequestPacket))]
    [ProtoInclude(19, typeof(ShootStatePacket))]
    [ProtoInclude(20, typeof(OverRidesPacket))]
    [ProtoInclude(21, typeof(PlayerControlRequestPacket))]
    [ProtoInclude(22, typeof(TerminalMonitorPacket))]
    [ProtoInclude(23, typeof(CompDataPacket))]
    [ProtoInclude(24, typeof(CompStatePacket))]
    [ProtoInclude(25, typeof(TargetPacket))]


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
    public class TargetPacket : Packet
    {
        [ProtoMember(1)] internal TransferTarget Target;

        public TargetPacket() { }

        public override void CleanUp()
        {
            base.CleanUp();
            Target = null;
        }
    }

    [ProtoContract]
    public class AmmoCycleRequestPacket : Packet
    {
        [ProtoMember(1)] internal int WeaponId;
        [ProtoMember(2)] internal int NewAmmoId;

        public AmmoCycleRequestPacket() { }

        public override void CleanUp()
        {
            base.CleanUp();
            WeaponId = 0;
            NewAmmoId = 0;
        }
    }

    [ProtoContract]
    public class PlayerControlRequestPacket : Packet
    {
        [ProtoMember(1)] internal long PlayerId;
        [ProtoMember(2)] internal CompStateValues.ControlMode Mode;

        public PlayerControlRequestPacket() { }

        public override void CleanUp()
        {
            base.CleanUp();
            Mode = CompStateValues.ControlMode.None;
            PlayerId = -1;
        }
    }

    [ProtoContract]
    public class CompStatePacket : Packet
    {
        [ProtoMember(1)] internal CompStateValues Data;
        public CompStatePacket() { }

        public override void CleanUp()
        {
            base.CleanUp();
            Data = null;
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
        [ProtoMember(1)] internal List<WeaponStateValues> Data;
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
    public class AiDataPacket : Packet
    {
        [ProtoMember(1)] internal AiDataValues Data;
        public AiDataPacket() { }

        public override void CleanUp()
        {
            base.CleanUp();
            Data = null;
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
    public class WeaponRandomGenerator
    {
        [ProtoMember(1)] public int TurretCurrentCounter;
        [ProtoMember(2)] public int ClientProjectileCurrentCounter;
        [ProtoMember(3)] public int CurrentSeed;
        public Random TurretRandom;
        public Random ClientProjectileRandom;
        public Random AcquireRandom;

        public enum RandomType
        {
            Deviation,
            ReAcquire,
            Acquire,
        }

        public WeaponRandomGenerator() { }

        public void Init(int uniqueId)
        {
            CurrentSeed = uniqueId;
            TurretRandom = new Random(CurrentSeed);
            ClientProjectileRandom = new Random(CurrentSeed);
            AcquireRandom = new Random(CurrentSeed);
        }

        public void Sync(WeaponRandomGenerator syncFrom)
        {
            CurrentSeed = syncFrom.CurrentSeed;

            TurretCurrentCounter = syncFrom.TurretCurrentCounter;
            ClientProjectileCurrentCounter = syncFrom.ClientProjectileCurrentCounter;

            TurretRandom = new Random(CurrentSeed);
            ClientProjectileRandom = new Random(CurrentSeed);
        }
    }


    #endregion
}
