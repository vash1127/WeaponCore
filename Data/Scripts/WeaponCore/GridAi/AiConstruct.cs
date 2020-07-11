using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using VRage.Game.Entity;
using VRage.ModAPI;
using VRage.Utils;
using WeaponCore.Platform;

namespace WeaponCore.Support
{
    public partial class GridAi
    {
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

            SubGridsChanged = AddSubGrids.Count != 0 || RemSubGrids.Count != 0;
        }

        public void SubGridChanges(bool clean = false)
        {
            foreach (var grid in AddSubGrids)
            {
                grid.Flags |= (EntityFlags)(1 << 31);
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
                GridAi removeAi;
                if (!Session.GridTargetingAIs.ContainsKey(grid))
                    Session.GridToMasterAi.TryRemove(grid, out removeAi);
            }
            RemSubGrids.Clear();

            if (!clean)
            {
                Construct.Refresh(this, Constructs.RefreshCaller.SubGridChange);
                foreach (var grid in SubGrids)
                {
                    if (Construct.RootAi != null)
                        Session.GridToMasterAi[grid] = Construct.RootAi;
                    else Log.Line($"Construct.RootAi is null");
                }
            }
        }

        public class Constructs
        {
            internal readonly List<GridAi> RefreshedAis = new List<GridAi>();
            internal readonly Dictionary<MyStringHash, int> Counter = new Dictionary<MyStringHash, int>(MyStringHash.Comparer);
            internal readonly Focus Focus = new Focus();
            internal readonly ConstructData Data = new ConstructData();
            internal float OptimalDps;
            internal int BlockCount;
            internal GridAi RootAi;

            internal enum RefreshCaller
            {
                Init,
                SubGridChange,
            }

            internal enum UpdateType
            {
                BlockScan,
                Focus,
                None,
            }

            internal void Refresh(GridAi ai, RefreshCaller caller)
            {
                FatMap fatMap;
                if (ai.Session.GridToFatMap.TryGetValue(ai.MyGrid, out fatMap)) {
                    GridAi tmpAi = null;
                    foreach (var grid in ai.SubGrids) {

                        GridAi checkAi;
                        if (ai.Session.GridTargetingAIs.TryGetValue(grid, out checkAi) && (tmpAi == null || tmpAi.MyGrid.EntityId > grid.EntityId)) tmpAi = checkAi;

                        if (ai.Session.GridToFatMap.TryGetValue(grid, out fatMap)) {
                            BlockCount += ai.Session.GridToFatMap[grid].MostBlocks;
                            if (checkAi != null) OptimalDps += checkAi.OptimalDps;
                        }
                        else Log.Line($"ConstructRefresh Failed sub no fatmap, sub is caller:{grid == ai.MyGrid}");
                    }
                    RootAi = tmpAi;

                    if (RootAi == null) {
                        Log.Line($"[rootAi is null in Update] subCnt:{ai.SubGrids.Count}(includeMe:{ai.SubGrids.Contains(ai.MyGrid)}) - caller:{caller}, forcing rootAi to caller - inGridTarget:{ai.Session.GridTargetingAIs.ContainsKey(ai.MyGrid)} -  myGridMarked:{ai.MyGrid.MarkedForClose} - aiMarked:{ai.MarkedForClose} - lastClosed:{ai.AiCloseTick} - aiSpawned:{ai.AiSpawnTick} - diff:{ai.AiSpawnTick - ai.AiCloseTick} - sinceSpawn:{ai.Session.Tick - ai.AiSpawnTick}");
                        RootAi = ai;
                    }

                    UpdateWeaponCounters(ai);
                    RootAi.ScanBlockGroups = true;
                    return;
                }
                Log.Line($"ConstructRefresh Failed main Ai no FatMap: {caller} - Marked: {ai.MyGrid.MarkedForClose}");
                OptimalDps = 0;
                BlockCount = 0;
                RootAi = null;
            }


            internal void UpdateConstruct(UpdateType type, bool sync = true)
            {
                switch (type)
                {
                    case UpdateType.BlockScan:
                    {
                        RootAi.ReScanBlockGroups();
                        UpdateLeafGroups();
                        if (RootAi.Session.MpActive && RootAi.Session.IsServer && sync)
                            RootAi.Session.SendConstructGroups(RootAi);
                        break;
                    }
                    case UpdateType.Focus:
                    {
                        UpdateLeafFoci();
                        if (RootAi.Session.MpActive && RootAi.Session.IsServer && sync)
                            RootAi.Session.SendConstructFoci(RootAi);
                        break;
                    }
                }
            }

