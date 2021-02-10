using System;
using CoreSystems.Support;
using Jakaria;
using Sandbox.Engine.Physics;
using Sandbox.Game.Entities;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;
using static CoreSystems.Support.WeaponDefinition.HardPointDef;
using static CoreSystems.Support.WeaponDefinition.AmmoDef.TrajectoryDef;
using CollisionLayers = Sandbox.Engine.Physics.MyPhysics.CollisionLayers;

namespace CoreSystems.Platform
{
    public partial class Weapon 
    {
        internal static bool CanShootTarget(Weapon weapon, ref Vector3D targetCenter, Vector3D targetLinVel, Vector3D targetAccel, out Vector3D targetPos, bool checkSelfHit = false, MyEntity target = null)
        {
            var prediction = weapon.System.Values.HardPoint.AimLeadingPrediction;
            var trackingWeapon = weapon.TurretMode ? weapon : weapon.Comp.TrackingWeapon;
            if (Vector3D.IsZero(targetLinVel, 5E-03)) targetLinVel = Vector3.Zero;
            if (Vector3D.IsZero(targetAccel, 5E-03)) targetAccel = Vector3.Zero;

            var validEstimate = true;
            if (prediction != Prediction.Off && !weapon.ActiveAmmoDef.AmmoDef.Const.IsBeamWeapon && weapon.ActiveAmmoDef.AmmoDef.Const.DesiredProjectileSpeed > 0)
                targetPos = TrajectoryEstimation(weapon, targetCenter, targetLinVel, targetAccel, out validEstimate);
            else
                targetPos = targetCenter;
            var targetDir = targetPos - weapon.MyPivotPos;

            double rangeToTarget;
            Vector3D.DistanceSquared(ref targetPos, ref weapon.MyPivotPos, out rangeToTarget);

            var inRange = rangeToTarget <= weapon.MaxTargetDistanceSqr && rangeToTarget >= weapon.MinTargetDistanceSqr;

            bool canTrack;
            bool isTracking;

            if (weapon == trackingWeapon)
                canTrack = validEstimate && MathFuncs.WeaponLookAt(weapon, ref targetDir, rangeToTarget, false, true, out isTracking);
            else
                canTrack = validEstimate && MathFuncs.IsDotProductWithinTolerance(ref weapon.MyPivotFwd, ref targetDir, weapon.AimingTolerance);

            bool selfHit = false;
            weapon.LastHitInfo = null;
            if (checkSelfHit && target != null && weapon.BaseComp.IsBlock) {

                var testLine = new LineD(targetCenter, weapon.BarrelOrigin);
                var predictedMuzzlePos = testLine.To + (-testLine.Direction * weapon.MuzzleDistToBarrelCenter);
                var ai = weapon.BaseComp.Ai;
                var localPredictedPos = Vector3I.Round(Vector3D.Transform(predictedMuzzlePos, ai.TopEntity.PositionComp.WorldMatrixNormalizedInv) * ai.GridEntity.GridSizeR);

                MyCube cube;
                var noCubeAtPosition = !ai.GridEntity.TryGetCube(localPredictedPos, out cube);
                if (noCubeAtPosition || cube.CubeBlock == weapon.BaseComp.Cube.SlimBlock) {

                    var noCubeInLine = !ai.GridEntity.GetIntersectionWithLine(ref testLine, ref ai.GridHitInfo);
                    var noCubesInLineOrHitSelf = noCubeInLine || ai.GridHitInfo.Position == weapon.BaseComp.Cube.Position;

                    if (noCubesInLineOrHitSelf) {

                        weapon.System.Session.Physics.CastRay(predictedMuzzlePos, testLine.From, out weapon.LastHitInfo, CollisionLayers.DefaultCollisionLayer);
                        
                        if (weapon.LastHitInfo != null && weapon.LastHitInfo.HitEntity == ai.TopEntity)
                            selfHit = true;
                    }
                }
                else selfHit = true;
            }

            return !selfHit && (inRange && canTrack || weapon.Comp.Data.Repo.Values.State.TrackingReticle);
        }

