using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Interfaces;
using VRage.Utils;
using VRageMath;
using WeaponCore.Support;
using static WeaponCore.Support.HitEntity.Type;

namespace WeaponCore.Projectiles
{
    public partial class Projectiles
    {
        private bool Hit(Projectile p, int poolId, bool lineCheck)
        {
            var beam = new LineD(p.LastPosition, p.Position);
            if (lineCheck)
            {
                p.PruneSphere.Center = p.Position;
                p.PruneSphere.Radius = p.T.System.CollisionSize;
                MyGamePruningStructure.GetTopmostEntitiesOverlappingRay(ref beam, p.SegmentList, p.PruneQuery);
            }
            else
            {
                p.PruneSphere = new BoundingSphereD(p.Position, 0).Include(new BoundingSphereD(p.LastPosition, 0));
                if (p.EwarActive && p.PruneSphere.Radius < p.T.System.AreaEffectSize)
                {
                    p.PruneSphere.Center = p.Position;
                    p.PruneSphere.Radius = p.T.System.AreaEffectSize;
                }
                else if (p.PruneSphere.Radius < p.T.System.CollisionSize)
                {
                    p.PruneSphere.Center = p.Position;
                    p.PruneSphere.Radius = p.T.System.CollisionSize;
                }

                var checkList = CheckPool[poolId].Get();
                MyGamePruningStructure.GetAllTopMostEntitiesInSphere(ref p.PruneSphere, checkList, p.PruneQuery);
                for (int i = 0; i < checkList.Count; i++)
                    p.SegmentList.Add(new MyLineSegmentOverlapResult<MyEntity> { Distance = 0, Element = checkList[i] });

                checkList.Clear();
                CheckPool[poolId].Return(checkList);
            }
            if (p.SegmentList.Count > 0)
            {
                Log.Line($"test:{p.SegmentList.Count}");
                var nearestHitEnt = GetAllEntitiesInLine(p, beam, poolId, lineCheck);
                if (nearestHitEnt != null && Intersected(p, DrawProjectiles[poolId], nearestHitEnt)) return true;
                p.T.HitList.Clear();
            }

            return false;
        }

