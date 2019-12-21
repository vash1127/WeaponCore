using System;
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
            var angPos = AngleBetween(cone.ConeDir, toSphere);
            double angRad = Math.Asin(targetSphere.Radius / toSphere.Length());

            var ang1 = angPos + angRad;
            var ang2 = angPos - angRad;

            if (ang1 < -cone.ConeAngle)
                return false; 

            if (ang2 > cone.ConeAngle)
                return false; 

            return true;
        }

        internal static float? IntersectEllipsoid(MatrixD ellipsoidMatrixInv, MatrixD ellipsoidMatrix, RayD ray)
        {
            var normSphere = new BoundingSphereD(Vector3.Zero, 1f);
            var kRay = new RayD(Vector3D.Zero, Vector3D.Forward);

            var krayPos = Vector3D.Transform(ray.Position, ellipsoidMatrixInv);
            var krayDir = Vector3D.Normalize(Vector3D.TransformNormal(ray.Direction, ellipsoidMatrixInv));

            kRay.Direction = krayDir;
            kRay.Position = krayPos;
            var nullDist = normSphere.Intersects(kRay);
            if (!nullDist.HasValue) return null;

            var hitPos = krayPos + (krayDir * -nullDist.Value);
            var worldHitPos = Vector3D.Transform(hitPos, ellipsoidMatrix);
            return Vector3.Distance(worldHitPos, ray.Position);
        }

        internal static bool IsDotProductWithinTolerance(ref Vector3D targetDir, ref Vector3D refDir, double tolerance)
        {
            double dot = Vector3D.Dot(targetDir, refDir);
            double num = targetDir.LengthSquared() * refDir.LengthSquared() * tolerance * Math.Abs(tolerance);
            return Math.Abs(dot) * dot > num;
        }

        //Relative velocity proportional navigation
        //aka: Whip-Nav
        internal static Vector3D CalculateMissileIntercept(Vector3D targetPosition, Vector3D targetVelocity, Vector3D missilePos, Vector3D missileVelocity, double missileAcceleration, double compensationFactor = 1, double maxLateralThrustProportion = 0.5)
        {
            var missileToTarget = Vector3D.Normalize(targetPosition - missilePos);
            var relativeVelocity = targetVelocity - missileVelocity;
            var parallelVelocity = relativeVelocity.Dot(missileToTarget) * missileToTarget;
            var normalVelocity = (relativeVelocity - parallelVelocity);

            var normalMissileAcceleration = normalVelocity * compensationFactor;

            if (Vector3D.IsZero(normalMissileAcceleration))
                return missileToTarget * missileAcceleration;

            double maxLateralThrust = missileAcceleration * Math.Min(1, Math.Max(0, maxLateralThrustProportion));
            if (normalMissileAcceleration.LengthSquared() > maxLateralThrust * maxLateralThrust)
            {
                Vector3D.Normalize(ref normalMissileAcceleration, out normalMissileAcceleration);
                normalMissileAcceleration *= maxLateralThrust;
            }
            double diff = missileAcceleration * missileAcceleration - normalMissileAcceleration.LengthSquared();
            var maxedDiff = Math.Max(0, diff);
            return Math.Sqrt(maxedDiff) * missileToTarget + normalMissileAcceleration;
        }

        /*
        /// Whip's Get Rotation Angles Method v14 - 9/25/18 ///
        Dependencies: AngleBetween
        */

        internal static double AngleBetween(Vector3D a, Vector3D b) //returns radians
        {
            if (Vector3D.IsZero(a) || Vector3D.IsZero(b))
                return 0;

            return Math.Acos(MathHelperD.Clamp(a.Dot(b) / Math.Sqrt(a.LengthSquared() * b.LengthSquared()), -1, 1));
        }

        internal static void GetRotationAngles(ref Vector3D targetVector, ref MatrixD matrix, out double yaw, out double pitch)
        {
            var localTargetVector = Vector3D.TransformNormal(targetVector, MatrixD.Transpose(matrix));
            var flattenedTargetVector = new Vector3D(localTargetVector.X, 0, localTargetVector.Z);
            yaw = AngleBetween(Vector3D.Forward, flattenedTargetVector) * -Math.Sign(localTargetVector.X); //right is positive
            if (Math.Abs(yaw) < 1E-6 && localTargetVector.Z > 0) //check for straight back case
                yaw = Math.PI;
            if (Vector3D.IsZero(flattenedTargetVector)) //check for straight up case
                pitch = MathHelper.PiOver2 * Math.Sign(localTargetVector.Y);
            else
                pitch = AngleBetween(localTargetVector, flattenedTargetVector) * Math.Sign(localTargetVector.Y); //up is positive
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

        internal static void WrapAngleAroundPI(ref float angle)
        {
            angle %= MathHelper.TwoPi;
            if (angle > Math.PI)
                angle = -MathHelper.TwoPi + angle;
            else if (angle < -Math.PI)
                angle = MathHelper.TwoPi + angle;
        }

        internal static double CalculateRotorDeviationAngle(Vector3D forwardVector, MatrixD lastOrientation)
        {
            var flattenedForwardVector = Rejection(forwardVector, lastOrientation.Up);
            return AngleBetween(flattenedForwardVector, lastOrientation.Forward) * Math.Sign(flattenedForwardVector.Dot(lastOrientation.Left));
        }

        internal static void GetAzimuthAngle(ref Vector3D targetVector, ref MatrixD matrix, out double azimuth)
        {
            var localTargetVector = Vector3D.TransformNormal(targetVector, MatrixD.Transpose(matrix));
            var flattenedTargetVector = new Vector3D(localTargetVector.X, 0, localTargetVector.Z);
            azimuth = AngleBetween(Vector3D.Forward, flattenedTargetVector) * -Math.Sign(localTargetVector.X); //right is positive
            if (Math.Abs(azimuth) < 1E-6 && localTargetVector.Z > 0) //check for straight back case
                azimuth = Math.PI;
        }
        internal static void GetElevationAngle(ref Vector3D targetVector, ref MatrixD matrix, out double pitch)
        {
            var localTargetVector = Vector3D.TransformNormal(targetVector, MatrixD.Transpose(matrix));
            var flattenedTargetVector = new Vector3D(localTargetVector.X, 0, localTargetVector.Z);
            if (Vector3D.IsZero(flattenedTargetVector)) //check for straight up case
                pitch = MathHelper.PiOver2 * Math.Sign(localTargetVector.Y);
            else
                pitch = AngleBetween(localTargetVector, flattenedTargetVector) * Math.Sign(localTargetVector.Y); //up is positive
        }

        internal static Vector3D SafeNormalize(Vector3D a)
        {
            if (Vector3D.IsZero(a)) return Vector3D.Zero; 
            if (Vector3D.IsUnit(ref a)) return a; 
            return Vector3D.Normalize(a);
        }

        internal static Vector3D Reflection(Vector3D a, Vector3D b, double rejectionFactor = 1) //reflect a over b
        {
            Vector3D project_a = Projection(a, b); 
            Vector3D reject_a = a - project_a; 
            return project_a - reject_a * rejectionFactor;
        }

        internal static Vector3D Rejection(Vector3D a, Vector3D b) //reject a on b
        {
            if (Vector3D.IsZero(a) || Vector3D.IsZero(b)) 
                return Vector3D.Zero; 
            return a - a.Dot(b) / b.LengthSquared() * b;
        }

        internal static Vector3D Projection(Vector3D a, Vector3D b)
        {
            if (Vector3D.IsZero(a) || Vector3D.IsZero(b)) 
                return Vector3D.Zero; 
            if (Vector3D.IsUnit(ref b)) 
                return a.Dot(b) * b; 

            return a.Dot(b) / b.LengthSquared() * b;
        }

        internal static double ScalarProjection(Vector3D a, Vector3D b)
        {
            if (Vector3D.IsZero(a) || Vector3D.IsZero(b)) return 0; 
            if (Vector3D.IsUnit(ref b)) 
                return a.Dot(b); 
            return a.Dot(b) / b.Length();
        }



        public static double CosBetween(Vector3D a, Vector3D b)
        {
            if (Vector3D.IsZero(a) || Vector3D.IsZero(b)) 
                return 0; 
            else 
                return MathHelper.Clamp(a.Dot(b) / Math.Sqrt(a.LengthSquared() * b.LengthSquared()), -1, 1);
        }

    }
}
