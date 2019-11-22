using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;
using static WeaponCore.Support.TargetingDefinition;
namespace WeaponCore.Support
{
    public partial class GridAi
    {
        internal void RequestDbUpdate()
        {
            if (!UpdateOwner() || Interlocked.CompareExchange(ref DbUpdating, 1, 1) == 1) return;
            GridCenter = MyGrid.PositionComp.WorldAABB.Center;
            GridRadius = MyGrid.PositionComp.LocalVolume.Radius;
            Session.DbsToUpdate.Add(this);
            TargetsUpdatedTick = Session.Tick;
        }

        private bool UpdateOwner()
        {
            if (MyGrid == null || !MyGrid.InScene || MyGrid.MarkedForClose)
                return false;
            var bigOwners = MyGrid.BigOwners;
            if (bigOwners == null || bigOwners.Count <= 0)
            {
                MyOwner = 0;
                return false;
            }
            MyOwner = bigOwners[0];
            return true;
        }

        internal void MyPlanetInfo(bool clear = false)
        {
            if (!clear)
            {
                MyPlanet = MyPlanetTmp;
                var gridRadius = MyGrid.PositionComp.LocalVolume.Radius;
                var planetCenter = MyPlanet.PositionComp.WorldAABB.Center;
                if (new BoundingSphereD(planetCenter, MyPlanet.MaximumRadius + gridRadius).Intersects(MyGrid.PositionComp.WorldVolume))
                {
                    var gridCenter = MyGrid.PositionComp.WorldAABB.Center;
                    PlanetClosestPoint = MyPlanet.GetClosestSurfacePointGlobal(gridCenter);
                    double pointDistSqr;
                    Vector3D.DistanceSquared(ref PlanetClosestPoint, ref gridCenter, out pointDistSqr);
                    pointDistSqr -= (gridRadius * gridRadius);
                    PlanetSurfaceInRange = pointDistSqr <= MaxTargetingRangeSqr;
                }
                else
                {
                    PlanetClosestPoint = Vector3D.Zero;
                    PlanetSurfaceInRange = false;
                }
            }
            else
            {
                MyPlanet = null;
                PlanetClosestPoint = Vector3D.Zero;
                PlanetSurfaceInRange = false;
            }
        }

        public static bool GridEnemy(MyCubeBlock myCube, MyCubeGrid grid, List<long> owners = null)
        {
            if (owners == null) owners = grid.BigOwners;
            if (owners.Count == 0) return true;
            var relationship = myCube.GetUserRelationToOwner(owners[0]);
            var enemy = relationship != MyRelationsBetweenPlayerAndBlock.Owner && relationship != MyRelationsBetweenPlayerAndBlock.FactionShare;
            return enemy;
        }

        private static int[] GetDeck(ref int[] deck, ref int prevDeckLen, int firstCard, int cardsToSort)
        {
            var count = cardsToSort - firstCard;
            if (prevDeckLen != count)
            {
                Array.Resize(ref deck, count);
                prevDeckLen = count;
            }

            for (int i = 0; i < count; i++)
            {
                var j = MyUtils.GetRandomInt(0, i + 1);

                deck[i] = deck[j];
                deck[j] = firstCard + i;
            }
            return deck;
        }

