using System.Collections.Concurrent;
using System.Collections.Generic;
using CoreSystems.Platform;
using Sandbox.Game.Entities;
using Sandbox.ModAPI.Ingame;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;

namespace CoreSystems.Support
{
    public partial class Ai
    {

        internal class AiCharger
        {
            internal readonly List<Part> ChargeGroup0 = new List<Part>();
            internal readonly List<Part> ChargeGroup1 = new List<Part>();
            internal readonly List<Part> ChargeGroup2 = new List<Part>();
            internal readonly float[] G0Power = { 1f, 0.75f, 0.5f, 0f };
            internal readonly float[] G1Power = { 1f, 0.25f, 0.25f, 0.5f };
            internal readonly float[] G2Power = { 1f, 0.25f, 0.25f, 0.5f };

            internal float TotalDesired;
            internal float GroupRequested0;
            internal float GroupRequested1;
            internal float GroupRequested2;
            internal int State;
            internal bool Rebalance;

            internal Ai Ai;
            internal AiCharger(Ai ai)
            {
                Ai = ai;
            }

            internal void Add(Part part)
            {
                part.InCharger = true;
                if (ChargeGroup0.Count == 0 && ChargeGroup1.Count == 0 && ChargeGroup2.Count == 0) {
                    Ai.Session.ChargingParts.Add(this);
                    Ai.Session.ChargingParts.ApplyAdditions();
                }

                switch (part.BaseComp.PowerGroupId)
                {
                    case 0:
                        ChargeGroup0.Add(part);
                        GroupRequested0 += part.DesiredPower;

                        break;
                    case 1:
                        ChargeGroup1.Add(part);
                        GroupRequested1 += part.DesiredPower;
                        break;
                    case 2:
                        ChargeGroup2.Add(part);
                        GroupRequested2 += part.DesiredPower;
                        break;
                }

                TotalDesired += part.DesiredPower;
                Rebalance = true;

                if (!MyUtils.IsZero(GroupRequested0) && !MyUtils.IsZero(GroupRequested1) && !MyUtils.IsZero(GroupRequested2))
                    State = 2;
                else if (!MyUtils.IsZero(GroupRequested0) && (!MyUtils.IsZero(GroupRequested1) || !MyUtils.IsZero(GroupRequested2)))
                    State = 1;
                else if (!MyUtils.IsZero(GroupRequested1) && !MyUtils.IsZero(GroupRequested2))
                    State = 3;
                else State = 0;
            }

            internal void Remove(Part part, int i)
            {
                switch (part.BaseComp.PowerGroupId)
                {
                    case 0:
                        ChargeGroup0.RemoveAtFast(i);
                        GroupRequested0 -= part.DesiredPower;

                        break;
                    case 1:
                        ChargeGroup1.RemoveAtFast(i);
                        GroupRequested1 -= part.DesiredPower;

                        break;
                    case 2:
                        ChargeGroup2.RemoveAtFast(i);
                        GroupRequested2 -= part.DesiredPower;
                        break;
                }

                Rebalance = true;

                if (ChargeGroup0.Count == 0 && ChargeGroup1.Count == 0 && ChargeGroup2.Count == 0) {

                    Ai.Session.ChargingParts.Remove(this);
                    GroupRequested0 = 0;
                    GroupRequested1 = 0;
                    GroupRequested2 = 0;
                    Rebalance = false;
                    TotalDesired = 0;
                }

                part.ExitCharger = false;
                part.InCharger = false;
                part.Loading = false;


                if (!MyUtils.IsZero(GroupRequested0) && !MyUtils.IsZero(GroupRequested1) && !MyUtils.IsZero(GroupRequested2))
                    State = 2;
                else if (!MyUtils.IsZero(GroupRequested0) && (!MyUtils.IsZero(GroupRequested1) || !MyUtils.IsZero(GroupRequested2)))
                    State = 1;
                else if (!MyUtils.IsZero(GroupRequested1) && !MyUtils.IsZero(GroupRequested2))
                    State = 3;
                else State = 0;
            }

            internal void Clean()
            {
                ChargeGroup0.Clear();
                ChargeGroup1.Clear();
                ChargeGroup2.Clear();
                GroupRequested0 = 0;
                GroupRequested1 = 0;
                GroupRequested2 = 0;
                Rebalance = false;
                TotalDesired = 0;
            }
        }

        internal struct DbScan
        {
            internal Ai Ai;   
            internal int Version;
        }

        internal class PartCounter
        {
            internal int Current;
            internal int Max;
        }

        public class FakeTarget
        {
            public Vector3D Position;
            public Vector3 LinearVelocity;
            public Vector3 Acceleration;
            public long EntityId;
            public uint LastUpdateTick;

