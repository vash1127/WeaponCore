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
        public readonly MyDefinitionId AmmoDefId;
        public readonly MyAmmoMagazineDefinition MagazineDef;
        public readonly bool SimpleFiringSound;

        public WeaponSystem(MyStringHash partName, WeaponDefinition weaponType, string weaponName, MyDefinitionId ammoDefId)
        {
            PartName = partName;
            WeaponType = weaponType;
            Barrels = weaponType.TurretDef.Barrels;
            WeaponName = weaponName;
            AmmoDefId = ammoDefId;
            MagazineDef = MyDefinitionManager.Static.GetAmmoMagazineDefinition(AmmoDefId);
            var audioDef = WeaponType.AudioDef;

            SimpleFiringSound = audioDef.FiringSoundStart != string.Empty 
                                && audioDef.FiringSoundLoop == string.Empty 
                                && audioDef.FiringSoundEnd == string.Empty; 

            if (WeaponType.GraphicDef.ModelName != string.Empty)
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
        public readonly Dictionary<MyDefinitionId, int> AmmoToWeaponIds;
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
            AmmoToWeaponIds = new Dictionary<MyDefinitionId, int>(MyDefinitionId.Comparer);
            foreach (var w in map)
            {
                var myNameHash = MyStringHash.GetOrCompute(w.Key);
                names[mapIndex] = myNameHash;

                var weaponTypeName = w.Value;
                var weaponDef = new WeaponDefinition();

                foreach (var weapon in wDef)
                    if (weapon.TurretDef.DefinitionId == weaponTypeName) weaponDef = weapon;

                var ammoDefId = new MyDefinitionId();
                var ammoBlank = weaponDef.TurretDef.AmmoMagazineId == string.Empty;
                foreach (var def in Session.Instance.AllDefinitions)
                    if (ammoBlank && def.Id.SubtypeId.String == "Blank" || def.Id.SubtypeId.String == weaponDef.TurretDef.AmmoMagazineId) ammoDefId = def.Id;

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

                WeaponSystems.Add(myNameHash, new WeaponSystem(myNameHash, weaponDef, weaponTypeName, ammoDefId));
                if (weaponDef.TurretDef.AmmoMagazineId != string.Empty) AmmoToWeaponIds.Add(ammoDefId, mapIndex);

                mapIndex++;
            }
            PartNames = names;
        }
    }
}
