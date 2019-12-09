using System.Collections.Generic;
using Sandbox.Game.Entities;
using VRage.ModAPI;
using VRage.Utils;
using VRage.Voxels;
using VRageMath;

namespace WeaponCore.Support
{
    internal static class VoxelIntersect
    {
        internal static bool PosHasVoxel(MyVoxelBase voxel, Vector3D testPos)
        {
            var planet = voxel as MyPlanet;
            var map = voxel as MyVoxelMap;
            var hit = new VoxelHit();

            if (planet != null)
            {
                var from = testPos;
                var localPosition = (Vector3)(from - planet.PositionLeftBottomCorner);
                var v = localPosition / 1f;
                Vector3I voxelCoord;
                Vector3I.Floor(ref v, out voxelCoord);
                planet.Storage.ExecuteOperationFast(ref hit, MyStorageDataTypeFlags.Content, ref voxelCoord, ref voxelCoord, notifyRangeChanged: false);
            }
            else if (map != null)
            {
                var from = testPos;
                var localPosition = (Vector3)(from - map.PositionLeftBottomCorner);
                var v = localPosition / 1f;
                Vector3I voxelCoord;
                Vector3I.Floor(ref v, out voxelCoord);
                map.Storage.ExecuteOperationFast(ref hit, MyStorageDataTypeFlags.Content, ref voxelCoord, ref voxelCoord, notifyRangeChanged: false);
            }

            return hit.HasHit;
        }

        internal static bool CheckPointsOnLine (MyVoxelBase voxel, LineD testLine, int distBetweenPoints)
        {
            var planet = voxel as MyPlanet;
            var map = voxel as MyVoxelMap;
            var hit = new VoxelHit();
            var checkPoints = (int)(testLine.Length / distBetweenPoints);
            for (int i = 0; i < checkPoints; i++)
            {
                var testPos = testLine.From + (testLine.Direction * (distBetweenPoints * i));
                //Log.Line($"i: {i} - lookAhead:{(distBetweenPoints * i)}");
                if (planet != null)
                {
                    var from = testPos;
                    var localPosition = (Vector3)(from - planet.PositionLeftBottomCorner);
                    var v = localPosition / 1f;
                    Vector3I voxelCoord;
                    Vector3I.Floor(ref v, out voxelCoord);
                    planet.Storage.ExecuteOperationFast(ref hit, MyStorageDataTypeFlags.Content, ref voxelCoord, ref voxelCoord, notifyRangeChanged: false);
                    if (hit.HasHit) return true;
                }
                else if (map != null)
                {
                    var from = testPos;
                    var localPosition = (Vector3)(from - map.PositionLeftBottomCorner);
                    var v = localPosition / 1f;
                    Vector3I voxelCoord;
                    Vector3I.Floor(ref v, out voxelCoord);
                    map.Storage.ExecuteOperationFast(ref hit, MyStorageDataTypeFlags.Content, ref voxelCoord, ref voxelCoord, notifyRangeChanged: false);
                    if (hit.HasHit) return true;
                }
            }

            return false;
        }

        internal static bool CheckSurfacePointsOnLine(MyPlanet planet, LineD testLine, double distBetweenPoints)
        {
            var checkPoints = (int)((testLine.Length / distBetweenPoints) + distBetweenPoints);
            var lastPoint = (checkPoints - 1);

            for (int i = 0; i < checkPoints; i++)
            {
                var planetInPath = i != lastPoint;
                var extend = (distBetweenPoints * i);
                if (extend > testLine.Length) extend = testLine.Length;
                var testPos = testLine.From + (testLine.Direction * extend);

                if (planetInPath)
                {
                    var closestSurface = planet.GetClosestSurfacePointGlobal(ref testPos);
                    double surfaceDistToTest;
                    Vector3D.DistanceSquared(ref closestSurface, ref testPos, out surfaceDistToTest);
                    if (surfaceDistToTest < 4) return true;
                }

                if (!planetInPath)
                {
                    Vector3D? voxelHit = null;
                    var closestSurface = planet.GetClosestSurfacePointGlobal(ref testPos);
                    var reverseLine = testLine;
                    reverseLine.Direction = -reverseLine.Direction;
                    reverseLine.From = testPos;
                    reverseLine.To = reverseLine.From + (reverseLine.Direction * (Vector3D.Distance(closestSurface, reverseLine.From) + distBetweenPoints));
                    planet.GetIntersectionWithLine(ref reverseLine, out voxelHit);
                    return voxelHit.HasValue;
                }

            }
            return false;
        }

