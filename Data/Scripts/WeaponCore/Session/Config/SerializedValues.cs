﻿using System.ComponentModel;
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


    }

    [ProtoContract]
    public class WeaponStateValues
    {
        [ProtoMember(1)] public float Heat;
        [ProtoMember(2)] public int CurrentAmmo;
        [ProtoMember(3)] public MyFixedPoint CurrentMags;
        [ProtoMember(4)] public int ShotsFired = 1;
        [ProtoMember(5)] public TerminalActionState ManualShoot = TerminalActionState.ShootOff;
    }

    [ProtoContract]
    public class WeaponSettingsValues
    {
        [ProtoMember(1)] public bool Enable = true;
    }

    [ProtoContract]
    public class CompGroupOverrides
    {
        [ProtoMember(1), DefaultValue(1)] public int Activate = 1;
        [ProtoMember(2)] public int Neutral = 0;
        [ProtoMember(3)] public int Friend = 0;
        [ProtoMember(4)] public int ManualAim = 0;
        [ProtoMember(5)] public int ManualFire = 0;
        [ProtoMember(6)] public int FocusTargets = 0;
        [ProtoMember(7)] public int FocusSubSystem = 0;
        [ProtoMember(8)] public int SubSystem = 0;
    }
}
