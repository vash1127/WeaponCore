using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using VRage.Game;
using VRage.Utils;
using VRageMath;
using static CoreSystems.Support.PartAnimation;
using static CoreSystems.Support.WeaponDefinition;
using static CoreSystems.Support.WeaponDefinition.AnimationDef.PartAnimationSetDef;
using static CoreSystems.Support.WeaponDefinition.HardPointDef;
namespace CoreSystems.Support
{
    internal class CoreSystem
    {
        public const string Arc = "Arc";
        public HardwareDef.HardwareType PartType;
        public MyStringHash PartNameIdHash;
        public int PartIdHash;
        public int PartId;
        public bool StayCharged;
        public string PartName;
        public Session Session;

        public Dictionary<EventTriggers, PartAnimation[]> WeaponAnimationSet;
        public Dictionary<EventTriggers, uint> PartAnimationLengths;
        public Dictionary<EventTriggers, ParticleEvent[]> ParticleEvents;
        public HashSet<string> AnimationIdLookup;
        public Dictionary<string, EmissiveState> PartEmissiveSet;
        public Dictionary<string, Matrix[]> PartLinearMoveSet;
        public string[] HeatingSubparts;

    }

    internal class UpgradeSystem : CoreSystem
    {
        public readonly UpgradeDefinition Values;

        public bool AnimationsInited;

        public UpgradeSystem(Session session, MyStringHash partNameIdHash, UpgradeDefinition values, string partName, int partIdHash, int partId)
        {
            Session = session;

            PartNameIdHash = partNameIdHash;

            Values = values;
            PartIdHash = partIdHash;
            PartId = partId;
            PartName = partName;
            PartType = (HardwareDef.HardwareType)Values.HardPoint.HardWare.Type;
            StayCharged = values.HardPoint.Other.StayCharged;
            Session.CreateAnimationSets(Values.Animations, this, out WeaponAnimationSet, out PartEmissiveSet, out PartLinearMoveSet, out AnimationIdLookup, out PartAnimationLengths, out HeatingSubparts, out ParticleEvents);

        }
    }

    internal class SupportSystem : CoreSystem
    {
        public readonly SupportDefinition Values;

        public bool AnimationsInited;

        public SupportSystem(Session session, MyStringHash partNameIdHash, SupportDefinition values, string partName, int partIdHash, int partId)
        {
            Session = session;

            PartNameIdHash = partNameIdHash;

            Values = values;
            PartIdHash = partIdHash;
            PartId = partId;
            PartName = partName;
            StayCharged = values.HardPoint.Other.StayCharged;

            Session.CreateAnimationSets(Values.Animations, this, out WeaponAnimationSet, out PartEmissiveSet, out PartLinearMoveSet, out AnimationIdLookup, out PartAnimationLengths, out HeatingSubparts, out ParticleEvents);

        }
    }

    internal class PhantomSystem : CoreSystem
    {
        public readonly PhantomDefinition Values;

        public bool AnimationsInited;

        public PhantomSystem(Session session, MyStringHash partNameIdHash, PhantomDefinition values, string partName, int partIdHash, int partId)
        {
            Session = session;

            PartNameIdHash = partNameIdHash;

            Values = values;
            PartIdHash = partIdHash;
            PartId = partId;
            PartName = partName;
            PartType = (HardwareDef.HardwareType)Values.HardPoint.HardWare.Type;
            StayCharged = values.HardPoint.Other.StayCharged;

            Session.CreateAnimationSets(Values.Animations, this, out WeaponAnimationSet, out PartEmissiveSet, out PartLinearMoveSet, out AnimationIdLookup, out PartAnimationLengths, out HeatingSubparts, out ParticleEvents);

        }
    }

    internal class WeaponSystem : CoreSystem
    {
        internal class AmmoType
        {
            public MyDefinitionId AmmoDefinitionId;
            public MyDefinitionId EjectionDefinitionId;
            public AmmoDef AmmoDef;
            public string AmmoName;
            public bool IsShrapnel;
        }

