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
using static WeaponCore.Support.AreaDamage.AreaEffectType;

namespace WeaponCore.Projectiles
{
    public partial class Projectiles
    {
        internal bool GetAllEntitiesInLine(Projectile p, LineD beam)
        {
            var shieldByPass = p.Info.System.Values.DamageScales.Shields.Type == ShieldDefinition.ShieldType.Bypass;
            var shieldFullBypass = shieldByPass && p.Info.System.ShieldBypassMod >= 1;
            var ai = p.Info.Ai;
            var found = false;
            var lineCheck = p.Info.System.CollisionIsLine;
            var planetBeam = beam;
            planetBeam.To = p.Info.System.IsBeamWeapon && p.MaxTrajectory > 1500 ? beam.From + (beam.Direction * 1500) : beam.To;
            bool projetileInShield = false;
            for (int i = 0; i < p.SegmentList.Count; i++)
            {
                var ent = p.SegmentList[i].Element;
                var grid = ent as MyCubeGrid;
                var destroyable = ent as IMyDestroyableObject;
                var voxel = ent as MyVoxelBase;
                if (grid == null && p.EwarActive && p.Info.System.AreaEffect != DotField && ent is IMyCharacter) continue;
                if (grid != null && (!(p.Info.System.SelfDamage || p.TerminalControlled) || p.SmartsOn) && p.Info.Ai.MyGrid.IsSameConstructAs(grid) || ent.MarkedForClose || !ent.InScene || ent == p.Info.Ai.MyShield) continue;
                if (!shieldFullBypass && !p.ShieldBypassed && !p.EwarActive)
                {
                    var shieldInfo = p.Info.Ai.Session.SApi?.MatchEntToShieldFastExt(ent, true);
                    if (shieldInfo != null)
                    {
                        double? dist = null;
                        if (ent.Physics != null && ent.Physics.IsPhantom)
                        {
                            dist = MathFuncs.IntersectEllipsoid(shieldInfo.Value.Item3.Item1, shieldInfo.Value.Item3.Item2, new RayD(beam.From, beam.Direction));
                            if (p.Info.Target.IsProjectile && Vector3D.Transform(p.Info.Target.Projectile.Position, shieldInfo.Value.Item3.Item1).LengthSquared() <= 1)
                                projetileInShield = true;
                        }

                        if (dist != null && dist.Value < beam.Length && !p.Info.Ai.MyGrid.IsSameConstructAs(shieldInfo.Value.Item1.CubeGrid))
                        {
                            if (shieldByPass) p.ShieldBypassed = true;
                            var hitEntity = HitEntityPool.Get();
                            hitEntity.Clean();
                            hitEntity.Info = p.Info;
                            hitEntity.Entity = (MyEntity)shieldInfo.Value.Item1;
                            hitEntity.Intersection = beam;
                            hitEntity.EventType = Shield;
                            hitEntity.SphereCheck = !lineCheck;
                            hitEntity.PruneSphere = p.PruneSphere;
                            hitEntity.HitPos = beam.From + (beam.Direction * dist.Value);
                            hitEntity.HitDist = dist;
                            found = true;
                            p.Info.HitList.Add(hitEntity);
                        }
                        else continue;
                    }
                }
                
                if ((ent == ai.MyPlanet && (p.LinePlanetCheck || p.DynamicGuidance || p.CachedPlanetHit)) || ent.Physics != null && !ent.IsPreview && (grid != null || voxel != null || destroyable != null))
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
                            if (p.CachedPlanetHit)
                            {
                                IHitInfo cachedPlanetResult;
                                if (p.Info.WeaponCache.VoxelHits[p.CachedId].NewResult(out cachedPlanetResult))
                                {
                                    //Log.Line("cached hit");
                                    voxelHit = cachedPlanetResult.Position;
                                }
                                else
                                {
                                    //Log.Line($"cachedPlanet but no new result: {p.LinePlanetCheck}");
                                    continue;
                                }
                            }

                            if (p.LinePlanetCheck)
                            {
                                var check = false;
                                var closestPos = ai.MyPlanet.GetClosestSurfacePointGlobal(ref p.Position);
                                var planetCenter = ai.MyPlanet.PositionComp.WorldAABB.Center;
                                double cDistToCenter;
                                Vector3D.DistanceSquared(ref closestPos, ref planetCenter, out cDistToCenter);
                                double pDistToCenter;
                                Vector3D.DistanceSquared(ref p.Position, ref planetCenter, out pDistToCenter);
                                double mDistToCenter;
                                Vector3D.DistanceSquared(ref p.Info.Origin, ref planetCenter, out mDistToCenter);
                                if (cDistToCenter > pDistToCenter || cDistToCenter > Vector3D.DistanceSquared(planetCenter, p.LastPosition) || pDistToCenter > mDistToCenter) check = true;
                                if (check)
                                {
                                    using (voxel.Pin())
                                    {
                                        voxel.GetIntersectionWithLine(ref planetBeam, out voxelHit);
                                    }
                                }
                            }
                        }
                        else
                            using (voxel.Pin())
                                voxel.GetIntersectionWithLine(ref beam, out voxelHit);

                        if (!voxelHit.HasValue)
                            continue;
                    }
                    var hitEntity = HitEntityPool.Get();
                    hitEntity.Clean();
                    hitEntity.Info = p.Info;
                    hitEntity.Entity = ent;
                    hitEntity.Intersection = beam;
                    hitEntity.SphereCheck = !lineCheck;
                    hitEntity.PruneSphere = p.PruneSphere;

