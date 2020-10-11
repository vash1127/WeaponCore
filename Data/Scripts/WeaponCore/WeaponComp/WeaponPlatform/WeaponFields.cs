using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Utils;
using VRageMath;
using WeaponCore.Support;
using static WeaponCore.Support.WeaponDefinition.AnimationDef.PartAnimationSetDef;
using static WeaponCore.Support.WeaponDefinition.HardPointDef.HardwareDef;

namespace WeaponCore.Platform
{
    public partial class Weapon
    {
        internal int NextMuzzle;
        internal volatile bool Casting;
        private readonly int _numOfBarrels;
        private readonly HashSet<string> _muzzlesToFire = new HashSet<string>();
        private readonly HashSet<string> _muzzlesFiring = new HashSet<string>();
        internal readonly Dictionary<int, string> MuzzleIdToName = new Dictionary<int, string>();
        
        internal readonly WeaponComponent Comp;
        internal readonly WeaponSystem System;
        internal readonly WeaponFrameCache WeaponCache;
        internal readonly WeaponAcquire Acquire;
        internal readonly Target Target;
        internal readonly Target NewTarget;
        internal readonly PartInfo MuzzlePart;
        internal readonly Dummy[] Dummies;
        internal readonly Dummy Ejector;
        internal readonly Muzzle[] Muzzles;
        internal readonly PartInfo AzimuthPart;
        internal readonly PartInfo ElevationPart;
        internal readonly bool AzimuthOnBase;
        internal readonly Dictionary<EventTriggers, ParticleEvent[]> ParticleEvents;
        internal readonly List<Action<long, int, ulong, long, Vector3D, bool>> Monitors = new List<Action<long, int, ulong, long, Vector3D, bool>>();
        internal readonly uint[] MIds = new uint[Enum.GetValues(typeof(PacketType)).Length];
        internal readonly uint WeaponCreatedTick;

        internal Action<object> CancelableReloadAction = (o) => {};
        private readonly int _numModelBarrels;
        private int _nextVirtual;
        private uint _ticksUntilShoot;
        private uint _spinUpTick;
        private uint _ticksBeforeSpinUp;
        internal bool HeatLoopRunning;
        internal bool PreFired;
        internal bool FinishBurst;
        internal bool LockOnFireState;
        internal bool ReloadSubscribed;
        internal bool ScheduleAmmoChange;
        internal uint LastMagSeenTick;
        internal uint GravityTick;
        internal uint ShootTick;
        internal uint TicksPerShot;
        internal uint LastSyncTick;
        internal uint PosChangedTick;
        internal uint ElevationTick;
        internal uint AzimuthTick;

        internal float HeatPerc;

        internal int ShortLoadId;
        internal int BarrelRate;
        internal int ArmorHits;
        internal int ShotsFired;
        internal int LastMuzzle;
        internal List<MyEntity> HeatingParts;
        internal Vector3D GravityPoint;
        internal Vector3D MyPivotPos;
        internal Vector3D MyPivotDir;
        internal Vector3D MyPivotUp;
        internal Vector3D AimOffset;
        internal MatrixD WeaponConstMatrix;
        internal LineD MyCenterTestLine;
        internal LineD MyBarrelTestLine;
        internal LineD MyPivotTestLine;
        internal LineD MyAimTestLine;
        internal LineD MyShootAlignmentLine;

        internal uint[] BeamSlot;

        internal MyOrientedBoundingBoxD TargetBox;
        internal LineD LimitLine;


        internal MathFuncs.Cone AimCone = new MathFuncs.Cone();
        internal Matrix[] BarrelRotationPerShot = new Matrix[10];
        internal MyParticleEffect[] BarrelEffects1;
        internal MyParticleEffect[] BarrelEffects2;
        internal MyParticleEffect[] HitEffects;
        internal MySoundPair ReloadSound;
        internal MySoundPair PreFiringSound;
        internal MySoundPair FiringSound;
        internal MySoundPair RotateSound;
        internal WeaponStateValues State;
        internal WeaponReloadValues Reload;
        internal TransferTarget TargetData;
        internal AmmoValues Ammo;
        internal WeaponSystem.WeaponAmmoTypes ActiveAmmoDef;
        internal int[] AmmoShufflePattern = {0};
        internal ParallelRayCallBack RayCallBack;

