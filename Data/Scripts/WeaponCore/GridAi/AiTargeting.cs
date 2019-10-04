using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;
using WeaponCore.Platform;
using WeaponCore.Projectiles;
using static WeaponCore.Support.TargetingDefinition.BlockTypes;

namespace WeaponCore.Support
{
    public partial class GridAi
    {
        internal static void AcquireTarget(Weapon w)
        {
            Session.Instance.TargetRequests++;
            //Log.Line($"check: {w.LastTargetCheck} - {w.TargetAttempts}");
            w.LastTargetCheck = 0;
            w.HitOther = false;
            var pCount = w.Comp.Ai.LiveProjectile.Count;
            var targetType = TargetType.None;

            if (w.SleepTargets)
                if (w.Comp.Ai.BlockCount != w.LastBlockCount)
                {
                    w.SleepingTargets.Clear();
                }

            w.Comp.UpdatePivotPos(w);
            w.AimCone.ConeDir = w.Comp.MyPivotDir;
            w.AimCone.ConeTip = w.Comp.MyPivotPos;

            var shootProjectile = pCount > 0 && w.System.TrackProjectile;
            var projectilesFirst = shootProjectile && w.System.Values.Targeting.Threats.Length > 0 && w.System.Values.Targeting.Threats[0] == TargetingDefinition.Threat.Projectiles;
            if (!projectilesFirst && w.System.TrackOther) AcquireOther(w, out targetType);
            else if (targetType == TargetType.None && shootProjectile) AcquireProjectile(w, out targetType);

            if (projectilesFirst && targetType == TargetType.None) AcquireOther(w, out targetType);
            if (targetType == TargetType.None)
            {
                w.NewTarget.Reset(false);
                if (w.TargetAttempts < w.MaxAttempts) w.TargetDelayMulti++;
                else
                {
                    w.SleepTargets = true;
                    w.LastBlockCount = w.Comp.Ai.BlockCount;
                }
                w.TargetAttempts++;
                w.LastTargetCheck = 1;
                w.Target.Expired = true;
            }
            else
                w.WakeTargets();

            //if (!w.HitOther) Log.Line("didn't hit other'");
        }

        internal static bool ReacquireTarget(Projectile p)
        {
            p.ChaseAge = p.Age;
            var physics = Session.Instance.Physics;
            var s = p.T.System;
            var ai = p.T.Ai;
            var weaponPos = p.Position;

            for (int i = 0; i < ai.SortedTargets.Count; i++)
            {
                var info = ai.SortedTargets[i];
                if (info.Target == null || info.Target.MarkedForClose || !info.Target.InScene || (info.EntInfo.Relationship == MyRelationsBetweenPlayerAndBlock.Neutral && !s.TrackNeutrals)) continue;

                var targetRadius = info.Target.PositionComp.LocalVolume.Radius;
                if (targetRadius < s.MinTargetRadius || targetRadius > s.MaxTargetRadius) continue;

                var targetPos = info.Target.PositionComp.WorldAABB.Center;
                if (Vector3D.DistanceSquared(targetPos, p.Position) > p.DistanceToTravelSqr || Obstruction(ref info, ref targetPos, p))
                    continue;

                if (info.IsGrid && s.TrackGrids)
                {
                    if (!AcquireBlock(p.T.System, p.T.Ai, p.T.Target, info, weaponPos)) continue;
                    return true;
                }

                var character = info.Target as IMyCharacter;
                if (character != null && !s.TrackCharacters) continue;

                var meteor = info.Target as IMyMeteor;
                if (meteor != null && !s.TrackMeteors) continue;

                IHitInfo hitInfo;
                physics.CastRay(weaponPos, targetPos, out hitInfo, 15, true);
                if (hitInfo.HitEntity == info.Target)
                {
                    double rayDist;
                    Vector3D.Distance(ref weaponPos, ref targetPos, out rayDist);
                    var shortDist = rayDist * (1 - hitInfo.Fraction);
                    var origDist = rayDist * hitInfo.Fraction;
                    var topEntId = info.Target.GetTopMostParent().EntityId;
                    p.T.Target.Set(info.Target, hitInfo.Position, shortDist, origDist, topEntId);
                    return true;
                }
            }
            //Log.Line($"{p.T.System.WeaponName} - no valid target returned - oldTargetNull:{target.Entity == null} - oldTargetMarked:{target.Entity?.MarkedForClose} - checked: {p.Ai.SortedTargets.Count} - Total:{p.Ai.Targeting.TargetRoots.Count}");
            p.T.Target.Reset(false);
            return false;
        }

