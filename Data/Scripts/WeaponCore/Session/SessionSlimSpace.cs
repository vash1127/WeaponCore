using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;
using WeaponCore.Support;

namespace WeaponCore
{
    public partial class Session
    {
        private readonly HashSet<IMySlimBlock> _slimsSet = new HashSet<IMySlimBlock>();
        private readonly List<(Vector3I, IMySlimBlock, Vector3I)> _slimsSortedList = new List<(Vector3I, IMySlimBlock, Vector3I)>();
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

        static void GetIntVectorsInSphere(MyCubeGrid grid, Vector3I center, double radius, List<(Vector3I, IMySlimBlock, Vector3I)> points)
        {
            points.Clear();
            radius *= grid.GridSizeR;
            double radiusSq = radius * radius;
            int radiusCeil = (int)Math.Ceiling(radius);
            int i, j, k;
            for (i = -radiusCeil; i <= radiusCeil; ++i)
            {
                for (j = -radiusCeil; j <= radiusCeil; ++j)
                {
                    for (k = -radiusCeil; k <= radiusCeil; ++k)
                    {
                        if (i * i + j * j + k * k < radiusSq)
                        {
                            var vector3I = center + new Vector3I(i, j, k);
                            IMySlimBlock slim = grid.GetCubeBlock(vector3I);
                            if (slim != null) points.Add((center, slim, vector3I));
                        }
                    }
                }
            }
        }

        private void GenerateBlockSphere(MyCubeSize gridSizeEnum, double radiusInMeters)
        {
            double gridSizeInv = 2.0; // Assume small grid (1 / 0.5)
            if (gridSizeEnum == MyCubeSize.Large)
                gridSizeInv = 0.4; // Large grid (1 / 2.5)

            double radiusInBlocks = radiusInMeters * gridSizeInv;
            double radiusSq = radiusInBlocks * radiusInBlocks;
            int radiusCeil = (int)Math.Ceiling(radiusInBlocks);
            int i, j, k;
            Vector3I max = Vector3I.One * radiusCeil;
            Vector3I min = Vector3I.One * -radiusCeil;

            var blockSphereLst = _blockSpherePool.Get();
            for (i = min.X; i <= max.X; ++i)
            for (j = min.Y; j <= max.Y; ++j)
            for (k = min.Z; k <= max.Z; ++k)
                if (i * i + j * j + k * k < radiusSq)
                    blockSphereLst.Add(new Vector3I(i, j, k));

            blockSphereLst.Sort((a, b) => Vector3I.Dot(a, a).CompareTo(Vector3I.Dot(b, b)));
            /* Change which DB we add to based on the grid size
             * NOTE:
             * We should key with either radiusInMeters or radiusInBlocks.
             * The former is more intuitive to me.
             */
            if (gridSizeEnum == MyCubeSize.Large)
                LargeBlockSphereDb.Add(radiusInMeters, blockSphereLst);
            else
                SmallBlockSphereDb.Add(radiusInMeters, blockSphereLst);
        }


        private void ShiftAndPruneBlockSphere(MyCubeGrid grid, Vector3I center, List<Vector3I> sphereOfCubes, List<(Vector3I, IMySlimBlock, Vector3I)> slims)
        {
            slims.Clear();
            for (int i = 0; i < sphereOfCubes.Count; i++)
            {
                var vector3I = center + sphereOfCubes[i];
                IMySlimBlock slim = grid.GetCubeBlock(vector3I);
                if (slim != null && slim.Position == vector3I) slims.Add((center, slim, vector3I));
            }
        }

        private void GetIntVectorsInSphere2(MyCubeGrid grid, Vector3I center, double radius, List<(Vector3I, IMySlimBlock, Vector3I)> points)
        {
            points.Clear();
            radius *= grid.GridSizeR;
            var gridMin = grid.Min;
            var gridMax = grid.Max;
            double radiusSq = radius * radius;
            int radiusCeil = (int)Math.Ceiling(radius);
            int i, j, k;
            Vector3I max = Vector3I.Min(Vector3I.One * radiusCeil, gridMax - center);
            Vector3I min = Vector3I.Max(Vector3I.One * -radiusCeil, gridMin - center);

            for (i = min.X; i <= max.X; ++i)
            {
                for (j = min.Y; j <= max.Y; ++j)
                {
                    for (k = min.Z; k <= max.Z; ++k)
                    {
                        if (i * i + j * j + k * k < radiusSq)
                        {
                            var vector3I = center + new Vector3I(i, j, k);
                            IMySlimBlock slim = grid.GetCubeBlock(vector3I);
                            if (slim != null && slim.Position == vector3I) points.Add((center, slim, vector3I));
                        }
                    }
                }
            }
            DsUtil.Start($"[sort] - radius:{radius} - time:", true);
            points.Sort((a, b) => Vector3I.DistanceManhattan(a.Item1, a.Item3).CompareTo(Vector3I.DistanceManhattan(b.Item1, b.Item3)));
            DsUtil.Complete();
        }

        public void GetBlocksInsideSphere(MyCubeGrid grid, Dictionary<Vector3I, IMySlimBlock> cubes, ref BoundingSphereD sphere, bool sorted, Vector3I center, bool checkTriangles = false)
        {
            if (grid.PositionComp == null) return;

            if (sorted) _slimsSortedList.Clear();
            else _slimsSet.Clear();

            var matrixNormalizedInv = grid.PositionComp.WorldMatrixNormalizedInv;
            Vector3D.Transform(ref sphere.Center, ref matrixNormalizedInv, out var result);
            var localSphere = new BoundingSphere(result, (float)sphere.Radius);
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
                    if (cubes.TryGetValue(next, out var cube))
                    {
                        if (new BoundingBox(cube.Min * grid.GridSize - grid.GridSizeHalf, cube.Max * grid.GridSize + grid.GridSizeHalf).Intersects(localSphere))
                        {
                            if (sorted) _slimsSortedList.Add((center, cube, cube.Position));
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
                        if (sorted) _slimsSortedList.Add((center, cube, cube.Position));
                        else _slimsSet.Add(cube);
                    }
                }
            }
            if (sorted) 
                _slimsSortedList.Sort((x, y) => Vector3I.DistanceManhattan(x.Item1, x.Item2.Position).CompareTo(Vector3I.DistanceManhattan(y.Item1, y.Item2.Position)));
        }
    }
}
