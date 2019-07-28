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

namespace WeaponCore.Support
{
    public partial class GridTargetingAi
    {
        internal void TimeToUpdateDb()
        {
            var notValid = MyGrid == null || MyGrid.MarkedForClose;
            var bigOwners = MyGrid?.BigOwners;
            if (notValid || bigOwners == null || bigOwners.Count <= 0)
            {
                MyOwner = 0;
                return;
            }
            MyOwner = bigOwners[0];

            if (Interlocked.CompareExchange(ref DbUpdating, 1, 1) == 1) return;
            Session.Instance.DbsToUpdate.Add(this);

            Stale = false;
            TargetsUpdatedTick = MySession.Tick;
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
                var compareParts = x.PartCount.CompareTo(y.PartCount);
                if (compareParts != 0) return -compareParts;
                var xNull = x.Target.Physics == null;
                var yNull = y.Target.Physics == null;
                var xandYNull = xNull && yNull;
                var xApproching = xandYNull || !xNull && Vector3.Dot(x.Target.Physics.LinearVelocity, x.Target.PositionComp.GetPosition() - x.MyGrid.PositionComp.GetPosition()) < 0;
                var yApproching = !xandYNull && !yNull && Vector3.Dot(y.Target.Physics.LinearVelocity, y.Target.PositionComp.GetPosition() - y.MyGrid.PositionComp.GetPosition()) < 0;
                return xApproching.CompareTo(yApproching);
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
