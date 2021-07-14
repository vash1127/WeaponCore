﻿using System.Collections.Generic;
using System.ComponentModel;
using CoreSystems.Settings;
using ProtoBuf;
using VRageMath;
using static CoreSystems.Support.CoreComponent;

namespace CoreSystems
{
    public enum PacketType
    {
        Invalid,

        WeaponComp,
        WeaponState,
        WeaponReload,
        WeaponAmmo,

        UpgradeComp,
        UpgradeState,

        SupportComp,
        SupportState,

        AiData,
        Construct,
        ConstructFoci,
        TargetChange,
        RequestSetDps,
        RequestSetGuidance,
        RequestSetRof,
        RequestSetOverload,
        RequestSetRange,
        OverRidesUpdate,
        AimTargetUpdate,
        PaintedTargetUpdate,
        ClientMouseEvent,
        ActiveControlUpdate,
        PlayerIdUpdate,
        FocusUpdate,
        FocusLockUpdate,
        ReticleUpdate,
        CountingDownUpdate,
        CriticalReactionUpdate,
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
        QueueShot,
        PlayerState,
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
    [ProtoInclude(19, typeof(WeaponCompPacket))]
    [ProtoInclude(20, typeof(WeaponStatePacket))]
    [ProtoInclude(21, typeof(TargetPacket))]
    [ProtoInclude(22, typeof(ConstructPacket))]
    [ProtoInclude(23, typeof(ConstructFociPacket))]
    [ProtoInclude(24, typeof(FloatUpdatePacket))]
    [ProtoInclude(25, typeof(ClientNotifyPacket))]
    [ProtoInclude(26, typeof(ServerPacket))]
    [ProtoInclude(27, typeof(WeaponReloadPacket))]
    // empty 28
    [ProtoInclude(29, typeof(WeaponAmmoPacket))]
    [ProtoInclude(30, typeof(UpgradeCompPacket))]
    [ProtoInclude(31, typeof(UpgradeStatePacket))]
    [ProtoInclude(32, typeof(SupportCompPacket))]
    [ProtoInclude(33, typeof(SupportStatePacket))]
    [ProtoInclude(34, typeof(EwaredBlocksPacket))]

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
    public class OverRidesPacket : Packet
    {
        [ProtoMember(1)] internal ProtoWeaponOverrides Data;
        [ProtoMember(2), DefaultValue("")] internal string GroupName = "";
        [ProtoMember(3), DefaultValue("")] internal string Setting = "";
        [ProtoMember(4)] internal int Value;

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
        [ProtoMember(1)] internal ProtoWeaponTransferTarget Target;

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

        public override void CleanUp()
        {
            base.CleanUp();
            Data = null;
        }
    }

    [ProtoContract]
    public class AmmoCycleRequestPacket : Packet
    {
        [ProtoMember(1)] internal int PartId;
        [ProtoMember(2)] internal int NewAmmoId;
        [ProtoMember(3), DefaultValue(-1)] internal long PlayerId;


        public override void CleanUp()
        {
            base.CleanUp();
            PartId = 0;
            NewAmmoId = 0;
            PlayerId = -1;
        }
    }

    [ProtoContract]
    public class QueuedShotPacket : Packet
    {
        [ProtoMember(1)] internal int PartId;
        [ProtoMember(2), DefaultValue(-1)] internal long PlayerId;


        public override void CleanUp()
        {
            base.CleanUp();
            PartId = 0;
            PlayerId = -1;
        }
    }

    [ProtoContract]
    public class EwaredBlocksPacket : Packet
    {
        [ProtoMember(1)] internal List<EwarValues> Data = new List<EwarValues>(32);

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
        [ProtoMember(1)] internal ProtoWeaponAmmo Data;
        [ProtoMember(2)] internal int PartId;


        public override void CleanUp()
        {
            base.CleanUp();
            Data = null;
            PartId = 0;
        }
    }

    [ProtoContract]
    public class PlayerControlRequestPacket : Packet
    {
        [ProtoMember(1)] internal long PlayerId;
        [ProtoMember(2)] internal ProtoWeaponState.ControlMode Mode;

        public override void CleanUp()
        {
            base.CleanUp();
            Mode = ProtoWeaponState.ControlMode.None;
            PlayerId = -1;
        }
    }

    [ProtoContract]
    public class WeaponReloadPacket : Packet
    {
        [ProtoMember(1)] internal ProtoWeaponReload Data;
        [ProtoMember(2)] internal int PartId;

        public override void CleanUp()
        {
            base.CleanUp();
            Data = null;
            PartId = 0;
        }
    }

    [ProtoContract]
    public class WeaponCompPacket : Packet
    {
        [ProtoMember(1)] internal ProtoWeaponComp Data;

        public override void CleanUp()
        {
            base.CleanUp();
            Data = null;
        }
    }

    [ProtoContract]
    public class WeaponStatePacket : Packet
    {
        [ProtoMember(1)] internal ProtoWeaponState Data;

        public override void CleanUp()
        {
            base.CleanUp();
            Data = null;
        }
    }

    [ProtoContract]
    public class UpgradeCompPacket : Packet
    {
        [ProtoMember(1)] internal ProtoUpgradeComp Data;

        public override void CleanUp()
        {
            base.CleanUp();
            Data = null;
        }
    }

    [ProtoContract]
    public class UpgradeStatePacket : Packet
    {
        [ProtoMember(1)] internal ProtoUpgradeState Data;

        public override void CleanUp()
        {
            base.CleanUp();
            Data = null;
        }
    }

    [ProtoContract]
    public class SupportCompPacket : Packet
    {
        [ProtoMember(1)] internal ProtoSupportComp Data;

        public override void CleanUp()
        {
            base.CleanUp();
            Data = null;
        }
    }

    [ProtoContract]
    public class SupportStatePacket : Packet
    {
        [ProtoMember(1)] internal ProtoSupportState Data;

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

        public override void CleanUp()
        {
            base.CleanUp();
            State = Change.Update;
        }
    }

    [ProtoContract]
    public class ShootStatePacket : Packet
    {
        [ProtoMember(1)] internal TriggerActions Action = TriggerActions.TriggerOff;
        [ProtoMember(2), DefaultValue(-1)] internal long PlayerId = -1;

        public override void CleanUp()
        {
            base.CleanUp();
            Action = TriggerActions.TriggerOff;
            PlayerId = -1;
        }
    }

    #endregion

}
