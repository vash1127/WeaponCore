using System.Collections.Generic;
using Sandbox.Definitions;
using VRage;
using VRage.Game;
using VRage.Utils;
using VRageMath;

namespace WeaponCore.Support
{
    public class WeaponSystem
    {
        private const string Arc = "Arc";

        public readonly MyStringHash AimPartName;
        public readonly MyStringHash MuzzlePartName;
        public readonly WeaponDefinition Values;
        public readonly MyDefinitionId AmmoDefId;
        public readonly MyAmmoMagazineDefinition MagazineDef;
        public readonly MyStringId ProjectileMaterial;
        public readonly Dictionary<MyDefinitionBase, float> CustomBlockDefinitionBasesToScales;
        public readonly string WeaponName;
        public readonly string[] Barrels;
        public readonly int ReloadTime;
        public readonly int DelayToFire;
        public readonly int TimeToCeaseFire;
        public readonly int MaxObjectsHit;
        public readonly int TargetLossTime;
        public readonly int ModelId;
        public readonly int MaxHeat;
        public readonly int WeaponID;
        public readonly bool BurstMode;
        public readonly bool AmmoParticle;
        public readonly bool HitParticle;
        public readonly bool HeatingEmissive;
        public readonly bool FiringEmissive;
        public readonly bool TrackingEmissive;
        public readonly bool ReloadingEmissive;
        public readonly bool BarrelAxisRotation;
        public readonly bool AmmoAreaEffect;
        public readonly bool AmmoSkipAccel;
        public readonly bool LineWidthVariance;
        public readonly bool LineColorVariance;
        public readonly bool EnergyAmmo;
        public readonly bool IsHybrid;
        public readonly bool BarrelEffect1;
        public readonly bool BarrelEffect2;
        public readonly bool HasBarrelShootAv;
        public readonly bool HasBackKickForce;
        public readonly bool SpeedVariance;
        public readonly bool RangeVariance;
        public readonly bool VirtualBeams;
        public readonly bool IsBeamWeapon;
        public readonly bool ConvergeBeams;
        public readonly bool RotateRealBeam;
        public readonly bool OneHitParticle;
        public readonly bool DamageScaling;
        public readonly bool ArmorScaling;
        public readonly bool CustomDamageScales;
        public readonly bool TargetOffSet;
        public readonly bool TargetSubSystems;
        public readonly bool OnlySubSystems;
        public readonly bool ClosestFirst;
        public readonly bool DegROF;
        public readonly bool TrackProjectile;
        public readonly bool TrackOther;
        public readonly bool TrackGrids;
        public readonly bool TrackCharacters;
        public readonly bool TrackMeteors;
        public readonly bool TrackNeutrals;
        public readonly double MaxTrajectory;
        public readonly double MaxTrajectorySqr;
        public readonly double AreaRadiusSmall;
        public readonly double AreaRadiusLarge;
        public readonly double DetonateRadiusSmall;
        public readonly double DetonateRadiusLarge;
        public readonly double MaxTargetSpeed;
        public readonly double ShieldModifier;
        public readonly float Barrel1AvTicks;
        public readonly float Barrel2AvTicks;
        public readonly float WepCooldown;
        public int HeatPShot;
        public float BaseDamage;
        public float ShotEnergyCost;
        public float FiringSoundDistSqr;
        public float ReloadSoundDistSqr;
        public float BarrelSoundDistSqr;
        public float HardPointSoundDistSqr;
        public float NoAmmoSoundDistSqr;
        public float HitSoundDistSqr;
        public float AmmoTravelSoundDistSqr;
        public float HardPointSoundMaxDistSqr;
        public float AmmoSoundMaxDistSqr;
        public float MinTargetRadius;
        public float MaxTargetRadius;
        public FiringSoundState FiringSound;
        public bool HitSound;
        public bool WeaponReloadSound;
        public bool NoAmmoSound;
        public bool HardPointRotationSound;
        public bool BarrelRotationSound;
        public bool AmmoTravelSound;

