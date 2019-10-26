using System;
using Sandbox.Game.Entities;
using VRage.Game;
using VRage.Game.Entity;
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
            double timeToIntercept;
            double rangeToTarget;
            if (Vector3D.IsZero(targetLinVel, 5E-03)) targetLinVel = Vector3.Zero;
            if (Vector3D.IsZero(targetAccel, 5E-03)) targetAccel = Vector3.Zero;

            if (prediction != Prediction.Off)
                targetPos = weapon.GetPredictedTargetPosition(targetCenter, targetLinVel, targetAccel, prediction, out timeToIntercept);
            else
                targetPos = targetCenter;
            var targetDir = targetPos - weapon.MyPivotPos;

            Vector3D.DistanceSquared(ref targetPos, ref weapon.MyPivotPos, out rangeToTarget);

            var inRange = rangeToTarget <= weapon.System.MaxTrajectorySqr;

            bool canTrack = false;

            if (weapon == trackingWeapon)
            {
                Vector3D currentVector;
                Vector3D.CreateFromAzimuthAndElevation(weapon.Azimuth, weapon.Elevation, out currentVector);
                currentVector = Vector3D.Rotate(currentVector, weapon.Comp.MyCube.WorldMatrix);

                var up = weapon.Comp.MyCube.WorldMatrix.Up;
                var left = Vector3D.Cross(up, currentVector);
                if (!Vector3D.IsUnit(ref left) && !Vector3D.IsZero(left))
                    left.Normalize();
                var forward = Vector3D.Cross(left, up);

                var matrix = new MatrixD { Forward = forward, Left = left, Up = up, };

                double desiredAzimuth;
                double desiredElevation;
                MathFuncs.GetRotationAngles(ref targetDir, ref matrix, out desiredAzimuth, out desiredElevation);

                var azConstraint = Math.Min(weapon.MaxAzimuthRadians, Math.Max(weapon.MinAzimuthRadians, desiredAzimuth));
                var elConstraint = Math.Min(weapon.MaxElevationRadians, Math.Max(weapon.MinElevationRadians, desiredElevation));
                var azConstrained = Math.Abs(elConstraint - desiredElevation) > 0.0000001;
                var elConstrained = Math.Abs(azConstraint - desiredAzimuth) > 0.0000001;
                canTrack = !azConstrained && !elConstrained;
            }
            else
                canTrack = MathFuncs.IsDotProductWithinTolerance(ref weapon.MyPivotDir, ref targetDir, weapon.AimingTolerance);

            var tracking = inRange && canTrack;

            return tracking;
        }

        /*
        internal static bool CanShootTargetObb(Weapon weapon, MyEntity entity, Vector3D targetLinVel, Vector3D targetAccel)
        {
            var prediction = weapon.System.Values.HardPoint.AimLeadingPrediction;
            var trackingWeapon = weapon.TurretMode ? weapon : weapon.Comp.TrackingWeapon;
            Vector3D targetPos;
            double timeToIntercept;
            double rangeToTarget;
            if (Vector3D.IsZero(targetLinVel, 5E-03)) targetLinVel = Vector3.Zero;
            if (Vector3D.IsZero(targetAccel, 5E-03)) targetAccel = Vector3.Zero;

            var rotMatrix = Quaternion.CreateFromRotationMatrix(entity.PositionComp.WorldMatrix);
            var obb = new MyOrientedBoundingBoxD(entity.PositionComp.WorldAABB.Center, entity.PositionComp.LocalAABB.HalfExtents, rotMatrix);

            if (prediction != Prediction.Off)
                targetPos = weapon.GetPredictedTargetPosition(obb.Center, targetLinVel, targetAccel, prediction, out timeToIntercept);
            else
                targetPos = obb.Center;

            obb.Center = targetPos;
            weapon.targetBox = obb;
            Vector3D.DistanceSquared(ref targetPos, ref weapon.MyPivotPos, out rangeToTarget);

            bool canTrack = false;
            if (rangeToTarget <= weapon.System.MaxTrajectorySqr)
            {
                var targetDir = targetPos - weapon.MyPivotPos;
                if (weapon == trackingWeapon)
                {
                    double checkAzimuth;
                    double checkElevation;

                    Vector3D currentVector;
                    Vector3D.CreateFromAzimuthAndElevation(weapon.Azimuth, weapon.Elevation, out currentVector);
                    currentVector = Vector3D.Rotate(currentVector, weapon.Comp.MyCube.WorldMatrix);
                    var up = weapon.Comp.MyCube.WorldMatrix.Up;
                    var left = Vector3D.Cross(up, currentVector);
                    if (!Vector3D.IsUnit(ref left) && !Vector3D.IsZero(left))
                        left.Normalize();
                    var forward = Vector3D.Cross(left, up);
                    var constraintMatrix = new MatrixD { Forward = forward, Left = left, Up = up, };

                    MathFuncs.GetRotationAngles(ref targetDir, ref constraintMatrix, out checkAzimuth, out checkElevation);

                    var azConstraint = Math.Min(weapon.MaxAzimuthRadians, Math.Max(weapon.MinAzimuthRadians, checkAzimuth));
                    var elConstraint = Math.Min(weapon.MaxElevationRadians, Math.Max(weapon.MinElevationRadians, checkElevation));
                    var azConstrained = Math.Abs(elConstraint - checkElevation) > 0.0000001;
                    var elConstrained = Math.Abs(azConstraint - checkAzimuth) > 0.0000001;
                    if (!azConstrained && !elConstrained)
                    {
                        var testRay = new RayD(weapon.MyPivotPos, constraintMatrix.Forward);
                        if (obb.Intersects(ref testRay) != null) canTrack = true;

                        if (weapon.Comp.Debug)
                        {
                            weapon.limitLine = new LineD(weapon.MyPivotPos, weapon.MyPivotPos + (constraintMatrix.Forward * weapon.System.MaxTrajectory));
                            DsDebugDraw.DrawLine(weapon.limitLine, Color.Black, 1);
                        }
                    }
                }
                else
                    canTrack = MathFuncs.IsDotProductWithinTolerance(ref weapon.MyPivotDir, ref targetDir, weapon.AimingTolerance);
            }

            return canTrack;
        }
        */
        internal static bool CanShootTargetObb(Weapon weapon, MyEntity entity, Vector3D targetLinVel, Vector3D targetAccel)
        {
            var prediction = weapon.System.Values.HardPoint.AimLeadingPrediction;
            var trackingWeapon = weapon.TurretMode ? weapon : weapon.Comp.TrackingWeapon;
            Vector3D targetPos;
            double timeToIntercept;
            double rangeToTarget;
            if (Vector3D.IsZero(targetLinVel, 5E-03)) targetLinVel = Vector3.Zero;
            if (Vector3D.IsZero(targetAccel, 5E-03)) targetAccel = Vector3.Zero;
            var entityCenter = entity.PositionComp.WorldAABB.Center;

            if (prediction != Prediction.Off)
                targetPos = weapon.GetPredictedTargetPosition(entityCenter, targetLinVel, targetAccel, prediction, out timeToIntercept);
            else
                targetPos = entityCenter;

            Vector3D.DistanceSquared(ref targetPos, ref weapon.MyPivotPos, out rangeToTarget);

            bool canTrack = false;
            if (rangeToTarget <= weapon.System.MaxTrajectorySqr)
            {
                var targetDir = targetPos - weapon.MyPivotPos;
                if (weapon == trackingWeapon)
                {
                    for (int i = 9; i-- > 0;)
                    {
                        if (i == 7)
                        {
                            var rotMatrix = Quaternion.CreateFromRotationMatrix(entity.PositionComp.WorldMatrix);
                            var obb = new MyOrientedBoundingBoxD(entityCenter, entity.PositionComp.LocalAABB.HalfExtents, rotMatrix) { Center = targetPos };
                            weapon.targetBox = obb;
                            obb.GetCorners(weapon.TargetObbCorners, 0);
                        }

                        var corner = weapon.TargetObbCorners[i];
                        targetDir = corner - weapon.MyPivotPos;

                        Vector3D currentVector;
                        Vector3D.CreateFromAzimuthAndElevation(weapon.Azimuth, weapon.Elevation, out currentVector);
                        currentVector = Vector3D.Rotate(currentVector, weapon.Comp.MyCube.WorldMatrix);

                        var up = weapon.Comp.MyCube.WorldMatrix.Up;
                        var left = Vector3D.Cross(up, currentVector);
                        if (!Vector3D.IsUnit(ref left) && !Vector3D.IsZero(left))
                            left.Normalize();
                        var forward = Vector3D.Cross(left, up);

                        var matrix = new MatrixD { Forward = forward, Left = left, Up = up, };

                        double desiredAzimuth;
                        double desiredElevation;
                        MathFuncs.GetRotationAngles(ref targetDir, ref matrix, out desiredAzimuth, out desiredElevation);

                        var azConstraint = Math.Min(weapon.MaxAzimuthRadians, Math.Max(weapon.MinAzimuthRadians, desiredAzimuth));
                        var elConstraint = Math.Min(weapon.MaxElevationRadians, Math.Max(weapon.MinElevationRadians, desiredElevation));
                        var azConstrained = Math.Abs(elConstraint - desiredElevation) > 0.0000001;
                        var elConstrained = Math.Abs(azConstraint - desiredAzimuth) > 0.0000001;
                        canTrack = !azConstrained && !elConstrained;
                        if (canTrack) break;
                    }
                }
                else
                    canTrack = MathFuncs.IsDotProductWithinTolerance(ref weapon.MyPivotDir, ref targetDir, weapon.AimingTolerance);
            }

            return canTrack;
        }

        internal static bool TargetAligned(Weapon weapon, Target target)
        {
            Vector3D targetPos;
            Vector3 targetLinVel = Vector3.Zero;
            Vector3 targetAccel = Vector3.Zero;

            var targetCenter = target.Projectile?.Position ?? target.Entity.PositionComp.WorldAABB.Center;
            double timeToIntercept;
            double rangeToTarget;

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

            if (weapon.Prediction != Prediction.Off)
                targetPos = weapon.GetPredictedTargetPosition(targetCenter, targetLinVel, targetAccel, weapon.Prediction, out timeToIntercept);
            else
                targetPos = targetCenter;

            var targetDir = targetPos - weapon.MyPivotPos;

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
            var targetCenter = target.Projectile?.Position ?? target.Entity.PositionComp.WorldAABB.Center;
            double timeToIntercept;
            double rangeToTarget;
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
            
            if (weapon.Prediction != Prediction.Off)
                targetPos = weapon.GetPredictedTargetPosition(targetCenter, targetLinVel, targetAccel, weapon.Prediction, out timeToIntercept);
            else
                targetPos = targetCenter;

            weapon.TargetPos = targetPos;

            Vector3D.DistanceSquared(ref targetPos, ref weapon.MyPivotPos, out rangeToTarget);
            var targetDir = targetPos - weapon.MyPivotPos;

            if (rangeToTarget <= weapon.System.MaxTrajectorySqr)
            {
                double checkAzimuth;
                double checkElevation;

                Vector3D currentVector;
                Vector3D.CreateFromAzimuthAndElevation(weapon.Azimuth, weapon.Elevation, out currentVector);
                currentVector = Vector3D.Rotate(currentVector, weapon.Comp.MyCube.WorldMatrix);
                var up = weapon.Comp.MyCube.WorldMatrix.Up;
                var left = Vector3D.Cross(up, currentVector);
                if (!Vector3D.IsUnit(ref left) && !Vector3D.IsZero(left))
                    left.Normalize();
                var forward = Vector3D.Cross(left, up);
                var constraintMatrix = new MatrixD { Forward = forward, Left = left, Up = up, };

                MathFuncs.GetRotationAngles(ref targetDir, ref constraintMatrix, out checkAzimuth, out checkElevation);

                var azConstraint = Math.Min(weapon.MaxAzimuthRadians, Math.Max(weapon.MinAzimuthRadians, checkAzimuth));
                var elConstraint = Math.Min(weapon.MaxElevationRadians, Math.Max(weapon.MinElevationRadians, checkElevation));
                var azConstrained = Math.Abs(elConstraint - checkElevation) > 0.0000001;
                var elConstrained = Math.Abs(azConstraint - checkAzimuth) > 0.0000001;

                weapon.IsTracking = !azConstrained && !elConstrained;

                if (weapon.IsTracking && step)
                {
                    double desiredAzimuth;
                    double desiredElevation;
                    MathFuncs.GetRotationAngles(ref targetDir, ref weapon.MyPivotMatrix, out desiredAzimuth, out desiredElevation);

                    if (desiredAzimuth > 1 || desiredAzimuth < -1)
                        desiredElevation = 0;

                    var maxAzimuthStep = weapon.System.AzStep;
                    var maxElevationStep = weapon.System.ElStep;

                    var oldAz = weapon.Azimuth;
                    var oldEl = weapon.Elevation;
                    var newAz = weapon.Azimuth + MathHelperD.Clamp(desiredAzimuth, -maxAzimuthStep, maxAzimuthStep);
                    var newEl = weapon.Elevation + MathHelperD.Clamp(desiredElevation, -maxElevationStep, maxElevationStep);
                    var azDiff = oldAz - newAz;
                    var elDiff = oldEl - newEl;
                    var azLocked = azDiff > -1E-06d && azDiff < 1E-06d;
                    var elLocked = elDiff > -1E-06d && elDiff < 1E-06d;
                    var aim = !azLocked || !elLocked;

                    if (aim)
                        weapon.AimBarrel(azDiff, elDiff);
                }
            }

            if (!step) return weapon.IsTracking;

            var isAligned = false;

            if (weapon.IsTracking)
                isAligned = MathFuncs.IsDotProductWithinTolerance(ref weapon.MyPivotDir, ref targetDir, weapon.AimingTolerance);
            else
                weapon.SeekTarget = true;

            var wasAligned = weapon.IsAligned;
            weapon.IsAligned = isAligned;

            var alignedChange = wasAligned != isAligned;
            if (alignedChange && isAligned)
            {
                if (weapon.System.DesignatorWeapon)
                {
                    var reset = false;
                    for (int i = 0; i < weapon.Comp.Platform.Weapons.Length; i++)
                    {
                        var w = weapon.Comp.Platform.Weapons[i];

                        if (w.Target.Expired && w != weapon)
                        {
                            GridAi.AcquireTarget(w, false, weapon.Target.Entity.GetTopMostParent());
                            if (!w.Target.Expired)
                                reset = false;
                        }
                    }
                    if (reset)
                    {
                        weapon.SeekTarget = true;
                        weapon.IsAligned = false;
                        weapon.Target.Expired = true;
                    }
                }
                else
                    weapon.StartShooting();
            }
            else if (alignedChange && !weapon.DelayCeaseFire)
                weapon.StopShooting();

            weapon.TurretTargetLock = weapon.IsTracking && weapon.IsAligned;
            return weapon.IsTracking;
        }

        public Vector3D GetPredictedTargetPosition(Vector3D targetPos, Vector3 targetLinVel, Vector3D targetAccel,
            Prediction prediction, out double timeToIntercept)
        {

            var ammoSpeed = System.Values.Ammo.Trajectory.DesiredSpeed;
            if (ammoSpeed <= 0)
            {
                timeToIntercept = 0;
                return targetPos;
            }

            var shooterPos = MyPivotPos;
            if (Comp.Ai.VelocityUpdateTick != Comp.Ai.Session.Tick)
            {
                Comp.Ai.GridVel = Comp.Ai.MyGrid.Physics.LinearVelocity;
                Comp.Ai.VelocityUpdateTick = Comp.Ai.Session.Tick;
            }
            var targetVel = targetLinVel;
            Vector3D predictedPos;
            if (prediction == Prediction.Basic) 
            {
                var deltaPos = targetPos - shooterPos;
                var deltaVel = targetVel - Comp.Ai.GridVel;
                timeToIntercept = MathFuncs.Intercept(deltaPos, deltaVel, ammoSpeed);
                predictedPos = targetPos + (float)timeToIntercept * deltaVel;
            }
            else if (prediction == Prediction.Accurate)
                predictedPos = CalculateProjectileInterceptPointFast(ammoSpeed, 60, Comp.Ai.GridVel, shooterPos, targetVel, targetAccel, targetPos, out timeToIntercept);
            else
                predictedPos = CalculateProjectileInterceptPoint(Comp.Ai.Session.MaxEntitySpeed, ammoSpeed, 60, Comp.Ai.GridVel, shooterPos, targetVel, targetAccel, targetPos, out timeToIntercept);

            return predictedPos;
        }

        /*
        ** Whip's Projectile Intercept - Modified for DarkStar 06.15.2019
        */
        //Vector3D _lastTargetVelocity1 = Vector3D.Zero;
        public static Vector3D CalculateProjectileInterceptPoint(
            double gridMaxSpeed,        /* Maximum grid speed           (m/s)   */
            double projectileSpeed,     /* Maximum projectile speed     (m/s)   */
            double updateFrequency,     /* Frequency this is run        (Hz)    */
            Vector3D shooterVelocity,   /* Shooter initial velocity     (m/s)   */
            Vector3D shooterPosition,   /* Shooter initial position     (m)     */
            Vector3D targetVelocity,    /* Target initial velocity      (m/s)   */
            Vector3D targetAccel,       /* Target Accel velocity        (m/s/s) */
            Vector3D targetPosition,    /* Target initial position      (m)     */
            out double timeToIntercept  /* Estimated time to intercept  (s)     */)
        {
            timeToIntercept = -1;

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
            else if (tmin > 0)
                timeToIntercept = tmin;
            else
                timeToIntercept = tmax;

            //Vector3D targetAcceleration = updateFrequency * (targetVelocity - _lastTargetVelocity1);
            //_lastTargetVelocity1 = targetVelocity;

            Vector3D interceptEst = targetPosition
                                    + targetVelocity * timeToIntercept;
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

        /*
        ** Whip's Projectile Intercept - Modified for DarkStar 06.15.2019
        */
        //Vector3D _lastTargetVelocity2 = Vector3D.Zero;
        private static Vector3D CalculateProjectileInterceptPointFast(
            double projectileSpeed,     /* Maximum projectile speed     (m/s) */
            double updateFrequency,     /* Frequency this is run        (Hz) */
            Vector3D shooterVelocity,   /* Shooter initial velocity     (m/s) */
            Vector3D shooterPosition,   /* Shooter initial position     (m) */
            Vector3D targetVelocity,    /* Target initial velocity      (m/s) */
            Vector3D targetAccel,       /* Target Accel velocity        (m/s/s) */
            Vector3D targetPosition,    /* Target initial position      (m) */
            out double timeToIntercept  /* Estimated time to intercept  (s) */)
        {
            timeToIntercept = -1;

            var directHeading = targetPosition - shooterPosition;
            var directHeadingNorm = Vector3D.Normalize(directHeading);
            var distanceToTarget = Vector3D.Dot(directHeading, directHeadingNorm);

            var relativeVelocity = targetVelocity - shooterVelocity;

            var parallelVelocity = relativeVelocity.Dot(directHeadingNorm) * directHeadingNorm;
            var normalVelocity = relativeVelocity - parallelVelocity;
            var diff = projectileSpeed * projectileSpeed - normalVelocity.LengthSquared();
            if (diff < 0)
                return targetPosition;

            var projectileForwardSpeed = Math.Sqrt(diff);
            var projectileForwardVelocity = projectileForwardSpeed * directHeadingNorm;
            timeToIntercept = distanceToTarget / projectileForwardSpeed;

            //var targetAcceleration = updateFrequency * (targetVelocity - _lastTargetVelocity2);
            //_lastTargetVelocity2 = targetVelocity;

            var interceptPoint = shooterPosition + (projectileForwardVelocity + normalVelocity) * timeToIntercept + 0.5 * targetAccel * timeToIntercept * timeToIntercept;
            return interceptPoint;
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