        internal static bool CanShootTargetObb(Weapon weapon, MyEntity entity, Vector3D targetLinVel, Vector3D targetAccel, out Vector3D targetPos)
        {
            var prediction = weapon.System.Values.HardPoint.AimLeadingPrediction;
            var trackingWeapon = weapon.TurretMode ? weapon : weapon.Comp.TrackingWeapon;

            if (Vector3D.IsZero(targetLinVel, 5E-03)) targetLinVel = Vector3.Zero;
            if (Vector3D.IsZero(targetAccel, 5E-03)) targetAccel = Vector3.Zero;

            var box = entity.PositionComp.LocalAABB;
            var obb = new MyOrientedBoundingBoxD(box, entity.PositionComp.WorldMatrixRef);

            var validEstimate = true;
            if (prediction != Prediction.Off && !weapon.ActiveAmmoDef.AmmoDef.Const.IsBeamWeapon && weapon.ActiveAmmoDef.AmmoDef.Const.DesiredProjectileSpeed > 0)
                targetPos = TrajectoryEstimation(weapon, obb.Center, targetLinVel, targetAccel, out validEstimate);
            else
                targetPos = obb.Center;

            obb.Center = targetPos;
            weapon.TargetBox = obb;

            var obbAbsMax = obb.HalfExtent.AbsMax();
            var maxRangeSqr = obbAbsMax + weapon.MaxTargetDistance;
            var minRangeSqr = obbAbsMax + weapon.MinTargetDistance;

            maxRangeSqr *= maxRangeSqr;
            minRangeSqr *= minRangeSqr;
            double rangeToTarget;
            Vector3D.DistanceSquared(ref targetPos, ref weapon.MyPivotPos, out rangeToTarget);

            bool canTrack = false;
            if (validEstimate && rangeToTarget <= maxRangeSqr && rangeToTarget >= minRangeSqr)
            {
                var targetDir = targetPos - weapon.MyPivotPos;
                if (weapon == trackingWeapon)
                {
                    double checkAzimuth;
                    double checkElevation;

                    MathFuncs.GetRotationAngles(ref targetDir, ref weapon.WeaponConstMatrix, out checkAzimuth, out checkElevation);

                    var azConstraint = Math.Min(weapon.MaxAzToleranceRadians, Math.Max(weapon.MinAzToleranceRadians, checkAzimuth));
                    var elConstraint = Math.Min(weapon.MaxElToleranceRadians, Math.Max(weapon.MinElToleranceRadians, checkElevation));

                    Vector3D constraintVector;
                    Vector3D.CreateFromAzimuthAndElevation(azConstraint, elConstraint, out constraintVector);
                    Vector3D.Rotate(ref constraintVector, ref weapon.WeaponConstMatrix, out constraintVector);

                    var testRay = new RayD(ref weapon.MyPivotPos, ref constraintVector);
                    if (obb.Intersects(ref testRay) != null) canTrack = true;

                    if (weapon.BaseComp.Debug)
                        weapon.LimitLine = new LineD(weapon.MyPivotPos, weapon.MyPivotPos + (constraintVector * weapon.ActiveAmmoDef.AmmoDef.Const.MaxTrajectory));
                }
                else
                    canTrack = MathFuncs.IsDotProductWithinTolerance(ref weapon.MyPivotFwd, ref targetDir, weapon.AimingTolerance);
            }
            return canTrack;
        }

