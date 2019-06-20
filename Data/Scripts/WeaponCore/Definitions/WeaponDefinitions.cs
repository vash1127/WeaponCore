using System.Collections.Generic;
using ProtoBuf;
using VRage.Utils;
using VRageMath;

namespace WeaponCore.Support
{
    [ProtoContract]
    public struct GraphicDefinition
    {
        public enum EffectType
        {
            Spark,
            Lance,
            Orb,
            Custom
        }
        [ProtoMember(1)] internal bool ShieldHitDraw;
        [ProtoMember(2)] internal bool ProjectileTrail;
        [ProtoMember(3)] internal bool ParticleTrail;
        [ProtoMember(4)] internal float ProjectileWidth;
        [ProtoMember(5)] internal float VisualProbability;
        [ProtoMember(6)] internal float ParticleRadiusMultiplier;
        [ProtoMember(7)] internal string ProjectileMaterial;
        [ProtoMember(8)] internal string ModelName;
        [ProtoMember(9)] internal Vector4 ProjectileColor;
        [ProtoMember(10)] internal Vector4 ParticleColor;
        [ProtoMember(11)] internal EffectType Effect;
        [ProtoMember(12)] internal string CustomEffect;
    }

    [ProtoContract]
    public struct AudioDefinition
    {
        [ProtoMember(1)] internal float AmmoTravelRange;
        [ProtoMember(2)] internal float AmmoTravelVolume;
        [ProtoMember(3)] internal float AmmoTravelPitchVar;
        [ProtoMember(4)] internal float AmmoTravelVolumeVar;
        [ProtoMember(5)] internal float AmmoHitRange;
        [ProtoMember(6)] internal float AmmoHitVolume;
        [ProtoMember(7)] internal float AmmoHitPitchVar;
        [ProtoMember(8)] internal float AmmoHitVolumeVar;
        [ProtoMember(9)] internal float ReloadRange;
        [ProtoMember(10)] internal float ReloadVolume;
        [ProtoMember(11)] internal float FiringRange;
        [ProtoMember(12)] internal float FiringVolume;
        [ProtoMember(13)] internal float FiringPitchVar;
        [ProtoMember(14)] internal float FiringVolumeVar;
        [ProtoMember(15)] internal string AmmoHitSound;
        [ProtoMember(16)] internal string ReloadSound;
        [ProtoMember(17)] internal string NoAmmoSound;
        [ProtoMember(18)] internal string AmmoTravelSound;
        [ProtoMember(19)] internal string TurretRotationSound;
        [ProtoMember(20)] internal string FiringSoundStart;
        [ProtoMember(21)] internal string FiringSoundLoop;
        [ProtoMember(22)] internal string FiringSoundEnd;
    }

    [ProtoContract]
    public struct TurretDefinition
    {
        [ProtoMember(1)] internal KeyValuePair<string, string>[] MountPoints;
        [ProtoMember(2)] internal string[] Barrels;
        [ProtoMember(3)] internal string DefinitionId;
        [ProtoMember(4)] internal string AmmoMagazineId;
        [ProtoMember(5)] internal bool TurretMode;
        [ProtoMember(6)] internal bool TrackTarget;
        [ProtoMember(7)] internal int RotateBarrelAxis;
        [ProtoMember(8)] internal int ReloadTime;
        [ProtoMember(9)] internal int RateOfFire;
        [ProtoMember(10)] internal int BarrelsPerShot;
        [ProtoMember(11)] internal int SkipBarrels;
        [ProtoMember(12)] internal int ShotsPerBarrel;
        [ProtoMember(13)] internal int HeatPerRoF;
        [ProtoMember(14)] internal int MaxHeat;
        [ProtoMember(15)] internal int HeatSinkRate;
        [ProtoMember(16)] internal int MuzzleFlashLifeSpan;
        [ProtoMember(17)] internal float RotateSpeed;
        [ProtoMember(18)] internal float ElevationSpeed;
        [ProtoMember(19)] internal float DeviateShotAngle;
        [ProtoMember(20)] internal float ReleaseTimeAfterFire;
        [ProtoMember(21)] internal float ShotEnergyCost;
        [ProtoMember(22)] internal double AimingTolerance;
    }

    [ProtoContract]
    public struct AmmoDefinition
    {
        internal enum GuidanceType
        {
            None,
            Remote,
            TravelTo,
            Smart
        }

        internal enum ShieldType
        {
            Bypass,
            Emp,
            Energy,
            Kinetic
        }

        [ProtoMember(1)] internal bool UseRandomizedRange;
        [ProtoMember(2)] internal bool RealisticDamage;
        [ProtoMember(3)] internal bool DetonateOnEnd;
        [ProtoMember(4)] internal float Mass;
        [ProtoMember(5)] internal float Health;
        [ProtoMember(6)] internal float ProjectileLength;
        [ProtoMember(7)] internal float InitalSpeed;
        [ProtoMember(8)] internal float AccelPerSec;
        [ProtoMember(9)] internal float DesiredSpeed;
        [ProtoMember(10)] internal float SpeedVariance;
        [ProtoMember(11)] internal float MaxTrajectory;
        [ProtoMember(12)] internal float BackkickForce;
        [ProtoMember(13)] internal float RangeMultiplier;
        [ProtoMember(14)] internal float ThermalDamage;
        [ProtoMember(15)] internal float AreaEffectYield;
        [ProtoMember(16)] internal float AreaEffectRadius;
        [ProtoMember(17)] internal float ShieldDmgMultiplier;
        [ProtoMember(18)] internal float DefaultDamage;
        [ProtoMember(19)] internal ShieldType ShieldDamage;
        [ProtoMember(20)] internal GuidanceType Guidance;
    }

    [ProtoContract]
    public struct WeaponDefinition
    {
        [ProtoMember(1)] internal bool HasAreaEffect;
        [ProtoMember(2)] internal bool HasThermalEffect;
        [ProtoMember(3)] internal bool HasKineticEffect;
        [ProtoMember(4)] internal bool SkipAcceleration;
        [ProtoMember(5)] internal float KeenScaler;
        [ProtoMember(6)] internal float ComputedBaseDamage;
        [ProtoMember(7)] internal TurretDefinition TurretDef;
        [ProtoMember(8)] internal AmmoDefinition AmmoDef;
        [ProtoMember(9)] internal GraphicDefinition GraphicDef;
        [ProtoMember(10)] internal AudioDefinition AudioDef;
        [ProtoMember(11)] internal string ModPath;
    }
}
