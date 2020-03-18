using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using VRage;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Utils;
using VRageMath;
using WeaponCore.Support;
using static WeaponCore.Support.Target;
using static WeaponCore.Support.WeaponDefinition;
using static WeaponCore.Support.WeaponDefinition.AnimationDef.PartAnimationSetDef;

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
        internal Action<object> CancelableReloadAction = (o) => {};
        private readonly int _numModelBarrels;
        private int _nextVirtual;
        private int _fakeHeatTick;
        private uint _ticksUntilShoot;
        private uint _azimuthSubpartUpdateTick;
        private uint _prefiredTick;
        private uint _spinUpTick;
        private uint _ticksBeforeSpinUp;
        internal bool HeatLoopRunning;
        internal bool PreFired;
        internal bool FinishBurst;
        internal bool FirstSync = true;
        internal bool LockOnFireState;
        internal uint ShootTick;
        internal uint TicksPerShot;
        internal uint LastSyncTick;
        internal uint PosChangedTick = 1;
        internal double TimePerShot;
        internal int LoadId;
        internal int ShortLoadId;
        internal int BarrelRate;

        internal PartInfo MuzzlePart;
        internal PartInfo AzimuthPart;
        internal PartInfo ElevationPart;
        internal List<MyEntity> HeatingParts;
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
        internal WeaponSystem System;
        internal Dummy[] Dummies;
        internal Muzzle[] Muzzles;
        internal uint[] BeamSlot;
        internal WeaponComponent Comp;

        internal WeaponFrameCache WeaponCache;

        internal MyOrientedBoundingBoxD TargetBox;
        internal LineD LimitLine;

        internal Target Target;
        internal Target NewTarget;
        internal MathFuncs.Cone AimCone = new MathFuncs.Cone();
        internal Matrix[] BarrelRotationPerShot = new Matrix[10];
        internal MyParticleEffect[] BarrelEffects1;
        internal MyParticleEffect[] BarrelEffects2;
        internal MyParticleEffect[] HitEffects;
        internal MySoundPair ReloadSound;
        internal MySoundPair PreFiringSound;
        internal MySoundPair FiringSound;
        internal MySoundPair RotateSound;
        internal WeaponSettingsValues Set;
        internal WeaponStateValues State;
        internal WeaponTimings Timings;
        internal WeaponAmmoTypes ActiveAmmoDef;
        internal readonly MyEntity3DSoundEmitter ReloadEmitter;
        internal readonly MyEntity3DSoundEmitter PreFiringEmitter;
        internal readonly MyEntity3DSoundEmitter FiringEmitter;
        internal readonly MyEntity3DSoundEmitter RotateEmitter;
        internal readonly Dictionary<EventTriggers, PartAnimation[]> AnimationsSet;
        internal readonly Dictionary<string, PartAnimation> AnimationLookup = new Dictionary<string, PartAnimation>();
        internal readonly bool TrackProjectiles;
        internal readonly bool PrimaryWeaponGroup;

        internal EventTriggers LastEvent;
        internal PartAnimation CurLgstAnimPlaying;
        internal float RequiredPower;
        internal float UseablePower;
        internal float OldUseablePower;
        internal float BaseDamage;
        internal float Dps;
        internal float ShotEnergyCost;
        internal float LastHeat;
        internal uint CeaseFireDelayTick = int.MaxValue;
        internal uint LastTargetTick;
        internal uint LastTrackedTick;
        internal uint LastMuzzleCheck;
        internal int RateOfFire;
        internal int BarrelSpinRate;
        internal int WeaponId;
        internal int HsRate;
        internal int EnergyPriority;
        internal int LastBlockCount;
        internal float HeatPShot;
        internal float CurrentAmmoVolume;
        internal double Azimuth;
        internal double Elevation;
        internal double AimingTolerance;
        internal double RotationSpeed;
        internal double ElevationSpeed;
        internal double MaxAzimuthRadians;
        internal double MinAzimuthRadians;
        internal double MaxElevationRadians;
        internal double MinElevationRadians;
        internal bool IsTurret;
        internal bool TurretMode;
        internal bool TrackTarget;
        internal bool AiShooting;
        internal bool SeekTarget;
        internal bool AiEnabled;
        internal bool IsShooting;
        internal bool PlayTurretAv;
        internal bool AvCapable;
        internal bool OutOfAmmo;
        internal bool CurrentlyDegrading;
        internal bool HitOther;
        internal bool FixedOffset;
        internal bool AiOnlyWeapon;
        internal bool DrawingPower;
        internal bool RequestedPower;
        internal bool ResetPower;
        internal bool RecalcPower;
        internal bool ProjectilesNear;
        internal bool StopBarrelAv;
        internal bool AcquiringTarget;
        internal bool BarrelSpinning;
        internal bool AzimuthOnBase;
        internal bool ReturingHome;
        internal bool IsHome;
        internal bool CanUseEnergyAmmo;
        internal bool CanUseHybridAmmo;
        internal bool CanUseChargeAmmo;
        internal bool CanUseBeams;
        internal bool ShotReady
        {
            get
            {
                var reloading = (!ActiveAmmoDef.AmmoDef.Const.EnergyAmmo || ActiveAmmoDef.AmmoDef.Const.MustCharge) && (State.Sync.Reloading || OutOfAmmo);
                var canShoot = !State.Sync.Overheated && !reloading && !System.DesignatorWeapon;
                var shotReady = canShoot && !State.Sync.Charging && (ShootTick <= Comp.Session.Tick) && (Timings.ShootDelayTick <= Comp.Session.Tick);
                return shotReady;
            }
        }

        public enum TerminalActionState
        {
            ShootOn,
            ShootOff,
            ShootOnce,
            ShootClick,
        }

        public class Muzzle
        {
            public Muzzle(int id)
            {
                MuzzleId = id;
            }

            public Vector3D Position;
            public Vector3D Direction;
            public Vector3D DeviatedDir;
            public uint LastUpdateTick;
            public uint LastAv1Tick;
            public uint LastAv2Tick;
            public int MuzzleId;
            public bool Av1Looping;
            public bool Av2Looping;

        }

        public Weapon(MyEntity entity, WeaponSystem system, int weaponId, WeaponComponent comp, Dictionary<EventTriggers, PartAnimation[]> animationSets)
        {
            LoadId = comp.Session.LoadAssigner();
            ShortLoadId = comp.Session.ShortLoadAssigner();
            MuzzlePart = new PartInfo { Entity = entity };
            AnimationsSet = animationSets;

            if (AnimationsSet != null)
            {
                foreach (var set in AnimationsSet)
                {
                    for (int j = 0; j < set.Value.Length; j++)
                    {
                        var animation = set.Value[j];
                        AnimationLookup.Add(animation.AnimationId, animation);
                    }
                }
            }
            
            System = system;
            Comp = comp;

            MyStringHash subtype;
            if (comp.MyCube.DefinitionId.HasValue && comp.Session.VanillaIds.TryGetValue(comp.MyCube.DefinitionId.Value, out subtype))
            {
                if (subtype.String.Contains("Gatling"))
                    _numModelBarrels = 6;
                else
                    _numModelBarrels = System.Barrels.Length;
            }
            else
                _numModelBarrels = System.Barrels.Length;


            bool hitParticle = false;
            foreach (var ammoType in System.WeaponAmmoTypes)
            {
                var c = ammoType.AmmoDef.Const;
                if (c.EnergyAmmo) CanUseEnergyAmmo = true;
                if (c.IsHybrid) CanUseHybridAmmo = true;
                if (c.MustCharge) CanUseChargeAmmo = true;
                if (c.IsBeamWeapon) CanUseBeams = true;
                if (c.HitParticle) hitParticle = true;
            }

            comp.HasEnergyWeapon = comp.HasEnergyWeapon || CanUseEnergyAmmo || CanUseHybridAmmo;

            AvCapable = System.HasBarrelShootAv && !Comp.Session.DedicatedServer;

            if (AvCapable && system.FiringSound == WeaponSystem.FiringSoundState.WhenDone)
            {
                FiringEmitter = new MyEntity3DSoundEmitter(Comp.MyCube, true, 1f);
                FiringSound = new MySoundPair();
                FiringSound.Init(System.Values.HardPoint.Audio.FiringSound);
            }

            if (AvCapable && system.PreFireSound)
            {
                PreFiringEmitter = new MyEntity3DSoundEmitter(Comp.MyCube, true, 1f);
                PreFiringSound = new MySoundPair();
                PreFiringSound.Init(System.Values.HardPoint.Audio.PreFiringSound);
            }

            if (AvCapable && system.WeaponReloadSound)
            {
                ReloadEmitter = new MyEntity3DSoundEmitter(Comp.MyCube, true, 1f);
                ReloadSound = new MySoundPair();
                ReloadSound.Init(System.Values.HardPoint.Audio.ReloadSound);
            }

            if (AvCapable && system.BarrelRotationSound)
            {
                RotateEmitter = new MyEntity3DSoundEmitter(Comp.MyCube, true, 1f);
                RotateSound = new MySoundPair();
                RotateSound.Init(System.Values.HardPoint.Audio.BarrelRotationSound);
            }

            if (AvCapable)
            {
                if (System.BarrelEffect1) BarrelEffects1 = new MyParticleEffect[System.Values.Assignments.Barrels.Length];
                if (System.BarrelEffect2) BarrelEffects2 = new MyParticleEffect[System.Values.Assignments.Barrels.Length];
                if (hitParticle && CanUseBeams) HitEffects = new MyParticleEffect[System.Values.Assignments.Barrels.Length];
            }

            WeaponId = weaponId;
            PrimaryWeaponGroup = WeaponId % 2 == 0;
            IsTurret = System.Values.HardPoint.Ai.TurretAttached;
            TurretMode = System.Values.HardPoint.Ai.TurretController;
            TrackTarget = System.Values.HardPoint.Ai.TrackTargets;
            
            if (System.Values.HardPoint.Ai.TurretController)
            {
                AiEnabled = true;
                AimOffset = System.Values.HardPoint.HardWare.Offset;
                FixedOffset = System.Values.HardPoint.HardWare.FixedOffset;
            }

            HsRate = System.Values.HardPoint.Loading.HeatSinkRate;
            EnergyPriority = System.Values.HardPoint.Other.EnergyPriority;
            var toleranceInRadians = MathHelperD.ToRadians(System.Values.HardPoint.AimingTolerance);
            AimCone.ConeAngle = toleranceInRadians;
            AimingTolerance = Math.Cos(toleranceInRadians);



            _numOfBarrels = System.Barrels.Length;
            BeamSlot = new uint[_numOfBarrels];
            Target = new Target(comp.MyCube);
            NewTarget = new Target(comp.MyCube);
            WeaponCache = new WeaponFrameCache(System.Values.Assignments.Barrels.Length);
            TrackProjectiles = System.TrackProjectile;
        }
    }
}
