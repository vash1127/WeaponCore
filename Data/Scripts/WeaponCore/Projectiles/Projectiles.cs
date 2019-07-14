using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Entity;
using VRage.ModAPI;
using VRageMath;
using WeaponCore.Support;

namespace WeaponCore.Projectiles
{
    internal partial class Projectiles
    {
        private const float StepConst = MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS;
        internal const int PoolCount = 16;
        internal readonly ConcurrentQueue<Projectile> Hits = new ConcurrentQueue<Projectile>();

        internal readonly ObjectsPool<Projectile>[] ProjectilePool = new ObjectsPool<Projectile>[PoolCount];
        internal readonly EntityPool<MyEntity>[][] EntityPool = new EntityPool<MyEntity>[PoolCount][];
        internal readonly MyConcurrentPool<List<MyLineSegmentOverlapResult<MyEntity>>>[] SegmentPool = new MyConcurrentPool<List<MyLineSegmentOverlapResult<MyEntity>>>[PoolCount];
        internal readonly MyConcurrentPool<List<HitEntity>>[] HitsPool = new MyConcurrentPool<List<HitEntity>>[PoolCount];
        internal readonly MyConcurrentPool<HitEntity>[] HitEntityPool = new MyConcurrentPool<HitEntity>[PoolCount];
        internal readonly MyConcurrentPool<List<MyEntity>>[] MyEntityPool = new MyConcurrentPool<List<MyEntity>>[PoolCount];
        internal readonly List<DrawProjectile>[] DrawProjectiles = new List<DrawProjectile>[PoolCount];
        internal readonly object[] Wait = new object[PoolCount];

        internal Projectiles()
        {
            for (int i = 0; i < Wait.Length; i++)
            {
                Wait[i] = new object();
                ProjectilePool[i] = new ObjectsPool<Projectile>(1250);
                SegmentPool[i] = new MyConcurrentPool<List<MyLineSegmentOverlapResult<MyEntity>>>(1250);
                HitsPool[i] = new MyConcurrentPool<List<HitEntity>>(1250);
                HitEntityPool[i] = new MyConcurrentPool<HitEntity>(250);
                MyEntityPool[i] = new MyConcurrentPool<List<MyEntity>>(500);
                DrawProjectiles[i] = new List<DrawProjectile>(500);
            }
        }

        internal static MyEntity EntityActivator(string model)
        {
            var ent = new MyEntity();
            ent.Init(null, model, null, null, null);
            ent.Render.CastShadows = false;
            ent.IsPreview = true;
            ent.Save = false;
            ent.SyncFlag = false;
            ent.NeedsWorldMatrix = false;
            ent.Flags |= EntityFlags.IsNotGamePrunningStructureObject;
            MyEntities.Add(ent);
            return ent;
        }

        internal void Update()
        {
            MyAPIGateway.Parallel.For(0, Wait.Length, Process, 1);
            //for (int i = 0; i < Wait.Length; i++) Process(i);
        }

