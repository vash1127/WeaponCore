using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRageMath;
using WeaponCore.Platform;
using WeaponCore.Projectiles;
using static WeaponCore.Support.SubSystemDefinition.BlockTypes;

namespace WeaponCore.Support
{
    public partial class GridAi
    {
        internal GridAi(MyCubeGrid grid, Session mySession)
        {
            MyGrid = grid;
            MySession = mySession;
            Targeting = MyGrid.Components.Get<MyGridTargeting>();
            Rnd = new Random((int)MyGrid.EntityId);
        }

        internal static void AcquireTarget(Weapon w)
        {
            w.LastTargetCheck = 0;
            var target = w.NewTarget;
            var physics = MyAPIGateway.Physics;
            var weaponPos = w.Comp.MyPivotPos;
            var ai = w.Comp.Ai;
            for (int i = 0; i < ai.SortedTargets.Count; i++)
            {
                var info = ai.SortedTargets[i];
                if (info.Target == null || info.Target.MarkedForClose || Vector3D.DistanceSquared(info.EntInfo.Position, w.Comp.MyPivotPos) > w.System.MaxTrajectorySqr) continue;

                if (w.TrackingAi)
                {
                    if (!Weapon.TrackingTarget(w, info.Target)) continue;
                }
                else if (!Weapon.ValidTarget(w, info.Target, true)) continue;

                if (info.IsGrid)
                {
                    if (!AcquireBlock(w.System, ref target, info, weaponPos, w)) continue;
                    return;
                }

                var targetPos = info.Target.PositionComp.WorldAABB.Center;
                IHitInfo hitInfo;
                physics.CastRay(weaponPos, targetPos, out hitInfo, 15, true);
                if (hitInfo?.HitEntity == info.Target)
                {
                    Log.Line($"{w.System.WeaponName} - found something");

                    double rayDist;
                    Vector3D.Distance(ref weaponPos, ref targetPos, out rayDist);
                    var shortDist = rayDist * (1 - hitInfo.Fraction);
                    var origDist = rayDist * hitInfo.Fraction;
                    var topEntId = info.Target.GetTopMostParent().EntityId;
                    target.Set(info.Target, hitInfo.Position, shortDist, origDist, topEntId);
                    return;
                }
            }
            Log.Line($"{w.System.WeaponName} - no valid target returned - oldTargetNull:{target.Entity == null} - oldTargetMarked:{target.Entity?.MarkedForClose} - checked: {w.Comp.Ai.SortedTargets.Count} - Total:{w.Comp.Ai.Targeting.TargetRoots.Count}");
            target.Reset();
            w.LastTargetCheck = 1;
            w.TargetExpired = true;
        }

        internal static bool ReacquireTarget(Projectile p)
        {
            p.ChaseAge = p.Age;
            var physics = MyAPIGateway.Physics;
            var ai = p.Ai;
            var weaponPos = p.Position;
            var target = p.Target;
            for (int i = 0; i < ai.SortedTargets.Count; i++)
            {
                var info = ai.SortedTargets[i];
                if (info.Target == null || info.Target.MarkedForClose || Vector3D.DistanceSquared(info.EntInfo.Position, p.Position) > p.DistanceToTravelSqr) continue;

                if (info.IsGrid)
                {
                    if (!AcquireBlock(p.System, ref target, info, weaponPos)) continue;
                    return true;
                }

                var targetPos = info.Target.PositionComp.WorldAABB.Center;
                IHitInfo hitInfo;
                physics.CastRay(weaponPos, targetPos, out hitInfo, 15, true);
                if (hitInfo?.HitEntity == info.Target)
                {
                    Log.Line($"{p.System.WeaponName} - found something");

                    double rayDist;
                    Vector3D.Distance(ref weaponPos, ref targetPos, out rayDist);
                    var shortDist = rayDist * (1 - hitInfo.Fraction);
                    var origDist = rayDist * hitInfo.Fraction;
                    var topEntId = info.Target.GetTopMostParent().EntityId;
                    target.Set(info.Target, hitInfo.Position, shortDist, origDist, topEntId);
                    return true;
                }
            }
            Log.Line($"{p.System.WeaponName} - no valid target returned - oldTargetNull:{target.Entity == null} - oldTargetMarked:{target.Entity?.MarkedForClose} - checked: {p.Ai.SortedTargets.Count} - Total:{p.Ai.Targeting.TargetRoots.Count}");
            target.Reset();
            return false;
        }

