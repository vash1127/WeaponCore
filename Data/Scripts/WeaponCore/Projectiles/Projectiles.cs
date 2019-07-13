using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;
using WeaponCore.Support;

namespace WeaponCore.Projectiles
{
    internal partial class Projectiles
    {
        private const float StepConst = MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS;
        internal const int PoolCount = 16;
        internal readonly ConcurrentQueue<Session.DamageEvent> Hits = new ConcurrentQueue<Session.DamageEvent>();

        internal readonly ObjectsPool<Projectile>[] ProjectilePool = new ObjectsPool<Projectile>[PoolCount];
        internal readonly EntityPool<MyEntity>[][] EntityPool = new EntityPool<MyEntity>[PoolCount][];
        internal readonly MyConcurrentPool<List<MyLineSegmentOverlapResult<MyEntity>>>[] SegmentPool = new MyConcurrentPool<List<MyLineSegmentOverlapResult<MyEntity>>>[PoolCount];
        internal readonly MyConcurrentPool<List<HitEntity>>[] HitsPool = new MyConcurrentPool<List<HitEntity>>[PoolCount];
        internal readonly MyConcurrentPool<HitEntity>[] HitEntityPool = new MyConcurrentPool<HitEntity>[PoolCount];
        internal readonly MyConcurrentPool<List<MyEntity>>[] MyEntityPool = new MyConcurrentPool<List<MyEntity>>[PoolCount];
        internal readonly MyConcurrentPool<List<LineD>>[] LinePool = new MyConcurrentPool<List<LineD>>[PoolCount];
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
                HitEntityPool[i] = new MyConcurrentPool<HitEntity>(1250);
                MyEntityPool[i] = new MyConcurrentPool<List<MyEntity>>(100);
                LinePool[i] = new MyConcurrentPool<List<LineD>>(1250);
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
                var linePool = LinePool[i];
                var modelClose = false;
                foreach (var p in pool.Active)
                {
                    p.Age++;
                    switch (p.State)
                    {
                        case Projectile.ProjectileState.Dead:
                            continue;
                        case Projectile.ProjectileState.Start:
                            p.Start(hitsPool.Get(), noAv);
                            if (p.ModelState == Projectile.EntityState.NoDraw)
                                modelClose = p.CloseModel(entPool[p.ModelId], drawList);
                            break;
                        case Projectile.ProjectileState.Ending:
                            if (p.ModelState != Projectile.EntityState.Exists) p.Stop(pool, hitsPool);
                            else
                            {
                                modelClose = p.CloseModel(entPool[p.ModelId], drawList);
                                p.Stop(pool, hitsPool);
                            }
                            continue;
                        case Projectile.ProjectileState.OneAndDone:
                            p.Stop(pool, hitsPool);
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
                                var commandedAccel = CalculateMissileIntercept(p.PrevTargetPos, p.PrevTargetVel, p.Position, p.Velocity, p.AccelPerSec, p.System.Values.Ammo.Trajectory.SmartsFactor);
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
                            if (p.MoveToAndActivate || p.System.AmmoAreaEffect)
                            {
                                GetEntitiesInBlastRadius(p.HitList, p.FiringCube, p.System, p.Direction, p.Position, i);
                                p.HitPos = p.Position;
                            }

                            p.ProjectileClose(pool, hitsPool, noAv);
                            continue;
                        }
                    }
                    if (p.HitPos != null) continue;

