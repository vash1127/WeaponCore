using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Ingame;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;
using WeaponCore.Platform;

namespace WeaponCore.Support
{
    public class GridTargetingAi
    {
        internal readonly MyCubeGrid MyGrid;
        internal readonly MyConcurrentDictionary<MyCubeBlock, WeaponComponent> WeaponBase = new MyConcurrentDictionary<MyCubeBlock, WeaponComponent>();
        internal readonly Dictionary<MyEntity, MyDetectedEntityInfo> ValidTargets = new Dictionary<MyEntity, MyDetectedEntityInfo>();
        internal readonly List<TargetInfo> SortedTargets = new List<TargetInfo>();
        internal readonly List<MyEntity> TargetBlocks = new List<MyEntity>();
        internal MyGridTargeting Targeting { get; set; }
        internal bool TargetNeutrals;
        internal bool TargetNoOwners;
        internal Random Rnd;
        internal Session MySession;

        private readonly object _tLock = new object();
        private readonly TargetCompare _targetCompare = new TargetCompare();
        private uint _targetsUpdatedTick;

        internal GridTargetingAi(MyCubeGrid grid, Session mySession)
        {
            MyGrid = grid;
            MySession = mySession;
            Targeting = MyGrid.Components.Get<MyGridTargeting>();
            Rnd = new Random((int)MyGrid.EntityId);
        }

        internal struct TargetInfo
        {
            internal readonly MyDetectedEntityInfo EntInfo;
            internal readonly MyEntity Target;
            internal readonly bool IsGrid;
            internal readonly int PartCount;
            internal readonly MyCubeGrid MyGrid;

            internal TargetInfo(MyDetectedEntityInfo entInfo, MyEntity target, bool isGrid, int partCount, MyCubeGrid myGrid)
            {
                EntInfo = entInfo;
                Target = target;
                IsGrid = isGrid;
                PartCount = partCount;
                MyGrid = myGrid;
            }
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
            }
            if (target != null && !target.MarkedForClose) return;
            if (MySession.Tick - weapon.CheckedForTargetTick < 100) return;

            weapon.CheckedForTargetTick = MySession.Tick;
            lock (_tLock) GetTarget(ref target, weapon);
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
                var bCount = TargetBlocks.Count;
                var found = false;
                var c = 0;
                var physics = MyAPIGateway.Physics;
                var weaponPos = weapon.Comp.MyPivotPos;
                while (!found)
                {
                    if (c++ > 100) break;
                    var next = Rnd.Next(0, bCount);
                    if (!TargetBlocks[next].MarkedForClose)
                    {
                        IHitInfo hitInfo;
                        physics.CastRay(weaponPos, TargetBlocks[next].PositionComp.GetPosition(), out hitInfo, 15);
                        if (hitInfo?.HitEntity == TargetBlocks[next].Parent)
                        {
                            target = TargetBlocks[next];
                            found = true;
                        }
                    }
                }

                if (!found)
                {
                    target = null;
                    Log.Line("while never picked block");
                }
            }
        }

        internal void GetTarget(ref MyEntity target, Weapon weapon)
        {
            var physics = MyAPIGateway.Physics;
            var found = false;
            for (int i = 0; i < SortedTargets.Count; i++)
            {
                var targetInfo = SortedTargets[i];
                if (targetInfo.Target == null || targetInfo.Target.MarkedForClose) continue;
                if (!(Weapon.TrackingTarget(weapon, targetInfo.Target))) continue;
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
            lock (_tLock)
            {
                var myOwner = MyGrid.BigOwners[0];
                ValidTargets.Clear();
                SortedTargets.Clear();
                foreach (var ent in Targeting.TargetRoots)
                {
                    if (ent == null || ent.MarkedForClose) continue;
                    var entInfo = MyDetectedEntityInfoHelper.Create(ent, myOwner);

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
        }
    }
}
