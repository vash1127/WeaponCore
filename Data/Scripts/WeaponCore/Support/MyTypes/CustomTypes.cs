using System;
using System.Collections.Generic;
using ProtoBuf;
using VRage.Game.Entity;
using VRage.Utils;
using VRageMath;
using static WeaponCore.Support.GraphicDefinition;

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
        [ProtoMember(7)] internal MyStringId ProjectileMaterial;
        [ProtoMember(8)] internal MyStringId ModelName;
        [ProtoMember(9)] internal Vector4 ProjectileColor;
        [ProtoMember(10)] internal Vector4 ParticleColor;
        [ProtoMember(11)] internal EffectType Effect;
        [ProtoMember(12)] internal string CustomEffect;
    }

    [ProtoContract]
    public struct AudioDefinition
    {
        [ProtoMember(1)] internal float AmmoTravelSoundRange;
        [ProtoMember(2)] internal float AmmoTravelSoundVolume;
        [ProtoMember(3)] internal float AmmoHitSoundRange;
        [ProtoMember(4)] internal float AmmoHitSoundVolume;
        [ProtoMember(5)] internal float ReloadSoundRange;
        [ProtoMember(6)] internal float ReloadSoundVolume;
        [ProtoMember(7)] internal float FiringSoundRange;
        [ProtoMember(8)] internal float FiringSoundVolume;
        [ProtoMember(9)] internal string AmmoTravelSound;
        [ProtoMember(10)] internal string AmmoHitSound;
        [ProtoMember(11)] internal string ReloadSound;
        [ProtoMember(12)] internal string FiringSound;
    }

    [ProtoContract]
    public struct TurretDefinition
    {
        [ProtoMember(1)] internal KeyValuePair<string, string>[] MountPoints;
        [ProtoMember(2)] internal string[] Barrels;
        [ProtoMember(3)] internal string DefinitionId;
        [ProtoMember(4)] internal bool TurretMode;
        [ProtoMember(5)] internal bool TrackTarget;
        [ProtoMember(6)] internal int RotateBarrelAxis;
        [ProtoMember(7)] internal int ReloadTime;
        [ProtoMember(8)] internal int RateOfFire;
        [ProtoMember(9)] internal int BarrelsPerShot;
        [ProtoMember(10)] internal int SkipBarrels;
        [ProtoMember(11)] internal int ShotsPerBarrel;
        [ProtoMember(12)] internal int HeatPerRoF;
        [ProtoMember(13)] internal int MaxHeat;
        [ProtoMember(14)] internal int HeatSinkRate;
        [ProtoMember(15)] internal int MuzzleFlashLifeSpan;
        [ProtoMember(16)] internal float RotateSpeed;
        [ProtoMember(17)] internal float DeviateShotAngle;
        [ProtoMember(18)] internal float ReleaseTimeAfterFire;

    }

    [ProtoContract]
    public struct AmmoDefinition
    {
        internal enum GuidanceType
        {
            None,
            Remote,
            Seeking,
            Lock,
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
        [ProtoMember(3)] internal float Mass;
        [ProtoMember(4)] internal float Health;
        [ProtoMember(5)] internal float ProjectileLength;
        [ProtoMember(6)] internal float InitalSpeed;
        [ProtoMember(7)] internal float AccelPerSec;
        [ProtoMember(8)] internal float DesiredSpeed;
        [ProtoMember(9)] internal float SpeedVariance;
        [ProtoMember(10)] internal float MaxTrajectory;
        [ProtoMember(11)] internal float BackkickForce;
        [ProtoMember(12)] internal float RangeMultiplier;
        [ProtoMember(13)] internal float ThermalDamage;
        [ProtoMember(14)] internal float AreaEffectYield;
        [ProtoMember(15)] internal float AreaEffectRadius;
        [ProtoMember(16)] internal float ShieldDmgMultiplier;
        [ProtoMember(17)] internal float DefaultDamage;
        [ProtoMember(18)] internal ShieldType ShieldDamage;
        [ProtoMember(19)] internal GuidanceType Guidance;
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
    }

    public struct WeaponSystem
    {
        public readonly MyStringHash PartName;
        public readonly WeaponDefinition WeaponType;
        public readonly string WeaponName;
        public readonly string[] Barrels;

        public WeaponSystem(MyStringHash partName, WeaponDefinition weaponType, string weaponName)
        {
            PartName = partName;
            WeaponType = weaponType;
            Barrels = weaponType.TurretDef.Barrels;
            WeaponName = weaponName;
        }
    }

    public struct WeaponStructure
    {
        public readonly Dictionary<MyStringHash, WeaponSystem> WeaponSystems;
        public readonly MyStringHash[] PartNames;
        public readonly bool MultiParts;

        public WeaponStructure(KeyValuePair<string, Dictionary<string, string>> tDef, List<WeaponDefinition> wDef)
        {
            var map = tDef.Value;
            var numOfParts = wDef.Count;
            MultiParts = numOfParts > 1;
            var names = new MyStringHash[numOfParts];
            var mapIndex = 0;
            WeaponSystems = new Dictionary<MyStringHash, WeaponSystem>(MyStringHash.Comparer);
            foreach (var w in map)
            {
                var myNameHash = MyStringHash.GetOrCompute(w.Key);
                names[mapIndex] = myNameHash;

                var weaponTypeName = w.Value;
                var weaponDef = new WeaponDefinition();

                foreach (var weapon in wDef)
                    if (weapon.TurretDef.DefinitionId == weaponTypeName) weaponDef = weapon;

                weaponDef.TurretDef.DeviateShotAngle = MathHelper.ToRadians(weaponDef.TurretDef.DeviateShotAngle);
                weaponDef.HasAreaEffect = weaponDef.AmmoDef.AreaEffectYield > 0 && weaponDef.AmmoDef.AreaEffectRadius > 0;
                weaponDef.SkipAcceleration = weaponDef.AmmoDef.AccelPerSec > 0;
                if (weaponDef.AmmoDef.RealisticDamage)
                {
                    weaponDef.HasKineticEffect = weaponDef.AmmoDef.Mass > 0 && weaponDef.AmmoDef.DesiredSpeed > 0;
                    weaponDef.HasThermalEffect = weaponDef.AmmoDef.ThermalDamage > 0;
                    var kinetic = ((weaponDef.AmmoDef.Mass / 2) * (weaponDef.AmmoDef.DesiredSpeed * weaponDef.AmmoDef.DesiredSpeed) / 1000) * weaponDef.KeenScaler;
                    weaponDef.ComputedBaseDamage = kinetic + weaponDef.AmmoDef.ThermalDamage;
                }
                else weaponDef.ComputedBaseDamage = weaponDef.AmmoDef.DefaultDamage; // For the unbelievers. 

                WeaponSystems.Add(myNameHash, new WeaponSystem(myNameHash, weaponDef, weaponTypeName));

                mapIndex++;
            }
            PartNames = names;
        }
    }

    public class Shrinking
    {
        internal WeaponDefinition WepDef;
        internal Vector3D Position;
        internal Vector3D Direction;
        internal double Length;
        internal int ReSizeSteps;
        internal double LineReSizeLen;
        internal int ShrinkStep;

        internal void Init(WeaponDefinition wepDef, LineD line, int reSizeSteps, double lineReSizeLen)
        {
            WepDef = wepDef;
            Position = line.To;
            Direction = line.Direction;
            Length = line.Length;
            ReSizeSteps = reSizeSteps;
            LineReSizeLen = lineReSizeLen;
            ShrinkStep = reSizeSteps;
        }

        internal LineD? GetLine()
        {
            if (ShrinkStep-- <= 0) return null;
            return new LineD(Position + -(Direction * (ShrinkStep * LineReSizeLen)), Position);
        }
    }

    public struct WeaponHit
    {
        public readonly WeaponComponent Logic;
        public readonly Vector3D HitPos;
        public readonly float Size;
        public readonly EffectType Effect;

        public WeaponHit(WeaponComponent logic, Vector3D hitPos, float size, EffectType effect)
        {
            Logic = logic;
            HitPos = hitPos;
            Size = size;
            Effect = effect;
        }
    }

    public struct TargetInfo
    {
        public enum TargetType
        {
            Player,
            Grid,
            Other
        }

        public readonly MyEntity Entity;
        public readonly double Distance;
        public readonly float Size;
        public readonly TargetType Type;
        public TargetInfo(MyEntity entity, double distance, float size, TargetType type)
        {
            Entity = entity;
            Distance = distance;
            Size = size;
            Type = type;
        }
    }

    public struct BlockInfo
    {
        public enum BlockType
        {
            Player,
            Grid,
            Other
        }

        public readonly MyEntity Entity;
        public readonly double Distance;
        public readonly float Size;
        public readonly BlockType Type;
        public BlockInfo(MyEntity entity, double distance, float size, BlockType type)
        {
            Entity = entity;
            Distance = distance;
            Size = size;
            Type = type;
        }
    }
}