        internal HitEntity GetAllEntitiesInLine(Projectile p, LineD beam, int poolId, bool lineCheck)
        {
            var shieldByPass = p.T.System.Values.DamageScales.Shields.Type == ShieldDefinition.ShieldType.Bypass;
            var ai = p.T.Ai;
            var found = false;

            //Log.Line($"get all entities in line: LineCheck:{lineCheck} - ewarActive:{eWarActive} - ewarInactive:{eWarInactive} - jump:{jumpNullField} - Vel:{p.VelocityLengthSqr}");

            for (int i = 0; i < p.SegmentList.Count; i++)
            {
                var ent = p.SegmentList[i].Element;
                var grid = ent as MyCubeGrid;
                var destroyable = ent as IMyDestroyableObject;
                if (grid != null && (grid == p.T.Ai.MyGrid || p.T.Ai.MyGrid.IsSameConstructAs(grid)) || ent.MarkedForClose || !ent.InScene || ent == p.T.Ai.MyShield) continue;

                if (!shieldByPass && !p.MovementField)
                {
                    var shieldBlock = Session.Instance.SApi?.MatchEntToShieldFast(ent, true);
                    if (shieldBlock != null)
                    {
                        if (ent.Physics == null && shieldBlock.CubeGrid != p.T.Ai.MyGrid)
                        {
                            var hitEntity = HitEntityPool[poolId].Get();
                            hitEntity.Clean();
                            hitEntity.PoolId = poolId;
                            hitEntity.Entity = (MyEntity)shieldBlock;
                            hitEntity.Beam = beam;

                            hitEntity.EventType = Shield;
                            hitEntity.SphereCheck = !lineCheck;
                            hitEntity.PruneSphere = p.PruneSphere;
                            found = true;
                            p.T.HitList.Add(hitEntity);
                        }
                        else continue;
                    }
                }
                var voxel = ent as MyVoxelBase;
                if ((ent == ai.MyPlanet && (p.CheckPlanet || p.DynamicGuidance)) || ent.Physics != null && !ent.IsPreview && (grid != null || voxel != null || destroyable != null))
                {
                    var extFrom = beam.From - (beam.Direction * (ent.PositionComp.WorldVolume.Radius * 2));
                    var extBeam = new LineD(extFrom, beam.To);
                    var rotMatrix = Quaternion.CreateFromRotationMatrix(ent.WorldMatrix);
                    var obb = new MyOrientedBoundingBoxD(ent.PositionComp.WorldAABB.Center, ent.PositionComp.LocalAABB.HalfExtents, rotMatrix);
                    if (lineCheck && obb.Intersects(ref extBeam) == null || !lineCheck && !obb.Intersects(ref p.PruneSphere)) continue;

                    Vector3D? voxelHit = null;
                    if (voxel != null)
                    {
                        if (voxel.RootVoxel != voxel) continue;
                        if (voxel == ai.MyPlanet)
                        {
                            var check = false;
                            var closestPos = ai.MyPlanet.GetClosestSurfacePointGlobal(ref p.Position);
                            var planetCenter = ai.MyPlanet.PositionComp.WorldAABB.Center;
                            double cDistToCenter;
                            Vector3D.DistanceSquared(ref closestPos, ref planetCenter, out cDistToCenter);
                            double pDistTocenter;
                            Vector3D.DistanceSquared(ref p.Position, ref planetCenter, out pDistTocenter);
                            if (cDistToCenter > pDistTocenter || cDistToCenter > Vector3D.DistanceSquared(planetCenter, p.LastPosition)) check = true;
                            if (check)
                            {
                                using (voxel.Pin())
                                {
                                    voxel.GetIntersectionWithLine(ref beam, out voxelHit);
                                }
                            }
                        }
                        else using (voxel.Pin()) voxel.GetIntersectionWithLine(ref beam, out voxelHit);
                        if (!voxelHit.HasValue) continue;
                    }
                    var hitEntity = HitEntityPool[poolId].Get();
                    hitEntity.Clean();
                    hitEntity.PoolId = poolId;
                    hitEntity.Entity = ent;
                    hitEntity.Beam = beam;
                    hitEntity.SphereCheck = !lineCheck;
                    hitEntity.PruneSphere = p.PruneSphere;

                    if (voxelHit != null) hitEntity.HitPos = voxelHit;

                    if (grid != null)
                    {
                        hitEntity.EventType = !(p.EwarActive && p.AreaEffect == AreaDamage.AreaEffectType.JumpNullField) ? Grid : JumpNullField;
                        Log.Line($"see grid: {hitEntity.EventType}");
                    }
                    else if (destroyable != null)
                        hitEntity.EventType = Destroyable;
                    else if (voxel != null)
                        hitEntity.EventType = Voxel;
                    found = true;
                    p.T.HitList.Add(hitEntity);
                }
            }

            if (p.T.Target.IsProjectile && !p.MovementField)
            {
                var targetPos = p.T.Target.Projectile.Position;
                var sphere = new BoundingSphereD(targetPos, p.T.Target.Projectile.T.System.CollisionSize);
                var rayCheck = p.T.System.CollisionIsLine && sphere.Intersects(new RayD(p.LastPosition, p.Direction)) != null;
                var sphereCheck = !rayCheck && sphere.Intersects(p.PruneSphere);
                if (rayCheck || sphereCheck)
                {
                    var hitEntity = HitEntityPool[poolId].Get();
                    hitEntity.Clean();
                    hitEntity.PoolId = poolId;
                    hitEntity.EventType = HitEntity.Type.Projectile;
                    hitEntity.Hit = true;
                    hitEntity.Projectile = p.T.Target.Projectile;
                    hitEntity.HitPos = targetPos;
                    hitEntity.SphereCheck = !lineCheck;
                    hitEntity.PruneSphere = p.PruneSphere;

                    hitEntity.Beam = new LineD(p.LastPosition, targetPos);
                    found = true;
                    p.T.HitList.Add(hitEntity);
                }
            }
            p.SegmentList.Clear();

            return found ? GenerateHitInfo(p, poolId) : null;
        }

