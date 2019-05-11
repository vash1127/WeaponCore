using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI.Ingame;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;
using WeaponCore.Support;
using IMyLargeTurretBase = Sandbox.ModAPI.IMyLargeTurretBase;

namespace WeaponCore
{
    public class Weapon
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
        }

        public IMyEntity EntityPart;
        public WeaponSystem WeaponSystem;
        public WeaponDefinition WeaponType;
        public Dummy[] Dummies;
        public Muzzle[] Muzzles;
        public Logic Logic;
        public MyEntity Target { get; set; }
        public Random Rnd = new Random(902138212);

        private readonly Vector3 _localTranslation;
        private readonly float _upPivotOffsetLen;

        private MatrixD _weaponMatrix;
        private MatrixD _oldWeaponMatrix;
        private Vector3D _weaponPosition;
        private Vector3D _oldWeaponPosition;
        private Vector3 _pivotOffsetVec;

        private int _rotationTime;
        private int _numOfBarrels;
        private int _nextMuzzle;
        private uint _posUpdatedTick = uint.MinValue;
        private uint _posChangedTick = 1;
        private uint _targetTick;
        private uint _ticksPerShot;
        private uint _shotCounter;
        private double _timePerShot;
        private double _step = 0.05d;
        private double _azimuth;
        private double _elevation;
        private double _desiredAzimuth;
        private double _desiredElevation;

        private bool _firstRun = true;
        private bool _weaponReady = true;
        private bool _azOk;
        private bool _elOk;

        internal bool TurretMode { get; set; }
        internal bool TrackTarget { get; set; }
        internal bool ReadyToTrack => Target != null && Logic.Turret.Target != Target && _azOk && _elOk;
        internal bool ReadyToShoot => _weaponReady && Target != null && Logic.Turret.Target == Target;
        internal bool TargetSwap => (Target != null || !Logic.Turret.HasTarget) && _targetTick++ > 240 || _firstRun;

        public void PositionChanged(MyPositionComponentBase pComp)
        {
            _posChangedTick = Session.Instance.Tick;
        }

        internal void Shoot()
        {
            var tick = Session.Instance.Tick;
            var rotateAxis = WeaponType.RotateBarrelAxis;
            var radiansPerShot = (2 * Math.PI / _numOfBarrels);
            var radiansPerTick = radiansPerShot / _timePerShot;
            if (_shotCounter == 0 && _nextMuzzle == 0) _rotationTime = 0;

            if (_shotCounter++ >= _ticksPerShot - 1) _shotCounter = 0;
            var bps = WeaponType.BarrelsPerShot;
            if (rotateAxis != 0) MovePart(radiansPerTick, -1 * bps, rotateAxis == 1, rotateAxis == 2, rotateAxis == 3);

            if (_shotCounter != 0) return;

            var updatePos = _posChangedTick > _posUpdatedTick;
            var muzzlesFired = 0;
            for (int j = 0; j < _numOfBarrels; j++)
            {
                var muzzle = Muzzles[j];
                if (updatePos)
                {
                    var dummy = Dummies[j];
                    var newInfo = dummy.Info;
                    muzzle.Direction = newInfo.Direction;
                    muzzle.Position = newInfo.Position;
                    muzzle.LastPosUpdate = tick;
                }

                if (j == _nextMuzzle && muzzlesFired < bps)
                {
                    muzzle.LastFireTick = Session.Instance.Tick;
                    if (_nextMuzzle + 1 != _numOfBarrels) _nextMuzzle++;
                    else _nextMuzzle = 0;
                    muzzlesFired++;
                    var color = Color.Red;
                    if (j % 2 == 0) color = Color.Blue;
                    DsDebugDraw.DrawLine(muzzle.Position, muzzle.Position + (muzzle.Direction * 1000), color, 0.02f);
                }

                if (muzzlesFired >= bps && !updatePos) break;
            }
            if (tick - _posChangedTick > 10) _posUpdatedTick = tick;
        }

        public void MovePart(double radians, int time, bool xAxis, bool yAxis, bool zAxis)
        {
            MatrixD rotationMatrix;
            if (xAxis) rotationMatrix = MatrixD.CreateRotationX(radians * _rotationTime);
            else if (yAxis) rotationMatrix = MatrixD.CreateRotationY(radians * _rotationTime);
            else if (zAxis) rotationMatrix = MatrixD.CreateRotationZ(radians * _rotationTime);
            else return;

            _rotationTime += time;
            rotationMatrix.Translation = _localTranslation;
            EntityPart.PositionComp.LocalMatrix = rotationMatrix;
        }

        internal void SelectTarget()
        {
            if (Target == null) Logic.Turret.ResetTargetingToDefault();

            _targetTick = 0;

            Target = GetTarget();

            if (Target != null)
            {
                _firstRun = false;
                var grid = Target as MyCubeGrid;
                if (grid == null)
                {
                    //Log.Line($"found entityL {Target.DebugName}");
                    Logic.Turret.TrackTarget(Target);
                }
                else
                {
                    var bCount = Logic.TargetBlocks.Count;
                    var found = false;
                    while (!found)
                    {
                        var next = Rnd.Next(0, bCount);
                        if (!Logic.TargetBlocks[next].MarkedForClose)
                        {
                            Target = Logic.TargetBlocks[next];
                            Logic.Turret.TrackTarget(Target);
                            //Log.Line($"found block - Block:{Logic.TargetBlocks[next].DebugName} - Target:{Target.DebugName} - random:{next} - bCount:{bCount}");
                            found = true;
                        }
                    }
                }
            }
        }

        internal void Rotate()
        {
            var myCube = Logic.MyCube;
            var myMatrix = myCube.PositionComp.WorldMatrix;
            var targetPos = Target.PositionComp.WorldAABB.Center;
            var myPivotPos = myCube.PositionComp.WorldAABB.Center;

            myPivotPos -= Vector3D.Normalize(myMatrix.Down - myMatrix.Up) * _upPivotOffsetLen;

            GetTurretAngles(ref targetPos, ref myPivotPos, Logic.Turret, _step, out _azimuth, out _elevation, out _desiredAzimuth, out _desiredElevation);
            var azDiff = 100 * (_desiredAzimuth - _azimuth) / _azimuth;
            var elDiff = 100 * (_desiredElevation - _elevation) / _elevation;

            _azOk = azDiff > -101 && azDiff < -99 || azDiff > -1 && azDiff < 1;
            _elOk = elDiff > -101 && elDiff < -99 || elDiff > -1 && elDiff < 1;
            Logic.Turret.Azimuth = (float)_azimuth;
            Logic.Turret.Elevation = (float)_elevation;
        }

        internal void GetTurretAngles(ref Vector3D targetPositionWorld, ref Vector3D turretPivotPointWorld, IMyLargeTurretBase turret, double maxAngularStep, out double azimuth, out double elevation, out double desiredAzimuth, out double desiredElevation)
        {

            // Get current turret facing
            Vector3D currentVector;
            Vector3D.CreateFromAzimuthAndElevation(turret.Azimuth, turret.Elevation, out currentVector);
            currentVector = Vector3D.Rotate(currentVector, turret.WorldMatrix);

            var up = turret.WorldMatrix.Up;
            var left = Vector3D.Cross(up, currentVector);
            if (!Vector3D.IsUnit(ref left) && !Vector3D.IsZero(left))
                left.Normalize();
            var forward = Vector3D.Cross(left, up);

            var matrix = new MatrixD()
            {
                Forward = forward,
                Left = left,
                Up = up,
            };

            // Get desired angles
            var targetDirection = targetPositionWorld - turretPivotPointWorld;
            GetRotationAngles(ref targetDirection, ref matrix, out desiredAzimuth, out desiredElevation);

            // Get control angles
            azimuth = turret.Azimuth + MathHelper.Clamp(desiredAzimuth, -maxAngularStep, maxAngularStep);
            elevation = turret.Elevation + MathHelper.Clamp(desiredElevation - turret.Elevation, -maxAngularStep, maxAngularStep);
        }

        /*
        /// Whip's Get Rotation Angles Method v14 - 9/25/18 ///
        Dependencies: AngleBetween
        */

        void GetRotationAngles(ref Vector3D targetVector, ref MatrixD worldMatrix, out double yaw, out double pitch)
        {
            var localTargetVector = Vector3D.Rotate(targetVector, MatrixD.Transpose(worldMatrix));
            var flattenedTargetVector = new Vector3D(localTargetVector.X, 0, localTargetVector.Z);
            yaw = AngleBetween(Vector3D.Forward, flattenedTargetVector) * -Math.Sign(localTargetVector.X); //right is negative

            if (Vector3D.IsZero(flattenedTargetVector)) //check for straight up case
                pitch = MathHelper.PiOver2 * Math.Sign(localTargetVector.Y);
            else
                pitch = AngleBetween(localTargetVector, flattenedTargetVector) * Math.Sign(localTargetVector.Y); //up is positive
        }

        /// <summary>
        /// Computes angle between 2 vectors
        /// </summary>
        public static double AngleBetween(Vector3D a, Vector3D b) //returns radians
        {
            if (Vector3D.IsZero(a) || Vector3D.IsZero(b))
                return 0;
            else
                return Math.Acos(MathHelper.Clamp(a.Dot(b) / Math.Sqrt(a.LengthSquared() * b.LengthSquared()), -1, 1));
        }

        internal MyEntity GetTarget()
        {
            foreach (var ent in Logic.Targeting.TargetRoots)
            {
                if (Target == ent || Target?.Parent == ent) continue;

                var entInfo = MyDetectedEntityInfoHelper.Create(ent, Logic.Turret.OwnerId);
                if (entInfo.IsEmpty() || (entInfo.Relationship == MyRelationsBetweenPlayerAndBlock.Owner)) continue;
                if (entInfo.Type == MyDetectedEntityType.SmallGrid || entInfo.Type == MyDetectedEntityType.LargeGrid)
                {
                    if (!GetTargetBlocks(ent)) return null;
                    return ent;
                }
                return ent;
            }

            return Target;
        }

        private bool GetTargetBlocks(MyEntity targetGrid)
        {
            Logic.TargetBlocks.Clear();
            IEnumerable<KeyValuePair<MyCubeGrid, List<MyEntity>>> allTargets = Logic.Targeting.TargetBlocks;
            var g = 0;
            var f = 0;
            foreach (var targets in allTargets)
            {
                var rootGrid = targets.Key;
                if (rootGrid != targetGrid) continue;
                if (rootGrid.MarkedForClose) return false;

                if (g++ > 0) break;
                foreach (var b in targets.Value)
                {
                    if (b == null) continue;
                    if (f++ > 9) return true;
                    Logic.TargetBlocks.Add(b);
                }
            }

            return f > 0;
        }

        /// <summary>
        /// Returns if the normalized dot product between two vectors is greater than the tolerance.
        /// This is helpful for determining if two vectors are "more parallel" than the tolerance.
        /// </summary>
        /// <param name="a">First vector</param>
        /// <param name="b">Second vector</param>
        /// <param name="tolerance">Cosine of maximum angle</param>
        /// <returns></returns>
        public static bool IsDotProductWithinTolerance(ref Vector3D a, ref Vector3D b, double tolerance)
        {
            double dot = Vector3D.Dot(a, b);
            double num = a.LengthSquared() * b.LengthSquared() * tolerance * Math.Sign(tolerance);
            return Math.Sign(dot) * dot > num;
        }

        bool sameSign(float num1, double num2)
        {
            if (num1 > 0 && num2 < 0)
                return false;
            if (num1 < 0 && num2 > 0)
                return false;
            return true;
        }
    }

    public class Muzzle
    {
        public Vector3D Position;
        public Vector3D Direction;
        public uint LastFireTick;
        public uint LastPosUpdate;
    }

    public class MyWeaponPlatform
    {
        public readonly Weapon[] Weapons;
        public readonly RecursiveSubparts SubParts = new RecursiveSubparts();
        public readonly WeaponStructure Structure;
        public uint[][] BeamSlot { get; set; }

        public MyWeaponPlatform(MyStringHash subTypeIdHash, IMyEntity entity, Logic logic)
        {
            Structure = Session.Instance.WeaponStructure[subTypeIdHash];
            //PartNames = Structure.PartNames;
            var subPartCount = Structure.PartNames.Length;

            Weapons = new Weapon[subPartCount];
            BeamSlot = new uint[subPartCount][];

            SubParts.Entity = entity;
            SubParts.CheckSubparts();
            for (int i = 0; i < subPartCount; i++)
            {
                var barrelCount = Structure.WeaponSystems[Structure.PartNames[i]].Barrels.Length;
                IMyEntity subPartEntity;
                SubParts.NameToEntity.TryGetValue(Structure.PartNames[i].String, out subPartEntity);
                BeamSlot[i] = new uint[barrelCount];
                Weapons[i] = new Weapon(subPartEntity, Structure.WeaponSystems[Structure.PartNames[i]])
                {
                    Muzzles = new Muzzle[barrelCount],
                    Dummies = new Dummy[barrelCount],
                    Logic = logic,
                };
            }

            CompileTurret();
        }

        private void CompileTurret()
        {
            var c = 0;
            foreach (var m in Structure.WeaponSystems)
            {
                var subPart = SubParts.NameToEntity[m.Key.String];
                var barrelCount = m.Value.Barrels.Length;
                Weapons[c].EntityPart.PositionComp.OnPositionChanged += Weapons[c].PositionChanged;
                for (int i = 0; i < barrelCount; i++)
                {
                    var barrel = m.Value.Barrels[i];
                    Weapons[c].Dummies[i] = new Dummy(subPart, barrel);
                    Weapons[c].Muzzles[i] = new Muzzle();
                }
                c++;
            }
        }
    }

}
