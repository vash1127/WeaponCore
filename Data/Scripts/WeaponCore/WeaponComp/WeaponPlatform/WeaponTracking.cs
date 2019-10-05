using Sandbox.Game.Weapons;
using Sandbox.ModAPI;
using SpaceEngineers.Game.ModAPI;
using System;
using System.Diagnostics.Eventing.Reader;
using VRage.Game;
using VRageMath;
using WeaponCore.Support;
using static WeaponCore.Support.HardPointDefinition;

namespace WeaponCore.Platform
{
    public partial class Weapon
    {
        internal static bool CanShootTarget(Weapon weapon, ref Vector3D targetCenter, ref Vector3D targetLinVel, ref Vector3D targetAccel)
        {
            var prediction = weapon.System.Values.HardPoint.AimLeadingPrediction;
            var cube = weapon.Comp.MyCube;
            var trackingWeapon = weapon.Comp.TrackingWeapon;
            Vector3D targetPos;
            double timeToIntercept;
            double rangeToTarget;
            if (Vector3D.IsZero(targetLinVel, 5E-02)) targetLinVel = Vector3.Zero;
            if (Vector3D.IsZero(targetAccel, 5E-02)) targetLinVel = Vector3.Zero;

            if (prediction != Prediction.Off)
                targetPos = weapon.GetPredictedTargetPosition(targetCenter, targetLinVel, targetAccel, prediction, out timeToIntercept);
            else
                targetPos = targetCenter;
            var targetDir = Vector3D.Normalize(targetPos - weapon.MyPivotPos);

            Vector3D.DistanceSquared(ref targetPos, ref weapon.MyPivotPos, out rangeToTarget);

            var inRange = rangeToTarget <= weapon.System.MaxTrajectorySqr;
            
            bool canTrack;
            if (weapon == trackingWeapon)
            {
                Vector3D currentVector;
                Vector3D.CreateFromAzimuthAndElevation(weapon.Azimuth, weapon.Elevation, out currentVector);
                currentVector = Vector3D.Rotate(currentVector, cube.WorldMatrix);

                var up = cube.WorldMatrix.Up;
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
            if (Vector3D.IsZero(targetLinVel, 5E-02)) targetLinVel = Vector3D.Zero;

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
            var AzimuthPart = weapon.AzimuthPart.Item1;
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
            if (Vector3D.IsZero(targetLinVel, 5E-02)) targetLinVel = Vector3D.Zero;

            if (weapon.Prediction != Prediction.Off)
                targetPos = weapon.GetPredictedTargetPosition(targetCenter, targetLinVel, targetAccel, weapon.Prediction, out timeToIntercept);
            else
                targetPos = targetCenter;

            Vector3D.DistanceSquared(ref targetPos, ref weapon.MyPivotPos, out rangeToTarget);
            var inRange = rangeToTarget <= weapon.System.MaxTrajectorySqr;

            weapon.TargetPos = targetPos;
            var targetDir = targetPos - weapon.MyPivotPos;
            targetDir.Normalize();

            var maxAzimuthStep = step ? weapon.System.AzStep : double.MinValue;
            var maxElevationStep = step ? weapon.System.ElStep : double.MinValue;

            var matrix = new MatrixD { Forward = weapon.MyPivotDir, Left = weapon.MyPivotLeft, Up = weapon.MyPivotUp };
            double desiredAzimuth;
            double desiredElevation;
            MathFuncs.GetRotationAngles(ref targetDir, ref matrix, out desiredAzimuth, out desiredElevation);
           
            var azConstraint = Math.Min(weapon.MaxAzimuthRadians, Math.Max(weapon.MinAzimuthRadians, desiredAzimuth));
            var elConstraint = Math.Min(weapon.MaxElevationRadians, Math.Max(weapon.MinElevationRadians, desiredElevation));
            var azConstrained = Math.Abs(elConstraint - desiredElevation) > 0.000001;
            var elConstrained = Math.Abs(azConstraint - desiredAzimuth) > 0.000001;
            weapon.IsTracking = inRange && !azConstrained && !elConstrained;
            if (!step) return weapon.IsTracking;

            if (weapon.IsTracking && maxAzimuthStep > float.MinValue)
            {
                var oldAz = weapon.Azimuth;
                var oldEl = weapon.Elevation;
                var newAz = weapon.Azimuth + MathHelperD.Clamp(desiredAzimuth, -maxAzimuthStep, maxAzimuthStep);
                var newEl = weapon.Elevation + MathHelperD.Clamp(desiredElevation, -maxElevationStep, maxElevationStep);
                var azDiff = oldAz - newAz;
                var elDiff = oldEl - newEl;
                var azLocked = azDiff > -1E-10d && azDiff < 1E-10d;
                var elLocked = elDiff > -1E-10d && elDiff < 1E-10d;
                var aim = !azLocked || !elLocked;
                weapon.Comp.AiMoving = aim;
                if (aim)
                {
                    weapon.Comp.LastTrackedTick = Session.Instance.Tick;
                    weapon.AimBarrel(azDiff, elDiff);  
                }
            }

            var isAligned = false;

            if (weapon.IsTracking)
                isAligned = MathFuncs.IsDotProductWithinTolerance(ref weapon.MyPivotDir, ref targetDir, weapon.AimingTolerance);
            else
                weapon.SeekTarget = true;

            var wasAligned = weapon.IsAligned;
            weapon.IsAligned = isAligned;

            Log.Line($"wasAligned: {wasAligned} isAligned: {isAligned}");

            var alignedChange = wasAligned != isAligned;
            if (alignedChange && isAligned) weapon.StartShooting();
            else if (alignedChange && !weapon.DelayCeaseFire)
                weapon.StopShooting();

            weapon.Comp.TurretTargetLock = weapon.IsTracking && weapon.IsAligned;
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
            var shooterVel = Comp.Physics.LinearVelocity;
            var targetVel = targetLinVel;
            Vector3D predictedPos;
            if (prediction == Prediction.Basic) 
            {
                var deltaPos = targetPos - shooterPos;
                var deltaVel = targetVel - shooterVel;
                timeToIntercept = MathFuncs.Intercept(deltaPos, deltaVel, ammoSpeed);
                predictedPos = targetPos + (float)timeToIntercept * deltaVel;
            }
            else if (prediction == Prediction.Accurate)
                predictedPos = CalculateProjectileInterceptPointFast(ammoSpeed, 60, shooterVel, shooterPos, targetVel, targetAccel, targetPos, out timeToIntercept);
            else
                predictedPos = CalculateProjectileInterceptPoint(Session.Instance.MaxEntitySpeed, ammoSpeed, 60, shooterVel, shooterPos, targetVel, targetAccel, targetPos, out timeToIntercept);

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
            MinElevationRadians = MathHelperD.ToRadians(MathFuncs.NormalizeAngle(System.MinElevation));
            MaxElevationRadians = MathHelperD.ToRadians(MathFuncs.NormalizeAngle(System.MaxElevation));

            if (MinElevationRadians > MaxElevationRadians)
                MinElevationRadians -= 6.283185f;
            MinAzimuthRadians = MathHelperD.ToRadians(MathFuncs.NormalizeAngle(System.MinAzimuth));
            MaxAzimuthRadians = MathHelperD.ToRadians(MathFuncs.NormalizeAngle(System.MaxAzimuth));
            if (MinAzimuthRadians > MaxAzimuthRadians)
                MinAzimuthRadians -= 6.283185f;
        }
    }
}