        private void Process(int i)
        {
            var noAv = Session.Instance.DedicatedServer;
            var camera = Session.Instance.Session.Camera;
            var cameraPos = camera.Position;
            lock (Wait[i])
            {
                var pool = ProjectilePool[i];
                var entPool = EntityPool[i];
                var drawList = DrawProjectiles[i];
                var segmentPool = SegmentPool[i];
                var hitsPool = HitsPool[i];
                var modelClose = false;
                foreach (var p in pool.Active)
                {
                    p.Age++;
                    switch (p.State)
                    {
                        case Projectile.ProjectileState.Dead:
                            continue;
                        case Projectile.ProjectileState.Start:
                            p.Start(hitsPool.Get(), noAv, i);
                            if (p.ModelState == Projectile.EntityState.NoDraw)
                                modelClose = p.CloseModel(entPool[p.ModelId], drawList);
                            break;
                        case Projectile.ProjectileState.Ending:
                        case Projectile.ProjectileState.OneAndDone:
                        case Projectile.ProjectileState.Depleted:
                            if (p.State == Projectile.ProjectileState.Depleted)
                                p.ProjectileClose(pool, hitsPool, noAv);
                            if (p.ModelState != Projectile.EntityState.Exists) p.Stop(pool, hitsPool);
                            else
                            {
                                modelClose = p.CloseModel(entPool[p.ModelId], drawList);
                                p.Stop(pool, hitsPool);
                            }
                            continue;
                    }
                    p.LastPosition = p.Position;

                    if (p.Guidance == AmmoTrajectory.GuidanceType.Smart)
                    {
                        try
                        {
                            Vector3D newVel;
                            if ((p.AccelLength <= 0 || Vector3D.DistanceSquared(p.Origin, p.Position) > p.SmartsDelayDistSqr))
                            {
                                if (p.Target != null && !p.Target.MarkedForClose)
                                {
                                    var physics = p.Target?.Physics ?? p.Target?.Parent?.Physics;
                                    var tVel = physics?.LinearVelocity ?? Vector3.Zero;
                                    var targetPos = p.Target.PositionComp.WorldAABB.Center;
                                    if (physics == null || targetPos == Vector3D.Zero) p.PrevTargetPos = p.PredictedTargetPos;
                                    else p.PrevTargetPos = targetPos;
                                    p.PrevTargetVel = tVel;
                                }
                                else if (p.State != Projectile.ProjectileState.Zombie)
                                {
                                    p.PrevTargetVel = Vector3.Zero;
                                    p.PrevTargetPos = p.PredictedTargetPos;
                                    p.DistanceTraveled = 0;
                                    p.DistanceToTravelSqr = (Vector3D.DistanceSquared(p.Position, p.PrevTargetPos) + 100);
                                    p.State = Projectile.ProjectileState.Zombie;
                                }
                                var commandedAccel = CalculateMissileIntercept(p.PrevTargetPos, p.PrevTargetVel, p.Position, p.Velocity, p.AccelPerSec, p.System.Values.Ammo.Trajectory.SmartsFactor, p.System.Values.Ammo.Trajectory.SmartsMaxLateralThrust);
                                newVel = p.Velocity + (commandedAccel * StepConst);
                                p.AccelDir = commandedAccel / p.AccelPerSec;
                            }
                            else newVel = p.Velocity += (p.Direction * p.AccelLength);

                            if (newVel.LengthSquared() > p.DesiredSpeedSqr)
                            {
                                newVel.Normalize();
                                newVel *= p.DesiredSpeed;
                            }
                            p.Velocity = newVel;
                            p.Direction = Vector3D.Normalize(p.Velocity);
                        }
                        catch (Exception ex) { Log.Line($"Exception in GuidanceType.Smart: {ex}"); }
                    }
                    else if (p.AccelLength > 0)
                    {
                        var newVel = p.Velocity + p.AccelVelocity;
                        if (newVel.LengthSquared() > p.DesiredSpeedSqr)
                        {
                            newVel.Normalize();
                            newVel *= p.DesiredSpeed;
                        }
                        p.Velocity = newVel;
                    }
                    if (p.State == Projectile.ProjectileState.OneAndDone)
                    {
                        var beamEnd = p.Position + (p.Direction * p.MaxTrajectory);
                        p.TravelMagnitude = p.Position - beamEnd;
                        p.Position = beamEnd;
                    }
                    else
                    {
                        p.TravelMagnitude = p.Velocity * StepConst;
                        p.Position += p.TravelMagnitude;
                    }
                    p.DistanceTraveled += Vector3D.Dot(p.Direction, p.Velocity * StepConst);

                    if (p.ModelState == Projectile.EntityState.Exists)
                    {
                        try
                        {
                            p.EntityMatrix = MatrixD.CreateWorld(p.Position, p.AccelDir, p.Entity.PositionComp.WorldMatrix.Up);
                            if (p.EnableAv && p.Effect1 != null && p.System.AmmoParticle)
                            {
                                var offVec = p.Position + Vector3D.Rotate(p.System.Values.Graphics.Particles.AmmoOffset, p.EntityMatrix);
                                p.Effect1.WorldMatrix = p.EntityMatrix;
                                p.Effect1.SetTranslation(offVec);
                            }
                        }
                        catch (Exception ex) { Log.Line($"Exception in EntityMatrix: {ex}"); }
                    }
                    else if (!p.ConstantSpeed && p.EnableAv && p.Effect1 != null && p.System.AmmoParticle)
                        p.Effect1.Velocity = p.Velocity;

                    if (p.State != Projectile.ProjectileState.OneAndDone)
                    {
                        if (p.DistanceTraveled * p.DistanceTraveled >= p.DistanceToTravelSqr)
                        {
                            if (p.MoveToAndActivate || p.System.Values.Ammo.DetonateOnEnd)
                            {
                                GetEntitiesInBlastRadius(p, i);
                                var hitEntity = p.HitList[0];
                                if (hitEntity != null)
                                {
                                    p.LastHitPos = hitEntity.HitPos;
                                    p.LastHitEntVel = hitEntity.Entity?.Physics?.LinearVelocity;
                                }
                            }
                            else p.ProjectileClose(pool, hitsPool, noAv);
                            continue;
                        }
                    }

                    var segmentList = segmentPool.Get();
                    var beam = new LineD(p.LastPosition, p.Position);
                    MyGamePruningStructure.GetTopmostEntitiesOverlappingRay(ref beam, segmentList);
                    var segCount = segmentList.Count;
                    if (segCount > 1 || segCount == 1 && segmentList[0].Element != p.FiringGrid)
                    {
                        try
                        {
                            HitEntity hitEntity = null;
                            if (GetAllEntitiesInLine(p, beam, segmentList, i))
                                hitEntity = GenerateHitInfo(p.HitList, i);

                            segmentList.Clear();

                            if (hitEntity != null)
                            {
                                if (!noAv && p.EnableAv && (p.DrawLine || p.ModelId != -1))
                                {
                                    var length = Vector3D.Distance(p.LastPosition, hitEntity.HitPos.Value);
                                    p.Trajectile = new Trajectile(p.LastPosition, hitEntity.HitPos.Value, p.Direction, length);
                                    p.TestSphere.Center = hitEntity.HitPos.Value;
                                    var hitOnScreen = camera.IsInFrustum(ref p.TestSphere);
                                    drawList.Add(new DrawProjectile(p.System, p.FiringCube, p.WeaponId, p.MuzzleId, p.Entity, p.EntityMatrix, hitEntity, p.Trajectile, p.MaxSpeedLength, p.ReSizeSteps, p.Shrink, true, hitOnScreen));
                                }
                                Hits.Enqueue(p);
                                p.LastHitPos = hitEntity.HitPos;
                                p.LastHitEntVel = hitEntity.Entity?.Physics?.LinearVelocity;
                                if (p.EnableAv) p.HitEffects();
                                continue;
                            }
                            p.HitList.Clear();
                        }
                        catch (Exception ex) { Log.Line($"Exception in Intersect check: {ex}"); }
                    }
                    segmentPool.Return(segmentList);

                    if (noAv || !p.EnableAv) continue;

                    if (p.System.AmmoParticle)
                    {
                        p.TestSphere.Center = p.Position;
                        if (camera.IsInFrustum(ref p.TestSphere))
                        {
                            if (p.ParticleStopped || p.ParticleLateStart)
                                p.ProjectileParticleStart();
                        }
                        else if (!p.ParticleStopped && p.Effect1 != null)
                        {
                            p.Effect1.Stop(false);
                            p.Effect1 = null;
                            p.ParticleStopped = true;
                        }
                    }

                    if (p.HasTravelSound)
                    {
                        if (!p.AmmoSound)
                        {
                            double dist;
                            Vector3D.DistanceSquared(ref p.Position, ref cameraPos, out dist);
                            if (dist <= p.AmmoTravelSoundRangeSqr) p.AmmoSoundStart();
                        }
                        else p.TravelEmitter.SetPosition(p.Position);
                    }

                    if (p.ModelState == Projectile.EntityState.Exists)
                    {
                        var lastSphere = new BoundingSphereD(p.LastEntityPos, p.ScreenCheckRadius);
                        var currentSphere = new BoundingSphereD(p.Position, p.ScreenCheckRadius);
                        if (camera.IsInFrustum(ref lastSphere) || camera.IsInFrustum(ref currentSphere) || p.FirstOffScreen)
                        {
                            p.FirstOffScreen = false;
                            p.OnScreen = true;
                            p.LastEntityPos = p.Position;
                            drawList.Add(new DrawProjectile(p.System, p.FiringCube, p.WeaponId, p.MuzzleId, p.Entity, p.EntityMatrix, null, p.Trajectile, p.MaxSpeedLength, p.ReSizeSteps, p.Shrink, false, p.OnScreen));
                        }
                        else p.OnScreen = false;
                        continue;
                    }

                    if (!p.DrawLine) continue;

                    if (p.Grow)
                    {
                        if (p.AccelLength <= 0)
                        {
                            p.Trajectile = new Trajectile(p.Position + -(p.Direction * p.DistanceTraveled), p.Position, p.Direction, p.DistanceTraveled);
                            if (p.GrowStep++ >= p.ReSizeSteps) p.Grow = false;
                        }
                        else
                            p.Trajectile = new Trajectile(p.Position + -(p.Direction * p.DistanceTraveled), p.Position, p.Direction, p.DistanceTraveled);

                        if (Vector3D.DistanceSquared(p.Origin, p.Position) > p.ShotLength * p.ShotLength) p.Grow = false;
                    }
                    else if (p.State == Projectile.ProjectileState.OneAndDone)
                        p.Trajectile = new Trajectile(p.LastPosition, p.Position, p.Direction, p.MaxTrajectory);
                    else
                        p.Trajectile = new Trajectile(p.Position + -(p.Direction * p.ShotLength), p.Position, p.Direction, p.ShotLength);

                    var bb = new BoundingBoxD(Vector3D.Min(p.Trajectile.PrevPosition, p.Trajectile.Position), Vector3D.Max(p.Trajectile.PrevPosition, p.Trajectile.Position));
                    if (camera.IsInFrustum(ref bb))
                    {
                        p.OnScreen = true;
                        drawList.Add(new DrawProjectile(p.System, p.FiringCube, p.WeaponId, p.MuzzleId, p.Entity, p.EntityMatrix, null, p.Trajectile, p.MaxSpeedLength, p.ReSizeSteps, p.Shrink, false, p.OnScreen));
                    }
                    else p.OnScreen = false;
                }

                if (modelClose)
                    foreach (var e in entPool)
                        e.DeallocateAllMarked();

                pool.DeallocateAllMarked();
            }
        }

