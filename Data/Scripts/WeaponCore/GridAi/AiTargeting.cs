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
            if (target != null && !target.MarkedForClose) return;
            if (MySession.Tick - weapon.CheckedForTargetTick < 100) return;

            weapon.CheckedForTargetTick = MySession.Tick;
            UpdateTarget(ref target, weapon);
            if (target != null)
            {
                weapon.Comp.Turret.EnableIdleRotation = false;
                var grid = target as MyCubeGrid;
                if (grid == null) return;

                if (!GetTargetBlocks(grid))
                {
                    target = null;
                    return;
                }
                var found = false;
                var physics = MyAPIGateway.Physics;
                var weaponPos = weapon.Comp.MyPivotPos;
                var blockCount = TargetBlocks.Count;
                var deck = GetDeck(blockCount);

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

        private bool GetTargetBlocks(MyEntity targetGrid)
        {
            TargetBlocks.Clear();
            IEnumerable<KeyValuePair<MyCubeGrid, List<MyEntity>>> allTargets = Targeting.TargetBlocks;
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
                    if (b == null) continue;
                    if (f++ > 9) return true;
                    TargetBlocks.Add(b);
                }
            }
            return f > 0;
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

        private int[] _deck = new int[0];
        private int _prevDeckLen;
        private int[] GetDeck(int targetCount)
        {
            var min = 0;
            var max = targetCount - 1;
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

        internal bool ReacquireTarget(ref MyEntity target, MyCubeBlock myParent, Vector3D currentPos, double distanceLeftToTravel)
        {
            if (myParent == null || myParent.MarkedForClose || myParent.CubeGrid.MarkedForClose)
            {
                target = null;
                return false;
            }

            GetTarget(ref target, currentPos, distanceLeftToTravel);
            if (target != null)
            {
                var targetGrid = target as MyCubeGrid;
                if (targetGrid == null)
                {
                    target = null;
                    return false;
                }
                MyEntity entity = null;

                try
                {
                    IEnumerable<KeyValuePair<MyCubeGrid, List<MyEntity>>> allTargets = Targeting.TargetBlocks;
                    foreach (var targets in allTargets)
                    {
                        var rootGrid = targets.Key;
                        if (rootGrid != targetGrid || targetGrid == myParent.CubeGrid || !GridEnemy(myParent, targetGrid)) continue;
                        if (rootGrid.MarkedForClose) return false;

                        foreach (var e in targets.Value)
                        {
                            if (e == null || e.MarkedForClose) continue;
                            entity = e;
                            break;
                        }
                    }
                }
                catch (Exception ex) { Log.Line($"Exception in Targeting.TargetBlocks: {ex}"); }

                var block = entity as MyCubeBlock;
                if (block == null) return false;

                target = block;
                //Log.Line("reaquired");
                return true;
            }
            Log.Line("could not reacquire a target");
            target = null;
            return false;
        }

        internal void GetTarget(ref MyEntity target, Vector3D currentPos, double distanceLeftToTravel)
        {
            for (int i = 0; i < SortedTargets.Count; i++)
            {
                var targetInfo = SortedTargets[i];
                if (targetInfo.Target == null || targetInfo.Target.MarkedForClose ||
                    Vector3D.DistanceSquared(targetInfo.EntInfo.Position, currentPos) >= distanceLeftToTravel)
                {
                    Log.Line($"null, closed or out of distance: {targetInfo.Target == null} - {targetInfo.Target?.MarkedForClose} - {Vector3D.DistanceSquared(targetInfo.EntInfo.Position, currentPos)} - {distanceLeftToTravel}");
                    continue;
                }
                //Log.Line($"got target: grid:{targetInfo.IsGrid}");
                target = targetInfo.Target;
                break;
            }
        }
    }
}