        internal static bool TargetAligned(Weapon weapon, Target target, out Vector3D targetPos)
        {
            Vector3 targetLinVel = Vector3.Zero;
            Vector3 targetAccel = Vector3.Zero;
            Vector3D targetCenter;

            if (weapon.Comp.Data.Repo.Values.State.TrackingReticle)
                targetCenter = weapon.BaseComp.Session.PlayerDummyTargets[weapon.Comp.Data.Repo.Values.State.PlayerId].Position;
            else if (target.IsProjectile)
                targetCenter = target.Projectile?.Position ?? Vector3D.Zero;
            else if (!target.IsFakeTarget)
                targetCenter = target.TargetEntity?.PositionComp.WorldAABB.Center ?? Vector3D.Zero;
            else
                targetCenter = Vector3D.Zero;

            var validEstimate = true;
            if (weapon.System.Prediction != Prediction.Off && (!weapon.ActiveAmmoDef.AmmoDef.Const.IsBeamWeapon && weapon.ActiveAmmoDef.AmmoDef.Const.DesiredProjectileSpeed > 0))
            {

                if (weapon.Comp.Data.Repo.Values.State.TrackingReticle)
                {
                    targetLinVel = weapon.BaseComp.Session.PlayerDummyTargets[weapon.Comp.Data.Repo.Values.State.PlayerId].LinearVelocity;
                    targetAccel = weapon.BaseComp.Session.PlayerDummyTargets[weapon.Comp.Data.Repo.Values.State.PlayerId].Acceleration;
                }
                else
                {
                    var cube = target.TargetEntity as MyCubeBlock;
                    var topMostEnt = cube != null ? cube.CubeGrid : target.TargetEntity;

                    if (target.Projectile != null)
                    {
                        targetLinVel = target.Projectile.Velocity;
                        targetAccel = target.Projectile.AccelVelocity;
                    }
                    else if (topMostEnt?.Physics != null)
                    {
                        targetLinVel = topMostEnt.Physics.LinearVelocity;
                        targetAccel = topMostEnt.Physics.LinearAcceleration;
                    }
                }
                if (Vector3D.IsZero(targetLinVel, 5E-03)) targetLinVel = Vector3.Zero;
                if (Vector3D.IsZero(targetAccel, 5E-03)) targetAccel = Vector3.Zero;
                targetPos = TrajectoryEstimation(weapon, targetCenter, targetLinVel, targetAccel, out validEstimate);
            }
            else
                targetPos = targetCenter;

            var targetDir = targetPos - weapon.MyPivotPos;

            double rangeToTarget;
            Vector3D.DistanceSquared(ref targetPos, ref weapon.MyPivotPos, out rangeToTarget);
            var inRange = rangeToTarget <= weapon.MaxTargetDistanceSqr && rangeToTarget >= weapon.MinTargetDistanceSqr;

            var isAligned = validEstimate && (inRange || weapon.Comp.Data.Repo.Values.State.TrackingReticle) && MathFuncs.IsDotProductWithinTolerance(ref weapon.MyPivotFwd, ref targetDir, weapon.AimingTolerance);

            weapon.Target.TargetPos = targetPos;
            weapon.Target.IsAligned = isAligned;
            return isAligned;
        }

        internal static Vector3D TargetCenter(Weapon weapon)
        {
            var targetCenter = Vector3D.Zero;
            if (weapon.Comp.Data.Repo.Values.State.TrackingReticle)
                targetCenter = weapon.BaseComp.Session.PlayerDummyTargets[weapon.Comp.Data.Repo.Values.State.PlayerId].Position;
            else if (weapon.Target.IsProjectile)
                targetCenter = weapon.Target.Projectile?.Position ?? Vector3D.Zero;
            else if (!weapon.Target.IsFakeTarget)
                targetCenter = weapon.Target.TargetEntity?.PositionComp.WorldAABB.Center ?? Vector3D.Zero;
            return targetCenter;
        }

