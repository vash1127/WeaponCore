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
            if (Interlocked.Read(ref DbUpdating) == 1) return;
            Interlocked.Exchange(ref DbUpdating, 1);

            Session.Instance.DbsToUpdate.Add(this);
            var bigOwners = MyGrid.BigOwners;
            MyOwner = bigOwners.Count > 0 ? bigOwners[0] : 0;
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
            var min = firstCard;
            var max = cardsToSort - 1;
            var count = max - min + 1;
            if (prevDeckLen != count)
            {
                Array.Resize(ref deck, count);
                prevDeckLen = count;
            }

            for (int i = 0; i < count; i++)
            {
                var j = MyUtils.GetRandomInt(0, i + 1);

                deck[i] = deck[j];
                deck[j] = min + i;
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
                var relationship = topMostParent.BigOwners.Count != 0 ? MyIDModule.GetRelationPlayerBlock(gridOwner, topMostParent.BigOwners[0], MyOwnershipShareModeEnum.Faction) : MyRelationsBetweenPlayerAndBlock.NoOwnership;
                entInfo = new MyDetectedEntityInfo(topMostParent.EntityId, string.Empty, type, null, MatrixD.Zero, Vector3.Zero, relationship, new BoundingBoxD(), Session.Instance.Tick);
                return true;
            }

            var myCharacter = entity as IMyCharacter;
            if (myCharacter != null)
            {
                var controllingId = myCharacter.ControllerInfo?.ControllingIdentityId;
                var playerId = controllingId ?? 0;

                var type = !myCharacter.IsPlayer ? MyDetectedEntityType.CharacterOther : MyDetectedEntityType.CharacterHuman;
                var relationPlayerBlock = MyIDModule.GetRelationPlayerBlock(gridOwner, playerId, MyOwnershipShareModeEnum.Faction);
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
            internal List<MyEntity> Cubes;
            internal MyDetectedEntityInfo EntInfo;

            public DetectInfo(MyEntity parent, List<MyEntity> cubes, MyDetectedEntityInfo entInfo)
            {
                Parent = parent;
                Cubes = cubes;
                EntInfo = entInfo;
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

        internal struct TargetInfo
        {
            internal readonly MyDetectedEntityInfo EntInfo;
            internal readonly MyEntity Target;
            internal readonly bool IsGrid;
            internal readonly int PartCount;
            internal readonly MyCubeGrid MyGrid;
            internal readonly GridTargetingAi Ai;
            internal List<MyEntity> Cubes;

            internal TargetInfo(MyDetectedEntityInfo entInfo, MyEntity target, bool isGrid, List<MyEntity> cubes, int partCount, MyCubeGrid myGrid, GridTargetingAi ai)
            {
                EntInfo = entInfo;
                Target = target;
                IsGrid = isGrid;
                PartCount = partCount;
                MyGrid = myGrid;
                Ai = ai;
                Cubes = cubes;
            }

            internal bool Clean()
            {
                if (Cubes != null)
                {
                    Cubes.Clear();
                    Ai.CubePool.Return(Cubes);
                    Cubes = null;
                }
                return true;
            }
        }
    }
}
