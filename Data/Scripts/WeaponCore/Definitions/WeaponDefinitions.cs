using System.Collections.Generic;
using ProtoBuf;
using VRageMath;

namespace WeaponCore.Support
{
    [ProtoContract]
    public struct WeaponDefinition
    {
        [ProtoMember(1)] internal HardPointDefinition HardPoint;
        [ProtoMember(2)] internal AmmoDefinition Ammo;
        [ProtoMember(3)] internal GraphicDefinition Graphics;
        [ProtoMember(4)] internal AudioDefinition Audio;
        [ProtoMember(5)] internal ModelAssignments Assignments;
        [ProtoMember(6)] internal UiDefinition Ui;
        [ProtoMember(7)] internal DamageScaleDefinition DamageScales;
        [ProtoMember(8)] internal TargetingDefinition Targeting;
        [ProtoMember(9)] internal string ModPath;
        [ProtoMember(10)] internal AnimationDefinition Animations;
    }


    [ProtoContract]
    public struct ModelAssignments
    {
        [ProtoMember(1)] internal MountPoint[] MountPoints;
        [ProtoMember(2)] internal string[] Barrels;
    }

    [ProtoContract]
    public struct UiDefinition
    {
        [ProtoMember(1)] internal bool RateOfFire;
        [ProtoMember(2)] internal bool DamageModifier;
        [ProtoMember(3)] internal bool ToggleGuidance;
        [ProtoMember(4)] internal bool EnableOverload;
    }

    [ProtoContract]
    public struct HardPointDefinition
    {
        public enum Prediction
        {
            Off,
            Basic,
            Accurate,
            Advanced,
        }

        [ProtoMember(1)] internal string WeaponId;
        [ProtoMember(2)] internal string AmmoMagazineId;
        [ProtoMember(3)] internal bool IsTurret;
        [ProtoMember(4)] internal bool TurretController;
        [ProtoMember(5)] internal bool TrackTargets;
        [ProtoMember(6)] internal bool Hybrid;
        [ProtoMember(7)] internal int DelayCeaseFire;
        [ProtoMember(8)] internal int RotateBarrelAxis;
        [ProtoMember(9)] internal int EnergyPriority;
        [ProtoMember(10)] internal int GridWeaponCap;
        [ProtoMember(11)] internal double RotateSpeed;
        [ProtoMember(12)] internal double ElevationSpeed;
        [ProtoMember(13)] internal float DeviateShotAngle;
        [ProtoMember(14)] internal float EnergyCost;
        [ProtoMember(15)] internal double AimingTolerance;
        [ProtoMember(16)] internal Prediction AimLeadingPrediction;
        [ProtoMember(17)] internal AmmoLoading Loading;
    }

    [ProtoContract]
    public struct AmmoLoading
    {
        [ProtoMember(1)] internal int ReloadTime;
        [ProtoMember(2)] internal int RateOfFire;
        [ProtoMember(3)] internal int BarrelsPerShot;
        [ProtoMember(4)] internal int SkipBarrels;
        [ProtoMember(5)] internal int TrajectilesPerBarrel;
        [ProtoMember(6)] internal int HeatPerShot;
        [ProtoMember(7)] internal int MaxHeat;
        [ProtoMember(8)] internal int HeatSinkRate;
        [ProtoMember(9)] internal float Cooldown;
        [ProtoMember(10)] internal int DelayUntilFire;
        [ProtoMember(11)] internal int ShotsInBurst;
        [ProtoMember(12)] internal int DelayAfterBurst;
        [ProtoMember(13)] internal bool DegradeROF;
    }

    [ProtoContract]
    public struct MountPoint
    {
        [ProtoMember(1)] internal string SubtypeId;
        [ProtoMember(2)] internal string AimPartId;
        [ProtoMember(3)] internal string MuzzlePartId;
    }

    [ProtoContract]
    public struct TargetingDefinition
    {
        public enum Threat
        {
            Projectiles,
            Characters,
            Grids,
            Neutrals,
            Meteors,
            Other
        }

        public enum BlockTypes
        {
            Any,
            Offense,
            Defense,
            Power,
            Production,
            Navigation
        }