        public enum FiringSoundState
        {
            None,
            PerShot,
            WhenDone
        }

        public WeaponSystem(MyStringHash aimPartName, MyStringHash muzzlePartName, WeaponDefinition values, string weaponName, MyDefinitionId ammoDefId, int weaponId)
        {
            AimPartName = aimPartName;
            MuzzlePartName = muzzlePartName;
            Values = values;
            Barrels = values.Assignments.Barrels;
            WeaponName = weaponName;
            AmmoDefId = ammoDefId;
            WeaponID = weaponId;
            MagazineDef = MyDefinitionManager.Static.GetAmmoMagazineDefinition(AmmoDefId);
            ProjectileMaterial = MyStringId.GetOrCompute(values.Graphics.Line.Material);

            AmmoParticle = values.Graphics.Particles.Ammo.Name != string.Empty;
            BarrelEffect1 = values.Graphics.Particles.Barrel1.Name != string.Empty;
            BarrelEffect2 = values.Graphics.Particles.Barrel2.Name != string.Empty;
            HitParticle = values.Graphics.Particles.Hit.Name != string.Empty;
            Barrel1AvTicks = values.Graphics.Particles.Barrel1.Extras.MaxDuration;
            Barrel2AvTicks = values.Graphics.Particles.Barrel2.Extras.MaxDuration;
            BarrelAxisRotation = values.HardPoint.RotateBarrelAxis != 0;
            LineColorVariance = values.Graphics.Line.ColorVariance.Start > 0 && values.Graphics.Line.ColorVariance.End > 0;
            LineWidthVariance = values.Graphics.Line.WidthVariance.Start > 0 || values.Graphics.Line.WidthVariance.End > 0;
            SpeedVariance = values.Ammo.Trajectory.SpeedVariance.Start > 0 || values.Ammo.Trajectory.SpeedVariance.End > 0;
            RangeVariance = values.Ammo.Trajectory.RangeVariance.Start > 0 || values.Ammo.Trajectory.RangeVariance.End > 0;

            TargetOffSet = values.Ammo.Trajectory.Smarts.Inaccuracy > 0;
            TimeToCeaseFire = values.HardPoint.DelayCeaseFire;
            ReloadTime = values.HardPoint.Loading.ReloadTime;
            DelayToFire = values.HardPoint.Loading.DelayUntilFire;
            TargetLossTime = values.Ammo.Trajectory.TargetLossTime > 0 ? values.Ammo.Trajectory.TargetLossTime : int.MaxValue;
            MaxObjectsHit = values.Ammo.ObjectsHit.MaxObjectsHit > 0 ? values.Ammo.ObjectsHit.MaxObjectsHit : int.MaxValue;
            BurstMode = values.HardPoint.Loading.ShotsInBurst > 0;
            DegROF = values.HardPoint.Loading.DegradeROF;
            MaxHeat = values.HardPoint.Loading.MaxHeat;
            HeatPShot = values.HardPoint.Loading.HeatPerShot;
            WepCooldown = values.HardPoint.Loading.Cooldown;
            if(WepCooldown < .2f) WepCooldown = .2f;
            if(WepCooldown > .95f) WepCooldown = .95f;
            AmmoAreaEffect = values.Ammo.AreaEffect.AreaEffect != AreaDamage.AreaEffectType.Disabled;
            
            AreaRadiusSmall = Session.ModRadius(values.Ammo.AreaEffect.AreaEffectRadius, false);
            AreaRadiusLarge = Session.ModRadius(values.Ammo.AreaEffect.AreaEffectRadius, true);
            DetonateRadiusSmall = Session.ModRadius(values.Ammo.AreaEffect.Detonation.DetonationRadius, false);
            DetonateRadiusLarge = Session.ModRadius(values.Ammo.AreaEffect.Detonation.DetonationRadius, true);
            ShieldModifier = Values.DamageScales.Shields.Modifier > 0 ? Values.DamageScales.Shields.Modifier : 1;
            AmmoSkipAccel = values.Ammo.Trajectory.AccelPerSec <= 0;
            EnergyAmmo = ammoDefId.SubtypeId.String == "Blank";
            IsHybrid = values.HardPoint.Hybrid;

            MaxTrajectory = values.Ammo.Trajectory.MaxTrajectory;
            MaxTrajectorySqr = MaxTrajectory * MaxTrajectory;
            HasBackKickForce = values.Ammo.BackKickForce > 0;
            MaxTargetSpeed = values.Targeting.StopTrackingSpeed > 0 ? values.Targeting.StopTrackingSpeed : double.MaxValue;

            ClosestFirst = values.Targeting.ClosestFirst;

            Sound();

            DamageScales(out DamageScaling, out ArmorScaling, out CustomDamageScales, out CustomBlockDefinitionBasesToScales);
            Models(out ModelId);
            Emissives(out TrackingEmissive, out FiringEmissive, out HeatingEmissive, out ReloadingEmissive);
            Beams(out IsBeamWeapon, out VirtualBeams, out RotateRealBeam, out ConvergeBeams, out OneHitParticle);
            Track(out TrackProjectile, out TrackGrids, out TrackCharacters, out TrackMeteors, out TrackNeutrals, out TrackOther);
            SubSystems(out TargetSubSystems, out OnlySubSystems);
            ValidTargetSize(out MinTargetRadius, out MaxTargetRadius);
            HasBarrelShootAv = BarrelEffect1 || BarrelEffect2 || HardPointRotationSound || FiringSound == FiringSoundState.WhenDone;
        }

