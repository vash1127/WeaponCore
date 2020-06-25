using System;
using ParallelTasks;
using Sandbox.ModAPI;
using VRageMath;
using WeaponCore.Support;
using WeaponCore.Platform;
using Sandbox.Game.Entities;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Game;
using Sandbox.Common.ObjectBuilders;
using VRage.Utils;
using System.Collections.Generic;
using Sandbox.Definitions;
using Sandbox.Game.Entities.Cube;
using System.Collections.Concurrent;
using VRage.Collections;

namespace WeaponCore
{
    public partial class Session
    {
        internal void Timings()
        {
            _paused = false;
            Tick++;
            Tick10 = Tick % 10 == 0;
            Tick20 = Tick % 20 == 0;
            Tick60 = Tick % 60 == 0;
            Tick120 = Tick % 120 == 0;
            Tick180 = Tick % 180 == 0;
            Tick300 = Tick % 300 == 0;
            Tick600 = Tick % 600 == 0;
            Tick1800 = Tick % 1800 == 0;
            Tick3600 = Tick % 3600 == 0;
            if (Tick60)
            {
                if (Av.ExplosionCounter - 5 >= 0) Av.ExplosionCounter -= 5;
                else Av.ExplosionCounter = 0;
            }
            if (++SCount == 60) SCount = 0;

            if (++AwakeCount == AwakeBuckets) AwakeCount = 0;
            if (++AsleepCount == AsleepBuckets) AsleepCount = 0;

            if (Count++ == 119)
            {
                Count = 0;
                UiBkOpacity = MyAPIGateway.Session.Config.UIBkOpacity;
                UiOpacity = MyAPIGateway.Session.Config.UIOpacity;
                CheckAdminRights();
                if (IsServer && MpActive && (AuthLogging || ConnectedAuthors.Count > 0)) AuthorDebug();
                
                if (PbActivate) Api.PbInit();
            }
            LCount++;
            if (LCount == 129)
                LCount = 0;

            if (!GameLoaded)
            {
                if (FirstLoop)
                {
                    if (!MiscLoaded)
                        MiscLoaded = true;

                    InitRayCast();

                    GameLoaded = true;                   
                    if (LocalVersion)
                        Log.Line($"Local WeaponCore Detected");
                }
                else if (!FirstLoop)
                {
                    FirstLoop = true;
                    foreach (var t in AllDefinitions)
                    {
                        var name = t.Id.SubtypeName;
                        var contains = name.Contains("BlockArmor");
                        if (contains)
                        {
                            AllArmorBaseDefinitions.Add(t);
                            if (name.Contains("HeavyBlockArmor")) HeavyArmorBaseDefinitions.Add(t);
                        }
                    }
                }
            }

            if (!PlayersLoaded && KeenFuckery())
                PlayersLoaded = true;

            if (ShieldMod && !ShieldApiLoaded && SApi.Load())
                ShieldApiLoaded = true;
        }

        internal void ProfilePerformance()
        {
            var netTime1 = DsUtil.GetValue("network1");
            var psTime = DsUtil.GetValue("ps");
            var piTIme = DsUtil.GetValue("pi");
            var pdTime = DsUtil.GetValue("pd");
            var paTime = DsUtil.GetValue("pa");

            var updateTime = DsUtil.GetValue("shoot");
            var drawTime = DsUtil.GetValue("draw");
            var av = DsUtil.GetValue("av");
            var db = DsUtil.GetValue("db");
            var ai = DsUtil.GetValue("ai");
            var charge = DsUtil.GetValue("charge");
            var acquire = DsUtil.GetValue("acquire");
            Log.LineShortDate($"(CPU-T) --- <AI>{ai.Median:0.0000}/{ai.Min:0.0000}/{ai.Max:0.0000} <Acq>{acquire.Median:0.0000}/{acquire.Min:0.0000}/{acquire.Max:0.0000} <SH>{updateTime.Median:0.0000}/{updateTime.Min:0.0000}/{updateTime.Max:0.0000} <CH>{charge.Median:0.0000}/{charge.Min:0.0000}/{charge.Max:0.0000} <PS>{psTime.Median:0.0000}/{psTime.Min:0.0000}/{psTime.Max:0.0000} <PI>{piTIme.Median:0.0000}/{piTIme.Min:0.0000}/{piTIme.Max:0.0000} <PD>{pdTime.Median:0.0000}/{pdTime.Min:0.0000}/{pdTime.Max:0.0000} <PA>{paTime.Median:0.0000}/{paTime.Min:0.0000}/{paTime.Max:0.0000} <DR>{drawTime.Median:0.0000}/{drawTime.Min:0.0000}/{drawTime.Max:0.0000} <AV>{av.Median:0.0000}/{av.Min:0.0000}/{av.Max:0.0000} <NET1>{netTime1.Median:0.0000}/{netTime1.Min:0.0000}/{netTime1.Max:0.0000}> <DB>{db.Median:0.0000}/{db.Min:0.0000}/{db.Max:0.0000}>", "perf");
            Log.LineShortDate($"(STATS) -------- AiReq:[{TargetRequests}] Targ:[{TargetChecks}] Bloc:[{BlockChecks}] Aim:[{CanShoot}] CCast:[{ClosestRayCasts}] RndCast[{RandomRayCasts}] TopCast[{TopRayCasts}]", "stats");
            TargetRequests = 0;
            TargetChecks = 0;
            BlockChecks = 0;
            CanShoot = 0;
            ClosestRayCasts = 0;
            RandomRayCasts = 0;
            TopRayCasts = 0;
            TargetTransfers = 0;
            TargetSets = 0;
            TargetResets = 0;
            AmmoMoveTriggered = 0;
            AmmoPulls = 0;
            Load = 0d;
            DsUtil.Clean();
        }

