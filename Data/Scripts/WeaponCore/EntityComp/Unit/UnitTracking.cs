using System;
using Jakaria;
using Sandbox.Game.Entities;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;
using WeaponCore.Support;
using static WeaponCore.Support.UnitDefinition.HardPointDef;
using static WeaponCore.Support.UnitDefinition.ConsumableDef.TrajectoryDef;
using CollisionLayers = Sandbox.Engine.Physics.MyPhysics.CollisionLayers;

namespace WeaponCore.Platform
{
    public partial class Unit
    {
        internal static bool CanShootTarget(Unit unit, ref Vector3D targetCenter, Vector3D targetLinVel, Vector3D targetAccel, out Vector3D targetPos, bool checkSelfHit = false, MyEntity target = null)
        {
            var prediction = unit.System.Values.HardPoint.AimLeadingPrediction;
            var trackingWeapon = unit.TurretMode ? unit : unit.Comp.TrackingUnit;
            if (Vector3D.IsZero(targetLinVel, 5E-03)) targetLinVel = Vector3.Zero;
            if (Vector3D.IsZero(targetAccel, 5E-03)) targetAccel = Vector3.Zero;

            var validEstimate = true;
            if (prediction != Prediction.Off && !unit.ActiveAmmoDef.ConsumableDef.Const.IsBeamWeapon && unit.ActiveAmmoDef.ConsumableDef.Const.DesiredProjectileSpeed > 0)
                targetPos = TrajectoryEstimation(unit, targetCenter, targetLinVel, targetAccel, out validEstimate);
            else
                targetPos = targetCenter;
            var targetDir = targetPos - unit.MyPivotPos;

            double rangeToTarget;
            Vector3D.DistanceSquared(ref targetPos, ref unit.MyPivotPos, out rangeToTarget);

            var inRange = rangeToTarget <= unit.MaxTargetDistanceSqr && rangeToTarget >= unit.MinTargetDistanceSqr;

            bool canTrack;
            bool isTracking;

            if (unit == trackingWeapon)
                canTrack = validEstimate && MathFuncs.WeaponLookAt(unit, ref targetDir, rangeToTarget, false, true, out isTracking);
            else
                canTrack = validEstimate && MathFuncs.IsDotProductWithinTolerance(ref unit.MyPivotFwd, ref targetDir, unit.AimingTolerance);

            bool selfHit = false;
            unit.LastHitInfo = null;
            if (checkSelfHit && target != null && unit.Comp.IsBlock) {

                var testLine = new LineD(targetPos, unit.BarrelOrigin);
                var predictedMuzzlePos = testLine.To + (-testLine.Direction * unit.MuzzleDistToBarrelCenter);
                var ai = unit.Comp.Ai;
                var localPredictedPos = Vector3I.Round(Vector3D.Transform(predictedMuzzlePos, ai.TopEntity.PositionComp.WorldMatrixNormalizedInv) * ai.GridEntity.GridSizeR);

                MyCube cube;
                var noCubeAtPosition = !ai.GridEntity.TryGetCube(localPredictedPos, out cube);
                if (noCubeAtPosition || cube.CubeBlock == unit.Comp.Cube.SlimBlock) {

                    var noCubeInLine = !ai.GridEntity.GetIntersectionWithLine(ref testLine, ref ai.GridHitInfo);
                    var noCubesInLineOrHitSelf = noCubeInLine || ai.GridHitInfo.Position == unit.Comp.Cube.Position;

                    if (noCubesInLineOrHitSelf) {

                        unit.System.Session.Physics.CastRay(predictedMuzzlePos, testLine.From, out unit.LastHitInfo, CollisionLayers.DefaultCollisionLayer);
                        
                        if (unit.LastHitInfo != null && unit.LastHitInfo.HitEntity == ai.TopEntity)
                            selfHit = true;
                    }
                }
                else selfHit = true;
            }

            return !selfHit && (inRange && canTrack || unit.Comp.Data.Repo.Base.State.TrackingReticle);
        }

        internal static bool CheckSelfHit(Unit w, ref Vector3D targetPos, ref Vector3D testPos, out Vector3D predictedMuzzlePos)
        {

            var testLine = new LineD(targetPos, testPos);
            predictedMuzzlePos = testLine.To + (-testLine.Direction * w.MuzzleDistToBarrelCenter);
            var ai = w.Comp.Ai;
            var localPredictedPos = Vector3I.Round(Vector3D.Transform(predictedMuzzlePos, ai.TopEntity.PositionComp.WorldMatrixNormalizedInv) * ai.GridEntity.GridSizeR);

            MyCube cube;
            var noCubeAtPosition = !ai.GridEntity.TryGetCube(localPredictedPos, out cube);
            if (noCubeAtPosition || cube.CubeBlock == w.Comp.Cube.SlimBlock) {

                var noCubeInLine = !ai.GridEntity.GetIntersectionWithLine(ref testLine, ref ai.GridHitInfo);
                var noCubesInLineOrHitSelf = noCubeInLine || ai.GridHitInfo.Position == w.Comp.Cube.Position;

                if (noCubesInLineOrHitSelf) {

                    w.System.Session.Physics.CastRay(predictedMuzzlePos, testLine.From, out w.LastHitInfo, CollisionLayers.DefaultCollisionLayer);

                    if (w.LastHitInfo != null && w.LastHitInfo.HitEntity == ai.TopEntity)
                        return true;
                }
            }
            else return true;

            return false;
        }


