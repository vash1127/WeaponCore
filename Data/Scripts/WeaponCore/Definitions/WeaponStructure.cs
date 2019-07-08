using System.Collections.Generic;
using Sandbox.Definitions;
using VRage.Game;
using VRage.Utils;
using VRageMath;

namespace WeaponCore.Support
{
    public struct WeaponSystem
    {
        public readonly MyStringHash PartName;
        public readonly WeaponDefinition Kind;
        public readonly string WeaponName;
        public readonly string[] Barrels;
        public readonly int ModelId;
        public readonly int ReloadTime;
        public readonly int DelayToFire;
        public readonly MyDefinitionId AmmoDefId;
        public readonly MyAmmoMagazineDefinition MagazineDef;
        public readonly FiringSoundState FiringSound;
        public readonly bool AmmoParticle;
        public readonly bool AmmoHitSound;
        public readonly bool AmmoTravelSound;
        public readonly bool TurretReloadSound;
        public readonly bool HardPointRotationSound;
        public readonly bool BarrelRotationSound;
        public readonly bool AmmoAreaEffect;
        public readonly bool AmmoSkipAccel;
        public readonly bool EnergyAmmo;
        public readonly bool TurretEffect1;
        public readonly bool TurretEffect2;
        public readonly bool HasTurretShootAv;
        public readonly double MaxTrajectorySqr;
        public readonly float HardPointMaxSoundDistSqr;
        public readonly float AmmoMaxSoundDistSqr;
        public readonly float ShotEnergyCost;
        public enum FiringSoundState
        {
            None,
            PerShot,
            WhenDone
        }

        public readonly MyStringId ProjectileMaterial;

        public WeaponSystem(MyStringHash partName, WeaponDefinition kind, string weaponName, MyDefinitionId ammoDefId)
        {
            PartName = partName;
            Kind = kind;
            Barrels = kind.HardPoint.Barrels;
            WeaponName = weaponName;
            AmmoDefId = ammoDefId;
            MagazineDef = MyDefinitionManager.Static.GetAmmoMagazineDefinition(AmmoDefId);
            ProjectileMaterial = MyStringId.GetOrCompute(kind.Graphics.Line.Material);

            AmmoParticle = kind.Graphics.Particles.AmmoParticle != string.Empty;
            AmmoHitSound = kind.Audio.Ammo.HitSound != string.Empty;
            AmmoTravelSound = kind.Audio.Ammo.TravelSound != string.Empty;
            TurretReloadSound = kind.Audio.HardPoint.ReloadSound != string.Empty;
            HardPointRotationSound = kind.Audio.HardPoint.HardPointRotationSound != string.Empty;
            BarrelRotationSound = kind.Audio.HardPoint.BarrelRotationSound != string.Empty;
            TurretEffect1 = kind.Graphics.Particles.Turret1Particle != string.Empty;
            TurretEffect2 = kind.Graphics.Particles.Turret2Particle != string.Empty;

            AmmoAreaEffect = kind.Ammo.AreaEffectRadius > 0;
            AmmoSkipAccel = kind.Ammo.Trajectory.AccelPerSec <= 0;
            EnergyAmmo = ammoDefId.SubtypeId.String == "Blank";

            MaxTrajectorySqr = kind.Ammo.Trajectory.MaxTrajectory * kind.Ammo.Trajectory.MaxTrajectory;
            HardPointMaxSoundDistSqr = kind.Audio.HardPoint.SoundMaxDistanceOveride * kind.Audio.HardPoint.SoundMaxDistanceOveride;
            AmmoMaxSoundDistSqr = kind.Audio.Ammo.SoundMaxDistanceOveride * kind.Audio.Ammo.SoundMaxDistanceOveride;
            ShotEnergyCost = kind.HardPoint.EnergyCost * kind.Ammo.DefaultDamage;

            ReloadTime = kind.HardPoint.ReloadTime;
            DelayToFire = kind.HardPoint.DelayUntilFire;
            var audioDef = kind.Audio;

            var fSoundStart = audioDef.HardPoint.FiringSound;

            if (fSoundStart != string.Empty && audioDef.HardPoint.FiringSoundPerShot)
                FiringSound = FiringSoundState.PerShot;
            else if (fSoundStart != string.Empty && !audioDef.HardPoint.FiringSoundPerShot)
                FiringSound = FiringSoundState.WhenDone;
            else FiringSound = FiringSoundState.None;

            if (kind.Graphics.ModelName != string.Empty && !kind.Graphics.Line.Trail)
            {
                ModelId = Session.Instance.ModelCount++;
                Session.Instance.ModelIdToName.Add(ModelId, kind.ModPath + kind.Graphics.ModelName);
            }
            else ModelId = -1;

            HasTurretShootAv = TurretEffect1 || TurretEffect2 || HardPointRotationSound || FiringSound == FiringSoundState.WhenDone;
        }
    }

