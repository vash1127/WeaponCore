using System;
using ProtoBuf;
using System.Collections.Generic;
using System.ComponentModel;
using VRageMath;
using WeaponCore.Settings;
using WeaponCore.Support;
using static WeaponCore.Support.WeaponComponent;
using static WeaponCore.WeaponStateValues;

namespace WeaponCore
{

    public enum PacketType
    {
        Invalid,
        AiData,
        CompBase,
        CompState,
        Construct,
        ConstructFoci,
        TargetChange,
        RequestSetDps,
        RequestSetGuidance,
        RequestSetRof,
        RequestSetOverload,
        RequestSetRange,
        OverRidesUpdate,
        FakeTargetUpdate,
        ClientMouseEvent,
        ActiveControlUpdate,
        PlayerIdUpdate,
        FocusUpdate,
        FocusLockUpdate,
        ReticleUpdate,
        ClientAiAdd,
        ClientAiRemove,
        RequestMouseStates,
        FullMouseUpdate,
        RequestShootUpdate,
        NextActiveUpdate,
        ReleaseActiveUpdate,
        AmmoCycleRequest,
        PlayerControlRequest,
        FixedWeaponHitEvent,
        ProblemReport,
        TerminalMonitor,
        ClientNotify,
        ServerData,
        WeaponReload,
        WeaponAmmo,
        QueueShot,
        EwaredBlocks,
    }

    #region packets
    [ProtoContract]
    [ProtoInclude(5, typeof(InputPacket))]
    [ProtoInclude(6, typeof(BoolUpdatePacket))]
    [ProtoInclude(7, typeof(FakeTargetPacket))]
    [ProtoInclude(8, typeof(FocusPacket))]
    [ProtoInclude(9, typeof(WeaponIdPacket))]
    [ProtoInclude(10, typeof(MouseInputSyncPacket))]
    [ProtoInclude(11, typeof(AiDataPacket))]
    [ProtoInclude(12, typeof(FixedWeaponHitPacket))]
    [ProtoInclude(13, typeof(ProblemReportPacket))]
    [ProtoInclude(14, typeof(AmmoCycleRequestPacket))]
    [ProtoInclude(15, typeof(ShootStatePacket))]
    [ProtoInclude(16, typeof(OverRidesPacket))]
    [ProtoInclude(17, typeof(PlayerControlRequestPacket))]
    [ProtoInclude(18, typeof(TerminalMonitorPacket))]
    [ProtoInclude(19, typeof(CompBasePacket))]
    [ProtoInclude(20, typeof(CompStatePacket))]
    [ProtoInclude(21, typeof(TargetPacket))]
    [ProtoInclude(22, typeof(ConstructPacket))]
    [ProtoInclude(23, typeof(ConstructFociPacket))]
    [ProtoInclude(24, typeof(FloatUpdatePacket))]
    [ProtoInclude(25, typeof(ClientNotifyPacket))]
    [ProtoInclude(26, typeof(ServerPacket))]
    [ProtoInclude(27, typeof(WeaponReloadPacket))]
    [ProtoInclude(28, typeof(QueuedShotPacket))]
    [ProtoInclude(29, typeof(WeaponAmmoPacket))]
    [ProtoInclude(30, typeof(EwaredBlocksPacket))]

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
    public class ConstructPacket : Packet
    {
        [ProtoMember(1)] internal ConstructDataValues Data;

        public ConstructPacket() { }

        public override void CleanUp()
        {
            base.CleanUp();
            Data = null;
        }
    }

    [ProtoContract]
    public class ConstructFociPacket : Packet
    {
        [ProtoMember(1)] internal FocusData Data;

        public ConstructFociPacket() { }

        public override void CleanUp()
        {
            base.CleanUp();
            Data = null;
        }
    }

    [ProtoContract]
    public class AmmoCycleRequestPacket : Packet
    {
        [ProtoMember(1)] internal int WeaponId;
        [ProtoMember(2)] internal int NewAmmoId;
        [ProtoMember(3), DefaultValue(-1)] internal long PlayerId;


        public AmmoCycleRequestPacket() { }

        public override void CleanUp()
        {
            base.CleanUp();
            WeaponId = 0;
            NewAmmoId = 0;
            PlayerId = -1;
        }
    }

    [ProtoContract]
    public class QueuedShotPacket : Packet
    {
        [ProtoMember(1)] internal int WeaponId;
        [ProtoMember(2), DefaultValue(-1)] internal long PlayerId;