        private void DamageScales(out bool damageScaling, out bool armorScaling, out bool customDamageScales, out Dictionary<MyDefinitionBase, float> customBlockDef)
        {
            armorScaling = false;
            customDamageScales = false;
            var d = Values.DamageScales;
            customBlockDef = null;
            if (d.Custom.Types != null && d.Custom.Types.Length > 0)
            {
                foreach (var def in MyDefinitionManager.Static.GetAllDefinitions())
                foreach (var customDef in d.Custom.Types)
                    if (customDef.Modifier >= 0 && def.Id.SubtypeId.String == customDef.SubTypeId)
                    {
                        if (customBlockDef == null) customBlockDef = new Dictionary<MyDefinitionBase, float>();
                        customBlockDef.Add(def, customDef.Modifier);
                        customDamageScales = customBlockDef.Count > 0;
                    }
            }
            damageScaling = d.MaxIntegrity > 0 || d.Armor.Armor >= 0 || d.Armor.NonArmor >= 0 || d.Armor.Heavy >= 0 || d.Armor.Light >= 0 || d.Grids.Large >= 0 || d.Grids.Small >= 0 || customDamageScales;
            if (damageScaling) armorScaling = d.Armor.Armor >= 0 || d.Armor.NonArmor >= 0 || d.Armor.Heavy >= 0 || d.Armor.Light >= 0;
        }

        private void Track(out bool trackProjectile, out bool trackGrids, out bool trackCharacters, out bool trackMeteors, out bool trackNeutrals, out bool trackOther)
        {
            trackProjectile = false;
            trackGrids = false;
            trackCharacters = false;
            trackMeteors = false;
            trackNeutrals = false;
            trackOther = false;

            var threats = Values.Targeting.Threats;
            foreach (var threat in threats)
            {
                if (threat == TargetingDefinition.Threat.Projectiles)
                    trackProjectile = true;
                else if (threat == TargetingDefinition.Threat.Grids)
                {
                    trackGrids = true;
                    trackOther = true;
                }
                else if (threat == TargetingDefinition.Threat.Characters)
                {
                    trackCharacters = true;
                    trackOther = true;
                }
                else if (threat == TargetingDefinition.Threat.Meteors)
                {
                    trackMeteors = true;
                    trackOther = true;
                }
                else if (threat == TargetingDefinition.Threat.Neutrals)
                {
                    trackNeutrals = true;
                    trackOther = true;
                }
            }
        }

