using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;
using WeaponCore.Platform;
using WeaponCore.Projectiles;
using static WeaponCore.Support.SubSystemDefinition.BlockTypes;

namespace WeaponCore.Support
{
    public partial class GridTargetingAi
    {
        internal GridTargetingAi(MyCubeGrid grid, Session mySession)
        {
            MyGrid = grid;
            MySession = mySession;
            Targeting = MyGrid.Components.Get<MyGridTargeting>();
            Rnd = new Random((int)MyGrid.EntityId);
        }

        internal void SelectTarget(Weapon w)
        {
            Log.Line($"{w.System.WeaponName} - running select target");
            UpdateTarget(w, out var targetInfo);
            if (targetInfo.HasValue)
            {
                w.LastTargetCheck = 0;
                var grid = targetInfo.Value.Target as MyCubeGrid;
                if (grid == null)
                {
                    Log.Line($"{w.System.WeaponName} - targetting nonGrid: {w.System.WeaponName}");
                    return;
                }

                if (targetInfo.Value.TypeDict.Count <= 0)
                {
                    Log.Line($"{w.System.WeaponName} - sees no valid cubes: {w.System.WeaponName}");
                    w.NewTarget.Entity = null;
                    return;
                }
                return;
            }
            w.LastTargetCheck = 1;
            Log.Line($"{w.System.WeaponName} - no valid target returned - oldTargetNull:{w.NewTarget.Entity == null} - oldTargetMarked:{w.NewTarget.Entity?.MarkedForClose} - checked: {w.Comp.MyAi.SortedTargets.Count} - Total:{w.Comp.MyAi.Targeting.TargetRoots.Count}");
            w.NewTarget.Entity = null;
            w.TargetExpired = true;
        }

        internal void UpdateTarget(Weapon w, out TargetInfo? targetInfo)
        {
            var physics = MyAPIGateway.Physics;
            for (int i = 0; i < SortedTargets.Count; i++)
            {
                var info = SortedTargets[i];
                if (info.Target == null || info.Target.MarkedForClose || Vector3D.DistanceSquared(info.EntInfo.Position, w.Comp.MyPivotPos) > w.System.MaxTrajectorySqr) continue;

                if (w.TrackingAi)
                {
                    if (!Weapon.TrackingTarget(w, info.Target)) continue;
                }
                else if (!Weapon.ValidTarget(w, info.Target, true)) continue;

                if (info.IsGrid)
                {
                    AcquireBlock(w, info);
                    if (w.NewTarget.Entity == info.Target)
                    {
                        continue;
                    }
                    targetInfo = info;
                    return;
                }

                var weaponPos = w.Comp.MyPivotPos;
                var targetPos = info.Target.PositionComp.WorldAABB.Center;
                physics.CastRay(weaponPos, targetPos, out var hitInfo, 15, true);
                if (hitInfo?.HitEntity == info.Target)
                {
                    Log.Line($"{w.System.WeaponName} - found something");

                    double rayDist;
                    Vector3D.Distance(ref weaponPos, ref targetPos, out rayDist);
                    w.NewTarget.Entity = info.Target;
                    w.NewTarget.HitPos = hitInfo.Position;
                    w.NewTarget.HitShortDist = rayDist * (1 - hitInfo.Fraction);
                    w.NewTarget.OrigDistance = rayDist * hitInfo.Fraction;
                    w.NewTarget.TopEntityId = info.Target.GetTopMostParent().EntityId;
                    targetInfo = info;
                    return;
                }
            }
            Log.Line($"{w.System.WeaponName} - failed to update targets");
            targetInfo = null;
        }

        private void AcquireBlock(Weapon w, TargetInfo info)
        {
            var weaponPos = w.Comp.MyPivotPos;
            var blockList = info.TypeDict[Any];
            if (w.OrderedTargets)
            {
                foreach (var bt in w.System.Values.Targeting.SubSystems.Systems)
                {
                    if (bt != Any && info.TypeDict[bt].Count > 0)
                    {
                        blockList = info.TypeDict[bt];
                        if (w.System.Values.Targeting.SubSystems.ClosestFirst)
                        {
                            UtilsStatic.GetClosestHitableBlockOfType(blockList, w);
                            if (w.NewTarget.Entity != null) return;
                        }
                    }
                }
            }

            var totalBlocks = blockList.Count;
            var lastBlocks = w.System.Values.Targeting.TopBlocks;
            if (lastBlocks > 0 && totalBlocks < lastBlocks) lastBlocks = totalBlocks;
            int[] deck = null;
            if (lastBlocks > 0) deck = GetDeck(ref w.Deck, ref w.PrevDeckLength, 0, lastBlocks);
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

                physics.CastRay(weaponPos, block.CubeGrid.GridIntegerToWorld(block.Position), out var hitInfo, 15, true);

                if (hitInfo?.HitEntity == null || hitInfo.HitEntity is MyVoxelBase || hitInfo.HitEntity == MyGrid || hitInfo.HitEntity is MyCubeGrid hitGrid && !GridEnemy(w.Comp.MyCube, hitGrid, hitGrid.BigOwners)) continue;

                w.NewTarget.Entity = block;
                return;
            }
        }

