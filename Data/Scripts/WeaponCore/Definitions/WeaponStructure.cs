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
        public readonly WeaponDefinition WeaponType;
        public readonly string WeaponName;
        public readonly string[] Barrels;
        public readonly int ModelId;
        public readonly int ReloadTime;
        public readonly MyDefinitionId AmmoDefId;
        public readonly MyAmmoMagazineDefinition MagazineDef;
        public readonly FiringSoundState FiringSound;
        public readonly bool AmmoParticle;
        public readonly bool AmmoHitSound;
        public readonly bool AmmoTravelSound;
        public readonly bool AmmoAreaEffect;
        public readonly bool AmmoSkipAccel;
        public readonly bool EnergyAmmo;
        public readonly bool TurretEffect1;
        public readonly bool TurretEffect2;
        public readonly double MaxTrajectorySqr;

        public enum FiringSoundState
        {
            None,
            Simple,
            Full
        }

        public readonly MyStringId ProjectileMaterial;

        public WeaponSystem(MyStringHash partName, WeaponDefinition weaponType, string weaponName, MyDefinitionId ammoDefId)
        {
            PartName = partName;
            WeaponType = weaponType;
            Barrels = weaponType.TurretDef.Barrels;
            WeaponName = weaponName;
            AmmoDefId = ammoDefId;
            MagazineDef = MyDefinitionManager.Static.GetAmmoMagazineDefinition(AmmoDefId);
            ProjectileMaterial = MyStringId.GetOrCompute(WeaponType.GraphicDef.Line.Material);
            AmmoParticle = WeaponType.GraphicDef.Particles.AmmoParticle != string.Empty;
            AmmoHitSound = WeaponType.AudioDef.Ammo.HitSound != string.Empty;
            AmmoTravelSound = WeaponType.AudioDef.Ammo.TravelSound != string.Empty;
            AmmoAreaEffect = WeaponType.AmmoDef.AreaEffectRadius > 0;
            AmmoSkipAccel = WeaponType.AmmoDef.Trajectory.AccelPerSec <= 0;
            EnergyAmmo = ammoDefId.SubtypeId.String == "Blank";
            TurretEffect1 = WeaponType.GraphicDef.Particles.Turret1Particle != string.Empty;
            TurretEffect2 = WeaponType.GraphicDef.Particles.Turret2Particle != string.Empty;
            MaxTrajectorySqr = weaponType.AmmoDef.Trajectory.MaxTrajectory * weaponType.AmmoDef.Trajectory.MaxTrajectory;
            ReloadTime = WeaponType.TurretDef.ReloadTime;
            var audioDef = WeaponType.AudioDef;

            var fSoundStart = audioDef.Turret.FiringSoundStart;
            var fSoundLoop = audioDef.Turret.FiringSoundLoop;
            var fSoundEnd = audioDef.Turret.FiringSoundEnd;
            var e = string.Empty;

            if (fSoundStart != e && fSoundLoop == e && fSoundEnd == e)
                FiringSound = FiringSoundState.Simple;
            else if (fSoundLoop == e && fSoundLoop == e && fSoundEnd == e)
                FiringSound = FiringSoundState.None;
            else FiringSound = FiringSoundState.Full;

            if (WeaponType.GraphicDef.ModelName != string.Empty && !WeaponType.GraphicDef.Line.Trail)
            {
                ModelId = Session.Instance.ModelCount++;
                Session.Instance.ModelIdToName.Add(ModelId, WeaponType.ModPath + WeaponType.GraphicDef.ModelName);
            }
            else ModelId = -1;
        }
    }

    public struct WeaponStructure
    {
        public readonly Dictionary<MyStringHash, WeaponSystem> WeaponSystems;
        public readonly Dictionary<MyDefinitionId, List<int>> AmmoToWeaponIds;
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
            AmmoToWeaponIds = new Dictionary<MyDefinitionId, List<int>>(MyDefinitionId.Comparer);
            foreach (var w in map)
            {
                var myNameHash = MyStringHash.GetOrCompute(w.Key);
                names[mapIndex] = myNameHash;

                var weaponTypeName = w.Value;
                var weaponDef = new WeaponDefinition();

                foreach (var weapon in wDef)
                    if (weapon.TurretDef.DefinitionId == weaponTypeName) weaponDef = weapon;

                var ammoDefId = new MyDefinitionId();
                var ammoBlank = weaponDef.TurretDef.AmmoMagazineId == string.Empty || weaponDef.TurretDef.AmmoMagazineId == "Blank";
                foreach (var def in Session.Instance.AllDefinitions)
                    if (ammoBlank && def.Id.SubtypeId.String == "Blank" || def.Id.SubtypeId.String == weaponDef.TurretDef.AmmoMagazineId) ammoDefId = def.Id;

                weaponDef.TurretDef.DeviateShotAngle = MathHelper.ToRadians(weaponDef.TurretDef.DeviateShotAngle);
                /*
                if (weaponDef.AmmoDef.RealisticDamage)
                {
                    weaponDef.HasKineticEffect = weaponDef.AmmoDef.Mass > 0 && weaponDef.AmmoDef.DesiredSpeed > 0;
                    weaponDef.HasThermalEffect = weaponDef.AmmoDef.ThermalDamage > 0;
                    var kinetic = ((weaponDef.AmmoDef.Mass / 2) * (weaponDef.AmmoDef.DesiredSpeed * weaponDef.AmmoDef.DesiredSpeed) / 1000) * weaponDef.KeenScaler;
                    weaponDef.ComputedBaseDamage = kinetic + weaponDef.AmmoDef.ThermalDamage;
                }
                */

                WeaponSystems.Add(myNameHash, new WeaponSystem(myNameHash, weaponDef, weaponTypeName, ammoDefId));
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