        private static bool AcquireBlock(WeaponSystem system, ref Target target, TargetInfo info, Vector3D currentPos, Weapon w = null)
        {
            if (system.OrderedTargets)
            {
                var subSystems = system.Values.Targeting.SubSystems;
                foreach (var bt in subSystems.Systems)
                {
                    // Log.Line($"sort:{bt} - closestFirst:{w.System.Values.Targeting.SubSystems.ClosestFirst} - {info.TypeDict[bt].Count} - {(info.Target as MyCubeGrid).GetFatBlocks().Count}");
                    if (bt != Any && info.TypeDict[bt].Count > 0)
                    {
                        var subSystemList = info.TypeDict[bt];
                        if (subSystems.ClosestFirst)
                        {
                            //Log.Line($"trying: {bt}");
                            if (bt != target.LastBlockType) target.Top5.Clear();
                            target.LastBlockType = bt;
                            UtilsStatic.GetClosestHitableBlockOfType(subSystemList, ref target, currentPos, w);
                            if (target.Entity != null)
                            {
                                //Log.Line($"cloest was: {w.NewTarget.Entity.DebugName} - type:{bt} - partCount:{info.PartCount}");
                                return true;
                            }
                        }
                        else if (FindRandomBlock(system, ref target, currentPos, subSystemList, w != null)) return true;
                    }
                }
            }
            if (FindRandomBlock(system, ref target, currentPos, info.TypeDict[Any], w != null)) return true;
            Log.Line("no valid target in line of sight");
            return false;
        }

        private static bool FindRandomBlock(WeaponSystem system, ref Target target, Vector3D currentPos, List<MyCubeBlock> blockList, bool cast)
        {
            var totalBlocks = blockList.Count;
            var lastBlocks = system.Values.Targeting.TopBlocks;
            if (lastBlocks > 0 && totalBlocks < lastBlocks) lastBlocks = totalBlocks;
            int[] deck = null;
            if (lastBlocks > 0) deck = GetDeck(ref target.Deck, ref target.PrevDeckLength, 0, lastBlocks);
            var physics = MyAPIGateway.Physics;
            for (int i = 0; i < totalBlocks; i++)
            {
                int next = i;
                if (i < lastBlocks)
                {
                    if (deck != null) next = deck[i];
                }
                var block = blockList[next];
                if (block.MarkedForClose) continue;

                IHitInfo hitInfo;
                var blockPos = block.CubeGrid.GridIntegerToWorld(block.Position);
                double rayDist;
                if (cast)
                {
                    physics.CastRay(currentPos, blockPos, out hitInfo, 15, true);

                    if (hitInfo?.HitEntity == null || hitInfo.HitEntity is MyVoxelBase || hitInfo.HitEntity == target.MyCube.CubeGrid)
                        continue;

                    var hitGrid = hitInfo.HitEntity as MyCubeGrid;
                    if (hitGrid != null)
                    {
                        var relationship = target.MyCube.GetUserRelationToOwner(hitGrid.BigOwners[0]);
                        var enemy = relationship != MyRelationsBetweenPlayerAndBlock.Owner && relationship != MyRelationsBetweenPlayerAndBlock.FactionShare;
                        if (!enemy)
                        {
                            Log.Line($"failed because not enemy");
                            continue;
                        }
                    }
                    Vector3D.Distance(ref currentPos, ref blockPos, out rayDist);
                    var shortDist = rayDist * (1 - hitInfo.Fraction);
                    var origDist = rayDist * hitInfo.Fraction;
                    var topEntId = block.GetTopMostParent().EntityId;
                    target.Set(block, hitInfo.Position, shortDist, origDist, topEntId);
                    return true;
                }
                    Vector3D.Distance(ref currentPos, ref blockPos, out rayDist);
                    target.Set(block, block.PositionComp.WorldAABB.Center, rayDist, rayDist, block.GetTopMostParent().EntityId);
                    return true;
            }
            return false;
        }
    }
}