        internal static bool TrackingTarget(Weapon weapon, Target target, out bool targetLock)
        {
            Vector3D targetPos;
            Vector3 targetLinVel = Vector3.Zero;
            Vector3 targetAccel = Vector3.Zero;
            Vector3D targetCenter;
            targetLock = false;

            if (weapon.Comp.Data.Repo.Values.State.TrackingReticle)
                targetCenter = weapon.BaseComp.Session.PlayerDummyTargets[weapon.Comp.Data.Repo.Values.State.PlayerId].Position;
            else if (target.IsProjectile)
                targetCenter = target.Projectile?.Position ?? Vector3D.Zero;
            else if (!target.IsFakeTarget)
                targetCenter = target.TargetEntity?.PositionComp.WorldAABB.Center ?? Vector3D.Zero;
            else
                targetCenter = Vector3D.Zero;

            var validEstimate = true;
            if (weapon.System.Prediction != Prediction.Off && !weapon.ActiveAmmoDef.AmmoDef.Const.IsBeamWeapon && weapon.ActiveAmmoDef.AmmoDef.Const.DesiredProjectileSpeed > 0) {

                if (weapon.Comp.Data.Repo.Values.State.TrackingReticle) {
                    targetLinVel = weapon.BaseComp.Session.PlayerDummyTargets[weapon.Comp.Data.Repo.Values.State.PlayerId].LinearVelocity;
                    targetAccel = weapon.BaseComp.Session.PlayerDummyTargets[weapon.Comp.Data.Repo.Values.State.PlayerId].Acceleration;
                }
                else {
                    var cube = target.TargetEntity as MyCubeBlock;
                    var topMostEnt = cube != null ? cube.CubeGrid : target.TargetEntity;
                    
                    if (target.Projectile != null) {
                        targetLinVel = target.Projectile.Velocity;
                        targetAccel = target.Projectile.AccelVelocity;
                    }
                    else if (topMostEnt?.Physics != null) {
                        targetLinVel = topMostEnt.Physics.LinearVelocity;
                        targetAccel = topMostEnt.Physics.LinearAcceleration;
                    }
                }
                if (Vector3D.IsZero(targetLinVel, 5E-03)) targetLinVel = Vector3.Zero;
                if (Vector3D.IsZero(targetAccel, 5E-03)) targetAccel = Vector3.Zero;
                targetPos = TrajectoryEstimation(weapon, targetCenter, targetLinVel, targetAccel, out validEstimate);
            }
            else
                targetPos = targetCenter;

            weapon.Target.TargetPos = targetPos;

            double rangeToTargetSqr;
            Vector3D.DistanceSquared(ref targetPos, ref weapon.MyPivotPos, out rangeToTargetSqr);

            var targetDir = targetPos - weapon.MyPivotPos;
            var readyToTrack = validEstimate && !weapon.BaseComp.ResettingSubparts && (weapon.Comp.Data.Repo.Values.State.TrackingReticle || rangeToTargetSqr <= weapon.MaxTargetDistanceSqr && rangeToTargetSqr >= weapon.MinTargetDistanceSqr);
            
            var locked = true;
            var isTracking = false;
            if (readyToTrack && weapon.Comp.Data.Repo.Values.State.Control != ProtoWeaponState.ControlMode.Camera) {

                if (MathFuncs.WeaponLookAt(weapon, ref targetDir, rangeToTargetSqr, true, false, out isTracking)) {

                    weapon.ReturingHome = false;
                    locked = false;
                    weapon.AimBarrel();
                }
            }
            
            weapon.Rotating = !locked;

            if (weapon.Comp.Data.Repo.Values.State.Control == ProtoWeaponState.ControlMode.Camera)
                return isTracking;

            var isAligned = false;

            if (isTracking)
                isAligned = locked || MathFuncs.IsDotProductWithinTolerance(ref weapon.MyPivotFwd, ref targetDir, weapon.AimingTolerance);

            var wasAligned = weapon.Target.IsAligned;
            weapon.Target.IsAligned = isAligned;
            var alignedChange = wasAligned != isAligned;
            if (weapon.System.DesignatorWeapon && weapon.System.Session.IsServer && alignedChange) { 
                for (int i = 0; i < weapon.BaseComp.Platform.Weapons.Count; i++) {
                    var w = weapon.BaseComp.Platform.Weapons[i];

                    if (isAligned && !w.System.DesignatorWeapon)
                        w.Target.Reset(weapon.System.Session.Tick, Target.States.Designator);
                    else if (!isAligned && w.System.DesignatorWeapon)
                        w.Target.Reset(weapon.System.Session.Tick, Target.States.Designator);
                }
            }

            targetLock = isTracking && weapon.Target.IsAligned;

            var rayCheckTest = !weapon.BaseComp.Session.IsClient && targetLock && (weapon.Comp.Data.Repo.Values.State.Control == ProtoWeaponState.ControlMode.None || weapon.Comp.Data.Repo.Values.State.Control == ProtoWeaponState.ControlMode.Ui) && weapon.ActiveAmmoDef.AmmoDef.Trajectory.Guidance != GuidanceType.Smart && (!weapon.Casting && weapon.BaseComp.Session.Tick - weapon.Comp.LastRayCastTick > 29 || weapon.System.Values.HardPoint.Other.MuzzleCheck && weapon.BaseComp.Session.Tick - weapon.LastMuzzleCheck > 29);
            
            if (rayCheckTest && !weapon.RayCheckTest())
                return false;
            
            return isTracking;
        }

        public bool SmartLos()
        {
            LastSmartLosCheck = BaseComp.Ai.Session.Tick;
            IHitInfo hitInfo;

            var trackingCheckPosition = GetScope.Info.Position;
            
            BaseComp.Ai.Session.Physics.CastRay(trackingCheckPosition + (MyPivotFwd * BaseComp.Ai.GridVolume.Radius), trackingCheckPosition, out hitInfo, 15, false);
            var grid = hitInfo?.HitEntity?.GetTopMostParent() as MyCubeGrid;
            if (grid != null && grid.IsSameConstructAs(BaseComp.Ai.GridEntity) && grid.GetTargetedBlock(hitInfo.Position + (-MyPivotFwd * 0.1f)) != BaseComp.Cube.SlimBlock)
            {
                PauseShoot = true;
                return false;
            }

            PauseShoot = false;
            return true;
        }

