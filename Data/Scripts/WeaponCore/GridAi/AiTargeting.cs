using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Ingame;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Utils;
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
            RegisterGridEvents(grid);
        }

        internal class TargetCompare : IComparer<TargetInfo>
        {
            public int Compare(TargetInfo x, TargetInfo y)
            {
                var compareParts = x.PartCount.CompareTo(y.PartCount);
                if (compareParts != 0) return -compareParts;
                var xApproching = Vector3.Dot(x.Target.Physics.LinearVelocity, x.Target.PositionComp.GetPosition() - x.MyGrid.PositionComp.GetPosition()) < 0;
                var yApproching = Vector3.Dot(y.Target.Physics.LinearVelocity, y.Target.PositionComp.GetPosition() - y.MyGrid.PositionComp.GetPosition()) < 0;
                return xApproching.CompareTo(yApproching);
            }
        }

        internal void SelectTarget(ref MyEntity target, Weapon weapon)
        {
            var cube = target as MyCubeBlock;
            if (target != null && !target.MarkedForClose && (cube == null || !cube.MarkedForClose)) return;
            if (MySession.Tick - weapon.CheckedForTargetTick < 100) return;

            weapon.CheckedForTargetTick = MySession.Tick;
            TargetInfo? targetInfo;
            UpdateTarget(weapon, out targetInfo);
            if (targetInfo.HasValue)
            {
                target = targetInfo.Value.Target;
                weapon.Comp.Turret.EnableIdleRotation = false;
                var grid = target as MyCubeGrid;
                if (grid == null)
                {
                    Log.Line($"wepaon targetting nonGrid");
                    return;
                }

                if (Targeting.AllowScanning) Log.Line($"allow scanning was true!");
                if (!targetInfo.Value.IsGrid || targetInfo.Value.Cubes.Count <= 0)
                {
                    Log.Line($"weapon sees no valid cubes");
                    target = null;
                    return;
                }
                var found = false;
                var physics = MyAPIGateway.Physics;
                var weaponPos = weapon.Comp.MyPivotPos;
                var blockCount = targetInfo.Value.Cubes.Count;
                var deck = GetDeck(ref weapon.Deck, ref weapon.PrevDeckLength,0, blockCount);
                for (int i = 0; i < blockCount; i++)
                {
                    var block = targetInfo.Value.Cubes[deck[i]];
                    if (block.MarkedForClose) continue;

                    IHitInfo hitInfo;
                    physics.CastRay(weaponPos, block.PositionComp.GetPosition(), out hitInfo, 15);

                    if (hitInfo?.HitEntity == null || hitInfo.HitEntity is MyVoxelBase) continue;

                    var isGrid = hitInfo.HitEntity as MyCubeGrid;
                    var parentIsGrid = hitInfo.HitEntity?.Parent as MyCubeGrid;
                    if (isGrid == weapon.Comp.MyGrid) continue;
                    if (isGrid != null && !GridEnemy(weapon.Comp.MyCube, isGrid) || parentIsGrid != null && !GridEnemy(weapon.Comp.MyCube, parentIsGrid)) continue;

                    target = block;
                    found = true;
                }
                if (!found)
                {
                    target = null;
                    Log.Line("never picked block");
                }
            }
        }

        internal void UpdateTarget(Weapon weapon, out TargetInfo? targetInfo)
        {
            var physics = MyAPIGateway.Physics;
            for (int i = 0; i < SortedTargets.Count; i++)
            {
                var info = SortedTargets[i];
                if (info.Target == null || info.Target.MarkedForClose || Vector3D.DistanceSquared(info.EntInfo.Position, weapon.Comp.MyPivotPos) > weapon.System.MaxTrajectorySqr) continue;
                if (weapon.TrackingAi && !Weapon.TrackingTarget(weapon, info.Target) || !weapon.TrackingAi && !Weapon.ValidTarget(weapon, info.Target, true)) continue;
                if (info.IsGrid)
                {
                    targetInfo = info;
                    return;
                }
                var weaponPos = weapon.Comp.MyPivotPos;
                IHitInfo hitInfo;
                physics.CastRay(weaponPos, info.Target.PositionComp.GetPosition(), out hitInfo,15, true);
                if (hitInfo?.HitEntity == info.Target)
                {
                    targetInfo = info;
                    return;
                }
            }
            targetInfo = null;
        }

        internal void UpdateTargetDb()
        {
            Targeting.AllowScanning = true;
            UpdateTargets();
            Targeting.AllowScanning = false;
            TargetsUpdatedTick = MySession.Tick;
            _myOwner = MyGrid.BigOwners[0];
        }

        private void UpdateTargets()
        {
            ValidGrids.Clear();
            SortedTargets.RemoveAll(x => x.Clean());
            foreach (var ent in Targeting.TargetRoots)
            {
                if (ent == null || ent.MarkedForClose) continue;
                var entInfo = MyDetectedEntityInfoHelper.Create(ent, _myOwner);
                switch (entInfo.Type)
                {
                    case MyDetectedEntityType.Asteroid:
                        continue;
                    case MyDetectedEntityType.Planet:
                        continue;
                    case MyDetectedEntityType.FloatingObject:
                        continue;
                    case MyDetectedEntityType.None:
                        continue;
                    case MyDetectedEntityType.Unknown:
                        continue;
                }
                switch (entInfo.Relationship)
                {
                    case MyRelationsBetweenPlayerAndBlock.Owner:
                        continue;
                    case MyRelationsBetweenPlayerAndBlock.FactionShare:
                        continue;
                    case MyRelationsBetweenPlayerAndBlock.NoOwnership:
                        if (!TargetNoOwners) continue;
                        break;
                    case MyRelationsBetweenPlayerAndBlock.Neutral:
                        if (!TargetNeutrals) continue;
                        break;
                }

                var grid = ent as MyCubeGrid;
                var isGrid = grid != null;
                if (!isGrid)
                {
                    var partCount = 1;
                    var targetInfo = new TargetInfo(entInfo, ent, false, partCount, MyGrid, this);
                    SortedTargets.Add(targetInfo);
                }

                if (isGrid)
                    ValidGrids.Add(ent, entInfo);
            }

            GetTargetBlocks(Targeting, this);
            SortedTargets.Sort(_targetCompare);
        }

        private static void GetTargetBlocks(MyGridTargeting targeting, GridTargetingAi ai)
        {
            IEnumerable<KeyValuePair<MyCubeGrid, List<MyEntity>>> allTargets = targeting.TargetBlocks;
            foreach (var targets in allTargets)
            {
                var rootGrid = targets.Key;
                MyDetectedEntityInfo entInfo;
                if (ai.ValidGrids.TryGetValue(rootGrid, out entInfo))
                {
                    var partCount = rootGrid.GetFatBlocks().Count;
                    var targetInfo = new TargetInfo(entInfo, rootGrid, true, partCount, ai.MyGrid, ai)
                    {
                        Cubes = targets.Value
                    };
                    ai.SortedTargets.Add(targetInfo);
                }
            }
        }

        private static bool GetTargetBlocksOld(MyEntity targetGrid, int numOfBlocks, MyGridTargeting targeting, List<MyEntity> targetBlocks)
        {
            var g = 0;
            var f = 0;
            IEnumerable<KeyValuePair<MyCubeGrid, List<MyEntity>>> allTargets = targeting.TargetBlocks;
            foreach (var targets in allTargets)
            {
                var rootGrid = targets.Key;
                if (rootGrid != targetGrid) continue;
                if (rootGrid.MarkedForClose) return false;
                if (g++ > 0) break;
                foreach (var b in targets.Value)
                {
                    var cube = b as MyCubeBlock;
                    if (cube == null || cube.MarkedForClose) continue;
                    if (f++ > numOfBlocks) return true;
                    targetBlocks.Add(b);
                }
            }
            return f > 0;
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
            var topTargets = p.System.Values.Ammo.Trajectory.Smarts.TopTargets;
            if (topTargets > 0 && totalTargets < topTargets) topTargets = totalTargets;

            TargetInfo? targetInfo;
            GetTarget(p.Ai, p.Position, p.DistanceToTravelSqr, p.TargetShuffle, p.TargetShuffleLen, topTargets, out targetInfo);
            if (targetInfo.HasValue)
            {
                p.Target = targetInfo.Value.Target;
                var targetGrid = p.Target as MyCubeGrid;
                if (targetGrid == null)
                {
                    Log.Line($"reacquired a new non-grid target: {p.Target.DebugName}");
                    return true;
                }
                if (p.Ai.Targeting.AllowScanning) Log.Line($"allow scanning was true!");
                if (!targetInfo.Value.IsGrid || targetInfo.Value.Cubes.Count <= 0)
                {
                    p.Target = null;
                    Log.Line("reacquired new target was not null and is grid but could not get target blocks");
                    return false;
                }

                var totalBlocks = targetInfo.Value.Cubes.Count;
                var firstBlocks = p.System.Values.Ammo.Trajectory.Smarts.TopBlocks;
                if (firstBlocks > 0 && totalBlocks < firstBlocks) firstBlocks = totalBlocks;

                var gotBlock = GetBlock(out p.Target, targetInfo.Value.Cubes, p.BlockSuffle, p.BlockShuffleLen, firstBlocks, p.Position, p.FiringCube, false);
                if (!gotBlock) Log.Line($"couldn't sort a target block");
                return gotBlock;
            }
            Log.Line("GetTarget returned null");
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

        internal static bool GetBlock(out MyEntity target, List<MyEntity> blocks, int[] blockSuffle, int blockSuffleLen, int randomizeFirstBlocks, Vector3D weaponPos, MyCubeBlock weaponBlock, bool checkRay = false)
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
                    IHitInfo hitInfo;
                    physics.CastRay(weaponPos, block.PositionComp.GetPosition(), out hitInfo, 15);

                    if (hitInfo?.HitEntity == null || hitInfo.HitEntity is MyVoxelBase) continue;

                    var isGrid = hitInfo.HitEntity as MyCubeGrid;
                    var parentIsGrid = hitInfo.HitEntity?.Parent as MyCubeGrid;
                    if (isGrid == weaponBlock.CubeGrid) continue;
                    if (isGrid != null && !GridEnemy(weaponBlock, isGrid) || parentIsGrid != null && !GridEnemy(weaponBlock, parentIsGrid)) continue;
                }
                newTarget = block;
                break;
            }
            target = newTarget;
            return newTarget != null;
        }

        private static int[] GetDeck(ref int[] deck , ref int prevDeckLen, int firstCard, int cardsToSort)
        {
            var min = firstCard;
            var max = cardsToSort - 1;
            var count = max - min + 1;
            if (prevDeckLen != count)
            {
                Array.Resize(ref deck, count);
                prevDeckLen = count;
            }

            for (int i = 0; i < count; i++)
            {
                var j = MyUtils.GetRandomInt(0, i + 1);

                deck[i] = deck[j];
                deck[j] = min + i;
            }
            return deck;
        }
    }
}