        private static void AcquireOther(Weapon w, out TargetType targetType)
        {
            var ai = w.Comp.Ai;
            var physics = Session.Instance.Physics;
            var weaponPos = w.Comp.MyPivotPos;
            var target = w.NewTarget;
            var s = w.System;
            for (int i = 0; i < ai.SortedTargets.Count; i++)
            {
                Session.Instance.TargetChecks++;
                var info = ai.SortedTargets[i];

                if (info.Target == null || info.Target.MarkedForClose || !info.Target.InScene || (info.EntInfo.Relationship == MyRelationsBetweenPlayerAndBlock.Neutral && !s.TrackNeutrals)) continue;

                var targetRadius = info.Target.PositionComp.LocalVolume.Radius;
                if (targetRadius < s.MinTargetRadius || targetRadius > s.MaxTargetRadius) continue;

                var targetCenter = info.Target.PositionComp.WorldAABB.Center;

                if (Vector3D.DistanceSquared(targetCenter, w.Comp.MyPivotPos) > s.MaxTrajectorySqr) continue;
                Vector3D targetLinVel = info.Target.Physics?.LinearVelocity ?? Vector3D.Zero;

                if (info.IsGrid)
                {
                    double intercept;
                    var newCenter = w.Prediction != HardPointDefinition.Prediction.Off ? w.GetPredictedTargetPosition(targetCenter, targetLinVel, w.Prediction, out intercept) : targetCenter;
                    var targetSphere = info.Target.PositionComp.WorldVolume;
                    targetSphere.Center = newCenter;
                    if (!s.TrackGrids) continue;
                    if (w.TargetAttempts > w.MaxAttempts)
                    {
                        Vector3D oldDir;
                        var newDir = targetCenter - weaponPos;
                        if (w.SleepingTargets.TryGetValue(info.Target, out oldDir))
                        {
                            if (oldDir.Equals(newDir, 1E-01))
                                continue;
                            //Log.Line($"target:{info.Target.DebugName} moved");
                            var oldNormDir = Vector3D.Normalize(oldDir);
                            var newNormDir = Vector3D.Normalize(newDir);
                            if (oldNormDir.Equals(newNormDir, 1E-02))
                            {
                                //Log.Line($"target:{info.Target.DebugName} dirNorm equal");
                                continue;
                            }
                            //Log.Line($"target:{info.Target.DebugName} reTest");
                            w.SleepingTargets.Remove(info.Target);
                        }
                        else w.SleepingTargets.Add(info.Target, newDir);
                    }
                    Session.Instance.CanShoot++;
                    if (!w.TrackingAi && !MathFuncs.TargetSphereInCone(ref targetSphere, ref w.AimCone) || w.TrackingAi && !Weapon.CanShootTarget(w, ref targetCenter, ref targetLinVel)) continue;

                    if (!AcquireBlock(s, w.Comp.Ai, target, info, weaponPos, w)) continue;

                    targetType = TargetType.Other;
                    target.TransferTo(w.Target);

                    return;
                }

                var meteor = info.Target as MyMeteor;
                if (meteor != null && !s.TrackMeteors) continue;

                var character = info.Target as IMyCharacter;
                if (character != null && !s.TrackCharacters) continue;

                if (!Weapon.CanShootTarget(w, ref targetCenter, ref targetLinVel)) continue;
                var targetPos = info.Target.PositionComp.WorldAABB.Center;
                Session.Instance.RayCasts++;
                IHitInfo hitInfo;
                physics.CastRay(weaponPos, targetPos, out hitInfo, 15, true);
                if (hitInfo != null && hitInfo.HitEntity == info.Target)
                {
                    double rayDist;
                    Vector3D.Distance(ref weaponPos, ref targetPos, out rayDist);
                    var shortDist = rayDist * (1 - hitInfo.Fraction);
                    var origDist = rayDist * hitInfo.Fraction;
                    var topEntId = info.Target.GetTopMostParent().EntityId;
                    target.Set(info.Target, hitInfo.Position, shortDist, origDist, topEntId);
                    targetType = TargetType.Other;
                    target.TransferTo(w.Target);
                    return;
                }
            }
            targetType = TargetType.None;
        }

