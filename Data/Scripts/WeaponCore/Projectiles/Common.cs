using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Interfaces;
using VRage.Utils;
using VRageMath;
using WeaponCore.Support;
using static WeaponCore.Projectiles.Projectiles.HitEntity.Type;
namespace WeaponCore.Projectiles
{
    public partial class Projectiles
    {
        private void GetEntitiesInBlastRadius(Projectile projectile, int poolId)
        {
            var sphere = new BoundingSphereD(projectile.Position, projectile.Trajectile.System.Values.Ammo.AreaEffect.AreaEffectRadius);
            var checkList = CheckPool[poolId].Get();
            MyGamePruningStructure.GetAllTopMostEntitiesInSphere(ref sphere, checkList);
            foreach (var ent in checkList)
            {
                var blastLine = new LineD(projectile.Position, ent.PositionComp.WorldAABB.Center);
                GetAllEntitiesInLine(projectile, blastLine, null, poolId, true);
            }
            checkList.Clear();
            CheckPool[poolId].Return(checkList);
            var count = projectile.HitList.Count;
            if (!Session.Instance.DedicatedServer && count <= 0)
            {
                var hitEntity = HitEntityPool[poolId].Get();
                hitEntity.Clean();
                hitEntity.EventType = Proximity;
                hitEntity.Hit = false;
                hitEntity.HitPos = projectile.Position;
                projectile.HitList.Add(hitEntity);
                Hits.Enqueue(projectile);
            }
            else if (Session.Instance.IsServer && count > 0)
                Hits.Enqueue(projectile);
        }

        internal HitEntity GetAllEntitiesInLine(Projectile p, LineD beam, List<MyLineSegmentOverlapResult<MyEntity>> segmentList, int poolId, bool quickCheck = false)
        {
            var listCnt = segmentList?.Count ?? p.HitList.Count;
            var found = false;
            for (int i = 0; i < listCnt; i++)
            {
                var ent = segmentList != null ? segmentList[i].Element : p.HitList[i].Entity;
                if (ent == p.Trajectile.FiringCube.CubeGrid || ent.MarkedForClose || !ent.InScene) continue;
                //if (fired.Age < 30 && ent.PositionComp.WorldAABB.Intersects(fired.ReverseOriginRay).HasValue) continue;
                var shieldBlock = Session.Instance.SApi?.MatchEntToShieldFast(ent, true);
                if (shieldBlock != null)
                {
                    if (ent.Physics == null && shieldBlock.CubeGrid != p.Trajectile.FiringCube.CubeGrid)
                    {
                        var hitEntity = HitEntityPool[poolId].Get();
                        hitEntity.Clean();
                        hitEntity.Entity = (MyEntity)shieldBlock;
                        hitEntity.Beam = beam;
                        if (quickCheck)
                        {
                            hitEntity.HitPos = Session.Instance.SApi.LineIntersectShield(shieldBlock, beam);
                            hitEntity.Hit = true;
                            hitEntity.EventType = Proximity;
                        }
                        found = true;
                        p.HitList.Add(hitEntity);
                    }
                    else continue;
                }

                var extFrom = beam.From - (beam.Direction * (ent.PositionComp.WorldVolume.Radius * 2));
                var extBeam = new LineD(extFrom, beam.To);
                var rotMatrix = Quaternion.CreateFromRotationMatrix(ent.WorldMatrix);
                var obb = new MyOrientedBoundingBoxD(ent.PositionComp.WorldAABB.Center, ent.PositionComp.LocalAABB.HalfExtents, rotMatrix);
                var dist = obb.Intersects(ref extBeam);
                if (dist == null && !quickCheck) continue;

                if (ent.Physics != null && !ent.IsPreview && (ent is MyCubeGrid || ent is MyVoxelBase || ent is IMyDestroyableObject))
                {
                    var hitEntity = HitEntityPool[poolId].Get();
                    hitEntity.Clean();
                    hitEntity.Entity = ent;
                    hitEntity.Beam = beam;
                    if (quickCheck)
                    {
                        var tmpDist = dist ?? 0;
                        hitEntity.HitPos = (beam.From + (beam.Direction * tmpDist));
                        hitEntity.Hit = true;
                        hitEntity.EventType = Proximity;
                    }
                    found = true;
                    p.HitList.Add(hitEntity);
                }
            }
            segmentList?.Clear();
            return found ? GenerateHitInfo(p, poolId) : null;
        }

