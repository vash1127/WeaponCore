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
            //RegisterGridEvents(grid);
        }

        internal void SelectTarget(ref MyEntity newTarget, Weapon weapon)
        {
            Stale = true;
            Log.Line($"{weapon.System.WeaponName} - running select target");
            UpdateTarget(weapon, out var targetInfo);
            if (targetInfo.HasValue)
            {
                weapon.LastTargetCheck = 0;
                newTarget = targetInfo.Value.Target;
                var grid = newTarget as MyCubeGrid;
                if (grid == null)
                {
                    Log.Line($"{weapon.System.WeaponName} - targetting nonGrid: {weapon.System.WeaponName}");
                    return;
                }

                if (targetInfo.Value.TypeDict.Count <= 0)
                {
                    Log.Line($"{weapon.System.WeaponName} - sees no valid cubes: {weapon.System.WeaponName}");
                    newTarget = null;
                    return;
                }
                var physics = MyAPIGateway.Physics;
                var weaponPos = weapon.Comp.MyPivotPos;

                var allBlocks = targetInfo.Value.TypeDict[BlockTypes.All];

                if (weapon.OrderedTargets) {
                    foreach(BlockTypes bt in weapon.System.Values.HardPoint.Targeting.priorities) {
                        if (targetInfo.Value.TypeDict[bt].Count > 0) {
                            allBlocks = targetInfo.Value.TypeDict[bt];
                        }
                    }
                }
                var blockCount = allBlocks.Count;
                var deck = GetDeck(ref weapon.Deck, ref weapon.PrevDeckLength,0, blockCount);
                for (int i = 0; i < blockCount; i++)
                {
                    var block = allBlocks[deck[i]];
                    if (block.MarkedForClose) continue;

                    physics.CastRay(weaponPos, block.PositionComp.GetPosition(), out var hitInfo, 15);

                    if (hitInfo?.HitEntity == null || hitInfo.HitEntity is MyVoxelBase) continue;

                    Log.Line($"{weapon.System.WeaponName} - found block");
                    newTarget = block;
                    return;
                }
            }
            weapon.LastTargetCheck = 1;
            newTarget = null;
            Log.Line($"{weapon.System.WeaponName} - no valid target returned, checked: {weapon.Comp.MyAi.SortedTargets.Count} - Total:{weapon.Comp.MyAi.Targeting.TargetRoots.Count}");
        }

        internal void UpdateTarget(Weapon weapon, out TargetInfo? targetInfo)
        {
            var physics = MyAPIGateway.Physics;
            for (int i = 0; i < SortedTargets.Count; i++)
            {
                var info = SortedTargets[i];
                if (info.Target == null || info.Target.MarkedForClose || Vector3D.DistanceSquared(info.EntInfo.Position, weapon.Comp.MyPivotPos) > weapon.System.MaxTrajectorySqr) continue;

                if (weapon.TrackTarget)
                    if (!Weapon.TrackingTarget(weapon, info.Target))
                    {
                        //Log.Line($"{weapon.System.WeaponName} - no trackingTarget - marked:{info.Target.MarkedForClose}");
                        continue;
                    }
                else if (!Weapon.ValidTarget(weapon, info.Target, true))
                    {
                        Log.Line($"{weapon.System.WeaponName} - no valid target - marked:{info.Target.MarkedForClose} - trackingTarget:{weapon.Comp.TrackingWeapon.Target != null} - trackingClosed:{weapon.Comp.TrackingWeapon.Target?.MarkedForClose}");
                        continue;
                    }

                if (info.IsGrid)
                {
                    Log.Line($"{weapon.System.WeaponName} - found grid");
                    targetInfo = info;
                    return;
                }
                var weaponPos = weapon.Comp.MyPivotPos;
                physics.CastRay(weaponPos, info.Target.PositionComp.GetPosition(), out var hitInfo,15, true);
                if (hitInfo?.HitEntity == info.Target)
                {
                    Log.Line($"{weapon.System.WeaponName} - found something");
                    targetInfo = info;
                    return;
                }
            }
            Log.Line($"{weapon.System.WeaponName} - failed to update targets");
            targetInfo = null;
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

            p.Ai.Stale = true;

            var totalTargets = p.Ai.SortedTargets.Count;
            var topTargets = p.System.Values.Ammo.Trajectory.Smarts.TopTargets;
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

                var cubes = targetInfo.Value.TypeDict[BlockTypes.All];

                if (p.System.Values.HardPoint.EnableTargeting)
                {
                    foreach (BlockTypes bt in p.System.Values.HardPoint.Targeting.priorities)
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
                if (p.Ai.Targeting.AllowScanning) Log.Line($"allow scanning was true!");
                if (!targetInfo.Value.IsGrid || cubes.Count <= 0)
                {
                    p.Target = null;
                    Log.Line("reacquired new target was not null and is grid but could not get target blocks");
                    return false;
                }

                var totalBlocks = cubes.Count;
                var firstBlocks = p.System.Values.Ammo.Trajectory.Smarts.TopBlocks;
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
