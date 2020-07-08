using System.Collections.Generic;
using Havok;
using Sandbox.Game.Entities;
using VRage.ModAPI;
using VRage.Utils;
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
                Overrides,
                //ManualShootingOff,
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


            internal void UpdateConstruct(UpdateType type)
            {
                switch (type)
                {
                    case UpdateType.BlockScan:
                    {
                        RootAi.ReScanBlockGroups();
                        UpdateLeafGroups();
                        if (RootAi.Session.MpActive && RootAi.Session.IsServer)
                            RootAi.Session.SendConstructGroups(RootAi);
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

            public void UpdateActiveControlDictionary(GridAi ai, MyCubeBlock cube, long playerId, bool updateAdd)
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
}
