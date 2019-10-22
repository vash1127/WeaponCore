using Sandbox.Definitions;
using System;
using VRage.Game;
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
            var azimuthPart = weapon.AzimuthPart.Item1;
            Vector3D targetPos;
            double timeToIntercept;
            double rangeToTarget;
            if (Vector3D.IsZero(targetLinVel, 5E-02)) targetLinVel = Vector3.Zero;
            if (Vector3D.IsZero(targetAccel, 5E-02)) targetLinVel = Vector3.Zero;

            if (prediction != Prediction.Off)
                targetPos = weapon.GetPredictedTargetPosition(targetCenter, targetLinVel, targetAccel, prediction, out timeToIntercept);
            else
                targetPos = targetCenter;
            var targetDir = targetPos - weapon.MyPivotPos;
            targetDir.Normalize();

            Vector3D.DistanceSquared(ref targetPos, ref weapon.MyPivotPos, out rangeToTarget);

            var inRange = rangeToTarget <= weapon.System.MaxTrajectorySqr;

            bool canTrack;
            if (weapon == trackingWeapon)
            {
                double desiredAzimuth;
                double desiredElevation;
                MathFuncs.GetRotationAngles(ref targetDir, ref weapon.MyPivotMatrix, out desiredAzimuth, out desiredElevation);

                var currentAzRadians = MathHelperD.ToRadians(weapon.Azimuth);
                var currentElRadians = MathHelperD.ToRadians(weapon.Elevation);
                var newDesiredAz = currentAzRadians + desiredAzimuth;
                var newDesiredEl = currentElRadians + desiredElevation;

                canTrack = newDesiredAz >= weapon.MinAzimuthRadians && newDesiredAz <= weapon.MaxAzimuthRadians && newDesiredEl >= weapon.MinElevationRadians && newDesiredEl <= weapon.MaxElevationRadians;
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
            if (Vector3D.IsZero(targetLinVel, 5E-04)) targetLinVel = Vector3D.Zero;

            if (weapon.Prediction != Prediction.Off)
                targetPos = weapon.GetPredictedTargetPosition(targetCenter, targetLinVel, targetAccel, weapon.Prediction, out timeToIntercept);
            else
                targetPos = targetCenter;

            Vector3D.DistanceSquared(ref targetPos, ref weapon.MyPivotPos, out rangeToTarget);
            var inRange = rangeToTarget <= weapon.System.MaxTrajectorySqr;

            weapon.TargetPos = targetPos;
            var targetDir = targetPos - weapon.MyPivotPos;

            var maxAzimuthStep = step ? weapon.System.Values.HardPoint.Block.RotateRate : double.MinValue;
            var maxElevationStep = step ? weapon.System.Values.HardPoint.Block.ElevateRate : double.MinValue;

            var matrix = new MatrixD { Forward = weapon.MyPivotDir, Left = weapon.MyPivotLeft, Up = weapon.MyPivotUp, };
            double desiredAzimuth;
            double desiredElevation;
            MathFuncs.GetRotationAngles(ref targetDir, ref matrix, out desiredAzimuth, out desiredElevation);

            var currentAzRadians = MathHelperD.ToRadians(weapon.Azimuth);
            var currentElRadians = MathHelperD.ToRadians(weapon.Elevation);
            var newDesiredAz = currentAzRadians + desiredAzimuth;
            var newDesiredEl = currentElRadians + desiredElevation;

            weapon.IsTracking = inRange && newDesiredAz >= weapon.MinAzimuthRadians && newDesiredAz <= weapon.MaxAzimuthRadians && newDesiredEl >= weapon.MinElevationRadians && newDesiredEl <= weapon.MaxElevationRadians;

            if (!step) return weapon.IsTracking;
            if (weapon.IsTracking && maxAzimuthStep > double.MinValue)
            {
                var oldAz = weapon.Azimuth;
                var oldEl = weapon.Elevation;
                var newAz = weapon.Azimuth + MathHelperD.Clamp(desiredAzimuth, -maxAzimuthStep, maxAzimuthStep);
                var newEl = weapon.Elevation + MathHelperD.Clamp(desiredElevation - weapon.Elevation, -maxElevationStep, maxElevationStep);
                var azDiff = oldAz - newAz;
                var elDiff = oldEl - newEl;

                var azLocked = azDiff > -1E-07d && azDiff < 1E-07d;
                var elLocked = elDiff > -1E-07d && elDiff < 1E-07d;
                var aim = !azLocked || !elLocked;
                weapon.Comp.AiMoving = aim;
                if (aim)
                {
                    weapon.LastTrackedTick = weapon.Comp.Ai.Session.Tick;
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

            var alignedChange = wasAligned != isAligned;
            if (alignedChange && isAligned) weapon.StartShooting();
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

            if (!Comp.IsAiOnlyTurret)
            {
                var baseDef = Comp.MyCube.BlockDefinition as MyLargeTurretBaseDefinition;
                if (baseDef != null)
                {
                    minAz = baseDef.MinAzimuthDegrees;
                    maxAz = baseDef.MaxAzimuthDegrees;
                    minEl = baseDef.MinElevationDegrees;
                    maxEl = baseDef.MaxElevationDegrees;
                }
            }

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
