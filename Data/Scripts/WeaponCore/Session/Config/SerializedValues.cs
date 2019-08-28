using System.ComponentModel;
using ProtoBuf;
using VRageMath;
namespace WeaponCore
{
    [ProtoContract]
    public class LogicStateValues
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
    public class WeaponStateValues
    {
        [ProtoMember(1)] public int Heat;
    }

    [ProtoContract]
    public class LogicSettingsValues
    {
        [ProtoMember(1)] public uint MId;
        [ProtoMember(2)] public bool Guidance = true;
        [ProtoMember(3)] public int Overload = 1;
        [ProtoMember(4)] public long Modes;
        [ProtoMember(5)] public float DPSModifier = 1;
        [ProtoMember(6)] public float ROFModifier = 1;
        [ProtoMember(7)] public WeaponSettingsValues[] Weapons;
    }

    [ProtoContract]
    public class WeaponSettingsValues
    {
        [ProtoMember(1)] public bool Enable = true;
    }


    [ProtoContract]
    public class WeaponHitValues
    {
        [ProtoMember(1)] public Vector3D HitPos;
        [ProtoMember(2)] public string Effect;
        [ProtoMember(3)] public float Size;
    }
}
