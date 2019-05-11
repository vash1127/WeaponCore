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
            Orb
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
        internal bool IsExplosive;
        internal bool UseRandomizedRange;
        internal bool ShieldHitDraw;
        internal bool Trail;
        internal uint MaxTicks;
        internal int RotateBarrelAxis; 
        internal int ReloadTime;
        internal int RateOfFire;
        internal int BarrelsPerShot;
        internal int ShotsPerBarrel;
        internal int HeatPerRoF;
        internal int MaxHeat;
        internal int HeatSinkRate;
        internal int MuzzleFlashLifeSpan;
        internal float ShieldDmgMultiplier;
        internal float Mass;
        internal float Health;
        internal float ShotLength;
        internal float DesiredSpeed;
        internal float SpeedVariance;
        internal float MaxTrajectory;
        internal float BackkickForce;
        internal float DeviateShotAngle;
        internal float ReleaseTimeAfterFire;
        internal float RangeMultiplier;
        internal float ExplosiveYield;
        internal MyStringHash PhysicalMaterial;
        internal Vector4 TrailColor;
        internal Vector4 ParticleColor;
        internal ShieldType ShieldDamage;
        internal AmmoType Ammo;
        internal EffectType Effect;
        internal GuidanceType Guidance;
        internal MySoundPair AmmoSound;
        internal MySoundPair ReloadSound;
        internal MySoundPair SecondarySound;
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
        public readonly bool WeaponOnBase;
        public readonly bool MultiParts;

        public WeaponStructure(KeyValuePair<string, TurretDefinition> tDef, Dictionary<string, WeaponDefinition> wDef, Dictionary<string, BarrelGroup> bDef)
        {
            var map = tDef.Value.TurretMap;
            var numOfParts = map.Count;
            MultiParts = numOfParts > 1;

            var names = new MyStringHash[numOfParts];
            var hasWeaponOnbase = map.ContainsKey("TurretBase");
            var weaponFoundOnbase = false;
            var mapIndex = hasWeaponOnbase ? 1 : 0;
            WeaponSystems = new Dictionary<MyStringHash, WeaponSystem>(MyStringHash.Comparer);
            foreach (var w in map)
            {
                if (hasWeaponOnbase)
                {
                    var isBase = w.Key == "TurretBase";
                    if (isBase)
                    {
                        weaponFoundOnbase = true;
                        mapIndex = 0;
                    }
                }
                var myNameHash = MyStringHash.GetOrCompute(w.Key);
                names[mapIndex] = myNameHash;
                var myBarrels = bDef[w.Value.BarrelGroup].Barrels;
                var barrelStrings = new string[myBarrels.Count];
                for (int i = 0; i < myBarrels.Count; i++)
                    barrelStrings[i] = myBarrels[i];
                var weaponTypeName = w.Value.WeaponType;
                var weaponDef = wDef[weaponTypeName];
                WeaponSystems.Add(myNameHash, new WeaponSystem(myNameHash, weaponDef, weaponTypeName, barrelStrings));

                mapIndex++;
            }
            PartNames = names;
            WeaponOnBase = weaponFoundOnbase;
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