        internal readonly MyEntity3DSoundEmitter ReloadEmitter;
        internal readonly MyEntity3DSoundEmitter PreFiringEmitter;
        internal readonly MyEntity3DSoundEmitter FiringEmitter;
        internal readonly MyEntity3DSoundEmitter RotateEmitter;
        internal readonly Dictionary<EventTriggers, PartAnimation[]> AnimationsSet;
        internal readonly Dictionary<string, PartAnimation> AnimationLookup = new Dictionary<string, PartAnimation>();
        internal readonly bool PrimaryWeaponGroup;
        internal readonly bool AiOnlyWeapon;

        internal EventTriggers LastEvent;
        internal float RequiredPower;
        internal float MaxCharge;
        internal float UseablePower;
        internal float OldUseablePower;
        internal float BaseDamage;
        internal float Dps;
        internal float ShotEnergyCost;
        internal float LastHeat;
        internal uint CeaseFireDelayTick = uint.MaxValue / 2;
        internal uint LastRotateTick;
        internal uint LastTargetTick;
        internal uint LastTrackedTick;
        internal uint LastMuzzleCheck;
        internal uint LastSmartLosCheck;
        internal uint LastLoadedTick;
        internal uint WeaponReadyTick;
        internal uint OffDelay;
        internal uint ChargeUntilTick;
        internal uint ChargeDelayTicks;
        internal uint AnimationDelayTick;
        internal uint LastHeatUpdateTick;
        internal uint LastInventoryTick;
        internal int ProposedAmmoId = -1;
        internal int FireCounter;
        internal int UniqueId;
        internal int RateOfFire;
        internal int BarrelSpinRate;
        internal int WeaponId;
        internal int EnergyPriority;
        internal int LastBlockCount;
        internal int ClientStartId;
        internal int ClientEndId;
        internal int ClientMakeUpShots;
        internal float HeatPShot;
        internal float HsRate;
        internal float CurrentAmmoVolume;
        internal double Azimuth;
        internal double Elevation;
        internal double AimingTolerance;
        internal double RotationSpeed;
        internal double ElevationSpeed;
        internal double MaxAzToleranceRadians;
        internal double MinAzToleranceRadians;
        internal double MaxElToleranceRadians;
        internal double MinElToleranceRadians;
        internal double MaxAzimuthRadians;
        internal double MinAzimuthRadians;
        internal double MaxElevationRadians;
        internal double MinElevationRadians;
        internal double MaxTargetDistance;
        internal double MaxTargetDistanceSqr;
        internal double MinTargetDistance;
        internal double MinTargetDistanceSqr;

        internal bool ClientReloading;
        internal bool Rotating;
        internal bool IsTurret;
        internal bool TurretMode;
        internal bool TrackTarget;
        internal bool AiShooting;
        internal bool AiEnabled;
        internal bool IsShooting;
        internal bool PlayTurretAv;
        internal bool AvCapable;
        internal bool NoMagsToLoad;
        internal bool CurrentlyDegrading;
        internal bool FixedOffset;
        internal bool DrawingPower;
        internal bool RequestedPower;
        internal bool ResetPower;
        internal bool RecalcPower;
        internal bool ProjectilesNear;
        internal bool StopBarrelAv;
        internal bool AcquiringTarget;
        internal bool BarrelSpinning;
        internal bool ReturingHome;
        internal bool IsHome = true;
        internal bool CanUseEnergyAmmo;
        internal bool CanUseHybridAmmo;
        internal bool CanUseChargeAmmo;
        internal bool CanUseBeams;
        internal bool PauseShoot;
        internal bool LastEventCanDelay;
        internal bool Reloading;
        internal bool Charging;
        internal bool ClientStaticShot;
        internal bool ShootOnce;

