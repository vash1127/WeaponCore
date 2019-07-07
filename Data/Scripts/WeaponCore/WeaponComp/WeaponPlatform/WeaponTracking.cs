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
            var prediction = weapon.Kind.HardPoint.TargetPrediction;

            Vector3D targetPos;
            var timeToIntercept = double.MinValue;

            if (prediction != Prediction.Off)
                targetPos = weapon.GetPredictedTargetPosition(target, prediction, out timeToIntercept);
            else
                targetPos = target.PositionComp.WorldMatrix.Translation;

            var targetDir = targetPos - weapon.Comp.MyPivotPos;
            var isAligned = IsDotProductWithinTolerance(ref trackingWeapon.Comp.MyPivotDir, ref targetDir, 0.9659);

            if (checkOnly) return isAligned;

            weapon.TargetPos = targetPos;
            weapon.TargetDir = targetDir;
            weapon.IsAligned = isAligned;

            return isAligned;
        }

        internal static bool TrackingTarget(Weapon weapon, MyEntity target, bool step = false)
        {
            var turret = weapon.Comp.Turret;
            var cube = weapon.Comp.MyCube;
            var prediction = weapon.Kind.HardPoint.TargetPrediction;

            Vector3D targetPos;
            var timeToIntercept = double.MinValue;

            if (prediction != Prediction.Off)
                targetPos = weapon.GetPredictedTargetPosition(target, prediction, out timeToIntercept);
            else
                targetPos = target.PositionComp.WorldMatrix.Translation;

            weapon.TargetPos = targetPos;
            weapon.TargetDir = targetPos - weapon.Comp.MyPivotPos;

            var maxAzimuthStep = step ? weapon.Kind.HardPoint.RotateSpeed : float.MinValue;
            var maxElevationStep = step ? weapon.Kind.HardPoint.ElevationSpeed : float.MinValue;
            Vector3D currentVector;
            Vector3D.CreateFromAzimuthAndElevation(turret.Azimuth, turret.Elevation, out currentVector);
            currentVector = Vector3D.Rotate(currentVector, cube.WorldMatrix);

            var up = cube.WorldMatrix.Up;
            var left = Vector3D.Cross(up, currentVector);
            if (!Vector3D.IsUnit(ref left) && !Vector3D.IsZero(left))
                left.Normalize();
            var forward = Vector3D.Cross(left, up);

            var matrix = new MatrixD { Forward = forward, Left = left, Up = up, };

            float desiredAzimuth;
            float desiredElevation;
            GetRotationAngles(ref weapon.TargetDir, ref matrix, out desiredAzimuth, out desiredElevation);

            var azConstraint = Math.Min(weapon.MaxAzimuthRadians, Math.Max(weapon.MinAzimuthRadians, desiredAzimuth));
            var elConstraint = Math.Min(weapon.MaxElevationRadians, Math.Max(weapon.MinElevationRadians, desiredElevation));
            var azConstrained = Math.Abs(elConstraint - desiredElevation) > 0.000001;
            var elConstrained = Math.Abs(azConstraint - desiredAzimuth) > 0.000001;
            weapon.IsTracking = !azConstrained && !elConstrained;
            if (!step) return weapon.IsTracking;

            if (weapon.IsTracking && maxAzimuthStep > float.MinValue)
            {
                var oldAz = weapon.Azimuth;
                var oldEl = weapon.Elevation;
                weapon.Azimuth = turret.Azimuth + MathHelper.Clamp(desiredAzimuth, -maxAzimuthStep, maxAzimuthStep);
                weapon.Elevation = turret.Elevation + MathHelper.Clamp(desiredElevation - turret.Elevation, -maxElevationStep, maxElevationStep);
                weapon.DesiredAzimuth = desiredAzimuth;
                weapon.DesiredElevation = desiredElevation;
                var azLocked = MathHelper.IsZero(oldAz - weapon.Azimuth);
                var elLocked = MathHelper.IsZero(oldEl - weapon.Elevation);
                if (!azLocked) turret.Azimuth = weapon.Azimuth;
                if (!elLocked) turret.Elevation = weapon.Elevation;
                weapon.Comp.AiLock = azLocked && elLocked;
            }

            var isInView = false;
            var isAligned = false;
            if (weapon.IsTracking)
            {
                isInView = IsTargetInView(weapon, targetPos);
                if (isInView)
                    isAligned = IsDotProductWithinTolerance(ref weapon.Comp.MyPivotDir, ref weapon.TargetDir, weapon.AimingTolerance);
            }
            else weapon.Target = null;

            weapon.IsInView = isInView;
            var wasAligned = weapon.IsAligned;
            weapon.IsAligned = isAligned;

            var alignedChange = wasAligned != isAligned;
            if (alignedChange && isAligned) weapon.StartShooting();
            else if (alignedChange) weapon.StopShooting();
            weapon.Comp.TurretTargetLock = weapon.IsTracking && weapon.IsInView && weapon.IsAligned;
            return weapon.IsTracking;
        }

        private static bool IsTargetInView(Weapon weapon, Vector3D predPos)
        {
            var lookAtPositionEuler = weapon.LookAt(predPos);
            var inRange = weapon.IsInRange(ref lookAtPositionEuler);
            //Log.Line($"isInRange: {inRange}");
            return inRange;
        }

        private bool _gunIdleElevationAzimuthUnknown = true;
        private float _gunIdleElevation;
        private float _gunIdleAzimuth;
        private Vector3 LookAt(Vector3D target)
        {
            Vector3D muzzleWorldPosition = Comp.MyPivotPos;
            float azimuth;
            float elevation;
            Vector3.GetAzimuthAndElevation(Vector3.Normalize(Vector3D.TransformNormal(target - muzzleWorldPosition, EntityPart.PositionComp.WorldMatrixInvScaled)), out azimuth, out elevation);
            if (_gunIdleElevationAzimuthUnknown)
            {
                Vector3.GetAzimuthAndElevation((Vector3)Comp.Gun.GunBase.GetMuzzleLocalMatrix().Forward, out _gunIdleAzimuth, out _gunIdleElevation);
                _gunIdleElevationAzimuthUnknown = false;
            }
            return new Vector3(elevation - _gunIdleElevation, MathHelper.WrapAngle(azimuth - _gunIdleAzimuth), 0.0f);
        }

        private bool IsInRange(ref Vector3 lookAtPositionEuler)
        {
            float y = lookAtPositionEuler.Y;
            float x = lookAtPositionEuler.X;
            if (y > MinAzimuthRadians && y < MaxAzimuthRadians && x > MinElevationRadians)
                return x < MaxElevationRadians;
            return false;
        }

        public static float AngleBetween(Vector3D a, Vector3D b) //returns radians
        {
            if (Vector3D.IsZero(a) || Vector3D.IsZero(b))
                return 0;
            else
                return (float)Math.Acos(MathHelper.Clamp(a.Dot(b) / Math.Sqrt(a.LengthSquared() * b.LengthSquared()), -1, 1));
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
        static void GetRotationAngles(ref Vector3D targetVector, ref MatrixD worldMatrix, out float yaw, out float pitch)
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

            if (target == null || target.MarkedForClose)
            {
                _lastTimeToIntercept = -1;
                timeToIntercept = _lastTimeToIntercept;
                return _lastPredictedPos;
            }

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
            var ammoSpeed = Kind.Ammo.Trajectory.DesiredSpeed;
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

        /// <summary>
        /// Returns if the normalized dot product between two vectors is greater than the tolerance.
        /// This is helpful for determining if two vectors are "more parallel" than the tolerance.
        /// </summary>
        /// <param name="a">First vector</param>
        /// <param name="b">Second vector</param>
        /// <param name="tolerance">Cosine of maximum angle</param>
        /// <returns></returns>
        public static bool IsDotProductWithinTolerance(ref Vector3D a, ref Vector3D b, double tolerance)
        {
            double dot = Vector3D.Dot(a, b);
            double num = a.LengthSquared() * b.LengthSquared() * tolerance * Math.Abs(tolerance);
            return Math.Abs(dot) * dot > num;
        }

        internal void InitTracking()
        {
            //_randomStandbyChange_ms = MyAPIGateway.Session.ElapsedPlayTime.Milliseconds;
            //_randomStandbyChangeConst_ms = MyUtils.GetRandomInt(3500, 4500);
            //_randomStandbyRotation = 0.0f;
            //_randomStandbyElevation = 0.0f;

            RotationSpeed = Comp.Platform.BaseDefinition.RotationSpeed;
            ElevationSpeed = Comp.Platform.BaseDefinition.ElevationSpeed;
            MinElevationRadians = MathHelper.ToRadians(NormalizeAngle(Comp.Platform.BaseDefinition.MinElevationDegrees));
            MaxElevationRadians = MathHelper.ToRadians(NormalizeAngle(Comp.Platform.BaseDefinition.MaxElevationDegrees));

            if ((double)MinElevationRadians > (double)MaxElevationRadians)
                MinElevationRadians -= 6.283185f;
            //_minSinElevationRadians = (float)Math.Sin((double)MinElevationRadians);
            //_maxSinElevationRadians = (float)Math.Sin((double)MaxElevationRadians);
            MinAzimuthRadians = MathHelper.ToRadians(NormalizeAngle(Comp.Platform.BaseDefinition.MinAzimuthDegrees));
            MaxAzimuthRadians = MathHelper.ToRadians(NormalizeAngle(Comp.Platform.BaseDefinition.MaxAzimuthDegrees));
            if ((double)MinAzimuthRadians > (double)MaxAzimuthRadians)
                MinAzimuthRadians -= 6.283185f;
        }

        private float NormalizeAngle(int angle)
        {
            int num = angle % 360;
            if (num == 0 && angle != 0)
                return 360f;
            return num;
        }

        /*
        private int _randomStandbyChange_ms;
        private int _randomStandbyChangeConst_ms;
        private float _randomStandbyElevation;
        private void GetCameraDummy()
        {
            if (this.m_base2.Model == null || !this.m_base2.Model.Dummies.ContainsKey("camera"))
                return;
            this.CameraDummy = this.m_base2.Model.Dummies["camera"];
        }
        */
    }
}
