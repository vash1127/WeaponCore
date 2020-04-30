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
            var dist = Vector3.Distance(worldHitPos, ray.Position);
            return float.IsNaN(dist) ? (float?) null : dist;
        }

        public static bool PointInEllipsoid(Vector3D point, MatrixD ellipsoidMatrixInv)
        {
            return Vector3D.Transform(point, ellipsoidMatrixInv).LengthSquared() <= 1;
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


        public static Vector3D NearestPointOnLine(Vector3D start, Vector3D end, Vector3D pnt)
        {
            var line = (end - start);
            var len = line.Length();
            line.Normalize();

            var v = pnt - start;
            var d = Vector3.Dot(v, line);
            MathHelper.Clamp(d, 0f, len);
            return start + line * d;
        }

        /*
        ** Returns the point on the line formed by (point1 + dir1 * x) that is closest to the point
        ** on the line formed by line (point2 + dir2 * t)
        */

        public static Vector3D GetClosestPointOnLine1(Vector3D point1, Vector3D dir1, Vector3D point2, Vector3D dir2)
        {
            Vector3D axis = Vector3D.Cross(dir1, dir2);
            if (Vector3D.IsZero(axis))
                return point1;
            Vector3D perpDir2 = Vector3D.Cross(dir2, axis);
            Vector3D point1To2 = point2 - point1;
            return point1 + Vector3D.Dot(point1To2, perpDir2) / Vector3D.Dot(dir1, perpDir2) * dir1;
        }

        /*
        ** Returns the point on the line1 that is closest to the point on line2
        */

        public static Vector3D GetClosestPointOnLine2(Vector3D line1Start, Vector3D line1End, Vector3D line2Start, Vector3D line2End)
        {
            Vector3D dir1 = line1End - line1Start;
            Vector3D dir2 = line2End - line2Start;
            Vector3D axis = Vector3D.Cross(dir1, dir2);
            if (Vector3D.IsZero(axis))
                return line1Start;
            Vector3D perpDir2 = Vector3D.Cross(dir2, axis);
            Vector3D point1To2 = line2Start - line1Start;
            return line1Start + Vector3D.Dot(point1To2, perpDir2) / Vector3D.Dot(dir1, perpDir2) * dir1;
        }

        public static Vector3D VectorProjection(Vector3D a, Vector3D b)
        {
            if (Vector3D.IsZero(b))
                return Vector3D.Zero;

            return a.Dot(b) / b.LengthSquared() * b;
        }

        public static bool SameSign(float num1, double num2)
        {
            if (num1 > 0 && num2 < 0)
                return false;
            if (num1 < 0 && num2 > 0)
                return false;
            return true;
        }

        public static bool NearlyEqual(double f1, double f2)
        {
            // Equal if they are within 0.00001 of each other
            return Math.Abs(f1 - f2) < 0.00001;
        }


        public static double InverseSqrDist(Vector3D source, Vector3D target, double range)
        {
            var rangeSq = range * range;
            var distSq = (target - source).LengthSquared();
            if (distSq > rangeSq)
                return 0.0;
            return 1.0 - (distSq / rangeSq);
        }

        public static double GetIntersectingSurfaceArea(MatrixD matrix, Vector3D hitPosLocal)
        {
            var surfaceArea = -1d;

            var boxMax = matrix.Backward + matrix.Right + matrix.Up;
            var boxMin = -boxMax;
            var box = new BoundingBoxD(boxMin, boxMax);

            var maxWidth = box.Max.LengthSquared();
            var testLine = new LineD(Vector3D.Zero, Vector3D.Normalize(hitPosLocal) * maxWidth);
            LineD testIntersection;
            box.Intersect(ref testLine, out testIntersection);

            var intersection = testIntersection.To;

            var epsilon = 1e-6;
            var projFront = MathFuncs.VectorProjection(intersection, matrix.Forward);
            if (Math.Abs(projFront.LengthSquared() - matrix.Forward.LengthSquared()) < epsilon)
            {
                var a = Vector3D.Distance(matrix.Left, matrix.Right);
                var b = Vector3D.Distance(matrix.Up, matrix.Down);
                surfaceArea = a * b;
            }

            var projLeft = MathFuncs.VectorProjection(intersection, matrix.Left);
            if (Math.Abs(projLeft.LengthSquared() - matrix.Left.LengthSquared()) < epsilon)
            {
                var a = Vector3D.Distance(matrix.Forward, matrix.Backward);
                var b = Vector3D.Distance(matrix.Up, matrix.Down);
                surfaceArea = a * b;
            }

            var projUp = MathFuncs.VectorProjection(intersection, matrix.Up);
            if (Math.Abs(projUp.LengthSquared() - matrix.Up.LengthSquared()) < epsilon)
            {
                var a = Vector3D.Distance(matrix.Forward, matrix.Backward);
                var b = Vector3D.Distance(matrix.Left, matrix.Right);
                surfaceArea = a * b;
            }
            return surfaceArea;
        }

        public static void FibonacciSeq(int magicNum)
        {
            var root5 = Math.Sqrt(5);
            var phi = (1 + root5) / 2;

            var n = 0;
            int Fn;
            do
            {
                Fn = (int)((Math.Pow(phi, n) - Math.Pow(-phi, -n)) / ((2 * phi) - 1));
                //Console.Write("{0} ", Fn);
                ++n;
            }
            while (Fn < magicNum);
        }

        public static double LargestCubeInSphere(double r)
        {

            // radius cannot be negative  
            if (r < 0)
                return -1;

            // side of the cube  
            var a = (2 * r) / Math.Sqrt(3);
            return a;
        }

        public static double AreaCube(double a)
        {
            return (a * a * a);
        }

        public static double SurfaceCube(double a)
        {
            return (6 * a * a);
        }

        public static double VolumeCube(double len)
        {
            return Math.Pow(len, 3);
        }

        public static double Percentile(double[] sequence, double excelPercentile)
        {
            Array.Sort(sequence);
            int N = sequence.Length;
            double n = (N - 1) * excelPercentile + 1;
            // Another method: double n = (N + 1) * excelPercentile;
            if (n == 1d) return sequence[0];
            else if (n == N) return sequence[N - 1];
            else
            {
                int k = (int)n;
                double d = n - k;
                return sequence[k - 1] + d * (sequence[k] - sequence[k - 1]);
            }
        }

        public static double GetMedian(int[] array)
        {
            int[] tempArray = array;
            int count = tempArray.Length;

            Array.Sort(tempArray);

            double medianValue = 0;

            if (count % 2 == 0)
            {
                // count is even, need to get the middle two elements, add them together, then divide by 2
                int middleElement1 = tempArray[(count / 2) - 1];
                int middleElement2 = tempArray[(count / 2)];
                medianValue = (middleElement1 + middleElement2) / 2;
            }
            else
            {
                // count is odd, simply get the middle element.
                medianValue = tempArray[(count / 2)];
            }

            return medianValue;
        }

        public static double Map(double value, double from1, double to1, double from2, double to2)
        {
            return (value - from1) / (to1 - from1) * (to2 - from2) + from2;
        }

        public static float Map(float value, float from1, float to1, float from2, float to2)
        {
            return (value - from1) / (to1 - from1) * (to2 - from2) + from2;
        }
    }

}