    public struct WeaponStructure
    {
        public readonly Dictionary<MyStringHash, WeaponSystem> WeaponSystems;
        public readonly Dictionary<MyDefinitionId, List<int>> AmmoToWeaponIds;
        public readonly MyStringHash[] PartNames;
        public readonly bool MultiParts;

        public WeaponStructure(KeyValuePair<string, Dictionary<string, string>> tDef, List<WeaponDefinition> wDefList)
        {
            var map = tDef.Value;
            var numOfParts = wDefList.Count;
            MultiParts = numOfParts > 1;
            var names = new MyStringHash[numOfParts];
            var mapIndex = 0;
            WeaponSystems = new Dictionary<MyStringHash, WeaponSystem>(MyStringHash.Comparer);
            AmmoToWeaponIds = new Dictionary<MyDefinitionId, List<int>>(MyDefinitionId.Comparer);
            foreach (var w in map)
            {
                var myNameHash = MyStringHash.GetOrCompute(w.Key);
                names[mapIndex] = myNameHash;

                var typeName = w.Value;
                var weaponDef = new WeaponDefinition();

                foreach (var weapon in wDefList)
                    if (weapon.HardPoint.DefinitionId == typeName) weaponDef = weapon;

                var ammoDefId = new MyDefinitionId();
                var ammoBlank = weaponDef.HardPoint.AmmoMagazineId == string.Empty || weaponDef.HardPoint.AmmoMagazineId == "Blank";
                foreach (var def in Session.Instance.AllDefinitions)
                {
                    if (ammoBlank && def.Id.SubtypeId.String == "Blank" || def.Id.SubtypeId.String == weaponDef.HardPoint.AmmoMagazineId) ammoDefId = def.Id;
                }

                weaponDef.HardPoint.DeviateShotAngle = MathHelper.ToRadians(weaponDef.HardPoint.DeviateShotAngle);
                /*
                if (weaponDef.AmmoDef.RealisticDamage)
                {
                    weaponDef.HasKineticEffect = weaponDef.AmmoDef.Mass > 0 && weaponDef.AmmoDef.DesiredSpeed > 0;
                    weaponDef.HasThermalEffect = weaponDef.AmmoDef.ThermalDamage > 0;
                    var kinetic = ((weaponDef.AmmoDef.Mass / 2) * (weaponDef.AmmoDef.DesiredSpeed * weaponDef.AmmoDef.DesiredSpeed) / 1000) * weaponDef.KeenScaler;
                    weaponDef.ComputedBaseDamage = kinetic + weaponDef.AmmoDef.ThermalDamage;
                }
                */

                WeaponSystems.Add(myNameHash, new WeaponSystem(myNameHash, weaponDef, typeName, ammoDefId));
                if (!ammoBlank)
                {
                    if (!AmmoToWeaponIds.ContainsKey(ammoDefId)) AmmoToWeaponIds[ammoDefId] = new List<int>();
                    AmmoToWeaponIds[ammoDefId].Add(mapIndex);
                }

                mapIndex++;
            }
            PartNames = names;
        }
    }
}
