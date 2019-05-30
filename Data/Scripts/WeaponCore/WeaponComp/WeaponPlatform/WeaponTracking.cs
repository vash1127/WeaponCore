using System;
using VRageMath;
using IMyLargeTurretBase = Sandbox.ModAPI.IMyLargeTurretBase;

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

            GetTurretAngles(ref targetPos, ref MyPivotPos, Comp.Turret, speed, out _azimuth, out _elevation, out _desiredAzimuth, out _desiredElevation);
            //GetTurretAngles2(ref targetPos, ref myPivotPos, ref myMatrix, out _azimuth, out _elevation);
            var azDiff = 100 * (_desiredAzimuth - _azimuth) / _azimuth;
            var elDiff = 100 * (_desiredElevation - _elevation) / _elevation;

            _azOk = azDiff > -101 && azDiff < -99 || azDiff > -1 && azDiff < 1;
            _elOk = elDiff > -101 && elDiff < -99 || elDiff > -1 && elDiff < 1;
            Comp.Turret.Azimuth = (float)_azimuth;
            Comp.Turret.Elevation = (float)_elevation;
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
    }
}
