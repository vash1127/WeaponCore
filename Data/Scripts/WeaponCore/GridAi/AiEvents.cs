using System;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.Entity;

namespace WeaponCore.Support
{
    public partial class GridAi
    {
        internal void RegisterMyGridEvents(bool register = true, MyCubeGrid grid = null)
        {
            if (grid == null) grid = MyGrid;
            if (register)
            {
                Registered = true;
                grid.OnFatBlockAdded += FatBlockAdded;
                grid.OnFatBlockRemoved += FatBlockRemoved;
                grid.OnClose += GridClose;
            }
            else
            {
                if (Registered)
                {
                    Registered = false;
                    grid.OnFatBlockAdded -= FatBlockAdded;
                    grid.OnFatBlockRemoved -= FatBlockRemoved;
                    grid.OnClose -= GridClose;
                }
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
                else if (myCubeBlock is IMyCargoContainer || myCubeBlock is IMyAssembler || myCubeBlock is IMyShipConnector || myCubeBlock is MyCockpit)
                {

                    MyInventory inventory;
                    if (myCubeBlock.TryGetInventory(out inventory) && Inventories.Add(inventory))
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
                else if (myCubeBlock is IMyUserControllableGun || myCubeBlock is MyConveyorSorter) ScanBlockGroups = true;
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
                    if (myCubeBlock.TryGetInventory(out inventory) && Inventories.Contains(inventory))
                    {
                        Inventories.Remove(inventory);
                        inventory.InventoryContentChanged -= CheckAmmoInventory;
                        foreach (var ammoInvetory in AmmoInventories)
                        {
                            MyFixedPoint pointRemoved;
                            if (ammoInvetory.Value.ContainsKey(inventory))
                                ammoInvetory.Value.TryRemove(inventory, out pointRemoved);
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
                    lock (AmmoInventories[magId])
                    {
                        var hasIntentory = AmmoInventories[magId].ContainsKey(myInventory);

                        if (!hasIntentory && amount > 0)
                        {
                            AmmoInventories[magId][myInventory] = amount;
                            Session.FutureEvents.Schedule(CheckReload, magId, 1);
                        }
                        else if (hasIntentory && AmmoInventories[magId][myInventory] + amount > 0)
                        {
                            AmmoInventories[magId][myInventory] += amount;
                            Session.FutureEvents.Schedule(CheckReload, magId, 1);
                        }
                        else if (hasIntentory)
                        {
                            MyFixedPoint pointRemoved;
                            AmmoInventories[magId].TryRemove(myInventory, out pointRemoved);
                        }
                    }
                }
            }
            Session.AmmoMoveTriggered++;
        }

        internal void GridClose(MyEntity myEntity)
        {
            RegisterMyGridEvents(false);
            Session.GridAiPool.Return(this);

            if (Session.IsClient)
                Session.SendUpdateRequest(MyGrid.EntityId, PacketType.ClientEntityClosed);
        }
    }
}