        internal HitEntity GenerateHitInfo(Projectile p,  int poolId)
        {
            var count = p.HitList.Count;
            if (count > 1) p.HitList.Sort((x, y) => GetEntityCompareDist(x, y, V3Pool.Get()));
            else GetEntityCompareDist(p.HitList[0], null, V3Pool.Get());

            //var afterSort = ents.Count;
            var endOfIndex = p.HitList.Count - 1;
            var lastValidEntry = int.MaxValue;

            for (int i = endOfIndex; i >= 0; i--)
            {
                if (p.HitList[i].Hit)
                {
                    lastValidEntry = i + 1;
                    //Log.Line($"lastValidEntry:{lastValidEntry} - endOfIndex:{endOfIndex}");
                    break;
                }
            }

            if (lastValidEntry == int.MaxValue) lastValidEntry = 0;
            var howManyToRemove = count - lastValidEntry;
            var howMany = howManyToRemove;
            while (howManyToRemove-- > 0)
            {
                //Log.Line($"removing: {endOfIndex} - hit:{ents[endOfIndex].Hit}");
                var ent = p.HitList[endOfIndex];
                p.HitList.RemoveAt(endOfIndex);
                HitEntityPool[poolId].Return(ent);
                endOfIndex--;
            }
            var finalCount = p.HitList.Count;
            HitEntity hitEntity = null;
            if (finalCount > 0)
            {
                hitEntity = p.HitList[0];
                p.LastHitPos = hitEntity.HitPos;
                p.LastHitEntVel = hitEntity.Entity?.Physics?.LinearVelocity;
                //Log.Line($"start:{count} - ASort:{afterSort} - Final:{ents.Count} - howMany:{howMany} - hit:{ents[0].Hit} - hitPos:{ents[0].HitPos.HasValue} - {ents[0].Entity.DebugName} ");
            }
            return hitEntity;
        }

