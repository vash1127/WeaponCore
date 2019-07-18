using System.Collections.Generic;
using Sandbox.Definitions;
using VRage.Game;
using VRage.Utils;
using VRageMath;

namespace WeaponCore.Support
{
    public class WeaponSystem
    {
        public readonly MyStringHash PartName;
        public readonly WeaponDefinition Values;
        public readonly string WeaponName;
        public readonly string[] Barrels;
        public readonly int ModelId;
        public readonly int ReloadTime;
        public readonly int DelayToFire;
        public readonly int TimeToCeaseFire;
        public readonly int MaxObjectsHit;
        public readonly int TargetLossTime;
        public readonly MyDefinitionId AmmoDefId;
        public readonly MyAmmoMagazineDefinition MagazineDef;
        public readonly FiringSoundState FiringSound;
        public readonly bool DamageScaling;
        public readonly bool ArmorScaling;
        public readonly bool BurstMode;
        public readonly bool AmmoParticle;
        public readonly bool AmmoTravelSound;
        public readonly bool HitParticle;
        public readonly bool HitSound;
        public readonly bool WeaponReloadSound;
        public readonly bool NoAmmoSound;
        public readonly bool HardPointRotationSound;
        public readonly bool BarrelRotationSound;
        public readonly bool BarrelAxisRotation;
        public readonly bool AmmoAreaEffect;
        public readonly bool AmmoSkipAccel;
        public readonly bool LineWidthVariance;
        public readonly bool LineColorVariance;
        public readonly bool EnergyAmmo;
        public readonly bool BarrelEffect1;
        public readonly bool BarrelEffect2;
        public readonly bool HasBarrelShootAv;
        public readonly bool HasBackKickForce;
        public readonly bool SpeedVariance;
        public readonly bool RangeVariance;
        public readonly bool IsBeamWeapon;
        public readonly bool CustomDamageScales;
        public readonly double MaxTrajectorySqr;
        public readonly float Barrel1AvTicks;
        public readonly float Barrel2AvTicks;
        public readonly float ShotEnergyCost;
        public readonly float FiringSoundDistSqr;
        public readonly float ReloadSoundDistSqr;
        public readonly float BarrelSoundDistSqr;
        public readonly float HardPointSoundDistSqr;
        public readonly float NoAmmoSoundDistSqr;
        public readonly float HitSoundDistSqr;
        public readonly float AmmoTravelSoundDistSqr;
        public readonly float HardPointSoundMaxDistSqr;
        public readonly float AmmoSoundMaxDistSqr;
        private const string Arc = "Arc";
        public readonly Dictionary<MyDefinitionBase, float> CustomBlockDefinitionBasesToScales;

        public enum FiringSoundState
        {
            None,
            PerShot,
            WhenDone
        }

        public readonly MyStringId ProjectileMaterial;

