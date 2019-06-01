using System;
using Sandbox;
using Sandbox.Definitions;
using Sandbox.Game.Weapons;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;
using VRageRender;
using IMyLargeTurretBase = Sandbox.ModAPI.IMyLargeTurretBase;
using WeaponCore.Support;

namespace WeaponCore.Platform
{
    public partial class Weapon
    {
        internal void Rotate(float speed)
        {
            var myCube = Comp.MyCube;
            var myMatrix = myCube.PositionComp.WorldMatrix;
            var targetPos = Target.PositionComp.WorldAABB.Center;
            var myPivotPos = myCube.PositionComp.WorldAABB.Center;
            myPivotPos += myMatrix.Up * _upPivotOffsetLen;
            MyPivotPos = myPivotPos;
            var predictedPos = GetPredictedTargetPosition(Target);
            GetTurretAngles(ref predictedPos, ref MyPivotPos, Comp.Turret, speed, out _azimuth, out _elevation, out _desiredAzimuth, out _desiredElevation);
            //GetTurretAngles2(ref targetPos, ref myPivotPos, ref myMatrix, out _azimuth, out _elevation);
            var azDiff = 100 * (_desiredAzimuth - _azimuth) / _azimuth;
            var elDiff = 100 * (_desiredElevation - _elevation) / _elevation;

            _azOk = azDiff > -101 && azDiff < -99 || azDiff > -1 && azDiff < 1;
            _elOk = elDiff > -101 && elDiff < -99 || elDiff > -1 && elDiff < 1;
            Comp.Turret.Azimuth = (float)_azimuth;
            Comp.Turret.Elevation = (float)_elevation;
            //Log.Line($"{_azimuth}({azDiff})[{_desiredAzimuth}] - {_elevation}({elDiff})[{_desiredElevation}]");
        }

        internal void GetTurretAngles(ref Vector3D targetPositionWorld, ref Vector3D turretPivotPointWorld, IMyLargeTurretBase turret, double maxAngularStep, out double azimuth, out double elevation, out double desiredAzimuth, out double desiredElevation)
        {
            // Get current turret facing
            Vector3D currentVector;
            Vector3D.CreateFromAzimuthAndElevation(turret.Azimuth, turret.Elevation, out currentVector);
            currentVector = Vector3D.Rotate(currentVector, turret.WorldMatrix);

            var up = turret.WorldMatrix.Up;
            var left = Vector3D.Cross(up, currentVector);
            if (!Vector3D.IsUnit(ref left) && !Vector3D.IsZero(left))
                left.Normalize();
            var forward = Vector3D.Cross(left, up);

            var matrix = new MatrixD()
            {
                Forward = forward,
                Left = left,
                Up = up,
            };

            // Get desired angles
            var targetDirection = targetPositionWorld - turretPivotPointWorld;
            GetRotationAngles(ref targetDirection, ref matrix, out desiredAzimuth, out desiredElevation);

            // Get control angles
            azimuth = turret.Azimuth + MathHelper.Clamp(desiredAzimuth, -maxAngularStep, maxAngularStep);
            elevation = turret.Elevation + MathHelper.Clamp(desiredElevation - turret.Elevation, -maxAngularStep, maxAngularStep);
        }

        /*
        /// Whip's Get Rotation Angles Method v14 - 9/25/18 ///
        Dependencies: AngleBetween
        */

        void GetRotationAngles(ref Vector3D targetVector, ref MatrixD worldMatrix, out double yaw, out double pitch)
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

        void GetTurretAngles2(ref Vector3D targetPositionWorld, ref Vector3D turretPivotPointWorld, ref MatrixD turretWorldMatrix, out double azimuth, out double elevation)
        {
            Vector3D localTargetPosition = targetPositionWorld - turretPivotPointWorld;
            GetRotationAngles2(ref localTargetPosition, ref turretWorldMatrix, out azimuth, out elevation);
        }