        private static bool Obstruction(ref TargetInfo info, ref Vector3D targetPos, Projectile p)
        {
            var ai = p.T.Ai;
            var obstruction = false;
            for (int j = 0; j < ai.Obstructions.Count; j++)
            {
                var ent = ai.Obstructions[j];
                var voxel = ent as MyVoxelBase;
                var dir = (targetPos - p.Position);
                if (voxel != null)
                {
                    if (new RayD(ref p.Position, ref dir).Intersects(ent.PositionComp.WorldVolume) != null)
                    {
                        var dirNorm = Vector3D.Normalize(dir);
                        var targetDist = Vector3D.Distance(p.Position, targetPos);
                        var tRadius = info.Target.PositionComp.LocalVolume.Radius;
                        var testPos = p.Position + (dirNorm * (targetDist - tRadius));
                        var lineTest = new LineD(p.Position, testPos);
                        Vector3D? voxelHit = null;
                        var rotMatrix = Quaternion.CreateFromRotationMatrix(ent.WorldMatrix);
                        var obb = new MyOrientedBoundingBoxD(ent.PositionComp.WorldAABB.Center, ent.PositionComp.LocalAABB.HalfExtents, rotMatrix);

                        if (obb.Intersects(ref lineTest) != null)
                            voxel.RootVoxel.GetIntersectionWithLine(ref lineTest, out voxelHit);

                        obstruction = voxelHit.HasValue;
                        if (obstruction)
                            break;
                    }
                }
                else
                {
                    if (new RayD(ref p.Position, ref dir).Intersects(ent.PositionComp.WorldVolume) != null)
                    {
                        var rotMatrix = Quaternion.CreateFromRotationMatrix(ent.WorldMatrix);
                        var obb = new MyOrientedBoundingBoxD(ent.PositionComp.WorldAABB.Center, ent.PositionComp.LocalAABB.HalfExtents, rotMatrix);
                        var lineTest = new LineD(p.Position, targetPos);
                        if (obb.Intersects(ref lineTest) != null)
                        {
                            obstruction = true;
                            break;
                        }
                    }
                }
            }

            if (!obstruction)
            {
                var dir = (targetPos - p.Position);
                var ray = new RayD(ref p.Position, ref dir);
                var dist = ai.MyGrid.PositionComp.WorldVolume.Intersects(ray);
                if (dist.HasValue)
                {
                    var rotMatrix = Quaternion.CreateFromRotationMatrix(ai.MyGrid.WorldMatrix);
                    var obb = new MyOrientedBoundingBoxD(ai.MyGrid.PositionComp.WorldAABB.Center, ai.MyGrid.PositionComp.LocalAABB.HalfExtents, rotMatrix);
                    if (obb.Intersects(ref ray) != null)
                        obstruction = ai.MyGrid.RayCastBlocks(p.Position, targetPos) != null;
                }

                if (!obstruction)
                {
                    foreach (var sub in ai.SubGrids)
                    {
                        var subDist = sub.PositionComp.WorldVolume.Intersects(ray);
                        if (subDist.HasValue)
                        {
                            var rotMatrix = Quaternion.CreateFromRotationMatrix(ai.MyGrid.WorldMatrix);
                            var obb = new MyOrientedBoundingBoxD(ai.MyGrid.PositionComp.WorldAABB.Center, ai.MyGrid.PositionComp.LocalAABB.HalfExtents, rotMatrix);
                            if (obb.Intersects(ref ray) != null)
                                obstruction = sub.RayCastBlocks(p.Position, targetPos) != null;
                        }

                        if (obstruction) break;
                    }
                }
            }
            return obstruction;
        }

