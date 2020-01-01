using System;
using Sandbox.ModAPI.Ingame;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Utils;
using VRageMath;
using WeaponCore.Support;
using static WeaponCore.Support.HardPointDefinition;

namespace WeaponCore.Platform
{
    public partial class Weapon
    {
        internal static bool CanShootTarget(Weapon weapon, Vector3D targetCenter, Vector3D targetLinVel, Vector3D targetAccel)
        {
            var prediction = weapon.System.Values.HardPoint.AimLeadingPrediction;
            var trackingWeapon = weapon.TurretMode ? weapon : weapon.Comp.TrackingWeapon;
            Vector3D targetPos;
            if (Vector3D.IsZero(targetLinVel, 5E-03)) targetLinVel = Vector3.Zero;
            if (Vector3D.IsZero(targetAccel, 5E-03)) targetAccel = Vector3.Zero;

            if (prediction != Prediction.Off && !weapon.System.IsBeamWeapon && weapon.System.DesiredProjectileSpeed > 0)
                targetPos = weapon.GetPredictedTargetPosition(targetCenter, targetLinVel, targetAccel);
            else
                targetPos = targetCenter;
            var targetDir = targetPos - weapon.MyPivotPos;

            double rangeToTarget;
            Vector3D.DistanceSquared(ref targetPos, ref weapon.MyPivotPos, out rangeToTarget);

            var inRange = rangeToTarget <= weapon.System.MaxTrajectorySqr;

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

                //tolerance is needed for az or el limited turrets as they may not be able to hit dead center
                var tolerance = weapon.System.LimitedAxisTurret ? weapon.AimCone.ConeAngle : 0;
                var azConstraint = Math.Min(weapon.MaxAzimuthRadians + tolerance, Math.Max(weapon.MinAzimuthRadians - tolerance, desiredAzimuth));
                var elConstraint = Math.Min(weapon.MaxElevationRadians + tolerance, Math.Max(weapon.MinElevationRadians - tolerance, desiredElevation));
                var azConstrained = Math.Abs(elConstraint - desiredElevation) > 0.0000001;
                var elConstrained = Math.Abs(azConstraint - desiredAzimuth) > 0.0000001;
                canTrack = !azConstrained && !elConstrained;
            }
            else
                canTrack = MathFuncs.IsDotProductWithinTolerance(ref weapon.MyPivotDir, ref targetDir, weapon.AimingTolerance);

            var tracking = inRange && canTrack;
            return tracking;
        }