        internal void NetReport()
        {
            foreach (var reports in Reporter.ReportData)
            {
                var typeStr = reports.Key.ToString();
                var reportList = reports.Value;
                int clientReceivers = 0;
                int serverReceivers = 0;
                int noneReceivers = 0;
                int validPackets = 0;
                int invalidPackets = 0;
                ulong dataTransfer = 0;
                foreach (var report in reportList)
                {
                    if (report.PacketValid) validPackets++;
                    else invalidPackets++;

                    if (report.Receiver == NetworkReporter.Report.Received.None) noneReceivers++;
                    else if (report.Receiver == NetworkReporter.Report.Received.Server) serverReceivers++;
                    else clientReceivers++;

                    dataTransfer += (uint)report.PacketSize;
                    Reporter.ReportPool.Return(report);
                }
                var packetCount = reports.Value.Count;
                if (packetCount > 0) Log.LineShortDate($"(NINFO) - <{typeStr}> packets:[{packetCount}] dataTransfer:[{dataTransfer}] validPackets:[{validPackets}] invalidPackets:[{invalidPackets}] serverReceive:[{serverReceivers}({IsServer})] clientReceive:[{clientReceivers}] unknownReceive:[{noneReceivers}]", "net");
            }

            foreach (var list in Reporter.ReportData.Values)
                list.Clear();
        }

        internal int ShortLoadAssigner()
        {
            if (_shortLoadCounter + 1 > 59) _shortLoadCounter = 0;
            else ++_shortLoadCounter;

            return _shortLoadCounter;
        }

        internal int LoadAssigner()
        {
            if (_loadCounter + 1 > 119) _loadCounter = 0;
            else ++_loadCounter;

            return _loadCounter;
        }