        internal int GetEntityCompareDist(HitEntity x, HitEntity y, List<Vector3I> slims)
        {
            var xDist = double.MaxValue;
            var yDist = double.MaxValue;
            var beam = x.Beam;
            var count = y != null ? 2 : 1;
            for (int i = 0; i < count; i++)
            {
                var isX = i == 0;

                MyEntity ent;
                HitEntity hitEnt;
                if (isX)
                {
                    hitEnt = x;
                    ent = hitEnt.Entity;
                }
                else
                {
                    hitEnt = y;
                    ent = hitEnt.Entity;
                }

                var shield = ent as IMyTerminalBlock;
                var grid = ent as MyCubeGrid;
                var voxel = ent as MyVoxelBase;

                var dist = double.MaxValue;
                if (shield != null)
                {
                    var hitPos = Session.Instance.SApi.LineIntersectShield(shield, beam);
                    if (hitPos != null)
                    {
                        dist = Vector3D.Distance(hitPos.Value, beam.From);
                        hitEnt.Hit = true;
                        hitEnt.HitPos = hitPos.Value;
                        hitEnt.EventType = Shield;
                    }
                }
                else if (grid != null)
                {
                    grid.RayCastCells(beam.From, beam.To, slims, null, true, true);
                    var closestBlockFound = false;
                    for (int j = 0; j < slims.Count; j++)
                    {
                        var firstBlock = grid.GetCubeBlock(slims[j]) as IMySlimBlock;
                        if (firstBlock != null && !firstBlock.IsDestroyed)
                        {
                            hitEnt.Blocks.Add(firstBlock);
                            if (closestBlockFound) continue;
                            hitEnt.EventType = Grid;
                            hitEnt.Hit = true;
                            Vector3D center;
                            firstBlock.ComputeWorldCenter(out center);

                            Vector3 halfExt;
                            firstBlock.ComputeScaledHalfExtents(out halfExt);

                            var blockBox = new BoundingBoxD(-halfExt, halfExt);
                            var rotMatrix = Quaternion.CreateFromRotationMatrix(firstBlock.CubeGrid.WorldMatrix);
                            var obb = new MyOrientedBoundingBoxD(center, blockBox.HalfExtents, rotMatrix);
                            dist = obb.Intersects(ref beam) ?? Vector3D.Distance(beam.From, center);

                            hitEnt.HitPos = beam.From + (beam.Direction * dist);
                            closestBlockFound = true;
                        }
                    }
                    //Log.Line($"testBlockCount:{testBlocks.Count} - closestFound:{closestBlockFound}");
                }
                else if (voxel != null)
                {
                    Vector3D? t;
                    voxel.GetIntersectionWithLine(ref beam, out t, true, IntersectionFlags.DIRECT_TRIANGLES);
                    if (t != null)
                    {
                        Log.Line($"voxel hit: {t.Value}");
                        hitEnt.Hit = true;
                        hitEnt.HitPos = beam.From + (beam.Direction * dist);
                        hitEnt.EventType = Voxel;
                    }
                    /*
                    {
                        var hitInfoRet = new MyHitInfo
                        {
                            Position = t.Value,
                        };
                    }
                    */
                }
                else if (ent is IMyDestroyableObject)
                {
                    var rotMatrix = Quaternion.CreateFromRotationMatrix(ent.PositionComp.WorldMatrix);
                    var obb = new MyOrientedBoundingBoxD(ent.PositionComp.WorldAABB.Center, ent.PositionComp.LocalAABB.HalfExtents, rotMatrix);
                    dist = obb.Intersects(ref beam) ?? double.MaxValue;
                    if (dist < double.MaxValue)
                    {
                        hitEnt.Hit = true;
                        hitEnt.HitPos = beam.From + (beam.Direction * dist);
                        hitEnt.EventType = Destroyable;
                    }
                }
                else Log.Line($"no hit master");

                if (isX) xDist = dist;
                else yDist = dist;
            }
            V3Pool.Return(slims);
            return xDist.CompareTo(yDist);
        }

        internal bool FastHitPos(HitEntity hitEntity, LineD beam, int poolId)
        {
            var shield = hitEntity.Entity as IMyTerminalBlock;
            var grid = hitEntity.Entity as MyCubeGrid;
            var voxel = hitEntity.Entity as MyVoxelBase;
            var ent = hitEntity.Entity;
            var dist = double.MaxValue;
            if (shield != null)
            {
                var hitPos = Session.Instance.SApi.LineIntersectShield(shield, beam);
                if (hitPos.HasValue)
                    hitEntity.HitPos = hitPos;
                return true;
            }

            if (grid != null)
            {
                var blockPos = grid.RayCastBlocks(beam.From, beam.To);
                if (blockPos.HasValue)
                {
                    //var center = grid.GridIntegerToWorld(blockPos.Value);
                    var firstBlock = grid.GetCubeBlock(blockPos.Value) as IMySlimBlock;
                    Vector3D center;
                    firstBlock.ComputeWorldCenter(out center);
                    Vector3 halfExt;
                    firstBlock.ComputeScaledHalfExtents(out halfExt);

                    var blockBox = new BoundingBoxD(-halfExt, halfExt);
                    var rotMatrix = Quaternion.CreateFromRotationMatrix(firstBlock.CubeGrid.WorldMatrix);
                    var obb = new MyOrientedBoundingBoxD(center, blockBox.HalfExtents, rotMatrix);
                    dist = obb.Intersects(ref beam) ?? -1;
                    if (dist < 0) return false;
                    hitEntity.HitPos = (beam.From + (beam.Direction * dist));
                    return true;
                }
                return false;
            }
            if (voxel != null)
            {
                Vector3D? t;
                voxel.GetIntersectionWithLine(ref beam, out t, true, IntersectionFlags.DIRECT_TRIANGLES);
                if (t != null)
                {
                    Log.Line($"voxel hit: {t.Value}");
                    hitEntity.HitPos = beam.From + (beam.Direction * dist);
                    return true;
                }
                return false;
            }
            if (ent is IMyDestroyableObject)
            {
                var rotMatrix = Quaternion.CreateFromRotationMatrix(ent.PositionComp.WorldMatrix);
                var obb = new MyOrientedBoundingBoxD(ent.PositionComp.WorldAABB.Center, ent.PositionComp.LocalAABB.HalfExtents, rotMatrix);
                dist = obb.Intersects(ref beam) ?? double.MaxValue;
                if (dist < double.MaxValue)
                {
                    hitEntity.HitPos = beam.From + (beam.Direction * dist);
                    return true;
                }
                return false;
            }
            Log.Line($"no hit slave - {hitEntity.Entity == null}");

            return false;
        }

