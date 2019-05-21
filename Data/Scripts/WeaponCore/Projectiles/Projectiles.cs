using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;
using WeaponCore.Platform;
using WeaponCore.Support;

namespace WeaponCore.Projectiles
{
    internal partial class Projectiles
    {
        private const float StepConst = MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS;
        internal readonly ObjectsPool<Projectile>[] ProjectilePool = new ObjectsPool<Projectile>[20];
        internal readonly MyConcurrentPool<List<MyLineSegmentOverlapResult<MyEntity>>>[] SegmentPool = new MyConcurrentPool<List<MyLineSegmentOverlapResult<MyEntity>>>[20];
        internal readonly MyConcurrentPool<List<MyEntity>>[] CheckPool = new MyConcurrentPool<List<MyEntity>>[20];
        internal readonly MyConcurrentPool<List<LineD>>[] LinePool = new MyConcurrentPool<List<LineD>>[20];
        internal readonly List<DrawProjectile>[] DrawProjectiles = new List<DrawProjectile>[20];
        internal readonly object[] Wait = new object[20];

        internal Projectiles()
        {
            for (int i = 0; i < Wait.Length; i++)
            {
                Wait[i] = new object();
                ProjectilePool[i] = new ObjectsPool<Projectile>(1250);
                SegmentPool[i] = new MyConcurrentPool<List<MyLineSegmentOverlapResult<MyEntity>>>(1250);
                CheckPool[i] = new MyConcurrentPool<List<MyEntity>>(1250);
                LinePool[i] = new MyConcurrentPool<List<LineD>>(1250);
                DrawProjectiles[i] = new List<DrawProjectile>(500);
            }
        }

        internal void Update()
        {
            MyAPIGateway.Parallel.For(0, Wait.Length, Process, 1);
        }

        private void Process(int i)
        {
            var noDraw = Session.Instance.DedicatedServer;
            lock (Wait[i])
            {
                var pool = ProjectilePool[i];
                var drawList = DrawProjectiles[i];
                var segmentPool = SegmentPool[i];
                var checkPool = CheckPool[i];
                var linePool = LinePool[i];
                foreach (var p in pool.Active)
                {
                    if (p.State != Projectile.ProjectileState.Alive) continue;
                    p.CurrentMagnitude = p.CurrentSpeed * StepConst;
                    p.LastPosition = p.Position;
                    p.Position += p.CurrentMagnitude;

                    Vector3D? intersect = null;
                    var segmentList = segmentPool.Get();
                    var beam = new LineD(p.LastPosition, p.Position + p.CurrentMagnitude);
                    MyGamePruningStructure.GetTopmostEntitiesOverlappingRay(ref beam, segmentList);
                    var segCount = segmentList.Count;
                    if (segCount > 1 || segCount == 1 && segmentList[0].Element != p.MyGrid)
                    {
                        var fired = new Fired(p.Weapon, linePool.Get());
                        GetAllEntitiesInLine2(p.CheckList, fired, beam, segmentList);
                        var hitInfo = GetHitEntities(p.CheckList, fired, beam);
                        if (GetDamageInfo(fired, beam, hitInfo, 0, false))
                        {
                            intersect = hitInfo.HitPos;
                            DamageEntities(fired);
                        }
                        linePool.Return(fired.Shots);
                        p.CheckList.Clear();
                        segmentList.Clear();

                        if (intersect != null)
                        {
                            var entity = hitInfo.Slim == null ? hitInfo.Entity : hitInfo.Slim.CubeGrid;
                            drawList.Add(new DrawProjectile(p.Weapon, 0, new LineD(p.Position + -(p.Direction * p.ShotLength), p.Position), p.CurrentSpeed, hitInfo.HitPos, entity, true, p.LineReSizeLen, p.ReSizeSteps, p.Shrink));
                            ProjectileClose(p, pool, checkPool);
                        }
                    }
                    segmentPool.Return(segmentList);
                    if (intersect != null) continue;

                    var distTraveled = (p.Origin - p.Position);
                    if (Vector3D.Dot(distTraveled, distTraveled) >= p.MaxTrajectory * p.MaxTrajectory)
                    {
                        ProjectileClose(p, pool, checkPool);
                        continue;
                    }

                    if (noDraw) continue;

                    if (p.Grow)
                    {
                        p.CurrentLine = new LineD(p.Position, p.Position + -(p.Direction * (p.GrowStep * p.LineReSizeLen)));
                        if (p.GrowStep++ >= p.ReSizeSteps) p.Grow = false;
                    }
                    else p.CurrentLine = new LineD(p.Position + -(p.Direction * p.ShotLength), p.Position);

                    var from = p.CurrentLine.From;
                    var to = p.CurrentLine.To;
                    var bb = new BoundingBoxD(Vector3D.Min(from, to), Vector3D.Max(from, to));

                    if (Session.Instance.Session.Camera.IsInFrustum(ref bb))
                        drawList.Add(new DrawProjectile(p.Weapon, 0, p.CurrentLine, p.CurrentSpeed, Vector3D.Zero, null, true, 0, 0 , false));
                }
                pool.DeallocateAllMarked();
            }
        }

