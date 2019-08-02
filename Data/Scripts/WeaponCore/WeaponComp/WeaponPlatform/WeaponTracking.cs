using System;
using VRage.Game;
using VRage.Game.Entity;
using VRageMath;
using WeaponCore.Support;
using static WeaponCore.Support.HardPointDefinition;

namespace WeaponCore.Platform
{
    public partial class Weapon
    {
        internal static bool ValidTarget(Weapon weapon, MyEntity target, bool checkOnly = false)
        {
            var trackingWeapon = weapon.Comp.TrackingWeapon;
            var prediction = weapon.System.Values.HardPoint.AimLeadingPrediction;

            Vector3D targetPos;

            if (prediction != Prediction.Off)
                targetPos = weapon.GetPredictedTargetPosition(target, prediction, out var timeToIntercept);
            else
                targetPos = target.PositionComp.WorldMatrix.Translation;

            var targetDir = targetPos - weapon.Comp.MyPivotPos;

            Vector3D.DistanceSquared(ref targetPos, ref weapon.Comp.MyPivotPos, out var rangeToTarget);
            var inRange = rangeToTarget <= weapon.System.MaxTrajectorySqr;

            var isAligned = inRange && IsDotProductWithinTolerance(ref trackingWeapon.Comp.MyPivotDir, ref targetDir, weapon.AimingTolerance);
            if (checkOnly)
                return isAligned;

            weapon.TargetPos = targetPos;
            weapon.TargetDir = targetDir;
            weapon.IsAligned = isAligned;

            return isAligned;
        }

        internal static bool TrackingTarget(Weapon weapon, MyEntity target, bool step = false)
        {
            var turret = weapon.Comp.Turret;
            var cube = weapon.Comp.MyCube;
            var prediction = weapon.System.Values.HardPoint.AimLeadingPrediction;

            Vector3D targetPos;

            if (prediction != Prediction.Off)
                targetPos = weapon.GetPredictedTargetPosition(target, prediction, out var timeToIntercept);
            else
                targetPos = target.PositionComp.WorldMatrix.Translation;

            Vector3D.DistanceSquared(ref targetPos, ref weapon.Comp.MyPivotPos, out var rangeToTarget);
            var inRange = rangeToTarget <= weapon.System.MaxTrajectorySqr;

            weapon.TargetPos = targetPos;
            weapon.TargetDir = targetPos - weapon.Comp.MyPivotPos;

            var maxAzimuthStep = step ? weapon.System.Values.HardPoint.RotateSpeed : double.MinValue;
            var maxElevationStep = step ? weapon.System.Values.HardPoint.ElevationSpeed : double.MinValue;
            Vector3D.CreateFromAzimuthAndElevation(turret.Azimuth, turret.Elevation, out var currentVector);
            currentVector = Vector3D.Rotate(currentVector, cube.WorldMatrix);

            var up = cube.WorldMatrix.Up;
            var left = Vector3D.Cross(up, currentVector);
            if (!Vector3D.IsUnit(ref left) && !Vector3D.IsZero(left))
                left.Normalize();
            var forward = Vector3D.Cross(left, up);

            var matrix = new MatrixD { Forward = forward, Left = left, Up = up, };

            GetRotationAngles(ref weapon.TargetDir, ref matrix, out var desiredAzimuth, out var desiredElevation);

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
                weapon.Azimuth = weapon.Azimuth + MathHelperD.Clamp(desiredAzimuth, -maxAzimuthStep, maxAzimuthStep);
                weapon.Elevation = weapon.Elevation + MathHelperD.Clamp(desiredElevation - weapon.Elevation, -maxElevationStep, maxElevationStep);
                weapon.DesiredAzimuth = desiredAzimuth;
                weapon.DesiredElevation = desiredElevation;
                var azDiff = oldAz - weapon.Azimuth;
                var elDiff = oldEl - weapon.Elevation;
                var azLocked = azDiff > -1E-07d && azDiff < 1E-07d;
                var elLocked = elDiff > -1E-07d && elDiff < 1E-07d;
                //var azLocked = MathHelper.IsZero(oldAz - weapon.Azimuth);
                //var elLocked = MathHelper.IsZero(oldEl - weapon.Elevation);
                var aim = !azLocked || !elLocked;
                weapon.Comp.AiMoving = aim;
                if (aim)
                {
                    weapon.Comp.LastTrackedTick = weapon.Comp.MyAi.MySession.Tick;
                    turret.Azimuth = (float) weapon.Azimuth;
                    turret.Elevation = (float) weapon.Elevation;
                }
            }

