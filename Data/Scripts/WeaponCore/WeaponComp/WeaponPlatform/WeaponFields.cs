using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Entity;
using VRage.ModAPI;
using VRageMath;
using WeaponCore.Projectiles;
using WeaponCore.Support;

namespace WeaponCore.Platform
{
    public partial class Weapon
    {
        internal const uint SuspendAmmoCount = 300;
        internal const uint UnSuspendAmmoCount = 1200;
        internal int NextMuzzle;
        internal static bool UiSet;
        internal volatile bool Casting;

        private readonly Vector3 _localTranslation;
        private readonly int _numOfBarrels;

        private int _rotationTime;
        private int _shotsInCycle;
        private int _shots = 1;
        private int _nextVirtual;
        private uint _ticksUntilShoot;
        private uint _posChangedTick = 1;
        private uint _lastShotTick;
        internal uint TicksPerShot;
        internal double TimePerShot;

        private bool _newCycle;
        //private bool _firstRun = true;

        internal MyEntity EntityPart;
        internal WeaponSystem System;
        internal Dummy[] Dummies;
        internal Muzzle[] Muzzles;
        internal uint[] BeamSlot;
        internal WeaponComponent Comp;

        internal WeaponFrameCache WeaponCache = new WeaponFrameCache();

        internal Target Target;
        internal Target NewTarget;
        internal Vector3D TargetPos;
        internal MathFuncs.Cone AimCone = new MathFuncs.Cone();
        internal MyParticleEffect[] BarrelEffects1;
        internal MyParticleEffect[] BarrelEffects2;
        internal MyParticleEffect[] HitEffects;
        internal MySoundPair ReloadSound;
        internal MySoundPair FiringSound;
        internal MySoundPair RotateSound;
        internal readonly MyEntity3DSoundEmitter ReloadEmitter;
        internal readonly MyEntity3DSoundEmitter FiringEmitter;
        internal readonly MyEntity3DSoundEmitter RotateEmitter;
        internal readonly CachingDictionary<Muzzle, uint> BarrelAvUpdater = new CachingDictionary<Muzzle, uint>();
        internal float RequiredPower;
        internal float BaseDamage;
        internal float ShotEnergyCost;
        internal float DPS;
        internal float areaEffectDmg;
        internal float detonateDmg;
        internal uint SuspendAmmoTick;
        internal uint UnSuspendAmmoTick;
        internal uint ShotCounter;
        internal uint LastTargetCheck;
        internal int RateOfFire;
        internal int CurrentAmmo;
        internal int AmmoMagTimer = int.MaxValue;
        internal int DelayFireCount;
        internal int WeaponId;
        internal int HsRate;
        internal int EnergyPriority;
        internal int HeatPShot;
        internal MyFixedPoint CurrentMags;
        internal double Azimuth;
        internal double Elevation;
        internal double DesiredAzimuth;
        internal double DesiredElevation;
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
        internal bool AiReady;
        internal bool SeekTarget;
        internal bool TrackingAi;
        internal bool IsTracking;
        internal bool IsInView;
        internal bool IsAligned;
        internal bool AmmoSuspend;
        internal bool AmmoFull;
        internal bool IsShooting;
        internal bool BarrelMove;
        internal bool PlayTurretAv;
        internal bool AvCapable;
        internal bool DelayCeaseFire;
        internal bool TargetWasExpired;
        internal bool Reloading;
        internal TerminalActionState ManualShoot = TerminalActionState.ShootOff;
        internal HardPointDefinition.Prediction Prediction;

        internal enum TerminalActionState
        {
            ShootOn,
            ShootOff,
            ShootOnce,
            ShootClick,
        }
        internal readonly List<string> FiringStrings = new List<string>()
        {
            "Firing0",
            "Firing1",
            "Firing2",
            "Firing3",
            "Firing4",
            "Firing5",
        };

        internal bool LoadAmmoMag
        {
            set
            {
                if (value)
                {
                    Comp.BlockInventory.RemoveItemsOfType(1, System.AmmoDefId);
                    AmmoMagTimer = System.ReloadTime;
                }
            }
        }

