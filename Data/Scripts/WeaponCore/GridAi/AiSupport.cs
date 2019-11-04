using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
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

        public void UpdateBlockGroups(bool clear = false)
        {
            if (BlockGroups == null)
            {
                Log.Line($"BlockGroups null");
                return;
            }
            if (BlockGroupPool == null)
            {
                Log.Line($"BlockGroupPool null");
                return;
            }
            if (MyGrid == null || MyGrid.MarkedForClose)
            {
                Log.Line($"MyGrid Null:{MyGrid == null} - Marked:{MyGrid?.MarkedForClose} - Age:{Session.Tick - CreatedTick}");
                return;
            }
            if (TmpBlockGroups == null)
            {
                Log.Line($"TmpBlockGroups null");
                return;
            }
            if (TerminalSystem == null)
            {
                Log.Line($"TerminalSystem null: Age:{Session.Tick - CreatedTick}");
                return;
            }
            foreach (var bg in BlockGroups)
            {
                bg.Value.Clear();
                BlockGroupPool.Return(bg.Value);
            }
            BlockGroups.Clear();

            if (clear) return;

            TerminalSystem.GetBlockGroups(TmpBlockGroups);
            foreach (var b in TmpBlockGroups)
            {
                var groupList = TmpBlockGroupPool.Get();
                b.GetBlocks(groupList);
                var name = b.Name;
                var groupSet = BlockGroupPool.Get();
                foreach (var terminal in groupList)
                {
                    var cube = terminal as MyCubeBlock;
                    if (cube != null)
                    {
                        WeaponComponent weaponComp;
                        if (WeaponBase.TryGetValue(cube, out weaponComp))
                        {
                            weaponComp.GroupNames.Add(name);
                            groupSet.Add(cube);
                        }
                    }
                }
                groupList.Clear();
                TmpBlockGroupPool.Return(groupList);
                BlockGroups.Add(name, groupSet);
            }
            TmpBlockGroups.Clear();
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

            public DetectInfo(Session session, MyEntity parent, Sandbox.ModAPI.Ingame.MyDetectedEntityInfo entInfo, int partCount)
            {
                Parent = parent;
                EntInfo = entInfo;
                PartCount = partCount;
                var armed = false;
                if (parent is MyCubeGrid)
                {
                    ConcurrentDictionary<BlockTypes, MyConcurrentList<MyCubeBlock>> blockTypeMap;
                    if (session.GridToBlockTypeMap.TryGetValue((MyCubeGrid)Parent, out blockTypeMap))
                    {
                        MyConcurrentList<MyCubeBlock> weaponBlocks;
                        if (blockTypeMap.TryGetValue(BlockTypes.Offense, out weaponBlocks) && weaponBlocks.Count > 0)
                            armed = true;
                    }
                }
                Armed = armed;
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
            internal bool Approaching;
            internal int PartCount;
            internal float OffenseRating;
            internal MyCubeGrid MyGrid;
            internal GridAi MyAi;
            internal GridAi TargetAi;

            internal void Init(ref DetectInfo detectInfo, bool isGrid, MyCubeGrid myGrid, GridAi myAi, GridAi targetAi)
            {
                EntInfo = detectInfo.EntInfo;
                Target = detectInfo.Parent;
                PartCount = detectInfo.PartCount;
                IsGrid = isGrid;
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
                else if (detectInfo.Armed) OffenseRating = 1;
                else OffenseRating = 0;
                Vector3D.DistanceSquared(ref TargetPos, ref myAi.GridCenter, out DistSqr);
                var adjustedDist = DistSqr - (MyAi.GridRadius * MyAi.GridRadius);
                CollisionDistSqr = adjustedDist > 0 ? adjustedDist : 0;
            }
        }

        #region Power
        internal void UpdateGridPower(bool updateLast)
        {
            GridAvailablePower = 0;
            GridMaxPower = 0;
            GridCurrentPower = 0;
            BatteryMaxPower = 0;
            BatteryCurrentOutput = 0;
            BatteryCurrentInput = 0;

            foreach (var source in Sources)
            {
                var battery = source.Entity as MyBatteryBlock;
                if (battery != null)
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
                else
                {
                    GridMaxPower += source.MaxOutputByType(GId);
                    GridCurrentPower += source.CurrentOutputByType(GId);
                }
            }
            GridMaxPower += BatteryMaxPower;
            GridCurrentPower += BatteryCurrentOutput;

            GridAvailablePower = GridMaxPower - GridCurrentPower;

            GridCurrentPower += BatteryCurrentInput;
            GridAvailablePower -= BatteryCurrentInput;
            UpdatePowerSources = false;
            if (updateLast)
            {
                if (GridMaxPower - CurrentWeaponsDraw > LastAvailablePower && CurrentWeaponsDraw > MinSinkPower) AvailablePowerIncrease = true;

                LastAvailablePower = GridMaxPower - CurrentWeaponsDraw;
                //Log.Line($"avail power: {gridAi.GridMaxPower - gridAi.CurrentWeaponsDraw}  Last Power: {gridAi.LastAvailablePower} Max: {gridAi.GridMaxPower}  Weapon Draw: {gridAi.CurrentWeaponsDraw} Current Power: {gridAi.GridCurrentPower}");
            }
            HadPower = HasPower;
            HasPower = GridMaxPower > 0;
        }
        #endregion
    }
}