        private static void AcquireProjectile(Weapon w, out TargetType targetType)
        {
            var wCache = w.WeaponCache;
            var ai = w.Comp.Ai;
            var s = w.System;
            var collection = s.ClosestFirst ? wCache.SortProjetiles : ai.LiveProjectile as IEnumerable<Projectile>;
            wCache.SortProjectiles(w);
            var physics = Session.Instance.Physics;
            var target = w.NewTarget;
            var weaponPos = w.Comp.MyPivotPos;
            foreach (var lp in collection)
            {
                Session.Instance.ProjectileChecks++;
                if (lp.MaxSpeed > s.MaxTargetSpeed || lp.MaxSpeed <= 0) continue;
                if (Weapon.CanShootTarget(w, ref lp.Position, ref lp.Velocity))
                {
                    var needsCast = false;
                    for (int i = 0; i < ai.Obstructions.Count; i++)
                    {
                        var ent = ai.Obstructions[i];
                        var obsSphere = ent.PositionComp.WorldVolume;

                        var dir = lp.Position - weaponPos;
                        var beam = new RayD(ref weaponPos, ref dir);
                        
                        if (beam.Intersects(obsSphere) != null)
                        {
                            var rotMatrix = Quaternion.CreateFromRotationMatrix(ent.WorldMatrix);
                            var obb = new MyOrientedBoundingBoxD(ent.PositionComp.WorldAABB.Center, ent.PositionComp.LocalAABB.HalfExtents, rotMatrix);
                            if (obb.Intersects(ref beam) != null)
                            {
                                needsCast = true;
                                break;
                            }
                        }
                    }

                    if (needsCast)
                    {
                        IHitInfo hitInfo;
                        physics.CastRay(weaponPos, lp.Position, out hitInfo, 15, true);
                        if (hitInfo?.HitEntity == null)
                        {
                            double hitDist;
                            Vector3D.Distance(ref weaponPos, ref lp.Position, out hitDist);
                            var shortDist = hitDist;
                            var origDist = hitDist;
                            const long topEntId = long.MaxValue;
                            target.Set(null, lp.Position, shortDist, origDist, topEntId, lp);
                            targetType = TargetType.Projectile;
                            target.TransferTo(w.Target);
                            return;
                        }
                    }
                    else
                    {
                        double hitDist;
                        Vector3D.Distance(ref weaponPos, ref lp.Position, out hitDist);
                        var shortDist = hitDist;
                        var origDist = hitDist;
                        const long topEntId = long.MaxValue;
                        target.Set(null, lp.Position, shortDist, origDist, topEntId, lp);
                        targetType = TargetType.Projectile;
                        target.TransferTo(w.Target);
                        return;
                    }
                }
            }
            targetType = TargetType.None;
        }


        private static bool AcquireBlock(WeaponSystem system, GridAi ai, Target target, TargetInfo info, Vector3D weaponPos, Weapon w = null)
        {
            if (system.TargetSubSystems)
            {
                var subSystems = system.Values.Targeting.SubSystems;
                var targetLinVel = info.Target.Physics?.LinearVelocity ?? Vector3D.Zero;
                foreach (var bt in subSystems)
                {
                    if (bt != Any && info.TypeDict[bt].Count > 0)
                    {
                        var subSystemList = info.TypeDict[bt];
                        if (system.ClosestFirst)
                        {
                            if (bt != target.LastBlockType) target.Top5.Clear();
                            target.LastBlockType = bt;
                            GetClosestHitableBlockOfType(subSystemList, ai, target, weaponPos, targetLinVel, system, w);
                            if (target.Entity != null) return true;
                        }
                        else if (FindRandomBlock(system, ai, target, weaponPos, info, w)) return true;
                    }
                }
                if (system.OnlySubSystems) return false;
            }
            if (FindRandomBlock(system, ai, target, weaponPos, info, w)) return true;
            return false;
        }