        internal static bool CanShootTargetObb(Weapon weapon, MyEntity entity, Vector3D targetLinVel, Vector3D targetAccel)
        {
            var prediction = weapon.System.Values.HardPoint.AimLeadingPrediction;
            var trackingWeapon = weapon.TurretMode ? weapon : weapon.Comp.TrackingWeapon;
            Vector3D targetPos;
            if (Vector3D.IsZero(targetLinVel, 5E-03)) targetLinVel = Vector3.Zero;
            if (Vector3D.IsZero(targetAccel, 5E-03)) targetAccel = Vector3.Zero;

            var rotMatrix = Quaternion.CreateFromRotationMatrix(entity.PositionComp.WorldMatrix);
            var obb = new MyOrientedBoundingBoxD(entity.PositionComp.WorldAABB.Center, entity.PositionComp.LocalAABB.HalfExtents, rotMatrix);

            if (prediction != Prediction.Off && !weapon.System.IsBeamWeapon && weapon.System.DesiredProjectileSpeed > 0)
                targetPos = weapon.GetPredictedTargetPosition(obb.Center, targetLinVel, targetAccel);
            else
                targetPos = obb.Center;

            obb.Center = targetPos;
            weapon.TargetBox = obb;

            double rangeToTarget;
            Vector3D.DistanceSquared(ref targetPos, ref weapon.MyPivotPos, out rangeToTarget);

            bool canTrack = false;
            if (rangeToTarget <= weapon.System.MaxTrajectorySqr)
            {
                var targetDir = targetPos - weapon.MyPivotPos;
                if (weapon == trackingWeapon)
                {
                    double checkAzimuth;
                    double checkElevation;

                    MathFuncs.GetRotationAngles(ref targetDir, ref weapon.WeaponConstMatrix, out checkAzimuth, out checkElevation);

                    var azConstraint = Math.Min(weapon.MaxAzimuthRadians, Math.Max(weapon.MinAzimuthRadians, checkAzimuth));
                    var elConstraint = Math.Min(weapon.MaxElevationRadians, Math.Max(weapon.MinElevationRadians, checkElevation));

                    Vector3D constraintVector;
                    Vector3D.CreateFromAzimuthAndElevation(azConstraint, elConstraint, out constraintVector);
                    constraintVector = Vector3D.Rotate(constraintVector, weapon.WeaponConstMatrix);

                    var testRay = new RayD(weapon.MyPivotPos, constraintVector);
                    if (obb.Intersects(ref testRay) != null) canTrack = true;

                    if (weapon.Comp.Debug)
                        weapon.LimitLine = new LineD(weapon.MyPivotPos, weapon.MyPivotPos + (constraintVector * weapon.System.MaxTrajectory));
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

            var targetCenter = target.Projectile?.Position ?? target.Entity.PositionComp.WorldAABB.Center;

            if (weapon.System.NeedsPrediction)
            {
                var topMostEnt = target.Entity?.GetTopMostParent();
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
                if (Vector3D.IsZero(targetLinVel, 5E-03)) targetLinVel = Vector3.Zero;
                if (Vector3D.IsZero(targetAccel, 5E-03)) targetAccel = Vector3.Zero;
                targetPos = weapon.GetPredictedTargetPosition(targetCenter, targetLinVel, targetAccel);
            }
            else
                targetPos = targetCenter;

            var targetDir = targetPos - weapon.MyPivotPos;

            double rangeToTarget;
            Vector3D.DistanceSquared(ref targetPos, ref weapon.MyPivotPos, out rangeToTarget);
            var inRange = rangeToTarget <= weapon.System.MaxTrajectorySqr;

            var isAligned = inRange && MathFuncs.IsDotProductWithinTolerance(ref weapon.MyPivotDir, ref targetDir, weapon.AimingTolerance);

            weapon.TargetPos = targetPos;
            weapon.IsAligned = isAligned;
            return isAligned;
        }

        internal static bool TrackingTarget(Weapon weapon, Target target, bool step = false)
        {
            Vector3D targetPos;
            Vector3 targetLinVel = Vector3.Zero;
            Vector3 targetAccel = Vector3.Zero;
            var system = weapon.System;
            var targetCenter = target.Projectile?.Position ?? target.Entity.PositionComp.WorldAABB.Center;

            if (weapon.System.NeedsPrediction)
            {
                var topMostEnt = target.Entity?.GetTopMostParent();
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
                if (Vector3D.IsZero(targetLinVel, 5E-03)) targetLinVel = Vector3.Zero;
                if (Vector3D.IsZero(targetAccel, 5E-03)) targetAccel = Vector3.Zero;
                targetPos = weapon.GetPredictedTargetPosition(targetCenter, targetLinVel, targetAccel);
            }
            else
                targetPos = targetCenter;

            weapon.TargetPos = targetPos;

            double rangeToTarget;
            Vector3D.DistanceSquared(ref targetPos, ref weapon.MyPivotPos, out rangeToTarget);
            var targetDir = targetPos - weapon.MyPivotPos;

            if (rangeToTarget <= system.MaxTrajectorySqr)
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
                var constraintMatrix = new MatrixD {Forward = forward, Left = left, Up = up,};

                double desiredAzimuth;
                double desiredElevation;
                MathFuncs.GetRotationAngles(ref targetDir, ref constraintMatrix, out desiredAzimuth, out desiredElevation);

                //tolerance is needed for az or el limited turrets as they may not be able to hit dead center
                var tolerance = system.LimitedAxisTurret ? weapon.AimCone.ConeAngle : 0;
                var azConstraint = Math.Min(weapon.MaxAzimuthRadians + tolerance, Math.Max(weapon.MinAzimuthRadians - tolerance, desiredAzimuth));
                var elConstraint = Math.Min(weapon.MaxElevationRadians + tolerance, Math.Max(weapon.MinElevationRadians - tolerance, desiredElevation));
                var elConstrained = Math.Abs(elConstraint - desiredElevation) > 0.0000001;
                var azConstrained = Math.Abs(azConstraint - desiredAzimuth) > 0.0000001;
                weapon.IsTracking = !azConstrained && !elConstrained;

                if (weapon.IsTracking && step)
                {
                    var oldAz = weapon.Azimuth;
                    var oldEl = weapon.Elevation;
                    weapon.Azimuth += MathHelperD.Clamp(desiredAzimuth, -maxAzimuthStep, maxAzimuthStep);
                    weapon.Elevation += MathHelperD.Clamp(desiredElevation - weapon.Elevation, -maxElevationStep, maxElevationStep);
                    var azDiff = oldAz - weapon.Azimuth;
                    var elDiff = oldEl - weapon.Elevation;
                    var azLocked = azDiff > -1E-07d && azDiff < 1E-07d;
                    var elLocked = elDiff > -1E-07d && elDiff < 1E-07d;
                    var aim = !azLocked || !elLocked;
                    if (aim)
                        weapon.AimBarrel(azDiff, elDiff);
                }
            }
            else weapon.IsTracking = false;

            if (!step) return weapon.IsTracking;

            var isAligned = false;

            if (weapon.IsTracking)
                isAligned = MathFuncs.IsDotProductWithinTolerance(ref weapon.MyPivotDir, ref targetDir, weapon.AimingTolerance);

            var wasAligned = weapon.IsAligned;
            weapon.IsAligned = isAligned;

            var alignedChange = wasAligned != isAligned;
            if (alignedChange && isAligned)
            {
                if (weapon.System.DesignatorWeapon)
                {
                    for (int i = 0; i < weapon.Comp.Platform.Weapons.Length; i++)
                    {
                        var w = weapon.Comp.Platform.Weapons[i];

                        if (w.Target.State != Target.Targets.Acquired && w != weapon)
                        {
                            w.WakeTargets();
                            GridAi.AcquireTarget(w, false, weapon.Target.Entity.GetTopMostParent());
                        }
                    }
                }
            }
            else if (alignedChange && !weapon.DelayCeaseFire)
                weapon.StopShooting();

            weapon.Target.TargetLock = weapon.IsTracking && weapon.IsAligned;
            return weapon.IsTracking;
        }

        public Vector3D GetPredictedTargetPosition(Vector3D targetPos, Vector3 targetLinVel, Vector3D targetAccel)
        {
            if (Comp.Ai.VelocityUpdateTick != Comp.Ai.Session.Tick)
            {
                Comp.Ai.GridVel = Comp.Ai.MyGrid.Physics?.LinearVelocity ?? Vector3D.Zero;
                Comp.Ai.VelocityUpdateTick = Comp.Ai.Session.Tick;
            }
            var predictedPos = TrajectoryEstimation(targetPos, targetLinVel, targetAccel, Comp.Ai.Session.MaxEntitySpeed, MyPivotPos, Comp.Ai.GridVel, System.DesiredProjectileSpeed, 0, System.Values.Ammo.Trajectory.AccelPerSec, 0, Vector3D.Zero, System.Prediction != Prediction.Advanced);
            //DsDebugDraw.DrawSingleVec(predictedPos, 2.5f, Color.Red);
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
                if (targetAcc.LengthSquared() < 1)
                    return estimatedImpactPoint;

                if (Vector3D.IsZero(deltaPos)) aimDirectionNorm = Vector3D.Zero;
                else if (Vector3D.IsUnit(ref deltaPos)) aimDirectionNorm = aimDirection;
                else aimDirectionNorm = Vector3D.Normalize(aimDirection);
                projectileVel += aimDirectionNorm * projectileMaxSpeed;
            }

            double dt = Math.Max(MyEngineConstants.UPDATE_STEP_SIZE_IN_SECONDS, timeToIntercept / 600); // This can be a const somewhere
            double dtSqr = dt * dt;
            Vector3D targetAccStep = targetAcc * dt;
            Vector3D projectileAccStep = aimDirectionNorm * projectileAccMag * dt;
            Vector3D gravityStep = gravity * gravityMultiplier * dt;
            Vector3D aimOffset = Vector3D.Zero; 
            double minDiff = double.MaxValue;
            for (int i = 0; i < 600; ++i)
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

            if (MinElevationRadians > MaxElevationRadians)
                MinElevationRadians -= 6.283185f;
            MinAzimuthRadians = MathHelperD.ToRadians(MathFuncs.NormalizeAngle(minAz));
            MaxAzimuthRadians = MathHelperD.ToRadians(MathFuncs.NormalizeAngle(maxAz));
            if (MinAzimuthRadians > MaxAzimuthRadians)
                MinAzimuthRadians -= 6.283185f;
        }
    }
}