        internal bool AmmoMagLoaded
        {
            get
            {
                if (--AmmoMagTimer > 0) return false;
                CurrentAmmo = System.MagazineDef.Capacity;
                AmmoMagTimer = int.MaxValue;
                return true;
            }
        }

        internal enum Emissives
        {
            Reloading,
            Firing,
            Tracking,
            Heating,
        }

        public Weapon(MyEntity entity, WeaponSystem system, int weaponId, WeaponComponent comp)
        {
            EntityPart = entity;
            _localTranslation = entity.PositionComp.LocalMatrix.Translation;
            System = system;
            Comp = comp;
            comp.HasEnergyWeapon = comp.HasEnergyWeapon || System.EnergyAmmo || System.IsHybrid;

            AvCapable = System.HasBarrelShootAv && !Session.Instance.DedicatedServer;

            if (AvCapable && system.FiringSound == WeaponSystem.FiringSoundState.WhenDone)
            {
                FiringEmitter = new MyEntity3DSoundEmitter(Comp.MyCube, true, 1f);
                FiringSound = new MySoundPair();
                FiringSound.Init(System.Values.Audio.HardPoint.FiringSound);
            }

            if (AvCapable && system.WeaponReloadSound)
            {
                ReloadEmitter = new MyEntity3DSoundEmitter(Comp.MyCube, true, 1f);
                ReloadSound = new MySoundPair();
                ReloadSound.Init(System.Values.Audio.HardPoint.ReloadSound);
            }

            if (AvCapable && system.BarrelRotationSound && system.Values.HardPoint.RotateBarrelAxis != 0)
            {
                RotateEmitter = new MyEntity3DSoundEmitter(Comp.MyCube, true, 1f);
                RotateSound = new MySoundPair();
                RotateSound.Init(System.Values.Audio.HardPoint.BarrelRotationSound);
            }

            if (AvCapable)
            {
                if (System.BarrelEffect1) BarrelEffects1 = new MyParticleEffect[System.Values.Assignments.Barrels.Length];
                if (System.BarrelEffect2) BarrelEffects2 = new MyParticleEffect[System.Values.Assignments.Barrels.Length];
                if (System.HitParticle && System.IsBeamWeapon) HitEffects = new MyParticleEffect[System.Values.Assignments.Barrels.Length];
            }

            WeaponId = weaponId;
            IsTurret = System.Values.HardPoint.IsTurret;
            TurretMode = System.Values.HardPoint.TurretController;
            TrackTarget = System.Values.HardPoint.TrackTargets;
            HsRate = System.Values.HardPoint.Loading.HeatSinkRate;
            EnergyPriority = System.Values.HardPoint.EnergyPriority;
            var toleranceInRadians = MathHelper.ToRadians(System.Values.HardPoint.AimingTolerance);
            AimCone.ConeAngle = toleranceInRadians;
            AimingTolerance = Math.Cos(toleranceInRadians);
            Prediction = System.Values.HardPoint.AimLeadingPrediction;

            _numOfBarrels = System.Barrels.Length;
            DelayCeaseFire = System.TimeToCeaseFire > 0;
            BeamSlot = new uint[_numOfBarrels];
            Target = new Target(comp.MyCube);
            NewTarget = new Target(comp.MyCube);
            if (System.MaxTrajectorySqr > Comp.Ai.MaxTargetingRangeSqr)
            {
                Comp.Ai.MaxTargetingRange = System.MaxTrajectory;
                Comp.Ai.MaxTargetingRangeSqr = System.MaxTrajectorySqr;
            }
        }

        internal void UpdateRequiredPower()
        {
            if (System.EnergyAmmo || System.IsHybrid)
                RequiredPower = ((ShotEnergyCost * (RateOfFire * MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS)) * System.Values.HardPoint.Loading.BarrelsPerShot) * System.Values.HardPoint.Loading.TrajectilesPerBarrel;
            else
                RequiredPower = Comp.IdlePower;

            Comp.MaxRequiredPower += RequiredPower;
        }

        internal void UpdateShotEnergy()
        {
            ShotEnergyCost = System.Values.HardPoint.EnergyCost * BaseDamage;
        }
    }
}
