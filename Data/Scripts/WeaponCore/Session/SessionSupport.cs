﻿using ParallelTasks;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using Sandbox.Game.EntityComponents;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Input;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;
using WeaponCore.Platform;
using WeaponCore.Settings;
using WeaponCore.Support;

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
            if (++QCount == 15) QCount = 0;
            if (++AwakeCount == AwakeBuckets) AwakeCount = 0;
            if (++AsleepCount == AsleepBuckets) AsleepCount = 0;

            if (Count++ == 119)
            {
                Count = 0;
                UiBkOpacity = MyAPIGateway.Session.Config.UIBkOpacity;
                UiOpacity = MyAPIGateway.Session.Config.UIOpacity;
                UIHudOpacity = MyAPIGateway.Session.Config.HUDBkOpacity;
                CheckAdminRights();
                if (IsServer && MpActive && (AuthLogging || ConnectedAuthors.Count > 0)) AuthorDebug();
                
                if (IsServer && PbActivate && !PbApiInited) Api.PbInit();

                if (HandlesInput && !ClientCheck && Tick > 1200)
                {
                    if (IsClient)
                    {
                        if (ServerVersion != ModContext.ModName)
                        {
                            var message = $"::WeaponCore Version Mismatch::    Server:{ServerVersion} - Client:{ModContext.ModName} -   Unexpected behavior may occur.";
                            MyAPIGateway.Utilities.ShowNotification(message, 10000, "Red");
                        }
                    }

                    if (!string.IsNullOrEmpty(PlayerMessage))
                        MyAPIGateway.Utilities.ShowNotification(PlayerMessage, 10000, "White");

                    ClientCheck = true;
                }

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
                        if (name.Contains("Armor"))
                        {
                            var normalArmor = name.Contains("ArmorBlock") || name.Contains("HeavyArmor") || name.StartsWith("LargeRoundArmor") || name.Contains("BlockArmor");
                            var blast = !normalArmor && (name == "ArmorCenter" || name == "ArmorCorner" || name == "ArmorInvCorner" || name == "ArmorSide" || name.StartsWith("SmallArmor"));
                            if (normalArmor || blast)
                            {
                                AllArmorBaseDefinitions.Add(t);
                                if (blast || name.Contains("Heavy")) HeavyArmorBaseDefinitions.Add(t);
                            }
                        }
                        else if (name.StartsWith("DSControl") || name.StartsWith("NPCControl") || name.StartsWith("Emitter") ||  name.StartsWith("NPCEmitter"))
                        {
                            CoreShieldBlockTypes.Add(t);
                        }
                    }
                }
            }

            if (!PlayersLoaded && KeenFuckery())
                PlayersLoaded = true;
        }

        internal void AddLosCheck(LosDebug debug)
        {
            //if (!WeaponLosDebugActive.Add(debug.Weapon))
                //return;
            
            LosDebugList.Add(debug);
        }
        
        internal void LosDebuging()
        {
            for (var i = LosDebugList.Count - 1; i >= 0; i--)
            {
                var info = LosDebugList[i];
                DsDebugDraw.DrawLine(info.Line, Color.Red, 0.15f);

                if (Tick - info.HitTick > 1200)
                {
                    LosDebugList.RemoveAtFast(i);
                    WeaponLosDebugActive.Remove(info.Weapon);
                }
            }
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
            Log.LineShortDate($"(STATS) -------- AIs:[{GridTargetingAIs.Count}] - WcBlocks:[{IdToCompMap.Count}] - AiReq:[{TargetRequests}] Targ:[{TargetChecks}] Bloc:[{BlockChecks}] Aim:[{CanShoot}] CCast:[{ClosestRayCasts}] RndCast[{RandomRayCasts}] TopCast[{TopRayCasts}]", "stats");
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
            if (_shortLoadCounter + 1 > 14) _shortLoadCounter = 0;
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

                                    if (MyIDModule.GetRelationPlayerBlock(playerId, gridAi.AiOwner) == MyRelationsBetweenPlayerAndBlock.Enemies) {
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


        internal void UpdateHomingWeapons()
        {
            for (int i = HomingWeapons.Count - 1; i >= 0; i--)
            {
                var w = HomingWeapons[i];
                var comp = w.Comp;
                if (w.Comp.Ai == null || comp.Ai.MyGrid.MarkedForClose || comp.Ai.Concealed || comp.MyCube.MarkedForClose || !comp.MyCube.IsFunctional) {
                    HomingWeapons.RemoveAtFast(i);
                    continue;
                }

                w.TurretHomePosition();

                if (w.IsHome || !w.ReturingHome)
                    HomingWeapons.RemoveAtFast(i);
            }
        }


        internal void CleanSounds(bool force = false)
        {
            for (int i = SoundsToClean.Count - 1; i >= 0; i--)
            {
                var sound = SoundsToClean[i];
                var age = Tick - sound.SpawnTick;
                if (force || age > 4 && (sound.Force || !sound.Emitter.IsPlaying) || sound.Hit && sound.Emitter.Loop && age > 180)
                {
                    if (sound.Force || sound.Emitter.Loop) {
                        sound.Emitter.StopSound(sound.Emitter.Loop);
                    }
                    sound.Emitter.Entity = null;
                    sound.EmitterPool.Push(sound.Emitter);
                    sound.SoundPairPool.Push(sound.SoundPair);
                    SoundsToClean.RemoveAtFast(i);
                }
            }
        }

        private void UpdateControlKeys()
        {
            if (ControlRequest == ControlQuery.Info)
            {

                MyAPIGateway.Input.GetListOfPressedKeys(_pressedKeys);
                if (_pressedKeys.Count > 0 && _pressedKeys[0] != MyKeys.Enter)
                {

                    var firstKey = _pressedKeys[0];
                    Settings.ClientConfig.InfoKey = firstKey.ToString();
                    UiInput.InfoKey = firstKey;
                    ControlRequest = ControlQuery.None;
                    Settings.VersionControl.UpdateClientCfgFile();
                    MyAPIGateway.Utilities.ShowNotification($"{firstKey.ToString()} is now the WeaponCore Info Key", 10000);
                }
            }
            else if (ControlRequest == ControlQuery.Action)
            {

                MyAPIGateway.Input.GetListOfPressedKeys(_pressedKeys);
                if (_pressedKeys.Count > 0 && _pressedKeys[0] != MyKeys.Enter)
                {

                    var firstKey = _pressedKeys[0];
                    Settings.ClientConfig.ActionKey = firstKey.ToString();
                    UiInput.ActionKey = firstKey;
                    ControlRequest = ControlQuery.None;
                    Settings.VersionControl.UpdateClientCfgFile();
                    MyAPIGateway.Utilities.ShowNotification($"{firstKey.ToString()} is now the WeaponCore Action Key", 10000);
                }
            }
            else if (ControlRequest == ControlQuery.Keyboard) {

                MyAPIGateway.Input.GetListOfPressedKeys(_pressedKeys);
                if (_pressedKeys.Count > 0 && _pressedKeys[0] != MyKeys.Enter) {

                    var firstKey = _pressedKeys[0];
                    Settings.ClientConfig.ControlKey = firstKey.ToString();
                    UiInput.ControlKey = firstKey;
                    ControlRequest = ControlQuery.None;
                    Settings.VersionControl.UpdateClientCfgFile();
                    MyAPIGateway.Utilities.ShowNotification($"{firstKey.ToString()} is now the WeaponCore Control Key", 10000);
                }
            }
            else if (ControlRequest == ControlQuery.Mouse) {

                MyAPIGateway.Input.GetListOfPressedMouseButtons(_pressedButtons);
                if (_pressedButtons.Count > 0) {

                    var firstButton = _pressedButtons[0];
                    var invalidButtons = firstButton == MyMouseButtonsEnum.Left || firstButton == MyMouseButtonsEnum.Right || firstButton == MyMouseButtonsEnum.None;

                    if (!invalidButtons)
                    {
                        Settings.ClientConfig.MenuButton = firstButton.ToString();
                        UiInput.MouseButtonMenu = firstButton;
                        Settings.VersionControl.UpdateClientCfgFile();
                        MyAPIGateway.Utilities.ShowNotification($"{firstButton.ToString()}MouseButton will now open and close the WeaponCore Menu", 10000);
                    }
                    else MyAPIGateway.Utilities.ShowNotification($"{firstButton.ToString()}Button is an invalid mouse button for this function", 10000);
                    ControlRequest = ControlQuery.None;
                }
            }
            _pressedKeys.Clear();
            _pressedButtons.Clear();
        }

        private void ChatMessageSet(string message, ref bool sendToOthers)
        {
            var somethingUpdated = false;

            if (message == "/wc" || message.StartsWith("/wc "))
            {
                switch (message)
                {

                    case "/wc remap keyboard":
                        ControlRequest = ControlQuery.Keyboard;
                        somethingUpdated = true;
                        MyAPIGateway.Utilities.ShowNotification($"Press the key you want to use for the WeaponCore Control key", 10000);
                        break;
                    case "/wc remap mouse":
                        ControlRequest = ControlQuery.Mouse;
                        somethingUpdated = true;
                        MyAPIGateway.Utilities.ShowNotification($"Press the mouse button you want to use to open and close the WeaponCore Menu", 10000);
                        break;
                    case "/wc remap action":
                        ControlRequest = ControlQuery.Action;
                        somethingUpdated = true;
                        MyAPIGateway.Utilities.ShowNotification($"Press the key you want to use for the WeaponCore Action key", 10000);
                        break;
                    case "/wc remap info":
                        ControlRequest = ControlQuery.Info;
                        somethingUpdated = true;
                        MyAPIGateway.Utilities.ShowNotification($"Press the key you want to use for the WeaponCore Info key", 10000);
                        break;
                }

                if (ControlRequest == ControlQuery.None) {

                    string[] tokens = message.Split(' ');

                    var tokenLength = tokens.Length;
                    if (tokenLength > 1)
                    {
                        switch (tokens[1])
                        {
                            case "drawlimit":
                            {
                                int maxDrawCount;
                                if (tokenLength > 2 && int.TryParse(tokens[2], out maxDrawCount))
                                {
                                    Settings.ClientConfig.MaxProjectiles = maxDrawCount;
                                    var enabled = maxDrawCount != 0;
                                    Settings.ClientConfig.ClientOptimizations = enabled;
                                    somethingUpdated = true;
                                    MyAPIGateway.Utilities.ShowNotification($"The maximum onscreen projectiles is now set to {maxDrawCount} and is Enabled:{enabled}", 10000);
                                    Settings.VersionControl.UpdateClientCfgFile();
                                }

                                break;
                            }
                            case "shipsizes":
                                Settings.ClientConfig.ShowHudTargetSizes = !Settings.ClientConfig.ShowHudTargetSizes;
                                somethingUpdated = true;
                                MyAPIGateway.Utilities.ShowNotification($"Shipsize icons have been set to: {Settings.ClientConfig.ShowHudTargetSizes}", 10000);
                                Settings.VersionControl.UpdateClientCfgFile();
                                FovChanged();
                                break;
                            case "changehud":
                                CanChangeHud = !CanChangeHud;
                                somethingUpdated = true;
                                MyAPIGateway.Utilities.ShowNotification($"Modify Hud set to: {CanChangeHud}", 10000);
                                break;
                            case "setdefaults":
                                Settings.ClientConfig = new CoreSettings.ClientSettings();
                                somethingUpdated = true;
                                MyAPIGateway.Utilities.ShowNotification($"Client configuration has been set to defaults", 10000);
                                Settings.VersionControl.UpdateClientCfgFile();
                                break;
                        }
                    }
                }

                if (!somethingUpdated)
                {
                    if (message.Length <= 3)
                        MyAPIGateway.Utilities.ShowNotification("Valid WeaponCore Commands:\n'/wc remap -- Remap keys'\n'/wc drawlimit 1000' -- Limits total number of projectiles on screen (default unlimited)\n'/wc changehud' to enable moving/resizing of WC Hud\n'/wc setdefaults' -- Resets shield client configs to default values\n", 10000);
                    else if (message.StartsWith("/wc remap"))
                        MyAPIGateway.Utilities.ShowNotification("'/wc remap keyboard' -- Remaps control key (default R)\n'/wc remap mouse' -- Remaps menu mouse key (default middle button)\n'/wc remap action' -- Remaps action key (default numpad0)\n'/wc remap info' -- Remaps info key (default decimal key, aka numpad period key)\n", 10000, "White");
                }
                sendToOthers = false;
            }
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
                            if ((ReplaceVanilla && VanillaIds.ContainsKey(defId)) || WeaponPlatforms.ContainsKey(defId))
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
                    
                    if (BlackListedPlayers.Contains(MultiplayerId))
                    {
                        SuppressWc = true;
                    }

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
            CounterKeenLogMessage(false);
        }

        private void PracticalJokes()
        {
            var chance = MyUtils.GetRandomInt(0, 4);
            if (chance == 2)
            {
                var messageIndex = MyUtils.GetRandomInt(0, JokeMessages.Length);
                MyAPIGateway.Utilities.ShowNotification(JokeMessages[messageIndex], 10000, "Red");
            }
        }

        internal readonly string[] JokeMessages =
        {
            "FakeStar in the house, there can be only one!",
            "Fake DarkStar is here, he loves to answer your shield questions!",
            "FakeStar has now joined to solve all of your shield problems"
        };

        private static void CounterKeenLogMessage(bool console = true)
        {
            var message = "\n***\n    [WeaponCore] Ignore log messages from keen stating 'Mod WeaponCore is accessing physics from parallel threads'\n     WC is using a thread safe parallel.for, not a parallel task\n***";
            if (console) MyLog.Default.WriteLineAndConsole(message);
            else MyLog.Default.WriteLine(message);
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
            DsDebugDraw.DrawLine(w.MyAimTestLine, Color.Black, 0.07f);
            DsDebugDraw.DrawSingleVec(w.MyPivotPos, 1f, Color.White);
            DsDebugDraw.DrawLine(w.AzimuthFwdLine.From, w.AzimuthFwdLine.To, Color.Cyan, 0.05f);

            //DsDebugDraw.DrawLine(w.MyCenterTestLine, Color.Green, 0.05f);

            //DsDebugDraw.DrawBox(w.targetBox, Color.Plum);
            //DsDebugDraw.DrawLine(w.LimitLine.From, w.LimitLine.To, Color.Orange, 0.05f);

            //if (w.Target.HasTarget)
            //DsDebugDraw.DrawLine(w.MyShootAlignmentLine, Color.Yellow, 0.05f);

        }

        private void ModChecker()
        {
            var validId = false;
            foreach (var mod in Session.Mods)
            {
                if (mod.PublishedFileId == 1918681825 || mod.PublishedFileId == 2189703321 || mod.PublishedFileId == 2496225055)
                    validId = true;


                if (mod.PublishedFileId == 1365616918 || mod.PublishedFileId == 2372872458) ShieldMod = true;
                else if (mod.GetPath().Contains("AppData\\Roaming\\SpaceEngineers\\Mods\\DefenseShields"))
                    ShieldMod = true;
                else if (mod.PublishedFileId == 1931509062 || mod.PublishedFileId == 1995197719 || mod.PublishedFileId == 2006751214 || mod.PublishedFileId == 2015560129)
                    ReplaceVanilla = true;
                else if (mod.GetPath().Contains("AppData\\Roaming\\SpaceEngineers\\Mods\\VanillaReplacement") || mod.Name.StartsWith("WCVanilla"))
                    ReplaceVanilla = true;
                else if (mod.PublishedFileId == 2189703321 || mod.PublishedFileId == 2496225055)
                {
                    if (mod.Name != ModContext.ModId)
                        SuppressWc = true;
                }
                else if (mod.PublishedFileId == 2200451495)
                    WaterMod = true;
                else if ((mod.Name == "WeaponCore" || mod.Name == "CoreSystems") && ModContext.ModId != mod.Name)
                    SuppressWc = true;
            }
            if (!validId && Session.SessionSettings.OnlineMode != MyOnlineModeEnum.OFFLINE)
                SuppressWc = true;
        }

        public string ModPath()
        {
            var modPath = ModContext.ModPath;
            return modPath;
        }

        private void Paused()
        {
            _paused = true;
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

        internal bool GridHasPower(MyCubeGrid grid, GridMap map = null)
        {
            bool state = false;
            if (map != null || GridDistributors.TryGetValue(grid, out map)) {
                var slim = map.FakeController.SlimBlock as IMySlimBlock;
                var cube = slim?.FatBlock as MyCubeBlock;

                if (cube != null && cube.CubeGrid == grid) {
                    var dist = map.FakeController.GridResourceDistributor;
                    state = dist?.ResourceState != MyResourceStateEnum.NoPower; 
                }

                map.PowerCheckTick = Tick;
                map.Powered = state;
            }
            return state;
        }

        internal void UpdateGridPowerState()
        {
            foreach (var pair in DirtyPowerGrids)
                GridHasPower(pair.Key, pair.Value);

            DirtyPowerGrids.Clear();
        }

        internal void CheckGridPowerState(MyCubeGrid grid, GridMap map)
        {
            if ((!map.Powered && Tick - map.PowerCheckTick > 600 || map.Powered && Tick - map.PowerCheckTick > 1800))
                DirtyPowerGrids.TryAdd(grid, map);
        }

        internal void MatchPlayersToGrids()
        {
            foreach (var playerInfo in Players)
            {
                //var controlCube = playerInfo.Value.Controller?. as MyCubeBlock;
            }
        }
        
        internal void NewThreat(Weapon w)
        {
            try
            {
                var topmost = w.Target.Entity.GetTopMostParent();
                GridAi.TargetInfo info;
                if (topmost != null && w.Comp.Ai.Construct.RootAi.Construct.PreviousTargets.Add(topmost) && w.Comp.Ai.Targets.TryGetValue(topmost, out info))
                {
                    IMyPlayer weaponOwner;
                    Players.TryGetValue(w.Comp.MyCube.OwnerId, out weaponOwner);
                    var wOwner = weaponOwner != null && !string.IsNullOrEmpty(weaponOwner.DisplayName) ? $"{weaponOwner.DisplayName}({w.Comp.MyCube.OwnerId})" : $"{w.Comp.MyCube.OwnerId}";
                    var weaponFaction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(w.Comp.MyCube.OwnerId);
                    var wFaction = weaponFaction != null && !string.IsNullOrEmpty(weaponFaction.Name) ? $"{weaponFaction.Name}({weaponFaction.FactionId})" : $"NA";

                    IMyPlayer aiOwner;
                    Players.TryGetValue(w.Comp.Ai.AiOwner, out aiOwner);
                    var aOwner = aiOwner != null && !string.IsNullOrEmpty(aiOwner.DisplayName) ? $"{aiOwner.DisplayName}({w.Comp.Ai.AiOwner})" : $"{w.Comp.Ai.AiOwner}";
                    var aiFaction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(w.Comp.Ai.AiOwner);
                    var aFaction = aiFaction != null && !string.IsNullOrEmpty(aiFaction.Name) ? $"{aiFaction.Name}({aiFaction.FactionId})" : $"NA";

                    Log.Line($"New Threat Detected:{topmost.DebugName}\n - by: {w.Comp.Ai.MyGrid.DebugName}" +
                             $"Attacking Weapon:{w.System.WeaponName} " + $"[Weapon] Owner:{wOwner} - Faction:{wFaction} - Neutrals:{w.Comp.Data.Repo.Base.Set.Overrides.Neutrals} - Friends:{w.Comp.Data.Repo.Base.Set.Overrides.Friendly} - Unowned:{w.Comp.Data.Repo.Base.Set.Overrides.Unowned}\n" +
                             $"[Ai] Owner:{aOwner} - Faction:{aFaction} - Relationship:{info.EntInfo.Relationship} - ThreatLevel:{info.OffenseRating} - isFocus:{w.Comp.Ai.Construct.RootAi.Construct.Focus.OldHasFocus}\n", "combat");
                }
            }
            catch (Exception ex) { Log.Line($"NewThreatLogging in SessionDraw: {ex}"); }
        }

        public void CalculateRestrictedShapes(MyStringHash subtype, MyOrientedBoundingBoxD cubeBoundingBox, out MyOrientedBoundingBoxD restrictedBox, out BoundingSphereD restrictedSphere)
        {
            restrictedSphere = new BoundingSphereD();
            restrictedBox = new MyOrientedBoundingBoxD();

            if (!WeaponAreaRestrictions.ContainsKey(subtype))
                return;

            WeaponAreaRestriction restriction = WeaponAreaRestrictions[subtype];
            if (restriction.RestrictionBoxInflation < 0.1 && restriction.RestrictionRadius < 0.1)
                return;

            bool checkBox = restriction.RestrictionBoxInflation > 0;
            bool checkSphere = restriction.RestrictionRadius > 0;

            if (checkBox)
            {
                restrictedBox = new MyOrientedBoundingBoxD(cubeBoundingBox.Center, cubeBoundingBox.HalfExtent, cubeBoundingBox.Orientation);
                restrictedBox.HalfExtent = restrictedBox.HalfExtent + new Vector3D(Math.Sign(restrictedBox.HalfExtent.X) * restriction.RestrictionBoxInflation, Math.Sign(restrictedBox.HalfExtent.Y) * restriction.RestrictionBoxInflation, Math.Sign(restrictedBox.HalfExtent.Z) * restriction.RestrictionBoxInflation);
            }
            if (checkSphere)
            {
                restrictedSphere = new BoundingSphereD(cubeBoundingBox.Center, restriction.RestrictionRadius);
            }
        }

        public bool IsWeaponAreaRestricted(MyStringHash subtype, MyOrientedBoundingBoxD cubeBoundingBox, MyCubeGrid myGrid, long ignoredEntity, GridAi gridAi, out MyOrientedBoundingBoxD restrictedBox, out BoundingSphereD restrictedSphere)
        {
            _tmpNearByBlocks.Clear();
            GridAi ai;
            if (gridAi == null)
            {
                if (!GridToMasterAi.ContainsKey(myGrid))
                {
                    restrictedSphere = new BoundingSphereD();
                    restrictedBox = new MyOrientedBoundingBoxD();
                    return false;
                }
                ai = GridToMasterAi[myGrid];
            } else
            {
                ai = gridAi;
            }

            CalculateRestrictedShapes(subtype, cubeBoundingBox, out restrictedBox, out restrictedSphere);
            var queryRadius = Math.Max(restrictedBox.HalfExtent.AbsMax(), restrictedSphere.Radius);
            if (queryRadius < 0.01)
                return false;
            
            var restriction = WeaponAreaRestrictions[subtype];
            var checkBox = restriction.RestrictionBoxInflation > 0;
            var checkSphere = restriction.RestrictionRadius > 0;
            var querySphere = new BoundingSphereD(cubeBoundingBox.Center, queryRadius);

            myGrid.Hierarchy.QuerySphere(ref querySphere, _tmpNearByBlocks);

            foreach (var grid in ai.SubGrids) {
                if (grid == myGrid || !GridTargetingAIs.ContainsKey(grid))
                    continue;
                grid.Hierarchy.QuerySphere(ref querySphere, _tmpNearByBlocks);
            }

            for (int l = 0; l < _tmpNearByBlocks.Count; l++) {

                var cube = _tmpNearByBlocks[l] as MyCubeBlock;
                if (cube == null || cube.EntityId == ignoredEntity || !WeaponCoreBlockDefs.ContainsKey(cube.BlockDefinition.Id.SubtypeId.String))
                    continue;

                if (!restriction.CheckForAnyWeapon && cube.BlockDefinition.Id.SubtypeId != subtype)
                    continue;

                if (checkBox) {
                    var cubeBox = new MyOrientedBoundingBoxD(cube.PositionComp.LocalAABB, cube.PositionComp.WorldMatrixRef);
                    if (restrictedBox.Contains(ref cubeBox) != ContainmentType.Disjoint)
                        return true;
                }

                if (checkSphere && restrictedSphere.Contains(cube.PositionComp.WorldAABB) != ContainmentType.Disjoint)                
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


        //Would use DSUnique but to many profiler hits
        internal bool UniqueListRemove<T>(T item, Dictionary<T, int> indexer, List<T> list)
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

        internal bool UniqueListAdd<T>(T item, Dictionary<T, int> indexer, List<T> list)
        {
            if (indexer.ContainsKey(item))
                return false;

            list.Add(item);
            indexer.Add(item, list.Count - 1);
            return true;
        }
    }
}
