using System;
using Sandbox.Game.Entities;
using VRage;
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
        private MatrixD _weaponMatrix;
        private MatrixD _oldWeaponMatrix;
        private Vector3D _weaponPosition;
        private Vector3D _oldWeaponPosition;
        private Vector3D _lastPredictedPos;
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
        internal WeaponSystem WeaponSystem;
        internal WeaponDefinition WeaponType;
        internal Dummy[] Dummies;
        internal Muzzle[] Muzzles;
        internal WeaponComponent Comp;
        internal uint[] BeamSlot { get; set; }
        internal MyEntity Target;
        internal Vector3D TargetPos;
        internal Vector3D TargetDir;

        internal uint SuspendAmmoTick;
        internal uint UnSuspendAmmoTick;
        internal uint ShotCounter;
        internal int CurrentAmmo;
        internal MyFixedPoint CurrentMags;
        internal double Azimuth;
        internal double Elevation;
        internal double DesiredAzimuth;
        internal double DesiredElevation;
        internal double AimingTolerance;
        internal int WeaponId;
        internal uint CheckedForTargetTick;
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
        public Weapon(IMyEntity entity, WeaponSystem weaponSystem, int weaponId)
        {
            EntityPart = entity;
            _localTranslation = entity.LocalMatrix.Translation;
            WeaponSystem = weaponSystem;
            WeaponType = weaponSystem.WeaponType;

            WeaponId = weaponId;
            IsTurret = WeaponType.TurretDef.IsTurret;
            TurretMode = WeaponType.TurretDef.TurretController;
            TrackTarget = WeaponType.TurretDef.TrackTargets;
            AimingTolerance = Math.Cos(MathHelper.ToRadians(WeaponType.TurretDef.AimingTolerance));
            _ticksPerShot = (uint)(3600 / WeaponType.TurretDef.RateOfFire);
            _timePerShot = (3600d / WeaponType.TurretDef.RateOfFire);
            _numOfBarrels = WeaponSystem.Barrels.Length;

            BeamSlot = new uint[_numOfBarrels];
        }
    }
}
