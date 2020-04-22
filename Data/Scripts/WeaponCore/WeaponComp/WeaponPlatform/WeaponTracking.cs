using System;
using Sandbox.Game.Entities;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;
using WeaponCore.Support;
using static WeaponCore.Support.WeaponDefinition.HardPointDef;
using static WeaponCore.Support.WeaponDefinition.AmmoDef.TrajectoryDef;

namespace WeaponCore.Platform
{
    public partial class Weapon
    {
        internal static bool CanShootTarget(Weapon weapon, Vector3D targetCenter, Vector3D targetLinVel, Vector3D targetAccel, out Vector3D targetPos)
        {
            var prediction = weapon.System.Values.HardPoint.AimLeadingPrediction;
            var trackingWeapon = weapon.TurretMode ? weapon : weapon.Comp.TrackingWeapon;
            if (Vector3D.IsZero(targetLinVel, 5E-03)) targetLinVel = Vector3.Zero;
            if (Vector3D.IsZero(targetAccel, 5E-03)) targetAccel = Vector3.Zero;

            if (prediction != Prediction.Off && !weapon.ActiveAmmoDef.AmmoDef.Const.IsBeamWeapon && weapon.ActiveAmmoDef.AmmoDef.Const.DesiredProjectileSpeed > 0)
                targetPos = weapon.GetPredictedTargetPosition(targetCenter, targetLinVel, targetAccel);
            else
                targetPos = targetCenter;
            var targetDir = targetPos - weapon.MyPivotPos;

            double rangeToTarget;
            Vector3D.DistanceSquared(ref targetPos, ref weapon.MyPivotPos, out rangeToTarget);

            var inRange = rangeToTarget <= weapon.MaxTargetDistanceSqr && rangeToTarget >= weapon.MinTargetDistanceSqr;

            bool canTrack;

            if (weapon == trackingWeapon)
            {
                Vector3D currentVector;
                Vector3D.CreateFromAzimuthAndElevation(weapon.Azimuth, weapon.Elevation, out currentVector);
                currentVector = Vector3D.Rotate(currentVector, weapon.WeaponConstMatrix);

                var up = weapon.MyPivotUp;
                var left = Vector3D.Cross(up, currentVector);
                if (!Vector3D.IsUnit(ref left) && !Vector3D.IsZero(left))
                    left.Normalize();
                var forward = Vector3D.Cross(left, up);

                var matrix = new MatrixD { Forward = forward, Left = left, Up = up, };

                double desiredAzimuth;
                double desiredElevation;
                MathFuncs.GetRotationAngles(ref targetDir, ref matrix, out desiredAzimuth, out desiredElevation);

                var azConstraint = Math.Min(weapon.MaxAzToleranceRadians, Math.Max(weapon.MinAzToleranceRadians, desiredAzimuth));
                var elConstraint = Math.Min(weapon.MaxElToleranceRadians, Math.Max(weapon.MinElToleranceRadians, desiredElevation));
                var azConstrained = Math.Abs(elConstraint - desiredElevation) > 0.0000001;
                var elConstrained = Math.Abs(azConstraint - desiredAzimuth) > 0.0000001;
                canTrack = !azConstrained && !elConstrained;
            }
            else
                canTrack = MathFuncs.IsDotProductWithinTolerance(ref weapon.MyPivotDir, ref targetDir, weapon.AimingTolerance);

            return (inRange && canTrack) || weapon.Comp.TrackReticle;
        }

