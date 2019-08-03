using System;
using System.Collections.Generic;
using System.Threading;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Ingame;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;
using static WeaponCore.Support.SubSystemDefinition;

namespace WeaponCore.Support
{
    public partial class GridTargetingAi
    {
        internal void RequestDbUpdate()
        {
            if (!UpdateOwner() || Interlocked.CompareExchange(ref DbUpdating, 1, 1) == 1) return;

            Session.Instance.DbsToUpdate.Add(this);
            TargetsUpdatedTick = MySession.Tick;
        }

        private bool UpdateOwner()
        {
            if (MyGrid == null || MyGrid.MarkedForClose)
            {
                MyOwner = 0;
                return false;
            }

            var bigOwners = MyGrid.BigOwners;
            if (bigOwners == null || bigOwners.Count <= 0)
            {
                MyOwner = 0;
                return false;
            }
            MyOwner = bigOwners[0];
            return true;
        }

        public void SubGridInfo()
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

        internal bool CreateEntInfo(MyEntity entity, long gridOwner, out MyDetectedEntityInfo entInfo)
        {
            if (entity == null)
            {
                entInfo = new MyDetectedEntityInfo();
                return false;
            }

            var topMostParent = entity.GetTopMostParent() as MyCubeGrid;
            if (topMostParent != null)
            {
                var type = topMostParent.GridSizeEnum != MyCubeSize.Small ? MyDetectedEntityType.LargeGrid : MyDetectedEntityType.SmallGrid;
                #if VERSION_191
                var relationship = topMostParent.BigOwners.Count != 0 ? MyIDModule.GetRelation(gridOwner, topMostParent.BigOwners[0], MyOwnershipShareModeEnum.Faction) : MyRelationsBetweenPlayerAndBlock.NoOwnership;
                #else
                var relationship = topMostParent.BigOwners.Count != 0 ? MyIDModule.GetRelationPlayerBlock(gridOwner, topMostParent.BigOwners[0], MyOwnershipShareModeEnum.Faction) : MyRelationsBetweenPlayerAndBlock.NoOwnership;
                #endif
                entInfo = new MyDetectedEntityInfo(topMostParent.EntityId, string.Empty, type, null, MatrixD.Zero, Vector3.Zero, relationship, new BoundingBoxD(), Session.Instance.Tick);
                return true;
            }

            var myCharacter = entity as IMyCharacter;
            if (myCharacter != null)
            {
                var controllingId = myCharacter.ControllerInfo?.ControllingIdentityId;
                var playerId = controllingId ?? 0;

                var type = !myCharacter.IsPlayer ? MyDetectedEntityType.CharacterOther : MyDetectedEntityType.CharacterHuman;
                #if VERSION_191
                var relationPlayerBlock = MyIDModule.GetRelation(gridOwner, playerId, MyOwnershipShareModeEnum.Faction);
                #else
                var relationPlayerBlock = MyIDModule.GetRelationPlayerBlock(gridOwner, playerId, MyOwnershipShareModeEnum.Faction);
                #endif

                entInfo = new MyDetectedEntityInfo(entity.EntityId, string.Empty, type, null, MatrixD.Zero, Vector3.Zero, relationPlayerBlock, new BoundingBoxD(), Session.Instance.Tick);
                return true;
            }
            const MyRelationsBetweenPlayerAndBlock relationship1 = MyRelationsBetweenPlayerAndBlock.Neutral;
            var myPlanet = entity as MyPlanet;
            if (myPlanet != null)
            {
                const MyDetectedEntityType type = MyDetectedEntityType.Planet;
                entInfo = new MyDetectedEntityInfo(entity.EntityId, string.Empty, type, null, MatrixD.Zero, Vector3.Zero, relationship1, new BoundingBoxD(), Session.Instance.Tick);
                return true;
            }
            if (entity is MyVoxelMap)
            {
                const MyDetectedEntityType type = MyDetectedEntityType.Asteroid;
                entInfo = new MyDetectedEntityInfo(entity.EntityId, string.Empty, type, null, MatrixD.Zero, Vector3.Zero, relationship1, new BoundingBoxD(), Session.Instance.Tick);
                return true;
            }
            if (entity is MyMeteor)
            {
                const MyDetectedEntityType type = MyDetectedEntityType.Meteor;
                entInfo = new MyDetectedEntityInfo(entity.EntityId, string.Empty, type, null, MatrixD.Zero, Vector3.Zero, relationship1, new BoundingBoxD(), Session.Instance.Tick);
                return true;
            }
            entInfo = new MyDetectedEntityInfo();
            return false;
        }

        internal struct DetectInfo
        {
            internal MyEntity Parent;
            internal Dictionary<BlockTypes, List<MyCubeBlock>> DictTypes;
            internal MyDetectedEntityInfo EntInfo;

            public DetectInfo(MyEntity parent, Dictionary<BlockTypes, List<MyCubeBlock>> dictTypes, MyDetectedEntityInfo entInfo)
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

                var xVelLen = !xNull ? xVel.Value.LengthSquared(): 0;
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
            internal readonly MyDetectedEntityInfo EntInfo;
            internal readonly MyEntity Target;
            internal readonly bool IsGrid;
            internal readonly int PartCount;
            internal readonly MyCubeGrid MyGrid;
            internal readonly GridTargetingAi Ai;
            internal Dictionary<BlockTypes, List<MyCubeBlock>> TypeDict;

            internal TargetInfo(MyDetectedEntityInfo entInfo, MyEntity target, bool isGrid, Dictionary<BlockTypes, List<MyCubeBlock>> typeDict, int partCount, MyCubeGrid myGrid, GridTargetingAi ai)
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
    }
}
