using System.Collections;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using VRage.Utils;
using VRageMath;
using WeaponCore.Support;
using static WeaponCore.Support.WeaponDefinition.AmmoType;
using static WeaponCore.Support.WeaponDefinition.ShieldType;
using static WeaponCore.Support.WeaponDefinition.EffectType;
using static WeaponCore.Support.WeaponDefinition.GuidanceType;
namespace WeaponCore
{
    class ConfigMe
    {
        class TurretDefinition2 : IEnumerable
        {
            public TurretDefinition2(string name)
            {
            }

            public IEnumerator GetEnumerator()
            {
                return null;
            }

            public void Add(string name, string something, string somethingElse)
            {

            }
        }

        class BarrelDefinition2 : IEnumerable
        {
            public BarrelDefinition2(string name)
            {
            }

            public IEnumerator GetEnumerator()
            {
                return null;
            }

            public void Add(string name)
            {

            }
        }

        public void Test()
        {
            var bg1 = new BarrelDefinition2("BarrelGroup1");
            bg1.Add("test1");
            bg1.Add("test2");

            var turret = new TurretDefinition2("TurretType1");
            turret.Add("TurretSubPart1", "LargePulseBeam", "BarrelGroup1");
            turret.Add("TurretSubPart1", "LargePulseBeam", "BarrelGroup1");
        }


        internal Dictionary<string, TurretDefinition> TurretDefinitions = new Dictionary<string, TurretDefinition>()
        {
            ["TurretType1"] = new TurretDefinition() { TurretMap = new Dictionary<string, TurretParts>()
                {
                    ["TurretSubPart1"] = new TurretParts("LargePulseBeam", "BarrelGroup1"),
                    ["TurretSubPart2"] = new TurretParts("LargeGatling", "BarrelGroup2")
                },
            },
            ["TurretType2"] = new TurretDefinition() { TurretMap = new Dictionary<string, TurretParts>()
                {
                    ["TurretSubPart1"] = new TurretParts("LargeMissile", "BarrelGroup1"),
                },
            },
            ["PDCTurretLB"] = new TurretDefinition() { TurretMap = new Dictionary<string, TurretParts>()
                {
                    ["Boomsticks"] = new TurretParts("LargeGatling", "BarrelGroup2"),
                    ["MissileTurretBarrels"] = new TurretParts("LargeMissile", "BarrelGroup3")
                },
            }
        };

        internal Dictionary<string, BarrelGroup> BarrelDefinitions = new Dictionary<string, BarrelGroup>()
        {
            ["BarrelGroup1"] = new BarrelGroup() { Barrels = new List<string>()
                {
                    "muzzle_barrel_001",
                }
            },
            ["BarrelGroup2"] = new BarrelGroup() { Barrels = new List<string>()
                {
                    "muzzle_barrel_001",
                    "muzzle_barrel_002",
                    "muzzle_barrel_003",
                    "muzzle_barrel_004",
                    "muzzle_barrel_005",
                    "muzzle_barrel_006",
                }
            },
            ["BarrelGroup3"] = new BarrelGroup() { Barrels = new List<string>()
                {
                    "muzzle_missile_001",
                    "muzzle_missile_002",
                    "muzzle_missile_003",
                    "muzzle_missile_004",
                    "muzzle_missile_005",
                    "muzzle_missile_006",
                }
            },
        };