        private void SubSystems(out bool targetSubSystems, out bool onlySubSystems)
        {
            targetSubSystems = false;
            var anySystemDetected = false;
            if (Values.Targeting.SubSystems.Length > 0)
            {
                foreach (var system in Values.Targeting.SubSystems)
                {
                    if (system != TargetingDefinition.BlockTypes.Any) targetSubSystems = true;
                    else anySystemDetected = true;
                }
            }
            if (TargetSubSystems && anySystemDetected) onlySubSystems = false;
            else onlySubSystems = true;
        }

        private void ValidTargetSize(out float minTargetRadius, out float maxTargetRadius)
        {
            var minDiameter = Values.Targeting.MinimumDiameter;
            var maxDiameter = Values.Targeting.MaximumDiameter;

            minTargetRadius = (float)(minDiameter > 0 ? minDiameter * 0.5d : 0);
            maxTargetRadius = (float)(maxDiameter > 0 ? maxDiameter * 0.5d : float.MaxValue);
        }

        private void Models(out int modelId)
        {

            if (Values.Graphics.ModelName != string.Empty)
            {
                modelId = Session.Instance.ModelCount++;
                Session.Instance.ModelIdToName.Add(ModelId, Values.ModPath + Values.Graphics.ModelName);
            }
            else modelId = -1;
        }

        private void Emissives(out bool tracking, out bool firing, out bool heating, out bool reloading)
        {
            tracking = Values.Graphics.Emissive.Tracking.Enable;
            firing = Values.Graphics.Emissive.Firing.Enable;
            heating = Values.Graphics.Emissive.Heating.Enable;
            reloading = Values.Graphics.Emissive.Reloading.Enable;
        }

        private void Beams(out bool isBeamWeapon, out bool virtualBeams, out bool rotateRealBeam, out bool convergeBeams, out bool oneHitParticle)
        {
            isBeamWeapon = Values.Ammo.Beams.Enable;
            virtualBeams = Values.Ammo.Beams.VirtualBeams && IsBeamWeapon;
            rotateRealBeam = Values.Ammo.Beams.RotateRealBeam && VirtualBeams;
            convergeBeams = !RotateRealBeam && Values.Ammo.Beams.ConvergeBeams && VirtualBeams;
            oneHitParticle = Values.Ammo.Beams.OneParticle && IsBeamWeapon;
        }

        private void Sound()
        {

            HitSound = Values.Audio.Ammo.HitSound != string.Empty;
            AmmoTravelSound = Values.Audio.Ammo.TravelSound != string.Empty;
            WeaponReloadSound = Values.Audio.HardPoint.ReloadSound != string.Empty;
            HardPointRotationSound = Values.Audio.HardPoint.HardPointRotationSound != string.Empty;
            BarrelRotationSound = Values.Audio.HardPoint.BarrelRotationSound != string.Empty;
            NoAmmoSound = Values.Audio.HardPoint.NoAmmoSound != string.Empty;
            var fSoundStart = Values.Audio.HardPoint.FiringSound;
            if (fSoundStart != string.Empty && Values.Audio.HardPoint.FiringSoundPerShot)
                FiringSound = FiringSoundState.PerShot;
            else if (fSoundStart != string.Empty && !Values.Audio.HardPoint.FiringSoundPerShot)
                FiringSound = FiringSoundState.WhenDone;
            else FiringSound = FiringSoundState.None;


            var fireSound = string.Concat(Arc, Values.Audio.HardPoint.FiringSound);
            var hitSound = string.Concat(Arc, Values.Audio.Ammo.HitSound);
            var travelSound = string.Concat(Arc, Values.Audio.Ammo.TravelSound);
            var reloadSound = string.Concat(Arc, Values.Audio.HardPoint.ReloadSound);
            var barrelSound = string.Concat(Arc, Values.Audio.HardPoint.BarrelRotationSound);
            var hardPointSound = string.Concat(Arc, Values.Audio.HardPoint.HardPointRotationSound);
            var noAmmoSound = string.Concat(Arc, Values.Audio.HardPoint.NoAmmoSound);
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
        }
    }

