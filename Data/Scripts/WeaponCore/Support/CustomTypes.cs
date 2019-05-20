using System.Collections.Generic;
using Sandbox.Game.Entities;
using VRage.Utils;
using VRageMath;
using static WeaponCore.Support.WeaponDefinition;

namespace WeaponCore.Support
{

    public struct TurretDefinition
    {
        public Dictionary<string, TurretParts> TurretMap;
    }

    public struct TurretParts
    {
        public readonly string WeaponType;
        public readonly string BarrelGroup;

        internal TurretParts(string weaponType, string barrelGroup)
        {
            WeaponType = weaponType;
            BarrelGroup = barrelGroup;
        }
    }
    
    public struct BarrelGroup
    {
        public List<string> Barrels;
    }

    public struct WeaponDefinition
    {
        internal enum AmmoType
        {
            Beam,
            Bolt,
            Missile
        }

        public enum EffectType
        {
            None,
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
            None,
            Bypass,
            Emp,
            Energy,
            Kinetic
        }

        internal bool TurretMode;
        internal bool TrackTarget;
        internal bool HasAreaEffect;
        internal bool HasThermalEffect;
        internal bool HasKineticEffect;
        internal bool SkipAcceleration;
        internal bool UseRandomizedRange;
        internal bool ShieldHitDraw;
        internal bool RealisticDamage;
        internal bool Trail;
        internal int RotateBarrelAxis; 
        internal int ReloadTime;
        internal int RateOfFire;
        internal int BarrelsPerShot;
        internal int SkipBarrels;
        internal int ShotsPerBarrel;
        internal int HeatPerRoF;
        internal int MaxHeat;
        internal int HeatSinkRate;
        internal int MuzzleFlashLifeSpan;
        internal float Mass;
        internal float Health;
        internal float ShotLength;
        internal float ShotWidth;
        internal float InitalSpeed;
        internal float AccelPerSec;
        internal float DesiredSpeed;
        internal float RotateSpeed;
        internal float SpeedVariance;
        internal float MaxTrajectory;
        internal float BackkickForce;
        internal float DeviateShotAngle;
        internal float ReleaseTimeAfterFire;
        internal float RangeMultiplier;
        internal float ThermalDamage;
        internal float KeenScaler;
        internal float AreaEffectYield;
        internal float AreaEffectRadius;
        internal float ShieldDmgMultiplier;
        internal float DefaultDamage;
        internal float ComputedBaseDamage;
        internal MyStringId PhysicalMaterial;
        internal MyStringId ModelName;
        internal Vector4 TrailColor;
        internal Vector4 ParticleColor;
        internal ShieldType ShieldDamage;
        internal AmmoType Ammo;
        internal EffectType Effect;
        internal GuidanceType Guidance;
        internal MySoundPair AmmoSound;
        internal MySoundPair ReloadSound;
        internal MySoundPair FiringSound;
    }

 
    public struct WeaponSystem
    {
        public readonly MyStringHash PartName;
        public readonly WeaponDefinition WeaponType;
        public readonly string WeaponName;
        public readonly string[] Barrels;

        public WeaponSystem(MyStringHash partName, WeaponDefinition weaponType, string weaponName, string[] barrels)
        {
            PartName = partName;
            WeaponType = weaponType;
            WeaponName = weaponName;
            Barrels = barrels;
        }
    }

    public struct WeaponStructure
    {
        public readonly Dictionary<MyStringHash, WeaponSystem> WeaponSystems;
        public readonly MyStringHash[] PartNames;
        public readonly bool MultiParts;

        public WeaponStructure(KeyValuePair<string, TurretDefinition> tDef, Dictionary<string, WeaponDefinition> wDef, Dictionary<string, BarrelGroup> bDef)
        {
            var map = tDef.Value.TurretMap;
            var numOfParts = map.Count;
            MultiParts = numOfParts > 1;

            var names = new MyStringHash[numOfParts];
            var mapIndex = 0;
            WeaponSystems = new Dictionary<MyStringHash, WeaponSystem>(MyStringHash.Comparer);
            foreach (var w in map)
            {
                var myNameHash = MyStringHash.GetOrCompute(w.Key);
                names[mapIndex] = myNameHash;
                var myBarrels = bDef[w.Value.BarrelGroup].Barrels;
                var barrelStrings = new string[myBarrels.Count];
                for (int i = 0; i < myBarrels.Count; i++)
                    barrelStrings[i] = myBarrels[i];
                var weaponTypeName = w.Value.WeaponType;

                var weaponDef = wDef[weaponTypeName];

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

                WeaponSystems.Add(myNameHash, new WeaponSystem(myNameHash, weaponDef, weaponTypeName, barrelStrings));

                mapIndex++;
            }
            PartNames = names;
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
}
