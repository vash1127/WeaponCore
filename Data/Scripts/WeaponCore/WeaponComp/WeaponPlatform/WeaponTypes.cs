using Sandbox.Game.Entities;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;
using WeaponCore.Support;

namespace WeaponCore.Platform
{
    public partial class Weapon
    {
        internal class ParallelRayCallBack
        {
            internal Weapon Weapon;

            internal ParallelRayCallBack(Weapon weapon)
            {
                Weapon = weapon;
            }

            public void NormalShootRayCallBack(IHitInfo hitInfo)
            {
                Weapon.Casting = false;
                var masterWeapon = Weapon.TrackTarget ? Weapon : Weapon.Comp.TrackingWeapon;
                var ignoreTargets = Weapon.Target.IsProjectile || Weapon.Target.Entity is IMyCharacter;

                double rayDist = 0;
                if (Weapon.Comp.Ai.ShieldNear)
                {
                    var targetPos = Weapon.Target.Projectile?.Position ?? Weapon.Target.Entity.PositionComp.WorldMatrixRef.Translation;
                    var targetDir = targetPos - Weapon.MyPivotPos;
                    if (Weapon.HitFriendlyShield(targetPos, targetDir))
                    {
                        masterWeapon.Target.Reset(Weapon.Comp.Session.Tick, Target.States.RayCheckFailed);
                        if (masterWeapon != Weapon) Weapon.Target.Reset(Weapon.Comp.Session.Tick, Target.States.RayCheckFailed);
                        return;
                    }
                }

                var hitTopEnt = (MyEntity)hitInfo?.HitEntity?.GetTopMostParent();
                if (hitTopEnt == null)
                {
                    if (ignoreTargets)
                    {
                        return;
                    }

                    masterWeapon.Target.Reset(Weapon.Comp.Session.Tick, Target.States.RayCheckFailed);
                    if (masterWeapon != Weapon) Weapon.Target.Reset(Weapon.Comp.Session.Tick, Target.States.RayCheckFailed);
                    return;
                }

                var targetTopEnt = Weapon.Target.Entity?.GetTopMostParent();
                if (targetTopEnt == null)
                {
                    return;
                }

                var unexpectedHit = ignoreTargets || targetTopEnt != hitTopEnt;
                var topAsGrid = hitTopEnt as MyCubeGrid;

                if (unexpectedHit)
                {
                    if (hitTopEnt is MyVoxelBase)
                    {
                        masterWeapon.Target.Reset(Weapon.Comp.Session.Tick, Target.States.RayCheckFailed);
                        if (masterWeapon != Weapon) Weapon.Target.Reset(Weapon.Comp.Session.Tick, Target.States.RayCheckFailed);
                        return;
                    }

                    if (topAsGrid == null)
                        return;

                    if (topAsGrid.IsSameConstructAs(Weapon.Comp.Ai.MyGrid))
                    {
                        masterWeapon.Target.Reset(Weapon.Comp.Session.Tick, Target.States.RayCheckFailed);
                        if (masterWeapon != Weapon) Weapon.Target.Reset(Weapon.Comp.Session.Tick, Target.States.RayCheckFailed);
                        return;
                    }

                    if (!Session.GridEnemy(Weapon.Comp.Ai.MyOwner, topAsGrid))
                    {
                        masterWeapon.Target.Reset(Weapon.Comp.Session.Tick, Target.States.RayCheckFailed);
                        if (masterWeapon != Weapon) Weapon.Target.Reset(Weapon.Comp.Session.Tick, Target.States.RayCheckFailed);
                        return;
                    }
                    return;
                }
                if (Weapon.System.ClosestFirst && topAsGrid != null)
                {
                    var maxChange = hitInfo.HitEntity.PositionComp.LocalAABB.HalfExtents.Min();
                    var targetPos = Weapon.Target.Entity.PositionComp.WorldMatrixRef.Translation;
                    var weaponPos = Weapon.MyPivotPos;

                    if (rayDist <= 0) Vector3D.Distance(ref weaponPos, ref targetPos, out rayDist);
                    var newHitShortDist = rayDist * (1 - hitInfo.Fraction);
                    var distanceToTarget = rayDist * hitInfo.Fraction;

                    var shortDistExceed = newHitShortDist - Weapon.Target.HitShortDist > maxChange;
                    var escapeDistExceed = distanceToTarget - Weapon.Target.OrigDistance > Weapon.Target.OrigDistance;
                    if (shortDistExceed || escapeDistExceed)
                    {
                        masterWeapon.Target.Reset(Weapon.Comp.Session.Tick, Target.States.RayCheckFailed);
                        if (masterWeapon != Weapon) Weapon.Target.Reset(Weapon.Comp.Session.Tick, Target.States.RayCheckFailed);
                    }
                }
            }
        }

        public class Muzzle
        {
            public Muzzle(int id, Session session)
            {
                MuzzleId = id;
                UniqueId = session.UniqueMuzzleId;
                session.VoxelCaches.Add(UniqueId, new VoxelCache());
            }

            public Vector3D Position;
            public Vector3D Direction;
            public Vector3D DeviatedDir;
            public uint LastUpdateTick;
            public uint LastAv1Tick;
            public uint LastAv2Tick;
            public int MuzzleId;
            public int UniqueId;
            public bool Av1Looping;
            public bool Av2Looping;

        }
    }
}