                    var segmentList = segmentPool.Get();
                    LineD beam;
                    if (p.State == Projectile.ProjectileState.OneAndDone || p.Guidance != AmmoTrajectory.GuidanceType.None) beam = new LineD(p.LastPosition, p.Position);
                    else beam = new LineD(p.LastPosition, p.Position);
                    MyGamePruningStructure.GetTopmostEntitiesOverlappingRay(ref beam, segmentList);
                    var segCount = segmentList.Count;
                    if (segCount > 1 || segCount == 1 && segmentList[0].Element != p.FiringGrid)
                    {
                        try
                        {
                            var fired = new Fired(p.System, linePool.Get(), p.FiringCube, p.ReverseOriginRay, p.Direction, p.WeaponId, p.MuzzleId, p.IsBeamWeapon, 0);
                            GetAllEntitiesInLine(p.FiringCube, beam, segmentList, p.HitList, i);

                            HitEntity hitEnt = null;
                            if (p.HitList.Count > 0) hitEnt = GenerateHitInfo(p.HitList, i);

                            linePool.Return(fired.Shots);
                            segmentList.Clear();

                            if (hitEnt?.HitPos != null)
                            {
                                p.HitPos = hitEnt.HitPos;
                                if (!noAv && p.EnableAv && (p.DrawLine || p.ModelId != -1))
                                {
                                    var hitLine = new LineD(p.LastPosition, p.HitPos.Value);
                                    p.TestSphere.Center = p.HitPos.Value;
                                    var hitOnScreen = camera.IsInFrustum(ref p.TestSphere);
                                    drawList.Add(new DrawProjectile(ref fired, p.Entity, p.EntityMatrix, 0, hitLine, p.Velocity, p.HitPos, hitEnt.Entity, true, p.MaxSpeedLength, p.ReSizeSteps, p.Shrink, false, hitOnScreen));
                                }
                                Hits.Enqueue(new Session.DamageEvent(fired.System, fired.Direction, p.HitList, fired.FiringCube, i));
                                p.ProjectileClose(pool, hitsPool, noAv);
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
                        else p.Sound1.SetPosition(p.Position);
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
                            drawList.Add(new DrawProjectile(ref p.DummyFired, p.Entity, p.EntityMatrix, 0, p.CurrentLine, p.Velocity, p.HitPos, null, true, 0, 0, false, false, true));
                        }
                        else p.OnScreen = false;
                        continue;
                    }

                    if (!p.DrawLine) continue;

                    if (p.Grow)
                    {
                        if (p.AccelLength <= 0)
                        {
                            p.CurrentLine = new LineD(p.Position, p.Position + -(p.Direction * (p.GrowStep * p.MaxSpeedLength)));
                            if (p.GrowStep++ >= p.ReSizeSteps) p.Grow = false;
                        }
                        else
                            p.CurrentLine = new LineD(p.Position, p.Position + - (p.Direction * Vector3D.Distance(p.Origin, p.Position)));

                        if (Vector3D.DistanceSquared(p.Origin, p.Position) > p.ShotLength * p.ShotLength) p.Grow = false;
                    }
                    else if (p.State == Projectile.ProjectileState.OneAndDone) p.CurrentLine = new LineD(p.LastPosition, p.Position);
                    else p.CurrentLine = new LineD(p.Position + -(p.Direction * p.ShotLength), p.Position);

                    var bb = new BoundingBoxD(Vector3D.Min(p.CurrentLine.From, p.CurrentLine.To), Vector3D.Max(p.CurrentLine.From, p.CurrentLine.To));
                    if (camera.IsInFrustum(ref bb))
                    {
                        p.OnScreen = true;
                        drawList.Add(new DrawProjectile(ref p.DummyFired, p.Entity, p.EntityMatrix, 0, p.CurrentLine, p.Velocity, p.HitPos, null, true, 0, 0, false, false, true));
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
        private Vector3D CalculateMissileIntercept(Vector3D targetPosition, Vector3D targetVelocity, Vector3D missilePos, Vector3D missileVelocity, double missileAcceleration, double compensationFactor = 1)
        {
            var missileToTarget = Vector3D.Normalize(targetPosition - missilePos);
            var relativeVelocity = targetVelocity - missileVelocity;
            var parallelVelocity = relativeVelocity.Dot(missileToTarget) * missileToTarget;
            var normalVelocity = (relativeVelocity - parallelVelocity);

            var normalMissileAcceleration = normalVelocity * compensationFactor;

            if (Vector3D.IsZero(normalMissileAcceleration))
                return missileToTarget * missileAcceleration;

            double diff = missileAcceleration * missileAcceleration - normalMissileAcceleration.LengthSquared();
            if (diff < 0)
            {
                return Vector3D.Normalize(normalMissileAcceleration) * missileAcceleration; //fly parallel to the target
            }

            return Math.Sqrt(diff) * missileToTarget + normalMissileAcceleration;
        }
    }
}