        internal static Vector3D TrajectoryEstimation(Weapon weapon, Vector3D targetPos, Vector3D targetVel, Vector3D targetAcc, out bool valid)
        {
            valid = true;
            var ai = weapon.BaseComp.Ai;
            var session = ai.Session;
            var ammoDef = weapon.ActiveAmmoDef.AmmoDef;
            if (ai.VelocityUpdateTick != session.Tick) {
                ai.GridVel = ai.TopEntity.Physics?.LinearVelocity ?? Vector3D.Zero;
                ai.IsStatic = ai.TopEntity.Physics?.IsStatic ?? false;
                ai.VelocityUpdateTick = session.Tick;
            }

            if (ammoDef.Const.FeelsGravity && session.Tick - weapon.GravityTick > 119) {
                weapon.GravityTick = session.Tick;
                float interference;
                weapon.GravityPoint = session.Physics.CalculateNaturalGravityAt(weapon.MyPivotPos, out interference);
            }

            var gravityMultiplier = ammoDef.Const.FeelsGravity && !MyUtils.IsZero(weapon.GravityPoint) ? ammoDef.Trajectory.GravityMultiplier : 0f;
            var targetMaxSpeed = weapon.BaseComp.Session.MaxEntitySpeed;
            var shooterPos = weapon.MyPivotPos;

            var shooterVel = (Vector3D)weapon.BaseComp.Ai.GridVel;
            var projectileMaxSpeed = ammoDef.Const.DesiredProjectileSpeed;
            var projectileInitSpeed = ammoDef.Trajectory.AccelPerSec * MyEngineConstants.UPDATE_STEP_SIZE_IN_SECONDS;
            var projectileAccMag = ammoDef.Trajectory.AccelPerSec;
            var gravity = weapon.GravityPoint;
            var basic = weapon.System.Prediction != Prediction.Advanced;
            Vector3D deltaPos = targetPos - shooterPos;
            Vector3D deltaVel = targetVel - shooterVel;
            Vector3D deltaPosNorm;
            if (Vector3D.IsZero(deltaPos)) deltaPosNorm = Vector3D.Zero;
            else if (Vector3D.IsUnit(ref deltaPos)) deltaPosNorm = deltaPos;
            else Vector3D.Normalize(ref deltaPos, out deltaPosNorm);

            double closingSpeed;
            Vector3D.Dot(ref deltaVel, ref deltaPosNorm, out closingSpeed);

            Vector3D closingVel = closingSpeed * deltaPosNorm;
            Vector3D lateralVel = deltaVel - closingVel;
            double projectileMaxSpeedSqr = projectileMaxSpeed * projectileMaxSpeed;
            double ttiDiff = projectileMaxSpeedSqr - lateralVel.LengthSquared();

            if (ttiDiff < 0) {
                valid = false;
                return targetPos;
            }

            double projectileClosingSpeed = Math.Sqrt(ttiDiff) - closingSpeed;

            double closingDistance;
            Vector3D.Dot(ref deltaPos, ref deltaPosNorm, out closingDistance);

            double timeToIntercept = ttiDiff < 0 ? 0 : closingDistance / projectileClosingSpeed;

            if (timeToIntercept < 0) {
                valid = false;
                return targetPos;
            }

            double maxSpeedSqr = targetMaxSpeed * targetMaxSpeed;
            double shooterVelScaleFactor = 1;
            bool projectileAccelerates = projectileAccMag > 1e-6;
            bool hasGravity = gravityMultiplier > 1e-6 && !MyUtils.IsZero(weapon.GravityPoint);

            if (!basic && projectileAccelerates)
                shooterVelScaleFactor = Math.Min(1, (projectileMaxSpeed - projectileInitSpeed) / projectileAccMag);

            Vector3D estimatedImpactPoint = targetPos + timeToIntercept * (targetVel - shooterVel * shooterVelScaleFactor);
            if (basic) return estimatedImpactPoint;
            Vector3D aimDirection = estimatedImpactPoint - shooterPos;

            Vector3D projectileVel = shooterVel;
            Vector3D projectilePos = shooterPos;

            Vector3D aimDirectionNorm;
            if (projectileAccelerates) {
                
                if (Vector3D.IsZero(deltaPos)) aimDirectionNorm = Vector3D.Zero;
                else if (Vector3D.IsUnit(ref deltaPos)) aimDirectionNorm = aimDirection;
                else aimDirectionNorm = Vector3D.Normalize(aimDirection);
                projectileVel += aimDirectionNorm * projectileInitSpeed;
            }
            else {
                
                if (targetAcc.LengthSquared() < 1 && !hasGravity)
                    return estimatedImpactPoint;

                if (Vector3D.IsZero(deltaPos)) aimDirectionNorm = Vector3D.Zero;
                else if (Vector3D.IsUnit(ref deltaPos)) aimDirectionNorm = aimDirection;
                else Vector3D.Normalize(ref aimDirection, out aimDirectionNorm);
                projectileVel += aimDirectionNorm * projectileMaxSpeed;
            }

            var count = projectileAccelerates ? 600 : hasGravity ? 320 : 60;

            double dt = Math.Max(MyEngineConstants.UPDATE_STEP_SIZE_IN_SECONDS, timeToIntercept / count); // This can be a const somewhere
            double dtSqr = dt * dt;
            Vector3D targetAccStep = targetAcc * dt;
            Vector3D projectileAccStep = aimDirectionNorm * projectileAccMag * dt;

            Vector3D aimOffset = Vector3D.Zero;
            double minTime = 0;
            
            for (int i = 0; i < count; ++i) {
                
                targetVel += targetAccStep;

                if (targetVel.LengthSquared() > maxSpeedSqr) {
                    Vector3D targetNormVel;
                    Vector3D.Normalize(ref targetVel, out targetNormVel);
                    targetVel = targetNormVel * targetMaxSpeed;

                }

                targetPos += targetVel * dt;
                if (projectileAccelerates) {
                    
                    projectileVel += projectileAccStep;
                    if (projectileVel.LengthSquared() > projectileMaxSpeedSqr) {
                        Vector3D pNormVel;
                        Vector3D.Normalize(ref projectileVel, out pNormVel);
                        projectileVel = pNormVel * projectileMaxSpeed;
                    }
                }

                projectilePos += projectileVel * dt;
                Vector3D diff = (targetPos - projectilePos);
                double diffLenSq = diff.LengthSquared();
                aimOffset = diff;
                minTime = dt * (i + 1);

                if (diffLenSq < projectileMaxSpeedSqr * dtSqr || Vector3D.Dot(diff, aimDirectionNorm) < 0)
                    break;
            }
            Vector3D perpendicularAimOffset = aimOffset - Vector3D.Dot(aimOffset, aimDirectionNorm) * aimDirectionNorm;
            Vector3D gravityOffset = hasGravity ? -0.5 * minTime * minTime * gravity : Vector3D.Zero;
            return estimatedImpactPoint + perpendicularAimOffset + gravityOffset;
        }