        public readonly MyStringHash MuzzlePartName;
        public readonly MyStringHash AzimuthPartName;
        public readonly MyStringHash ElevationPartName;
        public readonly MyStringHash SpinPartName;
        public readonly WeaponDefinition Values;
        public readonly AmmoType[] AmmoTypes;
        public readonly Stack<MySoundPair> PreFirePairs = new Stack<MySoundPair>();
        public readonly Stack<MySoundPair> FirePerShotPairs = new Stack<MySoundPair>();
        public readonly Stack<MySoundPair> FireWhenDonePairs = new Stack<MySoundPair>();
        public readonly Stack<MySoundPair> RotatePairs = new Stack<MySoundPair>();
        public readonly Stack<MySoundPair> ReloadPairs = new Stack<MySoundPair>();

        public readonly Prediction Prediction;
        public readonly TurretType TurretMovement;
        public readonly FiringSoundState FiringSound;

        public readonly string AltScopeName;
        public readonly string AltEjectorName;
        public readonly string[] Muzzles;

        public readonly int ReloadTime;
        public readonly int DelayToFire;
        public readonly int CeaseFireDelay;
        public readonly int MinAzimuth;
        public readonly int MaxAzimuth;
        public readonly int MinElevation;
        public readonly int MaxElevation;
        public readonly int MaxHeat;
        public readonly int WeaponIdHash;
        public readonly int WeaponId;
        public readonly int BarrelsPerShot;
        public readonly int HeatPerShot;
        public readonly int RateOfFire;
        public readonly int BarrelSpinRate;
        public readonly int ShotsPerBurst;

        public readonly bool HasAmmoSelection;
        public readonly bool HasEjector;
        public readonly bool HasScope;
        public readonly bool HasBarrelRotation;
        public readonly bool BarrelEffect1;
        public readonly bool BarrelEffect2;
        public readonly bool HasBarrelShootAv;
        public readonly bool TargetSubSystems;
        public readonly bool OnlySubSystems;
        public readonly bool ClosestFirst;
        public readonly bool DegRof;
        public readonly bool TrackProjectile;
        public readonly bool TrackTopMostEntities;
        public readonly bool TrackGrids;
        public readonly bool TrackCharacters;
        public readonly bool TrackMeteors;
        public readonly bool TrackNeutrals;
        public readonly bool DesignatorWeapon;
        public readonly bool DelayCeaseFire;
        public readonly bool AlwaysFireFullBurst;
        public readonly bool WeaponReloadSound;
        public readonly bool NoAmmoSound;
        public readonly bool HardPointRotationSound;
        public readonly bool BarrelRotationSound;
        public readonly bool PreFireSound;
        public readonly bool LockOnFocus;
        public readonly bool HasGuidedAmmo;
        public readonly bool SuppressFire;
        public readonly bool HasSpinPart;
        public readonly double MaxTargetSpeed;
        public readonly double AzStep;
        public readonly double ElStep;

        public readonly float Barrel1AvTicks;
        public readonly float Barrel2AvTicks;
        public readonly float WepCoolDown;
        public readonly float MinTargetRadius;
        public readonly float MaxTargetRadius;
        public readonly float MaxAmmoVolume;
        public readonly float FullAmmoVolume;
        public readonly float FiringSoundDistSqr;
        public readonly float ReloadSoundDistSqr;
        public readonly float BarrelSoundDistSqr;
        public readonly float HardPointSoundDistSqr;
        public readonly float NoAmmoSoundDistSqr;
        public readonly float HardPointAvMaxDistSqr;
        public readonly float ApproximatePeakPower;

        public bool AnimationsInited;

        public enum FiringSoundState
        {
            None,
            PerShot,
            WhenDone
        }

        public enum TurretType
        {
            Full,
            AzimuthOnly,
            ElevationOnly,
            Fixed //not used yet
        }

