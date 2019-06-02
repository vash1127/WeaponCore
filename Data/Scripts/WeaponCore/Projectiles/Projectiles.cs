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
            var noAv = Session.Instance.DedicatedServer;
            var camera = Session.Instance.Session.Camera;
            var cameraPos = camera.Position;
            lock (Wait[i])
            {
                var pool = ProjectilePool[i];
                var drawList = DrawProjectiles[i];
                var segmentPool = SegmentPool[i];
                var checkPool = CheckPool[i];
                var linePool = LinePool[i];
                foreach (var p in pool.Active)
                {
                    switch (p.State)
                    {
                        case Projectile.ProjectileState.Dead:
                            continue;
                        case Projectile.ProjectileState.Start:
                            p.Start(checkPool.Get(), noAv);
                            break;
                        case Projectile.ProjectileState.Ending:
                            p.Stop(pool, checkPool);
                            continue;
                    }

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
                            if (!noAv && p.DrawLine && p.Draw)
                            {
                                var entity = hitInfo.Slim == null ? hitInfo.Entity : hitInfo.Slim.CubeGrid;
                                drawList.Add(new DrawProjectile(p.Weapon, 0, new LineD(p.Position + -(p.Direction * p.ShotLength), hitInfo.HitPos), p.CurrentSpeed, hitInfo.HitPos, entity, true, p.LineReSizeLen, p.ReSizeSteps, p.Shrink));
                            }
                            p.ProjectileClose(pool, checkPool, noAv);
                        }
                    }
                    segmentPool.Return(segmentList);
                    if (intersect != null) continue;

                    var distTraveled = (p.Origin - p.Position);
                    if (Vector3D.Dot(distTraveled, distTraveled) >= p.MaxTrajectory * p.MaxTrajectory)
                    {
                        p.ProjectileClose(pool, checkPool, noAv);
                        continue;
                    }

                    if (noAv || !p.Draw) continue;

                    if (p.WepDef.AudioDef.AmmoTravelSound != null)
                    {
                        if (!p.AmmoSound)
                        {
                            double dist;
                            Vector3D.DistanceSquared(ref p.Position, ref cameraPos, out dist);
                            if (dist <= p.AmmoTravelSoundRangeSqr) p.AmmoSoundStart();
                        }
                        else p.Sound1.SetPosition(p.Position);
                    }

                    if (!p.DrawLine) continue;

                    if (p.Grow)
                    {
                        p.CurrentLine = new LineD(p.Position, p.Position + -(p.Direction * (p.GrowStep * p.LineReSizeLen)));
                        if (p.GrowStep++ >= p.ReSizeSteps) p.Grow = false;
                    }
                    else p.CurrentLine = new LineD(p.Position + -(p.Direction * p.ShotLength), p.Position);

                    var from = p.CurrentLine.From;
                    var to = p.CurrentLine.To;
                    var bb = new BoundingBoxD(Vector3D.Min(from, to), Vector3D.Max(from, to));

                    if (camera.IsInFrustum(ref bb))
                        drawList.Add(new DrawProjectile(p.Weapon, 0, p.CurrentLine, p.CurrentSpeed, Vector3D.Zero, null, true, 0, 0 , false));
                }
                pool.DeallocateAllMarked();
            }
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
    }
}
