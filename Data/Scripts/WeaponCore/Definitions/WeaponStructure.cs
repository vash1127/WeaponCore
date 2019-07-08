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
        public readonly int Barrel1AvTicks;
        public readonly int Barrel2AvTicks;
        public readonly MyDefinitionId AmmoDefId;
        public readonly MyAmmoMagazineDefinition MagazineDef;
        public readonly FiringSoundState FiringSound;
        public readonly bool AmmoParticle;
        public readonly bool AmmoHitSound;
        public readonly bool AmmoTravelSound;
        public readonly bool WeaponReloadSound;
        public readonly bool NoAmmoSound;
        public readonly bool HardPointRotationSound;
        public readonly bool BarrelRotationSound;
        public readonly bool AmmoAreaEffect;
        public readonly bool AmmoSkipAccel;
        public readonly bool EnergyAmmo;
        public readonly bool BarrelEffect1;
        public readonly bool BarrelEffect2;
        public readonly bool HasBarrelShootAv;
        public readonly double MaxTrajectorySqr;
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
            BarrelEffect1 = kind.Graphics.Particles.Barrel1Particle != string.Empty;
            BarrelEffect2 = kind.Graphics.Particles.Barrel2Particle != string.Empty;

            AmmoAreaEffect = kind.Ammo.AreaEffectRadius > 0;
            AmmoSkipAccel = kind.Ammo.Trajectory.AccelPerSec <= 0;
            EnergyAmmo = ammoDefId.SubtypeId.String == "Blank";

            MaxTrajectorySqr = kind.Ammo.Trajectory.MaxTrajectory * kind.Ammo.Trajectory.MaxTrajectory;
            ShotEnergyCost = kind.HardPoint.EnergyCost * kind.Ammo.DefaultDamage;

            ReloadTime = kind.HardPoint.ReloadTime;
            DelayToFire = kind.HardPoint.DelayUntilFire;
            Barrel1AvTicks = kind.Graphics.Particles.Barrel1Duration;
            Barrel2AvTicks = kind.Graphics.Particles.Barrel2Duration;

            AmmoHitSound = kind.Audio.Ammo.HitSound != string.Empty;
            AmmoTravelSound = kind.Audio.Ammo.TravelSound != string.Empty;
            WeaponReloadSound = kind.Audio.HardPoint.ReloadSound != string.Empty;
            HardPointRotationSound = kind.Audio.HardPoint.HardPointRotationSound != string.Empty;
            BarrelRotationSound = kind.Audio.HardPoint.BarrelRotationSound != string.Empty;
            NoAmmoSound = kind.Audio.HardPoint.NoAmmoSound != string.Empty;

            var audioDef = kind.Audio;
            var fSoundStart = audioDef.HardPoint.FiringSound;
            if (fSoundStart != string.Empty && audioDef.HardPoint.FiringSoundPerShot)
                FiringSound = FiringSoundState.PerShot;
            else if (fSoundStart != string.Empty && !audioDef.HardPoint.FiringSoundPerShot)
                FiringSound = FiringSoundState.WhenDone;
            else FiringSound = FiringSoundState.None;

            const string arc = "Arc";
            FiringSoundDistSqr = 0;
            AmmoTravelSoundDistSqr = 0;
            ReloadSoundDistSqr = 0;
            BarrelSoundDistSqr = 0;
            HardPointSoundDistSqr = 0;
            NoAmmoSoundDistSqr = 0;
            HitSoundDistSqr = 0;
            HardPointSoundMaxDistSqr = 0;
            AmmoSoundMaxDistSqr = 0;
            var fireSound = string.Concat(arc, kind.Audio.HardPoint.FiringSound);
            var hitSound = string.Concat(arc, kind.Audio.Ammo.HitSound);
            var travelSound = string.Concat(arc, kind.Audio.Ammo.TravelSound);
            var reloadSound = string.Concat(arc, kind.Audio.HardPoint.ReloadSound);
            var barrelSound = string.Concat(arc, kind.Audio.HardPoint.BarrelRotationSound);
            var hardPointSound = string.Concat(arc, kind.Audio.HardPoint.HardPointRotationSound);
            var noAmmoSound = string.Concat(arc, kind.Audio.HardPoint.NoAmmoSound);
            foreach (var def in Session.Instance.SoundDefinitions)
            {
                var id = def.Id.SubtypeId.String;
                if (FiringSound != FiringSoundState.None && id == fireSound)
                {
                    var ob = def.GetObjectBuilder() as MyObjectBuilder_AudioDefinition;
                    if (ob != null) FiringSoundDistSqr = ob.MaxDistance * ob.MaxDistance;
                    if (FiringSoundDistSqr > HardPointSoundMaxDistSqr) HardPointSoundMaxDistSqr = FiringSoundDistSqr;
                }
                if (AmmoHitSound && id == hitSound)
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

            if (kind.Graphics.ModelName != string.Empty && !kind.Graphics.Line.Trail)
            {
                ModelId = Session.Instance.ModelCount++;
                Session.Instance.ModelIdToName.Add(ModelId, kind.ModPath + kind.Graphics.ModelName);
            }
            else ModelId = -1;

            HasBarrelShootAv = BarrelEffect1 || BarrelEffect2 || HardPointRotationSound || FiringSound == FiringSoundState.WhenDone;
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