        [ProtoMember(1)] internal int TopTargets;
        [ProtoMember(2)] internal int TopBlocks;
        [ProtoMember(3)] internal double StopTrackingSpeed;
        [ProtoMember(4)] internal float MinimumDiameter;
        [ProtoMember(5)] internal float MaximumDiameter;
        [ProtoMember(6)] internal bool ClosestFirst;
        [ProtoMember(7)] internal BlockTypes[] SubSystems;
        [ProtoMember(8)] internal Threat[] Threats;
    }

    [ProtoContract]
    public struct AmmoDefinition
    {
        [ProtoMember(1)] internal float BaseDamage;
        [ProtoMember(2)] internal float Mass;
        [ProtoMember(3)] internal float Health;
        [ProtoMember(4)] internal float BackKickForce;
        [ProtoMember(5)] internal ShapeDefinition Shape;
        [ProtoMember(6)] internal ObjectsHit ObjectsHit;
        [ProtoMember(7)] internal AmmoTrajectory Trajectory;
        [ProtoMember(8)] internal AreaDamage AreaEffect;
        [ProtoMember(9)] internal BeamDefinition Beams;
        [ProtoMember(10)] internal Shrapnel Shrapnel;
    }

    [ProtoContract]
    public struct ShapeDefinition
    {
        public enum Shapes
        {
            Line,
            Sphere,
        }

        [ProtoMember(1)] internal Shapes Shape;
        [ProtoMember(2)] internal double Diameter;
    }

    [ProtoContract]
    public struct ObjectsHit
    {
        [ProtoMember(1)] internal int MaxObjectsHit;
        [ProtoMember(2)] internal bool CountBlocks;
    }

    [ProtoContract]
    public struct BeamDefinition
    {
        [ProtoMember(1)] internal bool Enable;
        [ProtoMember(2)] internal bool ConvergeBeams;
        [ProtoMember(3)] internal bool VirtualBeams;
        [ProtoMember(4)] internal bool RotateRealBeam;
        [ProtoMember(5)] internal bool OneParticle;
    }

    [ProtoContract]
    public struct AreaDamage
    {
        public enum AreaEffectType
        {
            Disabled,
            Explosive,
            Radiant,
            AntiSmart,
            JumpNullField,
            EnergySinkField,
            AnchorField,
            EmpField,
            OffenseField,
            NavField,
        }

        [ProtoMember(1)] internal double AreaEffectRadius;
        [ProtoMember(2)] internal float AreaEffectDamage;
        [ProtoMember(3)] internal Pulse Pulse;
        [ProtoMember(4)] internal AreaEffectType AreaEffect;
        [ProtoMember(5)] internal Detonate Detonation;
        [ProtoMember(6)] internal Explosion Explosions;
        [ProtoMember(7)] internal EwarFields EwarFields;
    }

    [ProtoContract]
    public struct Pulse
    {
        [ProtoMember(1)] internal int Interval;
        [ProtoMember(2)] internal int PulseChance;
    }

    [ProtoContract]
    public struct EwarFields
    {
        [ProtoMember(1)] internal int Duration;
        [ProtoMember(2)] internal bool StackDuration;
        [ProtoMember(3)] internal bool Depletable;
    }

    [ProtoContract]
    public struct Detonate
    {
        [ProtoMember(1)] internal bool DetonateOnEnd;
        [ProtoMember(2)] internal bool ArmOnlyOnHit;
        [ProtoMember(3)] internal float DetonationRadius;
        [ProtoMember(4)] internal float DetonationDamage;
    }

    [ProtoContract]
    public struct Explosion
    {
        [ProtoMember(1)] internal bool NoVisuals;
        [ProtoMember(2)] internal bool NoSound;
        [ProtoMember(3)] internal float Scale;
        [ProtoMember(4)] internal string CustomParticle;
        [ProtoMember(5)] internal string CustomSound;
    }

    [ProtoContract]
    public struct AmmoTrajectory
    {
        internal enum GuidanceType
        {
            None,
            Remote,
            TravelTo,
            Smart,
            PulseDetect
        }

        [ProtoMember(1)] internal float MaxTrajectory;
        [ProtoMember(2)] internal float AccelPerSec;
        [ProtoMember(3)] internal float DesiredSpeed;
        [ProtoMember(4)] internal float TargetLossDegree;
        [ProtoMember(5)] internal int TargetLossTime;
        [ProtoMember(6)] internal int RestTime;
        [ProtoMember(7)] internal Randomize SpeedVariance;
        [ProtoMember(8)] internal Randomize RangeVariance;
        [ProtoMember(9)] internal GuidanceType Guidance;
        [ProtoMember(10)] internal Smarts Smarts;
    }

