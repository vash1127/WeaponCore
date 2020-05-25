using System;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage;
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
                        inventory.InventoryContentChanged += CheckAmmoInventory;
                    
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
                else if (myCubeBlock is IMyCargoContainer || myCubeBlock is IMyAssembler || myCubeBlock is IMyShipConnector || myCubeBlock is MyCockpit)
                {
                    MyInventory inventory;
                    if (myCubeBlock.TryGetInventory(out inventory) && Inventories.Remove(inventory))
                        inventory.InventoryContentChanged -= CheckAmmoInventory;
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
            if (amount <= 0) return;
            var itemDef = item.Content.GetObjectId();
            if (Session.AmmoDefIds.Contains(itemDef))
                Session.FutureEvents.Schedule(CheckReload, itemDef, 1);
        }

        internal void GridClose(MyEntity myEntity)
        {
            RegisterMyGridEvents(false);
            if (Session.Tick - ProjectileTicker > 61)
            {
                Session.GridAiPool.Return(this);
                if (Session.IsClient)
                    Session.SendUpdateRequest(MyGrid.EntityId, PacketType.ClientEntityClosed);
            }
            else if (myEntity != null)
            {
                Session.DelayedGridAiClean.Add(this);
                Session.DelayedGridAiClean.ApplyAdditions();
            }
        }
    }
}
