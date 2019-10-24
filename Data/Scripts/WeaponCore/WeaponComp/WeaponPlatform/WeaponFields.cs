using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Entity;
using VRageMath;
using WeaponCore.Support;

namespace WeaponCore.Platform
{
    public partial class Weapon
    {
        internal int NextMuzzle;
        internal volatile bool Casting;

        private readonly int _numOfBarrels;
        private readonly HashSet<string> _muzzlesToFire = new HashSet<string>();
        internal readonly Dictionary<int, string> MuzzleIdToName = new Dictionary<int, string>();

        private int _shotsInCycle;
        private int _shots = 1;
        private int _nextVirtual;
        private uint _ticksUntilShoot;
        private uint _posChangedTick = 1;
        private uint _lastShotTick;
        private uint _reloadedTick;
        internal uint TicksPerShot;
        internal double TimePerShot;

        private bool _newCycle;
        //private bool _firstRun = true;
        internal MyTuple<MyEntity, Matrix, Matrix> MuzzlePart;
        internal MyTuple<MyEntity, Matrix, Matrix, Matrix, Matrix> AzimuthPart;
        internal MyTuple<MyEntity, Matrix, Matrix, Matrix, Matrix> ElevationPart;
        internal Vector3D MyPivotPos;
        internal Vector3D MyPivotDir;
        internal Vector3D MyPivotUp;
        internal Vector3D MyPivotLeft;
        internal Vector3D AimOffset;
        internal MatrixD MyPivotMatrix;
        internal LineD MyCenterTestLine;
        internal LineD MyBarrelTestLine;
        internal LineD MyPivotTestLine;
        internal LineD MyAimTestLine;
        internal LineD MyPivotDirLine;
        internal LineD MyShootAlignmentLine;
        internal WeaponSystem System;
        internal Dummy[] Dummies;
        internal Muzzle[] Muzzles;
        internal uint[] BeamSlot;
        internal WeaponComponent Comp;

        internal WeaponFrameCache WeaponCache = new WeaponFrameCache();

        internal Target Target;
        internal Target NewTarget;
        internal Vector3D TargetPos;
        internal Vector3D[] TestPoints = new Vector3D[5];
        internal MathFuncs.Cone AimCone = new MathFuncs.Cone();
        internal Matrix BarrelRotationPerShot;
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
        internal readonly Dictionary<EventTriggers, HashSet<PartAnimation>> AnimationsSet;
        internal readonly Dictionary<MyEntity, Vector3D> SleepingTargets = new Dictionary<MyEntity, Vector3D>();
        internal float RequiredPower;
        internal float BaseDamage;
        internal float ShotEnergyCost;
        internal float Dps;
        internal float AreaEffectDmg;
        internal float DetonateDmg;
        internal float LastHeat;
        internal uint ShotCounter;
        internal uint LastTargetTick;
        internal uint TargetCheckTick;
        internal uint FirstFireDelay;
        internal uint LastTrackedTick;
        internal uint OffDelay;
        internal int RateOfFire;
        internal int CurrentAmmo;
        internal int AmmoMagTimer = int.MaxValue;
        internal int DelayFireCount;
        internal int WeaponId;
        internal int HsRate;
        internal int EnergyPriority;
        internal int LastBlockCount;
        internal int AzZeroCrossCount;
        internal int ElZeroCrossCount;
        internal float HeatPShot;
        internal float CurrentAmmoVolume;
        internal MyFixedPoint CurrentMags;
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
        internal bool AiReady;
        internal bool SeekTarget;
        internal bool TrackingAi;
        internal bool IsTracking;
        internal bool IsAligned;
        internal bool IsShooting;
        internal bool PlayTurretAv;
        internal bool AvCapable;
        internal bool DelayCeaseFire;
        internal bool TargetWasExpired = true;
        internal bool Reloading;
        internal bool FirstLoad = true;
        internal bool ReturnHome;
        internal bool CurrentlyDegrading;
        internal bool SleepTargets;
        internal bool HitOther;
        internal bool FixedOffset;
        internal bool TurretTargetLock;
        internal double LastAzDiff;
        internal double LastElDiff;
        internal TerminalActionState ManualShoot = TerminalActionState.ShootOff;
        internal HardPointDefinition.Prediction Prediction;

        internal enum TerminalActionState
        {
            ShootOn,
            ShootOff,
            ShootOnce,
            ShootClick,
        }

        internal bool LoadAmmoMag
        {
            set
            {
                if (value)
                {
                    Comp.BlockInventory.RemoveItemsOfType(1, System.AmmoDefId);
                    AmmoMagTimer = FirstLoad ? 1 : System.ReloadTime;
                    _reloadedTick = Comp.Ai.Session.Tick + (uint)AmmoMagTimer;
                    FirstLoad = false;
                }
            }
        }

        internal bool AmmoMagLoaded
        {
            get
            {
                if (_reloadedTick > Comp.Ai.Session.Tick) return false;
                CurrentAmmo = System.MagazineDef.Capacity;
                AmmoMagTimer = int.MaxValue;
                return true;
            }
        }

        public enum EventTriggers
        {
            Reloading,
            Firing,
            Tracking,
            Overheated,
            TurnOn,
            TurnOff,
            BurstReload,
            OutOfAmmo,
            PreFire,
            EmptyOnGameLoad
        }

        public Weapon(MyEntity entity, WeaponSystem system, int weaponId, WeaponComponent comp, Dictionary<EventTriggers, HashSet<PartAnimation>> animationSets)
        {
            MuzzlePart = new MyTuple<MyEntity, Matrix, Matrix> {Item1 = entity };
            AnimationsSet = animationSets;
            System = system;
            Comp = comp;
            comp.HasEnergyWeapon = comp.HasEnergyWeapon || System.EnergyAmmo || System.IsHybrid;

            AvCapable = System.HasBarrelShootAv && !Comp.Ai.Session.DedicatedServer;

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
            IsTurret = System.Values.HardPoint.Block.TurretAttached;
            TurretMode = System.Values.HardPoint.Block.TurretController;
            TrackTarget = System.Values.HardPoint.Block.TrackTargets;
            HsRate = System.Values.HardPoint.Loading.HeatSinkRate;
            EnergyPriority = System.Values.HardPoint.EnergyPriority;
            var toleranceInRadians = MathHelperD.ToRadians(System.Values.HardPoint.AimingTolerance);
            AimCone.ConeAngle = toleranceInRadians;
            AimingTolerance = Math.Cos(toleranceInRadians);
            Prediction = System.Values.HardPoint.AimLeadingPrediction;

            _numOfBarrels = System.Barrels.Length;
            DelayCeaseFire = System.TimeToCeaseFire > 0;
            BeamSlot = new uint[_numOfBarrels];
            Target = new Target(comp.MyCube);
            NewTarget = new Target(comp.MyCube);
            var gridRadiusSqr = (Comp.Ai.GridRadius * Comp.Ai.GridRadius);
            if (System.MaxTrajectorySqr + gridRadiusSqr> Comp.Ai.MaxTargetingRangeSqr)
            {
                Comp.Ai.MaxTargetingRange = System.MaxTrajectory + Comp.Ai.GridRadius;
                Comp.Ai.MaxTargetingRangeSqr = System.MaxTrajectorySqr + gridRadiusSqr;
            }
        }
    }
}
