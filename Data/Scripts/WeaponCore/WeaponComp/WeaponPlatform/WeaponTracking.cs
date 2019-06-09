using System;
using Sandbox.ModAPI;
using VRage.Game.Entity;
using VRage.Utils;
using VRageMath;
using WeaponCore.Support;

namespace WeaponCore.Platform
{
    public partial class Weapon
    {
        internal static bool TrackingTarget(Weapon weapon, MyEntity target, bool step = false)
        {
            var trackingWeapon = weapon.Comp.TrackingWeapon;
            Log.Line($"match:{trackingWeapon == weapon} - this:{weapon.GetHashCode()}({weapon.WeaponSystem.WeaponName}) - controller:{trackingWeapon.GetHashCode()}({trackingWeapon.WeaponSystem.WeaponName})");
            var turret = trackingWeapon.Comp.Turret;
            var cube = weapon.Comp.MyCube;
            var targetPos = weapon.GetPredictedTargetPosition(target);
            weapon.TargetPos = targetPos;
            var weaponPos = weapon.Comp.MyPivotPos;
            var maxAngularStep = step ? weapon.WeaponType.TurretDef.RotateSpeed : double.MinValue;
            Vector3D currentVector;
            Vector3D.CreateFromAzimuthAndElevation(turret.Azimuth, turret.Elevation, out currentVector);
            currentVector = Vector3D.Rotate(currentVector, cube.WorldMatrix);

            var up = cube.WorldMatrix.Up;
            var left = Vector3D.Cross(up, currentVector);
            if (!Vector3D.IsUnit(ref left) && !Vector3D.IsZero(left))
                left.Normalize();
            var forward = Vector3D.Cross(left, up);

            var matrix = new MatrixD { Forward = forward, Left = left, Up = up, };

            var targetDirection = targetPos - weaponPos;

            double desiredAzimuth;
            double desiredElevation;
            GetRotationAngles(ref targetDirection, ref matrix, out desiredAzimuth, out desiredElevation);

            var azConstraint = Math.Min(trackingWeapon.MaxAzimuthRadians, Math.Max(trackingWeapon.MinAzimuthRadians, desiredAzimuth));
            var elConstraint = Math.Min(trackingWeapon.MaxElevationRadians, Math.Max(trackingWeapon.MinElevationRadians, desiredElevation));
            var azConstrained = Math.Abs(elConstraint - desiredElevation) > 0.000001;
            var elConstrained = Math.Abs(azConstraint - desiredAzimuth) > 0.000001;
            var tracking = !azConstrained && !elConstrained;
            if (!tracking) weapon.Target = null;
            else if (false && weapon == trackingWeapon && weapon.Target != null)
            {
                DsDebugDraw.DrawLine(weaponPos, weapon.Target.PositionComp.WorldAABB.Center, Color.Lime, 0.1f);
                DsDebugDraw.DrawLine(weaponPos, targetPos, Color.Orange, 0.1f);
            }

            if (weapon != trackingWeapon) return tracking;

            if (tracking && maxAngularStep > double.MinValue)
            {
                trackingWeapon.Azimuth = turret.Azimuth + MathHelper.Clamp(desiredAzimuth, -maxAngularStep, maxAngularStep);
                trackingWeapon.Elevation = turret.Elevation + MathHelper.Clamp(desiredElevation - turret.Elevation, -maxAngularStep, maxAngularStep);
                trackingWeapon.DesiredAzimuth = desiredAzimuth;
                trackingWeapon.DesiredElevation = desiredElevation;
                turret.Azimuth = (float) trackingWeapon.Azimuth;
                turret.Elevation = (float)trackingWeapon.Elevation;
            }
            return tracking;
        }

        /*
        /// Whip's Get Rotation Angles Method v14 - 9/25/18 ///
        Dependencies: AngleBetween
        */

        static void GetRotationAngles(ref Vector3D targetVector, ref MatrixD worldMatrix, out double yaw, out double pitch)
        {
            var localTargetVector = Vector3D.Rotate(targetVector, MatrixD.Transpose(worldMatrix));
            var flattenedTargetVector = new Vector3D(localTargetVector.X, 0, localTargetVector.Z);
            yaw = AngleBetween(Vector3D.Forward, flattenedTargetVector) * -Math.Sign(localTargetVector.X); //right is negative

            if (Vector3D.IsZero(flattenedTargetVector)) //check for straight up case
                pitch = MathHelper.PiOver2 * Math.Sign(localTargetVector.Y);
            else
                pitch = AngleBetween(localTargetVector, flattenedTargetVector) * Math.Sign(localTargetVector.Y); //up is positive
        }

        /// <summary>
        /// Computes angle between 2 vectors
        /// </summary>
        public static double AngleBetween(Vector3D a, Vector3D b) //returns radians
        {
            if (Vector3D.IsZero(a) || Vector3D.IsZero(b))
                return 0;
            else
                return Math.Acos(MathHelper.Clamp(a.Dot(b) / Math.Sqrt(a.LengthSquared() * b.LengthSquared()), -1, 1));
        }

