using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRageMath;

namespace WeaponCore.Support
{
    internal static class MathFuncs
    {
        internal struct Cone
        {
            internal Vector3D ConeDir;
            internal Vector3D ConeTip;
            internal double ConeAngle;
        }

        internal static bool TargetSphereInCone(ref BoundingSphereD targetSphere, ref Cone cone)
        {
            Vector3D toSphere = targetSphere.Center - cone.ConeTip;
            var angPos = MathHelperD.ToDegrees(AngleBetween(cone.ConeDir, toSphere));
            double angRad = MathHelperD.ToDegrees(Math.Asin(targetSphere.Radius / toSphere.Length()));

            var ang1 = angPos + angRad;
            var ang2 = angPos - angRad;

            if (ang1 < -cone.ConeAngle)
                return false; 

            if (ang2 > cone.ConeAngle)
                return false; 

            return true;
        }

        internal static bool IsDotProductWithinTolerance(ref Vector3D a, ref Vector3D b, double tolerance)
        {
            double dot = Vector3D.Dot(a, b);
            double num = a.LengthSquared() * b.LengthSquared() * tolerance * Math.Abs(tolerance);
            return Math.Abs(dot) * dot > num;
        }

        internal static float NormalizeAngle(int angle)
        {
            int num = angle % 360;
            if (num == 0 && angle != 0)
                return 360f;
            return num;
        }

        internal static double Intercept(Vector3D deltaPos, Vector3D deltaVel, float projectileVel)
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

        internal static double AngleBetween(Vector3D a, Vector3D b) //returns radians
        {
            if (Vector3D.IsZero(a) || Vector3D.IsZero(b))
                return 0;
            else
                return Math.Acos(MathHelperD.Clamp(a.Dot(b) / Math.Sqrt(a.LengthSquared() * b.LengthSquared()), -1, 1));
        }

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

        internal static double WrapAngle(double angle)
        {
            angle = Math.IEEERemainder(angle, MathHelperD.TwoPi);
            if (angle <= -Math.PI)
                angle += MathHelperD.TwoPi;
            else if (angle > Math.PI)
                angle -= MathHelperD.TwoPi;
            return angle;
        }

    }
}