        private bool RayCheckTest()
        {
            var trackingCheckPosition = GetScope.Info.Position;

            if (System.Session.DebugLos)
            {
                var trackPos = BarrelOrigin + (MyPivotFwd * MuzzleDistToBarrelCenter);
                var targetTestPos = Target.TargetEntity.PositionComp.WorldAABB.Center;
                var topEntity = Target.TargetEntity.GetTopMostParent();

                IHitInfo hitInfo;
                if (System.Session.Physics.CastRay(trackPos, targetTestPos, out hitInfo) && hitInfo.HitEntity == topEntity)
                {
                    var hitPos = hitInfo.Position;
                    double closestDist;
                    MyUtils.GetClosestPointOnLine(ref trackingCheckPosition, ref targetTestPos, ref hitPos, out closestDist);
                    var tDir = Vector3D.Normalize(targetTestPos - trackingCheckPosition);
                    var closestPos = trackingCheckPosition + (tDir * closestDist);

                    var missAmount = Vector3D.Distance(hitPos, closestPos);
                    System.Session.Rays++;
                    System.Session.RayMissAmounts += missAmount;
                }
            }
            
            var tick = BaseComp.Session.Tick;
            var masterWeapon = TrackTarget || Comp.TrackingWeapon == null ? this : Comp.TrackingWeapon;
            
            if (System.Values.HardPoint.Other.MuzzleCheck)
            {
                LastMuzzleCheck = tick;
                if (BaseComp.IsBlock && MuzzleHitSelf())
                {
                    masterWeapon.Target.Reset(BaseComp.Session.Tick, Target.States.RayCheckSelfHit, !Comp.Data.Repo.Values.State.TrackingReticle);
                    if (masterWeapon != this) Target.Reset(BaseComp.Session.Tick, Target.States.RayCheckSelfHit, !Comp.Data.Repo.Values.State.TrackingReticle);
                    return false;
                }
                if (tick - Comp.LastRayCastTick <= 29) return true;
            }
            
            if (Target.TargetEntity is IMyCharacter && !Comp.Data.Repo.Values.Set.Overrides.Biologicals || Target.TargetEntity is MyCubeBlock && !Comp.Data.Repo.Values.Set.Overrides.Grids)
            {
                masterWeapon.Target.Reset(BaseComp.Session.Tick, Target.States.RayCheckProjectile);
                if (masterWeapon != this) Target.Reset(BaseComp.Session.Tick, Target.States.RayCheckProjectile);
                return false;
            }

            Comp.LastRayCastTick = tick;

            if (Target.IsFakeTarget)
            {
                Casting = true;
                BaseComp.Session.Physics.CastRayParallel(ref trackingCheckPosition, ref Target.TargetPos, CollisionLayers.DefaultCollisionLayer, ManualShootRayCallBack);
                return true;
            }
            if (Comp.Data.Repo.Values.State.TrackingReticle) return true;


            if (Target.IsProjectile)
            {
                if (!BaseComp.Ai.LiveProjectile.Contains(Target.Projectile))
                {
                    masterWeapon.Target.Reset(BaseComp.Session.Tick, Target.States.RayCheckProjectile);
                    if (masterWeapon != this) Target.Reset(BaseComp.Session.Tick, Target.States.RayCheckProjectile);
                    return false;
                }
            }
            if (!Target.IsProjectile)
            {
                var character = Target.TargetEntity as IMyCharacter;
                if ((Target.TargetEntity == null || Target.TargetEntity.MarkedForClose) || character != null && (character.IsDead || character.Integrity <= 0 || BaseComp.Session.AdminMap.ContainsKey(character)))
                {
                    masterWeapon.Target.Reset(BaseComp.Session.Tick, Target.States.RayCheckOther);
                    if (masterWeapon != this) Target.Reset(BaseComp.Session.Tick, Target.States.RayCheckOther);
                    return false;
                }

                var cube = Target.TargetEntity as MyCubeBlock;
                if (cube != null && !cube.IsWorking && !BaseComp.Ai.Construct.Focus.EntityIsFocused(BaseComp.Ai, cube.CubeGrid))
                {
                    masterWeapon.Target.Reset(BaseComp.Session.Tick, Target.States.RayCheckDeadBlock);
                    if (masterWeapon != this) Target.Reset(BaseComp.Session.Tick, Target.States.RayCheckDeadBlock);
                    FastTargetResetTick = System.Session.Tick;
                    return false;
                }
                var topMostEnt = Target.TargetEntity.GetTopMostParent();
                if (Target.TopEntityId != topMostEnt.EntityId || !BaseComp.Ai.Targets.ContainsKey(topMostEnt))
                {
                    masterWeapon.Target.Reset(BaseComp.Session.Tick, Target.States.RayCheckFailed);
                    if (masterWeapon != this) Target.Reset(BaseComp.Session.Tick, Target.States.RayCheckFailed);
                    return false;
                }
            }
            
            var targetPos = Target.Projectile?.Position ?? Target.TargetEntity.PositionComp.WorldAABB.Center;
            var distToTargetSqr = Vector3D.DistanceSquared(targetPos, trackingCheckPosition);
            if (distToTargetSqr > MaxTargetDistanceSqr && distToTargetSqr < MinTargetDistanceSqr)
            {
                masterWeapon.Target.Reset(BaseComp.Session.Tick, Target.States.RayCheckDistExceeded);
                if (masterWeapon != this) Target.Reset(BaseComp.Session.Tick, Target.States.RayCheckDistExceeded);
                return false;
            }

            Water water = null;
            if (System.Session.WaterApiLoaded && !ActiveAmmoDef.AmmoDef.IgnoreWater && BaseComp.Ai.InPlanetGravity && BaseComp.Ai.MyPlanet != null && System.Session.WaterMap.TryGetValue(BaseComp.Ai.MyPlanet, out water))
            {
                var waterSphere = new BoundingSphereD(BaseComp.Ai.MyPlanet.PositionComp.WorldAABB.Center, water.radius);
                if (waterSphere.Contains(targetPos) != ContainmentType.Disjoint)
                {
                    masterWeapon.Target.Reset(BaseComp.Session.Tick, Target.States.RayCheckFailed);
                    if (masterWeapon != this) Target.Reset(BaseComp.Session.Tick, Target.States.RayCheckFailed);
                    return false;
                }
            }

            Casting = true;

            BaseComp.Session.Physics.CastRayParallel(ref trackingCheckPosition, ref targetPos, CollisionLayers.DefaultCollisionLayer, RayCallBack.NormalShootRayCallBack);
            return true;
        }

