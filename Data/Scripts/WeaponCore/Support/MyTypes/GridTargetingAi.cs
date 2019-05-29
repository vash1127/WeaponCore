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
        internal bool WeaponReady = true;
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
            internal readonly MyCubeGrid MyGrid;

            internal TargetInfo(MyDetectedEntityInfo entInfo, MyEntity target, MyCubeGrid myGrid)
            {
                EntInfo = entInfo;
                Target = target;
                MyGrid = myGrid;
            }
        }

        internal class TargetCompare : IComparer<TargetInfo>
        {
            public int Compare(TargetInfo x, TargetInfo y)
            {
                var xApproching = Vector3.Dot(x.Target.Physics.LinearVelocity, x.Target.PositionComp.GetPosition() - x.MyGrid.PositionComp.GetPosition()) < 0;
                var yApproching = Vector3.Dot(y.Target.Physics.LinearVelocity, y.Target.PositionComp.GetPosition() - y.MyGrid.PositionComp.GetPosition()) < 0;
                var compareApproch = xApproching.CompareTo(yApproching);
                if (compareApproch != 0) return -compareApproch;
                return x.Target.EntityId.CompareTo(y.Target.EntityId);
            }
        }

        internal void SelectTarget(ref MyEntity target, Vector3D weaponPos)
        {
            if (MySession.Tick - _targetsUpdatedTick >= 100)
            {
                UpdateTargets();
                _targetsUpdatedTick = MySession.Tick;
            }
            else if (target != null) return;
             
            WeaponReady = false;
            lock (_tLock) GetTarget(ref target, weaponPos);

            if (target != null)
            {
                WeaponReady = true;
                var grid = target as MyCubeGrid;
                if (grid == null) return;

                GetTargetBlocks(grid);
                var bCount = TargetBlocks.Count;
                var found = false;
                var c = 0;
                while (!found)
                {
                    if (c++ > 100) break;
                    var next = Rnd.Next(0, bCount);
                    if (!TargetBlocks[next].MarkedForClose)
                    {
                        target = TargetBlocks[next];
                        found = true;
                    }
                }
            }
        }

        internal void GetTarget(ref MyEntity target, Vector3D weaponPos)
        {
            var physics = MyAPIGateway.Physics;
            for (int i = 0; i < SortedTargets.Count; i++)
            {
                var targetInfo = SortedTargets[i];
                IHitInfo hitInfo;
                physics.CastRay(weaponPos, targetInfo.Target.PositionComp.GetPosition(), out hitInfo,0);
                if (hitInfo.HitEntity == targetInfo.Target)
                {
                    target = targetInfo.Target;
                }
                else target = null;
            }

            /*
            foreach (var ent in Targeting.TargetRoots)
            {
                if (ent == null || ent.MarkedForClose || target == ent || target?.Parent == ent) continue;
                var entInfo = MyDetectedEntityInfoHelper.Create(ent, MyGrid.BigOwners[0]);
                if (entInfo.IsEmpty() || (entInfo.Relationship == MyRelationsBetweenPlayerAndBlock.Owner)) continue;
                if (entInfo.Type == MyDetectedEntityType.SmallGrid || entInfo.Type == MyDetectedEntityType.LargeGrid)
                {
                    if (!GetTargetBlocks(ent)) continue;
                    target = ent;
                    return;
                }

                target = ent;
                return;
            }
            target = null;
            */
        }

        private bool GetTargetBlocks(MyEntity targetGrid)
        {
            Log.Line($"getting grid block cache");
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
                    SortedTargets.Add(new TargetInfo(entInfo, ent, MyGrid));
                }
                SortedTargets.Sort(_targetCompare);
            }
        }
    }
}
