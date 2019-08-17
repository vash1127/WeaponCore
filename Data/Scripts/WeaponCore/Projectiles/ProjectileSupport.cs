using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
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
        private void GetEntitiesInBlastRadius(Projectile p, int poolId)
        {
            var sphere = new BoundingSphereD(p.Position, p.T.System.Values.Ammo.AreaEffect.AreaEffectRadius);
            var checkList = CheckPool[poolId].Get();
            MyGamePruningStructure.GetAllTopMostEntitiesInSphere(ref sphere, checkList);
            foreach (var ent in checkList)
            {
                var blastLine = new LineD(p.Position, ent.PositionComp.WorldAABB.Center);
                GetAllEntitiesInLine(p, blastLine, null, poolId, true);
            }
            checkList.Clear();
            CheckPool[poolId].Return(checkList);
            var count = p.T.HitList.Count;
            if (!Session.Instance.DedicatedServer && count <= 0)
            {
                var hitEntity = HitEntityPool[poolId].Get();
                hitEntity.Clean();
                hitEntity.EventType = Proximity;
                hitEntity.Hit = false;
                hitEntity.HitPos = p.Position;
                p.T.HitList.Add(hitEntity);
                Session.Instance.Hits.Enqueue(p);
            }
            else if (Session.Instance.IsServer && count > 0)
                Session.Instance.Hits.Enqueue(p);
        }

        internal HitEntity GetAllEntitiesInLine(Projectile p, LineD beam, List<MyLineSegmentOverlapResult<MyEntity>> segmentList, int poolId, bool quickCheck = false)
        {
            var listCnt = segmentList?.Count ?? p.T.HitList.Count;
            var found = false;
            for (int i = 0; i < listCnt; i++)
            {
                var ent = segmentList != null ? segmentList[i].Element : p.T.HitList[i].Entity;
                if (ent == p.T.Ai.MyGrid || ent.MarkedForClose || !ent.InScene) continue;
                //if (fired.Age < 30 && ent.PositionComp.WorldAABB.Intersects(fired.ReverseOriginRay).HasValue) continue;
                var shieldBlock = Session.Instance.SApi?.MatchEntToShieldFast(ent, true);
                if (shieldBlock != null)
                {
                    if (ent.Physics == null && shieldBlock.CubeGrid != p.T.Ai.MyGrid)
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
                        p.T.HitList.Add(hitEntity);
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
                    p.T.HitList.Add(hitEntity);
                }
            }
            segmentList?.Clear();
            return found ? GenerateHitInfo(p, poolId) : null;
        }

        internal HitEntity GenerateHitInfo(Projectile p, int poolId)
        {
            var count = p.T.HitList.Count;
            if (count > 1) p.T.HitList.Sort((x, y) => GetEntityCompareDist(x, y, V3Pool.Get()));
            else GetEntityCompareDist(p.T.HitList[0], null, V3Pool.Get());

            //var afterSort = ents.Count;
            var endOfIndex = p.T.HitList.Count - 1;
            var lastValidEntry = int.MaxValue;

            for (int i = endOfIndex; i >= 0; i--)
            {
                if (p.T.HitList[i].Hit)
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

        private static MyEntity GetSubpartOwner(MyEntity entity)
        {
            if (entity == null)
                return null;
            if (!(entity is MyEntitySubpart))
                return entity;
            var myEntity = entity;
            while (myEntity is MyEntitySubpart)
                myEntity = myEntity.Parent;
            return myEntity ?? entity;
        }
    }
}
