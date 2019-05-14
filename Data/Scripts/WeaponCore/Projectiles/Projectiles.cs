using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using VRage.Collections;
using VRage.Game;
using VRage.ModAPI;
using VRageMath;
using WeaponCore.Platform;
using WeaponCore.Support;

namespace WeaponCore.Projectiles
{
    internal partial class Projectiles
    {
        private const float StepConst = MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS;
        internal readonly ObjectsPool<Projectile> ProjectilePool = new ObjectsPool<Projectile>(8192, (Func<Projectile>)null);
        private readonly MyConcurrentPool<List<LineD>> _linePool = new MyConcurrentPool<List<LineD>>();

        internal void Add(FiredProjectile fired)
        {
            foreach (var f in fired.Projectiles)
            {
                Projectile projectile;
                ProjectilePool.AllocateOrCreate(out projectile);
                projectile.Start(f, fired.Weapon,this, CheckPool.Get());
            }
        }

        internal void Update()
        {
            var test = new DSUtils();
            test.Sw.Restart();
            var c = 0;
            foreach (var p in ProjectilePool.Active)
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
                    c++;
                    var fired = new FiredBeam(p.Weapon, _linePool.Get());
                    GetAllEntitiesInLine2(p.CheckList, fired, beam, segmentList);
                    var hitInfo = GetHitEntities(p.CheckList, beam);
                    if (GetDamageInfo(fired, beam, hitInfo, 0, false))
                    {
                        ProjectilePool.MarkForDeallocate(p);
                        intersect = hitInfo.HitPos;
                        DamageEntities(fired);
                    }
                    _linePool.Return(fired.Beams);
                    p.CheckList.Clear();
                    segmentList.Clear();

                    if (intersect != null)
                    {
                        var entity = hitInfo.Slim == null ? hitInfo.Entity : hitInfo.Slim.CubeGrid;
                        Session.Instance.DrawProjectiles.Enqueue(new Session.DrawProjectile(p.Weapon, 0, new LineD(intersect.Value + -(p.Direction * p.Weapon.WeaponType.ShotLength), intersect.Value), p.CurrentMagnitude, hitInfo.HitPos, entity, true));
                        p.Close();
                    }
                }
                _segmentPool.Return(segmentList);
                if (intersect != null) continue;

                var distTraveled = (p.Origin - p.Position);
                if (Vector3D.Dot(distTraveled, distTraveled) >= p.MaxTrajectory * p.MaxTrajectory)
                {
                    p.Close();
                    continue;
                }
                var newLine = new LineD(p.Position + -(p.Direction * p.ShotLength), p.Position);
                p.PositionChecked = true;
                Session.Instance.DrawProjectiles.Enqueue(new Session.DrawProjectile(p.Weapon, 0, newLine, p.CurrentMagnitude, Vector3D.Zero, null, true));
            }
            test.StopWatchReport($"test: {ProjectilePool.Active.Count} - {c}", -1);
            ProjectilePool.DeallocateAllMarked();
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
