using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using VRage;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;
using WeaponCore.Support;

namespace WeaponCore
{
    public partial class Session
    {
        private readonly HashSet<IMySlimBlock> _slimsSet = new HashSet<IMySlimBlock>();
        private readonly List<MyTuple<Vector3I, IMySlimBlock>> _slimsSortedList = new List<MyTuple<Vector3I, IMySlimBlock>>();

        private void AddToSlimSpace(MyEntity entity)
        {
            var grid = (MyCubeGrid)entity;
            var slimDict = _slimSpacePool.Get();
            foreach (IMySlimBlock slim in grid.CubeBlocks)
                slimDict.Add(slim.Position, slim);

            SlimSpace.Add(grid, slimDict);
            grid.OnBlockAdded += BlockAdd;
            grid.OnBlockRemoved += BlockRemove;
        }

        private void RemoveFromSlimSpace(MyEntity entity)
        {
            var grid = (MyCubeGrid)entity;
            var dict = SlimSpace[grid];
            dict.Clear();
            _slimSpacePool.Return(dict);

            SlimSpace.Remove(entity);
            grid.OnBlockAdded -= BlockAdd;
            grid.OnBlockRemoved -= BlockRemove;
        }

        private void BlockAdd(IMySlimBlock slim)
        {
            var grid = slim.CubeGrid as MyCubeGrid;
            SlimSpace[grid].Add(slim.Position, slim);
        }

        private void BlockRemove(IMySlimBlock slim)
        {
            var grid = slim.CubeGrid as MyCubeGrid;
            SlimSpace[grid].Remove(slim.Position);
        }


        public void GetBlocksInsideSphere(MyCubeGrid grid, Dictionary<Vector3I, IMySlimBlock> cubes, ref BoundingSphereD sphere, bool sorted, Vector3I center, bool checkTriangles = false)
        {
            if (grid.PositionComp == null) return;

            if (sorted) _slimsSortedList.Clear();
            else _slimsSet.Clear();

            var fromSphere1 = BoundingBoxD.CreateFromSphere(sphere);
            var matrixNormalizedInv = grid.PositionComp.WorldMatrixNormalizedInv;
            Vector3D result;
            Vector3D.Transform(ref sphere.Center, ref matrixNormalizedInv, out result);
            var localSphere = new BoundingSphere((Vector3)result, (float)sphere.Radius);
            var fromSphere2 = BoundingBox.CreateFromSphere(localSphere);
            var min = (Vector3D)fromSphere2.Min;
            var max = (Vector3D)fromSphere2.Max;
            var vector3I1 = new Vector3I((int)Math.Round(min.X * grid.GridSizeR), (int)Math.Round(min.Y * grid.GridSizeR), (int)Math.Round(min.Z * grid.GridSizeR));
            var vector3I2 = new Vector3I((int)Math.Round(max.X * grid.GridSizeR), (int)Math.Round(max.Y * grid.GridSizeR), (int)Math.Round(max.Z * grid.GridSizeR));
            var start = Vector3I.Min(vector3I1, vector3I2);
            var end = Vector3I.Max(vector3I1, vector3I2);
            if ((end - start).Volume() < cubes.Count)
            {
                var vector3IRangeIterator = new Vector3I_RangeIterator(ref start, ref end);
                var next = vector3IRangeIterator.Current;
                while (vector3IRangeIterator.IsValid())
                {
                    IMySlimBlock cube;
                    if (cubes.TryGetValue(next, out cube))
                    {
                        if (new BoundingBox(cube.Min * grid.GridSize - grid.GridSizeHalf, cube.Max * grid.GridSize + grid.GridSizeHalf).Intersects(localSphere))
                        {
                            if (sorted) _slimsSortedList.Add(new MyTuple<Vector3I, IMySlimBlock>(center, cube));
                            else _slimsSet.Add(cube);
                        }
                    }
                    vector3IRangeIterator.GetNext(out next);
                }
            }
            else
            {
                foreach (var cube in cubes.Values)
                {
                    if (new BoundingBox(cube.Min * grid.GridSize - grid.GridSizeHalf, cube.Max * grid.GridSize + grid.GridSizeHalf).Intersects(localSphere))
                    {
                        if (sorted) _slimsSortedList.Add(new MyTuple<Vector3I, IMySlimBlock>(center, cube));
                        else _slimsSet.Add(cube);
                    }
                }
            }

            if (sorted)
                _slimsSortedList.Sort((x, y) => Vector3I.DistanceManhattan(x.Item1, x.Item2.Position).CompareTo(Vector3I.DistanceManhattan(y.Item1, y.Item2.Position)));
        }

        private void AddBlockInSphere(MyCubeGrid grid, ref BoundingBoxD aabb, bool sorted, Vector3I center, bool checkTriangles, ref BoundingSphere localSphere, IMySlimBlock slim)
        {
            //if (!new BoundingBox(slim.Min * grid.GridSize - grid.GridSizeHalf, slim.Max * grid.GridSize + grid.GridSizeHalf).Intersects(localSphere))
                //return;
            if (checkTriangles)
            {
                if (slim.FatBlock != null && !slim.FatBlock.GetIntersectionWithAABB(ref aabb))
                    return;
                if (sorted) _slimsSortedList.Add(new MyTuple<Vector3I, IMySlimBlock>(center, slim));
                else _slimsSet.Add(slim);
            }
            else
            {
                if (sorted) _slimsSortedList.Add(new MyTuple<Vector3I, IMySlimBlock>(center, slim));
                else _slimsSet.Add(slim);
            }
        }

        internal struct SlimDistance
        {
            internal readonly Vector3I Center;
            internal readonly Vector3I Position;
            internal readonly IMySlimBlock Block;

            internal SlimDistance(Vector3I center, IMySlimBlock block)
            {
                Center = center;
                Block = block;
                Position = block.Position;
            }
        }
    }
}