        public WeaponSystem(Session session, MyStringHash partNameIdHash, MyStringHash muzzlePartName, MyStringHash azimuthPartName, MyStringHash elevationPartName, MyStringHash spinPartName, WeaponDefinition values, string partName, AmmoType[] weaponAmmoTypes, int weaponIdHash, int weaponId)
        {
            Session = session;

            PartNameIdHash = partNameIdHash;
            MuzzlePartName = muzzlePartName;
            DesignatorWeapon = muzzlePartName.String == "Designator";
            AzimuthPartName = azimuthPartName;
            ElevationPartName = elevationPartName;
            SpinPartName = spinPartName;
            HasSpinPart = !string.IsNullOrEmpty(SpinPartName.String) && !SpinPartName.String.Contains("None") && !SpinPartName.String.Equals(ElevationPartName.String) && !SpinPartName.String.Equals(AzimuthPartName.String) && !SpinPartName.String.Equals(MuzzlePartName.String);

            Values = values;
            StayCharged = values.HardPoint.Loading.StayCharged || values.HardPoint.Loading.ReloadTime == 0;
            Muzzles = values.Assignments.Muzzles;
            WeaponIdHash = weaponIdHash;
            WeaponId = weaponId;
            PartName = partName;
            AmmoTypes = weaponAmmoTypes;
            MaxAmmoVolume = Values.HardPoint.HardWare.InventorySize;
            FullAmmoVolume = MaxAmmoVolume * 0.75f;
            CeaseFireDelay = values.HardPoint.DelayCeaseFire;
            DelayCeaseFire = CeaseFireDelay > 0;
            DelayToFire = values.HardPoint.Loading.DelayUntilFire;
            ReloadTime = values.HardPoint.Loading.ReloadTime;
            MaxTargetSpeed = values.Targeting.StopTrackingSpeed > 0 ? values.Targeting.StopTrackingSpeed : double.MaxValue;
            ClosestFirst = values.Targeting.ClosestFirst;
            AlwaysFireFullBurst = Values.HardPoint.Loading.FireFullBurst;
            Prediction = Values.HardPoint.AimLeadingPrediction;
            LockOnFocus = Values.HardPoint.Ai.LockOnFocus && !Values.HardPoint.Ai.TrackTargets;
            SuppressFire = Values.HardPoint.Ai.SuppressFire;
            PartType = Values.HardPoint.HardWare.Type;
            HasEjector = !string.IsNullOrEmpty(Values.Assignments.Ejector);
            AltEjectorName = HasEjector ? "subpart_" + Values.Assignments.Ejector : string.Empty;
            HasScope = !string.IsNullOrEmpty(Values.Assignments.Scope);
            AltScopeName = HasScope ? "subpart_" + Values.Assignments.Scope : string.Empty;
            TurretMovements(out AzStep, out ElStep, out MinAzimuth, out MaxAzimuth, out MinElevation, out MaxElevation, out TurretMovement);
            Heat(out DegRof, out MaxHeat, out WepCoolDown, out HeatPerShot);
            BarrelValues(out BarrelsPerShot, out RateOfFire, out ShotsPerBurst);
            BarrelsAv(out BarrelEffect1, out BarrelEffect2, out Barrel1AvTicks, out Barrel2AvTicks, out BarrelSpinRate, out HasBarrelRotation);
            Track(out TrackProjectile, out TrackGrids, out TrackCharacters, out TrackMeteors, out TrackNeutrals, out TrackTopMostEntities);
            SubSystems(out TargetSubSystems, out OnlySubSystems);
            ValidTargetSize(out MinTargetRadius, out MaxTargetRadius);
            HardPointSoundSetup(out WeaponReloadSound, out HardPointRotationSound, out BarrelRotationSound, out NoAmmoSound, out PreFireSound, out HardPointAvMaxDistSqr, out FiringSound);
            HardPointSoundDistMaxSqr(AmmoTypes, out FiringSoundDistSqr, out ReloadSoundDistSqr, out BarrelSoundDistSqr, out HardPointSoundDistSqr, out NoAmmoSoundDistSqr, out HardPointAvMaxDistSqr);

            HasBarrelShootAv = BarrelEffect1 || BarrelEffect2 || HardPointRotationSound || FiringSound == FiringSoundState.WhenDone;

            Session.CreateAnimationSets(Values.Animations, this, out WeaponAnimationSet, out PartEmissiveSet, out PartLinearMoveSet, out AnimationIdLookup, out PartAnimationLengths, out HeatingSubparts, out ParticleEvents);

            uint delay;
            if (PartAnimationLengths.TryGetValue(EventTriggers.PreFire, out delay))
                if (delay > DelayToFire)
                    DelayToFire = (int)delay;

            var ammoSelections = 0;
            for (int i = 0; i < AmmoTypes.Length; i++)
            {

                var ammo = AmmoTypes[i];
                ammo.AmmoDef.Const = new AmmoConstants(ammo, Values, Session, this, i);
                if (ammo.AmmoDef.Const.GuidedAmmoDetected)
                    HasGuidedAmmo = true;

                if (ammo.AmmoDef.Const.IsTurretSelectable)
                    ++ammoSelections;

                if (ammo.AmmoDef.Const.ChargSize > ApproximatePeakPower)
                    ApproximatePeakPower = ammo.AmmoDef.Const.ChargSize;
            }
            HasAmmoSelection = ammoSelections > 1;
        }