        internal static bool CanShootTargetObb(Unit unit, MyEntity entity, Vector3D targetLinVel, Vector3D targetAccel, out Vector3D targetPos)
        {
            var prediction = unit.System.Values.HardPoint.AimLeadingPrediction;
            var trackingWeapon = unit.TurretMode ? unit : unit.Comp.TrackingUnit;

            if (Vector3D.IsZero(targetLinVel, 5E-03)) targetLinVel = Vector3.Zero;
            if (Vector3D.IsZero(targetAccel, 5E-03)) targetAccel = Vector3.Zero;

            var box = entity.PositionComp.LocalAABB;
            var obb = new MyOrientedBoundingBoxD(box, entity.PositionComp.WorldMatrixRef);

            var validEstimate = true;
            if (prediction != Prediction.Off && !unit.ActiveAmmoDef.ConsumableDef.Const.IsBeamWeapon && unit.ActiveAmmoDef.ConsumableDef.Const.DesiredProjectileSpeed > 0)
                targetPos = TrajectoryEstimation(unit, obb.Center, targetLinVel, targetAccel, out validEstimate);
            else
                targetPos = obb.Center;

            obb.Center = targetPos;
            unit.TargetBox = obb;

            var obbAbsMax = obb.HalfExtent.AbsMax();
            var maxRangeSqr = obbAbsMax + unit.MaxTargetDistance;
            var minRangeSqr = obbAbsMax + unit.MinTargetDistance;

            maxRangeSqr *= maxRangeSqr;
            minRangeSqr *= minRangeSqr;
            double rangeToTarget;
            Vector3D.DistanceSquared(ref targetPos, ref unit.MyPivotPos, out rangeToTarget);

            bool canTrack = false;
            if (validEstimate && rangeToTarget <= maxRangeSqr && rangeToTarget >= minRangeSqr)
            {
                var targetDir = targetPos - unit.MyPivotPos;
                if (unit == trackingWeapon)
                {
                    double checkAzimuth;
                    double checkElevation;

                    MathFuncs.GetRotationAngles(ref targetDir, ref unit.WeaponConstMatrix, out checkAzimuth, out checkElevation);

                    var azConstraint = Math.Min(unit.MaxAzToleranceRadians, Math.Max(unit.MinAzToleranceRadians, checkAzimuth));
                    var elConstraint = Math.Min(unit.MaxElToleranceRadians, Math.Max(unit.MinElToleranceRadians, checkElevation));

                    Vector3D constraintVector;
                    Vector3D.CreateFromAzimuthAndElevation(azConstraint, elConstraint, out constraintVector);
                    Vector3D.Rotate(ref constraintVector, ref unit.WeaponConstMatrix, out constraintVector);

                    var testRay = new RayD(ref unit.MyPivotPos, ref constraintVector);
                    if (obb.Intersects(ref testRay) != null) canTrack = true;

                    if (unit.Comp.Debug)
                        unit.LimitLine = new LineD(unit.MyPivotPos, unit.MyPivotPos + (constraintVector * unit.ActiveAmmoDef.ConsumableDef.Const.MaxTrajectory));
                }
                else
                    canTrack = MathFuncs.IsDotProductWithinTolerance(ref unit.MyPivotFwd, ref targetDir, unit.AimingTolerance);
            }
            return canTrack;
        }

