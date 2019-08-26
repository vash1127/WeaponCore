using System;
using System.Collections.Generic;
using System.Threading;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;
using WeaponCore.Support;
using static WeaponCore.Support.TargetingDefinition;
namespace WeaponCore.Support
{
    public partial class GridAi
    {
        internal void RequestDbUpdate()
        {
            if (!UpdateOwner() || Interlocked.CompareExchange(ref DbUpdating, 1, 1) == 1) return;
            Session.Instance.DbsToUpdate.Add(this);
            TargetsUpdatedTick = Session.Instance.Tick;
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

        internal void DebugPlanet()
        {
            if (MyPlanet == null) return;
            var planetCenter = MyPlanet.PositionComp.WorldAABB.Center;
            var closestPointSphere = new BoundingSphereD(planetCenter, Vector3D.Distance(planetCenter, PlanetClosestPoint));
            var closestSphere = new BoundingSphereD(planetCenter, MyPlanet.MinimumRadius);
            var furthestSphere = new BoundingSphereD(planetCenter, MyPlanet.MaximumRadius);
            DsDebugDraw.DrawSphere(closestPointSphere, Color.Green);
            DsDebugDraw.DrawSphere(closestSphere, Color.Blue);
            DsDebugDraw.DrawSphere(furthestSphere, Color.Red);
            DsDebugDraw.DrawSingleVec(PlanetClosestPoint, 10f, Color.Red);
        }

        internal void SubGridInfo()
        {
            SubUpdate = false;
            SubTick = Session.Instance.Tick + 10;
            SubGridUpdate = true;
            SubGrids.Clear();
            foreach (var sub in MyAPIGateway.GridGroups.GetGroup(MyGrid, GridLinkTypeEnum.Mechanical))
                SubGrids.Add((MyCubeGrid) sub);

            foreach (var sub in SubGrids)
                    GroupAABB.Include(sub.PositionComp.WorldAABB);

            SubGridUpdate = false;
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
                entInfo = new Sandbox.ModAPI.Ingame.MyDetectedEntityInfo(topMostParent.EntityId, string.Empty, type, null, MatrixD.Zero, Vector3.Zero, relationship, new BoundingBoxD(), Session.Instance.Tick);
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

                entInfo = new Sandbox.ModAPI.Ingame.MyDetectedEntityInfo(entity.EntityId, string.Empty, type, null, MatrixD.Zero, Vector3.Zero, relationPlayerBlock, new BoundingBoxD(), Session.Instance.Tick);
                return true;
            }
            const MyRelationsBetweenPlayerAndBlock relationship1 = MyRelationsBetweenPlayerAndBlock.Neutral;
            var myPlanet = entity as MyPlanet;
            if (myPlanet != null)
            {
                const Sandbox.ModAPI.Ingame.MyDetectedEntityType type = Sandbox.ModAPI.Ingame.MyDetectedEntityType.Planet;
                entInfo = new Sandbox.ModAPI.Ingame.MyDetectedEntityInfo(entity.EntityId, string.Empty, type, null, MatrixD.Zero, Vector3.Zero, relationship1, new BoundingBoxD(), Session.Instance.Tick);
                return true;
            }
            if (entity is MyVoxelMap)
            {
                const Sandbox.ModAPI.Ingame.MyDetectedEntityType type = Sandbox.ModAPI.Ingame.MyDetectedEntityType.Asteroid;
                entInfo = new Sandbox.ModAPI.Ingame.MyDetectedEntityInfo(entity.EntityId, string.Empty, type, null, MatrixD.Zero, Vector3.Zero, relationship1, new BoundingBoxD(), Session.Instance.Tick);
                return true;
            }
            if (entity is MyMeteor)
            {
                const Sandbox.ModAPI.Ingame.MyDetectedEntityType type = Sandbox.ModAPI.Ingame.MyDetectedEntityType.Meteor;
                entInfo = new Sandbox.ModAPI.Ingame.MyDetectedEntityInfo(entity.EntityId, string.Empty, type, null, MatrixD.Zero, Vector3.Zero, relationship1, new BoundingBoxD(), Session.Instance.Tick);
                return true;
            }
            entInfo = new Sandbox.ModAPI.Ingame.MyDetectedEntityInfo();
            return false;
        }

        internal struct DetectInfo
        {
            internal MyEntity Parent;
            internal Dictionary<BlockTypes, List<MyCubeBlock>> DictTypes;
            internal Sandbox.ModAPI.Ingame.MyDetectedEntityInfo EntInfo;

            public DetectInfo(MyEntity parent, Dictionary<BlockTypes, List<MyCubeBlock>> dictTypes, Sandbox.ModAPI.Ingame.MyDetectedEntityInfo entInfo)
            {
                Parent = parent;
                DictTypes = dictTypes;
                EntInfo = entInfo;
            }
        }

