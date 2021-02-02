using Sandbox.Game.Entities;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;
using WeaponCore.Support;

namespace WeaponCore.Platform
{
    public partial class Unit
    {
        internal class ParallelRayCallBack
        {
            internal Unit Unit;

            internal ParallelRayCallBack(Unit unit)
            {
                Unit = unit;
            }

            public void NormalShootRayCallBack(IHitInfo hitInfo)
            {
                Unit.Casting = false;
                var masterWeapon = Unit.TrackTarget ? Unit : Unit.Comp.TrackingUnit;
                var ignoreTargets = Unit.Target.IsProjectile || Unit.Target.TargetEntity is IMyCharacter;
                var trackingCheckPosition = Unit.GetScope.CachedPos;
                double rayDist = 0;


                if (Unit.System.Session.DebugLos)
                {
                    var hitPos = hitInfo.Position;
                    if (rayDist <= 0) Vector3D.Distance(ref trackingCheckPosition, ref hitPos, out rayDist);

                    Unit.System.Session.AddLosCheck(new Session.LosDebug { Unit = Unit, HitTick = Unit.System.Session.Tick, Line = new LineD(trackingCheckPosition, hitPos) });
                }

                
                if (Unit.Comp.Ai.ShieldNear)
                {
                    var targetPos = Unit.Target.Projectile?.Position ?? Unit.Target.TargetEntity.PositionComp.WorldMatrixRef.Translation;
                    var targetDir = targetPos - trackingCheckPosition;
                    if (Unit.HitFriendlyShield(trackingCheckPosition, targetPos, targetDir))
                    {
                        masterWeapon.Target.Reset(Unit.Comp.Session.Tick, Target.States.RayCheckFriendly);
                        if (masterWeapon != Unit) Unit.Target.Reset(Unit.Comp.Session.Tick, Target.States.RayCheckFriendly);
                        return;
                    }
                }

                var hitTopEnt = (MyEntity)hitInfo?.HitEntity?.GetTopMostParent();
                if (hitTopEnt == null)
                {
                    if (ignoreTargets)
                        return;
                    masterWeapon.Target.Reset(Unit.Comp.Session.Tick, Target.States.RayCheckMiss);
                    if (masterWeapon != Unit) Unit.Target.Reset(Unit.Comp.Session.Tick, Target.States.RayCheckMiss);
                    return;
                }

                var targetTopEnt = Unit.Target.TargetEntity?.GetTopMostParent();
                if (targetTopEnt == null)
                    return;

                var unexpectedHit = ignoreTargets || targetTopEnt != hitTopEnt;
                var topAsGrid = hitTopEnt as MyCubeGrid;

                if (unexpectedHit)
                {
                    if (hitTopEnt is MyVoxelBase)
                    {
                        masterWeapon.Target.Reset(Unit.Comp.Session.Tick, Target.States.RayCheckVoxel);
                        if (masterWeapon != Unit) Unit.Target.Reset(Unit.Comp.Session.Tick, Target.States.RayCheckVoxel);
                        return;
                    }

                    if (topAsGrid == null)
                        return;
                    if (Unit.Target.TargetEntity != null && Unit.Comp.IsBlock && (topAsGrid.IsSameConstructAs(Unit.Comp.Ai.GridEntity) || !topAsGrid.DestructibleBlocks || topAsGrid.Immune || topAsGrid.GridGeneralDamageModifier <= 0))
                    {
                        var hitPos = Unit.Target.TargetEntity.PositionComp.WorldAABB.Center;
                        Vector3D pos; 
                        if (CheckSelfHit(Unit, ref trackingCheckPosition, ref hitPos, out pos))
                        {
                            masterWeapon.Target.Reset(Unit.Comp.Session.Tick, Target.States.RayCheckSelfHit);
                            if (masterWeapon != Unit) Unit.Target.Reset(Unit.Comp.Session.Tick, Target.States.RayCheckSelfHit);
                            return;
                        }
                        return;
                    }
                    if (!Session.GridEnemy(Unit.Comp.Ai.AiOwner, topAsGrid))
                    {
                        masterWeapon.Target.Reset(Unit.Comp.Session.Tick, Target.States.RayCheckFriendly);
                        if (masterWeapon != Unit) Unit.Target.Reset(Unit.Comp.Session.Tick, Target.States.RayCheckFriendly);
                        return;
                    }
                    return;
                }
                if (Unit.System.ClosestFirst && topAsGrid != null && topAsGrid == targetTopEnt)
                {
                    var halfExtMin = topAsGrid.PositionComp.LocalAABB.HalfExtents.Min();
                    var minSize = topAsGrid.GridSizeR * 8;
                    var maxChange = halfExtMin > minSize ? halfExtMin : minSize;
                    var targetPos = Unit.Target.TargetEntity.PositionComp.WorldAABB.Center;
                    var weaponPos = trackingCheckPosition;

                    if (rayDist <= 0) Vector3D.Distance(ref weaponPos, ref targetPos, out rayDist);
                    var newHitShortDist = rayDist * (1 - hitInfo.Fraction);
                    var distanceToTarget = rayDist * hitInfo.Fraction;

                    var shortDistExceed = newHitShortDist - Unit.Target.HitShortDist > maxChange;
                    var escapeDistExceed = distanceToTarget - Unit.Target.OrigDistance > Unit.Target.OrigDistance;
                    if (shortDistExceed || escapeDistExceed)
                    {
                        masterWeapon.Target.Reset(Unit.Comp.Session.Tick, Target.States.RayCheckDistOffset);
                        if (masterWeapon != Unit) Unit.Target.Reset(Unit.Comp.Session.Tick, Target.States.RayCheckDistOffset);
                    }
                }
            }
        }

        internal class Muzzle
        {
            internal Muzzle(Unit unit, int id, Session session)
            {
                MuzzleId = id;
                UniqueId = session.NewVoxelCache.Id;
                Unit = unit;
            }

            internal Unit Unit;
            internal Vector3D Position;
            internal Vector3D Direction;
            internal Vector3D DeviatedDir;
            internal uint LastUpdateTick;
            internal uint LastAv1Tick;
            internal uint LastAv2Tick;
            internal int MuzzleId;
            internal ulong UniqueId;
            internal bool Av1Looping;
            internal bool Av2Looping;

        }

        internal class WeaponAcquire
        {
            internal readonly Unit Unit;
            internal uint CreatedTick;
            internal int SlotId;
            internal bool IsSleeping;
            internal bool Monitoring;

            internal WeaponAcquire(Unit unit)
            {
                Unit = unit;
            }
        }

    }
}