            internal void UpdateConstructsPlayers(MyCubeBlock cube, long playerId, bool updateAdd)
            {
                foreach (var sub in RootAi.SubGrids)
                {
                    GridAi ai;
                    if (RootAi.Session.GridTargetingAIs.TryGetValue(sub, out ai))
                    {
                        UpdateActiveControlDictionary(ai, cube, playerId, updateAdd);
                    }
                }
            }

            public static void UpdateActiveControlDictionary(GridAi ai, MyCubeBlock cube, long playerId, bool updateAdd)
            {
                if (updateAdd) //update/add
                {
                    ai.Data.Repo.ControllingPlayers[playerId] = cube.EntityId;
                    ai.AiSleep = false;
                }
                else //remove
                {
                    ai.Data.Repo.ControllingPlayers.Remove(playerId);
                    ai.AiSleep = false;
                }
                if (ai.Session.MpActive)
                    ai.Session.SendAiData(ai);
            }

            internal static void UpdateWeaponCounters(GridAi cAi)
            {
                cAi.Construct.RefreshedAis.Clear();
                cAi.Construct.RefreshedAis.Add(cAi);

                if (cAi.SubGrids.Count > 1) {
                    foreach (var sub in cAi.SubGrids) {
                        if (sub == null || sub == cAi.MyGrid)
                        {
                            if (sub == null)
                                Log.Line($"UpdateWeaponCounters: how was sub null?");
                            continue;
                        }

                        GridAi subAi;
                        if (cAi.Session.GridTargetingAIs.TryGetValue(sub, out subAi))
                            cAi.Construct.RefreshedAis.Add(subAi);
                    }
                }

                for (int i = 0; i < cAi.Construct.RefreshedAis.Count; i++) {

                    var checkAi = cAi.Construct.RefreshedAis[i];
                    checkAi.Construct.Counter.Clear();

                    for (int x = 0; x < cAi.Construct.RefreshedAis.Count; x++) {
                        foreach (var wc in cAi.Construct.RefreshedAis[x].WeaponCounter)
                            checkAi.Construct.AddWeaponCount(wc.Key, wc.Value.Current);
                    }
                }
            }

            internal void AddWeaponCount(MyStringHash weaponHash, int incrementBy = 1)
            {
                if (!Counter.ContainsKey(weaponHash))
                    Counter.Add(weaponHash, incrementBy);
                else Counter[weaponHash] += incrementBy;
            }

            internal int GetWeaponCount(MyStringHash weaponHash)
            {
                int value;
                return Counter.TryGetValue(weaponHash, out value) ? value : 0;
            }

            internal void UpdateLeafGroups()
            {
                foreach (var sub in RootAi.SubGrids)
                {
                    if (RootAi.MyGrid == sub)
                        continue;

                    GridAi ai;
                    if (RootAi.Session.GridTargetingAIs.TryGetValue(sub, out ai))
                        ai.Construct.Data.Repo.Sync(ai.Construct, RootAi.Construct.Data.Repo);
                }
            }

            internal void UpdateLeafFoci()
            {
                foreach (var sub in RootAi.SubGrids)
                {
                    if (RootAi.MyGrid == sub)
                        continue;

                    GridAi ai;
                    if (RootAi.Session.GridTargetingAIs.TryGetValue(sub, out ai))
                        ai.Construct.Focus.Sync(ai, RootAi.Construct.Data.Repo.FocusData);
                }
            }

            internal void GroupRefresh(GridAi ai)
            {
                var s = ai.Session;
                if (ai != RootAi)  {

                    ai.ScanBlockGroups = false;
                    RootAi.ScanBlockGroups = true;
                }
                else {

                    if (s.IsServer)
                        UpdateConstruct(UpdateType.BlockScan);
                    else  {
                        ai.ScanBlockGroups = false;
                        if (s.MpActive)
                            s.SendGroupUpdate(RootAi);
                    }
                }

            }

            internal void Init(GridAi ai)
            {
                RootAi = ai;
                Data.Init(this);
            }