        internal static bool CanShootTargetObb(Weapon weapon, MyEntity entity, Vector3D targetLinVel, Vector3D targetAccel)
        {
            var prediction = weapon.System.Values.HardPoint.AimLeadingPrediction;
            var trackingWeapon = weapon.TurretMode ? weapon : weapon.Comp.TrackingWeapon;
            Vector3D targetPos;
            if (Vector3D.IsZero(targetLinVel, 5E-03)) targetLinVel = Vector3.Zero;
            if (Vector3D.IsZero(targetAccel, 5E-03)) targetAccel = Vector3.Zero;

            var rotMatrix = Quaternion.CreateFromRotationMatrix(entity.PositionComp.WorldMatrixRef);
            var obb = new MyOrientedBoundingBoxD(entity.PositionComp.WorldAABB.Center, entity.PositionComp.LocalAABB.HalfExtents, rotMatrix);

            if (prediction != Prediction.Off && !weapon.ActiveAmmoDef.AmmoDef.Const.IsBeamWeapon && weapon.ActiveAmmoDef.AmmoDef.Const.DesiredProjectileSpeed > 0)
                targetPos = weapon.GetPredictedTargetPosition(obb.Center, targetLinVel, targetAccel);
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
            if (rangeToTarget <= maxRangeSqr && rangeToTarget >= minRangeSqr)
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
                    constraintVector = Vector3D.Rotate(constraintVector, weapon.WeaponConstMatrix);

                    var testRay = new RayD(weapon.MyPivotPos, constraintVector);
                    if (obb.Intersects(ref testRay) != null) canTrack = true;

                    if (weapon.Comp.Debug)
                        weapon.LimitLine = new LineD(weapon.MyPivotPos, weapon.MyPivotPos + (constraintVector * weapon.ActiveAmmoDef.AmmoDef.Const.MaxTrajectory));
                }
                else
                    canTrack = MathFuncs.IsDotProductWithinTolerance(ref weapon.MyPivotDir, ref targetDir, weapon.AimingTolerance);
            }
            return canTrack;
        }

        internal static bool TargetAligned(Weapon weapon, Target target, out Vector3D targetPos)
        {
            Vector3 targetLinVel = Vector3.Zero;
            Vector3 targetAccel = Vector3.Zero;
            Vector3D targetCenter;

            if (weapon.Comp.TrackReticle)
                targetCenter = weapon.Comp.Ai.DummyTarget.Position;
            else if (target.IsProjectile)
                targetCenter = target.Projectile?.Position ?? Vector3D.Zero;
            else if (!target.IsFakeTarget)
                targetCenter = target.Entity?.PositionComp.WorldAABB.Center ?? Vector3D.Zero;
            else
                targetCenter = Vector3D.Zero;

            if (weapon.System.Prediction != Prediction.Off && (!weapon.ActiveAmmoDef.AmmoDef.Const.IsBeamWeapon && weapon.ActiveAmmoDef.AmmoDef.Const.DesiredProjectileSpeed > 0))
            {

                if (weapon.Comp.TrackReticle)
                {
                    targetLinVel = weapon.Comp.Ai.DummyTarget.LinearVelocity;
                    targetAccel = weapon.Comp.Ai.DummyTarget.Acceleration;
                }
                else
                {
                    var cube = target.Entity as MyCubeBlock;
                    var topMostEnt = cube != null ? cube.CubeGrid : target.Entity;

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
                targetPos = weapon.GetPredictedTargetPosition(targetCenter, targetLinVel, targetAccel);
            }
            else
                targetPos = targetCenter;

            var targetDir = targetPos - weapon.MyPivotPos;

            double rangeToTarget;
            Vector3D.DistanceSquared(ref targetPos, ref weapon.MyPivotPos, out rangeToTarget);
            var inRange = rangeToTarget <= weapon.MaxTargetDistanceSqr && rangeToTarget >= weapon.MinTargetDistanceSqr;

            var isAligned = (inRange || weapon.Comp.TrackReticle) && MathFuncs.IsDotProductWithinTolerance(ref weapon.MyPivotDir, ref targetDir, weapon.AimingTolerance);

            weapon.Target.TargetPos = targetPos;
            weapon.Target.IsAligned = isAligned;
            return isAligned;
        }

        internal static bool TrackingTarget(Weapon weapon, Target target)
        {
            Vector3D targetPos;
            Vector3 targetLinVel = Vector3.Zero;
            Vector3 targetAccel = Vector3.Zero;
            var system = weapon.System;
            Vector3D targetCenter;

            var rayCheckTest = !weapon.Comp.Session.IsClient && (weapon.Comp.State.Value.CurrentPlayerControl.ControlType == ControlType.None || weapon.Comp.State.Value.CurrentPlayerControl.ControlType == ControlType.Ui) && weapon.ActiveAmmoDef.AmmoDef.Trajectory.Guidance == GuidanceType.None && (!weapon.Casting && weapon.Comp.Session.Tick - weapon.Comp.LastRayCastTick > 29 || weapon.System.Values.HardPoint.Other.MuzzleCheck && weapon.Comp.Session.Tick - weapon.LastMuzzleCheck > 29);
            if (rayCheckTest && !weapon.RayCheckTest())
                return false;

            if (weapon.Comp.TrackReticle)
                targetCenter = weapon.Comp.Ai.DummyTarget.Position;
            else if (target.IsProjectile)
                targetCenter = target.Projectile?.Position ?? Vector3D.Zero;
            else if (!target.IsFakeTarget)
                targetCenter = target.Entity?.PositionComp.WorldAABB.Center ?? Vector3D.Zero;
            else
                targetCenter = Vector3D.Zero;
            
            if (weapon.System.Prediction != Prediction.Off && (!weapon.ActiveAmmoDef.AmmoDef.Const.IsBeamWeapon && weapon.ActiveAmmoDef.AmmoDef.Const.DesiredProjectileSpeed > 0)) {

                if (weapon.Comp.TrackReticle) {
                    targetLinVel = weapon.Comp.Ai.DummyTarget.LinearVelocity;
                    targetAccel = weapon.Comp.Ai.DummyTarget.Acceleration;
                }
                else {
                    var cube = target.Entity as MyCubeBlock;
                    var topMostEnt = cube != null ? cube.CubeGrid : target.Entity;
                    
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
                targetPos = weapon.GetPredictedTargetPosition(targetCenter, targetLinVel, targetAccel);
            }
            else
                targetPos = targetCenter;

            weapon.Target.TargetPos = targetPos;

            double rangeToTargetSqr;
            Vector3D.DistanceSquared(ref targetPos, ref weapon.MyPivotPos, out rangeToTargetSqr);
            var targetDir = targetPos - weapon.MyPivotPos;
            var locked = true;
            if (weapon.Comp.TrackReticle || rangeToTargetSqr <= weapon.MaxTargetDistanceSqr && rangeToTargetSqr >= weapon.MinTargetDistanceSqr)
            {
                var maxAzimuthStep = system.AzStep;
                var maxElevationStep = system.ElStep;

                Vector3D currentVector;
                Vector3D.CreateFromAzimuthAndElevation(weapon.Azimuth, weapon.Elevation, out currentVector);
                currentVector = Vector3D.Rotate(currentVector, weapon.WeaponConstMatrix);

                var up = weapon.MyPivotUp;
                var left = Vector3D.Cross(up, currentVector);
                if (!Vector3D.IsUnit(ref left) && !Vector3D.IsZero(left)) left.Normalize();
                var forward = Vector3D.Cross(left, up);
                var constraintMatrix = new MatrixD { Forward = forward, Left = left, Up = up, };

                double desiredAzimuth;
                double desiredElevation;
                MathFuncs.GetRotationAngles(ref targetDir, ref constraintMatrix, out desiredAzimuth, out desiredElevation);

                var desiredAzAbs = desiredAzimuth + weapon.Azimuth;
                var desiredElAbs = desiredElevation + weapon.Elevation;

                var azConstraint = MathHelper.Clamp(desiredAzAbs, weapon.MinAzToleranceRadians, weapon.MaxAzToleranceRadians);
                var elConstraint = MathHelper.Clamp(desiredElAbs, weapon.MinElToleranceRadians, weapon.MaxAzToleranceRadians);
                var elConstrained = Math.Abs(elConstraint - desiredElAbs) > 0.0000001;
                var azConstrained = Math.Abs(azConstraint - desiredAzAbs) > 0.0000001;
                weapon.Target.IsTracking = !azConstrained && !elConstrained;

                if (weapon.Target.IsTracking && weapon.Comp.State.Value.CurrentPlayerControl.ControlType != ControlType.Camera && !weapon.Comp.ResettingSubparts)
                {
                    var epsilon = target.IsProjectile ? 1E-06d : rangeToTargetSqr <= 640000 ? 1E-03d : rangeToTargetSqr <= 3240000 ? 1E-04d : 1E-05d;
                    var az = MathHelperD.Clamp(weapon.Azimuth + MathHelperD.Clamp(desiredAzimuth, -maxAzimuthStep, maxAzimuthStep), weapon.MinAzimuthRadians, weapon.MaxAzimuthRadians);

                    var el = MathHelperD.Clamp(weapon.Elevation + MathHelperD.Clamp(desiredElevation - weapon.Elevation, -maxElevationStep, maxElevationStep), weapon.MinElevationRadians, weapon.MaxElevationRadians);

                    var azDiff = weapon.Azimuth - az;
                    var elDiff = weapon.Elevation - el;
                    var azLocked = MyUtils.IsZero(azDiff, (float)epsilon);
                    var elLocked = MyUtils.IsZero(elDiff, (float)epsilon);

                    var aim = !azLocked || !elLocked;
                    locked = !aim;
                    if (aim)
                    {
                        if (!azLocked)
                            weapon.Azimuth = az;
                        if (!elLocked)
                            weapon.Elevation = el;
                        weapon.IsHome = false;
                        weapon.AimBarrel(!azLocked, !elLocked);
                    }
                }
            }
            else weapon.Target.IsTracking = false;

            if (weapon.Comp.State.Value.CurrentPlayerControl.ControlType == ControlType.Camera)
                return weapon.Target.IsTracking;

            var isAligned = false;

            if (weapon.Target.IsTracking)
                isAligned = locked || MathFuncs.IsDotProductWithinTolerance(ref weapon.MyPivotDir, ref targetDir, weapon.AimingTolerance);

            var wasAligned = weapon.Target.IsAligned;
            weapon.Target.IsAligned = isAligned;
            var alignedChange = wasAligned != isAligned;
            if (alignedChange && isAligned)
            {
                if (weapon.System.DesignatorWeapon)
                {
                    for (int i = 0; i < weapon.Comp.Platform.Weapons.Length; i++)
                    {
                        var w = weapon.Comp.Platform.Weapons[i];
                        w.Target.StateChange(false, Target.States.Expired);
                        w.Target.CheckTick -= 240;
                    }
                }
            }
            else if (alignedChange && !weapon.System.DelayCeaseFire)
                weapon.StopShooting();

            weapon.Target.TargetLock = weapon.Target.IsTracking && weapon.Target.IsAligned;
            return weapon.Target.IsTracking;
        }

        public bool SmartLos()
        {
            LastSmartLosCheck = Comp.Ai.Session.Tick;
            IHitInfo hitInfo;
            Comp.Ai.Session.Physics.CastRay(MyPivotPos + (MyPivotDir * Comp.Ai.GridVolume.Radius), MyPivotPos, out hitInfo, 15, false);
            var grid = hitInfo?.HitEntity?.GetTopMostParent() as MyCubeGrid;
            if (grid != null && grid.IsSameConstructAs(Comp.Ai.MyGrid) && grid.GetTargetedBlock(hitInfo.Position + (-MyPivotDir * 0.1f)) != Comp.MyCube.SlimBlock)
            {
                PauseShoot = true;
                return false;
            }

            PauseShoot = false;
            return true;
        }

        public Vector3D GetPredictedTargetPosition(Vector3D targetPos, Vector3 targetLinVel, Vector3D targetAccel)
        {
            if (Comp.Ai.VelocityUpdateTick != Comp.Session.Tick)
            {
                Comp.Ai.GridVel = Comp.Ai.MyGrid.Physics?.LinearVelocity ?? Vector3D.Zero;
                Comp.Ai.IsStatic = Comp.Ai.MyGrid.Physics?.IsStatic ?? false;
                Comp.Ai.VelocityUpdateTick = Comp.Session.Tick;
            }

            var gravityMultiplier = 0f;
            if (ActiveAmmoDef.AmmoDef.Const.FeelsGravity && Comp.Ai.Session.Tick - GravityTick > 119)
            {
                GravityTick = Comp.Ai.Session.Tick;
                gravityMultiplier = ActiveAmmoDef.AmmoDef.Trajectory.GravityMultiplier;
                GravityPoint = MyParticlesManager.CalculateGravityInPoint(MyPivotPos);
            }
            var predictedPos = TrajectoryEstimation(targetPos, targetLinVel, targetAccel, Comp.Session.MaxEntitySpeed, MyPivotPos, Comp.Ai.GridVel, ActiveAmmoDef.AmmoDef.Const.DesiredProjectileSpeed, 0, ActiveAmmoDef.AmmoDef.Trajectory.AccelPerSec, gravityMultiplier, GravityPoint, System.Prediction != Prediction.Advanced);

            return predictedPos;
        }

        /*
        ** Whip's advanced Projectile Intercept 
        */
        private static Vector3D TrajectoryEstimation(Vector3D targetPos, Vector3D targetVel, Vector3D targetAcc, double targetMaxSpeed, Vector3D shooterPos, Vector3D shooterVel, double projectileMaxSpeed, double projectileInitSpeed = 0, double projectileAccMag = 0, double gravityMultiplier = 0, Vector3D gravity = default(Vector3D), bool basic = false)
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
            double minDiff = double.MaxValue;
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
            }
            return estimatedImpactPoint + aimOffset;
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

        internal void InitTracking()
        {
            RotationSpeed = System.AzStep;
            ElevationSpeed = System.ElStep;
            var minAz = System.MinAzimuth;
            var maxAz = System.MaxAzimuth;
            var minEl = System.MinElevation;
            var maxEl = System.MaxElevation;

            MinElevationRadians = MathHelperD.ToRadians(MathFuncs.NormalizeAngle(minEl));
            MaxElevationRadians = MathHelperD.ToRadians(MathFuncs.NormalizeAngle(maxEl));

            MinAzimuthRadians = MathHelperD.ToRadians(MathFuncs.NormalizeAngle(minAz));
            MaxAzimuthRadians = MathHelperD.ToRadians(MathFuncs.NormalizeAngle(maxAz));

            var toleranceRads = MathHelperD.ToRadians(System.Values.HardPoint.AimingTolerance);

            if (MinElevationRadians > MaxElevationRadians)
                MinElevationRadians -= 6.283185f;

            if (MinAzimuthRadians > MaxAzimuthRadians)
                MinAzimuthRadians -= 6.283185f;

            MinAzToleranceRadians = MinAzimuthRadians;
            MaxAzToleranceRadians = MaxAzimuthRadians;

            MinElToleranceRadians = MinElevationRadians;
            MaxElToleranceRadians = MaxElevationRadians;

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

            MinAzToleranceRadians = MathHelper.Clamp(MinAzToleranceRadians, -180, 0);
            MaxAzToleranceRadians = MathHelper.Clamp(MaxAzToleranceRadians, 0, 180);

            MinElToleranceRadians = MathHelper.Clamp(MinElToleranceRadians, -90, 0);
            MaxElToleranceRadians = MathHelper.Clamp(MaxElToleranceRadians, 0, 90);
        }
    }
}