        private static bool FindRandomBlock(WeaponSystem system, GridAi ai, Target target, Vector3D weaponPos, TargetInfo info, Weapon w)
        {
            var blockList = info.TypeDict[Any];
            var totalBlocks = blockList.Count;
            var lastBlocks = system.Values.Targeting.TopBlocks;
            if (lastBlocks > 0 && totalBlocks < lastBlocks) lastBlocks = totalBlocks;
            int[] deck = null;
            if (lastBlocks > 0) deck = GetDeck(ref target.Deck, ref target.PrevDeckLength, 0, lastBlocks);
            var physics = Session.Instance.Physics;
            var turretCheck = w != null;
            var grid = info.Target.GetTopMostParent() as IMyCubeGrid;
            var gridPhysics = grid?.Physics;
            Vector3D targetLinVel = gridPhysics?.LinearVelocity ?? Vector3D.Zero;
            var notSelfHit = false;
            var foundBlock = false;

            for (int i = 0; i < totalBlocks; i++)
            {
                var next = i;
                if (i < lastBlocks)
                    if (deck != null) next = deck[i];

                var block = blockList[next];
                Session.Instance.BlockChecks++;
                if (block.MarkedForClose) continue;
                var blockPos = block.CubeGrid.GridIntegerToWorld(block.Position);
                double rayDist;
                if (turretCheck)
                {
                    Session.Instance.CanShoot++;
                    if (!Weapon.CanShootTarget(w, ref blockPos, ref targetLinVel)) continue;

                    if (!w.HitOther && GridIntersection.BresenhamGridIntersection(ai.MyGrid, weaponPos, blockPos))
                        continue;

                    Session.Instance.RayCasts++;
                    IHitInfo hitInfo;
                    physics.CastRay(weaponPos, blockPos, out hitInfo, 15, true);

                    if (hitInfo == null || hitInfo.HitEntity != ai.MyGrid)
                        notSelfHit = true;

                    if (hitInfo?.HitEntity == null || hitInfo.HitEntity is MyVoxelBase || hitInfo.HitEntity == ai.MyGrid)
                        continue;

                    var hitGrid = hitInfo.HitEntity as MyCubeGrid;
                    if (hitGrid != null)
                    {
                        if (hitGrid.MarkedForClose || !hitGrid.InScene) continue;
                        bool enemy;

                        var bigOwners = hitGrid.BigOwners;
                        if (bigOwners.Count == 0) enemy = true;
                        else
                        {
                            var relationship = target.FiringCube.GetUserRelationToOwner(hitGrid.BigOwners[0]);
                            enemy = relationship != MyRelationsBetweenPlayerAndBlock.Owner && relationship != MyRelationsBetweenPlayerAndBlock.FactionShare;
                        }
                        if (!enemy)
                            continue;
                    }
                    Vector3D.Distance(ref weaponPos, ref blockPos, out rayDist);
                    var shortDist = rayDist * (1 - hitInfo.Fraction);
                    var origDist = rayDist * hitInfo.Fraction;
                    var topEntId = block.GetTopMostParent().EntityId;
                    target.Set(block, hitInfo.Position, shortDist, origDist, topEntId);
                    foundBlock = true;
                    break;
                }
                Vector3D.Distance(ref weaponPos, ref blockPos, out rayDist);
                target.Set(block, block.PositionComp.WorldAABB.Center, rayDist, rayDist, block.GetTopMostParent().EntityId);
                foundBlock = true;
                break;
            }

            if (turretCheck && !notSelfHit) w.HitOther = true;
            return foundBlock;
        }