            internal void Clean()
            {
                Data.Clean();
                OptimalDps = 0;
                BlockCount = 0;
                RootAi = null;
                Counter.Clear();
                RefreshedAis.Clear();
            }
        }
    }

    public class Focus
    {
        public readonly long[] OldTarget = new long[2];
        public int OldActiveId;
        public bool OldHasFocus;
        public float OldDistToNearestFocusSqr;
        
        public bool ChangeDetected(GridAi ai)
        {
            var fd = ai.Construct.Data.Repo.FocusData;
            if (fd.Target[0] != OldTarget[0] || fd.Target[1] != OldTarget[1] || fd.ActiveId != OldActiveId || fd.HasFocus != OldHasFocus || Math.Abs(fd.DistToNearestFocusSqr - OldDistToNearestFocusSqr) > 0)  {

                OldTarget[0] = fd.Target[0];
                OldTarget[1] = fd.Target[1];
                OldActiveId = fd.ActiveId;
                OldHasFocus = fd.HasFocus;
                OldDistToNearestFocusSqr = fd.DistToNearestFocusSqr;

                //RefreshData(ai);
                Log.Line($"Focus Change Detected");
                return true;
            }

            return false;
        }

        /*
        internal void RefreshData(GridAi ai)
        {
            var f = ai.Construct.Data.Repo.FocusData;
            ++f.Revision;
            f.ActiveId = ActiveId;
            f.HasFocus = HasFocus;
            f.DistToNearestFocusSqr = DistToNearestFocusSqr;
            for (int i = 0; i < f.Target.Length; i++)
                f.Target[i] = Target[i];
        }
        */

        internal void Sync(GridAi ai, FocusData sync)
        {
            var fd = ai.Construct.Data.Repo.FocusData;
            for (int i = 0; i < fd.Target.Length; i++)
                fd.Target[i] = sync.Target[i];

            fd.ActiveId = sync.ActiveId;
            fd.HasFocus = sync.HasFocus;
            fd.DistToNearestFocusSqr = sync.DistToNearestFocusSqr;
            ai.Construct.Data.Repo.FocusData.Sync(sync);

            Log.Line($"Rev:Target0:{fd.Target[0]}({sync.Target[0]})[{ai.Construct.Data.Repo.FocusData.Target[0]}] - Target1:{fd.Target[1]} - ActId:{fd.ActiveId} - Focus:{fd.HasFocus} - Dist:{fd.DistToNearestFocusSqr}");
        }

        internal void ServerAddFocus(MyEntity target, GridAi ai)
        {
            var session = ai.Session;
            var fd = ai.Construct.Data.Repo.FocusData;

            fd.Target[fd.ActiveId] = target.EntityId;
            ai.TargetResetTick = session.Tick + 1;
            ServerIsFocused(ai);

            ai.Construct.UpdateConstruct(GridAi.Constructs.UpdateType.Focus, ChangeDetected(ai));
        }

        internal void RequestAddFocus(MyEntity target, GridAi ai)
        {
            if (ai.Session.IsServer)
                ServerAddFocus(target, ai);
            else
                ai.Session.SendFocusTargetUpdate(ai, target.EntityId);
        }

        internal void ServerNextActive(bool addSecondary, GridAi ai)
        {
            var fd = ai.Construct.Data.Repo.FocusData;

            var prevId = fd.ActiveId;
            var newActiveId = prevId;
            if (newActiveId + 1 > fd.Target.Length - 1) newActiveId -= 1;
            else newActiveId += 1;

            if (addSecondary && fd.Target[newActiveId] <= 0)
            {
                fd.Target[newActiveId] = fd.Target[prevId];
                fd.ActiveId = newActiveId;
            }
            else if (!addSecondary && fd.Target[newActiveId] > 0)
                fd.ActiveId = newActiveId;

            ServerIsFocused(ai);

            ai.Construct.UpdateConstruct(GridAi.Constructs.UpdateType.Focus, ChangeDetected(ai));

        }

        internal void RequestNextActive(bool addSecondary, GridAi ai)
        {
            if (ai.Session.IsServer)

                ServerNextActive(addSecondary, ai);
            else
                ai.Session.SendNextActiveUpdate(ai, addSecondary);
        }

        internal void ServerReleaseActive(GridAi ai)
        {
            var fd = ai.Construct.Data.Repo.FocusData;

            fd.Target[fd.ActiveId] = -1;

            ServerIsFocused(ai);

            ai.Construct.UpdateConstruct(GridAi.Constructs.UpdateType.Focus, ChangeDetected(ai));
        }

        internal void RequestReleaseActive(GridAi ai)
        {
            if (ai.Session.IsServer)
                ServerReleaseActive(ai);
            else
                ai.Session.SendReleaseActiveUpdate(ai);

        }

        internal bool ServerIsFocused(GridAi ai)
        {
            var fd = ai.Construct.Data.Repo.FocusData;

            fd.HasFocus = false;
            for (int i = 0; i < fd.Target.Length; i++)
            {

                if (fd.Target[i] > 0)
                {

                    if (MyEntities.GetEntityById(fd.Target[fd.ActiveId]) != null)
                        fd.HasFocus = true;
                    else
                        fd.Target[i] = -1;
                }

                if (fd.Target[0] <= 0 && fd.HasFocus)
                {

                    fd.Target[0] = fd.Target[i];
                    fd.Target[i] = -1;
                    fd.ActiveId = 0;
                }
            }

            //UpdateSubGrids(ai);

            return fd.HasFocus;
        }

        /*
        internal void UpdateSubGrids(GridAi ai, bool resetTick = false)
        {
            foreach (var sub in ai.SubGrids)
            {

                if (ai.MyGrid == sub) continue;

                GridAi gridAi;
                if (ai.Session.GridTargetingAIs.TryGetValue(sub, out gridAi))
                {

                    if (resetTick) gridAi.TargetResetTick = gridAi.Session.Tick + 1;
                    for (int i = 0; i < gridAi.Construct.Data.Repo.Focus.Target.Length; i++)
                    {
                        gridAi.Construct.Data.Repo.Focus.Target[i] = Target[i];
                        gridAi.Construct.Data.Repo.Focus.HasFocus = HasFocus;
                        gridAi.Construct.Data.Repo.Focus.ActiveId = ActiveId;
                    }
                }
            }
        }
        */

        internal bool ClientIsFocused(GridAi ai)
        {
            var fd = ai.Construct.Data.Repo.FocusData;

            if (ai.Session.IsServer)
                return ServerIsFocused(ai);

            bool focus = false;
            for (int i = 0; i < fd.Target.Length; i++)
            {

                if (fd.Target[i] > 0)
                    if (MyEntities.GetEntityById(fd.Target[fd.ActiveId]) != null)
                        focus = true;
            }

            return focus;
        }

        internal bool GetPriorityTarget(GridAi ai, out MyEntity target)
        {

            var fd = ai.Construct.Data.Repo.FocusData;

            if (fd.Target[fd.ActiveId] > 0 && MyEntities.TryGetEntityById(fd.Target[fd.ActiveId], out target, true))
                return true;

            for (int i = 0; i < fd.Target.Length; i++)
                if (MyEntities.TryGetEntityById(fd.Target[i], out target, true)) return true;

            target = null;
            return false;
        }

        internal void ReassignTarget(MyEntity target, int focusId, GridAi ai)
        {
            var fd = ai.Construct.Data.Repo.FocusData;

            if (focusId >= fd.Target.Length || target == null || target.MarkedForClose) return;
            fd.Target[focusId] = target.EntityId;
            ServerIsFocused(ai);

            ai.Construct.UpdateConstruct(GridAi.Constructs.UpdateType.Focus, ChangeDetected(ai));
        }

        internal bool FocusInRange(Weapon w)
        {
            var fd = w.Comp.Ai.Construct.Data.Repo.FocusData;

            fd.DistToNearestFocusSqr = float.MaxValue;
            for (int i = 0; i < fd.Target.Length; i++)
            {
                if (fd.Target[i] <= 0)
                    continue;

                MyEntity target;
                if (MyEntities.TryGetEntityById(fd.Target[i], out target))
                {
                    var sphere = target.PositionComp.WorldVolume;
                    var distSqr = (float)MyUtils.GetSmallestDistanceToSphere(ref w.MyPivotPos, ref sphere);
                    distSqr *= distSqr;
                    if (distSqr < fd.DistToNearestFocusSqr)
                        fd.DistToNearestFocusSqr = distSqr;
                }

            }
            return fd.DistToNearestFocusSqr <= w.MaxTargetDistanceSqr;
        }
    }
}
