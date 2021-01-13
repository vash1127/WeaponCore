using System;
using System.Collections.Generic;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage;
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
                var isWeaponBase = weaponType && cube.BlockDefinition != null && (Session.ReplaceVanilla && Session.VanillaIds.ContainsKey(cube.BlockDefinition.Id) || Session.WeaponPlatforms.ContainsKey(cube.BlockDefinition.Id));
                
                if (!isWeaponBase && (cube is MyConveyor || cube is IMyConveyorTube || cube is MyConveyorSorter || cube is MyCargoContainer || cube is MyCockpit || cube is IMyAssembler || cube is IMyShipConnector) && cube.CubeGrid.IsSameConstructAs(MyGrid)) { 
                    
                    MyInventory inventory;
                    if (cube.HasInventory && cube.TryGetInventory(out inventory) && InventoryMonitor.TryAdd(cube, inventory)) {

                        inventory.InventoryContentChanged += CheckAmmoInventory;
                        Construct.RootAi.Construct.NewInventoryDetected = true;

                        int monitors;
                        if (!Session.InventoryMonitors.TryGetValue(inventory, out monitors)) {
                            
                            Session.InventoryMonitors[inventory] = 0;
                            Session.InventoryItems[inventory] = Session.PhysicalItemListPool.Get();
                            Session.AmmoThreadItemList[inventory] = Session.BetterItemsListPool.Get();
                        }
                        else
                            Session.InventoryMonitors[inventory] = monitors + 1;
                    }
                }
                else if (battery != null) {
                    if (Batteries.Add(battery)) SourceCount++;
                    UpdatePowerSources = true;
                }
            }
            catch (Exception ex) { Log.Line($"Exception in Controller FatBlockAdded: {ex} - {cube?.BlockDefinition == null} - RootAiNull: {Construct.RootAi == null}"); }
        }

        private void FatBlockRemoved(MyCubeBlock cube)
        {
            try
            {
                var sessionNull = Session == null;
                var weaponType = (cube is MyConveyorSorter || cube is IMyUserControllableGun);
                var cubeDef = cube.BlockDefinition;
                var isWeaponBase = weaponType && cubeDef != null && !sessionNull && (Session.ReplaceVanilla && Session.VanillaIds.ContainsKey(cubeDef.Id) || Session.WeaponPlatforms.ContainsKey(cubeDef.Id));
                var battery = cube as MyBatteryBlock;
                if (sessionNull)
                    Log.Line($"FatBlockRemoved Session was null: AiMarked:{MarkedForClose} - AiClosed:{Closed} - cubeMarked:{cube.MarkedForClose} - CubeGridMarked:{cube.CubeGrid.MarkedForClose} - isRegistered:{SubGridsRegistered.Contains(cube.CubeGrid)} - regCnt:{SubGridsRegistered.Count}");

                MyInventory inventory;
                if (!isWeaponBase && cube.HasInventory && cube.TryGetInventory(out inventory)) {

                    if (!InventoryRemove(cube, inventory))
                        Log.Line($"FatBlock inventory remove failed: {cube.BlockDefinition?.Id.SubtypeName} - gridMatch:{cube.CubeGrid == MyGrid} - aiMarked:{MarkedForClose} - {cube.CubeGrid.DebugName} - {MyGrid?.DebugName}");
                }
                else if (battery != null) {
                    
                    if (Batteries.Remove(battery)) 
                        SourceCount--;
                    
                    UpdatePowerSources = true;
                }
            }
            catch (Exception ex) { Log.Line($"Exception in FatBlockRemoved last: {ex} - Marked: {MarkedForClose} - Closed:{Closed}"); }
        }
        
        
        private bool InventoryRemove(MyCubeBlock cube, MyInventory inventory)
        {
            try
            {
                MyInventory oldInventory;
                if (InventoryMonitor.TryRemove(cube, out oldInventory)) {

                    inventory.InventoryContentChanged -= CheckAmmoInventory;

                    int monitors;
                    if (Session.InventoryMonitors.TryGetValue(inventory, out monitors)) {

                        if (--monitors < 0) {

                            List<MyPhysicalInventoryItem> removedPhysical;
                            List<BetterInventoryItem> removedBetter;

                            if (Session.InventoryItems.TryRemove(inventory, out removedPhysical))
                                Session.PhysicalItemListPool.Return(removedPhysical);

                            if (Session.AmmoThreadItemList.TryRemove(inventory, out removedBetter))
                                Session.BetterItemsListPool.Return(removedBetter);
                            
                            Session.InventoryMonitors.Remove(inventory);
                        }
                        else Session.InventoryMonitors[inventory] = monitors;
                    }
                    else return false;
                }
            }
            catch (Exception ex) { Log.Line($"Exception in InventoryRemove: {ex}"); }
            return true;
        }

        internal void CheckAmmoInventory(MyInventoryBase inventory, MyPhysicalInventoryItem item, MyFixedPoint amount)
        {
            try
            {
                if (amount <= 0 || item.Content == null || inventory == null) return;
                var itemDef = item.Content.GetObjectId();
                if (Session.AmmoDefIds.Contains(itemDef))
                {
                    Construct.RootAi?.Construct.RecentItems.Add(itemDef);
                }
            }
            catch (Exception ex) { Log.Line($"Exception in CheckAmmoInventory: {ex} - BlockName:{((MyEntity)inventory?.Entity)?.DebugName} - BlockMarked:{((MyCubeBlock)inventory?.Entity)?.MarkedForClose} - aiMarked:{MarkedForClose} - gridMatch:{MyGrid == ((MyCubeBlock)inventory?.Entity)?.CubeGrid} - Session:{Session != null} - item:{item.Content?.SubtypeName} - RootConstruct:{Construct?.RootAi?.Construct != null}"); }
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

            Session.DelayedAiClean.Add(this);
            Session.DelayedAiClean.ApplyAdditions();
        }
    }
}
