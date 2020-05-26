using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using ProtoBuf;
using Sandbox.Game.Entities;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;
using WeaponCore.Platform;

namespace WeaponCore.Support
{
    public partial class GridAi
    {
        internal class WeaponCount
        {
            internal int Current;
            internal int Max;
        }

        [ProtoContract]
        public class FakeTarget
        {
            [ProtoMember(1)] public Vector3D Position;
            [ProtoMember(2)] public Vector3 LinearVelocity;
            [ProtoMember(3)] public Vector3 Acceleration;
            [ProtoMember(4)] public bool ClearTarget;

            internal void Update(Vector3D hitPos, GridAi ai, MyEntity ent = null, bool networkUpdate = false)
            {
                Position = hitPos;
                if (ent != null)
                {
                    LinearVelocity = ent.Physics?.LinearVelocity ?? Vector3.Zero;
                    Acceleration = ent.Physics?.LinearAcceleration ?? Vector3.Zero;
                }

                if (ai.Session.MpActive && !networkUpdate)
                    ai.Session.SendFakeTargetUpdate(ai, hitPos);

                ClearTarget = false;
            }

            internal FakeTarget() { }
        }

        internal class AiTargetingInfo
        {
            internal bool ThreatInRange;
            internal double ThreatRangeSqr;
            internal bool OtherInRange;
            internal double OthertRangeSqr;

            internal bool ValidTargetExists(Weapon w)
            {
                var comp = w.Comp;
                var ai = comp.Ai;
                var overRides = comp.Set.Value.Overrides;
                var overActive = overRides.Activate;
                var attackNeutrals = overActive && overRides.Neutrals;
                var attackNoOwner = overActive && overRides.Unowned;
                var attackFriends = overActive && overRides.Friendly;
                w.TargetNonThreats = (attackNoOwner || attackNeutrals || attackFriends);

                var targetInrange = w.TargetNonThreats ? OthertRangeSqr <= w.MaxTargetDistanceSqr && OthertRangeSqr >= w.MinTargetDistanceSqr 
                    : ThreatRangeSqr <= w.MaxTargetDistanceSqr && ThreatRangeSqr >= w.MinTargetDistanceSqr;

                return targetInrange || ai.Focus.HasFocus || ai.LiveProjectile.Count > 0;
            }

            internal void Clean()
            {
                ThreatRangeSqr = double.MaxValue;
                ThreatInRange = false;
                OthertRangeSqr = double.MaxValue;
                OtherInRange = false;
            }
        }

        internal class Constructs
        {
            internal readonly Dictionary<MyStringHash, int> Counter = new Dictionary<MyStringHash, int>(MyStringHash.Comparer);
            internal float OptimalDps;
            internal int BlockCount;
            internal GridAi RootAi;
            internal enum UpdateType
            {
                BlockScan,
                Overrides,
                ManualShootingOff,
                None,
            }

            internal void UpdateConstruct(UpdateType type)
            {
                foreach (var sub in RootAi.SubGrids) {

                    GridAi ai;
                    if (RootAi.Session.GridTargetingAIs.TryGetValue(sub, out ai)) {

                        switch (type) {
                            case UpdateType.BlockScan: {
                                ai.ReScanBlockGroups();
                                break;
                            }
                            case UpdateType.Overrides: {
                                ai.UpdateGroupOverRides();
                                break;
                            }
                            case UpdateType.ManualShootingOff:
                            {
                                ai.TurnManualShootOff();
                                break;
                            }
                        }
                    }
                }
            }

            internal void Update(GridAi ai)
            {
                FatMap fatMap;
                if (ai?.MyGrid != null && ai.Session.GridToFatMap.TryGetValue(ai.MyGrid, out fatMap))
                {
                    BlockCount = fatMap.MostBlocks;
                    OptimalDps = ai.OptimalDps;
                    GridAi tmpAi = null;
                    foreach (var grid in ai.SubGrids)
                    {
                        GridAi checkAi;
                        if (ai.Session.GridTargetingAIs.TryGetValue(grid, out checkAi) && (tmpAi == null || tmpAi.MyGrid.EntityId > grid.EntityId)) tmpAi = checkAi;

                        if (grid == ai.MyGrid) continue;
                        if (ai.Session.GridToFatMap.TryGetValue(grid, out fatMap))
                        {
                            BlockCount += ai.Session.GridToFatMap[grid].MostBlocks;
                            OptimalDps += ai.OptimalDps;
                        }
                    }
                    RootAi = tmpAi;
                    UpdateWeaponCounters(ai);
                    return;
                }

                OptimalDps = 0;
                BlockCount = 0;
                RootAi = null;
            }

            internal void UpdateWeaponCounters(GridAi ai)
            {
                Counter.Clear();
                foreach (var grid in ai.SubGrids)
                {
                    GridAi checkAi;
                    if (ai.Session.GridTargetingAIs.TryGetValue(grid, out checkAi))
                    {
                        foreach (var wc in checkAi.WeaponCounter)
                        {
                            if (Counter.ContainsKey(wc.Key))
                                Counter[wc.Key] += wc.Value.Current;
                            else Counter.Add(wc.Key, wc.Value.Current);
                        }
                    }
                }
            }

            internal void AddWeaponCount(MyStringHash weaponHash)
            {
                if (Counter.ContainsKey(weaponHash))
                    Counter[weaponHash]++;
                else Counter[weaponHash] = 1;
            }

            internal void RemoveWeaponCount(MyStringHash weaponHash)
            {
                if (Counter.ContainsKey(weaponHash))
                    Counter[weaponHash]--;
                else Counter[weaponHash] = 0;
            }

            internal int GetWeaponCount(MyStringHash weaponHash)
            {
                int value;
                Counter.TryGetValue(weaponHash, out value);
                return value;
            }