        public void ManualShootRayCallBack(IHitInfo hitInfo)
        {
            Casting = false;
            var masterWeapon = TrackTarget ? this : Comp.TrackingWeapon;

            var grid = hitInfo.HitEntity as MyCubeGrid;
            if (grid != null)
            {
                if (BaseComp.IsBlock && grid.IsSameConstructAs(BaseComp.Cube.CubeGrid))
                {
                    masterWeapon.Target.Reset(BaseComp.Session.Tick, Target.States.RayCheckFailed, false);
                    if (masterWeapon != this) Target.Reset(BaseComp.Session.Tick, Target.States.RayCheckFailed, false);
                }
            }
        }

        public bool HitFriendlyShield(Vector3D weaponPos, Vector3D targetPos, Vector3D dir)
        {
            var testRay = new RayD(weaponPos, dir);
            BaseComp.Ai.TestShields.Clear();
            var checkDistanceSqr = Vector3.DistanceSquared(targetPos, weaponPos);

            for (int i = 0; i < BaseComp.Ai.NearByFriendlyShields.Count; i++)
            {
                var shield = BaseComp.Ai.NearByFriendlyShields[i];
                var dist = testRay.Intersects(shield.PositionComp.WorldVolume);
                if (dist != null && dist.Value * dist.Value <= checkDistanceSqr)
                    BaseComp.Ai.TestShields.Add(shield);
            }

            if (BaseComp.Ai.TestShields.Count == 0)
                return false;

            var result = BaseComp.Ai.Session.SApi.IntersectEntToShieldFast(BaseComp.Ai.TestShields, testRay, true, false, BaseComp.Ai.AiOwner, checkDistanceSqr);

            return result.Item1 && result.Item2 > 0;
        }