        /*
        /// Whip's Get Rotation Angles Method v14 - 9/25/18 ///
        Dependencies: AngleBetween
        */
        void GetRotationAngles2(ref Vector3D targetVector, ref MatrixD worldMatrix, out double yaw, out double pitch)
        {
            var localTargetVector = Vector3D.Rotate(targetVector, MatrixD.Transpose(worldMatrix));
            var flattenedTargetVector = new Vector3D(localTargetVector.X, 0, localTargetVector.Z);

            yaw = AngleBetween(Vector3D.Forward, flattenedTargetVector) * -Math.Sign(localTargetVector.X); //right is negative
            if (Math.Abs(yaw) < 1E-6 && localTargetVector.Z > 0) //check for straight back case
                yaw = Math.PI;

            if (Vector3D.IsZero(flattenedTargetVector)) //check for straight up case
                pitch = MathHelper.PiOver2 * Math.Sign(localTargetVector.Y);
            else
                pitch = AngleBetween(localTargetVector, flattenedTargetVector) * Math.Sign(localTargetVector.Y); //up is positive
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

        public Vector3D GetPredictedTargetPosition(MyEntity target)
        {
            var thisTick = Comp.MyAi.MySession.Tick;
            if (thisTick == _lastPredictionTick && _lastTarget == target)
                return _lastPredictedPos;
            //if (thisTick != _lastPredictionTick)
                //this.m_muzzleWorldPosition = this.Turret.GunBase.GetMuzzleWorldPosition();
            _lastTarget = target;
            _lastPredictionTick = thisTick;
            if (target == null)
            {
                _lastPredictedPos = Vector3D.Zero;
                return _lastPredictedPos;
            }
            if (target.MarkedForClose)
            {
                _lastPredictedPos = target.PositionComp.GetPosition();
                return _lastPredictedPos;
            }
            var center = target.PositionComp.WorldAABB.Center;
            var deltaPos = center - MyPivotPos;
            //if (true)
            {
                DsDebugDraw.DrawLine(MyPivotPos, center, Color.Lime, 0.1f);
            }
            var projectileVel = 0.0f;
            var num1 = 0.0f;

            projectileVel = WeaponType.DesiredSpeed;
            num1 = WeaponType.MaxTrajectory;

            num1 += WeaponType.AreaEffectRadius;
            var flag = !WeaponType.SkipAcceleration;

            var num2 = projectileVel < 9.99999974737875E-06 ? 1E-06f : num1 / projectileVel;
            var vector3_1 = Vector3.Zero;
            if (target.Physics != null)
            {
                vector3_1 = target.Physics.LinearVelocity;
            }
            else
            {
                var topMostParent = target.GetTopMostParent();
                if (topMostParent?.Physics != null)
                    vector3_1 = topMostParent.Physics.LinearVelocity;
            }
            var vector3_2 = ((IMyCubeGrid)Comp.MyGrid)?.Physics.LinearVelocity ?? Vector3.Zero;
            var vector3_3 = vector3_1 - vector3_2;
            var num3 = MathHelper.Clamp(Intercept(deltaPos, vector3_3, projectileVel), 0.0, num2);
            var vector3D = center + (float)num3 * vector3_1;
            _lastPredictedPos = flag ? vector3D - (float)num3 / num2 * vector3_2 : vector3D - (float)num3 * vector3_2;
            //if (true)
            {
                DsDebugDraw.DrawLine(MyPivotPos, _lastPredictedPos, Color.Orange, 0.1f);
            }
            return _lastPredictedPos;
        }

        private double Intercept(Vector3D deltaPos, Vector3D deltaVel, float projectileVel)
        {
            var num1 = Vector3D.Dot(deltaVel, deltaVel) - projectileVel * projectileVel;
            var num2 = 2.0 * Vector3D.Dot(deltaVel, deltaPos);
            var num3 = Vector3D.Dot(deltaPos, deltaPos);
            var d = num2 * num2 - 4.0 * num1 * num3;
            if (d <= 0.0)
                return -1.0;
            return 2.0 * num3 / (Math.Sqrt(d) - num2);
        }
    }
}