        internal class Trajectile
        {
            internal WeaponSystem System;
            internal MyCubeBlock FiringCube;
            internal MyEntity Entity;
            internal MatrixD EntityMatrix = MatrixD.Identity;
            internal int WeaponId;
            internal int MuzzleId;

            internal void InitVirtual(WeaponSystem system, MyCubeBlock firingCube, MyEntity entity, int weaponId, int muzzleId, Vector3D position, Vector3D direction)
            {
                System = system;
                FiringCube = firingCube;
                Entity = entity;
                WeaponId = weaponId;
                MuzzleId = muzzleId;
                Position = position;
                PrevPosition = Position;
                Direction = direction;
            }

            internal Vector3D Position;
            internal Vector3D PrevPosition;
            internal Vector3D Direction;
            internal double Length;

            internal void UpdateVrShape(Vector3D prevPosition, Vector3D position, Vector3D direction, double length)
            {
                PrevPosition = prevPosition;
                Position = position;
                Direction = direction;
                Length = length;
            }

            internal void UpdateShape(Vector3D prevPosition, Vector3D position, Vector3D direction, double length)
            {
                PrevPosition = prevPosition;
                Position = position;
                Direction = direction;
                Length = length;
            }

            internal HitEntity HitEntity;
            internal double MaxSpeedLength;
            internal float LineWidth;
            internal int ReSizeSteps;
            internal bool Shrink;
            internal bool OnScreen;
            internal bool Last;
            internal Vector4 Color;
            internal void Complete(HitEntity hitEntity, bool last)
            {
                HitEntity = hitEntity;
                Last = last;
                var color = System.Values.Graphics.Line.Color;
                if (System.LineColorVariance)
                {
                    var cv = System.Values.Graphics.Line.ColorVariance;
                    var randomValue = MyUtils.GetRandomFloat(cv.Start, cv.End);
                    color.X *= randomValue;
                    color.Y *= randomValue;
                    color.Z *= randomValue;
                }

                var width = System.Values.Graphics.Line.Width;
                if (System.LineWidthVariance)
                {
                    var wv = System.Values.Graphics.Line.WidthVariance;
                    var randomValue = MyUtils.GetRandomFloat(wv.Start, wv.End);
                    width += randomValue;
                }

                LineWidth = width;
                Color = color;
            }
        }

        public class HitEntity
        {
            public enum Type
            {
                Shield,
                Grid,
                Voxel,
                Proximity,
                Destroyable,
                Stale,
            }

            public readonly List<IMySlimBlock> Blocks = new List<IMySlimBlock>();
            public MyEntity Entity;
            public LineD Beam;
            public bool Hit;
            public Vector3D? HitPos;
            public Type EventType;

            public HitEntity()
            {
            }

            public void Clean()
            {
                Entity = null;
                Beam.Length = 0;
                Beam.Direction = Vector3D.Zero;
                Beam.From = Vector3D.Zero;
                Beam.To = Vector3D.Zero;
                Blocks.Clear();
                Hit = false;
                HitPos = null;
                EventType = Stale;
            }
        }
    }
}