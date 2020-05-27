using System;
using System.Collections.Concurrent;
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
                var isWeaponBase = Session.ReplaceVanilla && Session.VanillaIds.ContainsKey(myCubeBlock.BlockDefinition.Id) || Session.WeaponPlatforms.ContainsKey(myCubeBlock.BlockDefinition.Id.SubtypeId);

                if (battery != null)
                {
                    if (Batteries.Add(battery)) SourceCount++;
                    UpdatePowerSources = true;
                }
                else if (!isWeaponBase && (myCubeBlock is IMyConveyor || myCubeBlock is IMyConveyorTube || myCubeBlock is IMyConveyorSorter || myCubeBlock is IMyCargoContainer || myCubeBlock is IMyCockpit || myCubeBlock is IMyAssembler))
                {
                    MyInventory inventory;
                    if (myCubeBlock.HasInventory && myCubeBlock.TryGetInventory(out inventory) && Session.UniqueListAdd(inventory, InventoryIndexer, Inventories))
                        inventory.InventoryContentChanged += CheckAmmoInventory;

                    foreach (var weapon in OutOfAmmoWeapons)
                        Session.ComputeStorage(weapon);
                }
                else if (isWeaponBase) ScanBlockGroups = true;

            }
            catch (Exception ex) { Log.Line($"Exception in Controller FatBlockAdded: {ex}"); }
        }


        private void FatBlockRemoved(MyCubeBlock myCubeBlock)
        {
            try
            {
                WeaponComponent comp;
                MyInventory inventory;

                var battery = myCubeBlock as MyBatteryBlock;

                if (battery != null)
                {
                    if (Batteries.Remove(battery)) SourceCount--;
                    UpdatePowerSources = true;
                }
                else if (myCubeBlock.Components.TryGet(out comp))
                {
                    foreach (var group in BlockGroups.Values)
                        group.Comps.Remove(comp);
                }
                else if (myCubeBlock.TryGetInventory(out inventory) && Session.UniqueListRemove(inventory, InventoryIndexer, Inventories))
                {
                    inventory.InventoryContentChanged -= CheckAmmoInventory;

                    ConcurrentDictionary<MyDefinitionId, MyFixedPoint> removed;
                    if (Session.InventoryItems.TryRemove(inventory, out removed))
                        removed.Clear();
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
            if (Session.Tick - ProjectileTicker > 61 && Session.DbTask.IsComplete)
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