        internal List<MyLineSegmentOverlapResult<MyEntity>> AimRayEnts = new List<MyLineSegmentOverlapResult<MyEntity>>();
        internal bool GetAimedAtBlock(out MyCubeBlock cube)
        {
            cube = null;
            if (UiInput.AimRay.Length > 0)
            {
                AimRayEnts.Clear();
                MyGamePruningStructure.GetTopmostEntitiesOverlappingRay(ref UiInput.AimRay, AimRayEnts);
                foreach (var ent in AimRayEnts)
                {
                    var grid = ent.Element as MyCubeGrid;
                    if (grid?.Physics != null && grid.Physics.Enabled && !grid.Physics.IsPhantom && grid.InScene && !grid.IsPreview)
                    {
                        MyCube myCube;
                        var hitV3I = grid.RayCastBlocks(UiInput.AimRay.From, UiInput.AimRay.To);
                        if (hitV3I.HasValue && grid.TryGetCube(hitV3I.Value, out myCube)) {

                            var slim = (IMySlimBlock)myCube.CubeBlock;
                            if (slim.FatBlock != null) {
                                cube = (MyCubeBlock)slim.FatBlock;
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }

        private void CheckAdminRights()
        {
            const string spider = "Space_spider";
            const string wolf = "SpaceWolf";
            foreach (var item in Players) {

                var pLevel = item.Value.PromoteLevel;
                var playerId = item.Key;
                var player = item.Value;
                var wasAdmin = Admins.ContainsKey(playerId);

                if (pLevel == MyPromoteLevel.Admin || pLevel == MyPromoteLevel.Owner || pLevel == MyPromoteLevel.SpaceMaster) {

                    var character = player.Character;
                    var isAdmin = false;
                    if (character != null ) {
                        if (character.Definition.Id.SubtypeName.Equals(wolf) || character.Definition.Id.SubtypeName.StartsWith(spider)) continue;

                        if (MySafeZone.CheckAdminIgnoreSafezones(player.SteamUserId))
                            isAdmin = true;
                        else {

                            foreach (var gridAi in GridTargetingAIs.Values) {

                                if (gridAi.Targets.ContainsKey((MyEntity)character) && gridAi.Weapons.Count > 0 && ((IMyTerminalBlock)gridAi.Weapons[0].MyCube).HasPlayerAccess(playerId)) {

                                    if (MyIDModule.GetRelationPlayerBlock(playerId, gridAi.MyOwner) == MyRelationsBetweenPlayerAndBlock.Enemies) {
                                        isAdmin = true;
                                        break;
                                    }
                                }
                            }
                        }

                        if (isAdmin) {
                            Admins[playerId] = character;
                            AdminMap[character] = player;
                            continue;
                        }
                    }
                }

                if (wasAdmin)
                {
                    IMyCharacter removeCharacter;
                    IMyPlayer removePlayer;
                    Admins.TryRemove(playerId, out removeCharacter);
                    AdminMap.TryRemove(removeCharacter, out removePlayer);
                }
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


        internal void InitRayCast()
        {
            List<IHitInfo> tmpList = new List<IHitInfo>();
            MyAPIGateway.Physics.CastRay(new Vector3D { X = 10, Y = 10, Z = 10 }, new Vector3D { X = -10, Y = -10, Z = -10 }, tmpList);
        }

        //threaded
        internal void ProccessAmmoMoves()
        {
            foreach (var inventoryItems in InventoryItems)
            {
                for (int i = 0; i < inventoryItems.Value.Count; i++)
                {
                    if (AmmoDefIds.Contains(inventoryItems.Value[i].Content.GetId()))
                    {
                        var newItem = BetterInventoryItems.Count > 0 ? BetterInventoryItems.Pop() : new BetterInventoryItem();
                        newItem.ItemId = inventoryItems.Value[i].ItemId;
                        newItem.Amount = (int)inventoryItems.Value[i].Amount;
                        newItem.Content = inventoryItems.Value[i].Content;
                        AmmoThreadItemList[inventoryItems.Key].Add(newItem);
                    }
                }
                inventoryItems.Value.Clear();
            }

            foreach (var blockInventoryItems in BlockInventoryItems)
            {
                foreach (var itemList in blockInventoryItems.Value)
                {
                    var newItem = BetterInventoryItems.Count > 0 ? BetterInventoryItems.Pop() : new BetterInventoryItem();
                    newItem.ItemId = itemList.Value.ItemId;
                    newItem.Amount = itemList.Value.Amount;
                    newItem.Content = itemList.Value.Content;
                    AmmoThreadItemList[blockInventoryItems.Key].Add(newItem);
                }
            }

            AmmoToRemove();
            AmmoPull();

            foreach (var itemList in AmmoThreadItemList)
            {
                for (int i = 0; i < itemList.Value.Count; i++)
                    BetterInventoryItems.Push(itemList.Value[i]);

                itemList.Value.Clear();
            }
        }

        internal void ProccessAmmoCallback()
        {
            RemoveAmmo();
            MoveAmmo();
        }

        internal void StartAmmoTask()
        {
            if (ITask.valid && ITask.Exceptions != null)
                TaskHasErrors(ref ITask, "ITask");

            for (int i = GridsToUpdateInvetories.Count - 1; i >= 0; i--)
            {
                for (int j = 0; j < GridsToUpdateInvetories[i].Inventories.Count; j++)
                {
                    var inventory = GridsToUpdateInvetories[i].Inventories[j];                 
                    InventoryItems[inventory].AddRange(inventory.GetItems());
                }
            }

            DefIdsComparer.Clear();
            GridsToUpdateInvetories.Clear();
            GridsToUpdateInvetoriesIndexer.Clear();

            ITask = MyAPIGateway.Parallel.StartBackground(ProccessAmmoMoves, ProccessAmmoCallback);
        }

        //Would use DSUnique but to many profiler hits
        internal bool UniqueListRemove<T>(T item, IDictionary<T, int> indexer, IList<T> list)
        {
            int oldPos;
            if (indexer.TryGetValue(item, out oldPos))
            {

                indexer.Remove(item);
                list.RemoveAtFast(oldPos);
                var count = list.Count;
                if (count > 0)
                {
                    count--;
                    if (oldPos <= count)
                        indexer[list[oldPos]] = oldPos;
                    else
                        indexer[list[count]] = count;
                }

                return true;
            }
            return false;
        }

        internal bool UniqueListAdd<T>(T item, IDictionary<T, int> indexer, IList<T> list)
        {
            if (indexer.ContainsKey(item)) return false;

            list.Add(item);
            indexer.Add(item, list.Count - 1);
            return true;
        }

        internal void RemoveCoreToolbarWeapons(MyCubeGrid grid)
        {
            foreach (var cube in grid.GetFatBlocks())
            {
                if (cube is MyShipController)
                {
                    var ob = (MyObjectBuilder_ShipController)cube.GetObjectBuilderCubeBlock();
                    var reinit = false;
                    for (int i = 0; i < ob.Toolbar.Slots.Count; i++)
                    {
                        var toolbarItem = ob.Toolbar.Slots[i].Data as MyObjectBuilder_ToolbarItemWeapon;
                        if (toolbarItem != null)
                        {
                            var defId = (MyDefinitionId)toolbarItem.defId;
                            if ((ReplaceVanilla && VanillaIds.ContainsKey(defId)) || WeaponPlatforms.ContainsKey(defId.SubtypeId))
                            {
                                var index = ob.Toolbar.Slots[i].Index;
                                var item = ob.Toolbar.Slots[i].Item;
                                ob.Toolbar.Slots[i] = new MyObjectBuilder_Toolbar.Slot { Index = index, Item = item };
                                reinit = true;
                            }
                        }
                    }

                    if (reinit)
                        cube.Init(ob, grid);
                }
            }
        }


        internal bool KeenFuckery()
        {
            try
            {
                if (HandlesInput)
                {
                    if (Session?.Player == null) return false;
                    MultiplayerId = MyAPIGateway.Multiplayer.MyId;
                    PlayerId = Session.Player.IdentityId;

                    List<IMyPlayer> players = new List<IMyPlayer>();
                    MyAPIGateway.Multiplayer.Players.GetPlayers(players);

                    for (int i = 0; i < players.Count; i++)
                        PlayerConnected(players[i].IdentityId);

                    PlayerMouseStates[PlayerId] = UiInput.ClientInputState;

                    if (IsClient)
                        SendUpdateRequest(-1, PacketType.RequestMouseStates);
                }

                return true;
            }
            catch (Exception ex) { Log.Line($"Exception in UpdatingStopped: {ex} - Session:{Session != null} - Player:{Session?.Player != null} - ClientMouseState:{UiInput.ClientInputState != null}"); }

            return false;
        }

        internal void ReallyStupidKeenShit() //aka block group removal of individual blocks
        {
            var categories = MyDefinitionManager.Static.GetCategories();

            var removeDefs = new HashSet<MyDefinitionId>(MyDefinitionId.Comparer);
            var keepDefs = new HashSet<string>();

            foreach (var weaponDef in WeaponDefinitions)
            {
                foreach (var mount in weaponDef.Assignments.MountPoints)
                {
                    var subTypeId = mount.SubtypeId;

                    MyDefinitionId defId = new MyDefinitionId();

                    if ((ReplaceVanilla && VanillaCoreIds.TryGetValue(MyStringHash.GetOrCompute(subTypeId), out defId)))
                        removeDefs.Add(defId);
                    else
                    {
                        foreach (var def in AllDefinitions)
                        {
                            if (def.Id.SubtypeName == subTypeId)
                            {
                                removeDefs.Add(def.Id);
                                break;
                            }
                        }
                    }
                }
            }

            foreach (var def in AllDefinitions)
                if (!removeDefs.Contains(def.Id))
                    keepDefs.Add(def.Id.SubtypeName);

            foreach (var category in categories)
            {
                if (category.Value.IsShipCategory)
                {
                    var removeList = new List<string>();
                    foreach (var item in category.Value.ItemIds)
                    {
                        foreach (var def in removeDefs)
                        {
                            var type = def.TypeId.ToString().Replace("MyObjectBuilder_", "");
                            var subType = string.IsNullOrEmpty(def.SubtypeName) ? "(null)" : def.SubtypeName;

                            if ((item.Contains(type) || item.Contains(subType)))
                                removeList.Add(item);
                        }
                    }

                    foreach (var keep in keepDefs)
                    {
                        for (int i = 0; i < removeList.Count; i++)
                        {
                            var toRemove = removeList[i];
                            if (!string.IsNullOrEmpty(keep) && toRemove.EndsWith(keep))
                                removeList.RemoveAtFast(i);
                        }
                    }

                    for (int i = 0; i < removeList.Count; i++)
                        category.Value.ItemIds.Remove(removeList[i]);
                }
            }
        }

        internal static double ModRadius(double radius, bool largeBlock)
        {
            if (largeBlock && radius < 3) radius = 3;
            else if (largeBlock && radius > 25) radius = 25;
            else if (!largeBlock && radius > 5) radius = 5;

            radius = Math.Ceiling(radius);
            return radius;
        }

        public void WeaponDebug(Weapon w)
        {
            DsDebugDraw.DrawLine(w.MyPivotTestLine, Color.Red, 0.05f);
            DsDebugDraw.DrawLine(w.MyBarrelTestLine, Color.Blue, 0.05f);
            DsDebugDraw.DrawLine(w.MyCenterTestLine, Color.Green, 0.05f);
            DsDebugDraw.DrawLine(w.MyAimTestLine, Color.Black, 0.07f);
            DsDebugDraw.DrawSingleVec(w.MyPivotPos, 1f, Color.White);
            //DsDebugDraw.DrawBox(w.targetBox, Color.Plum);
            DsDebugDraw.DrawLine(w.LimitLine.From, w.LimitLine.To, Color.Orange, 0.05f);

            if (w.Target.HasTarget)
                DsDebugDraw.DrawLine(w.MyShootAlignmentLine, Color.Yellow, 0.05f);
        }

        public string ModPath()
        {
            var modPath = ModContext.ModPath;
            return modPath;
        }

        private void Paused()
        {
            _paused = true;
            if (Wheel.WheelActive && Wheel.Ai != null) Wheel.CloseWheel();
        }

        public bool TaskHasErrors(ref Task task, string taskName)
        {
            if (task.Exceptions != null && task.Exceptions.Length > 0)
            {
                foreach (var e in task.Exceptions)
                {
                    Log.Line($"{taskName} thread!\n{e}");
                }

                return true;
            }

            return false;
        }

        internal MyEntity TriggerEntityActivator()
        {
            var ent = new MyEntity();
            ent.Init(null, TriggerEntityModel, null, null, null);
            ent.Render.CastShadows = false;
            ent.IsPreview = true;
            ent.Save = false;
            ent.SyncFlag = false;
            ent.NeedsWorldMatrix = false;
            ent.Flags |= EntityFlags.IsNotGamePrunningStructureObject;
            MyEntities.Add(ent);

            ent.PositionComp.SetWorldMatrix(ref MatrixD.Identity, null, false, false, false);
            ent.InScene = false;
            ent.Render.RemoveRenderObjects();
            return ent;
        }

        internal void TriggerEntityClear(MyEntity myEntity)
        {
            myEntity.PositionComp.SetWorldMatrix(ref MatrixD.Identity, null, false, false, false);
            myEntity.InScene = false;
            myEntity.Render.RemoveRenderObjects();
        }

        internal void LoadVanillaData()
        {
            var smallMissileId = new MyDefinitionId(typeof(MyObjectBuilder_SmallMissileLauncher), null);
            var smallGatId = new MyDefinitionId(typeof(MyObjectBuilder_SmallGatlingGun), null);
            var largeGatId = new MyDefinitionId(typeof(MyObjectBuilder_LargeGatlingTurret), null);
            var largeMissileId = new MyDefinitionId(typeof(MyObjectBuilder_LargeMissileTurret), null);

            var smallMissileHash = MyStringHash.GetOrCompute("SmallMissileLauncher");
            var smallGatHash = MyStringHash.GetOrCompute("SmallGatlingGun");
            var largeGatHash = MyStringHash.GetOrCompute("LargeGatlingTurret");
            var smallGatTurretHash = MyStringHash.GetOrCompute("SmallGatlingTurret");
            var largeMissileHash = MyStringHash.GetOrCompute("LargeMissileTurret");

            VanillaIds[smallMissileId] = smallMissileHash;
            VanillaIds[smallGatId] = smallGatHash;
            VanillaIds[largeGatId] = largeGatHash;
            VanillaIds[largeMissileId] = largeMissileHash;

            VanillaCoreIds[smallMissileHash] = smallMissileId;
            VanillaCoreIds[smallGatHash] = smallGatId;
            VanillaCoreIds[largeGatHash] = largeGatId;
            VanillaCoreIds[largeMissileHash] = largeMissileId;

            VanillaSubpartNames.Add("InteriorTurretBase1");
            VanillaSubpartNames.Add("InteriorTurretBase2");
            VanillaSubpartNames.Add("MissileTurretBase1");
            VanillaSubpartNames.Add("MissileTurretBarrels");
            VanillaSubpartNames.Add("GatlingTurretBase1");
            VanillaSubpartNames.Add("GatlingTurretBase2");
            VanillaSubpartNames.Add("GatlingBarrel");
        }
    }
}