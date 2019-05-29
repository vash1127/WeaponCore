using System.ComponentModel;
using ProtoBuf;
using VRageMath;
using static WeaponCore.Support.WeaponDefinition;
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
    }

    [ProtoContract]
    public class LogicSettingsValues
    {
        [ProtoMember(1)] public uint MId;
        [ProtoMember(2), DefaultValue(true)] public bool Guidance;
        [ProtoMember(3), DefaultValue(true)] public bool GetDoubleRate;
        [ProtoMember(4)] public long Modes;
        [ProtoMember(5)] public float PowerScale;
        [ProtoMember(6), DefaultValue(-1)] public int PowerWatts = 999;
    }

    [ProtoContract]
    public class WeaponHitValues
    {
        [ProtoMember(1)] public Vector3D HitPos;
        [ProtoMember(2)] public EffectType Effect;
        [ProtoMember(3)] public float Size;
    }
}
