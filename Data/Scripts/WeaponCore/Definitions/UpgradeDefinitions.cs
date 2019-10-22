using System.Collections.Generic;
using ProtoBuf;
using VRageMath;
using static WeaponCore.Platform.Weapon;

namespace WeaponCore.Support
{

    [ProtoContract]
    public struct UpgradeDefinition
    {
        [ProtoMember(1)] internal HardPointDefinition HardPoint;
        [ProtoMember(2)] internal AmmoDefinition Ammo;
        [ProtoMember(3)] internal GraphicDefinition Graphics;
        [ProtoMember(4)] internal AudioDefinition Audio;
        [ProtoMember(5)] internal ModelAssignments Assignments;
        [ProtoMember(6)] internal DamageScaleDefinition DamageScales;
        [ProtoMember(7)] internal TargetingDefinition Targeting;
        [ProtoMember(8)] internal string ModPath;
        [ProtoMember(9)] internal AnimationDefinition Animations;
    }
}
