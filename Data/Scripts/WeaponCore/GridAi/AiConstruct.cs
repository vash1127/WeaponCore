using System.Collections.Generic;
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

        public void SubGridChanges()
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
                Session.GridToMasterAi.TryRemove(grid, out removeAi);
            }
            RemSubGrids.Clear();

            Construct.Update(this);

            foreach (var grid in SubGrids)
            {
                if (Construct?.RootAi != null)
                    Session.GridToMasterAi[grid] = Construct.RootAi;
            }
        }

        internal class Constructs
        {
            internal readonly Dictionary<MyStringHash, int> Counter = new Dictionary<MyStringHash, int>(MyStringHash.Comparer);
            internal float OptimalDps;
            internal int BlockCount;
            internal GridAi RootAi;
            internal enum UpdateType
            {
                BlockScan,
                Overrides,
                ManualShootingOff,
                None,
            }

            internal void UpdateConstruct(UpdateType type)
            {
                foreach (var sub in RootAi.SubGrids)
                {

                    GridAi ai;
                    if (RootAi.Session.GridTargetingAIs.TryGetValue(sub, out ai))
                    {

                        switch (type)
                        {
                            case UpdateType.BlockScan:
                                {
                                    ai.ReScanBlockGroups();
                                    break;
                                }
                            case UpdateType.Overrides:
                                {
                                    ai.UpdateGroupOverRides();
                                    break;
                                }
                            case UpdateType.ManualShootingOff:
                                {
                                    ai.TurnManualShootOff();
                                    break;
                                }
                        }
                    }
                }
            }
            internal void Update(GridAi ai)
            {
                FatMap fatMap;
                if (ai?.MyGrid != null && ai.Session.GridToFatMap.TryGetValue(ai.MyGrid, out fatMap))
                {
                    BlockCount = fatMap.MostBlocks;
                    OptimalDps = ai.OptimalDps;
                    GridAi tmpAi = null;
                    foreach (var grid in ai.SubGrids)
                    {
                        GridAi checkAi;
                        if (ai.Session.GridTargetingAIs.TryGetValue(grid, out checkAi) && (tmpAi == null || tmpAi.MyGrid.EntityId > grid.EntityId)) tmpAi = checkAi;

                        if (grid == ai.MyGrid) continue;
                        if (ai.Session.GridToFatMap.TryGetValue(grid, out fatMap))
                        {
                            BlockCount += ai.Session.GridToFatMap[grid].MostBlocks;
                            OptimalDps += ai.OptimalDps;
                        }
                    }
                    RootAi = tmpAi;
                    UpdateWeaponCounters(ai);
                    return;
                }

                OptimalDps = 0;
                BlockCount = 0;
                RootAi = null;
            }

            internal void UpdateWeaponCounters(GridAi ai)
            {
                Counter.Clear();
                foreach (var grid in ai.SubGrids)
                {
                    GridAi checkAi;
                    if (ai.Session.GridTargetingAIs.TryGetValue(grid, out checkAi))
                    {
                        foreach (var wc in checkAi.WeaponCounter)
                        {
                            if (Counter.ContainsKey(wc.Key))
                                Counter[wc.Key] += wc.Value.Current;
                            else Counter.Add(wc.Key, wc.Value.Current);
                        }
                    }
                }
            }

            internal void AddWeaponCount(MyStringHash weaponHash)
            {
                if (Counter.ContainsKey(weaponHash))
                    Counter[weaponHash]++;
                else Counter[weaponHash] = 1;
            }

            internal void RemoveWeaponCount(MyStringHash weaponHash)
            {
                if (Counter.ContainsKey(weaponHash))
                    Counter[weaponHash]--;
                else Counter[weaponHash] = 0;
            }

            internal int GetWeaponCount(MyStringHash weaponHash)
            {
                int value;
                Counter.TryGetValue(weaponHash, out value);
                return value;
            }

            internal void Clean()
            {
                OptimalDps = 0;
                BlockCount = 0;
                RootAi = null;
                Counter.Clear();
            }
        }
    }
}
