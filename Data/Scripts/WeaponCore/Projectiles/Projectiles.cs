using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;
using WeaponCore.Support;

namespace WeaponCore.Projectiles
{
    internal partial class Projectiles
    {
        private const float StepConst = MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS;
        internal const int PoolCount = 16;
        internal readonly ConcurrentQueue<IThreadHits> Hits = new ConcurrentQueue<IThreadHits>();

        internal readonly ObjectsPool<Projectile>[] ProjectilePool = new ObjectsPool<Projectile>[PoolCount];
        internal readonly EntityPool<MyEntity>[][] EntityPool = new EntityPool<MyEntity>[PoolCount][];
        internal readonly MyConcurrentPool<List<MyLineSegmentOverlapResult<MyEntity>>>[] SegmentPool = new MyConcurrentPool<List<MyLineSegmentOverlapResult<MyEntity>>>[PoolCount];
        internal readonly MyConcurrentPool<List<MyEntity>>[] CheckPool = new MyConcurrentPool<List<MyEntity>>[PoolCount];
        internal readonly MyConcurrentPool<List<LineD>>[] LinePool = new MyConcurrentPool<List<LineD>>[PoolCount];
        internal readonly MyConcurrentPool<DamageInfo>[] DamagePool = new MyConcurrentPool<DamageInfo>[PoolCount];
        internal readonly MyConcurrentDictionary<IMySlimBlock, DamageInfo>[] HitBlocks = new MyConcurrentDictionary<IMySlimBlock, DamageInfo>[PoolCount];
        internal readonly MyConcurrentDictionary<IMyEntity, DamageInfo>[] HitEnts = new MyConcurrentDictionary<IMyEntity, DamageInfo>[PoolCount];
        internal readonly List<DrawProjectile>[] DrawProjectiles = new List<DrawProjectile>[PoolCount];
        internal readonly object[] Wait = new object[PoolCount];

