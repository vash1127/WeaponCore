using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;
using WeaponCore.Platform;

namespace WeaponCore.Projectiles
{
    internal class Projectile
    {
        //private static int _checkIntersectionCnt = 0;
        internal ProjectileState State;
        internal Vector3D Direction;
        internal Vector3D Position;
        internal Vector3D LastPosition;
        internal Weapon Weapon;
        internal List<MyEntity> CheckList;
        internal MyCubeGrid MyGrid;
        internal Vector3D Origin;
        internal Vector3D StartSpeed;
        internal Vector3D AddSpeed;
        internal Vector3D CurrentSpeed;
        internal Vector3D FinalSpeed;
        internal Vector3D CurrentMagnitude;
        internal LineD CurrentLine;
        internal double ShotLength;
        internal float SpeedLength;
        internal float MaxTrajectory;
        internal bool PositionChecked;
        internal double LineReSizeLen;
        internal int ReSizeSteps;
        internal int GrowStep = 1;
        internal bool Grow;
        internal bool Shrink;
        internal MyParticleEffect Effect1 = new MyParticleEffect();
        private static readonly MyTimedItemCache _voxelRayCache = new MyTimedItemCache(4000);
        private static List<MyLineSegmentOverlapResult<MyEntity>> _entityRaycastResult = null;

        internal void Start(Projectiles.Shot fired, Weapon weapon, List<MyEntity> checkList)
        {
            Weapon = weapon;
            var wDef = weapon.WeaponType;
            MaxTrajectory = wDef.MaxTrajectory;
            ShotLength = wDef.ShotLength;
            SpeedLength = wDef.DesiredSpeed;// * MyUtils.GetRandomFloat(1f, 1.5f);
            LineReSizeLen = SpeedLength / 60;
            var reSizeSteps = (int)(ShotLength / LineReSizeLen);
            ReSizeSteps = reSizeSteps > 0 ? reSizeSteps : 1;
            Grow = ReSizeSteps > 1;
            Shrink = Grow;
            Origin = fired.Position;
            Direction = fired.Direction;
            Position = Origin;
            MyGrid = weapon.Logic.MyGrid;
            CheckList = checkList;
            StartSpeed = Weapon.Logic.Turret.CubeGrid.Physics.LinearVelocity;
            AddSpeed = Direction * SpeedLength;
            FinalSpeed = StartSpeed + AddSpeed;
            CurrentSpeed = FinalSpeed;
            State = ProjectileState.Alive;
            PositionChecked = false;

            //_desiredSpeed = wDef.DesiredSpeed * ((double)ammoDefinition.SpeedVar > 0.0 ? MyUtils.GetRandomFloat(1f - ammoDefinition.SpeedVar, 1f + ammoDefinition.SpeedVar) : 1f);
            //_checkIntersectionIndex = _checkIntersectionCnt % 5;
            //_checkIntersectionCnt += 3;
            //ProjectileParticleStart();
        }

        private void PrefetchVoxelPhysicsIfNeeded()
        {
            var ray = new LineD(Origin, Origin + Direction * MaxTrajectory, MaxTrajectory);
            var lineD = new LineD(new Vector3D(Math.Floor(ray.From.X) * 0.5, Math.Floor(ray.From.Y) * 0.5, Math.Floor(ray.From.Z) * 0.5), new Vector3D(Math.Floor(Direction.X * 50.0), Math.Floor(Direction.Y * 50.0), Math.Floor(Direction.Z * 50.0)));
            if (_voxelRayCache.IsItemPresent(lineD.GetHash(), (int)MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds, true))
                return;
            using (MyUtils.ReuseCollection(ref _entityRaycastResult))
            {
                MyGamePruningStructure.GetAllEntitiesInRay(ref ray, _entityRaycastResult, MyEntityQueryType.Static);
                foreach (var segmentOverlapResult in _entityRaycastResult)
                    (segmentOverlapResult.Element as MyPlanet)?.PrefetchShapeOnRay(ref ray);
            }
        }

        private MyEntity GetSubpartOwner(MyEntity entity)
        {
            if (entity == null)
                return null;
            if (!(entity is MyEntitySubpart))
                return entity;
            var myEntity = entity;
            while (myEntity is MyEntitySubpart)
                myEntity = myEntity.Parent;
            return myEntity ?? entity;
        }

        public static void ApplyProjectileForce(
          MyEntity entity,
          Vector3D intersectionPosition,
          Vector3 normalizedDirection,
          bool isPlayerShip,
          float impulse)
        {
            if (entity.Physics == null || !entity.Physics.Enabled || entity.Physics.IsStatic)
                return;
            if (entity is IMyCharacter)
                impulse *= 100f;
            entity.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_IMPULSE_AND_WORLD_ANGULAR_IMPULSE, normalizedDirection * impulse, intersectionPosition, Vector3.Zero, new float?(), true, false);
        }

        //Relative velocity proportional navigation
        //aka: Whip-Nav
        Vector3D CalculateMissileIntercept(Vector3D targetPosition, Vector3D targetVelocity, Vector3D missilePos, Vector3D missileVelocity, double missileAcceleration, double compensationFactor = 1)
        {
            var missileToTarget = Vector3D.Normalize(targetPosition - missilePos);
            var relativeVelocity = targetVelocity - missileVelocity;
            var parallelVelocity = relativeVelocity.Dot(missileToTarget) * missileToTarget;
            var normalVelocity = (relativeVelocity - parallelVelocity);

            var normalMissileAcceleration = normalVelocity * compensationFactor;

            if (Vector3D.IsZero(normalMissileAcceleration))
                return missileToTarget;

            double diff = missileAcceleration * missileAcceleration - normalMissileAcceleration.LengthSquared();
            if (diff < 0)
            {
                return normalMissileAcceleration; //fly parallel to the target
            }

            return Math.Sqrt(diff) * missileToTarget + normalMissileAcceleration;
        }

        private void ProjectileParticleStart()
        {
            var color = new Vector4(255, 255, 255, 128f); // comment out to use beam color
            var mainParticle = 32;
            var to = Position;
            var matrix = MatrixD.CreateTranslation(to);
            MyParticlesManager.TryCreateParticleEffect(mainParticle, out Effect1, ref matrix, ref to, uint.MaxValue, true); // 15, 16, 24, 25, 28, (31, 32) 211 215 53
            if (Effect1 == null) return;
            Effect1.DistanceMax = MaxTrajectory;
            Effect1.UserColorMultiplier = color;
            Effect1.UserRadiusMultiplier = 1f;
            Effect1.UserEmitterScale = 1f;
            Effect1.Velocity = CurrentSpeed;
        }

        public enum ProjectileState
        {
            Alive,
            Dead,
        }
    }
}