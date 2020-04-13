using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using ProtoBuf;
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
using WeaponCore.Platform;
using WeaponCore.Support;
using WeaponCore.Projectiles;
using static WeaponCore.Session;
using static WeaponCore.Support.WeaponDefinition.TargetingDef;
using static WeaponCore.WeaponRandomGenerator;

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
            internal bool TargetInRange;
            internal double ThreatRangeSqr;

            internal bool ValidTargetExists(Weapon w)
            {
                var comp = w.Comp;
                var ai = comp.Ai;

                return ThreatRangeSqr <= w.MaxTargetDistanceSqr || ai.Focus.HasFocus;
            }

            internal void Clean()
            {
                ThreatRangeSqr = double.MaxValue;
                TargetInRange = false;
            }
        }



        internal void UpdateConstruct()
        {
            Construct = new Constructs(this);
        }

        internal struct Constructs
        {
            internal readonly float OptimalDps;
            internal readonly int BlockCount;

            internal Constructs(GridAi ai)
            {
                FatMap fatMap;
                if (ai?.MyGrid != null && ai.Session.GridToFatMap.TryGetValue(ai.MyGrid, out fatMap))
                {
                    BlockCount = fatMap.MostBlocks;
                    OptimalDps = ai.OptimalDps;
                    foreach (var grid in ai.SubGrids)
                    {
                        if (grid == ai.MyGrid) continue;
                        if (ai.Session.GridToFatMap.TryGetValue(grid, out fatMap))
                        {
                            BlockCount += ai.Session.GridToFatMap[grid].MostBlocks;
                            OptimalDps += ai.OptimalDps;
                        }
                    }
                    return;
                }

                OptimalDps = 0;
                BlockCount = 0;
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
                    ConcurrentDictionary<BlockTypes, ConcurrentCachingList<MyCubeBlock>> blockTypeMap;
                    if (session.GridToBlockTypeMap.TryGetValue((MyCubeGrid)Parent, out blockTypeMap))
                    {
                        ConcurrentCachingList<MyCubeBlock> weaponBlocks;
                        if (blockTypeMap.TryGetValue(BlockTypes.Offense, out weaponBlocks) && weaponBlocks.Count > 0)
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
            internal Vector3D TargetPos;
            internal Vector3 Velocity;
            internal double DistSqr;
            internal float VelLenSqr;
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
                if (!MyUtils.IsZero(Velocity, 1E-02F))
                {
                    TargetDir = Vector3D.Normalize(Velocity);
                    var refDir = Vector3D.Normalize(myAi.GridVolume.Center - TargetPos);
                    Approaching = MathFuncs.IsDotProductWithinTolerance(ref TargetDir, ref refDir, myAi.Session.ApproachDegrees);
                }
                else
                {
                    TargetDir = Vector3D.Zero;
                    Approaching = false;
                }

                if (targetAi != null)
                {
                    OffenseRating = targetAi.Construct.OptimalDps / myAi.Construct.OptimalDps;
                }
                else if (detectInfo.Armed) OffenseRating = 0.0001f;
                else OffenseRating = 0;

                var targetDist = Vector3D.Distance(myAi.GridVolume.Center, TargetPos) - TargetRadius;
                targetDist -= myAi.GridVolume.Radius;
                if (targetDist < 0) targetDist = 0;
                DistSqr = targetDist * targetDist;
            }
        }

        internal void RequestDbUpdate()
        {
            GridVolume = MyGrid.PositionComp.WorldVolume;
            Session.DbsToUpdate.Add(this);
            TargetsUpdatedTick = Session.Tick;
        }

        internal void ReScanBlockGroups(bool networkSync = false)
        {
            if (TerminalSystem == null)
                TerminalSystem = MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(MyGrid);
            if (TerminalSystem != null)
            {
                TerminalSystem.GetBlockGroups(null, group =>
                {
                    GroupInfo groupInfo = null;
                    if (BlockGroups.TryGetValue(group.Name, out groupInfo))
                    {
                        groupInfo.ChangeState = GroupInfo.ChangeStates.None;
                        groupInfo.Name = group.Name;
                        groupInfo.Comps.Clear();
                    }

                    group.GetBlocks(null, block =>
                    {
                        var cube = (MyCubeBlock) block;
                        WeaponComponent comp;
                        if (cube.Components.TryGet(out comp) && SubGrids.Contains(cube.CubeGrid))
                        {
                            if (groupInfo == null)
                            {
                                groupInfo = Session.GroupInfoPool.Get();
                                groupInfo.Name = group.Name;
                                groupInfo.ChangeState = GroupInfo.ChangeStates.Add;
                                BlockGroups.Add(group.Name, groupInfo);
                            }
                            groupInfo.Comps.Add(comp);
                            if (groupInfo.ChangeState == GroupInfo.ChangeStates.None)
                                groupInfo.ChangeState = GroupInfo.ChangeStates.Modify;
                        }
                        return false;
                    });

                    return false;
                });
                BlockGroups.ApplyAdditionsAndModifications();
                foreach (var group in BlockGroups)
                {
                    if (group.Value.ChangeState == GroupInfo.ChangeStates.None)
                    {
                        group.Value.Comps.Clear();
                        Session.GroupInfoPool.Return(group.Value);
                        BlockGroups.Remove(group.Key);
                    }
                    else group.Value.ChangeState = GroupInfo.ChangeStates.None;
                }
                BlockGroups.ApplyRemovals();

                ScanBlockGroups = false;

                if (Session.MpActive && Session.HandlesInput && !networkSync)
                {
                    Session.SendGroupUpdate(this);
                }
            }
        }

        internal void UpdateGroupOverRides()
        {
            var resetOverRides = new GroupOverrides() {Activate = true };

            foreach (var group in GroupsToCheck)
            {
                var groupOverrides = GetOverrides(this, group.Name);
                foreach(var comp in group.Comps)
                {
                    if (comp.State.Value.CurrentBlockGroup != group.Name) continue;

                    var o = comp.Set.Value.Overrides;
                    if (groupOverrides.Equals(o))
                        return;
                }

                SyncGridOverrides(this, group.Name, resetOverRides);
            }
            GroupsToCheck.Clear();
            ScanBlockGroupSettings = false;
        }

        internal void CompChange(bool add, WeaponComponent comp)
        {
            if (add)
            {
                if (WeaponsIdx.ContainsKey(comp))
                    return;
                WeaponsIdx.Add(comp, Weapons.Count);
                Weapons.Add(comp);
            }
            else
            {

                int idx;
                if (!WeaponsIdx.TryGetValue(comp, out idx))
                    return;

                Weapons.RemoveAtFast(idx);
                if (idx < Weapons.Count)
                    WeaponsIdx[Weapons[idx]] = idx;

                WeaponsIdx.Remove(comp);
            }
        }


        internal bool WeaponTerminalReleased()
        {
            if (LastWeaponTerminal != null && WeaponTerminalAccess)
            {
                if (MyAPIGateway.Gui.ActiveGamePlayScreen == "MyGuiScreenTerminal") return false;
                WeaponTerminalAccess = false;
                return true;
            }
            return false;
        }

        internal bool UpdateOwner()
        {
            using (MyGrid.Pin())
            {
                if (MyGrid == null || MyGrid.MarkedForClose || !MyGrid.InScene)
                    return false;

                var bigOwners = MyGrid.BigOwners;
                MyOwner = bigOwners == null || bigOwners.Count <= 0 ? 0 : MyOwner = bigOwners[0];
                return true;
            }
        }

        internal void MyPlanetInfo(bool clear = false)
        {
            if (!clear)
            {
                MyPlanet = MyPlanetTmp;
                var gridVolume = MyGrid.PositionComp.WorldVolume;
                var gridRadius = gridVolume.Radius;
                var gridCenter = gridVolume.Center;
                var planetCenter = MyPlanet.PositionComp.WorldAABB.Center;

                ClosestPlanetSqr = double.MaxValue;
                if (new BoundingSphereD(planetCenter, MyPlanet.AtmosphereRadius + gridRadius).Intersects(gridVolume))
                {
                    InPlanetGravity = true;
                    PlanetClosestPoint = MyPlanet.GetClosestSurfacePointGlobal(gridCenter);
                    double pointDistSqr;
                    Vector3D.DistanceSquared(ref PlanetClosestPoint, ref gridCenter, out pointDistSqr);

                    pointDistSqr -= (gridRadius * gridRadius);
                    if (pointDistSqr < 0) pointDistSqr = 0;
                    ClosestPlanetSqr = pointDistSqr;
                    PlanetSurfaceInRange = pointDistSqr <= MaxTargetingRangeSqr;
                }
                else
                {
                    InPlanetGravity = false;
                    PlanetClosestPoint = MyPlanet.GetClosestSurfacePointGlobal(gridCenter);
                    double pointDistSqr;
                    Vector3D.DistanceSquared(ref PlanetClosestPoint, ref gridCenter, out pointDistSqr);

                    pointDistSqr -= (gridRadius * gridRadius);
                    if (pointDistSqr < 0) pointDistSqr = 0;
                    ClosestPlanetSqr = pointDistSqr;
                }
            }
            else
            {
                MyPlanet = null;
                PlanetClosestPoint = Vector3D.Zero;
                PlanetSurfaceInRange = false;
                InPlanetGravity = false;
                ClosestPlanetSqr = double.MaxValue;
            }
        }

        internal void MyStaticInfo()
        {
            ClosestStaticSqr = double.MaxValue;
            StaticGridInRange = false;
            MyEntity closestEnt = null;
            var closestCenter = Vector3D.Zero;
            double closestDistSqr = double.MaxValue;
            for (int i = 0; i < StaticsInRange.Count; i++)
            {
                var ent = StaticsInRange[i];
                if (ent == null) continue;
                using (ent.Pin())
                {
                    if (ent.MarkedForClose) continue;

                    var staticCenter = ent.PositionComp.WorldAABB.Center;
                    if (ent is MyCubeGrid) StaticGridInRange = true;

                    double distSqr;
                    Vector3D.DistanceSquared(ref staticCenter, ref GridVolume.Center, out distSqr);
                    if (distSqr < closestDistSqr)
                    {
                        closestDistSqr = distSqr;
                        closestEnt = ent;
                        closestCenter = staticCenter;
                    }
                }
            }

            if (closestEnt != null)
            {
                var dist = Vector3D.Distance(GridVolume.Center, closestCenter);
                dist -= closestEnt.PositionComp.LocalVolume.Radius;
                dist -= GridVolume.Radius;
                if (dist < 0) dist = 0;

                var distSqr = dist * dist;

                if (ClosestPlanetSqr < distSqr) distSqr = ClosestPlanetSqr;

                ClosestStaticSqr = distSqr;
            }
            else if (ClosestPlanetSqr < ClosestStaticSqr) ClosestStaticSqr = ClosestPlanetSqr;
        }

        public static bool GridEnemy(long gridOwner, MyCubeGrid grid, List<long> owners = null)
        {
            if (owners == null) owners = grid.BigOwners;
            if (owners.Count == 0) return true;
            var relationship = MyIDModule.GetRelationPlayerBlock(gridOwner, owners[0], MyOwnershipShareModeEnum.Faction);
            var enemy = relationship != MyRelationsBetweenPlayerAndBlock.Owner && relationship != MyRelationsBetweenPlayerAndBlock.FactionShare;
            return enemy;
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
                /*
                var hasOwner = topMostParent.BigOwners.Count != 0;
                MyRelationsBetweenPlayerAndBlock relationship;
                
                if (hasOwner)
                {
                    var topOwner = topMostParent.BigOwners[0];
                    relationship = MyIDModule.GetRelationPlayerBlock(gridOwner, topOwner, MyOwnershipShareModeEnum.Faction);

                    if (relationship == MyRelationsBetweenPlayerAndBlock.Neutral)
                    {
                        var topFaction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(topOwner);
                        if (topFaction != null)
                        {
                            var aiFaction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(gridOwner);
                            if (aiFaction != null && MyAPIGateway.Session.Factions.GetReputationBetweenPlayerAndFaction(aiFaction.FactionId, topFaction.FactionId) < -500)
                                relationship = MyRelationsBetweenPlayerAndBlock.Enemies;
                        }
                    }
                }
                else relationship = MyRelationsBetweenPlayerAndBlock.Owner;
                */
                MyRelationsBetweenPlayerAndBlock relationship;
                if (topMostParent.BigOwners.Count > 0)
                {
                    var topOwner = topMostParent.BigOwners[0];
                    relationship = MyIDModule.GetRelationPlayerBlock(gridOwner, topOwner, MyOwnershipShareModeEnum.Faction);

                    if (relationship != MyRelationsBetweenPlayerAndBlock.Owner && relationship != MyRelationsBetweenPlayerAndBlock.Friends && relationship != MyRelationsBetweenPlayerAndBlock.FactionShare)
                    {
                        var topFaction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(topOwner);
                        if (topFaction != null)
                        {
                            var rep = MyAPIGateway.Session.Factions.GetReputationBetweenPlayerAndFaction(gridOwner, topFaction.FactionId);
                            if (topFaction.Members.ContainsKey(gridOwner))
                                relationship = MyRelationsBetweenPlayerAndBlock.FactionShare;
                            else if (rep < -500)
                                relationship = MyRelationsBetweenPlayerAndBlock.Enemies;
                            else if (rep <= 0)
                                relationship = MyRelationsBetweenPlayerAndBlock.Neutral;
                            else relationship = MyRelationsBetweenPlayerAndBlock.Friends;
                        }
                    }
                }
                else relationship = MyRelationsBetweenPlayerAndBlock.NoOwnership;
                var type = topMostParent.GridSizeEnum != MyCubeSize.Small ? Sandbox.ModAPI.Ingame.MyDetectedEntityType.LargeGrid : Sandbox.ModAPI.Ingame.MyDetectedEntityType.SmallGrid;
                entInfo = new Sandbox.ModAPI.Ingame.MyDetectedEntityInfo(topMostParent.EntityId, string.Empty, type, null, MatrixD.Zero, Vector3.Zero, relationship, new BoundingBoxD(), Session.Tick);
                return true;
            }

            var myCharacter = entity as IMyCharacter;
            if (myCharacter != null)
            {
                var controllingId = myCharacter.ControllerInfo?.ControllingIdentityId;
                var playerId = controllingId ?? 0;

                var type = !myCharacter.IsPlayer ? Sandbox.ModAPI.Ingame.MyDetectedEntityType.CharacterOther : Sandbox.ModAPI.Ingame.MyDetectedEntityType.CharacterHuman;
                var relationPlayerBlock = MyIDModule.GetRelationPlayerBlock(gridOwner, playerId, MyOwnershipShareModeEnum.Faction);

                entInfo = new Sandbox.ModAPI.Ingame.MyDetectedEntityInfo(entity.EntityId, string.Empty, type, null, MatrixD.Zero, Vector3.Zero, relationPlayerBlock, new BoundingBoxD(), Session.Tick);
                return !myCharacter.IsDead || myCharacter.Integrity <= 0;
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
                entInfo = new Sandbox.ModAPI.Ingame.MyDetectedEntityInfo(entity.EntityId, string.Empty, type, null, MatrixD.Zero, Vector3.Zero, MyRelationsBetweenPlayerAndBlock.Enemies, new BoundingBoxD(), Session.Tick);
                return true;
            }
            entInfo = new Sandbox.ModAPI.Ingame.MyDetectedEntityInfo();
            return false;
        }

        private static int[] GetDeck(ref int[] deck, ref int prevDeckLen, int firstCard, int cardsToSort, int cardsToShuffle, WeaponRandomGenerator rng, RandomType type)
        {
            var count = cardsToSort - firstCard;
            if (prevDeckLen < count)
            {
                deck = new int[count];
                prevDeckLen = count;
            }

            Random rnd;
            if (type == RandomType.Acquire)
                rnd = rng.AcquireRandom;
            else
            {
                rnd = rng.ClientProjectileRandom;
                rng.ClientProjectileCurrentCounter += count;
            }

            for (int i = 0; i < count; i++)
            {
                var j = i < cardsToShuffle ? rnd.Next(i + 1) : i;

                deck[i] = deck[j];
                deck[j] = firstCard + i;
            }
            return deck;
        }

        static void ShellSort(List<Projectile> list, Vector3D weaponPos)
        {
            int length = list.Count;

            for (int h = length / 2; h > 0; h /= 2)
            {
                for (int i = h; i < length; i += 1)
                {
                    var tempValue = list[i];
                    double temp;
                    Vector3D.DistanceSquared(ref list[i].Position, ref weaponPos, out temp);

                    int j;
                    for (j = i; j >= h && Vector3D.DistanceSquared(list[j - h].Position, weaponPos) > temp; j -= h)
                    {
                        list[j] = list[j - h];
                    }

                    list[j] = tempValue;
                }
            }
        }

        internal List<Projectile> GetProCache()
        {
            if (LiveProjectileTick > _pCacheTick)
            {
                ProjetileCache.Clear();
                ProjetileCache.AddRange(LiveProjectile);
                _pCacheTick = LiveProjectileTick;
            }
            return ProjetileCache;
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

                var distanceFromCenters = Vector3D.Distance(MyGrid.PositionComp.WorldAABB.Center, target.PositionComp.WorldAABB.Center);
                distanceFromCenters -= MyGrid.PositionComp.LocalVolume.Radius;
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

                FatMap fatMap;
                if (Session.GridToFatMap.TryGetValue(grid, out fatMap))
                {
                    var blocks = fatMap.MyCubeBocks;
                    for (int i = 0; i < blocks.Count; i++)
                        FatBlockAdded(blocks[i]);
                }
            }
            AddSubGrids.Clear();

            foreach (var grid in RemSubGrids)
            {
                if (grid == MyGrid) continue;
                SubGrids.Remove(grid);
                grid.OnFatBlockAdded -= FatBlockAdded;
                grid.OnFatBlockRemoved -= FatBlockRemoved;
            }
            RemSubGrids.Clear();

            UpdateConstruct();
        }

        #region Power
        internal void InitFakeShipController()
        {
            FatMap fatMap;
            if (FakeShipController != null && Session.GridToFatMap.TryGetValue(MyGrid, out fatMap) && fatMap.MyCubeBocks.Count > 0)
            {
                FakeShipController.SlimBlock = fatMap.MyCubeBocks[0].SlimBlock;
                PowerDistributor = FakeShipController.GridResourceDistributor;
            }
        }

        internal void CleanUp()
        {
            RegisterMyGridEvents(false);
            foreach (var grid in SubGrids)
            {
                if (grid == MyGrid) continue;
                RemSubGrids.Add(grid);
            }
            AddSubGrids.Clear();
            SubGridChanges();
            SubGrids.Clear();
            Obstructions.Clear();
            TargetAis.Clear();
            EntitiesInRange.Clear();
            Batteries.Clear();
            Targets.Clear();
            SortedTargets.Clear();
            BlockGroups.Clear();
            Weapons.Clear();
            WeaponsIdx.Clear();
            WeaponBase.Clear();
            AmmoInventories.Clear();
            Inventories.Clear();
            LiveProjectile.Clear();
            DeadProjectiles.Clear();
            ControllingPlayers.Clear();
            SourceCount = 0;
            BlockCount = 0;
            MyOwner = 0;
            PointDefense = false;
            FadeOut = false;
            SupressMouseShoot = false;
            OverPowered = false;
            UpdatePowerSources = false;
            AvailablePowerChanged = false;
            PowerIncrease = false;
            RequestedPowerChanged = false;
            RequestIncrease = false;
            DbReady = false;
            Focus.Clean();
            MyShieldTmp = null;
            MyShield = null;
            MyPlanetTmp = null;
            MyPlanet = null;
            TerminalSystem = null;
            LastWeaponTerminal = null;
            LastTerminal = null;
            PowerDistributor = null;
        }
        internal void UpdateGridPower()
        {
            try
            {
                LastPowerUpdateTick = Session.Tick;
                GridAvailablePower = 0;
                GridMaxPower = 0;
                GridCurrentPower = 0;
                BatteryMaxPower = 0;
                BatteryCurrentOutput = 0;
                BatteryCurrentInput = 0;

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

                try
                {
                    using (FakeShipController.CubeGrid?.Pin())
                    {
                        if (FakeShipController.CubeGrid == null || FakeShipController.CubeGrid.MarkedForClose || FakeShipController.GridResourceDistributor == null || FakeShipController.GridResourceDistributor != PowerDistributor)
                        {
                            try
                            {
                                if (Weapons.Count > 0)
                                {
                                    var cube = Weapons[Weapons.Count - 1].MyCube;
                                    if (cube != null)
                                    {
                                        using (cube.Pin())
                                        {
                                            if (cube.MarkedForClose || cube.CubeGrid.MarkedForClose && cube.SlimBlock != null)
                                            {
                                                Log.Line($"powerDist cube is not ready");
                                                return;
                                            }
                                            FakeShipController.SlimBlock = cube.SlimBlock;
                                            PowerDistributor = FakeShipController.GridResourceDistributor;
                                            if (PowerDistributor == null)
                                                return;
                                        }
                                    }
                                    else
                                    {
                                        Log.Line($"powerDist cube is null");
                                        return;
                                    }
                                }
                                else return;
                            }
                            catch (Exception ex) { Log.Line($"Exception in UpdateGridPower: {ex} - probable null!"); }
                        }

                        if (PowerDistributor == null)
                        {
                            Log.Line($"powerDist is null - check 1");
                            return;
                        }

                        try
                        {
                            GridMaxPower = PowerDistributor.MaxAvailableResourceByType(GId);
                            GridCurrentPower = PowerDistributor.TotalRequiredInputByType(GId);
                        }
                        catch (Exception ex) { Log.Line($"Exception in UpdateGridPower: {ex} - impossible null!"); }
                    }
                }
                catch (Exception ex) { Log.Line($"Exception in UpdateGridPower: {ex} - unlikely null!"); }


                GridAvailablePower = GridMaxPower - GridCurrentPower;

                GridCurrentPower += BatteryCurrentInput;
                GridAvailablePower -= BatteryCurrentInput;
                UpdatePowerSources = false;

                RequestedPowerChanged = Math.Abs(LastRequestedPower - RequestedWeaponsDraw) > 0.001 && LastRequestedPower > 0;
                AvailablePowerChanged = Math.Abs(GridMaxPower - LastAvailablePower) > 0.001 && LastAvailablePower > 0;

                RequestIncrease = LastRequestedPower < RequestedWeaponsDraw;
                PowerIncrease = LastAvailablePower < GridMaxPower;

                LastAvailablePower = GridMaxPower;
                LastRequestedPower = RequestedWeaponsDraw;

                HadPower = HasPower;
                HasPower = GridMaxPower > 0;
                if (!WasPowered && HasPower) 
                    WasPowered = true;

                if (HasPower) return;
                if (HadPower)
                    WeaponShootOff();
            }
            catch (Exception ex) { Log.Line($"Exception in UpdateGridPower: {ex} - SessionNull{Session == null} - FakeShipControllerNull{FakeShipController == null} - PowerDistributorNull{PowerDistributor == null} - MyGridNull{MyGrid == null}"); }
        }

        private void WeaponShootOff()
        {

            for (int i = 0; i < Weapons.Count; i++)
            {
                var comp = Weapons[i];
                for (int x = 0; x < comp.Platform.Weapons.Length; x++)
                {
                    var w = comp.Platform.Weapons[x];
                    w.StopReloadSound();
                    w.StopShooting();
                }
            }
        }


        internal void TurnManualShootOff(bool isCallingGrid = true)
        {
            if (TurnOffManualTick == Session.Tick) return;

            TurnOffManualTick = Session.Tick;

            if (isCallingGrid)
            {
                foreach (var grid in SubGrids)
                {
                    GridAi ai;
                    if (grid != MyGrid && Session.GridTargetingAIs.TryGetValue(grid, out ai))
                        ai.TurnManualShootOff(false);
                }
            }

            foreach (var cubeComp in WeaponBase)
            {
                var comp = cubeComp.Value;
                if (comp?.Platform.State != MyWeaponPlatform.PlatformState.Ready) continue;
                var currPlayer = comp.State.Value.CurrentPlayerControl;

                if (currPlayer.PlayerId != Session.PlayerId) continue;

                var cState = comp.State.Value;
                var overRides = comp.Set.Value.Overrides;

                currPlayer.ControlType = ControlType.None;
                currPlayer.PlayerId = -1;

                if (comp.Session.MpActive)
                    comp.Session.SendControlingPlayer(comp);

                if (cState.ClickShoot)
                {
                    for (int i = 0; i < comp.Platform.Weapons.Length; i++)
                    {
                        var wState = cState.Weapons[comp.Platform.Weapons[i].WeaponId];

                        if (cState.ClickShoot)
                            wState.ManualShoot = Weapon.ManualShootActionState.ShootOff;

                    }

                    cState.ClickShoot = false;

                    if (comp.Session.MpActive)
                        comp.Session.SendActionShootUpdate(comp, Weapon.ManualShootActionState.ShootOff);
                }
                else if (overRides.TargetPainter || overRides.ManualControl)
                {
                    overRides.TargetPainter = false;
                    overRides.ManualControl = false;
                    if (comp.Session.MpActive)
                    {
                        comp.Session.SendOverRidesUpdate(comp, overRides);
                        comp.TrackReticle = false;
                        comp.Session.SendTrackReticleUpdate(comp);
                    }

                    GroupInfo group;
                    if (!string.IsNullOrEmpty(comp.State.Value.CurrentBlockGroup) && BlockGroups.TryGetValue(comp.State.Value.CurrentBlockGroup, out group))
                    {
                        GroupsToCheck.Add(group);
                        ScanBlockGroupSettings = true;
                    }
                }
            }
        }

        internal void CheckReload(object o = null)
        {
            var magId = (MyDefinitionId?)o ?? new MyDefinitionId();

            for (int i = 0; i < Weapons.Count; i++)
            {
                for(int j = 0; j < Weapons[i].Platform.Weapons.Length; j++)
                {
                    var w = Weapons[i].Platform.Weapons[j];
                    if (w.ActiveAmmoDef.AmmoDefinitionId == magId)
                        ComputeStorage(w);
                }
            }
        }

        #endregion
    }
}