        private void Heat(out bool degRof, out int maxHeat, out float wepCoolDown, out int heatPerShot)
        {
            degRof = Values.HardPoint.Loading.DegradeRof;
            maxHeat = Values.HardPoint.Loading.MaxHeat;
            wepCoolDown = Values.HardPoint.Loading.Cooldown;
            heatPerShot = Values.HardPoint.Loading.HeatPerShot;
            if (wepCoolDown < 0) wepCoolDown = 0;
            if (wepCoolDown > .95f) wepCoolDown = .95f;
        }

        private void BarrelsAv(out bool barrelEffect1, out bool barrelEffect2, out float barrel1AvTicks, out float barrel2AvTicks, out int barrelSpinRate, out bool hasBarrelRotation)
        {
            barrelEffect1 = Values.HardPoint.Graphics.Effect1.Name != string.Empty;
            barrelEffect2 = Values.HardPoint.Graphics.Effect2.Name != string.Empty;
            barrel1AvTicks = Values.HardPoint.Graphics.Effect1.Extras.MaxDuration;
            barrel2AvTicks = Values.HardPoint.Graphics.Effect2.Extras.MaxDuration;

            barrelSpinRate = 0;
            if (Values.HardPoint.Other.RotateBarrelAxis != 0)
            {
                if (Values.HardPoint.Loading.BarrelSpinRate > 0) barrelSpinRate = Values.HardPoint.Loading.BarrelSpinRate < 3600 ? Values.HardPoint.Loading.BarrelSpinRate : 3599;
                else barrelSpinRate = RateOfFire < 3699 ? RateOfFire : 3599;
            }
            hasBarrelRotation = barrelSpinRate > 0 && (HasSpinPart || (MuzzlePartName.String != "None" && !string.IsNullOrEmpty(MuzzlePartName.String)));
        }

        private void BarrelValues(out int barrelsPerShot, out int rateOfFire, out int shotsPerBurst)
        {
            barrelsPerShot = Values.HardPoint.Loading.BarrelsPerShot;
            rateOfFire = Values.HardPoint.Loading.RateOfFire;
            shotsPerBurst = Values.HardPoint.Loading.ShotsInBurst;
        }

