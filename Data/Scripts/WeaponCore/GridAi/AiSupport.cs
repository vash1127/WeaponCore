using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
using WeaponCore.Projectiles;
using static WeaponCore.Support.TargetingDefinition;
namespace WeaponCore.Support
{
    public partial class GridAi
    {
        internal void RequestDbUpdate()
        {
            GridCenter = MyGrid.PositionComp.WorldAABB.Center;
            GridRadius = MyGrid.PositionComp.LocalVolume.Radius;
            Session.DbsToUpdate.Add(this);
            TargetsUpdatedTick = Session.Tick;
        }

        internal void ReScanBlockGroups()
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

        public static bool GridEnemy(long gridOwner, MyCubeGrid grid, List<long> owners = null)
        {
            if (owners == null) owners = grid.BigOwners;
            if (owners.Count == 0) return true;
            var relationship = MyIDModule.GetRelationPlayerBlock(gridOwner, owners[0], MyOwnershipShareModeEnum.Faction);
            var enemy = relationship != MyRelationsBetweenPlayerAndBlock.Owner && relationship != MyRelationsBetweenPlayerAndBlock.FactionShare;
            return enemy;
        }

        private static int[] GetDeck(ref int[] deck, ref int prevDeckLen, int firstCard, int cardsToSort, int cardsToShuffle)
        {
            var count = cardsToSort - firstCard;
            if (prevDeckLen != count)
            {
                Array.Resize(ref deck, count);
                prevDeckLen = count;
            }

            for (int i = 0; i < count; i++)
            {
                var j = i < cardsToShuffle ? MyUtils.GetRandomInt(0, i + 1) : i;

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
                IMyPlayer player;
                Session.Players.TryGetValue(playerId, out player);
                if (player != null && Session.Session.IsUserAdmin(player.SteamUserId)) return false;

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
                entInfo = new Sandbox.ModAPI.Ingame.MyDetectedEntityInfo(entity.EntityId, string.Empty, type, null, MatrixD.Zero, Vector3.Zero, MyRelationsBetweenPlayerAndBlock.Enemies, new BoundingBoxD(), Session.Tick);
                return true;
            }
            entInfo = new Sandbox.ModAPI.Ingame.MyDetectedEntityInfo();
            return false;
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

                FatMap fatMap;
                if (Session.GridToFatMap.TryGetValue(grid, out fatMap))
                {
                    var blocks = fatMap.MyCubeBocks;
                    for (int i = 0; i < blocks.Count; i++)
                        FatBlockAdded(blocks[i]);
                }
                else Log.Line($"AddSubGrids fatmap didnt exist for: {grid.DebugName}");
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
        }

        #region Power
        internal void InitFakeShipController()
        {
            FatMap fatMap;
            if (FakeShipController != null && Session.GridToFatMap.TryGetValue(MyGrid, out fatMap) && fatMap.MyCubeBocks.Count > 0)
                FakeShipController.SlimBlock = fatMap.MyCubeBocks[0].SlimBlock;
        }

        internal void DelayedGridCleanUp(object o)
        {
            foreach (var grid in SubGrids)
            {
                if (grid == MyGrid) continue;
                RemSubGrids.Add(grid);
            }
            AddSubGrids.Clear();
            SubGridChanges();
            SubGrids.Clear();
            Gunners.Clear();
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

            Focus.Clean();
            MyShieldTmp = null;
            MyShield = null;
            MyPlanetTmp = null;
            MyPlanet = null;
            FakeShipController = null;
            TerminalSystem = null;
        }

        internal void UpdateGridPower()
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

            if (FakeShipController.GridResourceDistributor == null)
            {
                FatMap fatMap;
                if (Session.GridToFatMap.TryGetValue(MyGrid, out fatMap) && fatMap.MyCubeBocks.Count > 0)
                {
                    FakeShipController.SlimBlock = fatMap.MyCubeBocks[0].SlimBlock;
                    if (FakeShipController.GridResourceDistributor == null) return;
                }
                else return;
            }

            GridMaxPower = FakeShipController.GridResourceDistributor.MaxAvailableResourceByType(GId);
            GridCurrentPower = FakeShipController.GridResourceDistributor.TotalRequiredInputByType(GId);
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

            if (HasPower) return;
            if (HadPower)
                WeaponShootOff();
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


        internal void TurnMouseShootOff()
        {
            foreach (var cubeComp in WeaponBase)
            {
                var comp = cubeComp.Value;   
                if (comp?.Platform.State != MyWeaponPlatform.PlatformState.Ready) return;
                for (int i = 0; i < comp.Platform.Weapons.Length; i++)
                {
                    var wState = comp.State.Value.Weapons[comp.Platform.Weapons[i].WeaponId];

                    if (wState.ManualShoot == Weapon.TerminalActionState.ShootClick)
                    {
                        comp.MouseShoot = false;
                        wState.ManualShoot = Weapon.TerminalActionState.ShootOff;
                        ManualComps = ManualComps - 1 > 0 ? ManualComps - 1 : 0;
                        comp.Shooting = comp.Shooting - 1 > 0 ? comp.Shooting - 1 : 0;
                    }

                }
            }
        }

        internal class AiTargetingInfo
        {
            internal bool TargetInRange;
            internal double ThreatRangeSqr;

            internal bool ValidTargetExists(Weapon w)
            {
                var comp = w.Comp;
                var ai = comp.Ai;


                var weaponRangeSqr = comp.Set.Value.Range * comp.Set.Value.Range;

                return ThreatRangeSqr <= weaponRangeSqr || ai.Focus.HasFocus;
            }

            internal void Clean()
            {
                ThreatRangeSqr = double.MaxValue;
                TargetInRange = false;
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

        internal class WeaponCount
        {
            internal int Current;
            internal int Max;
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

                var targetDist = Vector3D.Distance(myAi.GridCenter, TargetPos) - TargetRadius;
                targetDist -= myAi.GridRadius;
                if (targetDist < 0) targetDist = 0;
                DistSqr = targetDist * targetDist;
            }
        }

        #endregion
    }
}