        public WeaponSystem(MyStringHash partName, WeaponDefinition values, string weaponName, MyDefinitionId ammoDefId)
        {
            PartName = partName;
            Values = values;
            Barrels = values.Assignments.Barrels;
            WeaponName = weaponName;
            AmmoDefId = ammoDefId;
            MagazineDef = MyDefinitionManager.Static.GetAmmoMagazineDefinition(AmmoDefId);

            ProjectileMaterial = MyStringId.GetOrCompute(values.Graphics.Line.Material);
            AmmoParticle = values.Graphics.Particles.Ammo.Name != string.Empty;
            BarrelEffect1 = values.Graphics.Particles.Barrel1.Name != string.Empty;
            BarrelEffect2 = values.Graphics.Particles.Barrel2.Name != string.Empty;

            LineColorVariance = values.Graphics.Line.ColorVariance.Start > 0 && values.Graphics.Line.ColorVariance.End > 0;
            LineWidthVariance = values.Graphics.Line.WidthVariance.Start > 0 || values.Graphics.Line.WidthVariance.End > 0;
            SpeedVariance = values.Ammo.Trajectory.SpeedVariance.Start > 0 || values.Ammo.Trajectory.SpeedVariance.End > 0;
            RangeVariance = values.Ammo.Trajectory.RangeVariance.Start > 0 || values.Ammo.Trajectory.RangeVariance.End > 0;
            var d = values.DamageScales;
            if (d.Custom.Types != null && d.Custom.Types.Length > 0)
            {
                foreach (var def in MyDefinitionManager.Static.GetAllDefinitions())
                foreach (var customDef in d.Custom.Types)
                    if (customDef.Modifier >= 0 && def.Id.SubtypeId.String == customDef.SubTypeId)
                    {
                        if (CustomBlockDefinitionBasesToScales == null) CustomBlockDefinitionBasesToScales = new Dictionary<MyDefinitionBase, float>();
                        CustomBlockDefinitionBasesToScales.Add(def, customDef.Modifier);
                        CustomDamageScales = CustomBlockDefinitionBasesToScales != null && CustomBlockDefinitionBasesToScales.Count > 0;
                        Log.Line("found custom def");
                    }
            }
            DamageScaling =  d.MaxIntegrity > 0 || d.Armor.Armor >= 0 || d.Armor.NonArmor >= 0 || d.Armor.Heavy >= 0 || d.Armor.Light >= 0 || d.Grids.Large >= 0 || d.Grids.Small >= 0 || CustomDamageScales;
            if (DamageScaling) ArmorScaling = d.Armor.Armor >= 0 || d.Armor.NonArmor >= 0 || d.Armor.Heavy >= 0 || d.Armor.Light >= 0;
            TargetLossTime = values.Ammo.Trajectory.TargetLossTime > 0 ? values.Ammo.Trajectory.TargetLossTime : int.MaxValue;
            MaxObjectsHit = values.Ammo.MaxObjectsHit > 0 ? values.Ammo.MaxObjectsHit : int.MaxValue;
            BurstMode = values.HardPoint.Loading.ShotsInBurst > 0;
            AmmoAreaEffect = values.Ammo.AreaEffectRadius > 0;
            AmmoSkipAccel = values.Ammo.Trajectory.AccelPerSec <= 0;
            EnergyAmmo = ammoDefId.SubtypeId.String == "Blank";

            MaxTrajectorySqr = values.Ammo.Trajectory.MaxTrajectory * values.Ammo.Trajectory.MaxTrajectory;
            ShotEnergyCost = values.HardPoint.EnergyCost * values.Ammo.DefaultDamage;
            HasBackKickForce = values.Ammo.BackKickForce > 0;
            ReloadTime = values.HardPoint.Loading.ReloadTime;
            DelayToFire = values.HardPoint.Loading.DelayUntilFire;
            TimeToCeaseFire = values.HardPoint.DelayCeaseFire;
            Barrel1AvTicks = values.Graphics.Particles.Barrel1.Extras.MaxDuration;
            Barrel2AvTicks = values.Graphics.Particles.Barrel2.Extras.MaxDuration;
            BarrelAxisRotation = values.HardPoint.RotateBarrelAxis != 0;

            HitParticle = values.Graphics.Particles.Hit.Name != string.Empty;
            HitSound = values.Audio.Ammo.HitSound != string.Empty;
            AmmoTravelSound = values.Audio.Ammo.TravelSound != string.Empty;
            WeaponReloadSound = values.Audio.HardPoint.ReloadSound != string.Empty;
            HardPointRotationSound = values.Audio.HardPoint.HardPointRotationSound != string.Empty;
            BarrelRotationSound = values.Audio.HardPoint.BarrelRotationSound != string.Empty;
            NoAmmoSound = values.Audio.HardPoint.NoAmmoSound != string.Empty;
            IsBeamWeapon = Values.Ammo.Trajectory.DesiredSpeed <= 0 && Values.Ammo.Trajectory.MaxTrajectory > 0;
            var fSoundStart = values.Audio.HardPoint.FiringSound;
            if (fSoundStart != string.Empty && values.Audio.HardPoint.FiringSoundPerShot)
                FiringSound = FiringSoundState.PerShot;
            else if (fSoundStart != string.Empty && !values.Audio.HardPoint.FiringSoundPerShot)
                FiringSound = FiringSoundState.WhenDone;
            else FiringSound = FiringSoundState.None;

            var fireSound = string.Concat(Arc, values.Audio.HardPoint.FiringSound);
            var hitSound = string.Concat(Arc, values.Audio.Ammo.HitSound);
            var travelSound = string.Concat(Arc, values.Audio.Ammo.TravelSound);
            var reloadSound = string.Concat(Arc, values.Audio.HardPoint.ReloadSound);
            var barrelSound = string.Concat(Arc, values.Audio.HardPoint.BarrelRotationSound);
            var hardPointSound = string.Concat(Arc, values.Audio.HardPoint.HardPointRotationSound);
            var noAmmoSound = string.Concat(Arc, values.Audio.HardPoint.NoAmmoSound);
            foreach (var def in Session.Instance.SoundDefinitions)
            {
                var id = def.Id.SubtypeId.String;
                if (FiringSound != FiringSoundState.None && id == fireSound)
                {
                    var ob = def.GetObjectBuilder() as MyObjectBuilder_AudioDefinition;
                    if (ob != null) FiringSoundDistSqr = ob.MaxDistance * ob.MaxDistance;
                    if (FiringSoundDistSqr > HardPointSoundMaxDistSqr) HardPointSoundMaxDistSqr = FiringSoundDistSqr;
                }
                if (HitSound && id == hitSound)
                {
                    var ob = def.GetObjectBuilder() as MyObjectBuilder_AudioDefinition;
                    if (ob != null) HitSoundDistSqr = ob.MaxDistance * ob.MaxDistance;
                    if (HitSoundDistSqr > AmmoSoundMaxDistSqr) AmmoSoundMaxDistSqr = HitSoundDistSqr;
                }
                else if (AmmoTravelSound && id == travelSound)
                {
                    var ob = def.GetObjectBuilder() as MyObjectBuilder_AudioDefinition;
                    if (ob != null) AmmoTravelSoundDistSqr = ob.MaxDistance * ob.MaxDistance;
                    if (AmmoTravelSoundDistSqr > AmmoSoundMaxDistSqr) AmmoSoundMaxDistSqr = AmmoTravelSoundDistSqr;
                }
                else if (WeaponReloadSound && id == reloadSound)
                {
                    var ob = def.GetObjectBuilder() as MyObjectBuilder_AudioDefinition;
                    if (ob != null) ReloadSoundDistSqr = ob.MaxDistance * ob.MaxDistance;
                    if (ReloadSoundDistSqr > HardPointSoundMaxDistSqr) HardPointSoundMaxDistSqr = ReloadSoundDistSqr;

                }
                else if (BarrelRotationSound && id == barrelSound)
                {
                    var ob = def.GetObjectBuilder() as MyObjectBuilder_AudioDefinition;
                    if (ob != null) BarrelSoundDistSqr = ob.MaxDistance * ob.MaxDistance;
                    if (BarrelSoundDistSqr > HardPointSoundMaxDistSqr) HardPointSoundMaxDistSqr = BarrelSoundDistSqr;
                }
                else if (HardPointRotationSound && id == hardPointSound)
                {
                    var ob = def.GetObjectBuilder() as MyObjectBuilder_AudioDefinition;
                    if (ob != null) HardPointSoundDistSqr = ob.MaxDistance * ob.MaxDistance;
                    if (HardPointSoundDistSqr > HardPointSoundMaxDistSqr) HardPointSoundMaxDistSqr = HardPointSoundDistSqr;
                }
                else if (NoAmmoSound && id == noAmmoSound)
                {
                    var ob = def.GetObjectBuilder() as MyObjectBuilder_AudioDefinition;
                    if (ob != null) NoAmmoSoundDistSqr = ob.MaxDistance * ob.MaxDistance;
                    if (NoAmmoSoundDistSqr > HardPointSoundMaxDistSqr) HardPointSoundMaxDistSqr = NoAmmoSoundDistSqr;
                }
            }

            if (values.Graphics.ModelName != string.Empty)
            {
                ModelId = Session.Instance.ModelCount++;
                Session.Instance.ModelIdToName.Add(ModelId, values.ModPath + values.Graphics.ModelName);
            }
            else ModelId = -1;

            HasBarrelShootAv = BarrelEffect1 || BarrelEffect2 || HardPointRotationSound || FiringSound == FiringSoundState.WhenDone;
        }
    }

    public class WeaponStructure
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