            internal void Clean()
            {
                OptimalDps = 0;
                BlockCount = 0;
                RootAi = null;
                Counter.Clear();
            }
        }

        internal struct DetectInfo
        {
            internal readonly MyEntity Parent;
            internal readonly Sandbox.ModAPI.Ingame.MyDetectedEntityInfo EntInfo;
            internal readonly int PartCount;
            internal readonly int FatCount;
            internal readonly bool Armed;
            internal readonly bool IsGrid;
            internal readonly bool LargeGrid;

            public DetectInfo(Session session, MyEntity parent, Sandbox.ModAPI.Ingame.MyDetectedEntityInfo entInfo, int partCount, int fatCount)
            {
                Parent = parent;
                EntInfo = entInfo;
                PartCount = partCount;
                FatCount = fatCount;
                var armed = false;
                var isGrid = false;
                var largeGrid = false;
                var grid = parent as MyCubeGrid;
                if (grid != null)
                {
                    isGrid = true;
                    largeGrid = grid.GridSizeEnum == MyCubeSize.Large;
                    ConcurrentDictionary<WeaponDefinition.TargetingDef.BlockTypes, ConcurrentCachingList<MyCubeBlock>> blockTypeMap;
                    if (session.GridToBlockTypeMap.TryGetValue((MyCubeGrid)Parent, out blockTypeMap))
                    {
                        ConcurrentCachingList<MyCubeBlock> weaponBlocks;
                        if (blockTypeMap.TryGetValue(WeaponDefinition.TargetingDef.BlockTypes.Offense, out weaponBlocks) && weaponBlocks.Count > 0)
                            armed = true;
                    }
                }
                else if (parent is MyMeteor || parent is IMyCharacter) armed = true;

                Armed = armed;
                IsGrid = isGrid;
                LargeGrid = largeGrid;
            }
        }

        internal class TargetCompare : IComparer<TargetInfo>
        {
            public int Compare(TargetInfo x, TargetInfo y)
            {
                var gridCompare = (x.Target is MyCubeGrid).CompareTo(y.Target is MyCubeGrid);
                if (gridCompare != 0) return -gridCompare;
                var xCollision = x.Approaching && x.DistSqr < 90000 && x.VelLenSqr > 100;
                var yCollision = y.Approaching && y.DistSqr < 90000 && y.VelLenSqr > 100;
                var collisionRisk = xCollision.CompareTo(yCollision);
                if (collisionRisk != 0) return collisionRisk;

                var xIsImminentThreat = x.Approaching && x.DistSqr < 640000 && x.OffenseRating > 0;
                var yIsImminentThreat = y.Approaching && y.DistSqr < 640000 && y.OffenseRating > 0;
                var imminentThreat = -xIsImminentThreat.CompareTo(yIsImminentThreat);
                if (imminentThreat != 0) return imminentThreat;

                var compareOffense = x.OffenseRating.CompareTo(y.OffenseRating);
                return -compareOffense;
            }
        }

        internal class TargetInfo
        {
            internal Sandbox.ModAPI.Ingame.MyDetectedEntityInfo EntInfo;
            internal Vector3D TargetDir;
            internal Vector3D TargetHeading;
            internal Vector3D TargetPos;
            internal Vector3D Velocity;
            internal double DistSqr;
            internal double VelLenSqr;
            internal double TargetRadius;
            internal bool IsGrid;
            internal bool LargeGrid;
            internal bool Approaching;
            internal int PartCount;
            internal int FatCount;
            internal float OffenseRating;
            internal MyEntity Target;
            internal MyCubeGrid MyGrid;
            internal GridAi MyAi;
            internal GridAi TargetAi;

            internal void Init(ref DetectInfo detectInfo, MyCubeGrid myGrid, GridAi myAi, GridAi targetAi)
            {
                EntInfo = detectInfo.EntInfo;
                Target = detectInfo.Parent;
                PartCount = detectInfo.PartCount;
                FatCount = detectInfo.FatCount;
                IsGrid = detectInfo.IsGrid;
                LargeGrid = detectInfo.LargeGrid;
                MyGrid = myGrid;
                MyAi = myAi;
                TargetAi = targetAi;
                Velocity = Target.Physics.LinearVelocity;
                VelLenSqr = Velocity.LengthSquared();
                var targetSphere = Target.PositionComp.WorldVolume;
                TargetPos = targetSphere.Center;
                TargetRadius = targetSphere.Radius;

                var myCenter = myAi.GridVolume.Center;

                if (!MyUtils.IsZero(Velocity, 1E-02F))
                {
                    var targetMag = myCenter - TargetPos;
                    Approaching = MathFuncs.IsDotProductWithinTolerance(ref Velocity, ref targetMag, myAi.Session.ApproachDegrees);
                }
                else
                {
                    Approaching = false;
                    TargetHeading = Vector3D.Zero;
                }

                if (targetAi != null)
                {
                    OffenseRating = targetAi.Construct.OptimalDps / myAi.Construct.OptimalDps;
                }
                else if (detectInfo.Armed) OffenseRating = 0.0001f;
                else OffenseRating = 0;

                var myRadius = myAi.MyGrid.PositionComp.LocalVolume.Radius;
                var sphereDistance = MyUtils.GetSmallestDistanceToSphere(ref myCenter, ref targetSphere);
                if (sphereDistance <= myRadius)
                    sphereDistance = 0;
                else sphereDistance -= myRadius;

                DistSqr = sphereDistance * sphereDistance;
            }
        }

        internal struct Shields
        {
            internal long Id;
            internal MyEntity ShieldEnt;
            internal MyCubeBlock ShieldBlock;
        }
    }
}