        internal static bool ReacquireTarget(Projectile p)
        {
            p.ChaseAge = p.Age;
            if (p.FiringCube == null || p.FiringCube.MarkedForClose || p.FiringCube.CubeGrid.MarkedForClose)
            {
                p.Target = null;
                Log.Line("could not reacquire my weapon is closed");
                return false;
            }

            var totalTargets = p.Ai.SortedTargets.Count;
            var topTargets = p.System.Values.Targeting.TopTargets;
            if (topTargets > 0 && totalTargets < topTargets) topTargets = totalTargets;
            GetTarget(p.Ai, p.Position, p.DistanceToTravelSqr, p.TargetShuffle, p.TargetShuffleLen, topTargets, out var targetInfo);
            if (targetInfo.HasValue)
            {
                p.Target = targetInfo.Value.Target;
                var targetGrid = p.Target as MyCubeGrid;
                if (targetGrid == null)
                {
                    Log.Line($"reacquired a new non-grid target: {p.Target.DebugName}");
                    return true;
                }

                var cubes = targetInfo.Value.TypeDict[Any];

                if (p.System.OrderedTargets)
                {
                    foreach (var bt in p.System.Values.Targeting.SubSystems.Systems)
                    {
                        if (targetInfo.Value.TypeDict[bt].Count > 0)
                        {
                            cubes = targetInfo.Value.TypeDict[bt];
                        }
                    }
                }

                if (cubes == null)
                {
                    Log.Line($"cube list is null");
                    return false;
                }
                if (!targetInfo.Value.IsGrid || cubes.Count <= 0)
                {
                    p.Target = null;
                    Log.Line("reacquired new target was not null and is grid but could not get target blocks");
                    return false;
                }

                var totalBlocks = cubes.Count;
                var firstBlocks = p.System.Values.Targeting.TopBlocks;
                if (firstBlocks > 0 && totalBlocks < firstBlocks) firstBlocks = totalBlocks;

                var gotBlock = GetBlock(out p.Target, cubes, p.BlockSuffle, p.BlockShuffleLen, firstBlocks, p.Position, p.FiringCube, false);
                return gotBlock;
            }
            Log.Line($"GetTarget returned null: sortedTargets:{p.Ai.SortedTargets.Count}");
            return false;
        }

        internal static void GetTarget(GridTargetingAi ai, Vector3D currentPos, double distanceLeftToTravelSqr, int[] targetSuffle, int targetSuffleLen, int randomizeTopTargets, out TargetInfo? targetInfo)
        {
            int[] deck = null;
            if (randomizeTopTargets > 0) deck = GetDeck(ref targetSuffle, ref targetSuffleLen, 0, randomizeTopTargets);
            for (int i = 0; i < ai.SortedTargets.Count; i++)
            {
                int next = i;
                if (i < randomizeTopTargets)
                {
                    if (deck != null) next = deck[i];
                }
                var info = ai.SortedTargets[next];
                if (info.Target == null || info.Target.MarkedForClose || Vector3D.DistanceSquared(info.EntInfo.Position, currentPos) >= distanceLeftToTravelSqr)
                    continue;

                targetInfo = info;
                return;
            }
            targetInfo = null;
        }

        internal static bool GetBlock(out MyEntity target, List<MyCubeBlock> blocks, int[] blockSuffle, int blockSuffleLen, int randomizeFirstBlocks, Vector3D weaponPos, MyCubeBlock weaponBlock, bool checkRay = false)
        {
            var physics = MyAPIGateway.Physics;
            var blockCount = blocks.Count;
            int[] deck = null;
            if (randomizeFirstBlocks > 0) deck = GetDeck(ref blockSuffle, ref blockSuffleLen, 0, randomizeFirstBlocks);

            MyEntity newTarget = null;
            for (int i = 0; i < blockCount; i++)
            {
                int next = i;
                if (i < randomizeFirstBlocks)
                {
                    if (deck != null) next = deck[i];
                }
                var block = blocks[next];
                if (block.MarkedForClose) continue;

                if (checkRay)
                {
                    physics.CastRay(weaponPos, block.PositionComp.GetPosition(), out var hitInfo, 15);

                    if (hitInfo?.HitEntity == null || hitInfo.HitEntity is MyVoxelBase) continue;

                    var isGrid = hitInfo.HitEntity as MyCubeGrid;
                    //var parentIsGrid = hitInfo.HitEntity?.Parent as MyCubeGrid;
                    if (isGrid == weaponBlock.CubeGrid) continue;
                    //if (isGrid != null && !GridEnemy(weaponBlock, isGrid) || parentIsGrid != null && !GridEnemy(weaponBlock, parentIsGrid)) continue;
                }
                newTarget = block;
                break;
            }
            target = newTarget;
            return newTarget != null;
        }
    }
}