        internal static Vector3D? ProcessVoxel(LineD trajectile, MyVoxelBase voxel, WeaponSystem system, List<Vector3I> testPoints)
        {
            var planet = voxel as MyPlanet;
            var voxelMap = voxel as MyVoxelMap;
            var ray = new RayD(trajectile.From, trajectile.Direction);
            var voxelAABB = voxel.PositionComp.WorldAABB;
            var rayVoxelDist = ray.Intersects(voxelAABB);
            if (rayVoxelDist.HasValue)
            {
                var voxelMaxLen = voxel.PositionComp.WorldVolume.Radius * 2;
                var start = trajectile.From + (ray.Direction * rayVoxelDist.Value);
                var lenRemain = trajectile.Length - rayVoxelDist.Value;
                var end = voxelMaxLen > lenRemain ? start + (ray.Direction * lenRemain) : start + (ray.Direction * voxelMaxLen);
                var testLine = new LineD(trajectile.From + (ray.Direction * rayVoxelDist.Value), end);
                var rotMatrix = Quaternion.CreateFromRotationMatrix(voxel.WorldMatrix);
                var obb = new MyOrientedBoundingBoxD(voxel.PositionComp.WorldAABB.Center, voxel.PositionComp.LocalAABB.HalfExtents, rotMatrix);
                if (obb.Intersects(ref testLine) != null)
                {
                    Log.Line("obb");
                    if (planet != null)
                    {
                        var startPos = trajectile.From - planet.PositionLeftBottomCorner;
                        var startInt = Vector3I.Round(startPos);
                        var endPos = trajectile.To - planet.PositionLeftBottomCorner;
                        var endInt = Vector3I.Round(endPos);

                        BresenhamLineDraw(startInt, endInt, testPoints);

                        for (int i = 0; i < testPoints.Count; ++i)
                        {
                            var voxelCoord = testPoints[i];
                            var voxelHit = new VoxelHit();
                            planet.Storage.ExecuteOperationFast(ref voxelHit, MyStorageDataTypeFlags.Content, ref voxelCoord, ref voxelCoord, notifyRangeChanged: false);
                            if (voxelHit.HasHit)
                                return (Vector3D)voxelCoord + planet.PositionLeftBottomCorner;
                        }
                    }
                    else if (voxelMap != null)
                    {
                        var startPos = trajectile.From - voxelMap.PositionLeftBottomCorner;
                        var startInt = Vector3I.Round(startPos);
                        var endPos = trajectile.To - voxelMap.PositionLeftBottomCorner;
                        var endInt = Vector3I.Round(endPos);

                        BresenhamLineDraw(startInt, endInt, testPoints);

                        for (int i = 0; i < testPoints.Count; ++i)
                        {
                            var voxelCoord = testPoints[i];
                            var voxelHit = new VoxelHit();
                            voxelMap.Storage.ExecuteOperationFast(ref voxelHit, MyStorageDataTypeFlags.Content, ref voxelCoord, ref voxelCoord, notifyRangeChanged: false);
                            if (voxelHit.HasHit)
                                return (Vector3D)voxelCoord + voxelMap.PositionLeftBottomCorner;
                        }
                    }
                }
            }

            return null;
        }

        // Math magic by Whiplash
        internal static void BresenhamLineDraw(Vector3I start, Vector3I end, List<Vector3I> points)
        {
            points.Clear();
            points.Add(start);
            Vector3I delta = end - start;
            Vector3I step = Vector3I.Sign(delta);
            delta *= step;
            int max = delta.AbsMax();

            if (max == delta.X)
            {
                int p1 = 2 * delta.Y - delta.X;
                int p2 = 2 * delta.Z - delta.X;
                while (start.X != end.X)
                {
                    start.X += step.X;
                    if (p1 >= 0)
                    {
                        start.Y += step.Y;
                        p1 -= 2 * delta.X;
                    }

                    if (p2 >= 0)
                    {
                        start.Z += step.Z;
                        p2 -= 2 * delta.X;
                    }
                    p1 += 2 * delta.Y;
                    p2 += 2 * delta.Z;
                    points.Add(start);
                }
            }
            else if (max == delta.Y)
            {
                int p1 = 2 * delta.X - delta.Y;
                int p2 = 2 * delta.Z - delta.Y;
                while (start.Y != end.Y)
                {
                    start.Y += step.Y;
                    if (p1 >= 0)
                    {
                        start.X += step.X;
                        p1 -= 2 * delta.Y;
                    }

                    if (p2 >= 0)
                    {
                        start.Z += step.Z;
                        p2 -= 2 * delta.Y;
                    }
                    p1 += 2 * delta.X;
                    p2 += 2 * delta.Z;
                    points.Add(start);
                }
            }
            else
            {
                int p1 = 2 * delta.X - delta.Z;
                int p2 = 2 * delta.Y - delta.Z;
                while (start.Z != end.Z)
                {
                    start.Z += step.Z;
                    if (p1 >= 0)
                    {
                        start.X += step.X;
                        p1 -= 2 * delta.Z;
                    }

                    if (p2 >= 0)
                    {
                        start.Y += step.Y;
                        p2 -= 2 * delta.Z;
                    }
                    p1 += 2 * delta.X;
                    p2 += 2 * delta.Y;
                    points.Add(start);
                }
            }
        }

        internal struct VoxelHit : IVoxelOperator
        {
            internal bool HasHit;

            public void Op(ref Vector3I pos, MyStorageDataTypeEnum dataType, ref byte content)
            {
                if (content != MyVoxelConstants.VOXEL_CONTENT_EMPTY)
                {
                    HasHit = true;
                }
            }

            public VoxelOperatorFlags Flags
            {
                get { return VoxelOperatorFlags.Read; }
            }
        }
    }
}
