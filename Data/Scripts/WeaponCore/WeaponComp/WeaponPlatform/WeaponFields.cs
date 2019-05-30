using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using VRage.Game.Entity;
using VRage.ModAPI;
using VRageMath;
using WeaponCore.Support;

namespace WeaponCore.Platform
{
    public partial class Weapon
    {
        public Weapon(IMyEntity entity, WeaponSystem weaponSystem)
        {
            EntityPart = entity;
            _localTranslation = entity.LocalMatrix.Translation;
            _pivotOffsetVec = (Vector3.Transform(entity.PositionComp.LocalAABB.Center, entity.PositionComp.LocalMatrix) - entity.GetTopMostParent(typeof(MyCubeBlock)).PositionComp.LocalAABB.Center);
            _upPivotOffsetLen = _pivotOffsetVec.Length();

            WeaponSystem = weaponSystem;
            WeaponType = weaponSystem.WeaponType;
            TurretMode = WeaponType.TurretMode;
            TrackTarget = WeaponType.TrackTarget;
            _ticksPerShot = (uint)(3600 / WeaponType.RateOfFire);
            _timePerShot = (3600d / WeaponType.RateOfFire);
            _numOfBarrels = WeaponSystem.Barrels.Length;

            BeamSlot = new uint[_numOfBarrels];

            FiringSoundPair = new MySoundPair(WeaponType.FiringSound);
            ReloadSoundPair = new MySoundPair(WeaponType.ReloadSound);
            AmmoTravelSoundPair = new MySoundPair(WeaponType.AmmoTravelSound);
            AmmoHitSoundPair = new MySoundPair(WeaponType.AmmoHitSound);
        }

        public IMyEntity EntityPart;
        public WeaponSystem WeaponSystem;
        public WeaponDefinition WeaponType;
        public Dummy[] Dummies;
        public Muzzle[] Muzzles;
        public WeaponComponent Comp;
        public uint[] BeamSlot { get; set; }
        public MyEntity Target;
        public readonly MySoundPair FiringSoundPair;
        public readonly MySoundPair ReloadSoundPair;
        public readonly MySoundPair AmmoTravelSoundPair;
        public readonly MySoundPair AmmoHitSoundPair;

        private readonly Vector3 _localTranslation;
        private readonly float _upPivotOffsetLen;

        private MatrixD _weaponMatrix;
        private MatrixD _oldWeaponMatrix;
        private Vector3D _weaponPosition;
        private Vector3D _oldWeaponPosition;
        private Vector3 _pivotOffsetVec;
        private int _rotationTime;
        private int _numOfBarrels;
        private int _shotsInCycle;
        private int _nextMuzzle;
        private uint _posUpdatedTick = uint.MinValue;
        private uint _posChangedTick = 1;
        private uint _targetTick;
        private uint _ticksPerShot;
        internal uint ShotCounter;
        private double _timePerShot;
        private double _azimuth;
        private double _elevation;
        private double _desiredAzimuth;
        private double _desiredElevation;

        private bool _newCycle = false;
        //private bool _firstRun = true;
        private bool _azOk;
        private bool _elOk;

        internal Vector3D MyPivotPos;
        internal bool TurretMode { get; set; }
        internal bool TrackTarget { get; set; }
        internal bool ReadyToTrack => Target != null && (_azOk && _elOk || !WeaponType.TurretMode);
        internal bool ReadyToShoot => Comp.MyAi.WeaponReady && ReadyToTrack;
        internal bool SeekTarget => Target == null || Target != null && Target.MarkedForClose;

    }
}