            var isInView = false;
            var isAligned = false;

            if (weapon.IsTracking)
            {
                isInView = IsTargetInView(weapon, targetPos);
                if (isInView)
                    isAligned = IsDotProductWithinTolerance(ref weapon.Comp.MyPivotDir, ref weapon.TargetDir, weapon.AimingTolerance);
            }
            else
            {
                Log.Line($"{weapon.System.WeaponName} - is not tracking - marked:{target.MarkedForClose} - Pos:{target.PositionComp.GetPosition()} - controller:{weapon.System.Values.HardPoint.TurretController} - isTrackingWeapon:{weapon == weapon.Comp.TrackingWeapon}");
                weapon.SeekTarget = true;
            }

            weapon.IsInView = isInView;
            var wasAligned = weapon.IsAligned;
            weapon.IsAligned = isAligned;

            var alignedChange = wasAligned != isAligned;
            if (alignedChange && isAligned) weapon.StartShooting();
            else if (alignedChange && !weapon.DelayCeaseFire)
            {
                Log.Line($"{weapon.System.WeaponName} - align change NoDelayCeaseFire - inRange:{inRange} - isAligned:{isAligned} - wasAligned:{wasAligned} - marked:{target.MarkedForClose} - controller:{weapon.System.Values.HardPoint.TurretController} - isTrackingWeapon:{weapon == weapon.Comp.TrackingWeapon}");
                weapon.StopShooting();
            }
            weapon.Comp.TurretTargetLock = weapon.IsTracking && weapon.IsInView && weapon.IsAligned;
            return weapon.IsTracking;
        }

        internal static bool IsTargetInView(Weapon weapon, Vector3D predPos)
        {
            var lookAtPositionEuler = weapon.LookAt(predPos);
            var inRange = weapon.IsInRange(ref lookAtPositionEuler);
            return inRange;
        }

        private bool _gunIdleElevationAzimuthUnknown = true;
        private double _gunIdleElevation;
        private double _gunIdleAzimuth;
        private Vector3D LookAt(Vector3D target)
        {
            var muzzleWorldPosition = Comp.MyPivotPos;
            Vector3D.GetAzimuthAndElevation(Vector3D.Normalize(Vector3D.TransformNormal(target - muzzleWorldPosition, EntityPart.PositionComp.WorldMatrixInvScaled)), out var azimuth, out var elevation);
            if (_gunIdleElevationAzimuthUnknown)
            {
                Vector3D.GetAzimuthAndElevation(Comp.Gun.GunBase.GetMuzzleLocalMatrix().Forward, out _gunIdleAzimuth, out _gunIdleElevation);
                _gunIdleElevationAzimuthUnknown = false;
            }

            var angle = azimuth - _gunIdleAzimuth;
            angle = Math.IEEERemainder(angle, MathHelperD.TwoPi);
            if (angle <= -Math.PI)
                angle += MathHelperD.TwoPi;
            else if (angle > Math.PI)
                angle -= MathHelperD.TwoPi;

            return new Vector3D(elevation - _gunIdleElevation, angle, 0.0d);
        }

        public static double WrapAngle(double angle)
        {
            angle = Math.IEEERemainder(angle, MathHelperD.TwoPi);
            if (angle <= -Math.PI)
                angle += MathHelperD.TwoPi;
            else if (angle > Math.PI)
                angle -= MathHelperD.TwoPi;
            return angle;
        }

