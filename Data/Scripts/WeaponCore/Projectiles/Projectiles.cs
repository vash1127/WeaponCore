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
            foreach (var projectile in ProjectilePool.Active)
            {
                if (projectile.State != Projectile.ProjectileState.Alive) continue;

                projectile.CurrentMagnitude = projectile.CurrentSpeed * StepConst;
                projectile.LastPosition = projectile.Position;
                projectile.Position += projectile.CurrentMagnitude;

                Vector3D? intersect = null;
                var segmentList = _segmentPool.Get();
                var beam = new LineD(projectile.LastPosition, projectile.Position + projectile.CurrentMagnitude);
                MyGamePruningStructure.GetTopmostEntitiesOverlappingRay(ref beam, segmentList);
                var segCount = segmentList.Count;
                if (segCount > 1 || segCount == 1 && segmentList[0].Element != projectile.MyGrid)
                {
                    c++;
                    var fired = new FiredBeam(projectile.Weapon, _linePool.Get());
                    GetAllEntitiesInLine2(projectile.CheckList, fired, beam, segmentList);
                    var hitInfo = GetHitEntities(projectile.CheckList, beam);
                    if (GetDamageInfo(fired, beam, hitInfo, 0, false))
                    {
                        ProjectilePool.MarkForDeallocate(projectile);
                        intersect = hitInfo.HitPos;
                        DamageEntities(fired);
                    }
                    _linePool.Return(fired.Beams);
                    projectile.CheckList.Clear();
                    segmentList.Clear();

                    if (intersect != null)
                    {
                        Session.Instance.DrawProjectiles.Enqueue(new Session.DrawProjectile(projectile.Weapon, 0, new LineD(projectile.LastPosition, intersect.Value), projectile.CurrentMagnitude, Vector3D.Zero, null, true));
                        projectile.Close();
                    }
                }
                _segmentPool.Return(segmentList);
                if (intersect != null) continue;

                var distTraveled = (projectile.Origin - projectile.Position);
                if (Vector3D.Dot(distTraveled, distTraveled) >= projectile.MaxTrajectory * projectile.MaxTrajectory)
                {
                    projectile.Close();
                    continue;
                }

                var newLine = new LineD(projectile.LastPosition, projectile.Position);
                projectile.PositionChecked = true;
                Session.Instance.DrawProjectiles.Enqueue(new Session.DrawProjectile(projectile.Weapon, 0, newLine, projectile.CurrentMagnitude, Vector3D.Zero, null, true));
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
