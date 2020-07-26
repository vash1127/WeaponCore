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
        internal void RegisterMyGridEvents(bool register = true, MyCubeGrid grid = null)
        {
            if (grid == null) grid = MyGrid;

            if (register) {

                if (Registered)
                    Log.Line($"Ai RegisterMyGridEvents error");

                Registered = true;
                MarkedForClose = false;
                AiMarkedTick = Session.Tick;
                grid.OnFatBlockAdded += FatBlockAdded;
                grid.OnFatBlockRemoved += FatBlockRemoved;
                grid.OnClose += GridClose;
            }
            else {

                if (!Registered)
                    Log.Line($"Ai UnRegisterMyGridEvents error");
                MarkedForClose = true;
                AiMarkedTick = Session.Tick;
                if (Registered) {

                    Registered = false;
                    grid.OnFatBlockAdded -= FatBlockAdded;
                    grid.OnFatBlockRemoved -= FatBlockRemoved;
                    grid.OnClose -= GridClose;
                }
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
                        else Log.Line($"FatBlockAdded invalid inventory - null:{inventory == null} - has:{cube.HasInventory}");
                    }

                    foreach (var weapon in OutOfAmmoWeapons)
                        Session.CheckStorage.Add(weapon);
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
                var weaponType = (cube is MyConveyorSorter || cube is IMyUserControllableGun);
                var cubeDef = cube?.BlockDefinition;
                var isWeaponBase = weaponType && cubeDef != null && (Session.ReplaceVanilla && Session.VanillaIds.ContainsKey(cubeDef.Id) || Session.WeaponPlatforms.ContainsKey(cubeDef.Id.SubtypeId));

                try {
                    var battery = cube as MyBatteryBlock;
                    MyInventory inventory;
                    if (isWeaponBase && Session.IsServer)
                        Construct.RootAi.ScanBlockGroups = true;
                    else if (cube != null && cube.HasInventory && cube.TryGetInventory(out inventory))
                    {
                        try
                        {
                            var sessionNull = Session == null;
                            
                            if (sessionNull)
                                Log.Line($"FatBlockRemoved Session was null");

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
                            else if (inventory == null) Log.Line($"FatBlockRemoved invalid inventory - hasInventory:{cube.HasInventory}");
                            
                        } catch (Exception ex) { Log.Line($"Exception in FatBlockRemoved inventory: {ex}"); }
                    }
                    else if (battery != null) {
                        if (Batteries.Remove(battery)) SourceCount--;
                        UpdatePowerSources = true;
                    }
                }
                catch (Exception ex) { Log.Line($"Exception in FatBlockRemoved main: {ex}"); }
            }
            catch (Exception ex) { Log.Line($"Exception in FatBlockRemoved last: {ex}"); }
        }

        internal void CheckAmmoInventory(MyInventoryBase inventory, MyPhysicalInventoryItem item, MyFixedPoint amount)
        {
            try
            {
                if (amount <= 0 || item.Content == null || inventory == null) return;
                var itemDef = item.Content.GetObjectId();
                if (Session.AmmoDefIds.Contains(itemDef))
                    CheckReload(itemDef);
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

            ProjectileTicker = Session.Tick;
            Session.DelayedGridAiClean.Add(this);
            Session.DelayedGridAiClean.ApplyAdditions();
        }
    }
}
