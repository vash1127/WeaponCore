using System;
using System.Collections.Generic;
using ParallelTasks;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Entity;
using VRage.ModAPI;
using VRageMath;
using WeaponCore.Platform;
using WeaponCore.Support;

namespace WeaponCore.Projectiles
{
    internal partial class Projectiles
    {
        private const float StepConst = MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS;
        internal readonly ObjectsPool<Projectile> ProjectilePoolA = new ObjectsPool<Projectile>(5000, (Func<Projectile>)null);
        internal readonly ObjectsPool<Projectile> ProjectilePoolB = new ObjectsPool<Projectile>(5000, (Func<Projectile>)null);
        internal readonly ObjectsPool<Projectile> ProjectilePoolC = new ObjectsPool<Projectile>(5000, (Func<Projectile>)null);
        internal readonly ObjectsPool<Projectile> ProjectilePoolD = new ObjectsPool<Projectile>(5000, (Func<Projectile>)null);
        internal readonly ObjectsPool<Projectile> ProjectilePoolE = new ObjectsPool<Projectile>(5000, (Func<Projectile>)null);
        internal readonly ObjectsPool<Projectile> ProjectilePoolF = new ObjectsPool<Projectile>(5000, (Func<Projectile>)null);
        internal readonly MyConcurrentPool<List<MyLineSegmentOverlapResult<MyEntity>>> SegmentPoolA = new MyConcurrentPool<List<MyLineSegmentOverlapResult<MyEntity>>>(5000);
        internal readonly MyConcurrentPool<List<MyLineSegmentOverlapResult<MyEntity>>> SegmentPoolB = new MyConcurrentPool<List<MyLineSegmentOverlapResult<MyEntity>>>(5000);
        internal readonly MyConcurrentPool<List<MyLineSegmentOverlapResult<MyEntity>>> SegmentPoolC = new MyConcurrentPool<List<MyLineSegmentOverlapResult<MyEntity>>>(5000);
        internal readonly MyConcurrentPool<List<MyLineSegmentOverlapResult<MyEntity>>> SegmentPoolD = new MyConcurrentPool<List<MyLineSegmentOverlapResult<MyEntity>>>(5000);
        internal readonly MyConcurrentPool<List<MyLineSegmentOverlapResult<MyEntity>>> SegmentPoolE = new MyConcurrentPool<List<MyLineSegmentOverlapResult<MyEntity>>>(5000);
        internal readonly MyConcurrentPool<List<MyLineSegmentOverlapResult<MyEntity>>> SegmentPoolF = new MyConcurrentPool<List<MyLineSegmentOverlapResult<MyEntity>>>(5000);
        internal readonly MyConcurrentPool<List<IMyEntity>> CheckPoolA = new MyConcurrentPool<List<IMyEntity>>(500);
        internal readonly MyConcurrentPool<List<IMyEntity>> CheckPoolB = new MyConcurrentPool<List<IMyEntity>>(500);
        internal readonly MyConcurrentPool<List<IMyEntity>> CheckPoolC = new MyConcurrentPool<List<IMyEntity>>(500);
        internal readonly MyConcurrentPool<List<IMyEntity>> CheckPoolD = new MyConcurrentPool<List<IMyEntity>>(500);
        internal readonly MyConcurrentPool<List<IMyEntity>> CheckPoolE = new MyConcurrentPool<List<IMyEntity>>(500);
        internal readonly MyConcurrentPool<List<IMyEntity>> CheckPoolF = new MyConcurrentPool<List<IMyEntity>>(500);

        internal readonly MyConcurrentPool<List<LineD>> LinePoolA = new MyConcurrentPool<List<LineD>>(500);
        internal readonly MyConcurrentPool<List<LineD>> LinePoolB = new MyConcurrentPool<List<LineD>>(500);
        internal readonly MyConcurrentPool<List<LineD>> LinePoolC = new MyConcurrentPool<List<LineD>>(500);
        internal readonly MyConcurrentPool<List<LineD>> LinePoolD = new MyConcurrentPool<List<LineD>>(500);
        internal readonly MyConcurrentPool<List<LineD>> LinePoolE = new MyConcurrentPool<List<LineD>>(500);
        internal readonly MyConcurrentPool<List<LineD>> LinePoolF = new MyConcurrentPool<List<LineD>>(500);
        internal readonly object WaitA = new object();
        internal readonly object WaitB = new object();
        internal readonly object WaitC = new object();
        internal readonly object WaitD = new object();
        internal readonly object WaitE = new object();
        internal readonly object WaitF = new object();