    public class WeaponStructure
    {
        private static int _weaponCount;
        public readonly Dictionary<MyStringHash, WeaponSystem> WeaponSystems;
        public readonly Dictionary<MyDefinitionId, List<int>> AmmoToWeaponIds;
        public readonly MyStringHash[] AimPartNames;
        public readonly MyStringHash[] MuzzlePartNames;
        public readonly bool MultiParts;

        public WeaponStructure(KeyValuePair<string, Dictionary<string, MyTuple<string, string>>> tDef, List<WeaponDefinition> wDefList)
        {
            var map = tDef.Value;
            var numOfParts = wDefList.Count;
            MultiParts = numOfParts > 1;
            var aimPartNames = new MyStringHash[numOfParts];
            var muzzlePartNames = new MyStringHash[numOfParts];
            var mapIndex = 0;
            WeaponSystems = new Dictionary<MyStringHash, WeaponSystem>(MyStringHash.Comparer);
            AmmoToWeaponIds = new Dictionary<MyDefinitionId, List<int>>(MyDefinitionId.Comparer);
            foreach (var w in map)
            {
                var myAimNameHash = MyStringHash.GetOrCompute(w.Key);
                var myMuzzleNameHash = MyStringHash.GetOrCompute(w.Value.Item1);

                aimPartNames[mapIndex] = myAimNameHash;
                muzzlePartNames[mapIndex] = myMuzzleNameHash;

                var typeName = w.Value.Item2;
                var weaponDef = new WeaponDefinition();

                foreach (var weapon in wDefList)
                    if (weapon.HardPoint.WeaponId == typeName) weaponDef = weapon;

                var ammoDefId = new MyDefinitionId();
                var ammoBlank = weaponDef.HardPoint.AmmoMagazineId == string.Empty || weaponDef.HardPoint.AmmoMagazineId == "Blank";
                foreach (var def in Session.Instance.AllDefinitions)
                {
                    if (ammoBlank && def.Id.SubtypeId.String == "Blank" || def.Id.SubtypeId.String == weaponDef.HardPoint.AmmoMagazineId) ammoDefId = def.Id;
                }

                weaponDef.HardPoint.DeviateShotAngle = MathHelper.ToRadians(weaponDef.HardPoint.DeviateShotAngle);
  
                WeaponSystems.Add(myAimNameHash, new WeaponSystem(myAimNameHash, myMuzzleNameHash, weaponDef, typeName, ammoDefId, _weaponCount));
                _weaponCount++;
                if (!ammoBlank)
                {
                    if (!AmmoToWeaponIds.ContainsKey(ammoDefId)) AmmoToWeaponIds[ammoDefId] = new List<int>();
                    AmmoToWeaponIds[ammoDefId].Add(mapIndex);
                }

                mapIndex++;
                /*
                  if (weaponDef.AmmoDef.RealisticDamage)
                  {
                      weaponDef.HasKineticEffect = weaponDef.AmmoDef.Mass > 0 && weaponDef.AmmoDef.DesiredSpeed > 0;
                      weaponDef.HasThermalEffect = weaponDef.AmmoDef.ThermalDamage > 0;
                      var kinetic = ((weaponDef.AmmoDef.Mass / 2) * (weaponDef.AmmoDef.DesiredSpeed * weaponDef.AmmoDef.DesiredSpeed) / 1000) * weaponDef.KeenScaler;
                      weaponDef.ComputedBaseDamage = kinetic + weaponDef.AmmoDef.ThermalDamage;
                  }
                  */
            }
            AimPartNames = aimPartNames;
            MuzzlePartNames = muzzlePartNames;
        }
    }
}
