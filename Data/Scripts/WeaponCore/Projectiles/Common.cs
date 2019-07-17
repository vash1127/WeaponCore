using System.Collections.Generic;
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
    internal partial class Projectiles
    {
        private void GetEntitiesInBlastRadius(Projectile projectile, int poolId)
        {
            var sphere = new BoundingSphereD(projectile.Position, projectile.System.Values.Ammo.AreaEffectRadius);
            var entityList = MyEntityPool[poolId].Get();
            MyGamePruningStructure.GetAllTopMostEntitiesInSphere(ref sphere, entityList);
            foreach (var ent in entityList)
            {
                var blastLine = new LineD(projectile.Position, ent.PositionComp.WorldAABB.Center);
                GetAllEntitiesInLine(projectile, blastLine, null, poolId, true);
            }
            entityList.Clear();
            MyEntityPool[poolId].Return(entityList);
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
                if (ent == p.FiringCube.CubeGrid) continue;
                //if (fired.Age < 30 && ent.PositionComp.WorldAABB.Intersects(fired.ReverseOriginRay).HasValue) continue;
                var shieldBlock = Session.Instance.SApi?.MatchEntToShieldFast(ent, true);
                if (shieldBlock != null)
                {
                    if (ent.Physics == null && shieldBlock.CubeGrid != p.FiringCube.CubeGrid)
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

                if (ent.Physics != null && (ent is MyCubeGrid || ent is MyVoxelBase || ent is IMyDestroyableObject))
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

            if (count > 1) p.HitList.Sort((x, y) => GetEntityCompareDist(x, y));
            else GetEntityCompareDist(p.HitList[0], null);

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

        internal static int GetEntityCompareDist(HitEntity x, HitEntity y)
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
                    var testBlocks = new List<Vector3I>();
                    grid.RayCastCells(beam.From, beam.To, testBlocks, null, true, true);
                    var closestBlockFound = false;
                    for (int j = 0; j < testBlocks.Count; j++)
                    {
                        var firstBlock = grid.GetCubeBlock(testBlocks[j]) as IMySlimBlock;
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
                else Log.Line($"no hit");

                if (isX) xDist = dist;
                else yDist = dist;
            }
            return xDist.CompareTo(yDist);
        }

        internal struct DrawProjectile
        {
            internal readonly Projectile Projectile;
            internal readonly HitEntity HitEntity;
            internal readonly float LineWidth;
            internal readonly bool Last;
            internal readonly Vector4 Color;

            internal DrawProjectile(Projectile projectile, HitEntity hitEntity, bool last)
            {
                Projectile = projectile;
                HitEntity = hitEntity;
                Last = last;
                var color = Projectile.System.Values.Graphics.Line.Color;
                if (Projectile.System.LineColorVariance)
                {
                    var cv = Projectile.System.Values.Graphics.Line.ColorVariance;
                    var randomValue = MyUtils.GetRandomFloat(cv.Start, cv.End);
                    color.X *= randomValue;
                    color.Y *= randomValue;
                    color.Z *= randomValue;
                }

                var width = Projectile.System.Values.Graphics.Line.Width;
                if (Projectile.System.LineWidthVariance)
                {
                    var wv = Projectile.System.Values.Graphics.Line.WidthVariance;
                    var randomValue = MyUtils.GetRandomFloat(wv.Start, wv.End);
                    width += randomValue;
                }

                LineWidth = width;
                Color = color;
            }
        }

        internal struct Trajectile
        {
            internal readonly Vector3D PrevPosition;
            internal readonly Vector3D Position;
            internal readonly Vector3D Direction;
            internal readonly double Length;
            internal Trajectile(Vector3D prevPosition, Vector3D position, Vector3D direction, double length)
            {
                PrevPosition = prevPosition;
                Position = position;
                Direction = direction;
                Length = length;
            }
        }

        internal class HitEntity
        {
            internal enum Type
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