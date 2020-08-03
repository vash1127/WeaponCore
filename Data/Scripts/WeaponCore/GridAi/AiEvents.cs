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
using static WeaponCore.Session;

namespace WeaponCore.Support
{
    public partial class GridAi
    {
        internal void RegisterMyGridEvents(bool register, MyCubeGrid grid, bool force = false)
        {
            if (grid == null) grid = MyGrid;

            if (register) {

                if (Registered)
                    Log.Line($"Ai RegisterMyGridEvents error");

                Registered = true;
                grid.OnFatBlockAdded += FatBlockAdded;
                grid.OnFatBlockRemoved += FatBlockRemoved;
                grid.OnMarkForClose += GridClose;
                if (SubGridsRegistered.Contains(grid))
                    Log.Line($"Main Grid Already Registered");

                SubGridsRegistered.Add(grid);
            }
            else {

                if (Registered) {

                    Registered = false;
                    grid.OnFatBlockAdded -= FatBlockAdded;
                    grid.OnFatBlockRemoved -= FatBlockRemoved;
                    grid.OnMarkForClose -= GridClose;

                    if (!SubGridsRegistered.Contains(grid))
                        Log.Line($"Main Grid Already UnRegistered");
                    SubGridsRegistered.Remove(grid);
                }
                else if (!force) Log.Line($"NotRegistered: gridReg:{SubGridsRegistered.Contains(grid)}- Aimarked:{MarkedForClose} - aiClosed:{Closed} - Ticks:{Session?.Tick - AiCloseTick} - NullSession:{Session == null} - gridMarked:{grid.MarkedForClose}");
            }
        }

        internal void FatBlockAdded(MyCubeBlock cube)
        {
            try
            {
                var battery = cube as MyBatteryBlock;
                var weaponType = (cube is MyConveyorSorter || cube is IMyUserControllableGun);
                var isWeaponBase = weaponType && cube.BlockDefinition != null && (Session.ReplaceVanilla && Session.VanillaIds.ContainsKey(cube.BlockDefinition.Id) || Session.WeaponPlatforms.ContainsKey(cube.BlockDefinition.Id.SubtypeId));

                if (isWeaponBase && Session.IsServer) Construct.RootAi.ScanBlockGroups = true;
                else if (cube is MyConveyor || cube is IMyConveyorTube || cube is MyConveyorSorter || cube is MyCargoContainer || cube is MyCockpit || cube is IMyAssembler)
                {
                    MyInventory inventory;
                    if (cube.HasInventory && cube.TryGetInventory(out inventory))
                    {
                        if (inventory != null && Session.UniqueListAdd(inventory, InventoryIndexer, Inventories))
                        {
                            inventory.InventoryContentChanged += CheckAmmoInventory;
                            Session.InventoryItems.TryAdd(inventory, new List<MyPhysicalInventoryItem>());
                            Session.AmmoThreadItemList[inventory] = new List<BetterInventoryItem>();
                        }
                    }

                    foreach (var weapon in Construct.RootAi.Construct.OutOfAmmoWeapons)
                        weapon.CheckInventorySystem = true;
                }
                else if (battery != null) {
                    if (Batteries.Add(battery)) SourceCount++;
                    UpdatePowerSources = true;
                }

            }
            catch (Exception ex) { Log.Line($"Exception in Controller FatBlockAdded: {ex} - {cube?.BlockDefinition == null}"); }
        }

        private void FatBlockRemoved(MyCubeBlock cube)
        {
            try
            {
                var sessionNull = Session == null;
                var weaponType = (cube is MyConveyorSorter || cube is IMyUserControllableGun);
                var cubeDef = cube.BlockDefinition;
                var isWeaponBase = weaponType && cubeDef != null && !sessionNull && (Session.ReplaceVanilla && Session.VanillaIds.ContainsKey(cubeDef.Id) || Session.WeaponPlatforms.ContainsKey(cubeDef.Id.SubtypeId));
                if (sessionNull)
                    Log.Line($"FatBlockRemoved Session was null: AiMarked:{MarkedForClose} - AiClosed:{Closed} - cubeMarked:{cube.MarkedForClose} - CubeGridMarked:{cube.CubeGrid.MarkedForClose} - isRegistered:{SubGridsRegistered.Contains(cube.CubeGrid)} - regCnt:{SubGridsRegistered.Count}");

                try {
                    var battery = cube as MyBatteryBlock;
                    MyInventory inventory;
                    if (isWeaponBase && Session.IsServer)
                        Construct.RootAi.ScanBlockGroups = true;
                    else if (cube.HasInventory && cube.TryGetInventory(out inventory))
                    {
                        try
                        {


                            if (inventory != null && !sessionNull && Session.UniqueListRemove(inventory, InventoryIndexer, Inventories))
                            {
                                inventory.InventoryContentChanged -= CheckAmmoInventory;
                                List<MyPhysicalInventoryItem> removedPhysical;
                                List<BetterInventoryItem> removedBetter;
                                if (Session.InventoryItems.TryRemove(inventory, out removedPhysical))
                                    removedPhysical.Clear();

                                if (Session.AmmoThreadItemList.TryRemove(inventory, out removedBetter))
                                    removedBetter.Clear();
                            }
                            
                        } catch (Exception ex) { Log.Line($"Exception in FatBlockRemoved inventory: {ex}"); }
                    }
                    else if (battery != null) {
                        if (Batteries.Remove(battery)) SourceCount--;
                        UpdatePowerSources = true;
                    }
                }
                catch (Exception ex) { Log.Line($"Exception in FatBlockRemoved main: {ex}"); }
            }
            catch (Exception ex) { Log.Line($"Exception in FatBlockRemoved last: {ex} - Marked: {MarkedForClose} - Closed:{Closed}"); }
        }

        internal void CheckAmmoInventory(MyInventoryBase inventory, MyPhysicalInventoryItem item, MyFixedPoint amount)
        {
            try
            {
                if (amount <= 0 || item.Content == null || inventory == null) return;
                var itemDef = item.Content.GetObjectId();
                if (Session.AmmoDefIds.Contains(itemDef))
                    Construct.RootAi.Construct.RecentItems.Add(itemDef);
            }
            catch (Exception ex) { Log.Line($"Exception in CheckAmmoInventory: {ex}"); }
        }

        internal void GridClose(MyEntity myEntity)
        {
            if (Session == null || MyGrid == null || Closed)
            {
                Log.Line($"[GridClose] Session: {Session != null} - MyGrid:{MyGrid != null} - Closed:{Closed} - myEntity:{myEntity != null}");
                return;
            }

            MarkedForClose = true;
            AiMarkedTick = Session.Tick;

            RegisterMyGridEvents(false, MyGrid);

            CleanSubGrids();

            Session.DelayedGridAiClean.Add(this);
            Session.DelayedGridAiClean.ApplyAdditions();
        }
    }
}