        private void TurretMovements(out double azStep, out double elStep, out int minAzimuth, out int maxAzimuth, out int minElevation, out int maxElevation, out TurretType turretMove)
        {
            azStep = Values.HardPoint.HardWare.RotateRate;
            elStep = Values.HardPoint.HardWare.ElevateRate;
            minAzimuth = Values.HardPoint.HardWare.MinAzimuth;
            maxAzimuth = Values.HardPoint.HardWare.MaxAzimuth;
            minElevation = Values.HardPoint.HardWare.MinElevation;
            maxElevation = Values.HardPoint.HardWare.MaxElevation;

            turretMove = TurretType.Full;

            if (minAzimuth == maxAzimuth)
                turretMove = TurretType.ElevationOnly;
            if (minElevation == maxElevation && TurretMovement != TurretType.Full)
                turretMove = TurretType.Fixed;
            else if (minElevation == maxElevation)
                turretMove = TurretType.AzimuthOnly;
        }


        private void Track(out bool trackProjectile, out bool trackGrids, out bool trackCharacters, out bool trackMeteors, out bool trackNeutrals, out bool trackTopMostEntities)
        {
            trackProjectile = false;
            trackGrids = false;
            trackCharacters = false;
            trackMeteors = false;
            trackNeutrals = false;
            trackTopMostEntities = false;

            var threats = Values.Targeting.Threats;
            foreach (var threat in threats)
            {
                if (threat == TargetingDef.Threat.Projectiles)
                    trackProjectile = true;
                else if (threat == TargetingDef.Threat.Grids)
                {
                    trackGrids = true;
                    trackTopMostEntities = true;
                }
                else if (threat == TargetingDef.Threat.Characters)
                {
                    trackCharacters = true;
                    trackTopMostEntities = true;
                }
                else if (threat == TargetingDef.Threat.Meteors)
                {
                    trackMeteors = true;
                    trackTopMostEntities = true;
                }
                else if (threat == TargetingDef.Threat.Neutrals)
                {
                    trackNeutrals = true;
                    trackTopMostEntities = true;
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
                    if (system != TargetingDef.BlockTypes.Any) targetSubSystems = true;
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
            maxTargetRadius = (float)(maxDiameter > 0 ? maxDiameter * 0.5d : 8192);
        }


        private void HardPointSoundSetup(out bool weaponReloadSound, out bool hardPointRotationSound, out bool barrelRotationSound, out bool noAmmoSound, out bool preFireSound, out float hardPointAvMaxDistSqr, out FiringSoundState firingSound)
        {
            weaponReloadSound = Values.HardPoint.Audio.ReloadSound != string.Empty;
            hardPointRotationSound = Values.HardPoint.Audio.HardPointRotationSound != string.Empty;
            barrelRotationSound = Values.HardPoint.Audio.BarrelRotationSound != string.Empty;
            noAmmoSound = Values.HardPoint.Audio.NoAmmoSound != string.Empty;
            preFireSound = Values.HardPoint.Audio.PreFiringSound != string.Empty;

            var fSoundStart = Values.HardPoint.Audio.FiringSound;
            if (fSoundStart != string.Empty && Values.HardPoint.Audio.FiringSoundPerShot)
                firingSound = FiringSoundState.PerShot;
            else if (fSoundStart != string.Empty && !Values.HardPoint.Audio.FiringSoundPerShot)
                firingSound = FiringSoundState.WhenDone;
            else firingSound = FiringSoundState.None;

            hardPointAvMaxDistSqr = 0;
            if (Values.HardPoint.Graphics.Effect1.Extras.MaxDistance * Values.HardPoint.Graphics.Effect1.Extras.MaxDistance > HardPointAvMaxDistSqr)
                hardPointAvMaxDistSqr = Values.HardPoint.Graphics.Effect1.Extras.MaxDistance * Values.HardPoint.Graphics.Effect1.Extras.MaxDistance;

            if (Values.HardPoint.Graphics.Effect2.Extras.MaxDistance * Values.HardPoint.Graphics.Effect2.Extras.MaxDistance > HardPointAvMaxDistSqr)
                hardPointAvMaxDistSqr = Values.HardPoint.Graphics.Effect2.Extras.MaxDistance * Values.HardPoint.Graphics.Effect2.Extras.MaxDistance;
        }

        private void HardPointSoundDistMaxSqr(AmmoType[] weaponAmmo, out float firingSoundDistSqr, out float reloadSoundDistSqr, out float barrelSoundDistSqr, out float hardPointSoundDistSqr, out float noAmmoSoundDistSqr, out float hardPointAvMaxDistSqr)
        {
            var fireSound = string.Concat(Arc, Values.HardPoint.Audio.FiringSound);
            var reloadSound = string.Concat(Arc, Values.HardPoint.Audio.ReloadSound);
            var barrelSound = string.Concat(Arc, Values.HardPoint.Audio.BarrelRotationSound);
            var hardPointSound = string.Concat(Arc, Values.HardPoint.Audio.HardPointRotationSound);
            var noAmmoSound = string.Concat(Arc, Values.HardPoint.Audio.NoAmmoSound);

            firingSoundDistSqr = 0f;
            reloadSoundDistSqr = 0f;
            barrelSoundDistSqr = 0f;
            hardPointSoundDistSqr = 0f;
            noAmmoSoundDistSqr = 0f;
            hardPointAvMaxDistSqr = HardPointAvMaxDistSqr;

            foreach (var def in Session.SoundDefinitions)
            {
                var id = def.Id.SubtypeId.String;

                if (FiringSound != FiringSoundState.None && id == fireSound)
                {
                    var ob = def.GetObjectBuilder() as MyObjectBuilder_AudioDefinition;
                    if (ob != null) firingSoundDistSqr = ob.MaxDistance * ob.MaxDistance;
                    if (firingSoundDistSqr > hardPointAvMaxDistSqr) hardPointAvMaxDistSqr = FiringSoundDistSqr;
                }
                if (WeaponReloadSound && id == reloadSound)
                {
                    var ob = def.GetObjectBuilder() as MyObjectBuilder_AudioDefinition;
                    if (ob != null) reloadSoundDistSqr = ob.MaxDistance * ob.MaxDistance;
                    if (reloadSoundDistSqr > hardPointAvMaxDistSqr) hardPointAvMaxDistSqr = ReloadSoundDistSqr;

                }
                if (BarrelRotationSound && id == barrelSound)
                {
                    var ob = def.GetObjectBuilder() as MyObjectBuilder_AudioDefinition;
                    if (ob != null) barrelSoundDistSqr = ob.MaxDistance * ob.MaxDistance;
                    if (barrelSoundDistSqr > hardPointAvMaxDistSqr) hardPointAvMaxDistSqr = BarrelSoundDistSqr;
                }
                if (HardPointRotationSound && id == hardPointSound)
                {
                    var ob = def.GetObjectBuilder() as MyObjectBuilder_AudioDefinition;
                    if (ob != null) hardPointSoundDistSqr = ob.MaxDistance * ob.MaxDistance;
                    if (hardPointSoundDistSqr > hardPointAvMaxDistSqr) hardPointAvMaxDistSqr = HardPointSoundDistSqr;
                }
                if (NoAmmoSound && id == noAmmoSound)
                {
                    var ob = def.GetObjectBuilder() as MyObjectBuilder_AudioDefinition;
                    if (ob != null) noAmmoSoundDistSqr = ob.MaxDistance * ob.MaxDistance;
                    if (noAmmoSoundDistSqr > hardPointAvMaxDistSqr) hardPointAvMaxDistSqr = NoAmmoSoundDistSqr;
                }
            }

            if (firingSoundDistSqr <= 0)
                foreach (var ammoType in weaponAmmo)
                    if (ammoType.AmmoDef.Trajectory.MaxTrajectory * ammoType.AmmoDef.Trajectory.MaxTrajectory > firingSoundDistSqr)
                        firingSoundDistSqr = ammoType.AmmoDef.Trajectory.MaxTrajectory * ammoType.AmmoDef.Trajectory.MaxTrajectory;
        }
    }

}
