using System.Collections.Concurrent;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Ingame;
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
        internal struct DbScan
        {
            internal GridAi Ai;   
            internal int Version;
        }

        internal class WeaponCount
        {
            internal int Current;
            internal int Max;
        }

        public class FakeTargets
        {
            public FakeTarget ManualTarget = new FakeTarget(FakeTarget.FakeType.Manual);
            public FakeTarget PaintedTarget = new FakeTarget(FakeTarget.FakeType.Painted);
        }

        public class FakeTarget
        {
            
            public FakeTarget(FakeType type)
            {
                Type = type;
            }

            public enum FakeType
            {
                Manual,
                Painted,
            }

            public FakeWorldTargetInfo FakeInfo = new FakeWorldTargetInfo();
            public readonly FakeType Type;
            public Vector3D LocalPosition;
            public long EntityId;
            public uint LastUpdateTick;
            public uint LastInfoTick;
            public bool Dirty;

            internal void Update(Vector3D hitPos, GridAi ai, MyEntity ent = null, long entId = 0)
            {
                if ((ent != null || entId != 0 && MyEntities.TryGetEntityById(entId, out ent)) && ent.Physics != null) {
                    var referenceWorldMatrix = ent.PositionComp.WorldMatrixRef;
                    Vector3D referenceWorldPosition = referenceWorldMatrix.Translation; 
                    Vector3D worldDirection = hitPos - referenceWorldPosition;
                    
                    EntityId = ent.EntityId;
                    LocalPosition = Vector3D.TransformNormal(worldDirection, MatrixD.Transpose(referenceWorldMatrix));
                }
                else
                {
                    if (Type == FakeType.Manual)
                        FakeInfo.WorldPosition = hitPos;

                    EntityId = 0;
                    LocalPosition = Vector3D.Zero;
                    FakeInfo.LinearVelocity = Vector3.Zero;
                    FakeInfo.Acceleration = Vector3.Zero;
                }

                Dirty = false;
                LastInfoTick = 0;
                LastUpdateTick = ai.Session.Tick;
            }

            internal void Sync(FakeTargetPacket packet, GridAi ai)
            {
                if (packet.TargetId == 0) {

                    EntityId = 0;
                    LocalPosition = Vector3D.Zero;
                    FakeInfo.WorldPosition = packet.Pos;
                    FakeInfo.LinearVelocity = Vector3.Zero;
                    FakeInfo.Acceleration = Vector3.Zero;
                }
                else {
                    EntityId = packet.TargetId;
                    LocalPosition = packet.Pos;
                }

                LastInfoTick = 0;
                LastUpdateTick = ai.Session.Tick;
            }

            internal FakeWorldTargetInfo GetFakeTargetInfo(GridAi ai)
            {
                MyEntity ent;
                if (EntityId != 0 && (MyEntities.TryGetEntityById(EntityId, out ent) && ent.Physics != null))
                {
                    if (ai.Session.Tick != LastInfoTick)
                    {
                        LastInfoTick = ai.Session.Tick;
                        if (Type != FakeType.Painted || ai.Targets.ContainsKey(ent))
                        {
                            FakeInfo.WorldPosition = Vector3D.Transform(LocalPosition, ent.PositionComp.WorldMatrixRef);
                            FakeInfo.LinearVelocity = ent.Physics.LinearVelocity;
                            FakeInfo.Acceleration = ent.Physics.LinearAcceleration;
                        }
                        else if (Type == FakeType.Painted)
                            Dirty = true;
                    }
                }
                else if (Type == FakeType.Painted)
                    Dirty = true;

                return FakeInfo;
            }

            public class FakeWorldTargetInfo
            {
                public Vector3D WorldPosition;
                public Vector3 LinearVelocity;
                public Vector3 Acceleration;
            }
        }

        public class AiTargetingInfo
        {
            internal bool ThreatInRange;
            internal double ThreatRangeSqr;
            internal bool OtherInRange;
            internal double OtherRangeSqr;
            internal bool DroneInRange;
            internal double DroneRangeSqr;
            internal bool SomethingInRange;
            internal bool RamProximity;
            internal int DroneCount;

            internal bool ValidTargetExists(Weapon w)
            {
                var comp = w.Comp;
                var ai = comp.Ai;

                var targetInrange = !comp.TargetNonThreats ? ThreatRangeSqr <= w.MaxTargetDistanceSqr : (OtherRangeSqr <= w.MaxTargetDistanceSqr || ThreatRangeSqr <= w.MaxTargetDistanceSqr);

                return targetInrange || ai.Construct.Data.Repo.FocusData.HasFocus || ai.LiveProjectile.Count > 0 && w.System.TrackProjectile && w.Comp.Data.Repo.Base.Set.Overrides.Projectiles;
            }

            internal void DroneAdd(GridAi ai, TargetInfo info)
            {
                var rootConstruct = ai.Construct.RootAi.Construct;
                DroneCount++;

                if (DroneCount > rootConstruct.DroneCount) {
                    rootConstruct.DroneCount = DroneCount;
                    rootConstruct.LastDroneTick = ai.Session.Tick + 1;
                }

                if (info.DistSqr < 9000000) 
                    rootConstruct.DroneAlert = true;
            }

            internal void Clean(GridAi ai)
            {
                ThreatRangeSqr = double.MaxValue;
                ThreatInRange = false;
                OtherRangeSqr = double.MaxValue;
                OtherInRange = false;
                DroneInRange = false;
                DroneRangeSqr = double.MaxValue;
                SomethingInRange = false;
                RamProximity = false;
                DroneCount = 0;

                var rootConstruct = ai.Construct.RootAi.Construct;
                if (rootConstruct.DroneCount != 0 && ai.Session.Tick - rootConstruct.LastDroneTick > 200)
                    rootConstruct.DroneCleanup();
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
            internal readonly bool SuspectedDrone;

            public DetectInfo(Session session, MyEntity parent, MyDetectedEntityInfo entInfo, int partCount, int fatCount, bool suspectedDrone, bool loneWarhead)
            {
                Parent = parent;
                EntInfo = entInfo;
                PartCount = partCount;
                FatCount = fatCount;
                SuspectedDrone = suspectedDrone;
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
                else if (parent is MyMeteor || parent is IMyCharacter || loneWarhead) armed = true;

                Armed = armed;
                IsGrid = isGrid;
                LargeGrid = largeGrid;
            }
        }

        internal class TargetCompare : IComparer<TargetInfo>
        {
            public int Compare(TargetInfo x, TargetInfo y)
            {
                var xDroneThreat = (x.Approaching && x.DistSqr < 9000000 || x.DistSqr < 1000000) && x.Drone;
                var yDroneThreat = (y.Approaching && y.DistSqr < 9000000 || y.DistSqr < 1000000) && y.Drone;
                var droneCompare = xDroneThreat.CompareTo(yDroneThreat);

                if (droneCompare != 0) return -droneCompare;

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
            internal bool Drone;
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
                IsStatic = Target.Physics.IsStatic;
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

                if (!MyUtils.IsZero(Velocity, 1E-01F))
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
                var myRadius = myAi.MyGrid.PositionComp.LocalVolume.Radius;
                var sphereDistance = MyUtils.GetSmallestDistanceToSphere(ref myCenter, ref targetSphere);
                if (sphereDistance <= myRadius)
                    sphereDistance = 0;
                else sphereDistance -= myRadius;
                DistSqr = sphereDistance * sphereDistance;

                Drone = (VelLenSqr > 100 || Approaching && DistSqr < 90000) && detectInfo.SuspectedDrone;
                
                if (Drone && OffenseRating < 10) 
                    OffenseRating = 10;
            }

            internal void Clean()
            {
                Target = null;
                MyGrid = null;
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