        internal static void GetClosestHitableBlockOfType(List<MyCubeBlock> cubes, GridAi ai, Target target, Vector3D currentPos, Vector3D targetLinVel, WeaponSystem system, Weapon w = null)
        {
            var minValue = double.MaxValue;
            var minValue0 = double.MaxValue;
            var minValue1 = double.MaxValue;
            var minValue2 = double.MaxValue;
            var minValue3 = double.MaxValue;

            MyCubeBlock newEntity = null;
            MyCubeBlock newEntity0 = null;
            MyCubeBlock newEntity1 = null;
            MyCubeBlock newEntity2 = null;
            MyCubeBlock newEntity3 = null;
            var bestCubePos = Vector3D.Zero;
            var top5Count = target.Top5.Count;
            var testPos = currentPos;
            var top5 = target.Top5;
            var physics = Session.Instance.Physics;
            IHitInfo hitInfo = null;
            var notSelfHit = false;
            for (int i = 0; i < cubes.Count + top5Count; i++)
            {
                Session.Instance.BlockChecks++;
                var index = i < top5Count ? i : i - top5Count;
                var cube = i < top5Count ? top5[index] : cubes[index];
                if (cube.MarkedForClose || cube == newEntity || cube == newEntity0 || cube == newEntity1 || cube == newEntity2 || cube == newEntity3) continue;
                var grid = cube.CubeGrid;
                var cubePos = grid.GridIntegerToWorld(cube.Position);
                var range = cubePos - testPos;
                var test = (range.X * range.X) + (range.Y * range.Y) + (range.Z * range.Z);
                if (test < minValue3)
                {
                    IHitInfo hit = null;
                    var best = test < minValue;
                    var bestTest = false;
                    if (best)
                    {
                        if (w != null && !(!w.IsTurret && system.Values.Ammo.Trajectory.Smarts.OverideTarget))
                        {
                            Session.Instance.CanShoot++;
                            
                            var canShoot = Weapon.CanShootTarget(w, ref cubePos, ref targetLinVel);
                            var castRay = false;
                            if (canShoot)
                            {
                                if (!w.HitOther)
                                {
                                    if (!GridIntersection.BresenhamGridIntersection(ai.MyGrid, testPos, cubePos))
                                        castRay = true;
                                    else continue;
                                }
                                else castRay = true;
                                Session.Instance.RayCasts++;
                            }
                            bestTest = castRay && physics.CastRay(testPos, cubePos, out hit, 15, true) && hit?.HitEntity == cube.CubeGrid;
                            if (hit == null || hit.HitEntity != ai.MyGrid)
                                notSelfHit = true;
                        }
                        else bestTest = true;
                    }
                    if (best && bestTest)
                    {
                        minValue3 = minValue2;
                        newEntity3 = newEntity2;
                        minValue2 = minValue1;
                        newEntity2 = newEntity1;
                        minValue1 = minValue0;
                        newEntity1 = newEntity0;
                        minValue0 = minValue;
                        newEntity0 = newEntity;
                        minValue = test;

                        newEntity = cube;
                        bestCubePos = cubePos;
                        hitInfo = hit;
                    }
                    else if (test < minValue0)
                    {
                        minValue3 = minValue2;
                        newEntity3 = newEntity2;
                        minValue2 = minValue1;
                        newEntity2 = newEntity1;
                        minValue1 = minValue0;
                        newEntity1 = newEntity0;
                        minValue0 = test;

                        newEntity0 = cube;
                    }
                    else if (test < minValue1)
                    {
                        minValue3 = minValue2;
                        newEntity3 = newEntity2;
                        minValue2 = minValue1;
                        newEntity2 = newEntity1;
                        minValue1 = test;

                        newEntity1 = cube;
                    }
                    else if (test < minValue2)
                    {
                        minValue3 = minValue2;
                        newEntity3 = newEntity2;
                        minValue2 = test;

                        newEntity2 = cube;
                    }
                    else
                    {
                        minValue3 = test;
                        newEntity3 = cube;
                    }
                }

            }
            top5.Clear();
            if (newEntity != null && hitInfo != null)
            {
                double rayDist;
                Vector3D.Distance(ref testPos, ref bestCubePos, out rayDist);
                var shortDist = rayDist * (1 - hitInfo.Fraction);
                var origDist = rayDist * hitInfo.Fraction;
                var topEntId = newEntity.GetTopMostParent().EntityId;
                target.Set(newEntity, hitInfo.Position, shortDist, origDist, topEntId);
                top5.Add(newEntity);
            }
            else if (newEntity != null)
            {
                double rayDist;
                Vector3D.Distance(ref testPos, ref bestCubePos, out rayDist);
                var shortDist = rayDist;
                var origDist = rayDist;
                var topEntId = newEntity.GetTopMostParent().EntityId;
                target.Set(newEntity, bestCubePos, shortDist, origDist, topEntId);
                top5.Add(newEntity);
            }
            else target.Reset(false);

            if (newEntity0 != null) top5.Add(newEntity0);
            if (newEntity1 != null) top5.Add(newEntity1);
            if (newEntity2 != null) top5.Add(newEntity2);   
            if (newEntity3 != null) top5.Add(newEntity3);

            if (!notSelfHit) w.HitOther = true;
        }
    }
}
