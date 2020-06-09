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

            if (register) {

                if (Registered)
                    Log.Line($"Ai RegisterMyGridEvents error");

                Registered = true;
                MarkedForClose = false;
                grid.OnFatBlockAdded += FatBlockAdded;
                grid.OnFatBlockRemoved += FatBlockRemoved;
                grid.OnClose += GridClose;
            }
            else {

                if (!Registered)
                    Log.Line($"Ai UnRegisterMyGridEvents error");
                MarkedForClose = true;
                if (Registered) {


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
                var isWeaponBase = Session.ReplaceVanilla && myCubeBlock?.BlockDefinition != null && Session.VanillaIds.ContainsKey(myCubeBlock.BlockDefinition.Id) || myCubeBlock?.BlockDefinition != null && Session.WeaponPlatforms.ContainsKey(myCubeBlock.BlockDefinition.Id.SubtypeId);

                if (battery != null)
                {
                    if (Batteries.Add(battery)) SourceCount++;
                    UpdatePowerSources = true;
                }
                else if (!isWeaponBase && (myCubeBlock is MyConveyor || myCubeBlock is IMyConveyorTube || myCubeBlock is MyConveyorSorter || myCubeBlock is MyCargoContainer || myCubeBlock is MyCockpit || myCubeBlock is IMyAssembler))
                {
                    MyInventory inventory;
                    if (myCubeBlock.HasInventory && myCubeBlock.TryGetInventory(out inventory) && Session.UniqueListAdd(inventory, InventoryIndexer, Inventories))
                        inventory.InventoryContentChanged += CheckAmmoInventory;

                    foreach (var weapon in OutOfAmmoWeapons)
                        Session.CheckStorage.Add(weapon);
                }
                else if (isWeaponBase) ScanBlockGroups = true;

            }
            catch (Exception ex) { Log.Line($"Exception in Controller FatBlockAdded: {ex} - {myCubeBlock?.BlockDefinition == null}"); }
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

        internal void CheckAmmoInventory(MyInventoryBase inventory, MyPhysicalInventoryItem item, MyFixedPoint amount)
        {
            if (amount <= 0) return;
            var itemDef = item.Content.GetObjectId();
            if (Session.AmmoDefIds.Contains(itemDef))
                Session.FutureEvents.Schedule(CheckReload, itemDef, 1);
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

            if (Session.IsClient)
                Session.SendUpdateRequest(MyGrid.EntityId, PacketType.ClientEntityClosed);
        }
    }
}