    [ProtoContract]
    public struct Smarts
    {
        [ProtoMember(1)] internal double Inaccuracy;
        [ProtoMember(2)] internal double Aggressiveness;
        [ProtoMember(3)] internal double MaxLateralThrust;
        [ProtoMember(4)] internal double TrackingDelay;
        [ProtoMember(5)] internal int MaxChaseTime;
        [ProtoMember(6)] internal bool OverideTarget;
    }

    [ProtoContract]
    public struct Shrapnel
    {
        internal enum ShrapnelShape
        {
            Cone,
            HalfMoon,
            FullMoon,
        }

        [ProtoMember(1)] internal float BaseDamage;
        [ProtoMember(2)] internal int Fragments;
        [ProtoMember(3)] internal float MaxTrajectory;
        [ProtoMember(4)] internal bool NoAudioVisual;
        [ProtoMember(5)] internal bool NoGuidance;
        [ProtoMember(6)] internal ShrapnelShape Shape;
    }

    [ProtoContract]
    public struct GraphicDefinition
    {
        [ProtoMember(1)] internal bool ShieldHitDraw;
        [ProtoMember(2)] internal float VisualProbability;
        [ProtoMember(3)] internal string ModelName;
        [ProtoMember(4)] internal ParticleDefinition Particles;
        [ProtoMember(5)] internal LineDefinition Line;
        [ProtoMember(6)] internal EmissiveDefinition Emissive;
    }

    [ProtoContract]
    public struct ParticleDefinition
    {
        [ProtoMember(1)] internal Particle Ammo;
        [ProtoMember(2)] internal Particle Hit;
        [ProtoMember(3)] internal Particle Barrel1;
        [ProtoMember(4)] internal Particle Barrel2;
    }

    [ProtoContract]
    public struct Particle
    {
        [ProtoMember(1)] internal string Name;
        [ProtoMember(2)] internal Vector4 Color;
        [ProtoMember(3)] internal Vector3D Offset;
        [ProtoMember(4)] internal ParticleOptions Extras;
    }

    [ProtoContract]
    public struct ParticleOptions
    {
        [ProtoMember(1)] internal float Scale;
        [ProtoMember(2)] internal float MaxDistance;
        [ProtoMember(3)] internal float MaxDuration;
        [ProtoMember(4)] internal bool Loop;
        [ProtoMember(5)] internal bool Restart;
    }

    [ProtoContract]
    public struct LineDefinition
    {
        [ProtoMember(1)] internal bool Trail;
        [ProtoMember(2)] internal float Length;
        [ProtoMember(3)] internal float Width;
        [ProtoMember(4)] internal string Material;
        [ProtoMember(5)] internal Vector4 Color;
        [ProtoMember(6)] internal Randomize ColorVariance;
        [ProtoMember(7)] internal Randomize WidthVariance;
    }

    [ProtoContract]
    public struct EmissiveDefinition
    {
        [ProtoMember(1)] internal HeatingEmissive Heating;
        [ProtoMember(2)] internal TrackingEmissive Tracking;
        [ProtoMember(3)] internal FiringEmissive Firing;
        [ProtoMember(4)] internal ReloadingEmissive Reloading;
    }

    [ProtoContract]
    public struct HeatingEmissive
    {
        [ProtoMember(1)] internal bool Enable;
    }

    [ProtoContract]
    public struct TrackingEmissive
    {
        [ProtoMember(1)] internal bool Enable;
        [ProtoMember(2)] internal Vector4 Color;
    }

    [ProtoContract]
    public struct FiringEmissive
    {
        [ProtoMember(1)] internal bool Enable;
        [ProtoMember(2)] internal int Stages;
        [ProtoMember(3)] internal Vector4 Color;
    }

    [ProtoContract]
    public struct ReloadingEmissive
    {
        [ProtoMember(1)] internal bool Enable;
        [ProtoMember(2)] internal Vector4 Color;
    }

    [ProtoContract]
    public struct Randomize
    {
        [ProtoMember(1)] internal float Start;
        [ProtoMember(2)] internal float End;
    }