        internal HitEntity GenerateHitInfo(Projectile p, int poolId)
        {
            var count = p.T.HitList.Count;
            if (count > 1) p.T.HitList.Sort((x, y) => GetEntityCompareDist(x, y, V3Pool.Get()));
            else GetEntityCompareDist(p.T.HitList[0], null, V3Pool.Get());

            var endOfIndex = p.T.HitList.Count - 1;
            var lastValidEntry = int.MaxValue;

            for (int i = endOfIndex; i >= 0; i--)
            {
                if (p.T.HitList[i].Hit)
                {
                    lastValidEntry = i + 1;
                    break;
                }
            }

            if (lastValidEntry == int.MaxValue) lastValidEntry = 0;
            var howManyToRemove = count - lastValidEntry;
            while (howManyToRemove-- > 0)
            {
                var ent = p.T.HitList[endOfIndex];
                p.T.HitList.RemoveAt(endOfIndex);
                HitEntityPool[poolId].Return(ent);
                endOfIndex--;
            }
            var finalCount = p.T.HitList.Count;
            HitEntity hitEntity = null;
            if (finalCount > 0)
            {
                hitEntity = p.T.HitList[0];
                p.LastHitPos = hitEntity.HitPos;
                p.LastHitEntVel = hitEntity.Entity?.Physics?.LinearVelocity;
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
                if (hitEnt.Projectile != null) dist = Vector3D.Distance(hitEnt.HitPos.Value, beam.From);
                else if (shield != null)
                {
                    var hitPos = Session.Instance.SApi.LineIntersectShield(shield, beam);
                    if (hitPos != null)
                    {
                        dist = Vector3D.Distance(hitPos.Value, beam.From);
                        hitEnt.Hit = true;
                        hitEnt.HitPos = hitPos.Value;
                    }
                }
                else if (grid != null)
                {
                    if (hitEnt.Hit) dist = Vector3D.Distance(hitEnt.Beam.From, hitEnt.HitPos.Value);
                    else
                    {
                        if (hitEnt.SphereCheck)
                        {
                            var fieldActive = hitEnt.EventType == JumpNullField;

                            dist = 0;
                            hitEnt.Hit = true;
                            var hitPos = !fieldActive ? hitEnt.PruneSphere.Center + (hitEnt.Beam.Direction * hitEnt.PruneSphere.Radius) : hitEnt.PruneSphere.Center;
                            hitEnt.HitPos = hitPos;

                            if (!fieldActive)
                            {
                                var hashSet = HitEntity.CastOrGetHashSet(hitEnt, ((MyCubeGrid)null)?.CubeBlocks);
                                grid.GetBlocksInsideSphere(ref hitEnt.PruneSphere, hashSet, false);

                                hitEnt.Blocks.AddRange(hashSet);
                                hitEnt.Blocks.Sort((a, b) =>
                                {
                                    var aPos = grid.GridIntegerToWorld(a.Position);
                                    var bPos = grid.GridIntegerToWorld(b.Position);
                                    return Vector3D.DistanceSquared(aPos, hitPos).CompareTo(Vector3D.DistanceSquared(bPos, hitPos));
                                });
                            }
                        }
                        else
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
                        }
                    }
                }
                else if (voxel != null)
                {
                    var hitPos = hitEnt.HitPos.Value;
                    hitEnt.Hit = true;
                    Vector3D.Distance(ref beam.From, ref hitPos, out dist);
                    hitEnt.HitPos = beam.From + (beam.Direction * dist);
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
                    }
                }

                if (isX) xDist = dist;
                else yDist = dist;
            }
            V3Pool.Return(slims);
            return xDist.CompareTo(yDist);
        }

        public static List<Vector3D> CreateRandomLineSegOffsets(double maxRange, double minForwardStep, double maxForwardStep, double maxOffset, ref List<Vector3D> offsetList)
        {
            double currentForwardDistance = 0;

            while (currentForwardDistance < maxRange)
            {
                currentForwardDistance += MyUtils.GetRandomDouble(minForwardStep, maxForwardStep);
                var lateralXDistance = MyUtils.GetRandomDouble(maxOffset * -1, maxOffset);
                var lateralYDistance = MyUtils.GetRandomDouble(maxOffset * -1, maxOffset);
                offsetList.Add(new Vector3D(lateralXDistance, lateralYDistance, currentForwardDistance * -1));
            }
            return offsetList;
        }

        public static void DisplayLineOffsetEffect(MatrixD startMatrix, Vector3D endCoords, float beamRadius, Color color, List<Vector3D> offsetList, MyStringId offsetMaterial, bool isDedicated = false)
        {

            var maxDistance = Vector3D.Distance(startMatrix.Translation, endCoords);

            for (int i = 0; i < offsetList.Count; i++)
            {

                Vector3D fromBeam;
                Vector3D toBeam;

                if (i == 0)
                {
                    fromBeam = startMatrix.Translation;
                    toBeam = Vector3D.Transform(offsetList[i], startMatrix);
                }
                else
                {
                    fromBeam = Vector3D.Transform(offsetList[i - 1], startMatrix);
                    toBeam = Vector3D.Transform(offsetList[i], startMatrix);
                }

                var vectorColor = color.ToVector4();
                MySimpleObjectDraw.DrawLine(fromBeam, toBeam, offsetMaterial, ref vectorColor, beamRadius);

                if (Vector3D.Distance(startMatrix.Translation, toBeam) > maxDistance) break;
            }
        }

        private static void PrefetchVoxelPhysicsIfNeeded(Projectile p)
        {
            var ray = new LineD(p.Origin, p.Origin + p.Direction * p.MaxTrajectory, p.MaxTrajectory);
            var lineD = new LineD(new Vector3D(Math.Floor(ray.From.X) * 0.5, Math.Floor(ray.From.Y) * 0.5, Math.Floor(ray.From.Z) * 0.5), new Vector3D(Math.Floor(p.Direction.X * 50.0), Math.Floor(p.Direction.Y * 50.0), Math.Floor(p.Direction.Z * 50.0)));
            if (p.VoxelRayCache.IsItemPresent(lineD.GetHash(), (int)MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds, true))
                return;
            using (MyUtils.ReuseCollection(ref p.EntityRaycastResult))
            {
                MyGamePruningStructure.GetAllEntitiesInRay(ref ray, p.EntityRaycastResult, MyEntityQueryType.Static);
                foreach (var segmentOverlapResult in p.EntityRaycastResult)
                    (segmentOverlapResult.Element as MyPlanet)?.PrefetchShapeOnRay(ref ray);
            }
        }
    }
}