            internal void Update(Vector3D hitPos, Ai ai, MyEntity ent = null, long entId = 0)
            {
                Position = hitPos;
                if (ai.Session.HandlesInput && ent != null) {
                    EntityId = ent.EntityId;
                    LinearVelocity = ent.Physics?.LinearVelocity ?? Vector3.Zero;
                    Acceleration = ent.Physics?.LinearAcceleration ?? Vector3.Zero;
                }
                else if (entId != 0 && MyEntities.TryGetEntityById(entId, out ent))
                {
                    LinearVelocity = ent.Physics?.LinearVelocity ?? Vector3.Zero;
                    Acceleration = ent.Physics?.LinearAcceleration ?? Vector3.Zero;
                    EntityId = entId;
                }

                LastUpdateTick = ai.Session.Tick;
            }
        }

        public class AiDetectionInfo
        {
            internal bool PriorityInRange;
            internal double PriorityRangeSqr;
            internal bool OtherInRange;
            internal double OtherRangeSqr;
            internal bool SomethingInRange;

            internal bool ValidSignalExists(Weapon w)
            {
                var signalInRange = !w.Comp.DetectOtherSignals ? PriorityRangeSqr <= w.MaxTargetDistanceSqr : (OtherRangeSqr <= w.MaxTargetDistanceSqr || PriorityRangeSqr <= w.MaxTargetDistanceSqr);
                return signalInRange || w.Comp.Ai.Construct.Data.Repo.FocusData.HasFocus || w.Comp.Ai.LiveProjectile.Count > 0;
            }

            internal bool ValidSignalExists(SupportSys a)
            {
                var signalInRange = true;
                return signalInRange;
            }

            internal bool ValidSignalExists(Upgrades u)
            {
                var signalInRange = true;
                return signalInRange;
            }

            internal bool ValidSignalExists(Phantom p)
            {
                var signalInRange = true;
                return signalInRange;
            }

            internal void Clean()
            {
                PriorityRangeSqr = double.MaxValue;
                PriorityInRange = false;
                OtherRangeSqr = double.MaxValue;
                OtherInRange = false;
                SomethingInRange = false;
            }
        }


        internal struct DetectInfo
        {
            internal readonly MyEntity Parent;
            internal readonly MyDetectedEntityInfo EntInfo;
            internal readonly int PartCount;
            internal readonly int FatCount;
            internal readonly bool Armed;
            internal readonly bool IsGrid;
            internal readonly bool LargeGrid;

            public DetectInfo(Session session, MyEntity parent, MyDetectedEntityInfo entInfo, int partCount, int fatCount)
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
                        else if (blockTypeMap.TryGetValue(WeaponDefinition.TargetingDef.BlockTypes.Utility, out weaponBlocks) && weaponBlocks.Count > 0)
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
            internal MyDetectedEntityInfo EntInfo;
            internal Vector3D TargetHeading;
            internal Vector3D TargetPos;
            internal Vector3D Velocity;
            internal double DistSqr;
            internal double VelLenSqr;
            internal double TargetRadius;
            internal bool IsGrid;
            internal bool LargeGrid;
            internal bool Approaching;
            internal bool IsStatic;
            internal int PartCount;
            internal int FatCount;
            internal float OffenseRating;
            internal MyEntity Target;
            internal Ai MyAi;
            internal Ai TargetAi;
            internal void Init(ref DetectInfo detectInfo, Ai myAi, Ai targetAi)
            {
                EntInfo = detectInfo.EntInfo;
                Target = detectInfo.Parent;
                PartCount = detectInfo.PartCount;
                FatCount = detectInfo.FatCount;
                IsStatic = Target.Physics.IsStatic;
                IsGrid = detectInfo.IsGrid;
                LargeGrid = detectInfo.LargeGrid;
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
                    if (OffenseRating <= 0 && detectInfo.Armed)
                        OffenseRating = 0.0001f;
                }
                else if (detectInfo.Armed) OffenseRating = 0.0001f;
                else if (Approaching && VelLenSqr >= 1225) OffenseRating = 0.0001f;
                else OffenseRating = 0;
                var myRadius = myAi.TopEntity.PositionComp.LocalVolume.Radius;
                var sphereDistance = MyUtils.GetSmallestDistanceToSphere(ref myCenter, ref targetSphere);
                if (sphereDistance <= myRadius)
                    sphereDistance = 0;
                else sphereDistance -= myRadius;
                DistSqr = sphereDistance * sphereDistance;
            }

            internal void Clean()
            {
                Target = null;
                MyAi = null;
                TargetAi = null;
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