        //Relative velocity proportional navigation
        //aka: Whip-Nav
        private Vector3D CalculateMissileIntercept(Vector3D targetPosition, Vector3D targetVelocity, Vector3D missilePos, Vector3D missileVelocity, double missileAcceleration, double compensationFactor = 1, double maxLateralThrustProportion = 0.5)
        {
            var missileToTarget = Vector3D.Normalize(targetPosition - missilePos);
            var relativeVelocity = targetVelocity - missileVelocity;
            var parallelVelocity = relativeVelocity.Dot(missileToTarget) * missileToTarget;
            var normalVelocity = (relativeVelocity - parallelVelocity);

            var normalMissileAcceleration = normalVelocity * compensationFactor;

            if (Vector3D.IsZero(normalMissileAcceleration))
                return missileToTarget * missileAcceleration;

            double maxLateralThrust = missileAcceleration * Math.Min(1, Math.Max(0, maxLateralThrustProportion));
            if (normalMissileAcceleration.LengthSquared() > maxLateralThrust * maxLateralThrust)
            {
                Vector3D.Normalize(ref normalMissileAcceleration, out normalMissileAcceleration);
                normalMissileAcceleration *= maxLateralThrustProportion;
            }

            double diff = missileAcceleration * missileAcceleration - normalMissileAcceleration.LengthSquared();
            return Math.Sqrt(diff) * missileToTarget + normalMissileAcceleration;
        }
    }
}