        internal void Update()
        {
            MyAPIGateway.Parallel.Start(StartPoolA);
            MyAPIGateway.Parallel.Start(StartPoolB);
            MyAPIGateway.Parallel.Start(StartPoolC);
            MyAPIGateway.Parallel.Start(StartPoolD);
            MyAPIGateway.Parallel.Start(StartPoolE);
            MyAPIGateway.Parallel.Start(StartPoolF);
        }

        private void StartPoolA()
        {
            lock (WaitA) Process(ProjectilePoolA, Session.Instance.DrawProjectilesA, SegmentPoolA, CheckPoolA, LinePoolA);
        }

        private void StartPoolB()
        {
            lock (WaitB) Process(ProjectilePoolB, Session.Instance.DrawProjectilesB, SegmentPoolB, CheckPoolB, LinePoolB);
        }

        private void StartPoolC()
        {
            lock (WaitC) Process(ProjectilePoolC, Session.Instance.DrawProjectilesC, SegmentPoolC, CheckPoolC, LinePoolC);
        }

        private void StartPoolD()
        {
            lock (WaitD) Process(ProjectilePoolD, Session.Instance.DrawProjectilesD, SegmentPoolD, CheckPoolD, LinePoolD);
        }

        private void StartPoolE()
        {
            lock (WaitE) Process(ProjectilePoolE, Session.Instance.DrawProjectilesE, SegmentPoolE, CheckPoolE, LinePoolE);
        }

        private void StartPoolF()
        {
            lock (WaitF) Process(ProjectilePoolF, Session.Instance.DrawProjectilesF, SegmentPoolF, CheckPoolF, LinePoolF);
        }

        private void Process(
            ObjectsPool<Projectile> pool, 
            List<Session.DrawProjectile> drawList, 
            MyConcurrentPool<List<MyLineSegmentOverlapResult<MyEntity>>> segmentPool, 
            MyConcurrentPool<List<IMyEntity>> checkPool, 
            MyConcurrentPool<List<LineD>> linePool)
        {
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
                    var fired = new FiredBeam(p.Weapon, linePool.Get());
                    GetAllEntitiesInLine2(p.CheckList, fired, beam, segmentList);
                    var hitInfo = GetHitEntities(p.CheckList, beam);
                    if (GetDamageInfo(fired, beam, hitInfo, 0, false))
                    {
                        intersect = hitInfo.HitPos;
                        DamageEntities(fired);
                    }
                    linePool.Return(fired.Beams);
                    p.CheckList.Clear();
                    segmentList.Clear();

                    if (intersect != null)
                    {
                        var entity = hitInfo.Slim == null ? hitInfo.Entity : hitInfo.Slim.CubeGrid;
                        drawList.Add(new Session.DrawProjectile(p.Weapon, 0, new LineD(intersect.Value + -(p.Direction * p.Weapon.WeaponType.ShotLength), intersect.Value), p.CurrentMagnitude, hitInfo.HitPos, entity, true));
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

                if (p.Grow)
                {
                    p.CurrentLine = new LineD(p.Position, p.Position + -(p.Direction * (p.GrowStep * p.LineReSizeLen)));
                    if (p.GrowStep++ >= p.ReSizeSteps) p.Grow = false;
                }
                else p.CurrentLine = new LineD(p.Position + -(p.Direction * p.ShotLength), p.Position);
                var bb = new BoundingBoxD(p.CurrentLine.From, p.CurrentLine.To);

                if (MyAPIGateway.Session.Camera.IsInFrustum(ref bb))
                    drawList.Add(new Session.DrawProjectile(p.Weapon, 0, p.CurrentLine, p.CurrentSpeed, Vector3D.Zero, null, true));
            }
            pool.DeallocateAllMarked();
        }

        private void ProjectileClose(Projectile p, ObjectsPool<Projectile> pool, MyConcurrentPool<List<IMyEntity>> checkPool)
        {
            p.State = Projectile.ProjectileState.Dead;
            p.Effect1.Close(true, false);
            checkPool.Return(p.CheckList);
            pool.MarkForDeallocate(p);
        }

        internal struct Shot
        {
            public readonly Vector3D Position;
            public readonly Vector3D Direction;

            public Shot(Vector3D position, Vector3D direction)
            {
                Position = position;
                Direction = direction;
            }
        }
    }
}