                    if (voxelHit != null)
                    {
                        var hitPos = voxelHit.Value;
                        hitEntity.HitPos = hitPos;

                        double dist;
                        Vector3D.Distance(ref beam.From, ref hitPos, out dist);
                        hitEntity.HitDist = dist;
                    }

                    if (grid != null)
                    {
                        if (!(p.EwarActive && p.Info.System.EwarEffect))
                            hitEntity.EventType = Grid;
                        else if (!p.Info.System.Pulse)
                            hitEntity.EventType = Effect;
                        else hitEntity.EventType = Field;
                        if (p.Info.System.AreaEffect == DotField) hitEntity.DamageOverTime = true;
                    }
                    else if (destroyable != null)
                        hitEntity.EventType = Destroyable;
                    else if (voxel != null)
                        hitEntity.EventType = Voxel;
                    found = true;
                    p.Info.HitList.Add(hitEntity);
                }
            }

            if (p.Info.Target.IsProjectile && !p.Info.System.EwarEffect && !projetileInShield)
            {
                var detonate = p.State == Projectile.ProjectileState.Detonate;
                var hitTolerance = detonate ? p.Info.System.Values.Ammo.AreaEffect.Detonation.DetonationRadius : p.Info.System.AreaEffectSize > p.Info.System.CollisionSize ? p.Info.System.AreaEffectSize : p.Info.System.CollisionSize;
                var useLine = p.Info.System.CollisionIsLine && !detonate && p.Info.System.AreaEffectSize <= 0;

                var sphere = new BoundingSphereD(p.Info.Target.Projectile.Position, p.Info.Target.Projectile.Info.System.CollisionSize);
                sphere.Include(new BoundingSphereD(p.Info.Target.Projectile.LastPosition, 1));
                var rayCheck = useLine && sphere.Intersects(new RayD(p.LastPosition, p.Direction)) != null;
                var testSphere = p.PruneSphere;
                testSphere.Radius = hitTolerance;

                if (rayCheck || sphere.Intersects(testSphere))
                    found = ProjectileHit(p, p.Info.Target.Projectile, lineCheck, ref beam);
            }
            p.SegmentList.Clear();

            return found && GenerateHitInfo(p);
        }

        internal bool ProjectileHit(Projectile attacker, Projectile target, bool lineCheck, ref LineD beam)
        {
            var hitEntity = HitEntityPool.Get();
            hitEntity.Clean();
            hitEntity.Info = attacker.Info;
            hitEntity.EventType = HitEntity.Type.Projectile;
            hitEntity.Hit = true;
            hitEntity.Projectile = target;
            hitEntity.HitPos = attacker.Position;
            hitEntity.SphereCheck = !lineCheck;
            hitEntity.PruneSphere = attacker.PruneSphere;
            double dist;
            Vector3D.Distance(ref beam.From, ref target.Position, out dist);
            hitEntity.HitDist = dist;

            hitEntity.Intersection = new LineD(attacker.LastPosition, target.Position);
            attacker.Info.HitList.Add(hitEntity);
            return true;
        }

        internal bool GenerateHitInfo(Projectile p)
        {
            var count = p.Info.HitList.Count;
            if (count > 1) p.Info.HitList.Sort((x, y) => GetEntityCompareDist(x, y, V3Pool.Get(), p.Info));
            else GetEntityCompareDist(p.Info.HitList[0], null, V3Pool.Get(), p.Info);

            for (int i = p.Info.HitList.Count - 1; i >= 0; i--)
            {
                var ent = p.Info.HitList[i];
                if (!ent.Hit)
                {
                    p.Info.HitList.RemoveAtFast(i);
                    HitEntityPool.Return(ent);
                }
                else break;
            }

            var finalCount = p.Info.HitList.Count;
            if (finalCount > 0)
            {
                if (p.Info.System.Ewar && p.Info.System.Pulse && !p.Info.TriggeredPulse && p.Info.System.EwarTriggerRange > 0)
                {
                    p.Info.TriggeredPulse = true;
                    p.DistanceToTravelSqr = p.Info.DistanceTraveled * p.Info.DistanceTraveled;
                    p.Velocity = Vector3D.Zero;
                    p.Info.HitList.Clear();
                    return false;
                }
                var hitEntity = p.Info.HitList[0];
                p.LastHitPos = hitEntity.HitPos;
                p.LastHitEntVel = hitEntity.Projectile?.Velocity ?? hitEntity.Entity?.Physics?.LinearVelocity ?? Vector3D.Zero;
                p.Info.LastHitShield = hitEntity.EventType == Shield;

                IMySlimBlock hitBlock = null;
                if (p.Info.System.VirtualBeams && hitEntity.Entity is MyCubeGrid)
                    hitBlock = hitEntity.Blocks[0];
                p.Hit = new Hit { Block = hitBlock, Entity = hitEntity.Entity, Projectile = null, HitPos = p.LastHitPos ?? Vector3D.Zero, HitVelocity = p.LastHitEntVel ?? Vector3D.Zero };
                if (p.EnableAv) p.Info.AvShot.Hit = p.Hit;

                return true;
            }
            return false;
        }

        internal int GetEntityCompareDist(HitEntity x, HitEntity y, List<Vector3I> slims, ProInfo info)
        {
            var xDist = double.MaxValue;
            var yDist = double.MaxValue;
            var beam = x.Intersection;
            var count = y != null ? 2 : 1;
            var triggerEvent = info.System.Ewar && info.System.Pulse && !info.TriggeredPulse && info.System.EwarTriggerRange > 0;
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

                var dist = double.MaxValue;


                var shield = ent as IMyTerminalBlock;
                var grid = ent as MyCubeGrid;
                var voxel = ent as MyVoxelBase;

                if (triggerEvent && !info.Ai.Targets.ContainsKey(ent) && ent.Physics != null && ent.Physics.Enabled && ent.IsPreview)
                {
                    hitEnt.Hit = false;
                    dist = double.MaxValue;
                }
                else if (hitEnt.Projectile != null)
                {
                    dist = hitEnt.HitDist.Value;
                    hitEnt.Hit = true;
                    hitEnt.HitPos = hitEnt.Projectile.Position;
                }
                else if (shield != null)
                {
                    hitEnt.Hit = true;
                    dist = hitEnt.HitDist.Value;
                }
                else if (grid != null)
                {
                    if (hitEnt.Hit) dist = Vector3D.Distance(hitEnt.Intersection.From, hitEnt.HitPos.Value);
                    else
                    {
                        if (hitEnt.SphereCheck)
                        {
                            var ewarActive = hitEnt.EventType == Field || hitEnt.EventType == Effect;

                            var hitPos = !ewarActive ? hitEnt.PruneSphere.Center + (hitEnt.Intersection.Direction * hitEnt.PruneSphere.Radius) : hitEnt.PruneSphere.Center;
                            if (grid.IsSameConstructAs(hitEnt.Info.Ai.MyGrid) && Vector3D.DistanceSquared(hitPos, hitEnt.Info.Origin) <= grid.GridSize * grid.GridSize)
                                continue;

                            if (!ewarActive)
                                GetAndSortBlocksInSphere(hitEnt.Info.System, hitEnt.Info.Ai, grid, hitEnt.PruneSphere, false, hitEnt.Blocks);

                            if (hitEnt.Blocks.Count > 0 || ewarActive)
                            {
                                dist = 0;
                                hitEnt.Hit = true;
                                hitEnt.HitPos = hitPos;
                            }
                        }
                        else
                        {
                            grid.RayCastCells(beam.From, beam.To, slims, null, true, true);
                            var closestBlockFound = false;
                            var rotMatrix = Quaternion.CreateFromRotationMatrix(grid.PositionComp.WorldMatrix);
                            for (int j = 0; j < slims.Count; j++)
                            {
                                var firstBlock = grid.GetCubeBlock(slims[j]) as IMySlimBlock;
                                if (firstBlock != null && !firstBlock.IsDestroyed && firstBlock != hitEnt.Info.Target.FiringCube.SlimBlock)
                                {
                                    hitEnt.Blocks.Add(firstBlock);
                                    if (closestBlockFound) continue;
                                    MyOrientedBoundingBoxD obb;
                                    var fat = firstBlock.FatBlock;
                                    if (fat != null)
                                        obb = new MyOrientedBoundingBoxD(fat.Model.BoundingBox, fat.PositionComp.WorldMatrix);
                                    else
                                    {
                                        Vector3D center;
                                        firstBlock.ComputeWorldCenter(out center);
                                        Vector3 halfExt;
                                        firstBlock.ComputeScaledHalfExtents(out halfExt);
                                        var blockBox = new BoundingBoxD(-halfExt, halfExt);
                                        obb = new MyOrientedBoundingBoxD(center, blockBox.HalfExtents, rotMatrix);
                                    }

                                    var hitDist = obb.Intersects(ref beam) ?? Vector3D.Distance(beam.From, obb.Center);
                                    var hitPos = beam.From + (beam.Direction * hitDist);
                                    if (grid.IsSameConstructAs(hitEnt.Info.Ai.MyGrid) && Vector3D.DistanceSquared(hitPos, hitEnt.Info.Origin) < 10)
                                    {
                                        hitEnt.Blocks.Clear();
                                        break;
                                    }

                                    dist = hitDist;
                                    hitEnt.Hit = true;
                                    hitEnt.HitPos = hitPos;
                                    closestBlockFound = true;
                                }
                            }
                        }
                    }
                }
                else if (voxel != null)
                {
                    hitEnt.Hit = true;
                    dist = hitEnt.HitDist.Value;
                }
                else if (ent is IMyDestroyableObject)
                {
                    if (hitEnt.Hit) dist = Vector3D.Distance(hitEnt.Intersection.From, hitEnt.HitPos.Value);
                    else
                    {
                        if (hitEnt.SphereCheck)
                        {
                            var ewarActive = hitEnt.EventType == Field || hitEnt.EventType == Effect;

                            dist = 0;
                            hitEnt.Hit = true;
                            var hitPos = !ewarActive
                                ? hitEnt.PruneSphere.Center + (hitEnt.Intersection.Direction * hitEnt.PruneSphere.Radius)
                                : hitEnt.PruneSphere.Center;
                            hitEnt.HitPos = hitPos;
                        }
                        else
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
                    }
                }

                if (isX) xDist = dist;
                else yDist = dist;
            }
            V3Pool.Return(slims);
            return xDist.CompareTo(yDist);
        }

        internal static void GetAndSortBlocksInSphere(WeaponSystem system, GridAi ai, MyCubeGrid grid, BoundingSphereD sphere, bool fatOnly, List<IMySlimBlock> blocks)
        {
            var matrixNormalizedInv = grid.PositionComp.WorldMatrixNormalizedInv;
            Vector3D result;
            Vector3D.Transform(ref sphere.Center, ref matrixNormalizedInv, out result);
            var localSphere = new BoundingSphere(result, (float)sphere.Radius);
            var fieldType = system.Values.Ammo.AreaEffect.AreaEffect;
            var hitPos = sphere.Center;
            if (fatOnly)
            {
                foreach (var cube in ai.Session.GridToFatMap[grid].MyCubeBocks)
                {
                    if (!(cube is IMyTerminalBlock)) continue;
                    switch (fieldType)
                    {
                        case JumpNullField:
                            if (!(cube is MyJumpDrive)) continue;
                            break;
                        case EnergySinkField:
                            if (!(cube is IMyPowerProducer)) continue;
                            break;
                        case AnchorField:
                            if (!(cube is MyThrust)) continue;
                            break;
                        case NavField:
                            if (!(cube is MyGyro)) continue;
                            break;
                        case OffenseField:
                            if (!(cube is IMyGunBaseUser)) continue;
                            break;
                        case EmpField:
                        case DotField:
                            break;
                        default: continue;
                    }
                    var block = cube.SlimBlock as IMySlimBlock;
                    if (!new BoundingBox(block.Min * grid.GridSize - grid.GridSizeHalf, block.Max * grid.GridSize + grid.GridSizeHalf).Intersects(localSphere))
                        continue;
                    blocks.Add(block);
                }
            }
            else
            {
                foreach (IMySlimBlock block in grid.GetBlocks())
                {
                    if (block.IsDestroyed) continue;
                    if (!new BoundingBox(block.Min * grid.GridSize - grid.GridSizeHalf, block.Max * grid.GridSize + grid.GridSizeHalf).Intersects(localSphere))
                        continue;
                    blocks.Add(block);
                }
            }

            blocks.Sort((a, b) =>
            {
                var aPos = grid.GridIntegerToWorld(a.Position);
                var bPos = grid.GridIntegerToWorld(b.Position);
                return Vector3D.DistanceSquared(aPos, hitPos).CompareTo(Vector3D.DistanceSquared(bPos, hitPos));
            });
        }
        /*
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
        */
    }
}
