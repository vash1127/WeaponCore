﻿using System;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;

namespace WeaponCore.Support
{
    public partial class GridAi
    {
        private void RegisterMyGridEvents(bool register = true, MyCubeGrid grid = null)
        {
            if (grid == null) grid = MyGrid;
            if (register)
            {
                grid.OnFatBlockAdded += FatBlockAdded;
                grid.OnFatBlockRemoved += FatBlockRemoved;
                grid.OnMarkForClose += GridClose;
                //grid.OnClose += GridClose;
            }
            else
            {
                grid.OnFatBlockAdded -= FatBlockAdded;
                grid.OnFatBlockRemoved -= FatBlockRemoved;
                grid.OnMarkForClose -= GridClose;
                //grid.OnClose -= GridClose;
            }
        }

        internal void FatBlockAdded(MyCubeBlock myCubeBlock)
        {
            try
            {
                var battery = myCubeBlock as MyBatteryBlock;
                if (battery != null)
                {
                    if (Batteries.Add(battery)) SourceCount++;
                    UpdatePowerSources = true;
                }
                else if (myCubeBlock is IMyCargoContainer || myCubeBlock is IMyAssembler || myCubeBlock is IMyShipConnector)
                {
                    MyInventory inventory;
                    if (myCubeBlock.TryGetInventory(out inventory))
                    {
                        inventory.InventoryContentChanged += CheckAmmoInventory;
                        foreach (var item in inventory.GetItems())
                        {
                            var ammoMag = item.Content as MyObjectBuilder_AmmoMagazine;
                            if (ammoMag != null && AmmoInventories.ContainsKey(ammoMag.GetObjectId()))
                                CheckAmmoInventory(inventory, item, item.Amount);
                        }
                    }
                }
                else if (myCubeBlock is IMyLargeTurretBase || myCubeBlock is MyConveyorSorter) ScanBlockGroups = true;
            }
            catch (Exception ex) { Log.Line($"Exception in Controller FatBlockAdded: {ex}"); }
        }

        private void FatBlockRemoved(MyCubeBlock myCubeBlock)
        {
            try
            {
                WeaponComponent comp;
                
                var battery = myCubeBlock as MyBatteryBlock;
                if (battery != null)
                {
                    if (Batteries.Remove(battery)) SourceCount--;
                    UpdatePowerSources = true;
                }
                else if (myCubeBlock is IMyCargoContainer || myCubeBlock is IMyAssembler)
                {
                    MyInventory inventory;
                    if (myCubeBlock.TryGetInventory(out inventory))
                    {
                        inventory.InventoryContentChanged -= CheckAmmoInventory;
                        foreach (var ammoInvetory in AmmoInventories) {
                            if (ammoInvetory.Value.ContainsKey(inventory))
                                ammoInvetory.Value.Remove(inventory);
                        }
                    }
                }
                else if (myCubeBlock.Components.TryGet(out comp))
                {
                    foreach (var group in BlockGroups.Values)
                        group.Comps.Remove(comp);
                }
            }
            catch (Exception ex) { Log.Line($"Exception in Controller FatBlockRemoved: {ex}"); }
        }

        private void CheckAmmoInventory(MyInventoryBase inventory, MyPhysicalInventoryItem item, MyFixedPoint amount)
        {
            var ammoMag = item.Content as MyObjectBuilder_AmmoMagazine;
            if (ammoMag != null)
            {
                var myInventory = inventory as MyInventory;
                if (myInventory == null) return;
                var magId = ammoMag.GetObjectId();
                if (AmmoInventories.ContainsKey(magId))
                {
                    var hasIntentory = AmmoInventories[magId].ContainsKey(myInventory);
                    if (!hasIntentory && amount > 0)
                        AmmoInventories[magId][myInventory] = amount;

                    else if (hasIntentory && AmmoInventories[magId][myInventory] + amount > 0)
                        AmmoInventories[magId][myInventory] += amount;

                    else if (hasIntentory)
                        AmmoInventories[magId].Remove(myInventory);

                    CheckReload = true;
                    NewAmmoType = magId;
                }
            }
            Session.AmmoMoveTriggered++;
        }

        private void GridClose(MyEntity myEntity)
        { 
            Session.FutureEvents.Schedule(DelayedGridCleanUp, null, 120);
        }
    }
}