        private bool IsInRange(ref Vector3D lookAtPositionEuler)
        {
            double y = lookAtPositionEuler.Y;
            double x = lookAtPositionEuler.X;
            if (y > MinAzimuthRadians && y < MaxAzimuthRadians && x > MinElevationRadians)
                return x < MaxElevationRadians;
            return false;
        }

        public static double AngleBetween(Vector3D a, Vector3D b) //returns radians
        {
            if (Vector3D.IsZero(a) || Vector3D.IsZero(b))
                return 0;
            else
                return Math.Acos(MathHelperD.Clamp(a.Dot(b) / Math.Sqrt(a.LengthSquared() * b.LengthSquared()), -1, 1));
        }

        private static double Intercept(Vector3D deltaPos, Vector3D deltaVel, float projectileVel)
        {
            var num1 = Vector3D.Dot(deltaVel, deltaVel) - projectileVel * projectileVel;
            var num2 = 2.0 * Vector3D.Dot(deltaVel, deltaPos);
            var num3 = Vector3D.Dot(deltaPos, deltaPos);
            var d = num2 * num2 - 4.0 * num1 * num3;
            if (d <= 0.0)
                return -1.0;
            return 2.0 * num3 / (Math.Sqrt(d) - num2);
        }

        /*
        /// Whip's Get Rotation Angles Method v14 - 9/25/18 ///
        Dependencies: AngleBetween
        */
        internal static void GetRotationAngles(ref Vector3D targetVector, ref MatrixD worldMatrix, out double yaw, out double pitch)
        {
            var localTargetVector = Vector3D.Rotate(targetVector, MatrixD.Transpose(worldMatrix));
            var flattenedTargetVector = new Vector3D(localTargetVector.X, 0, localTargetVector.Z);
            yaw = AngleBetween(Vector3D.Forward, flattenedTargetVector) * -Math.Sign(localTargetVector.X); //right is negative

            if (Vector3D.IsZero(flattenedTargetVector)) //check for straight up case
                pitch = MathHelper.PiOver2 * Math.Sign(localTargetVector.Y);
            else
                pitch = AngleBetween(localTargetVector, flattenedTargetVector) * Math.Sign(localTargetVector.Y); //up is positive
        }

        public Vector3D GetPredictedTargetPosition(MyEntity target, Prediction prediction, out double timeToIntercept)
        {
            var tick = Comp.MyAi.MySession.Tick;

            if (tick == _lastPredictionTick && _lastTarget == target)
            {
                timeToIntercept = _lastTimeToIntercept;
                return _lastPredictedPos;
            }
            _lastTarget = target;
            _lastPredictionTick = tick;

            var targetCenter = target.PositionComp.WorldAABB.Center;
            var shooterPos = Comp.MyPivotPos;
            var shooterVel = Comp.Physics.LinearVelocity;
            var ammoSpeed = System.Values.Ammo.Trajectory.DesiredSpeed;
            var projectileVel = ammoSpeed > 0 ? ammoSpeed : float.MaxValue * 0.1f;
            var targetVel = Vector3.Zero;

            if (target.Physics != null) targetVel = target.Physics.LinearVelocity;
            else
            {
                var topMostParent = target.GetTopMostParent();
                if (topMostParent?.Physics != null)
                    targetVel = topMostParent.Physics.LinearVelocity;
            }

            if (prediction == Prediction.Basic) 
            {
                var deltaPos = targetCenter - shooterPos;
                var deltaVel = targetVel - shooterVel;
                timeToIntercept = Intercept(deltaPos, deltaVel, projectileVel);
                _lastPredictedPos = targetCenter + (float)timeToIntercept * deltaVel;
            }
            else if (prediction == Prediction.Accurate)
                _lastPredictedPos = CalculateProjectileInterceptPointFast(projectileVel, 60, shooterVel, shooterPos, targetVel, targetCenter, out timeToIntercept);
            else
                _lastPredictedPos = CalculateProjectileInterceptPoint(Session.Instance.MaxEntitySpeed, projectileVel, 60, shooterVel, shooterPos, targetVel, targetCenter, out timeToIntercept);

            _lastTimeToIntercept = timeToIntercept;
            return _lastPredictedPos;
        }