        internal static bool TargetAligned(Unit unit, Target target, out Vector3D targetPos)
        {
            Vector3 targetLinVel = Vector3.Zero;
            Vector3 targetAccel = Vector3.Zero;
            Vector3D targetCenter;

            if (unit.Comp.Data.Repo.Base.State.TrackingReticle)
                targetCenter = unit.Comp.Session.PlayerDummyTargets[unit.Comp.Data.Repo.Base.State.PlayerId].Position;
            else if (target.IsProjectile)
                targetCenter = target.Projectile?.Position ?? Vector3D.Zero;
            else if (!target.IsFakeTarget)
                targetCenter = target.TargetEntity?.PositionComp.WorldAABB.Center ?? Vector3D.Zero;
            else
                targetCenter = Vector3D.Zero;

            var validEstimate = true;
            if (unit.System.Prediction != Prediction.Off && (!unit.ActiveAmmoDef.ConsumableDef.Const.IsBeamWeapon && unit.ActiveAmmoDef.ConsumableDef.Const.DesiredProjectileSpeed > 0))
            {

                if (unit.Comp.Data.Repo.Base.State.TrackingReticle)
                {
                    targetLinVel = unit.Comp.Session.PlayerDummyTargets[unit.Comp.Data.Repo.Base.State.PlayerId].LinearVelocity;
                    targetAccel = unit.Comp.Session.PlayerDummyTargets[unit.Comp.Data.Repo.Base.State.PlayerId].Acceleration;
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
                targetPos = TrajectoryEstimation(unit, targetCenter, targetLinVel, targetAccel, out validEstimate);
            }
            else
                targetPos = targetCenter;

            var targetDir = targetPos - unit.MyPivotPos;

            double rangeToTarget;
            Vector3D.DistanceSquared(ref targetPos, ref unit.MyPivotPos, out rangeToTarget);
            var inRange = rangeToTarget <= unit.MaxTargetDistanceSqr && rangeToTarget >= unit.MinTargetDistanceSqr;

            var isAligned = validEstimate && (inRange || unit.Comp.Data.Repo.Base.State.TrackingReticle) && MathFuncs.IsDotProductWithinTolerance(ref unit.MyPivotFwd, ref targetDir, unit.AimingTolerance);

            unit.Target.TargetPos = targetPos;
            unit.Target.IsAligned = isAligned;
            return isAligned;
        }

        internal static Vector3D TargetCenter(Unit unit)
        {
            var targetCenter = Vector3D.Zero;
            if (unit.Comp.Data.Repo.Base.State.TrackingReticle)
                targetCenter = unit.Comp.Session.PlayerDummyTargets[unit.Comp.Data.Repo.Base.State.PlayerId].Position;
            else if (unit.Target.IsProjectile)
                targetCenter = unit.Target.Projectile?.Position ?? Vector3D.Zero;
            else if (!unit.Target.IsFakeTarget)
                targetCenter = unit.Target.TargetEntity?.PositionComp.WorldAABB.Center ?? Vector3D.Zero;
            return targetCenter;
        }

        internal static bool TrackingTarget(Unit unit, Target target, out bool targetLock)
        {
            Vector3D targetPos;
            Vector3 targetLinVel = Vector3.Zero;
            Vector3 targetAccel = Vector3.Zero;
            Vector3D targetCenter;
            targetLock = false;

            if (unit.Comp.Data.Repo.Base.State.TrackingReticle)
                targetCenter = unit.Comp.Session.PlayerDummyTargets[unit.Comp.Data.Repo.Base.State.PlayerId].Position;
            else if (target.IsProjectile)
                targetCenter = target.Projectile?.Position ?? Vector3D.Zero;
            else if (!target.IsFakeTarget)
                targetCenter = target.TargetEntity?.PositionComp.WorldAABB.Center ?? Vector3D.Zero;
            else
                targetCenter = Vector3D.Zero;

            var validEstimate = true;
            if (unit.System.Prediction != Prediction.Off && !unit.ActiveAmmoDef.ConsumableDef.Const.IsBeamWeapon && unit.ActiveAmmoDef.ConsumableDef.Const.DesiredProjectileSpeed > 0) {

                if (unit.Comp.Data.Repo.Base.State.TrackingReticle) {
                    targetLinVel = unit.Comp.Session.PlayerDummyTargets[unit.Comp.Data.Repo.Base.State.PlayerId].LinearVelocity;
                    targetAccel = unit.Comp.Session.PlayerDummyTargets[unit.Comp.Data.Repo.Base.State.PlayerId].Acceleration;
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
                targetPos = TrajectoryEstimation(unit, targetCenter, targetLinVel, targetAccel, out validEstimate);
            }
            else
                targetPos = targetCenter;

            unit.Target.TargetPos = targetPos;

            double rangeToTargetSqr;
            Vector3D.DistanceSquared(ref targetPos, ref unit.MyPivotPos, out rangeToTargetSqr);

            var targetDir = targetPos - unit.MyPivotPos;
            var readyToTrack = validEstimate && !unit.Comp.ResettingSubparts && (unit.Comp.Data.Repo.Base.State.TrackingReticle || rangeToTargetSqr <= unit.MaxTargetDistanceSqr && rangeToTargetSqr >= unit.MinTargetDistanceSqr);
            
            var locked = true;
            var isTracking = false;
            if (readyToTrack && unit.Comp.Data.Repo.Base.State.Control != CompStateValues.ControlMode.Camera) {

                if (MathFuncs.WeaponLookAt(unit, ref targetDir, rangeToTargetSqr, true, false, out isTracking)) {

                    unit.ReturingHome = false;
                    locked = false;
                    unit.AimBarrel();
                }
            }
            
            unit.Rotating = !locked;

            if (unit.Comp.Data.Repo.Base.State.Control == CompStateValues.ControlMode.Camera)
                return isTracking;

            var isAligned = false;

            if (isTracking)
                isAligned = locked || MathFuncs.IsDotProductWithinTolerance(ref unit.MyPivotFwd, ref targetDir, unit.AimingTolerance);

            var wasAligned = unit.Target.IsAligned;
            unit.Target.IsAligned = isAligned;
            var alignedChange = wasAligned != isAligned;
            if (unit.System.DesignatorWeapon && unit.System.Session.IsServer && alignedChange) { 
                for (int i = 0; i < unit.Comp.Platform.Weapons.Length; i++) {
                    var w = unit.Comp.Platform.Weapons[i];
                    if (isAligned && !w.System.DesignatorWeapon)
                        w.Target.Reset(unit.System.Session.Tick, Target.States.Designator);
                    else if (!isAligned && w.System.DesignatorWeapon)
                        w.Target.Reset(unit.System.Session.Tick, Target.States.Designator);
                }
            }

            targetLock = isTracking && unit.Target.IsAligned;

            var rayCheckTest = !unit.Comp.Session.IsClient && targetLock && (unit.Comp.Data.Repo.Base.State.Control == CompStateValues.ControlMode.None || unit.Comp.Data.Repo.Base.State.Control == CompStateValues.ControlMode.Ui) && unit.ActiveAmmoDef.ConsumableDef.Trajectory.Guidance != GuidanceType.Smart && (!unit.Casting && unit.Comp.Session.Tick - unit.Comp.LastRayCastTick > 29 || unit.System.Values.HardPoint.Other.MuzzleCheck && unit.Comp.Session.Tick - unit.LastMuzzleCheck > 29);
            
            if (rayCheckTest && !unit.RayCheckTest())
                return false;
            
            return isTracking;
        }

        public bool SmartLos()
        {
            LastSmartLosCheck = Comp.Ai.Session.Tick;
            IHitInfo hitInfo;

            var trackingCheckPosition = GetScope.Info.Position;
            
            Comp.Ai.Session.Physics.CastRay(trackingCheckPosition + (MyPivotFwd * Comp.Ai.GridVolume.Radius), trackingCheckPosition, out hitInfo, 15, false);
            var grid = hitInfo?.HitEntity?.GetTopMostParent() as MyCubeGrid;
            if (grid != null && grid.IsSameConstructAs(Comp.Ai.GridEntity) && grid.GetTargetedBlock(hitInfo.Position + (-MyPivotFwd * 0.1f)) != Comp.Cube.SlimBlock)
            {
                PauseShoot = true;
                return false;
            }

            PauseShoot = false;
            return true;
        }
        /*
        internal static Vector3D TrajectoryEstimation(Weapon weapon, Vector3D targetPos, Vector3D targetVel, Vector3D targetAcc, out bool valid, int simIterations = 3)
        {
            valid = true;
            var ai = weapon.Comp.Ai;
            var session = ai.Session;
            var ammoDef = weapon.ActiveAmmoDef.AmmoDef;
            if (ai.VelocityUpdateTick != session.Tick)
            {
                ai.GridVel = ai.MyGrid.Physics?.LinearVelocity ?? Vector3D.Zero;
                ai.IsStatic = ai.MyGrid.Physics?.IsStatic ?? false;
                ai.VelocityUpdateTick = session.Tick;
            }

            if (ammoDef.Const.FeelsGravity && session.Tick - weapon.GravityTick > 119)
            {
                weapon.GravityTick = session.Tick;
                weapon.GravityPoint = MyParticlesManager.CalculateGravityInPoint(weapon.MyPivotPos);
            }

            var gravityMultiplier = ammoDef.Const.FeelsGravity && !MyUtils.IsZero(weapon.GravityPoint) ? ammoDef.Trajectory.GravityMultiplier : 0f;
            bool hasGravity = gravityMultiplier > 1e-6;
            var targetMaxSpeed = weapon.Comp.Session.MaxEntitySpeed;
            var shooterPos = weapon.MyPivotPos;

            var shooterVel = (Vector3D)weapon.Comp.Ai.GridVel;
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

            if (ttiDiff < 0)
            {
                valid = false;
                return targetPos;
            }

            double projectileClosingSpeed = Math.Sqrt(ttiDiff) - closingSpeed;

            double closingDistance;
            Vector3D.Dot(ref deltaPos, ref deltaPosNorm, out closingDistance);

            double timeToIntercept = ttiDiff < 0 ? 0 : closingDistance / projectileClosingSpeed;

            if (timeToIntercept < 0)
            {
                valid = false;
                return targetPos;
            }

            double maxSpeedSqr = targetMaxSpeed * targetMaxSpeed;
            double shooterVelScaleFactor = 1;
            bool projectileAccelerates = projectileAccMag > 1e-6;

            if (!basic && projectileAccelerates)
                shooterVelScaleFactor = Math.Min(1, (projectileMaxSpeed - projectileInitSpeed) / projectileAccMag);

            Vector3D estimatedImpactPoint = targetPos + timeToIntercept * (targetVel - shooterVel * shooterVelScaleFactor);
            if (basic) return estimatedImpactPoint;

            Vector3D aimPos = estimatedImpactPoint;
            double closestTime = timeToIntercept;
            double offsetWeight = 0.5; // This makes the estimates converge. Should be < 1 and > 0
            for (int i = 0; i < simIterations; ++i)
            {
                // First iteration will use our first guess, later iterations will use the guess of the previous iterations
                SimulateTrajectories(i, aimPos, closestTime, projectileInitSpeed, projectileMaxSpeed, projectileAccMag, gravity * gravityMultiplier, shooterPos, shooterVel, targetPos, targetVel, targetAcc, deltaPos, maxSpeedSqr, targetMaxSpeed, hasGravity, projectileAccelerates, out aimPos, out closestTime, offsetWeight);
            }
            return aimPos;
        }

        static void SimulateTrajectories(int iterator, Vector3D estimatedImpactPoint, double timeToIntercept, double projectileInitSpeed, double projectileMaxSpeed, double projectileAccMag, Vector3D gravity, Vector3D shooterPos, Vector3D shooterVel, Vector3D targetPos, Vector3D targetVel, Vector3D targetAcc, Vector3D deltaPos, double maxSpeedSqr, double targetMaxSpeed, bool hasGravity, bool projectileAccelerates, out Vector3D aimPoint, out double closestTime, double offsetWeight = 0.5)
        {
            Vector3D aimDirection = estimatedImpactPoint - shooterPos;

            Vector3D projectileVel = shooterVel;
            Vector3D projectilePos = shooterPos;

            Vector3D aimDirectionNorm;
            if (Vector3D.IsZero(deltaPos)) aimDirectionNorm = Vector3D.Zero;
            else if (Vector3D.IsUnit(ref deltaPos)) aimDirectionNorm = aimDirection;
            else Vector3D.Normalize(ref aimDirection, out aimDirectionNorm);
            if (projectileAccelerates)
            {
                projectileVel += aimDirectionNorm * projectileInitSpeed;
            }
            else
            {
                if (targetAcc.LengthSquared() < 1 && !hasGravity)
                {
                    closestTime = timeToIntercept;
                    aimPoint = estimatedImpactPoint;
                    return;
                }
                projectileVel += aimDirectionNorm * projectileMaxSpeed;
            }

            var count = projectileAccelerates ? 6200 : 20; // Divided by 3 since we default will do 3 full sims

            double dt = Math.Max(MyEngineConstants.UPDATE_STEP_SIZE_IN_SECONDS, timeToIntercept / count); // This can be a const somewhere
            double dtSqr = dt * dt;
            Vector3D targetAccStep = targetAcc * dt;
            Vector3D projectileAccStep = aimDirectionNorm * projectileAccMag * dt;
            Vector3D gravityStep = gravity * dt;
            Vector3D aimOffset = Vector3D.Zero;
            double minTime = 0;

            for (int i = 0; i < count; ++i)
            {
                // Update target
                targetVel += targetAccStep;
                if (targetVel.LengthSquared() > maxSpeedSqr)
                {
                    Vector3D targetNormVel;
                    Vector3D.Normalize(ref targetVel, out targetNormVel);
                    targetVel = targetNormVel * targetMaxSpeed;

                }
                targetPos += targetVel * dt;

                // Update projectile
                if (hasGravity)
                    projectileVel += gravityStep;
                if (projectileAccelerates)
                {

                    projectileVel += projectileAccStep;
                    if (projectileVel.LengthSquared() > (projectileMaxSpeed * projectileMaxSpeed))
                    {
                        Vector3D pNormVel;
                        Vector3D.Normalize(ref projectileVel, out pNormVel);
                        projectileVel = pNormVel * projectileMaxSpeed;
                    }
                }
                projectilePos += projectileVel * dt;

                // Check for end condition
                Vector3D diff = (targetPos - projectilePos);
                double diffLenSq = diff.LengthSquared();
                aimOffset = diff;
                minTime = dt * (i + 1);
                if (diffLenSq < (projectileMaxSpeed * projectileMaxSpeed) * dtSqr || Vector3D.Dot(diff, aimDirectionNorm) < 0)
                    break;
            }
            Vector3D perpendicularAimOffset = aimOffset - Vector3D.Dot(aimOffset, aimDirectionNorm) * aimDirectionNorm;
            aimPoint = estimatedImpactPoint + perpendicularAimOffset * offsetWeight;
            closestTime = minTime;
            //Log.CleanLine($"simId:{iterator} - ct:{closestTime} - pOffset:{perpendicularAimOffset} - aimPos:{aimPoint} - sPos:{shooterPos} - tPos:{targetPos} - g:{gravity}");
        }
        */
        internal static Vector3D TrajectoryEstimation(Unit unit, Vector3D targetPos, Vector3D targetVel, Vector3D targetAcc, out bool valid)
        {
            valid = true;
            var ai = unit.Comp.Ai;
            var session = ai.Session;
            var ammoDef = unit.ActiveAmmoDef.ConsumableDef;
            if (ai.VelocityUpdateTick != session.Tick) {
                ai.GridVel = ai.TopEntity.Physics?.LinearVelocity ?? Vector3D.Zero;
                ai.IsStatic = ai.TopEntity.Physics?.IsStatic ?? false;
                ai.VelocityUpdateTick = session.Tick;
            }

            if (ammoDef.Const.FeelsGravity && session.Tick - unit.GravityTick > 119) {
                unit.GravityTick = session.Tick;
                float interference;
                unit.GravityPoint = session.Physics.CalculateNaturalGravityAt(unit.MyPivotPos, out interference);
            }

            var gravityMultiplier = ammoDef.Const.FeelsGravity && !MyUtils.IsZero(unit.GravityPoint) ? ammoDef.Trajectory.GravityMultiplier : 0f;
            var targetMaxSpeed = unit.Comp.Session.MaxEntitySpeed;
            var shooterPos = unit.MyPivotPos;

            var shooterVel = (Vector3D)unit.Comp.Ai.GridVel;
            var projectileMaxSpeed = ammoDef.Const.DesiredProjectileSpeed;
            var projectileInitSpeed = ammoDef.Trajectory.AccelPerSec * MyEngineConstants.UPDATE_STEP_SIZE_IN_SECONDS;
            var projectileAccMag = ammoDef.Trajectory.AccelPerSec;
            var gravity = unit.GravityPoint;
            var basic = unit.System.Prediction != Prediction.Advanced;
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
            bool hasGravity = gravityMultiplier > 1e-6 && !MyUtils.IsZero(unit.GravityPoint);

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
        
        internal static Vector3D Old1TrajectoryEstimation(Unit unit, Vector3D targetPos, Vector3D targetVel, Vector3D targetAcc, out bool valid)
        {
            valid = true;
            var ai = unit.Comp.Ai;
            var session = ai.Session;
            var ammoDef = unit.ActiveAmmoDef.ConsumableDef;
            if (ai.VelocityUpdateTick != session.Tick)
            {
                ai.GridVel = ai.TopEntity.Physics?.LinearVelocity ?? Vector3D.Zero;
                ai.IsStatic = ai.TopEntity.Physics?.IsStatic ?? false;
                ai.VelocityUpdateTick = session.Tick;
            }

            if (ammoDef.Const.FeelsGravity && session.Tick - unit.GravityTick > 119)
            {
                unit.GravityTick = session.Tick;
                float interference;
                unit.GravityPoint = session.Physics.CalculateNaturalGravityAt(unit.MyPivotPos, out interference);
            }

            var gravityMultiplier = ammoDef.Const.FeelsGravity && !MyUtils.IsZero(unit.GravityPoint) ? ammoDef.Trajectory.GravityMultiplier : 0f;
            var targetMaxSpeed = unit.Comp.Session.MaxEntitySpeed;
            var shooterPos = unit.MyPivotPos;
            
            var shooterVel = (Vector3D)unit.Comp.Ai.GridVel;
            var projectileMaxSpeed = ammoDef.Const.DesiredProjectileSpeed;
            var projectileInitSpeed = ammoDef.Trajectory.AccelPerSec * MyEngineConstants.UPDATE_STEP_SIZE_IN_SECONDS;
            var projectileAccMag = ammoDef.Trajectory.AccelPerSec;
            var gravity = unit.GravityPoint;
            var basic = unit.System.Prediction != Prediction.Advanced;
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
            bool hasGravity = gravityMultiplier > 1e-6;
            
            if (!basic && projectileAccelerates)
                shooterVelScaleFactor = Math.Min(1, (projectileMaxSpeed - projectileInitSpeed) / projectileAccMag);

            Vector3D estimatedImpactPoint = targetPos + timeToIntercept * (targetVel - shooterVel * shooterVelScaleFactor);
            if (basic) return estimatedImpactPoint;
            Vector3D aimDirection = estimatedImpactPoint - shooterPos;

            Vector3D projectileVel = shooterVel;
            Vector3D projectilePos = shooterPos;

            Vector3D aimDirectionNorm;
            if (projectileAccelerates)
            {
                if (Vector3D.IsZero(deltaPos)) aimDirectionNorm = Vector3D.Zero;
                else if (Vector3D.IsUnit(ref deltaPos)) aimDirectionNorm = aimDirection;
                else aimDirectionNorm = Vector3D.Normalize(aimDirection);
                projectileVel += aimDirectionNorm * projectileInitSpeed;
            }
            else
            {
                if (targetAcc.LengthSquared() < 1 && !hasGravity)
                    return estimatedImpactPoint;

                if (Vector3D.IsZero(deltaPos)) aimDirectionNorm = Vector3D.Zero;
                else if (Vector3D.IsUnit(ref deltaPos)) aimDirectionNorm = aimDirection;
                else Vector3D.Normalize(ref aimDirection, out aimDirectionNorm);
                projectileVel += aimDirectionNorm * projectileMaxSpeed;
            }

            var count = projectileAccelerates ? 600 : 60;

            double dt = Math.Max(MyEngineConstants.UPDATE_STEP_SIZE_IN_SECONDS, timeToIntercept / count); // This can be a const somewhere
            double dtSqr = dt * dt;
            Vector3D targetAccStep = targetAcc * dt;
            Vector3D projectileAccStep = aimDirectionNorm * projectileAccMag * dt;
            Vector3D gravityStep = gravity * gravityMultiplier * dt;
            Vector3D aimOffset = Vector3D.Zero;
            double minDiff = double.MaxValue;
            for (int i = 0; i < count; ++i)
            {
                targetVel += targetAccStep;

                if (targetVel.LengthSquared() > maxSpeedSqr)
                {
                    Vector3D targetNormVel;
                    Vector3D.Normalize(ref targetVel, out targetNormVel);
                    targetVel = targetNormVel * targetMaxSpeed;

                }

                targetPos += targetVel * dt;
                if (projectileAccelerates)
                {
                    projectileVel += projectileAccStep;
                    if (projectileVel.LengthSquared() > projectileMaxSpeedSqr)
                    {
                        Vector3D pNormVel;
                        Vector3D.Normalize(ref projectileVel, out pNormVel);
                        projectileVel = pNormVel * projectileMaxSpeed;
                    }
                }
                /*
                if (hasGravity)
                    projectileVel += gravityStep;

                projectilePos += projectileVel * dt;
                Vector3D diff = (targetPos - projectilePos);
                double diffLenSq = diff.LengthSquared();
                if (diffLenSq < projectileMaxSpeedSqr * dtSqr)
                {
                    aimOffset = diff;
                    break;
                }
                if (diffLenSq < minDiff)
                {
                    minDiff = diffLenSq;
                    aimOffset = diff;
                }
                */
                if (hasGravity)
                    projectileVel += gravityStep;

                projectilePos += projectileVel * dt;
                Vector3D diff = (targetPos - projectilePos);
                double diffLenSq = diff.LengthSquared();
                aimOffset = diff;

                if (diffLenSq < projectileMaxSpeedSqr * dtSqr || Vector3D.Dot(diff, aimDirectionNorm) < 0)
                    break;
            }
            Vector3D perpendicularAimOffset = aimOffset - Vector3D.Dot(aimOffset, aimDirectionNorm) * aimDirectionNorm;
            return estimatedImpactPoint + perpendicularAimOffset;
        }

        public Vector3D GetPredictedTargetPositionOld(Vector3D targetPos, Vector3 targetLinVel, Vector3D targetAccel)
        {
            if (Comp.Ai.VelocityUpdateTick != Comp.Session.Tick)
            {
                Comp.Ai.GridVel = Comp.Ai.TopEntity.Physics?.LinearVelocity ?? Vector3D.Zero;
                Comp.Ai.IsStatic = Comp.Ai.TopEntity.Physics?.IsStatic ?? false;
                Comp.Ai.VelocityUpdateTick = Comp.Session.Tick;
            }

            if (ActiveAmmoDef.ConsumableDef.Const.FeelsGravity && Comp.Ai.Session.Tick - GravityTick > 119)
            {
                GravityTick = Comp.Ai.Session.Tick;
                float interference;
                GravityPoint = System.Session.Physics.CalculateNaturalGravityAt(MyPivotPos, out interference);
            }
            var gravityMultiplier = ActiveAmmoDef.ConsumableDef.Const.FeelsGravity && !MyUtils.IsZero(GravityPoint) ? ActiveAmmoDef.ConsumableDef.Trajectory.GravityMultiplier : 0f;

            var predictedPos = Old2TrajectoryEstimation(targetPos, targetLinVel, targetAccel, Comp.Session.MaxEntitySpeed, MyPivotPos, Comp.Ai.GridVel, ActiveAmmoDef.ConsumableDef.Const.DesiredProjectileSpeed, ActiveAmmoDef.ConsumableDef.Trajectory.AccelPerSec * MyEngineConstants.UPDATE_STEP_SIZE_IN_SECONDS, ActiveAmmoDef.ConsumableDef.Trajectory.AccelPerSec, gravityMultiplier, GravityPoint, System.Prediction != Prediction.Advanced);

            return predictedPos;
        }


        /*
        ** Whip's advanced Projectile Intercept 
        */
        internal static Vector3D Old2TrajectoryEstimation(Vector3D targetPos, Vector3D targetVel, Vector3D targetAcc, double targetMaxSpeed, Vector3D shooterPos, Vector3D shooterVel, double projectileMaxSpeed, double projectileInitSpeed = 0, double projectileAccMag = 0, double gravityMultiplier = 0, Vector3D gravity = default(Vector3D), bool basic = false)
        {
            Vector3D deltaPos = targetPos - shooterPos;
            Vector3D deltaVel = targetVel - shooterVel;

            Vector3D deltaPosNorm;
            if (Vector3D.IsZero(deltaPos)) deltaPosNorm = Vector3D.Zero;
            else if (Vector3D.IsUnit(ref deltaPos)) deltaPosNorm = deltaPos;
            else deltaPosNorm = Vector3D.Normalize(deltaPos);

            double closingSpeed = Vector3D.Dot(deltaVel, deltaPosNorm);
            Vector3D closingVel = closingSpeed * deltaPosNorm;
            Vector3D lateralVel = deltaVel - closingVel;
            double projectileMaxSpeedSqr = projectileMaxSpeed * projectileMaxSpeed;
            double ttiDiff = projectileMaxSpeedSqr - lateralVel.LengthSquared();
            double projectileClosingSpeed = Math.Sqrt(ttiDiff) - closingSpeed;
            double closingDistance = Vector3D.Dot(deltaPos, deltaPosNorm);
            double timeToIntercept = ttiDiff < 0 ? 0 : closingDistance / projectileClosingSpeed;
            double maxSpeedSqr = targetMaxSpeed * targetMaxSpeed;
            double shooterVelScaleFactor = 1;
            bool projectileAccelerates = projectileAccMag > 1e-6;
            bool hasGravity = gravityMultiplier > 1e-6;
            if (projectileAccelerates)
            {
                /*
                This is a rough estimate to smooth out our initial guess based upon the missile parameters.
                The reasoning is that the longer it takes to reach max velocity, the more the initial velocity
                has an overall impact on the estimated impact point.
                */
                shooterVelScaleFactor = Math.Min(1, (projectileMaxSpeed - projectileInitSpeed) / projectileAccMag);
            }
            /*
            Estimate our predicted impact point and aim direction
            */
            Vector3D estimatedImpactPoint = targetPos + timeToIntercept * (targetVel - shooterVel * shooterVelScaleFactor);
            if (basic) return estimatedImpactPoint;

            Vector3D aimDirection = estimatedImpactPoint - shooterPos;

            Vector3D projectileVel = shooterVel;
            Vector3D projectilePos = shooterPos;

            Vector3D aimDirectionNorm;
            if (projectileAccelerates)
            {
                if (Vector3D.IsZero(deltaPos)) aimDirectionNorm = Vector3D.Zero;
                else if (Vector3D.IsUnit(ref deltaPos)) aimDirectionNorm = aimDirection;
                else aimDirectionNorm = Vector3D.Normalize(aimDirection);
                projectileVel += aimDirectionNorm * projectileInitSpeed;
            }
            else
            {
                if (targetAcc.LengthSquared() < 1 && !hasGravity)
                    return estimatedImpactPoint;

                if (Vector3D.IsZero(deltaPos)) aimDirectionNorm = Vector3D.Zero;
                else if (Vector3D.IsUnit(ref deltaPos)) aimDirectionNorm = aimDirection;
                else aimDirectionNorm = Vector3D.Normalize(aimDirection);
                projectileVel += aimDirectionNorm * projectileMaxSpeed;
            }

            var count = projectileAccelerates ? 600 : 60;

            double dt = Math.Max(MyEngineConstants.UPDATE_STEP_SIZE_IN_SECONDS, timeToIntercept / count); // This can be a const somewhere
            double dtSqr = dt * dt;
            Vector3D targetAccStep = targetAcc * dt;
            Vector3D projectileAccStep = aimDirectionNorm * projectileAccMag * dt;
            Vector3D gravityStep = gravity * gravityMultiplier * dt;
            Vector3D aimOffset = Vector3D.Zero;
            for (int i = 0; i < count; ++i)
            {
                targetVel += targetAccStep;

                if (targetVel.LengthSquared() > maxSpeedSqr)
                    targetVel = Vector3D.Normalize(targetVel) * targetMaxSpeed;

                targetPos += targetVel * dt;
                if (projectileAccelerates)
                {
                    projectileVel += projectileAccStep;
                    if (projectileVel.LengthSquared() > projectileMaxSpeedSqr)
                    {
                        projectileVel = Vector3D.Normalize(projectileVel) * projectileMaxSpeed;
                    }
                }

                if (hasGravity)
                    projectileVel += gravityStep;

                projectilePos += projectileVel * dt;
                Vector3D diff = (targetPos - projectilePos);
                double diffLenSq = diff.LengthSquared();
                aimOffset = diff;

                if (diffLenSq < projectileMaxSpeedSqr * dtSqr || Vector3D.Dot(diff, aimDirectionNorm) < 0)
                    break;
            }
            Vector3D perpendicularAimOffset = aimOffset - Vector3D.Dot(aimOffset, aimDirectionNorm) * aimDirectionNorm;
            return estimatedImpactPoint + perpendicularAimOffset;
        }

        /*
        ** Whip's Projectile Intercept - Modified for DarkStar 06.15.2019
        */
        //Vector3D _lastTargetVelocity1 = Vector3D.Zero;
        public static Vector3D CalculateProjectileInterceptPoint(
            double gridMaxSpeed,        /* Maximum grid speed           (m/s)   */
            double projectileSpeed,     /* Maximum projectile speed     (m/s)   */
            Vector3D shooterVelocity,   /* Shooter initial velocity     (m/s)   */
            Vector3D shooterPosition,   /* Shooter initial position     (m)     */
            Vector3D targetVelocity,    /* Target initial velocity      (m/s)   */
            Vector3D targetAccel,       /* Target Accel velocity        (m/s/s) */
            Vector3D targetPosition    /* Target initial position      (m)     */)
        {
            Vector3D deltaPos = targetPosition - shooterPosition;
            Vector3D deltaVel = targetVelocity - shooterVelocity;
            double a = Vector3D.Dot(deltaVel, deltaVel) - projectileSpeed * projectileSpeed;
            double b = 2 * Vector3D.Dot(deltaVel, deltaPos);
            double c = Vector3D.Dot(deltaPos, deltaPos);
            double d = b * b - 4 * a * c;
            if (d < 0)
                return targetPosition;

            double sqrtD = Math.Sqrt(d);
            double t1 = 2 * c / (-b + sqrtD);
            double t2 = 2 * c / (-b - sqrtD);
            double tmin = Math.Min(t1, t2);
            double tmax = Math.Max(t1, t2);
            if (t1 < 0 && t2 < 0)
                return targetPosition;

            var timeToIntercept = tmin > 0 ? tmin : tmax;

            Vector3D interceptEst = targetPosition + targetVelocity * timeToIntercept;
            /*
            ** Target trajectory estimation
            */
            const double dt = MyEngineConstants.UPDATE_STEP_SIZE_IN_SECONDS;

            double simtime = 0;
            double maxSpeedSq = gridMaxSpeed * gridMaxSpeed;
            Vector3D tgtPosSim = targetPosition;
            Vector3D tgtVelSim = deltaVel;
            Vector3D tgtAccStep = targetAccel * dt;
            var simCondition = timeToIntercept < 1200 ? timeToIntercept : 1200;

            while (simtime < simCondition)
            {
                simtime += dt;
                tgtVelSim += tgtAccStep;
                if (tgtVelSim.LengthSquared() > maxSpeedSq)
                    tgtVelSim = Vector3D.Normalize(tgtVelSim) * gridMaxSpeed;

                tgtPosSim += tgtVelSim * dt;
            }

            /*
            ** Applying correction
            */
            return tgtPosSim;
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
            
            var tick = Comp.Session.Tick;
            var masterWeapon = TrackTarget || Comp.TrackingUnit == null ? this : Comp.TrackingUnit;
            
            if (System.Values.HardPoint.Other.MuzzleCheck)
            {
                LastMuzzleCheck = tick;
                if (Comp.IsBlock && MuzzleHitSelf())
                {
                    masterWeapon.Target.Reset(Comp.Session.Tick, Target.States.RayCheckSelfHit, !Comp.Data.Repo.Base.State.TrackingReticle);
                    if (masterWeapon != this) Target.Reset(Comp.Session.Tick, Target.States.RayCheckSelfHit, !Comp.Data.Repo.Base.State.TrackingReticle);
                    return false;
                }
                if (tick - Comp.LastRayCastTick <= 29) return true;
            }
            
            if (Target.TargetEntity is IMyCharacter && !Comp.Data.Repo.Base.Set.Overrides.Biologicals || Target.TargetEntity is MyCubeBlock && !Comp.Data.Repo.Base.Set.Overrides.Grids)
            {
                masterWeapon.Target.Reset(Comp.Session.Tick, Target.States.RayCheckProjectile);
                if (masterWeapon != this) Target.Reset(Comp.Session.Tick, Target.States.RayCheckProjectile);
                return false;
            }

            Comp.LastRayCastTick = tick;

            if (Target.IsFakeTarget)
            {
                Casting = true;
                Comp.Session.Physics.CastRayParallel(ref trackingCheckPosition, ref Target.TargetPos, CollisionLayers.DefaultCollisionLayer, ManualShootRayCallBack);
                return true;
            }
            if (Comp.Data.Repo.Base.State.TrackingReticle) return true;


            if (Target.IsProjectile)
            {
                if (!Comp.Ai.LiveProjectile.Contains(Target.Projectile))
                {
                    masterWeapon.Target.Reset(Comp.Session.Tick, Target.States.RayCheckProjectile);
                    if (masterWeapon != this) Target.Reset(Comp.Session.Tick, Target.States.RayCheckProjectile);
                    return false;
                }
            }
            if (!Target.IsProjectile)
            {
                var character = Target.TargetEntity as IMyCharacter;
                if ((Target.TargetEntity == null || Target.TargetEntity.MarkedForClose) || character != null && (character.IsDead || character.Integrity <= 0 || Comp.Session.AdminMap.ContainsKey(character)))
                {
                    masterWeapon.Target.Reset(Comp.Session.Tick, Target.States.RayCheckOther);
                    if (masterWeapon != this) Target.Reset(Comp.Session.Tick, Target.States.RayCheckOther);
                    return false;
                }

                var cube = Target.TargetEntity as MyCubeBlock;
                if (cube != null && !cube.IsWorking && !Comp.Ai.Construct.Focus.EntityIsFocused(Comp.Ai, cube.CubeGrid))
                {
                    masterWeapon.Target.Reset(Comp.Session.Tick, Target.States.RayCheckDeadBlock);
                    if (masterWeapon != this) Target.Reset(Comp.Session.Tick, Target.States.RayCheckDeadBlock);
                    FastTargetResetTick = System.Session.Tick;
                    return false;
                }
                var topMostEnt = Target.TargetEntity.GetTopMostParent();
                if (Target.TopEntityId != topMostEnt.EntityId || !Comp.Ai.Targets.ContainsKey(topMostEnt))
                {
                    masterWeapon.Target.Reset(Comp.Session.Tick, Target.States.RayCheckFailed);
                    if (masterWeapon != this) Target.Reset(Comp.Session.Tick, Target.States.RayCheckFailed);
                    return false;
                }
            }
            
            var targetPos = Target.Projectile?.Position ?? Target.TargetEntity.PositionComp.WorldMatrixRef.Translation;
            var distToTargetSqr = Vector3D.DistanceSquared(targetPos, trackingCheckPosition);
            if (distToTargetSqr > MaxTargetDistanceSqr && distToTargetSqr < MinTargetDistanceSqr)
            {
                masterWeapon.Target.Reset(Comp.Session.Tick, Target.States.RayCheckDistExceeded);
                if (masterWeapon != this) Target.Reset(Comp.Session.Tick, Target.States.RayCheckDistExceeded);
                return false;
            }

            Water water = null;
            if (System.Session.WaterApiLoaded && !ActiveAmmoDef.ConsumableDef.IgnoreWater && Comp.Ai.InPlanetGravity && Comp.Ai.MyPlanet != null && System.Session.WaterMap.TryGetValue(Comp.Ai.MyPlanet, out water))
            {
                var waterSphere = new BoundingSphereD(Comp.Ai.MyPlanet.PositionComp.WorldAABB.Center, water.radius);
                if (waterSphere.Contains(targetPos) != ContainmentType.Disjoint)
                {
                    masterWeapon.Target.Reset(Comp.Session.Tick, Target.States.RayCheckFailed);
                    if (masterWeapon != this) Target.Reset(Comp.Session.Tick, Target.States.RayCheckFailed);
                    return false;
                }
            }

            Casting = true;

            Comp.Session.Physics.CastRayParallel(ref trackingCheckPosition, ref targetPos, CollisionLayers.DefaultCollisionLayer, RayCallBack.NormalShootRayCallBack);
            return true;
        }

        public void ManualShootRayCallBack(IHitInfo hitInfo)
        {
            Casting = false;
            var masterWeapon = TrackTarget ? this : Comp.TrackingUnit;

            var grid = hitInfo.HitEntity as MyCubeGrid;
            if (grid != null)
            {
                if (Comp.IsBlock && grid.IsSameConstructAs(Comp.Cube.CubeGrid))
                {
                    masterWeapon.Target.Reset(Comp.Session.Tick, Target.States.RayCheckFailed, false);
                    if (masterWeapon != this) Target.Reset(Comp.Session.Tick, Target.States.RayCheckFailed, false);
                }
            }
        }

        public bool HitFriendlyShield(Vector3D weaponPos, Vector3D targetPos, Vector3D dir)
        {
            var testRay = new RayD(weaponPos, dir);
            Comp.Ai.TestShields.Clear();
            var checkDistanceSqr = Vector3.DistanceSquared(targetPos, weaponPos);

            for (int i = 0; i < Comp.Ai.NearByFriendlyShields.Count; i++)
            {
                var shield = Comp.Ai.NearByFriendlyShields[i];
                var dist = testRay.Intersects(shield.PositionComp.WorldVolume);
                if (dist != null && dist.Value * dist.Value <= checkDistanceSqr)
                    Comp.Ai.TestShields.Add(shield);
            }

            if (Comp.Ai.TestShields.Count == 0)
                return false;

            var result = Comp.Ai.Session.SApi.IntersectEntToShieldFast(Comp.Ai.TestShields, testRay, true, false, Comp.Ai.AiOwner, checkDistanceSqr);

            return result.Item1 && result.Item2 > 0;
        }

        public bool MuzzleHitSelf()
        {
            for (int i = 0; i < Muzzles.Length; i++)
            {
                var m = Muzzles[i];
                var grid = Comp.Ai.GridEntity;
                var dummy = Dummies[i];
                var newInfo = dummy.Info;
                m.Direction = newInfo.Direction;
                m.Position = newInfo.Position;
                m.LastUpdateTick = Comp.Session.Tick;

                var start = m.Position;
                var end = m.Position + (m.Direction * grid.PositionComp.LocalVolume.Radius);

                Vector3D? hit;
                if (GridIntersection.BresenhamGridIntersection(grid, ref start, ref end, out hit, Comp.Cube, Comp.Ai))
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

            if (System.TurretMovement == CoreSystem.TurretType.AzimuthOnly || System.Values.HardPoint.AddToleranceToTracking)
            {
                MinElToleranceRadians -= toleranceRads;
                MaxElToleranceRadians += toleranceRads;
            }
            else if (System.TurretMovement == CoreSystem.TurretType.ElevationOnly || System.Values.HardPoint.AddToleranceToTracking)
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