using System;
using System.Collections.Generic;
using System.Diagnostics;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Collections;
using VRage.Game;
using VRageMath;
using WeaponCore.Platform;
using WeaponCore.Support;

namespace WeaponCore.Projectiles
{
    internal partial class Projectiles
    {
        private const float StepConst = MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS;
        internal readonly ObjectsPool<Projectile> ProjectilePool0 = new ObjectsPool<Projectile>(15000, (Func<Projectile>)null);
        internal readonly ObjectsPool<Projectile> ProjectilePool1 = new ObjectsPool<Projectile>(15000, (Func<Projectile>)null);

        private readonly MyConcurrentPool<List<LineD>> _linePool = new MyConcurrentPool<List<LineD>>(32000);

        internal void Add(bool even)
        {
            lock (Session.Instance.Fired)
            {
                var firedList = Session.Instance.Fired;
                if (even)
                {
                    for (int i = 0; i < firedList.Count; i++)
                    {
                        var fired = firedList[i];
                        for (int j = 0; j < fired.Projectiles.Count; j++)
                        {
                            var f = fired.Projectiles[j];
                            Projectile projectile;
                            ProjectilePool0.AllocateOrCreate(out projectile);
                            projectile.Start(f, fired.Weapon, this, CheckPool.Get());
                        }
                    }
                }
                else
                {
                    for (int i = 0; i < firedList.Count; i++)
                    {
                        var fired = firedList[i];
                        for (int j = 0; j < fired.Projectiles.Count; j++)
                        {
                            var f = fired.Projectiles[j];
                            Projectile projectile;
                            ProjectilePool1.AllocateOrCreate(out projectile);
                            projectile.Start(f, fired.Weapon, this, CheckPool.Get());
                        }
                    }
                }
                firedList.Clear();
            }
        }

        internal void Update()
        {
            var even = Session.Instance.Tick % 2 == 0;
            Add(even);
            if (even)
            {
                lock (ProjectilePool0)
                {
                    lock (Session.Instance.DrawProjectiles0) Process(ProjectilePool0, true);
                    ProjectilePool0.DeallocateAllMarked();
                }
            }
            else
            {
                lock (ProjectilePool1)
                {
                    lock (Session.Instance.DrawProjectiles1) Process(ProjectilePool1, false);
                    ProjectilePool1.DeallocateAllMarked();
                }

            }
        }

        internal void Process(ObjectsPool<Projectile> pool, bool even)
        {
            foreach (var p in pool.Active)
            {
                if (p.State != Projectile.ProjectileState.Alive) continue;
                p.CurrentMagnitude = p.CurrentSpeed * StepConst;
                p.LastPosition = p.Position;
                p.Position += p.CurrentMagnitude;

                Vector3D? intersect = null;
                var segmentList = _segmentPool.Get();
                var beam = new LineD(p.LastPosition, p.Position + p.CurrentMagnitude);
                MyGamePruningStructure.GetTopmostEntitiesOverlappingRay(ref beam, segmentList);
                var segCount = segmentList.Count;
                if (segCount > 1 || segCount == 1 && segmentList[0].Element != p.MyGrid)
                {
                    var fired = new FiredBeam(p.Weapon, _linePool.Get());
                    GetAllEntitiesInLine2(p.CheckList, fired, beam, segmentList);
                    var hitInfo = GetHitEntities(p.CheckList, beam);
                    if (GetDamageInfo(fired, beam, hitInfo, 0, false))
                    {
                        pool.MarkForDeallocate(p);
                        intersect = hitInfo.HitPos;
                        DamageEntities(fired);
                    }
                    _linePool.Return(fired.Beams);
                    p.CheckList.Clear();
                    segmentList.Clear();

                    if (intersect != null)
                    {
                        var entity = hitInfo.Slim == null ? hitInfo.Entity : hitInfo.Slim.CubeGrid;
                        //Session.Instance.DrawProjectiles.Enqueue(new Session.DrawProjectile(p.Weapon, 0, new LineD(intersect.Value + -(p.Direction * p.Weapon.WeaponType.ShotLength), intersect.Value), p.CurrentMagnitude, hitInfo.HitPos, entity, true));
                        p.Close(even);
                    }
                }
                _segmentPool.Return(segmentList);
                if (intersect != null) continue;

                var distTraveled = (p.Origin - p.Position);
                if (Vector3D.Dot(distTraveled, distTraveled) >= p.MaxTrajectory * p.MaxTrajectory)
                {
                    p.Close(even);
                    continue;
                }

                if (p.Grow)
                {
                    //Log.Line($"grown:{p.GrowStep * p.LineReSizeLen} - stepping:{p.GrowStep}[{p.ReSizeSteps}] - growPerStep:{p.LineReSizeLen} - total:{p.ShotLength} - speed:{p.SpeedLength}[{p.SpeedLength / 60}]");
                    p.CurrentLine = new LineD(p.Position, p.Position + -(p.Direction * (p.GrowStep * p.LineReSizeLen)));
                    if (p.GrowStep++ >= p.ReSizeSteps) p.Grow = false;
                }
                else p.CurrentLine = new LineD(p.Position + -(p.Direction * p.ShotLength), p.Position);
                var sp = new BoundingBoxD(p.CurrentLine.From, p.CurrentLine.To);
                if (MyAPIGateway.Session.Camera.IsInFrustum(ref sp))
                {
                    if (even) Session.Instance.DrawProjectiles0.Add(new Session.DrawProjectile(p.Weapon, 0, p.CurrentLine, p.CurrentSpeed, Vector3D.Zero, null, true));
                    else Session.Instance.DrawProjectiles1.Add(new Session.DrawProjectile(p.Weapon, 0, p.CurrentLine, p.CurrentSpeed, Vector3D.Zero, null, true));
                }
            }
        }

        internal struct FiredProjectile
        {
            public readonly List<Shot> Projectiles;
            public readonly Weapon Weapon;

            public FiredProjectile(Weapon weapon, List<Shot> projectiles)
            {
                Weapon = weapon;
                Projectiles = projectiles;
            }
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
