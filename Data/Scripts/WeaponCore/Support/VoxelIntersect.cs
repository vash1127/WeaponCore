using System.Collections.Generic;
using Sandbox.Game.Entities;
using VRage.ModAPI;
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
        internal static Vector3D? ProcessVoxel(LineD trajectile, MyVoxelBase voxel, WeaponSystem system, List<Vector3I> testPoints)
        {
            var planet = voxel as MyPlanet;
            var voxelMap = voxel as MyVoxelMap;
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
