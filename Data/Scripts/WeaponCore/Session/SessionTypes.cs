using System;
using System.Collections.Generic;
using System.Linq;
using ProtoBuf;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using WeaponCore.Platform;
using WeaponCore.Support;
using static WeaponCore.Platform.Weapon;

namespace WeaponCore
{
    public partial class Session
    {
        internal class ProblemReport
        {
            internal readonly Dictionary<string, Dictionary<string, Func<string>>> AllDicts = new Dictionary<string, Dictionary<string, Func<string>>>();
            internal readonly Dictionary<string, Func<string>> SessionFields;
            internal readonly Dictionary<string, Func<string>> AiFields;
            internal readonly Dictionary<string, Func<string>> CompFields;
            internal readonly Dictionary<string, Func<string>> PlatformFields;
            internal readonly Dictionary<string, Func<string>> WeaponFields;

            internal Session Session;
            internal bool Generating;
            internal MyCubeBlock TargetBlock;
            internal DataReport MyData;
            internal DataReport RemoteData;
            internal MyWeaponPlatform TmpPlatform;
            internal string Report;
            internal uint RequestTime = 1800;
            internal uint LastRequestTick = uint.MaxValue - 7200;

            internal ProblemReport(Session session)
            {
                Session = session;
                SessionFields = InitSessionFields();
                AiFields = InitAiFields();
                CompFields = InitCompFields();
                PlatformFields = InitPlatformFields();
                WeaponFields = InitWeaponFields();

                AllDicts.Add("Session", SessionFields);
                AllDicts.Add("Ai", AiFields);
                AllDicts.Add("Comp", CompFields);
                AllDicts.Add("Platform", PlatformFields);
                AllDicts.Add("Weapon", WeaponFields);
            }

            internal void GenerateReport(MyCubeBlock targetBlock)
            {
                if (Generating || Session.Tick - LastRequestTick < RequestTime) {
                    if (Generating) Log.Line($"Report generation already in progress");
                    else Log.Line($"Report may only be requested every {RequestTime / 60} seconds: {Session.Tick - LastRequestTick}");
                    return;
                }
                Log.Line($"GenerateReport0");
                Generating = true;
                LastRequestTick = Session.Tick;
                TargetBlock = targetBlock;
                MyData = new DataReport();

                if (Session.IsServer) {

                    Compile();
                    if (Session.MpActive) {
                        foreach (var player in Session.Players)
                            NetworkTransfer(false, player.Value.SteamUserId);
                    }
                }
                else {
                    Compile();
                    NetworkTransfer(true);
                }

                Log.Line($"GenerateReport1");
                Session.FutureEvents.Schedule(CompleteReport, null, 600);
            }

            internal DataReport PullData(MyCubeBlock targetBlock)
            {
                Log.Line($"PullData");

                MyData = new DataReport();
                TargetBlock = targetBlock;

                Compile();
                
                Log.Line($"ReturnData");
                return MyData;
            }

            internal void Compile()
            {
                try
                {
                    Log.Line($"Compile Data");
                    BuildData(MyData);
                }
                catch (Exception ex) { Log.Line($"Exception in ReportCompile: {ex}"); }
            }

            internal void BuildData(DataReport data)
            {
                if (Session.DedicatedServer) Log.Line("Build Data");
                foreach (var d in AllDicts)
                {
                    if (Session.DedicatedServer) Log.Line($"dictionary:{d.Key}");
                    foreach (var f in d.Value)
                    {
                        var value = f.Value.Invoke();
                        GetStorage(data, d.Key)[f.Key] = value;
                        if (Session.DedicatedServer) Log.Line($"Member:{f.Key} - Value:{value}");
                    }
                }
            }


            internal string[] IndexToString = { "Session", "Ai", "Comp", "Platform", "Weapon" };
            internal Dictionary<string, string> GetStorage(DataReport data, string storageName)
            {
                switch (storageName)
                {
                    case "Session":
                        return data.Session;
                    case "Ai":
                        return data.Ai;
                    case "Comp":
                        return data.Comp;
                    case "Platform":
                        return data.Platform;
                    case "Weapon":
                        return data.Weapon;
                    default:
                        return null;
                }
            }

            internal void NetworkTransfer(bool toServer, ulong clientId = 0, DataReport data = null)
            {
                Log.Line($"network transfer: toServer: {toServer} - dataNull:{data == null} - clientId:{clientId}");
                if (toServer) {
                    Session.PacketsToServer.Add(new ProblemReportPacket
                    {
                        SenderId = Session.MultiplayerId,
                        PType = PacketType.ProblemReport,
                        EntityId = TargetBlock.EntityId,
                        Type = ProblemReportPacket.RequestType.RequestServerReport,
                    });
                }
                else {
                    Session.PacketsToClient.Add(new PacketInfo {
                        Packet = new ProblemReportPacket {
                            SenderId = clientId,
                            PType = PacketType.ProblemReport,
                            Data = data,
                            Type = ProblemReportPacket.RequestType.SendReport,

                        },
                        SingleClient = true,
                    });
                }
            }

