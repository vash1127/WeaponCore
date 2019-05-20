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
        internal Projectiles()
        {
            for (int i = 0; i < Wait.Length; i++)
            {
                Wait[i] = new object();
                ProjectilePool[i] = new ObjectsPool<Projectile>(5000, (Func<Projectile>)null);
                SegmentPool[i] = new MyConcurrentPool<List<MyLineSegmentOverlapResult<MyEntity>>>(5000);
                CheckPool[i] = new MyConcurrentPool<List<MyEntity>>(5000);
                LinePool[i] = new MyConcurrentPool<List<LineD>>(5000);
                DrawProjectiles[i] = new List<DrawProjectile>();
            }
        }

        private const float StepConst = MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS;
        internal readonly ObjectsPool<Projectile>[] ProjectilePool = new ObjectsPool<Projectile>[6];
        internal readonly MyConcurrentPool<List<MyLineSegmentOverlapResult<MyEntity>>>[] SegmentPool = new MyConcurrentPool<List<MyLineSegmentOverlapResult<MyEntity>>>[6];
        internal readonly MyConcurrentPool<List<MyEntity>>[] CheckPool = new MyConcurrentPool<List<MyEntity>>[6];
        internal readonly MyConcurrentPool<List<LineD>>[] LinePool = new MyConcurrentPool<List<LineD>>[6];
        internal readonly List<DrawProjectile>[] DrawProjectiles = new List<DrawProjectile>[6];
        internal readonly object[] Wait = new object[6];

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
            lock (Wait[0]) Process(ProjectilePool[0], DrawProjectiles[0], SegmentPool[0], CheckPool[0], LinePool[0]);
        }

        private void StartPoolB()
        {
            lock (Wait[1]) Process(ProjectilePool[1], DrawProjectiles[1], SegmentPool[1], CheckPool[1], LinePool[1]);
        }

        private void StartPoolC()
        {
            lock (Wait[2]) Process(ProjectilePool[2], DrawProjectiles[2], SegmentPool[2], CheckPool[2], LinePool[2]);
        }

        private void StartPoolD()
        {
            lock (Wait[3]) Process(ProjectilePool[3], DrawProjectiles[3], SegmentPool[3], CheckPool[3], LinePool[3]);
        }

        private void StartPoolE()
        {
            lock (Wait[4]) Process(ProjectilePool[4], DrawProjectiles[4], SegmentPool[4], CheckPool[4], LinePool[4]);
        }

        private void StartPoolF()
        {
            lock (Wait[5]) Process(ProjectilePool[5], DrawProjectiles[5], SegmentPool[5], CheckPool[5], LinePool[5]);
        }

        private void Process(
            ObjectsPool<Projectile> pool, 
            List<DrawProjectile> drawList, 
            MyConcurrentPool<List<MyLineSegmentOverlapResult<MyEntity>>> segmentPool, 
            MyConcurrentPool<List<MyEntity>> checkPool, 
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
                    var hitInfo = GetHitEntities(p.CheckList, fired, beam);
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
                        drawList.Add(new DrawProjectile(p.Weapon, 0, p.CurrentLine, p.CurrentSpeed, hitInfo.HitPos, entity, true));
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
                    drawList.Add(new DrawProjectile(p.Weapon, 0, p.CurrentLine, p.CurrentSpeed, Vector3D.Zero, null, true));
            }
            pool.DeallocateAllMarked();
        }

        private void ProjectileClose(Projectile p, ObjectsPool<Projectile> pool, MyConcurrentPool<List<MyEntity>> checkPool)
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
