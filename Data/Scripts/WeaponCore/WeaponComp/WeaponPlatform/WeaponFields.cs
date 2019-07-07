using System;
using Sandbox.Game.Entities;
using VRage;
using VRage.Game;
using VRage.Game.Entity;
using VRage.ModAPI;
using VRageMath;
using WeaponCore.Support;

namespace WeaponCore.Platform
{
    public partial class Weapon
    {
        internal const uint SuspendAmmoCount = 300;
        internal const uint UnSuspendAmmoCount = 1200;
        internal int NextMuzzle;

        private readonly Vector3 _localTranslation;

        private MyEntity _lastTarget;
        //private MatrixD _weaponMatrix;
        //private MatrixD _oldWeaponMatrix;
        ///private Vector3D _weaponPosition;
        //private Vector3D _oldWeaponPosition;
        private Vector3D _lastPredictedPos;
        private double _lastTimeToIntercept;
        private int _rotationTime;
        private int _numOfBarrels;
        private int _shotsInCycle;
        private uint _lastPredictionTick;
        private uint _posChangedTick = 1;
        private uint _targetTick;
        private uint _ticksPerShot;
        private double _timePerShot;


        private bool _newCycle = false;
        //private bool _firstRun = true;

        internal IMyEntity EntityPart;
        internal WeaponSystem System;
        internal WeaponDefinition Kind;
        internal Dummy[] Dummies;
        internal Muzzle[] Muzzles;
        internal WeaponComponent Comp;
        internal uint[] BeamSlot { get; set; }
        internal MyEntity Target;
        internal Vector3D TargetPos;
        internal Vector3D TargetDir;

        internal MyParticleEffect MuzzleEffect1;
        internal MyParticleEffect MuzzleEffect2;

        internal MySoundPair ReloadSound;
        internal MySoundPair FiringSound;
        internal readonly MyEntity3DSoundEmitter ReloadEmitter;
        internal readonly MyEntity3DSoundEmitter FiringEmitter;

        internal uint SuspendAmmoTick;
        internal uint UnSuspendAmmoTick;
        internal uint ShotCounter;
        internal int CurrentAmmo;
        internal int AmmoMagTimer = int.MaxValue;
        internal MyFixedPoint CurrentMags;
        internal float Azimuth;
        internal float Elevation;
        internal float DesiredAzimuth;
        internal float DesiredElevation;
        internal double AimingTolerance;
        internal int WeaponId;
        internal uint CheckedForTargetTick;
        internal uint TicksUntilShoot;
        internal float RotationSpeed;
        internal float ElevationSpeed;
        internal float MaxAzimuthRadians;
        internal float MinAzimuthRadians;
        internal float MaxElevationRadians;
        internal float MinElevationRadians;

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

        public Weapon(IMyEntity entity, WeaponSystem system, int weaponId, WeaponComponent comp)
        {
            EntityPart = entity;
            _localTranslation = entity.LocalMatrix.Translation;
            System = system;
            Kind = system.Kind;
            Comp = comp;

            if (system.FiringSound == WeaponSystem.FiringSoundState.Full)
            {
                FiringEmitter = new MyEntity3DSoundEmitter(Comp.MyCube, true, 1f);
                FiringSound = new MySoundPair();
                FiringSound.Init(Kind.Audio.HardPoint.FiringSound);
            }

            if (system.TurretReloadSound)
            {
                ReloadEmitter = new MyEntity3DSoundEmitter(Comp.MyCube, true, 1f);
                ReloadSound = new MySoundPair();
                ReloadSound.Init(Kind.Audio.HardPoint.ReloadSound);
            }

            WeaponId = weaponId;
            IsTurret = Kind.HardPoint.IsTurret;
            TurretMode = Kind.HardPoint.TurretController;
            TrackTarget = Kind.HardPoint.TrackTargets;
            AimingTolerance = Math.Cos(MathHelper.ToRadians(Kind.HardPoint.AimingTolerance));
            _ticksPerShot = (uint)(3600 / Kind.HardPoint.RateOfFire);
            _timePerShot = (3600d / Kind.HardPoint.RateOfFire);
            _numOfBarrels = System.Barrels.Length;

            BeamSlot = new uint[_numOfBarrels];
        }
    }
}