        internal bool CheckInventorySystem = true;
        internal bool ShotReady
        {
            get
            {
                var reloading = (!ActiveAmmoDef.AmmoDef.Const.EnergyAmmo || ActiveAmmoDef.AmmoDef.Const.MustCharge) && (Reloading || Ammo.CurrentAmmo == 0);
                var canShoot = !State.Overheated && !reloading && !System.DesignatorWeapon;
                var shotReady = canShoot && !Charging && (ShootTick <= Comp.Session.Tick) && (AnimationDelayTick <= Comp.Session.Tick || !LastEventCanDelay);
                return shotReady;
            }
        }

        internal struct AmmoLoad
        {
            internal enum ChangeType
            {
                Add,
                Remove
            }

            internal MyPhysicalInventoryItem Item;
            internal int Amount;
            internal int OldId;
            internal ChangeType Change;
        }

        internal Weapon(MyEntity entity, WeaponSystem system, int weaponId, WeaponComponent comp, RecursiveSubparts parts, MyEntity elevationPart, MyEntity azimuthPart, string azimuthPartName, string elevationPartName)
        {

            System = system;
            Comp = comp;
            WeaponCreatedTick = System.Session.Tick;

            AnimationsSet = comp.Session.CreateWeaponAnimationSet(system, parts);
            foreach (var set in AnimationsSet) {
                foreach (var pa in set.Value) {
                    comp.AllAnimations.Add(pa);
                    AnimationLookup.Add(pa.AnimationId, pa);
                }
            }

            ParticleEvents = comp.Session.CreateWeaponParticleEvents(system, parts); 

            MyStringHash subtype;
            if (comp.MyCube.DefinitionId.HasValue && comp.Session.VanillaIds.TryGetValue(comp.MyCube.DefinitionId.Value, out subtype)) {
                if (subtype.String.Contains("Gatling"))
                    _numModelBarrels = 6;
                else
                    _numModelBarrels = System.Barrels.Length;
            }
            else
                _numModelBarrels = System.Barrels.Length;


            bool hitParticle = false;
            foreach (var ammoType in System.AmmoTypes)
            {
                var c = ammoType.AmmoDef.Const;
                if (c.EnergyAmmo) CanUseEnergyAmmo = true;
                if (c.IsHybrid) CanUseHybridAmmo = true;
                if (c.MustCharge) CanUseChargeAmmo = true;
                if (c.IsBeamWeapon) CanUseBeams = true;
                if (c.HitParticle) hitParticle = true;
            }

            comp.HasEnergyWeapon = comp.HasEnergyWeapon || CanUseEnergyAmmo || CanUseHybridAmmo;

            AvCapable = System.HasBarrelShootAv && !Comp.Session.DedicatedServer || hitParticle;

            if (AvCapable && system.FiringSound == WeaponSystem.FiringSoundState.WhenDone)
            {
                FiringEmitter = System.Session.Emitters.Count > 0 ? System.Session.Emitters.Pop() : new MyEntity3DSoundEmitter(null, false, 1f);
                FiringEmitter.CanPlayLoopSounds = true;
                FiringEmitter.Entity = Comp.MyCube;
                FiringSound = System.FireWhenDonePairs.Count > 0 ? System.FireWhenDonePairs.Pop() : new MySoundPair(System.Values.HardPoint.Audio.FiringSound, false);
            }

            if (AvCapable && system.PreFireSound)
            {
                PreFiringEmitter = System.Session.Emitters.Count > 0 ? System.Session.Emitters.Pop() : new MyEntity3DSoundEmitter(null, false, 1f);
                PreFiringEmitter.CanPlayLoopSounds = true;

                PreFiringEmitter.Entity = Comp.MyCube;
                PreFiringSound = System.PreFirePairs.Count > 0 ? System.PreFirePairs.Pop() : new MySoundPair(System.Values.HardPoint.Audio.PreFiringSound, false);
            }

            if (AvCapable && system.WeaponReloadSound)
            {
                ReloadEmitter = System.Session.Emitters.Count > 0 ? System.Session.Emitters.Pop() : new MyEntity3DSoundEmitter(null, false, 1f);
                ReloadEmitter.CanPlayLoopSounds = true;

                ReloadEmitter.Entity = Comp.MyCube;
                ReloadSound = System.ReloadPairs.Count > 0 ? System.ReloadPairs.Pop() : new MySoundPair(System.Values.HardPoint.Audio.ReloadSound, false);
            }

            if (AvCapable && system.BarrelRotationSound)
            {
                RotateEmitter = System.Session.Emitters.Count > 0 ? System.Session.Emitters.Pop() : new MyEntity3DSoundEmitter(null, false, 1f);
                RotateEmitter.CanPlayLoopSounds = true;

                RotateEmitter.Entity = Comp.MyCube;
                RotateSound = System.RotatePairs.Count > 0 ? System.RotatePairs.Pop() : new MySoundPair(System.Values.HardPoint.Audio.BarrelRotationSound, false);
            }

            if (AvCapable)
            {
                if (System.BarrelEffect1)
                    BarrelEffects1 = new MyParticleEffect[System.Values.Assignments.Barrels.Length];
                if (System.BarrelEffect2)
                    BarrelEffects2 = new MyParticleEffect[System.Values.Assignments.Barrels.Length];
                if (hitParticle && CanUseBeams)
                    HitEffects = new MyParticleEffect[System.Values.Assignments.Barrels.Length];
            }

            if (System.Armor != ArmorState.IsWeapon)
                Comp.HasArmor = true;

            WeaponId = weaponId;
            PrimaryWeaponGroup = WeaponId % 2 == 0;
            IsTurret = System.Values.HardPoint.Ai.TurretAttached;
            TurretMode = System.Values.HardPoint.Ai.TurretController;
            TrackTarget = System.Values.HardPoint.Ai.TrackTargets;

            if (System.Values.HardPoint.Ai.TurretController)
                AiEnabled = true;

            AimOffset = System.Values.HardPoint.HardWare.Offset;
            FixedOffset = System.Values.HardPoint.HardWare.FixedOffset;

            HsRate = System.Values.HardPoint.Loading.HeatSinkRate / 3;
            EnergyPriority = System.Values.HardPoint.Other.EnergyPriority;
            var toleranceInRadians = MathHelperD.ToRadians(System.Values.HardPoint.AimingTolerance);
            AimCone.ConeAngle = toleranceInRadians;
            AimingTolerance = Math.Cos(toleranceInRadians);

            if (Comp.Platform.Structure.PrimaryWeapon ==  weaponId)
                comp.TrackingWeapon = this;

            if (IsTurret && !TrackTarget)
                Target = comp.TrackingWeapon.Target;
            else Target = new Target(this, true);

            _numOfBarrels = System.Barrels.Length;
            BeamSlot = new uint[_numOfBarrels];
            Muzzles = new Muzzle[_numOfBarrels];
            Dummies = new Dummy[_numOfBarrels];
            WeaponCache = new WeaponFrameCache(_numOfBarrels);
            NewTarget = new Target(this);
            RayCallBack = new ParallelRayCallBack(this);
            Acquire = new WeaponAcquire(this);
            AzimuthPart = new PartInfo {Entity = azimuthPart};
            ElevationPart = new PartInfo {Entity = elevationPart};
            MuzzlePart = new PartInfo { Entity = entity };
            AzimuthOnBase = azimuthPart.Parent == comp.MyCube;
            AiOnlyWeapon = Comp.BaseType != WeaponComponent.BlockType.Turret || (Comp.BaseType == WeaponComponent.BlockType.Turret && (azimuthPartName != "MissileTurretBase1" && elevationPartName != "MissileTurretBarrels" && azimuthPartName != "InteriorTurretBase1" && elevationPartName != "InteriorTurretBase2" && azimuthPartName != "GatlingTurretBase1" && elevationPartName != "GatlingTurretBase2"));

            UniqueId = comp.Session.UniqueWeaponId;
            ShortLoadId = comp.Session.ShortLoadAssigner();

            MyEntity ejectorPart;
            if (System.HasEjector && Comp.Platform.Parts.NameToEntity.TryGetValue(System.Values.Assignments.Ejector, out ejectorPart))
                Ejector = new Dummy(ejectorPart,this, System.Values.Assignments.Ejector);

            Monitors = Comp.Monitors[WeaponId];
        }
    }
}