        internal bool CreateEntInfo(MyEntity entity, long gridOwner, out Sandbox.ModAPI.Ingame.MyDetectedEntityInfo entInfo)
        {
            if (entity == null)
            {
                entInfo = new Sandbox.ModAPI.Ingame.MyDetectedEntityInfo();
                return false;
            }

            var topMostParent = entity.GetTopMostParent() as MyCubeGrid;
            if (topMostParent != null)
            {
                var type = topMostParent.GridSizeEnum != MyCubeSize.Small ? Sandbox.ModAPI.Ingame.MyDetectedEntityType.LargeGrid : Sandbox.ModAPI.Ingame.MyDetectedEntityType.SmallGrid;
                #if VERSION_191
                var relationship = topMostParent.BigOwners.Count != 0 ? MyIDModule.GetRelation(gridOwner, topMostParent.BigOwners[0], MyOwnershipShareModeEnum.Faction) : MyRelationsBetweenPlayerAndBlock.NoOwnership;
                #else
                var relationship = topMostParent.BigOwners.Count != 0 ? MyIDModule.GetRelationPlayerBlock(gridOwner, topMostParent.BigOwners[0], MyOwnershipShareModeEnum.Faction) : MyRelationsBetweenPlayerAndBlock.NoOwnership;
                #endif
                entInfo = new Sandbox.ModAPI.Ingame.MyDetectedEntityInfo(topMostParent.EntityId, string.Empty, type, null, MatrixD.Zero, Vector3.Zero, relationship, new BoundingBoxD(), Session.Tick);
                return true;
            }

            var myCharacter = entity as IMyCharacter;
            if (myCharacter != null)
            {
                var controllingId = myCharacter.ControllerInfo?.ControllingIdentityId;
                var playerId = controllingId ?? 0;

                var type = !myCharacter.IsPlayer ? Sandbox.ModAPI.Ingame.MyDetectedEntityType.CharacterOther : Sandbox.ModAPI.Ingame.MyDetectedEntityType.CharacterHuman;
                #if VERSION_191
                var relationPlayerBlock = MyIDModule.GetRelation(gridOwner, playerId, MyOwnershipShareModeEnum.Faction);
                #else
                var relationPlayerBlock = MyIDModule.GetRelationPlayerBlock(gridOwner, playerId, MyOwnershipShareModeEnum.Faction);
                #endif

                entInfo = new Sandbox.ModAPI.Ingame.MyDetectedEntityInfo(entity.EntityId, string.Empty, type, null, MatrixD.Zero, Vector3.Zero, relationPlayerBlock, new BoundingBoxD(), Session.Tick);
                return true;
            }
            const MyRelationsBetweenPlayerAndBlock relationship1 = MyRelationsBetweenPlayerAndBlock.Neutral;
            var myPlanet = entity as MyPlanet;
            if (myPlanet != null)
            {
                const Sandbox.ModAPI.Ingame.MyDetectedEntityType type = Sandbox.ModAPI.Ingame.MyDetectedEntityType.Planet;
                entInfo = new Sandbox.ModAPI.Ingame.MyDetectedEntityInfo(entity.EntityId, string.Empty, type, null, MatrixD.Zero, Vector3.Zero, relationship1, new BoundingBoxD(), Session.Tick);
                return true;
            }
            if (entity is MyVoxelMap)
            {
                const Sandbox.ModAPI.Ingame.MyDetectedEntityType type = Sandbox.ModAPI.Ingame.MyDetectedEntityType.Asteroid;
                entInfo = new Sandbox.ModAPI.Ingame.MyDetectedEntityInfo(entity.EntityId, string.Empty, type, null, MatrixD.Zero, Vector3.Zero, relationship1, new BoundingBoxD(), Session.Tick);
                return true;
            }
            if (entity is MyMeteor)
            {
                const Sandbox.ModAPI.Ingame.MyDetectedEntityType type = Sandbox.ModAPI.Ingame.MyDetectedEntityType.Meteor;
                entInfo = new Sandbox.ModAPI.Ingame.MyDetectedEntityInfo(entity.EntityId, string.Empty, type, null, MatrixD.Zero, Vector3.Zero, relationship1, new BoundingBoxD(), Session.Tick);
                return true;
            }
            entInfo = new Sandbox.ModAPI.Ingame.MyDetectedEntityInfo();
            return false;
        }

        internal struct DetectInfo
        {
            internal readonly MyEntity Parent;
            internal readonly Sandbox.ModAPI.Ingame.MyDetectedEntityInfo EntInfo;
            internal readonly int PartCount;
            internal readonly bool Armed;
            internal readonly bool IsGrid;
            internal readonly bool LargeGrid;