            internal void CompleteReport(object o)
            {
                if (Session.MpActive && (RemoteData == null || MyData == null))
                {
                    Log.Line($"RemoteData:{RemoteData !=null} - MyData:{MyData!= null}, null data detected, waiting 10 second");
                    Clean();
                    return;
                }
                Log.Line($"CompleteReport");
                CompileReport();

                Log.CleanLine($"{Report}");

                Clean();
            }

            internal void CompileReport()
            {
                Log.Line("Compile Report");
                Report = string.Empty;
                var myRole = Session.IsClient ? "Client" : "Server";
                var otherRole = Session.IsClient ? "Server" : "Client";

                for (int i = 0; i < 5; i++)
                {
                    var indexString = IndexToString[i];
                    var myStorage = GetStorage(MyData, indexString);
                    var storageCnt = Session.MpActive ? 2 : 1;
                    Report += $"Class: {indexString}\n";

                    foreach (var p in myStorage)
                    {
                        if (storageCnt > 1)
                        {
                            var remoteStorage = GetStorage(RemoteData, indexString);
                            var remoteValue = remoteStorage[p.Key];
                            Report += $"    [{p.Key}]\n      {myRole}:{p.Value} - {otherRole}:{remoteValue} - Matches:{p.Value == remoteValue}\n";
                        }
                        else
                        {
                            Report += $"    [{p.Key}]\n      {myRole}:{p.Value}\n";
                        }
                    }
                }

            }


            internal Dictionary<string, Func<string>> InitSessionFields()
            {
                var sessionFields = new Dictionary<string, Func<string>>
                {
                    {"DedicatedServer", () => Session.DedicatedServer.ToString() }
                };

                return sessionFields;
            }

            internal Dictionary<string, Func<string>> InitAiFields()
            {
                var aiFields = new Dictionary<string, Func<string>>
                {
                    {"Version", () => GetAi()?.Version.ToString() ?? string.Empty }
                };

                return aiFields;
            }

            internal Dictionary<string, Func<string>> InitCompFields()
            {
                var compFields = new Dictionary<string, Func<string>>
                {
                    {"IsAsleep", () => GetComp()?.IsAsleep.ToString() ?? string.Empty }
                };

                return compFields;
            }

            internal Dictionary<string, Func<string>> InitPlatformFields()
            {
                var platformFields = new Dictionary<string, Func<string>>
                {
                    {"State", () => GetPlatform()?.State.ToString() ?? string.Empty }
                };

                return platformFields;
            }

            internal Dictionary<string, Func<string>> InitWeaponFields()
            {
                var weaponFields = new Dictionary<string, Func<string>>
                {
                    {"AiEnabled", () => {
                        var message = string.Empty;
                        return !TryGetValidPlatform(out TmpPlatform) ? string.Empty : TmpPlatform.Weapons.Aggregate(message, (current, w) => current + $"{w.AiEnabled} ");
                    } }
                };

                return weaponFields;
            }


            internal GridAi GetAi()
            {
                GridAi ai;
                if (Session.GridTargetingAIs.TryGetValue(TargetBlock.CubeGrid, out ai))
                {
                    return ai;
                }
                return null;

            }

            internal WeaponComponent GetComp()
            {
                GridAi ai;
                if (Session.GridTargetingAIs.TryGetValue(TargetBlock.CubeGrid, out ai))
                {
                    WeaponComponent comp;
                    if (ai.WeaponBase.TryGetValue(TargetBlock, out comp))
                    {
                        return comp;
                    }
                }
                return null;

            }

            internal MyWeaponPlatform GetPlatform()
            {
                GridAi ai;
                if (Session.GridTargetingAIs.TryGetValue(TargetBlock.CubeGrid, out ai))
                {
                    WeaponComponent comp;
                    if (ai.WeaponBase.TryGetValue(TargetBlock, out comp))
                    {
                        return comp.Platform;
                    }
                }
                return null;

            }

            internal bool TryGetValidPlatform(out MyWeaponPlatform platform)
            {
                platform = GetPlatform();
                return platform != null;
            }

            internal void Clean()
            {
                MyData = null;
                RemoteData = null;
                TargetBlock = null;
                Generating = false;
                Log.Line("Clean");
            }
        }