        public bool MuzzleHitSelf()
        {
            for (int i = 0; i < Muzzles.Length; i++)
            {
                var m = Muzzles[i];
                var grid = BaseComp.Ai.GridEntity;
                var dummy = Dummies[i];
                var newInfo = dummy.Info;
                m.Direction = newInfo.Direction;
                m.Position = newInfo.Position;
                m.LastUpdateTick = BaseComp.Session.Tick;

                var start = m.Position;
                var end = m.Position + (m.Direction * grid.PositionComp.LocalVolume.Radius);

                Vector3D? hit;
                if (GridIntersection.BresenhamGridIntersection(grid, ref start, ref end, out hit, BaseComp.Cube, BaseComp.Ai))
                    return true;
            }
            return false;
        }

        internal void InitTracking()
        {
            RotationSpeed = System.AzStep;
            ElevationSpeed = System.ElStep;
            var minAz = System.MinAzimuth;
            var maxAz = System.MaxAzimuth;
            var minEl = System.MinElevation;
            var maxEl = System.MaxElevation;
            var toleranceRads = MathHelperD.ToRadians(System.Values.HardPoint.AimingTolerance);

            MinElevationRadians = MinElToleranceRadians = MathHelperD.ToRadians(MathFuncs.NormalizeAngle(minEl));
            MaxElevationRadians = MaxElToleranceRadians = MathHelperD.ToRadians(MathFuncs.NormalizeAngle(maxEl));

            MinAzimuthRadians = MinAzToleranceRadians = MathHelperD.ToRadians(MathFuncs.NormalizeAngle(minAz));
            MaxAzimuthRadians = MaxAzToleranceRadians = MathHelperD.ToRadians(MathFuncs.NormalizeAngle(maxAz));

            if (System.TurretMovement == WeaponSystem.TurretType.AzimuthOnly || System.Values.HardPoint.AddToleranceToTracking)
            {
                MinElToleranceRadians -= toleranceRads;
                MaxElToleranceRadians += toleranceRads;
            }
            else if (System.TurretMovement == WeaponSystem.TurretType.ElevationOnly || System.Values.HardPoint.AddToleranceToTracking)
            {
                MinAzToleranceRadians -= toleranceRads;
                MaxAzToleranceRadians += toleranceRads;
            }

            if (MinElToleranceRadians > MaxElToleranceRadians)
                MinElToleranceRadians -= 6.283185f;

            if (MinAzToleranceRadians > MaxAzToleranceRadians)
                MinAzToleranceRadians -= 6.283185f;
            
            var dummyInfo = Dummies[MiddleMuzzleIndex];
            MuzzleDistToBarrelCenter = Vector3D.Distance(dummyInfo.Info.Position, dummyInfo.Entity.PositionComp.WorldAABB.Center);
        }
    }
}