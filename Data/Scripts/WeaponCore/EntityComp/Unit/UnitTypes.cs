using Sandbox.Game.Entities;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;
using WeaponCore.Support;

namespace WeaponCore.Platform
{
    public partial class Part
    {
        internal class ParallelRayCallBack
        {
            internal Part Part;

            internal ParallelRayCallBack(Part part)
            {
                Part = part;
            }

            public void NormalShootRayCallBack(IHitInfo hitInfo)
            {
                Part.Casting = false;
                var masterWeapon = Part.TrackTarget ? Part : Part.Comp.TrackingPart;
                var ignoreTargets = Part.Target.IsProjectile || Part.Target.TargetEntity is IMyCharacter;
                var trackingCheckPosition = Part.GetScope.CachedPos;
                double rayDist = 0;


                if (Part.System.Session.DebugLos)
                {
                    var hitPos = hitInfo.Position;
                    if (rayDist <= 0) Vector3D.Distance(ref trackingCheckPosition, ref hitPos, out rayDist);

                    Part.System.Session.AddLosCheck(new Session.LosDebug { Part = Part, HitTick = Part.System.Session.Tick, Line = new LineD(trackingCheckPosition, hitPos) });
                }

                
                if (Part.Comp.Ai.ShieldNear)
                {
                    var targetPos = Part.Target.Projectile?.Position ?? Part.Target.TargetEntity.PositionComp.WorldMatrixRef.Translation;
                    var targetDir = targetPos - trackingCheckPosition;
                    if (Part.HitFriendlyShield(trackingCheckPosition, targetPos, targetDir))
                    {
                        masterWeapon.Target.Reset(Part.Comp.Session.Tick, Target.States.RayCheckFriendly);
                        if (masterWeapon != Part) Part.Target.Reset(Part.Comp.Session.Tick, Target.States.RayCheckFriendly);
                        return;
                    }
                }

                var hitTopEnt = (MyEntity)hitInfo?.HitEntity?.GetTopMostParent();
                if (hitTopEnt == null)
                {
                    if (ignoreTargets)
                        return;
                    masterWeapon.Target.Reset(Part.Comp.Session.Tick, Target.States.RayCheckMiss);
                    if (masterWeapon != Part) Part.Target.Reset(Part.Comp.Session.Tick, Target.States.RayCheckMiss);
                    return;
                }

                var targetTopEnt = Part.Target.TargetEntity?.GetTopMostParent();
                if (targetTopEnt == null)
                    return;

                var unexpectedHit = ignoreTargets || targetTopEnt != hitTopEnt;
                var topAsGrid = hitTopEnt as MyCubeGrid;

                if (unexpectedHit)
                {
                    if (hitTopEnt is MyVoxelBase)
                    {
                        masterWeapon.Target.Reset(Part.Comp.Session.Tick, Target.States.RayCheckVoxel);
                        if (masterWeapon != Part) Part.Target.Reset(Part.Comp.Session.Tick, Target.States.RayCheckVoxel);
                        return;
                    }

                    if (topAsGrid == null)
                        return;
                    if (Part.Target.TargetEntity != null && Part.Comp.IsBlock && (topAsGrid.IsSameConstructAs(Part.Comp.Ai.GridEntity) || !topAsGrid.DestructibleBlocks || topAsGrid.Immune || topAsGrid.GridGeneralDamageModifier <= 0))
                    {
                        var hitPos = Part.Target.TargetEntity.PositionComp.WorldAABB.Center;
                        Vector3D pos; 
                        if (CheckSelfHit(Part, ref trackingCheckPosition, ref hitPos, out pos))
                        {
                            masterWeapon.Target.Reset(Part.Comp.Session.Tick, Target.States.RayCheckSelfHit);
                            if (masterWeapon != Part) Part.Target.Reset(Part.Comp.Session.Tick, Target.States.RayCheckSelfHit);
                            return;
                        }
                        return;
                    }
                    if (!Session.GridEnemy(Part.Comp.Ai.AiOwner, topAsGrid))
                    {
                        masterWeapon.Target.Reset(Part.Comp.Session.Tick, Target.States.RayCheckFriendly);
                        if (masterWeapon != Part) Part.Target.Reset(Part.Comp.Session.Tick, Target.States.RayCheckFriendly);
                        return;
                    }
                    return;
                }
                if (Part.System.ClosestFirst && topAsGrid != null && topAsGrid == targetTopEnt)
                {
                    var halfExtMin = topAsGrid.PositionComp.LocalAABB.HalfExtents.Min();
                    var minSize = topAsGrid.GridSizeR * 8;
                    var maxChange = halfExtMin > minSize ? halfExtMin : minSize;
                    var targetPos = Part.Target.TargetEntity.PositionComp.WorldAABB.Center;
                    var weaponPos = trackingCheckPosition;

                    if (rayDist <= 0) Vector3D.Distance(ref weaponPos, ref targetPos, out rayDist);
                    var newHitShortDist = rayDist * (1 - hitInfo.Fraction);
                    var distanceToTarget = rayDist * hitInfo.Fraction;

                    var shortDistExceed = newHitShortDist - Part.Target.HitShortDist > maxChange;
                    var escapeDistExceed = distanceToTarget - Part.Target.OrigDistance > Part.Target.OrigDistance;
                    if (shortDistExceed || escapeDistExceed)
                    {
                        masterWeapon.Target.Reset(Part.Comp.Session.Tick, Target.States.RayCheckDistOffset);
                        if (masterWeapon != Part) Part.Target.Reset(Part.Comp.Session.Tick, Target.States.RayCheckDistOffset);
                    }
                }
            }
        }

        internal class Muzzle
        {
            internal Muzzle(Part part, int id, Session session)
            {
                MuzzleId = id;
                UniqueId = session.NewVoxelCache.Id;
                Part = part;
            }

            internal Part Part;
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
            internal readonly Part Part;
            internal uint CreatedTick;
            internal int SlotId;
            internal bool IsSleeping;
            internal bool Monitoring;

            internal WeaponAcquire(Part part)
            {
                Part = part;
            }
        }

    }
}
