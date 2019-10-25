using System.Collections.Concurrent;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Entity;
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
        internal static void AcquireTarget(Weapon w, bool attemptReset = false, MyEntity targetGrid = null)
        {
            w.HitOther = false;
            var tick = w.Comp.Ai.Session.Tick;
            w.TargetCheckTick = tick;
            var pCount = w.Comp.Ai.LiveProjectile.Count;
            var targetType = TargetType.None;

            if (w.SleepTargets)
            {
                if (w.Comp.Ai.BlockCount != w.LastBlockCount && tick - w.LastTargetTick > 300)
                    w.SleepingTargets.Clear();
            }

            w.UpdatePivotPos();
            w.AimCone.ConeDir = w.MyPivotDir;
            w.AimCone.ConeTip = w.MyPivotPos;

            var shootProjectile = pCount > 0 && w.System.TrackProjectile;
            var projectilesFirst = !attemptReset && shootProjectile && w.System.Values.Targeting.Threats.Length > 0 && w.System.Values.Targeting.Threats[0] == TargetingDefinition.Threat.Projectiles;

            if (!projectilesFirst && w.System.TrackOther) AcquireOther(w, out targetType, attemptReset, targetGrid);
            else if (!attemptReset && targetType == TargetType.None && shootProjectile) AcquireProjectile(w, out targetType);
            if (projectilesFirst && targetType == TargetType.None) AcquireOther(w, out targetType, false, targetGrid);

            //Log.Line($"targetType: {targetType}");
            if (targetType == TargetType.None)
            {
                w.NewTarget.Reset(false);
                w.SleepTargets = true;
                w.LastBlockCount = w.Comp.Ai.BlockCount;
                w.Target.Expired = true;
            }
            else w.WakeTargets();
        }

        internal static bool ReacquireTarget(Projectile p)
        {
            p.ChaseAge = p.Age;
            var physics = p.T.Ai.Session.Physics;
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

        private static void AcquireOther(Weapon w, out TargetType targetType, bool attemptReset = false, MyEntity targetGrid = null)
        {
            var ai = w.Comp.Ai;
            ai.Session.TargetRequests++;
            var physics = ai.Session.Physics;
            var weaponPos = w.MyPivotPos;
            var target = w.NewTarget;
            var s = w.System;
            var accelPrediction = (int) s.Values.HardPoint.AimLeadingPrediction > 1;
            TargetInfo primeInfo = null;
            if (ai.PrimeTarget != null)
                ai.Targets.TryGetValue(ai.PrimeTarget, out primeInfo);

            TargetInfo gridInfo = null;
            var forceTarget = false;
            if (targetGrid != null)
                if(ai.Targets.TryGetValue(targetGrid, out gridInfo))
                    forceTarget = true;

            var targetCount = ai.SortedTargets.Count;
            var needOffset = primeInfo != null;
            var offset = needOffset ? 1 : 0;
            var adjTargetCount = needOffset ? targetCount + offset : targetCount;
            for (int x = 0; x < adjTargetCount; x++)
            {
                if (attemptReset && x > 0) break;
                var primeTarget = x < 1 && needOffset;
                var info = !forceTarget ? primeTarget ? primeInfo : ai.SortedTargets[x - offset] : gridInfo;
                if (info?.Target == null || needOffset && x > 0 && info.Target == primeInfo.Target || info.Target.MarkedForClose || !info.Target.InScene || (info.EntInfo.Relationship == MyRelationsBetweenPlayerAndBlock.Neutral && !s.TrackNeutrals)) continue;
                var targetRadius = info.Target.PositionComp.LocalVolume.Radius;

                if (targetRadius < s.MinTargetRadius || targetRadius > s.MaxTargetRadius || !primeTarget && info.OffenseRating == 0) continue;
                var targetCenter = info.Target.PositionComp.WorldAABB.Center;

                if (Vector3D.DistanceSquared(targetCenter, w.MyPivotPos) > s.MaxTrajectorySqr) continue;
                w.Comp.Ai.Session.TargetChecks++;

                Vector3D targetLinVel = info.Target.Physics?.LinearVelocity ?? Vector3D.Zero;
                Vector3D targetAccel = accelPrediction ? info.Target.Physics?.LinearAcceleration ?? Vector3D.Zero : Vector3.Zero;

                if (info.IsGrid)
                {
                    double intercept;
                    var newCenter = w.Prediction != HardPointDefinition.Prediction.Off ? w.GetPredictedTargetPosition(targetCenter, targetLinVel, targetAccel, w.Prediction, out intercept) : targetCenter;
                    var targetSphere = info.Target.PositionComp.WorldVolume;
                    targetSphere.Center = newCenter;
                    if (!s.TrackGrids) continue;
                    if (w.SleepTargets)
                    {
                        Vector3D oldDir;
                        var newDir = targetCenter - weaponPos;
                        if (w.SleepingTargets.TryGetValue(info.Target, out oldDir))
                        {
                            if (oldDir.Equals(newDir, 1E-01))
                                continue;

                            var oldNormDir = Vector3D.Normalize(oldDir);
                            var newNormDir = Vector3D.Normalize(newDir);
                            var dotDirChange = Vector3D.Dot(oldNormDir, newNormDir);
                            if (dotDirChange < ai.Session.AimDirToleranceCosine)
                                continue;

                            w.SleepingTargets.Remove(info.Target);
                        }
                        else w.SleepingTargets.Add(info.Target, newDir);
                    }
                    ai.Session.CanShoot++;


                    if (!w.TrackingAi && !MathFuncs.TargetSphereInCone(ref targetSphere, ref w.AimCone) || w.TrackingAi && !Weapon.CanShootTargetObb(w, info.Target, targetLinVel, targetAccel)) continue;


                    if (!AcquireBlock(s, w.Comp.Ai, target, info, weaponPos, w)) continue;

                    targetType = TargetType.Other;
                    target.TransferTo(w.Target);

                    return;
                }


                var meteor = info.Target as MyMeteor;
                if (meteor != null && !s.TrackMeteors) continue;

                var character = info.Target as IMyCharacter;
                if (character != null && !s.TrackCharacters) continue;
                //if(!Weapon.CanShootTarget(w, targetCenter, targetLinVel, targetAccel)) continue;
                if (!Weapon.CanShootTargetObb(w, info.Target, targetLinVel, targetAccel)) continue;
                var targetPos = info.Target.PositionComp.WorldAABB.Center;
                ai.Session.TopRayCasts++;
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
                if (forceTarget) break;
            }
            if (!attemptReset || w.Target.Expired) targetType = TargetType.None;
            else targetType = w.Target.IsProjectile ? TargetType.Projectile : TargetType.Other;
        }
        
        private static bool AcquireBlock(WeaponSystem system, GridAi ai, Target target, TargetInfo info, Vector3D weaponPos, Weapon w = null)
        {
            if (system.TargetSubSystems)
            {
                var subSystems = system.Values.Targeting.SubSystems;
                var targetLinVel = info.Target.Physics?.LinearVelocity ?? Vector3D.Zero;
                var targetAccel = (int)system.Values.HardPoint.AimLeadingPrediction > 1 ? info.Target.Physics?.LinearAcceleration ?? Vector3D.Zero : Vector3.Zero;

                foreach (var bt in subSystems)
                {
                    ConcurrentDictionary<TargetingDefinition.BlockTypes, MyConcurrentList<MyCubeBlock>> blockTypeMap;
                    ai.Session.GridToBlockTypeMap.TryGetValue((MyCubeGrid) info.Target, out blockTypeMap);
                    if (bt != Any && blockTypeMap != null && blockTypeMap[bt].Count > 0)
                    {
                        var subSystemList = blockTypeMap[bt];
                        if (system.ClosestFirst)
                        {
                            if (target.Top5.Count > 0 && (bt != target.LastBlockType || target.Top5[0].CubeGrid != subSystemList[0].CubeGrid))
                                target.Top5.Clear();

                            target.LastBlockType = bt;
                            GetClosestHitableBlockOfType(subSystemList, ai, target, weaponPos, targetLinVel, targetAccel, system, w);
                            if (target.Entity != null) return true;
                        }
                        else if (FindRandomBlock(system, ai, target, weaponPos, info, subSystemList, w)) return true;
                    }
                }
                if (system.OnlySubSystems) return false;
            }
            MyConcurrentList<MyCubeBlock> fatList;
            ai.Session.GridToFatMap.TryGetValue((MyCubeGrid)info.Target, out fatList);
            return fatList != null && FindRandomBlock(system, ai, target, weaponPos, info, fatList, w);
        }

        private static bool FindRandomBlock(WeaponSystem system, GridAi ai, Target target, Vector3D weaponPos, TargetInfo info, MyConcurrentList<MyCubeBlock> subSystemList, Weapon w)
        {
            var totalBlocks = subSystemList.Count;

            var topEnt = info.Target.GetTopMostParent();
            var entSphere = topEnt.PositionComp.WorldVolume;
            var distToEnt = MyUtils.GetSmallestDistanceToSphere(ref weaponPos, ref entSphere);
            var turretCheck = w != null;
            var lastBlocks = system.Values.Targeting.TopBlocks > 10 && distToEnt < 1000 ? system.Values.Targeting.TopBlocks : 10;
            if (totalBlocks < lastBlocks) lastBlocks = totalBlocks;
            var deck = GetDeck(ref target.Deck, ref target.PrevDeckLength, 0, lastBlocks);
            var physics = ai.Session.Physics;
            var grid = topEnt as IMyCubeGrid;
            var gridPhysics = grid?.Physics;
            Vector3D targetLinVel = gridPhysics?.LinearVelocity ?? Vector3D.Zero;
            Vector3D targetAccel = (int)system.Values.HardPoint.AimLeadingPrediction > 1 ? info.Target.Physics?.LinearAcceleration ?? Vector3D.Zero : Vector3.Zero;
            var notSelfHit = false;
            var foundBlock = false;
            for (int i = 0; i < totalBlocks; i++)
            {
                if (turretCheck && i > lastBlocks)
                    break;

                var next = i;
                if (i < lastBlocks)
                    next = deck[i];

                var block = subSystemList[next];
                if (block.MarkedForClose) continue;

                ai.Session.BlockChecks++;

                var blockPos = block.CubeGrid.GridIntegerToWorld(block.Position);

                double rayDist;
                if (turretCheck)
                {
                    ai.Session.CanShoot++;
                    //if (!Weapon.CanShootTarget(w, blockPos, targetLinVel, targetAccel)) continue;
                    if (!Weapon.CanShootTargetObb(w, block, targetLinVel, targetAccel)) continue;

                    if (!w.HitOther && GridIntersection.BresenhamGridIntersection(ai.MyGrid, weaponPos, blockPos))
                        continue;

                    ai.Session.RandomRayCasts++;
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

        internal static void GetClosestHitableBlockOfType(MyConcurrentList<MyCubeBlock> cubes, GridAi ai, Target target, Vector3D currentPos, Vector3D targetLinVel, Vector3D targetAccel, WeaponSystem system, Weapon w = null)
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
            var physics = ai.Session.Physics;
            IHitInfo hitInfo = null;
            var notSelfHit = false;
            for (int i = 0; i < cubes.Count + top5Count; i++)
            {
                ai.Session.BlockChecks++;
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
                            ai.Session.CanShoot++;
                            var castRay = false;

                            //if (Weapon.CanShootTarget(w, cubePos, targetLinVel, targetAccel))
                            //  castRay = !w.HitOther || !GridIntersection.BresenhamGridIntersection(ai.MyGrid, testPos, cubePos);

                            if (Weapon.CanShootTargetObb(w, cube, targetLinVel, targetAccel))
                                castRay = !w.HitOther || !GridIntersection.BresenhamGridIntersection(ai.MyGrid, testPos, cubePos);

                            if (castRay)
                            {
                                ai.Session.ClosestRayCasts++;
                                bestTest = physics.CastRay(testPos, cubePos, out hit, 15, true) && hit?.HitEntity == cube.CubeGrid;

                                if (hit.HitEntity != ai.MyGrid || hit == null)
                                    notSelfHit = true;
                            }
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

        private static void AcquireProjectile(Weapon w, out TargetType targetType)
        {
            var wCache = w.WeaponCache;
            var ai = w.Comp.Ai;
            var s = w.System;
            var collection = s.ClosestFirst ? wCache.SortProjetiles : ai.LiveProjectile as IEnumerable<Projectile>;
            wCache.SortProjectiles(w);
            var physics = ai.Session.Physics;
            var target = w.NewTarget;
            var weaponPos = w.MyPivotPos;
            const Projectile.ProjectileState ignoreStates = (Projectile.ProjectileState)1;
            foreach (var lp in collection)
            {
                ai.Session.ProjectileChecks++;
                if (lp.MaxSpeed > s.MaxTargetSpeed || lp.MaxSpeed <= 0 || lp.State > ignoreStates) continue;
                if (lp.State != Projectile.ProjectileState.Alive && lp.State != Projectile.ProjectileState.Start) Log.Line($"invaid projectile state: {lp.State}");
                if (Weapon.CanShootTarget(w, lp.Position, lp.Velocity, lp.AccelVelocity))
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

    }
}