        internal class TerminalMonitor
        {
            internal Session Session;
            internal WeaponComponent Comp;
            internal int OriginalAiVersion;
            internal bool Active;

            internal TerminalMonitor(Session session)
            {
                Session = session;
            }

            internal void Update(WeaponComponent comp, bool isCaller = false)
            {
                Comp = comp;

                var oldActiveWeaponTerminal = comp.Ai.Construct.RootAi.ActiveWeaponTerminal;
                comp.Ai.Construct.RootAi.ActiveWeaponTerminal = comp.MyCube;
                var changed = oldActiveWeaponTerminal != comp.Ai.Construct.RootAi.ActiveWeaponTerminal || !Active;

                Active = true;
                OriginalAiVersion = comp.Ai.Version;

                if (comp.IsAsleep)
                    comp.WakeupComp();

                if (Session.IsClient && isCaller && changed) {
                    comp.MIds[(int)PacketType.TerminalMonitor]++;
                    var mId = comp.MIds[(int)PacketType.TerminalMonitor];
                    Log.Line($"sending terminal update");
                    Session.PacketsToServer.Add(new TerminalMonitorPacket {
                        SenderId = Session.MultiplayerId,
                        PType = PacketType.TerminalMonitor,
                        EntityId = Comp.MyCube.EntityId,
                        State = TerminalMonitorPacket.Change.Update,
                        MId = mId,
                    });
                }
            }

            internal void Clean(bool isCaller = false)
            {
                if (Comp != null && Comp.Ai.Version == OriginalAiVersion)
                    Comp.Ai.Construct.RootAi.ActiveWeaponTerminal = null;

                if (isCaller) {
                    Log.Line($"sending terminal clean");
                    Session.PacketsToServer.Add(new TerminalMonitorPacket {
                        SenderId = Session.MultiplayerId,
                        PType = PacketType.TerminalMonitor,
                        EntityId = 0,
                        State = TerminalMonitorPacket.Change.Clean,
                    });
                }

                Comp = null;
                OriginalAiVersion = -1;
                Active = false;
            }

            internal void Monitor()
            {
                if (IsActive()) {
                    if (Session.Tick20)
                        Comp.TerminalRefresh();
                }
                else if (Active)
                    Clean(Session.IsClient);
            }

            internal bool IsActive()
            {
                if (Comp?.Ai == null) return false;

                var sameVersion = Comp.Ai.Version == OriginalAiVersion;
                var nothingMarked = !Comp.MyCube.MarkedForClose && !Comp.Ai.MyGrid.MarkedForClose && !Comp.Ai.MyGrid.MarkedForClose;
                var sameGrid = Comp.MyCube.CubeGrid == Comp.Ai.MyGrid;
                var inTerminalWindow = Session.InMenu && MyAPIGateway.Gui.GetCurrentScreen == MyTerminalPageEnum.ControlPanel || Session.DedicatedServer;
                var compReady = Comp.Platform.State == MyWeaponPlatform.PlatformState.Ready;
                var sameTerminalBlock = Session.LastTerminal == Comp.Ai.Construct.RootAi?.ActiveWeaponTerminal;

                return (sameVersion && nothingMarked && sameGrid && compReady && inTerminalWindow && sameTerminalBlock);
            }


            internal void Purge()
            {
                Clean();
                Session = null;
            }
        }

        internal class AcquireManager
        {
            internal Session Session;
            internal readonly HashSet<WeaponAcquire> Awake = new HashSet<WeaponAcquire>();
            internal readonly HashSet<WeaponAcquire> Asleep = new HashSet<WeaponAcquire>();

            internal readonly List<WeaponAcquire> Collector = new List<WeaponAcquire>();
            internal readonly List<WeaponAcquire> Removal = new List<WeaponAcquire>();

            internal int LastSleepSlot = -1;
            internal int LastAwakeSlot = -1;
            internal int WasAwake;
            internal int WasAsleep;

            internal AcquireManager(Session session)
            {
                Session = session;
            }

            internal void Awaken(WeaponAcquire wa)
            {
                var notValid = !wa.Weapon.Set.Enable || !wa.Weapon.Comp.State.Value.Online || !wa.Weapon.Comp.Set.Value.Overrides.Activate || !wa.Weapon.TrackTarget || Session.IsClient;
                if (notValid)
                {
                    if (!Session.IsClient) Log.Line($"cannot awaken: wEnable:{wa.Weapon.Set.Enable} - cOnline:{wa.Weapon.Comp.State.Value.Online} - cOverride:{wa.Weapon.Comp.Set.Value.Overrides.Activate} - tracking:{wa.Weapon.TrackTarget}");
                    return;
                }

                wa.CreatedTick = Session.Tick;

                if (!wa.Asleep)
                    return;

                Asleep.Remove(wa);

                AddAwake(wa);
            }