        internal void Start(Projectile p, Shot fired, Weapon weapon, List<MyEntity> checkList)
        {
            p.Weapon = weapon;
            var wDef = weapon.WeaponType;
            p.MaxTrajectory = wDef.MaxTrajectory;
            p.ShotLength = wDef.ShotLength;
            p.SpeedLength = wDef.DesiredSpeed;// * MyUtils.GetRandomFloat(1f, 1.5f);
            p.LineReSizeLen = p.SpeedLength / 60;
            p.GrowStep = 1;
            var reSizeSteps = (int)(p.ShotLength / p.LineReSizeLen);
            p.ReSizeSteps = reSizeSteps > 0 ? reSizeSteps : 1;
            p.Grow = p.ReSizeSteps > 1;
            p.Shrink = p.Grow;
            p.Origin = fired.Position;
            p.Direction = fired.Direction;
            p.Position = p.Origin;
            p.MyGrid = weapon.Logic.MyGrid;
            p.CheckList = checkList;
            p.StartSpeed = p.Weapon.Logic.Turret.CubeGrid.Physics.LinearVelocity;
            p.AddSpeed = p.Direction * p.SpeedLength;
            p.FinalSpeed = p.StartSpeed + p.AddSpeed;
            p.CurrentSpeed = p.FinalSpeed;
            p.State = Projectile.ProjectileState.Alive;
            p.PositionChecked = false;
            //_desiredSpeed = wDef.DesiredSpeed * ((double)ammoDefinition.SpeedVar > 0.0 ? MyUtils.GetRandomFloat(1f - ammoDefinition.SpeedVar, 1f + ammoDefinition.SpeedVar) : 1f);
            //_checkIntersectionIndex = _checkIntersectionCnt % 5;
            //_checkIntersectionCnt += 3;
            //ProjectileParticleStart();
        }

        private void ProjectileClose(Projectile p, ObjectsPool<Projectile> pool, MyConcurrentPool<List<MyEntity>> checkPool)
        {
            p.State = Projectile.ProjectileState.Dead;
            p.Effect1.Close(true, false);
            checkPool.Return(p.CheckList);
            pool.MarkForDeallocate(p);
        }

        private void PrefetchVoxelPhysicsIfNeeded(Projectile p)
        {
            var ray = new LineD(p.Origin, p.Origin + p.Direction * p.MaxTrajectory, p.MaxTrajectory);
            var lineD = new LineD(new Vector3D(Math.Floor(ray.From.X) * 0.5, Math.Floor(ray.From.Y) * 0.5, Math.Floor(ray.From.Z) * 0.5), new Vector3D(Math.Floor(p.Direction.X * 50.0), Math.Floor(p.Direction.Y * 50.0), Math.Floor(p.Direction.Z * 50.0)));
            if (p.VoxelRayCache.IsItemPresent(lineD.GetHash(), (int)MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds, true))
                return;
            using (MyUtils.ReuseCollection(ref p.EntityRaycastResult))
            {
                MyGamePruningStructure.GetAllEntitiesInRay(ref ray, p.EntityRaycastResult, MyEntityQueryType.Static);
                foreach (var segmentOverlapResult in p.EntityRaycastResult)
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

        private void ProjectileParticleStart(Projectile p)
        {
            var color = new Vector4(255, 255, 255, 128f); // comment out to use beam color
            var mainParticle = 32;
            var to = p.Position;
            var matrix = MatrixD.CreateTranslation(to);
            MyParticlesManager.TryCreateParticleEffect(mainParticle, out p.Effect1, ref matrix, ref to, uint.MaxValue, true); // 15, 16, 24, 25, 28, (31, 32) 211 215 53
            if (p.Effect1 == null) return;
            p.Effect1.DistanceMax = p.MaxTrajectory;
            p.Effect1.UserColorMultiplier = color;
            p.Effect1.UserRadiusMultiplier = 1f;
            p.Effect1.UserEmitterScale = 1f;
            p.Effect1.Velocity = p.CurrentSpeed;
        }
    }
}