        public QueuedShotPacket() { }

        public override void CleanUp()
        {
            base.CleanUp();
            WeaponId = 0;
            PlayerId = -1;
        }
    }

    [ProtoContract]
    public class EwaredBlocksPacket : Packet
    {
        [ProtoMember(1)] internal List<long> Data = new List<long>(32);

        public EwaredBlocksPacket() { }

        public override void CleanUp()
        {
            base.CleanUp();
            Data.Clear();
        }
    }


    [ProtoContract]
    public class WeaponAmmoPacket : Packet
    {
        [ProtoMember(1)] internal AmmoValues Data;
        [ProtoMember(2)] internal int WeaponId;


        public WeaponAmmoPacket() { }

        public override void CleanUp()
        {
            base.CleanUp();
            Data = null;
            WeaponId = 0;
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
    public class WeaponReloadPacket : Packet
    {
        [ProtoMember(1)] internal WeaponReloadValues Data;
        [ProtoMember(2)] internal int WeaponId;

        public WeaponReloadPacket() { }

        public override void CleanUp()
        {
            base.CleanUp();
            Data = null;
            WeaponId = 0;
        }
    }

    [ProtoContract]
    public class CompBasePacket : Packet
    {
        [ProtoMember(1)] internal CompBaseValues Data;
        public CompBasePacket() { }

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
    public class FloatUpdatePacket : Packet
    {
        [ProtoMember(1)] internal float Data;
        public FloatUpdatePacket() { }

        public override void CleanUp()
        {
            base.CleanUp();
            Data = 0;
        }
    }

    [ProtoContract]
    public class ClientNotifyPacket : Packet
    {
        [ProtoMember(1)] internal string Message;
        [ProtoMember(2)] internal string Color;
        [ProtoMember(3)] internal int Duration;

        public ClientNotifyPacket() { }

        public override void CleanUp()
        {
            base.CleanUp();
            Message = string.Empty;
            Color = string.Empty;
            Duration = 0;
        }
    }

    [ProtoContract]
    public class ServerPacket : Packet
    {
        [ProtoMember(1)] internal CoreSettings.ServerSettings Data;
        [ProtoMember(2)] internal string VersionString;

        public ServerPacket() { }

        public override void CleanUp()
        {
            base.CleanUp();
            Data = null;
            VersionString = string.Empty;
        }
    }

    [ProtoContract]
    public class FakeTargetPacket : Packet
    {
        [ProtoMember(1)] internal Vector3 Pos;
        [ProtoMember(2)] internal long TargetId;

        public FakeTargetPacket() { }

        public override void CleanUp()
        {
            base.CleanUp();
            Pos = new Vector3();
            TargetId = 0;
        }
    }


    [ProtoContract]
    public class FocusPacket : Packet
    {
        [ProtoMember(1)] internal long TargetId;
        [ProtoMember(2), DefaultValue(-1)] internal int FocusId;
        [ProtoMember(3)] internal bool AddSecondary;
        public FocusPacket() { }

        public override void CleanUp()
        {
            base.CleanUp();
            TargetId = 0;
            FocusId = -1;
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
    internal class InputStateData
    {
        [ProtoMember(1)] internal bool MouseButtonLeft;
        [ProtoMember(2)] internal bool MouseButtonMenu;
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
            MouseButtonMenu = syncFrom.MouseButtonMenu;
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
        [ProtoMember(3)] public int AcquireCurrentCounter;
        [ProtoMember(4)] public int CurrentSeed;
        public Random TurretRandom;
        public Random ClientProjectileRandom;
        public Random AcquireRandom;
        public int AcquireTmpCounter;

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
            AcquireTmpCounter = syncFrom.AcquireCurrentCounter;
            TurretRandom = new Random(CurrentSeed);
            ClientProjectileRandom = new Random(CurrentSeed);
        }

        internal void ReInitRandom()
        {
            TurretCurrentCounter = 0;
            ClientProjectileCurrentCounter = 0;
            AcquireCurrentCounter = 0;
            CurrentSeed = TurretRandom.Next(1, int.MaxValue);
            TurretRandom = new Random(CurrentSeed);
            ClientProjectileRandom = new Random(CurrentSeed);
            AcquireRandom = new Random(CurrentSeed);
        }
    }


    #endregion
}
