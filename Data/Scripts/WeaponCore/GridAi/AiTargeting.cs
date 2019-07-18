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
            if (MySession.Tick - _targetsUpdatedTick > 100)
            {
                UpdateTargets();
                _targetsUpdatedTick = MySession.Tick;
                _myOwner = MyGrid.BigOwners[0];
            }
            var cube = target as MyCubeBlock;
            if (target != null && !target.MarkedForClose && (cube == null || !cube.MarkedForClose)) return;
            if (MySession.Tick - weapon.CheckedForTargetTick < 100) return;

            weapon.CheckedForTargetTick = MySession.Tick;
            UpdateTarget(ref target, weapon);
            if (target != null)
            {
                weapon.Comp.Turret.EnableIdleRotation = false;
                var grid = target as MyCubeGrid;
                if (grid == null) return;

                if (!GetTargetBlocks(grid, 10, Targeting, TargetBlocks))
                {
                    target = null;
                    return;
                }
                var found = false;
                var physics = MyAPIGateway.Physics;
                var weaponPos = weapon.Comp.MyPivotPos;
                var blockCount = TargetBlocks.Count;
                var deck = GetDeck(0, blockCount);

                for (int i = 0; i < blockCount; i++)
                {
                    var block = TargetBlocks[deck[i]];
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

        internal void UpdateTarget(ref MyEntity target, Weapon weapon)
        {
            var physics = MyAPIGateway.Physics;
            var found = false;
            for (int i = 0; i < SortedTargets.Count; i++)
            {
                var targetInfo = SortedTargets[i];
                if (targetInfo.Target == null || targetInfo.Target.MarkedForClose || Vector3D.DistanceSquared(targetInfo.EntInfo.Position, weapon.Comp.MyPivotPos) > weapon.System.MaxTrajectorySqr) continue;
                if (weapon.TrackingAi && !Weapon.TrackingTarget(weapon, targetInfo.Target) || !weapon.TrackingAi && !Weapon.ValidTarget(weapon, targetInfo.Target, true)) continue;
                if (targetInfo.IsGrid)
                {
                    target = targetInfo.Target;
                    found = true;
                    break;
                }
                var weaponPos = weapon.Comp.MyPivotPos;
                IHitInfo hitInfo;
                physics.CastRay(weaponPos, targetInfo.Target.PositionComp.GetPosition(), out hitInfo,15, true);
                if (hitInfo?.HitEntity == targetInfo.Target)
                {
                    target = targetInfo.Target;
                    found = true;
                    break;
                }
            }
            if (!found) target = null;
        }

        private void UpdateTargets()
        {
            ValidTargets.Clear();
            SortedTargets.Clear();
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
                ValidTargets.Add(ent, entInfo);

                var grid = ent as MyCubeGrid;
                var isGrid = grid != null;
                int partCount = isGrid ? grid.GetFatBlocks().Count : 1;

                SortedTargets.Add(new TargetInfo(entInfo, ent, isGrid, partCount, MyGrid));
            }
            SortedTargets.Sort(_targetCompare);
        }

        internal bool ReacquireTarget(Projectile p)
        {
            if (p.FiringCube == null || p.FiringCube.MarkedForClose || p.FiringCube.CubeGrid.MarkedForClose)
            {
                p.Target = null;
                Log.Line("could not reacquire a target");
                return false;
            }

            GetTarget(ref p.Target, p.Position, p.DistanceToTravelSqr);
            if (p.Target != null)
            {
                var targetGrid = p.Target as MyCubeGrid;
                if (targetGrid == null)
                {
                    Log.Line("could not reacquire a target");
                    return false;
                }
                if (!GetTargetBlocks(targetGrid, 10, Targeting, p.MyEntityList))
                {
                    p.Target = null;
                    Log.Line("could not reacquire a target");
                    return false;
                }
                var gotBlock = GetBlock(out p.Target, p.MyEntityList, p.Position, p.FiringCube, false);
                if (!gotBlock) Log.Line($"couldn't get block");
                return gotBlock;
            }
            Log.Line("could not reacquire a target");
            return false;
        }

        private static bool GetTargetBlocks(MyEntity targetGrid, int numOfBlocks, MyGridTargeting targeting, List<MyEntity> targetBlocks)
        {
            targetBlocks.Clear();
            IEnumerable<KeyValuePair<MyCubeGrid, List<MyEntity>>> allTargets = targeting.TargetBlocks;
            var g = 0;
            var f = 0;
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

        internal void GetTarget(ref MyEntity target, Vector3D currentPos, double distanceLeftToTravelSqr)
        {
            for (int i = 0; i < SortedTargets.Count; i++)
            {
                var targetInfo = SortedTargets[i];
                if (targetInfo.Target == null || targetInfo.Target.MarkedForClose || Vector3D.DistanceSquared(targetInfo.EntInfo.Position, currentPos) >= distanceLeftToTravelSqr)
                {
                    Log.Line($"null, closed or out of distance: {targetInfo.Target == null} - {targetInfo.Target?.MarkedForClose} - {Vector3D.DistanceSquared(targetInfo.EntInfo.Position, currentPos)} - {distanceLeftToTravelSqr}");
                    continue;
                }
                //Log.Line($"got target: grid:{targetInfo.IsGrid}");
                target = targetInfo.Target;
                break;
            }
        }

        internal bool GetBlock(out MyEntity target, List<MyEntity> blocks, Vector3D weaponPos, MyCubeBlock weaponBlock, bool checkRay = false)
        {
            var physics = MyAPIGateway.Physics;
            var blockCount = blocks.Count;
            var deck = GetDeck(0, blockCount);
            for (int i = 0; i < blockCount; i++)
            {
                var block = TargetBlocks[deck[i]];
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

                target = block;
                blocks.Clear();
                return true;
            }
            blocks.Clear();
            target = null;
            return false;
        }

        private int[] _deck = new int[0];
        private int _prevDeckLen;
        private int[] GetDeck(int firstBlock, int blocksToSort)
        {
            var min = firstBlock;
            var max = blocksToSort - 1;
            var count = max - min + 1;
            if (_prevDeckLen != count)
            {
                Array.Resize(ref _deck, count);
                _prevDeckLen = count;
            }

            for (int i = 0; i < count; i++)
            {
                var j = MyUtils.GetRandomInt(0, i + 1);

                _deck[i] = _deck[j];
                _deck[j] = min + i;
            }

            return _deck;
        }

    }
}