    [ProtoContract]
    public struct AudioDefinition
    {
        [ProtoMember(1)] internal AudioHardPointDefinition HardPoint;
        [ProtoMember(2)] internal AudioAmmoDefinition Ammo;
    }

    [ProtoContract]
    public struct AudioAmmoDefinition
    {
        [ProtoMember(1)] internal string TravelSound;
        [ProtoMember(2)] internal string HitSound;
    }

    [ProtoContract]
    public struct AudioHardPointDefinition
    {
        [ProtoMember(1)] internal string ReloadSound;
        [ProtoMember(2)] internal string NoAmmoSound;
        [ProtoMember(3)] internal string HardPointRotationSound;
        [ProtoMember(4)] internal string BarrelRotationSound;
        [ProtoMember(5)] internal string FiringSound;
        [ProtoMember(6)] internal bool FiringSoundPerShot;
    }

    [ProtoContract]
    public struct DamageScaleDefinition
    {
        [ProtoMember(1)] internal GridSizeDefinition Grids;
        [ProtoMember(2)] internal ArmorDefinition Armor;
        [ProtoMember(3)] internal float MaxIntegrity;
        [ProtoMember(4)] internal bool DamageVoxels;
        [ProtoMember(5)] internal ShieldDefinition Shields;
        [ProtoMember(6)] internal float Characters;
        [ProtoMember(7)] internal CustomScalesDefinition Custom;
    }

    [ProtoContract]
    public struct GridSizeDefinition
    {
        [ProtoMember(1)] internal float Large;
        [ProtoMember(2)] internal float Small;
    }

    [ProtoContract]
    public struct ArmorDefinition
    {
        [ProtoMember(1)] internal float Armor;
        [ProtoMember(2)] internal float Heavy;
        [ProtoMember(3)] internal float Light;
        [ProtoMember(4)] internal float NonArmor;
    }

    [ProtoContract]
    public struct CustomBlocksDefinition
    {
        [ProtoMember(1)] internal string SubTypeId;
        [ProtoMember(2)] internal float Modifier;
    }

    [ProtoContract]
    public struct CustomScalesDefinition
    {
        [ProtoMember(1)] internal CustomBlocksDefinition[] Types;
        [ProtoMember(2)] internal bool IgnoreAllOthers;
    }

    [ProtoContract]
    public struct ShieldDefinition
    {
        internal enum ShieldType
        {
            Bypass,
            Emp,
            Energy,
            Kinetic
        }

        [ProtoMember(1)] internal float Modifier;
        [ProtoMember(2)] internal ShieldType Type;
    }

    [ProtoContract]
    public struct AnimationDefinition
    {
        [ProtoMember(1)] internal PartAnimationSetDef[] WeaponAnimationSets;
    }

    [ProtoContract(IgnoreListHandling = true)]
    public struct PartAnimationSetDef
    {
        public enum EventOptions
        {
            Firing,
            Reloading,
            Overheated,
            Tracking,
            Locked,
            OnOff,
        }

        [ProtoMember(1)] internal string SubpartId;
        [ProtoMember(2)] internal string muzzle;
        [ProtoMember(3)] internal uint StartupDelay;
        [ProtoMember(4)] internal uint motionDelay;
        [ProtoMember(5)] internal EventOptions[] Reverse;
        [ProtoMember(6)] internal EventOptions[] Loop;
        [ProtoMember(7)] internal Dictionary<EventOptions, RelMove[]> EventMoveSets;

    }

    [ProtoContract]
    internal struct RelMove
    {
        public enum MoveType
        {
            Linear,
            ExpoDecay,
            ExpoGrowth,
            Delay,
            Show, //instant or fade
            Hide, //instant or fade
            Teleport 
        }

        [ProtoMember(1)] internal MoveType MovementType;
        [ProtoMember(2)] internal XYZ[] linearPoints;
        [ProtoMember(3)] internal XYZ rotation;
        [ProtoMember(4)] internal XYZ rotAroundCenter;
        [ProtoMember(5)] internal uint ticksToMove;
        [ProtoMember(6)] internal string CenterEmpty;
    }

    [ProtoContract]
    internal struct XYZ
    {
        [ProtoMember(1)] internal double x;
        [ProtoMember(2)] internal double y;
        [ProtoMember(3)] internal double z;
    }
}
