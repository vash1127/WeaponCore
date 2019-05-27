using System.Collections.Generic;
using ProtoBuf;
using VRage.Game.Entity;
using VRage.Utils;
using VRageMath;
using static WeaponCore.Support.WeaponDefinition;

namespace WeaponCore.Support
{
    [ProtoContract]
    public struct WeaponDefinition
    {
        public enum EffectType
        {
            Spark,
            Lance,
            Orb,
            Custom
        }

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

        [ProtoMember(1)] internal KeyValuePair<string, string>[] MountPoints;
        [ProtoMember(2)] internal string[] Barrels;
        [ProtoMember(3)] internal string DefinitionId;
        [ProtoMember(4)] internal bool TurretMode;
        [ProtoMember(5)] internal bool TrackTarget;
        [ProtoMember(6)] internal bool HasAreaEffect;
        [ProtoMember(7)] internal bool HasThermalEffect;
        [ProtoMember(8)] internal bool HasKineticEffect;
        [ProtoMember(9)] internal bool SkipAcceleration;
        [ProtoMember(10)] internal bool UseRandomizedRange;
        [ProtoMember(11)] internal bool ShieldHitDraw;
        [ProtoMember(12)] internal bool RealisticDamage;
        [ProtoMember(13)] internal bool LineTrail;
        [ProtoMember(14)] internal bool ParticleTrail;
        [ProtoMember(15)] internal int RotateBarrelAxis;
        [ProtoMember(16)] internal int ReloadTime;
        [ProtoMember(17)] internal int RateOfFire;
        [ProtoMember(18)] internal int BarrelsPerShot;
        [ProtoMember(19)] internal int SkipBarrels;
        [ProtoMember(20)] internal int ShotsPerBarrel;
        [ProtoMember(21)] internal int HeatPerRoF;
        [ProtoMember(22)] internal int MaxHeat;
        [ProtoMember(23)] internal int HeatSinkRate;
        [ProtoMember(24)] internal int MuzzleFlashLifeSpan;
        [ProtoMember(25)] internal float Mass;
        [ProtoMember(26)] internal float Health;
        [ProtoMember(27)] internal float LineLength;
        [ProtoMember(28)] internal float LineWidth;
        [ProtoMember(29)] internal float InitalSpeed;
        [ProtoMember(30)] internal float AccelPerSec;
        [ProtoMember(31)] internal float DesiredSpeed;
        [ProtoMember(32)] internal float RotateSpeed;
        [ProtoMember(33)] internal float SpeedVariance;
        [ProtoMember(34)] internal float MaxTrajectory;
        [ProtoMember(35)] internal float BackkickForce;
        [ProtoMember(36)] internal float DeviateShotAngle;
        [ProtoMember(37)] internal float ReleaseTimeAfterFire;
        [ProtoMember(38)] internal float RangeMultiplier;
        [ProtoMember(39)] internal float ThermalDamage;
        [ProtoMember(40)] internal float KeenScaler;
        [ProtoMember(41)] internal float AreaEffectYield;
        [ProtoMember(42)] internal float AreaEffectRadius;
        [ProtoMember(43)] internal float ShieldDmgMultiplier;
        [ProtoMember(44)] internal float DefaultDamage;
        [ProtoMember(45)] internal float ComputedBaseDamage;
        [ProtoMember(46)] internal float VisualProbability;
        [ProtoMember(47)] internal float ParticleRadiusMultiplier;
        [ProtoMember(48)] internal float AmmoTravelSoundRange;
        [ProtoMember(49)] internal float AmmoTravelSoundVolume;
        [ProtoMember(50)] internal float AmmoHitSoundRange;
        [ProtoMember(51)] internal float AmmoHitSoundVolume;
        [ProtoMember(52)] internal float ReloadSoundRange;
        [ProtoMember(53)] internal float ReloadSoundVolume;
        [ProtoMember(54)] internal float FiringSoundRange;
        [ProtoMember(55)] internal float FiringSoundVolume;
        [ProtoMember(56)] internal MyStringId PhysicalMaterial;
        [ProtoMember(57)] internal MyStringId ModelName;
        [ProtoMember(58)] internal Vector4 TrailColor;
        [ProtoMember(59)] internal Vector4 ParticleColor;
        [ProtoMember(60)] internal ShieldType ShieldDamage;
        [ProtoMember(61)] internal EffectType Effect;
        [ProtoMember(62)] internal GuidanceType Guidance;
        [ProtoMember(63)] internal string AmmoTravelSound;
        [ProtoMember(64)] internal string AmmoHitSound;
        [ProtoMember(65)] internal string ReloadSound;
        [ProtoMember(66)] internal string FiringSound;
        [ProtoMember(67)] internal string CustomEffect;
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
            Barrels = weaponType.Barrels;
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
            var numOfParts = map.Count;
            MultiParts = numOfParts > 1;

            var names = new MyStringHash[numOfParts];
            var mapIndex = 0;
            WeaponSystems = new Dictionary<MyStringHash, WeaponSystem>(MyStringHash.Comparer);
            foreach (var w in map)
            {
                var myNameHash = MyStringHash.GetOrCompute(w.Key);
                names[mapIndex] = myNameHash;

                var weaponTypeName = w.Value;
                WeaponDefinition weaponDef = new WeaponDefinition();

                foreach (var weapon in wDef)
                    if (weapon.DefinitionId == weaponTypeName) weaponDef = weapon;

                weaponDef.DeviateShotAngle = MathHelper.ToRadians(weaponDef.DeviateShotAngle);
                weaponDef.HasAreaEffect = weaponDef.AreaEffectYield > 0 && weaponDef.AreaEffectRadius > 0;
                weaponDef.SkipAcceleration = weaponDef.AccelPerSec > 0;
                if (weaponDef.RealisticDamage)
                {
                    weaponDef.HasKineticEffect = weaponDef.Mass > 0 && weaponDef.DesiredSpeed > 0;
                    weaponDef.HasThermalEffect = weaponDef.ThermalDamage > 0;
                    var kinetic = ((weaponDef.Mass / 2) * (weaponDef.DesiredSpeed * weaponDef.DesiredSpeed) / 1000) * weaponDef.KeenScaler;
                    weaponDef.ComputedBaseDamage = kinetic + weaponDef.ThermalDamage;
                }
                else weaponDef.ComputedBaseDamage = weaponDef.DefaultDamage; // For the unbelievers. 

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
        public readonly Logic Logic;
        public readonly Vector3D HitPos;
        public readonly float Size;
        public readonly EffectType Effect;

        public WeaponHit(Logic logic, Vector3D hitPos, float size, EffectType effect)
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