        public Vector3D GetPredictedTargetPosition(MyEntity target)
        {
            var thisTick = Comp.MyAi.MySession.Tick;
            if (thisTick == _lastPredictionTick && _lastTarget == target)
                return _lastPredictedPos;
            _lastTarget = target;
            _lastPredictionTick = thisTick;
            if (target == null || target.MarkedForClose)
            {
                _lastPredictedPos = Vector3D.Zero;
                return _lastPredictedPos;
            }
            var center = target.PositionComp.WorldAABB.Center;
            var deltaPos = center - Comp.MyPivotPos;
            var projectileVel = WeaponType.AmmoDef.DesiredSpeed;
            var targetVel = Vector3.Zero;
            if (target.Physics != null)
            {
                targetVel = target.Physics.LinearVelocity;
            }
            else
            {
                var topMostParent = target.GetTopMostParent();
                if (topMostParent?.Physics != null)
                    targetVel = topMostParent.Physics.LinearVelocity;
            }
            var myVel = Comp.Physics.LinearVelocity;
            var deltaVel = targetVel - myVel;
            var timeToIntercept = Intercept(deltaPos, deltaVel, projectileVel);
            // IFF timeToIntercept is less than 0, intercept is not possible!!!
            _lastPredictedPos = center + (float)timeToIntercept * deltaVel;
            return _lastPredictedPos;
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

        internal void InitTracking()
        {
            _randomStandbyChange_ms = MyAPIGateway.Session.ElapsedPlayTime.Milliseconds;
            _randomStandbyChangeConst_ms = MyUtils.GetRandomInt(3500, 4500);
            _randomStandbyRotation = 0.0f;
            _randomStandbyElevation = 0.0f;

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

        public static bool NearlyEqual(double f1, double f2)
        {
            // Equal if they are within 0.00001 of each other
            return Math.Abs(f1 - f2) < 0.00001;
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
            double num = a.LengthSquared() * b.LengthSquared() * tolerance * Math.Sign(tolerance);
            return Math.Sign(dot) * dot > num;
        }

        bool sameSign(float num1, double num2)
        {
            if (num1 > 0 && num2 < 0)
                return false;
            if (num1 < 0 && num2 > 0)
                return false;
            return true;
        }

        private int _randomStandbyChange_ms;
        private int _randomStandbyChangeConst_ms;
        private float _randomStandbyRotation;
        private float _randomStandbyElevation;
        /*
        private Vector3 LookAt(Vector3D target)
        {
            var muzzleWorldPosition = Comp.MyPivotPos;
            float azimuth;
            float elevation;
            Vector3.GetAzimuthAndElevation(Vector3.Normalize(Vector3D.TransformNormal(target - muzzleWorldPosition, Comp.MyCube.PositionComp.WorldMatrixInvScaled)), out azimuth, out elevation);
            if (_gunIdleElevationAzimuthUnknown)
            {
                Vector3.GetAzimuthAndElevation(EntityPart.LocalMatrix.Forward, out _gunIdleAzimuth, out _gunIdleElevation);
                _gunIdleElevationAzimuthUnknown = false;
            }
            return new Vector3(elevation - _gunIdleElevation, MathHelper.WrapAngle(azimuth - _gunIdleAzimuth), 0.0f);
        }

        private void RandomMovement()
        {
            if (!_enableIdleRotation || Gunner)
                return;
            ResetRandomAiming();
            var randomStandbyRotation = _randomStandbyRotation;
            var standbyElevation = _randomStandbyElevation;
            var max1 = RotationSpeed * 16f;
            var flag = false;
            var rotation = Azimuth;
            var num1 = (randomStandbyRotation - rotation);
            if (num1 * num1 > 9.99999943962493E-11)
            {
                Azimuth += MathHelper.Clamp(num1, -max1, max1);
                flag = true;
            }
            if (standbyElevation > BarrelElevationMin)
            {
                var max2 = ElevationSpeed * 16f;
                var num2 = standbyElevation - Elevation;
                if (num2 * num2 > 9.99999943962493E-11)
                {
                    Elevation += MathHelper.Clamp(num2, -max2, max2);
                    flag = true;
                }
            }
            //this.m_playAimingSound = flag;
            ClampRotationAndElevation();
            if (_randomIsMoving)
            {
                if (flag)
                    return;
                //this.SetupSearchRaycast();
                _randomIsMoving = false;
            }
            else
            {
                if (!flag)
                    return;
                _randomIsMoving = true;
            }
        }

        private void SetupSearchRaycast()
        {
            MatrixD muzzleWorldMatrix = this.m_gunBase.GetMuzzleWorldMatrix();
            Vector3D vector3D = muzzleWorldMatrix.Translation + muzzleWorldMatrix.Forward * (double)this.m_searchingRange;
            this.m_laserLength = (double)this.m_searchingRange;
            this.m_hitPosition = vector3D;
        }

        private void GetCameraDummy()
        {
            if (this.m_base2.Model == null || !this.m_base2.Model.Dummies.ContainsKey("camera"))
                return;
            this.CameraDummy = this.m_base2.Model.Dummies["camera"];
        }

        public void UpdateVisual()
        {
            base.UpdateVisual();
            this.m_transformDirty = true;
        }
        */
    }
}