            public DetectInfo(Session session, MyEntity parent, Sandbox.ModAPI.Ingame.MyDetectedEntityInfo entInfo, int partCount)
            {
                Parent = parent;
                EntInfo = entInfo;
                PartCount = partCount;
                var armed = false;
                var isGrid = false;
                var largeGrid = false;
                var grid = parent as MyCubeGrid;
                if (grid != null)
                {
                    isGrid = true;
                    largeGrid = grid.GridSizeEnum == MyCubeSize.Large;
                    ConcurrentDictionary<BlockTypes, MyConcurrentList<MyCubeBlock>> blockTypeMap;
                    if (session.GridToBlockTypeMap.TryGetValue((MyCubeGrid)Parent, out blockTypeMap))
                    {
                        MyConcurrentList<MyCubeBlock> weaponBlocks;
                        if (blockTypeMap.TryGetValue(BlockTypes.Offense, out weaponBlocks) && weaponBlocks.Count > 0)
                            armed = true;
                    }
                }
                Armed = armed;
                IsGrid = isGrid;
                LargeGrid = largeGrid;
            }
        }

        internal class TargetCompare : IComparer<TargetInfo>
        {
            public int Compare(TargetInfo x, TargetInfo y)
            {
                var xCollision = x.Approaching && x.CollisionDistSqr < 90000 && x.VelLenSqr > 100;
                var yCollision = y.Approaching && y.CollisionDistSqr < 90000 && y.VelLenSqr > 100;
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

        internal class WeaponCount
        {
            internal int Current;
            internal int Max;
        }

        internal class TargetInfo
        {
            internal Sandbox.ModAPI.Ingame.MyDetectedEntityInfo EntInfo;
            internal MyEntity Target;
            internal Vector3D TargetDir;
            internal Vector3D TargetPos;
            internal Vector3 Velocity;
            internal double DistSqr;
            internal double CollisionDistSqr;
            internal float VelLenSqr;
            internal bool IsGrid;
            internal bool LargeGrid;
            internal bool Approaching;
            internal int PartCount;
            internal float OffenseRating;
            internal MyCubeGrid MyGrid;
            internal GridAi MyAi;
            internal GridAi TargetAi;

            internal void Init(ref DetectInfo detectInfo, MyCubeGrid myGrid, GridAi myAi, GridAi targetAi)
            {
                EntInfo = detectInfo.EntInfo;
                Target = detectInfo.Parent;
                PartCount = detectInfo.PartCount;
                IsGrid = detectInfo.IsGrid;
                LargeGrid = detectInfo.LargeGrid;
                MyGrid = myGrid;
                MyAi = myAi;
                TargetAi = targetAi;
                Velocity = Target.Physics.LinearVelocity;
                VelLenSqr = Velocity.LengthSquared();
                TargetPos = Target.PositionComp.WorldAABB.Center;
                if (!MyUtils.IsZero(Velocity, 1E-02F))
                {
                    TargetDir = Vector3D.Normalize(Velocity);
                    var refDir = Vector3D.Normalize(myAi.GridCenter - TargetPos);
                    Approaching = MathFuncs.IsDotProductWithinTolerance(ref TargetDir, ref refDir, myAi.Session.ApproachDegrees);
                }
                else
                {
                    TargetDir = Vector3D.Zero;
                    Approaching = false;
                }

                if (targetAi != null)
                {
                    OffenseRating = targetAi.OptimalDps / myAi.OptimalDps;
                }
                else if (detectInfo.Armed) OffenseRating = 0.01f;
                else OffenseRating = 0;
                Vector3D.DistanceSquared(ref TargetPos, ref myAi.GridCenter, out DistSqr);
                var adjustedDist = DistSqr - (MyAi.GridRadius * MyAi.GridRadius);
                CollisionDistSqr = adjustedDist > 0 ? adjustedDist : 0;
            }
        }

        internal bool GetTargetState()
        {
            var validFocus = false;
            for (int i = 0; i < Focus.Target.Length; i++)
            {
                var target = Focus.Target[i];
                TargetInfo info;
                if (target == null || !Targets.TryGetValue(target, out info)) continue;
                validFocus = true;
                if (!Session.Tick20 && Focus.PrevTargetId[i] == info.EntInfo.EntityId) continue;
                Focus.PrevTargetId[i] = info.EntInfo.EntityId;
                var targetVel = target.Physics?.LinearVelocity ?? Vector3.Zero;
                if (MyUtils.IsZero(targetVel, 1E-02F)) targetVel = Vector3.Zero;
                var targetDir = Vector3D.Normalize(targetVel);
                var targetRevDir = -targetDir;
                var targetPos = target.PositionComp.WorldAABB.Center;
                var myPos = MyGrid.PositionComp.WorldAABB.Center;
                var myHeading = Vector3D.Normalize(myPos - targetPos);

                if (info.LargeGrid && info.PartCount > 24000) Focus.TargetState[i].Size = 6;
                else if (info.LargeGrid && info.PartCount > 12000) Focus.TargetState[i].Size = 5;
                else if (info.LargeGrid && info.PartCount > 6000) Focus.TargetState[i].Size = 4;
                else if (info.LargeGrid && info.PartCount > 3000) Focus.TargetState[i].Size = 3;
                else if (info.LargeGrid) Focus.TargetState[i].Size = 2;
                else if (info.PartCount > 2000) Focus.TargetState[i].Size = 1;
                else Focus.TargetState[i].Size = 0;

                var intercept = MathFuncs.IsDotProductWithinTolerance(ref targetDir, ref myHeading, Session.ApproachDegrees);
                var retreat = MathFuncs.IsDotProductWithinTolerance(ref targetRevDir, ref myHeading, Session.ApproachDegrees);
                if (intercept) Focus.TargetState[i].Engagement = 0;
                else if (retreat) Focus.TargetState[i].Engagement = 1;
                else Focus.TargetState[i].Engagement = 2;

                var distanceFromCenters = Vector3D.Distance(GridCenter, target.PositionComp.WorldAABB.Center);
                distanceFromCenters -= GridRadius;
                distanceFromCenters -= target.PositionComp.LocalVolume.Radius;
                distanceFromCenters = distanceFromCenters <= 0 ? 0 : distanceFromCenters;

                var distPercent = (distanceFromCenters / MaxTargetingRange) * 100;
                if (distPercent > 95) Focus.TargetState[i].Distance = 9;
                else if (distPercent > 90) Focus.TargetState[i].Distance = 8;
                else if (distPercent > 80) Focus.TargetState[i].Distance = 7;
                else if (distPercent > 70) Focus.TargetState[i].Distance = 6;
                else if (distPercent > 60) Focus.TargetState[i].Distance = 5;
                else if (distPercent > 50) Focus.TargetState[i].Distance = 4;
                else if (distPercent > 40) Focus.TargetState[i].Distance = 3;
                else if (distPercent > 30) Focus.TargetState[i].Distance = 2;
                else if (distPercent > 20) Focus.TargetState[i].Distance = 1;
                else if (distPercent > 0) Focus.TargetState[i].Distance = 0;
                else Focus.TargetState[i].Distance = -1;

                var speed = Math.Round(target.Physics?.Speed ?? 0, 1);
                if (speed <= 0) Focus.TargetState[i].Speed = -1;
                else
                {
                    var speedPercent = (speed / Session.MaxEntitySpeed) * 100;
                    if (speedPercent > 95) Focus.TargetState[i].Speed = 9;
                    else if (speedPercent > 90) Focus.TargetState[i].Speed = 8;
                    else if (speedPercent > 80) Focus.TargetState[i].Speed = 7;
                    else if (speedPercent > 70) Focus.TargetState[i].Speed = 6;
                    else if (speedPercent > 60) Focus.TargetState[i].Speed = 5;
                    else if (speedPercent > 50) Focus.TargetState[i].Speed = 4;
                    else if (speedPercent > 40) Focus.TargetState[i].Speed = 3;
                    else if (speedPercent > 30) Focus.TargetState[i].Speed = 2;
                    else if (speedPercent > 20) Focus.TargetState[i].Speed = 1;
                    else if (speedPercent > 0) Focus.TargetState[i].Speed = 0;
                    else Focus.TargetState[i].Speed = -1;
                }

                MyTuple<bool, bool, float, float, float, int> shieldInfo = new MyTuple<bool, bool, float, float, float, int>();
                if (Session.ShieldApiLoaded) shieldInfo = Session.SApi.GetShieldInfo(target);
                if (shieldInfo.Item1)
                {
                    var shieldPercent = shieldInfo.Item5;
                    if (shieldPercent > 95) Focus.TargetState[i].ShieldHealth = 9;
                    else if (shieldPercent > 90) Focus.TargetState[i].ShieldHealth = 8;
                    else if (shieldPercent > 80) Focus.TargetState[i].ShieldHealth = 7;
                    else if (shieldPercent > 70) Focus.TargetState[i].ShieldHealth = 6;
                    else if (shieldPercent > 60) Focus.TargetState[i].ShieldHealth = 5;
                    else if (shieldPercent > 50) Focus.TargetState[i].ShieldHealth = 4;
                    else if (shieldPercent > 40) Focus.TargetState[i].ShieldHealth = 3;
                    else if (shieldPercent > 30) Focus.TargetState[i].ShieldHealth = 2;
                    else if (shieldPercent > 20) Focus.TargetState[i].ShieldHealth = 1;
                    else if (shieldPercent > 0) Focus.TargetState[i].ShieldHealth = 0;
                    else Focus.TargetState[i].ShieldHealth = -1;
                }
                else Focus.TargetState[i].ShieldHealth = -1;

                var grid = target as MyCubeGrid;
                var friend = false;
                if (grid != null && grid.BigOwners.Count != 0)
                {
                    var relation = MyIDModule.GetRelationPlayerBlock(MyOwner, grid.BigOwners[0], MyOwnershipShareModeEnum.Faction);
                    if (relation == MyRelationsBetweenPlayerAndBlock.FactionShare || relation == MyRelationsBetweenPlayerAndBlock.Owner || relation == MyRelationsBetweenPlayerAndBlock.Friends) friend = true;
                }

                if (friend) Focus.TargetState[i].ThreatLvl = -1;
                else
                {
                    int shieldBonus = 0;
                    if (Session.ShieldApiLoaded)
                    {
                        var myShieldInfo = Session.SApi.GetShieldInfo(MyGrid);
                        if (shieldInfo.Item1 && myShieldInfo.Item1)
                            shieldBonus = shieldInfo.Item5 > myShieldInfo.Item5 ? 1 : -1;
                        else if (shieldInfo.Item1) shieldBonus = 1;
                        else if (myShieldInfo.Item1) shieldBonus = -1;
                    }

                    var offenseRating = info.OffenseRating;
                    if (offenseRating > 5) Focus.TargetState[i].ThreatLvl = shieldBonus < 0 ? 8 : 9;
                    else if (offenseRating > 4) Focus.TargetState[i].ThreatLvl = 8 + shieldBonus;
                    else if (offenseRating > 3) Focus.TargetState[i].ThreatLvl = 7 + shieldBonus;
                    else if (offenseRating > 2) Focus.TargetState[i].ThreatLvl = 6 + shieldBonus;
                    else if (offenseRating > 1) Focus.TargetState[i].ThreatLvl = 5 + shieldBonus;
                    else if (offenseRating > 0.5) Focus.TargetState[i].ThreatLvl = 4 + shieldBonus;
                    else if (offenseRating > 0.25) Focus.TargetState[i].ThreatLvl = 3 + shieldBonus;

                    else if (offenseRating > 0.125) Focus.TargetState[i].ThreatLvl = 2 + shieldBonus;
                    else if (offenseRating > 0.0625) Focus.TargetState[i].ThreatLvl = 1 + shieldBonus;
                    else if (offenseRating > 0) Focus.TargetState[i].ThreatLvl = shieldBonus > 0 ? 1 : 0;
                    else Focus.TargetState[i].ThreatLvl = -1;
                }
            }
            return validFocus;
        }

        public void SubGridDetect()
        {
            if (PrevSubGrids.Count == 0) return;

            AddSubGrids.Clear();
            foreach (var sub in PrevSubGrids)
            {
                AddSubGrids.Add(sub);
                TmpSubGrids.Add(sub);
            }

            TmpSubGrids.IntersectWith(RemSubGrids);
            RemSubGrids.ExceptWith(AddSubGrids);
            AddSubGrids.ExceptWith(TmpSubGrids);
            TmpSubGrids.Clear();

            SubGridsChanged =  AddSubGrids.Count != 0 || RemSubGrids.Count != 0;
        }

        public void SubGridChanges()
        {
            foreach (var grid in AddSubGrids)
            {
                if (grid == MyGrid) continue;
                grid.OnFatBlockAdded += FatBlockAdded;
                grid.OnFatBlockRemoved += FatBlockRemoved;

                var blocks = Session.GridToFatMap[grid].MyCubeBocks;
                for (int i = 0; i < blocks.Count; i++)
                    FatBlockAdded(blocks[i]);
            }
            AddSubGrids.Clear();

            foreach (var grid in RemSubGrids)
            {
                if (grid == MyGrid) continue;
                grid.OnFatBlockAdded -= FatBlockAdded;
                grid.OnFatBlockRemoved -= FatBlockRemoved;

                var blocks = Session.GridToFatMap[grid].MyCubeBocks;
                for (int i = 0; i < blocks.Count; i++)
                    FatBlockRemoved(blocks[i]);
            }
            RemSubGrids.Clear();
        }

        #region Power
        internal void InitFakeShipController()
        {
            FatMap fatMap;
            if (FakeShipController != null && Session.GridToFatMap.TryGetValue(MyGrid, out fatMap) && !fatMap.MyCubeBocks.Empty)
                FakeShipController.SlimBlock = fatMap.MyCubeBocks[0].SlimBlock;
        }

        internal void UpdateGridPower()
        {
            GridAvailablePower = 0;
            GridMaxPower = 0;
            GridCurrentPower = 0;
            BatteryMaxPower = 0;
            BatteryCurrentOutput = 0;
            BatteryCurrentInput = 0;

            if (FakeShipController?.GridResourceDistributor != null)
            {
                if (Session.Tick60)
                {
                    foreach (var battery in Batteries)
                    {
                        if (!battery.IsWorking) continue;
                        var currentInput = battery.CurrentInput;
                        var currentOutput = battery.CurrentOutput;
                        var maxOutput = battery.MaxOutput;
                        if (currentInput > 0)
                        {
                            BatteryCurrentInput += currentInput;
                            if (battery.IsCharging) BatteryCurrentOutput -= currentInput;
                            else BatteryCurrentOutput -= currentInput;
                        }
                        BatteryMaxPower += maxOutput;
                        BatteryCurrentOutput += currentOutput;
                    }
                }
                GridMaxPower = FakeShipController.GridResourceDistributor.MaxAvailableResourceByType(GId);
                GridCurrentPower = FakeShipController.GridResourceDistributor.TotalRequiredInputByType(GId);
                GridAvailablePower = GridMaxPower - GridCurrentPower;

                GridCurrentPower += BatteryCurrentInput;
                GridAvailablePower -= BatteryCurrentInput;
            }

            UpdatePowerSources = false;

            if (GridMaxPower - CurrentWeaponsDraw > LastAvailablePower && CurrentWeaponsDraw > MinSinkPower) AvailablePowerIncrease = true;
            LastAvailablePower = GridMaxPower - CurrentWeaponsDraw;

            HadPower = HasPower;
            HasPower = GridMaxPower > 0;

            if (HasPower) return;
            if (HadPower)
                Session.FutureEvents.Schedule(Session.WeaponShootOff, this, 1);
        }
        #endregion
    }
}