        internal Projectiles()
        {
            for (int i = 0; i < Wait.Length; i++)
            {
                Wait[i] = new object();
                ProjectilePool[i] = new ObjectsPool<Projectile>(1250);
                SegmentPool[i] = new MyConcurrentPool<List<MyLineSegmentOverlapResult<MyEntity>>>(1250);
                CheckPool[i] = new MyConcurrentPool<List<MyEntity>>(1250);
                LinePool[i] = new MyConcurrentPool<List<LineD>>(1250);
                DamagePool[i] = new MyConcurrentPool<DamageInfo>(500);
                HitBlocks[i] = new MyConcurrentDictionary<IMySlimBlock, DamageInfo>(500);
                HitEnts[i] = new MyConcurrentDictionary<IMyEntity, DamageInfo>(50);
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
            //MyAPIGateway.Parallel.For(0, Wait.Length, Process, 1);
            for (int i = 0; i < Wait.Length; i++)
            {
                Process(i);
            }
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
                var checkPool = CheckPool[i];
                var linePool = LinePool[i];
                var damagePool = DamagePool[i];
                var hitBlocks = HitBlocks[i];
                var hitEnts = HitEnts[i];
                var modelClose = false;
                foreach (var p in pool.Active)
                {
                    p.Age++;
                    switch (p.State)
                    {
                        case Projectile.ProjectileState.Dead:
                            continue;
                        case Projectile.ProjectileState.Start:
                            p.Start(checkPool.Get(), noAv);
                            break;
                        case Projectile.ProjectileState.Ending:
                            if (p.ModelId == -1)
                                p.Stop(pool, checkPool, null, null);
                            else
                            {
                                p.Stop(pool, checkPool, entPool[p.ModelId], drawList);
                                modelClose = true;
                            }
                            continue;
                        case Projectile.ProjectileState.OneAndDone:
                            p.Stop(pool, checkPool, null, null);
                            continue;
                    }
                    p.LastPosition = p.Position;

                    if (p.Guidance == AmmoTrajectory.GuidanceType.Smart)
                    {
                        Vector3D newVel;
                        if ((p.AccelLength <= 0 || Vector3D.DistanceSquared(p.Origin, p.Position) > p.SmartsDelayDistSqr))
                        {
                            var trajInfo = p.System.Values.Ammo.Trajectory;
                            if (p.Target != null && !p.Target.MarkedForClose)
                            {
                                var physics = p.Target.Physics ?? p.Target.Parent.Physics;
                                var targetPos = p.Target.PositionComp.WorldAABB.Center;
                                if (targetPos != Vector3D.Zero) p.PrevTargetPos = targetPos;
                                p.PrevTargetVel = physics.LinearVelocity;
                            }
                            else if (p.State != Projectile.ProjectileState.Zombie)
                            {
                                p.DistanceTraveled = 0;
                                p.DistanceToTravelSqr = (Vector3D.DistanceSquared(p.Position, p.PrevTargetPos) + 100);
                                p.State = Projectile.ProjectileState.Zombie;
                            }
                            var commandedAccel = CalculateMissileIntercept(p.PrevTargetPos, p.PrevTargetVel, p.Position, p.Velocity, trajInfo.AccelPerSec, trajInfo.SmartsFactor);
                            newVel = p.Velocity + (commandedAccel * StepConst);
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
                            p.EntityMatrix = MatrixD.CreateWorld(p.Position, p.Direction, p.Entity.PositionComp.WorldMatrix.Up);
                            if (p.Effect1 != null && p.System.AmmoParticle)
                            {
                                var offVec = p.Position + Vector3D.Rotate(p.System.Values.Graphics.Particles.AmmoOffset, p.EntityMatrix);
                                p.Effect1.WorldMatrix = p.EntityMatrix;
                                p.Effect1.SetTranslation(offVec);
                            }
                        }
                        catch (Exception ex) { Log.Line($"Exception in EntityMatrix: {ex}"); }
                    }
                    else if (!p.ConstantSpeed && p.Effect1 != null && p.System.AmmoParticle)
                        p.Effect1.Velocity = p.Velocity;

                    Vector3D? intersect = null;
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
                            var fired = new Fired(p.System, linePool.Get(), p.FiringCube, p.ReverseOriginRay, p.Direction, p.Age);
                            GetAllEntitiesInLine(p.CheckList, fired, beam, segmentList, null);
                            var hitInfo = GetHitEntities(p.CheckList, fired, beam);
                            if (GetDamageInfo(fired, p.Entity, p.EntityMatrix, beam, hitInfo, hitEnts, hitBlocks, damagePool, 0, false))
                            {
                                intersect = hitInfo.HitPos;
                                DamageEntities(fired, hitEnts, hitBlocks, damagePool);
                            }
                            linePool.Return(fired.Shots);
                            p.CheckList.Clear();
                            segmentList.Clear();

                            if (intersect != null)
                            {
                                if (!noAv && p.Draw && (p.DrawLine || p.ModelId != -1))
                                {
                                    var entity = hitInfo.Slim == null ? hitInfo.Entity : hitInfo.Slim.CubeGrid;
                                    var hitLine = new LineD(p.LastPosition, hitInfo.HitPos);
                                    drawList.Add(new DrawProjectile(ref fired, p.Entity, p.EntityMatrix, 0, hitLine, p.Velocity, hitInfo.HitPos, entity, true, p.MaxSpeedLength, p.ReSizeSteps, p.Shrink, false));
                                }
                                p.ProjectileClose(pool, checkPool, noAv);
                            }
                        }
                        catch (Exception ex) { Log.Line($"Exception in GetTopmostEntitiesOverlappingRay: {ex}"); }
                    }
                    segmentPool.Return(segmentList);
                    if (intersect != null) continue;

                    if (p.State != Projectile.ProjectileState.OneAndDone)
                    {
                        if (p.DistanceTraveled * p.DistanceTraveled >= p.DistanceToTravelSqr)
                        {
                            if (p.MoveToAndActivate || p.System.AmmoAreaEffect)
                                GetEntitiesInBlastRadius(new Fired(p.System, null, p.FiringCube, p.ReverseOriginRay, p.Direction, p.Age), p.Position, i);

                            p.ProjectileClose(pool, checkPool, noAv);
                            continue;
                        }
                    }

                    if (noAv || !p.Draw) continue;

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
                            p.LastEntityPos = p.Position;
                            drawList.Add(new DrawProjectile(ref p.DummyFired, p.Entity, p.EntityMatrix, 0, p.CurrentLine, p.Velocity, Vector3D.Zero, null, true, 0, 0, false, false));
                        }
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
                        drawList.Add(new DrawProjectile(ref p.DummyFired, p.Entity, p.EntityMatrix, 0, p.CurrentLine, p.Velocity, Vector3D.Zero, null, true, 0, 0, false, false));
                    }
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