        internal class TargetCompare : IComparer<TargetInfo>
        {
            public int Compare(TargetInfo x, TargetInfo y)
            {
                var xNull = x.Target.Physics == null;
                var yNull = y.Target.Physics == null;
                var xandYNull = xNull && yNull;
                var xVel = xNull ? (Vector3?) null : x.Target.Physics.LinearVelocity;
                var yVel = yNull ? (Vector3?)null : y.Target.Physics.LinearVelocity;
                var xTargetPos = x.Target.PositionComp.GetPosition();
                var xMyPos = x.MyGrid.PositionComp.GetPosition();
                var yTargetPos = y.Target.PositionComp.GetPosition();
                var yMyPos = y.MyGrid.PositionComp.GetPosition();

                double xDist;
                double yDist;
                Vector3D.DistanceSquared(ref xTargetPos, ref xMyPos, out xDist);
                Vector3D.DistanceSquared(ref yTargetPos, ref yMyPos, out yDist);

                var xVelLen = !xNull ? xVel.Value.LengthSquared() : 0;
                var yVelLen = !yNull ? yVel.Value.LengthSquared() : 0;

                if (!xNull) xVel.Value.LengthSquared();

                var xApproching = xandYNull || !xNull && Vector3.Dot(xVel.Value, xTargetPos - xMyPos) < 0 && xVelLen > 25;
                var yApproching = !xandYNull && !yNull && Vector3.Dot(yVel.Value, yTargetPos - yMyPos) < 0 && yVelLen > 25;

                var compareApproch = xApproching.CompareTo(yApproching);
                if (compareApproch != 0 && (xDist < 640000 || yDist < 640000)) return -compareApproch;

                if (xDist < 1000000 || yDist < 1000000)
                {
                    var compareVelocity = xVelLen.CompareTo(yVelLen);
                    if (compareVelocity != 0 && (xVelLen > 3600 || yVelLen > 3600)) return -compareVelocity;
                }

                if (xDist > 10000 && x.PartCount < 5) xDist = double.MaxValue;
                if (yDist > 10000 && y.PartCount < 5) yDist = double.MaxValue;

                var compareDist = xDist.CompareTo(yDist);
                if (compareDist != 0 && (xDist < 360000 || yDist < 360000)) return compareDist;

                var compareParts = x.PartCount.CompareTo(y.PartCount);
                return -compareParts;
            }
        }

        internal struct TargetInfo
        {
            internal readonly Sandbox.ModAPI.Ingame.MyDetectedEntityInfo EntInfo;
            internal readonly MyEntity Target;
            internal readonly bool IsGrid;
            internal readonly int PartCount;
            internal readonly MyCubeGrid MyGrid;
            internal readonly GridAi Ai;
            internal Dictionary<BlockTypes, List<MyCubeBlock>> TypeDict;

            internal TargetInfo(Sandbox.ModAPI.Ingame.MyDetectedEntityInfo entInfo, MyEntity target, bool isGrid, Dictionary<BlockTypes, List<MyCubeBlock>> typeDict, int partCount, MyCubeGrid myGrid, GridAi ai)
            {
                EntInfo = entInfo;
                Target = target;
                IsGrid = isGrid;
                PartCount = partCount;
                MyGrid = myGrid;
                Ai = ai;
                TypeDict = typeDict;
            }

            internal bool Clean()
            {
                if (TypeDict != null)
                {
                    foreach (var type in TypeDict)
                    {
                        type.Value.Clear();
                        Ai.CubePool.Return(type.Value);
                    }
                    TypeDict.Clear();
                    Ai.BlockTypePool.Return(TypeDict);
                    TypeDict = null;
                }
                return true;
            }
        }

        internal Dictionary<string,HashSet<IMyLargeTurretBase>> GetWeaponGroups()
        {
            Dictionary<string, HashSet<IMyLargeTurretBase>> weaponGroups = new Dictionary<string, HashSet<IMyLargeTurretBase>>();

            var TermSys = MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(MyGrid);
            List<IMyBlockGroup> groups = new List<IMyBlockGroup>();
            TermSys.GetBlockGroups(groups);

            List<IMyLargeTurretBase> blocks = new List<IMyLargeTurretBase>();

            for (int i = 0; i < groups.Count; i++) {
                groups[i].GetBlocksOfType(blocks);
                var coreWeapons = new HashSet<IMyLargeTurretBase>();
                for (int j = 0; j < blocks.Count; j++) {
                    if(blocks[j]?.Components?.Get<WeaponComponent>() != null)
                    {
                        coreWeapons.Add(blocks[j]);
                    }
                }

                if (coreWeapons.Count > 0)
                    weaponGroups.Add(groups[i].Name,coreWeapons);

                blocks.Clear();
            }
            return weaponGroups;
        }

        #region Power
        internal bool UpdateGridPower(bool updateLast)
        {
            GridAvailablePower = 0;
            GridMaxPower = 0;
            GridCurrentPower = 0;
            BatteryMaxPower = 0;
            BatteryCurrentOutput = 0;
            BatteryCurrentInput = 0;

            foreach (var source in Sources)
            {
                var battery = source.Entity as IMyBatteryBlock;
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
            return GridMaxPower > 0;
        }
        #endregion
    }
}
