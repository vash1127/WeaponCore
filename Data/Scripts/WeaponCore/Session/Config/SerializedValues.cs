using System.ComponentModel;
using ProtoBuf;
using VRage;
using VRage.Game;
using VRageMath;
using WeaponCore.Support;
using static WeaponCore.Platform.Weapon;
using static WeaponCore.Support.TargetingDefinition;

namespace WeaponCore
{


    [ProtoContract]
    public class CompStateValues
    {
        [ProtoMember(1)] public uint MId;
        [ProtoMember(2), DefaultValue(-1)] public float PowerLevel;
        [ProtoMember(3)] public bool Online;
        [ProtoMember(4)] public bool Overload;
        [ProtoMember(5)] public bool Message;
        [ProtoMember(6)] public int Heat;
        [ProtoMember(7)] public WeaponStateValues[] Weapons;
        [ProtoMember(8), DefaultValue(-1)] public long PlayerIdInTerminal = -1;
        [ProtoMember(9)] public bool ShootOn;
        [ProtoMember(10)] public bool ClickShoot;
        [ProtoMember(11)] public PlayerControl ManualControl;

    }

    [ProtoContract]
    public class CompSettingsValues
    {
        [ProtoMember(1)] public uint MId;
        [ProtoMember(2)] public bool Guidance = true;
        [ProtoMember(3)] public int Overload = 1;
        [ProtoMember(4)] public long Modes;
        [ProtoMember(5)] public float DpsModifier = 1;
        [ProtoMember(6)] public float RofModifier = 1;
        [ProtoMember(7)] public WeaponSettingsValues[] Weapons;
        [ProtoMember(8)] public float Range = 100;
        [ProtoMember(9)] internal MyObjectBuilder_Inventory Inventory = null;
        [ProtoMember(10)] public CompGroupOverrides Overrides;

        public CompSettingsValues()
        {
            Overrides = new CompGroupOverrides();
        }

    }

    [ProtoContract]
    public class WeaponStateValues
    {
        [ProtoMember(1)] public float Heat;
        [ProtoMember(2)] public int CurrentAmmo;
        [ProtoMember(3)] public MyFixedPoint CurrentMags;
        [ProtoMember(4)] public int ShotsFired;
        [ProtoMember(5)] public TerminalActionState ManualShoot = TerminalActionState.ShootOff;
        [ProtoMember(6)] public int SingleShotCounter;
    }

    [ProtoContract]
    public class PlayerControl
    {
        [ProtoMember(1), DefaultValue(-1)] public long PlayerId = -1;
        [ProtoMember(2)] public ControlType CurrentControlType;
    }
    
    public enum ControlType
    {
        UI,
        Toolbar,
        None
    }

    [ProtoContract]
    public class WeaponSettingsValues
    {
        [ProtoMember(1)] public bool Enable = true;
    }

    [ProtoContract]
    public class CompGroupOverrides
    {
        [ProtoMember(1), DefaultValue(true)] public bool Activate = true;
        [ProtoMember(2)] public bool Neutrals = false;
        [ProtoMember(3)] public bool Unowned = false;
        [ProtoMember(4)] public bool Friendly = false;
        [ProtoMember(5)] public bool TargetPainter = false;
        [ProtoMember(6)] public bool ManaulControl = false;
        [ProtoMember(7)] public bool FocusTargets = false;
        [ProtoMember(8)] public bool FocusSubSystem = false;
        [ProtoMember(9)] public BlockTypes SubSystem = BlockTypes.Any;
        [ProtoMember(10)] public bool Meteors = false;
        [ProtoMember(11)] public bool Biologicals = false;
        [ProtoMember(12)] public bool Projectiles = false;
    }
}