        internal Dictionary<string, WeaponDefinition> WeaponDefinitions = new Dictionary<string, WeaponDefinition>() {
            //Weapon1SubTyeId is the first SubtypeId in your block.sbc
            ["LargePulseBeam"] = new WeaponDefinition() {
                IsExplosive = false,
                UseRandomizedRange = true,
                ShieldHitDraw = true,
                Trail = true,
                TurretMode = true,
                TrackTarget = true,
                MaxTicks = 180,
                RotateBarrelAxis = 0,
                ReloadTime = 10,
                RateOfFire = 60,
                BarrelsPerShot = 1,
                SkipBarrels = 1,
                ShotsPerBarrel = 1,
                HeatPerRoF = 1,
                MaxHeat = 180,
                HeatSinkRate = 2,
                MuzzleFlashLifeSpan = 0,
                ShieldDmgMultiplier = 1.1f,
                Mass = 200.5f,
                Health = 201.1f,
                ShotLength = 15.6f,
                DesiredSpeed = 200f,
                SpeedVariance = 5f,
                MaxTrajectory = 8000.5f,
                BackkickForce = 15f,
                DeviateShotAngle = 1f,
                ReleaseTimeAfterFire = 10f,
                RangeMultiplier = 2.1f,
                ExplosiveYield = 10000.1f,
                PhysicalMaterial = MyStringId.GetOrCompute("ProjectileTrailLine"),
                TrailColor = new Vector4(1, 1, 1, 1),
                ParticleColor = new Vector4(1, 1, 1, 1),
                ShieldDamage = Kinetic,
                Ammo = Beam,
                Effect = Lance,
                Guidance = Smart,
                AmmoSound = new MySoundPair("cueName"),
                ReloadSound = new MySoundPair("cueName"),
                SecondarySound = new MySoundPair("cueName")
            },

            //Weapon2SubTyeId is the second SubtypeId in your block.sbc
            ["LargeGatling"] = new WeaponDefinition() {
                TurretMode = true,
                TrackTarget = true,
                IsExplosive = false,
                UseRandomizedRange = true,
                ShieldHitDraw = true,
                Trail = true,
                MaxTicks = 60,
                RotateBarrelAxis = 3, // 0 = off, 1 = xAxis, 2 = yAxis, 3 = zAxis
                ReloadTime = 10,
                RateOfFire = 3600,
                BarrelsPerShot = 6,
                SkipBarrels = 0,
                ShotsPerBarrel = 1,
                HeatPerRoF = 1,
                MaxHeat = 180,
                HeatSinkRate = 2,
                MuzzleFlashLifeSpan = 0,
                ShieldDmgMultiplier = 1.1f,
                Mass = 200.5f,
                Health = 201.1f,
                ShotLength = 4f,
                ShotWidth = 0.1f,
                DesiredSpeed = 100f,
                SpeedVariance = 5f,
                MaxTrajectory = 800f,
                BackkickForce = 2.5f,
                DeviateShotAngle = 10f,
                ReleaseTimeAfterFire = 10f,
                RangeMultiplier = 2.1f,
                ExplosiveYield = 10000.1f,
                PhysicalMaterial = MyStringId.GetOrCompute("ProjectileTrailLine"),
                TrailColor = new Vector4(255, 10, 0, 110f),
                ParticleColor = new Vector4(255, 0, 0, 175),
                ShieldDamage = Kinetic,
                Ammo = Bolt,
                Effect = Lance,
                Guidance = Smart,
                AmmoSound = new MySoundPair("cueName"),
                ReloadSound = new MySoundPair("cueName"),
                SecondarySound = new MySoundPair("cueName")
            },

            //Weapon3SubTyeId is the second SubtypeId in your block.sbc
            ["LargeMissile"] = new WeaponDefinition()
            {
                TurretMode = false,
                TrackTarget = false,
                IsExplosive = false,
                UseRandomizedRange = true,
                ShieldHitDraw = true,
                Trail = true,
                MaxTicks = 180,
                RotateBarrelAxis = 0,
                ReloadTime = 10,
                RateOfFire = 60,
                BarrelsPerShot = 1,
                SkipBarrels = 1,
                ShotsPerBarrel = 1,
                HeatPerRoF = 1,
                MaxHeat = 180,
                HeatSinkRate = 2,
                MuzzleFlashLifeSpan = 0,
                ShieldDmgMultiplier = 1.1f,
                Mass = 200.5f,
                Health = 201.1f,
                ShotLength = 15.6f,
                DesiredSpeed = 200f,
                SpeedVariance = 5f,
                MaxTrajectory = 8000.5f,
                BackkickForce = 15f,
                DeviateShotAngle = 1f,
                ReleaseTimeAfterFire = 10f,
                RangeMultiplier = 2.1f,
                ExplosiveYield = 10000.1f,
                PhysicalMaterial = MyStringId.GetOrCompute("ProjectileTrailLine"),
                TrailColor = new Vector4(1, 1, 1, 1),
                ParticleColor = new Vector4(1, 1, 1, 1),
                ShieldDamage = Kinetic,
                Ammo = Beam,
                Effect = Lance,
                Guidance = Smart,
                AmmoSound = new MySoundPair("cueName"),
                ReloadSound = new MySoundPair("cueName"),
                SecondarySound = new MySoundPair("cueName")
            },

            //Weapon4SubTyeId is the second SubtypeId in your block.sbc
            ["LargeBeamLance"] = new WeaponDefinition()
            {
                IsExplosive = false,
                UseRandomizedRange = true,
                ShieldHitDraw = true,
                Trail = true,
                TurretMode = true,
                TrackTarget = true,
                MaxTicks = 180,
                RotateBarrelAxis = 0,
                ReloadTime = 10,
                RateOfFire = 60,
                BarrelsPerShot = 1,
                SkipBarrels = 1,
                ShotsPerBarrel = 1,
                HeatPerRoF = 1,
                MaxHeat = 180,
                HeatSinkRate = 2,
                MuzzleFlashLifeSpan = 0,
                ShieldDmgMultiplier = 1.1f,
                Mass = 200.5f,
                Health = 201.1f,
                ShotLength = 15.6f,
                DesiredSpeed = 200f,
                SpeedVariance = 5f,
                MaxTrajectory = 8000.5f,
                BackkickForce = 15f,
                DeviateShotAngle = 1f,
                ReleaseTimeAfterFire = 10f,
                RangeMultiplier = 2.1f,
                ExplosiveYield = 10000.1f,
                PhysicalMaterial = MyStringId.GetOrCompute("ProjectileMaterial"),
                TrailColor = new Vector4(1, 1, 1, 1),
                ParticleColor = new Vector4(1, 1, 1, 1),
                ShieldDamage = Kinetic,
                Ammo = Beam,
                Effect = Lance,
                Guidance = Smart,
                AmmoSound = new MySoundPair("cueName"),
                ReloadSound = new MySoundPair("cueName"),
                SecondarySound = new MySoundPair("cueName")
            }
        };

        internal void Init()
        {
            foreach (var def in TurretDefinitions)
                Session.Instance.WeaponStructure.Add(MyStringHash.GetOrCompute(def.Key), new WeaponStructure(def, WeaponDefinitions, BarrelDefinitions));
        }
    }
}