            internal void AddAwake(WeaponAcquire wa)
            {
                var notValid = !wa.Weapon.Set.Enable || !wa.Weapon.Comp.State.Value.Online || !wa.Weapon.Comp.Set.Value.Overrides.Activate || !wa.Weapon.TrackTarget || Session.IsClient;
                if (notValid)
                {
                    if (!Session.IsClient) Log.Line($"cannot add: wEnable:{wa.Weapon.Set.Enable} - cOnline:{wa.Weapon.Comp.State.Value.Online} - cOverride:{wa.Weapon.Comp.Set.Value.Overrides.Activate} - tracking:{wa.Weapon.TrackTarget}");
                    return;
                }

                wa.Enabled = true;
                wa.Asleep = false;
                wa.CreatedTick = Session.Tick;

                if (LastAwakeSlot < AwakeBuckets - 1)
                {

                    wa.SlotId = ++LastAwakeSlot;

                    Awake.Add(wa);
                }
                else
                {

                    wa.SlotId = LastAwakeSlot = 0;

                    Awake.Add(wa);
                }
            }

            internal void Remove(WeaponAcquire wa)
            {
                wa.Enabled = false;

                if (wa.Asleep)
                {

                    wa.Asleep = false;
                    Asleep.Remove(wa);
                }
                else
                {
                    Awake.Remove(wa);
                }
            }


            internal void UpdateAsleep()
            {
                WasAwake = 0;
                WasAwake += Awake.Count;

                foreach (var wa in Awake)
                {

                    if (wa.Weapon.Target.HasTarget)
                    {
                        Removal.Add(wa);
                        continue;
                    }

                    if (Session.Tick - wa.CreatedTick > 599)
                    {

                        if (LastSleepSlot < AsleepBuckets - 1)
                        {

                            wa.SlotId = ++LastSleepSlot;
                            wa.Asleep = true;

                            Asleep.Add(wa);
                            Removal.Add(wa);
                        }
                        else
                        {

                            wa.SlotId = LastSleepSlot = 0;
                            wa.Asleep = true;

                            Asleep.Add(wa);
                            Removal.Add(wa);
                        }
                    }
                }

                for (int i = 0; i < Removal.Count; i++)
                    Awake.Remove(Removal[i]);

                Removal.Clear();
            }


            internal void ReorderSleep()
            {
                foreach (var wa in Asleep)
                {

                    var remove = wa.Weapon.Target.HasTarget || wa.Weapon.Comp.IsAsleep || !wa.Weapon.Set.Enable || !wa.Weapon.Comp.State.Value.Online || !wa.Weapon.Comp.Set.Value.Overrides.Activate || Session.IsClient || !wa.Weapon.TrackTarget;

                    if (remove)
                    {
                        Removal.Add(wa);
                        continue;
                    }
                    Collector.Add(wa);
                }

                Asleep.Clear();

                for (int i = 0; i < Removal.Count; i++)
                    Remove(Removal[i]);

                WasAsleep = Collector.Count;

                ShellSort(Collector);

                LastSleepSlot = -1;
                for (int i = 0; i < Collector.Count; i++)
                {

                    var wa = Collector[i];
                    if (LastSleepSlot < AsleepBuckets - 1)
                    {

                        wa.SlotId = ++LastSleepSlot;

                        Asleep.Add(wa);
                    }
                    else
                    {

                        wa.SlotId = LastSleepSlot = 0;

                        Asleep.Add(wa);
                    }
                }
                Collector.Clear();
                Removal.Clear();
            }

            static void ShellSort(List<WeaponAcquire> list)
            {
                int length = list.Count;

                for (int h = length / 2; h > 0; h /= 2)
                {
                    for (int i = h; i < length; i += 1)
                    {
                        var tempValue = list[i];
                        var temp = list[i].Weapon.UniqueId;

                        int j;
                        for (j = i; j >= h && list[j - h].Weapon.UniqueId > temp; j -= h)
                        {
                            list[j] = list[j - h];
                        }

                        list[j] = tempValue;
                    }
                }
            }

            internal void Clean()
            {
                Awake.Clear();
                Asleep.Clear();
                Collector.Clear();
                Removal.Clear();
            }

        }

        public class WeaponAmmoMoveRequest
        {
            public Weapon Weapon;
            public List<InventoryMags> Inventories = new List<InventoryMags>();

            public void Clean()
            {
                Weapon = null;
                Inventories.Clear();
            }
        }

        public struct InventoryMags
        {
            public MyInventory Inventory;
            public int Amount;
        }

    }
}