        /*
        ** Whip's Projectile Intercept - Modified for DarkStar 06.15.2019
        */
        Vector3D _lastTargetVelocity1 = Vector3D.Zero;
        public Vector3D CalculateProjectileInterceptPoint(
            double gridMaxSpeed,        /* Maximum grid speed           (m/s)   */
            double projectileSpeed,     /* Maximum projectile speed     (m/s)   */
            double updateFrequency,     /* Frequency this is run        (Hz)    */
            Vector3D shooterVelocity,   /* Shooter initial velocity     (m/s)   */
            Vector3D shooterPosition,   /* Shooter initial position     (m)     */
            Vector3D targetVelocity,    /* Target initial velocity      (m/s)   */
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

            Vector3D targetAcceleration = updateFrequency * (targetVelocity - _lastTargetVelocity1);
            _lastTargetVelocity1 = targetVelocity;

            Vector3D interceptEst = targetPosition
                                    + targetVelocity * timeToIntercept;
            /*
            ** Target trajectory estimation
            */
            //double dt = 1.0 / 60.0; // This can be a const somewhere
            double dt = MyEngineConstants.UPDATE_STEP_SIZE_IN_SECONDS; // This can be a const somewhere

            double simtime = 0;
            double maxSpeedSq = gridMaxSpeed * gridMaxSpeed;
            Vector3D tgtPosSim = targetPosition;
            Vector3D tgtVelSim = deltaVel;
            Vector3D tgtAccStep = targetAcceleration * dt;

            while (simtime < timeToIntercept)
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
        Vector3D _lastTargetVelocity2 = Vector3D.Zero;
        private Vector3D CalculateProjectileInterceptPointFast(
            double projectileSpeed,     /* Maximum projectile speed     (m/s) */
            double updateFrequency,     /* Frequency this is run        (Hz) */
            Vector3D shooterVelocity,   /* Shooter initial velocity     (m/s) */
            Vector3D shooterPosition,   /* Shooter initial position     (m) */
            Vector3D targetVelocity,    /* Target initial velocity      (m/s) */
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

            var targetAcceleration = updateFrequency * (targetVelocity - _lastTargetVelocity2);
            _lastTargetVelocity2 = targetVelocity;

            var interceptPoint = shooterPosition + (projectileForwardVelocity + normalVelocity) * timeToIntercept + 0.5 * targetAcceleration * timeToIntercept * timeToIntercept;
            return interceptPoint;
        }

        public static bool IsDotProductWithinTolerance(ref Vector3D a, ref Vector3D b, double tolerance)
        {
            double dot = Vector3D.Dot(a, b);
            double num = a.LengthSquared() * b.LengthSquared() * tolerance * Math.Abs(tolerance);
            return Math.Abs(dot) * dot > num;
        }

        internal void InitTracking()
        {
            RotationSpeed = Comp.Platform.BaseDefinition.RotationSpeed;
            ElevationSpeed = Comp.Platform.BaseDefinition.ElevationSpeed;
            MinElevationRadians = MathHelperD.ToRadians(NormalizeAngle(Comp.Platform.BaseDefinition.MinElevationDegrees));
            MaxElevationRadians = MathHelperD.ToRadians(NormalizeAngle(Comp.Platform.BaseDefinition.MaxElevationDegrees));

            if (MinElevationRadians > MaxElevationRadians)
                MinElevationRadians -= 6.283185f;
            MinAzimuthRadians = MathHelperD.ToRadians(NormalizeAngle(Comp.Platform.BaseDefinition.MinAzimuthDegrees));
            MaxAzimuthRadians = MathHelperD.ToRadians(NormalizeAngle(Comp.Platform.BaseDefinition.MaxAzimuthDegrees));
            if (MinAzimuthRadians > MaxAzimuthRadians)
                MinAzimuthRadians -= 6.283185f;
        }

        private float NormalizeAngle(int angle)
        {
            int num = angle % 360;
            if (num == 0 && angle != 0)
                return 360f;
            return num;
        }
    }
}
