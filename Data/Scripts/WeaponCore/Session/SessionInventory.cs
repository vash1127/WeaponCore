using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using WeaponCore.Platform;
using WeaponCore.Support;

namespace WeaponCore
{
    public partial class Session
    {
        internal void StartAmmoTask()
        {
            InventoryUpdate = true;
            if (ITask.valid && ITask.Exceptions != null)
                TaskHasErrors(ref ITask, "ITask");

            StallReporter.Start("StartAmmoTask", 3);
            foreach (var ai in GridsToUpdateInventories) { 

                var logged = 0;
                foreach (var inventory in ai.InventoryMonitor.Values) { 
                    
                    var items = inventory?.GetItems();
                    if (items != null) {

                        MyConcurrentList<MyPhysicalInventoryItem> phyItemList;
                        if (InventoryItems.TryGetValue(inventory, out phyItemList))
                            phyItemList.AddRange(items);
                        else if (logged++ == 0)
                            Log.Line($"phyItemList and inventory.entity is null in StartAmmoTask - grid:{ai.TopEntity.DebugName} - aiMarked:{ai.MarkedForClose} - cTick:{Tick - ai.AiCloseTick} - mTick:{Tick - ai.AiMarkedTick} - sTick:{Tick - ai.CreatedTick}");
                    }
                }
            }
            StallReporter.End();

            DefIdsComparer.Clear();
            GridsToUpdateInventories.Clear();

            ITask = MyAPIGateway.Parallel.StartBackground(ProccessAmmoMoves, ProccessConsumableCallback);
        }

        internal void ProccessAmmoMoves() // In Thread
        {
            foreach (var inventoryItems in InventoryItems) {
                
                for (int i = 0; i < inventoryItems.Value.Count; i++) {
                    
                    var item = inventoryItems.Value[i];
                    if (AmmoDefIds.Contains(item.Content.GetId())) {
                        
                        var newItem = BetterInventoryItems.Get();
                        newItem.Item = item;
                        newItem.Amount = (int)item.Amount;
                        newItem.Content = item.Content;
                        newItem.DefId = item.Content.GetId();
                        ConsumableItemList[inventoryItems.Key].Add(newItem);
                    }
                }
                inventoryItems.Value.Clear();
            }

            foreach (var blockInventoryItems in CoreInventoryItems) {
                
                foreach (var itemList in blockInventoryItems.Value) {
                    
                    var newItem = BetterInventoryItems.Get();
                    newItem.Item = itemList.Value.Item;
                    newItem.Amount = itemList.Value.Amount;
                    newItem.Content = itemList.Value.Content;
                    newItem.DefId = itemList.Value.Content.GetId();
                    ConsumableItemList[blockInventoryItems.Key].Add(newItem);
                }
            }

            ConsumablePull();

            foreach (var itemList in ConsumableItemList) {
                
                for (int i = 0; i < itemList.Value.Count; i++)
                    BetterInventoryItems.Return(itemList.Value[i]);

                itemList.Value.Clear();
            }
        }
        
        internal void ConsumablePull()  // In Thread
        {
            try
            {
                foreach (var part in PartToPullConsumable) { 

                    using (part.Comp.Ai?.TopEntity.Pin())
                    using (part.Comp.CoreEntity.Pin()) {

                        if (part.Comp.CoreEntity.MarkedForClose || part.Comp.Ai == null || part.Comp.Ai.MarkedForClose || part.Comp.Ai.TopEntity.MarkedForClose || !part.Comp.InventoryInited || part.Comp.Platform.State != CorePlatform.PlatformState.Ready) {
                            InvPullClean.Add(part);
                            continue;
                        }

                        var defId = part.ActiveAmmoDef.AmmoDefinitionId;
                        var freeSpace = part.System.MaxAmmoVolume - part.Comp.CurrentInventoryVolume;
                        var spotsFree = (int)(freeSpace / part.ActiveAmmoDef.AmmoDef.Const.MagVolume);
                        var magsNeeded = (int)((part.System.FullAmmoVolume - part.CurrentAmmoVolume) / part.ActiveAmmoDef.AmmoDef.Const.MagVolume);
                        magsNeeded = magsNeeded > spotsFree ? spotsFree : magsNeeded;

                        var consumablePullRequests = InventoryMoveRequestPool.Get();
                        consumablePullRequests.Weapon = part;
                        var magsAdded = 0;
                        var logged = 0;

                        foreach (var inventory in part.Comp.Ai.InventoryMonitor.Values) {

                            MyConcurrentList<BetterInventoryItem> items;
                            if (ConsumableItemList.TryGetValue(inventory, out items)) {
                                
                                for (int l = items.Count - 1; l >= 0; l--) {
                                    
                                    var item = items[l];
                                    if (!item.DefId.Equals(defId)) continue;

                                    var magsAvailable = item.Amount;

                                    if (magsAvailable > 0 && magsNeeded > 0 && ((IMyInventory)inventory).CanTransferItemTo(part.Comp.CoreInventory, defId)) {
                                        
                                        if (magsAvailable >= magsNeeded) {
                                            
                                            consumablePullRequests.Inventories.Add(new InventoryMags { Inventory = inventory, Item = item, Amount = magsNeeded });
                                            magsAdded += magsNeeded;
                                            item.Amount -= magsNeeded;
                                            magsNeeded = 0;
                                        }
                                        else {
                                            
                                            consumablePullRequests.Inventories.Add(new InventoryMags { Inventory = inventory, Item = item, Amount = magsAvailable });

                                            magsNeeded -= magsAvailable;
                                            magsAdded += magsAvailable;
                                            item.Amount -= magsAvailable;

                                            items.RemoveAtFast(l);
                                            BetterInventoryItems.Return(item);
                                        }
                                        part.CurrentAmmoVolume = magsAdded * part.ActiveAmmoDef.AmmoDef.Const.MagVolume;
                                    }

                                    if (magsNeeded <= 0)
                                        break;
                                }
                            }
                            else if (logged++ == 0) 
                                Log.Line($"[Inventory invalid in ConsumablePull] Weapon:{part.Comp.SubtypeName}  - blockMarked:{part.Comp.CoreEntity.MarkedForClose} - aiMarked:{part.Comp.Ai.MarkedForClose} - cTick:{Tick - part.Comp.Ai.AiCloseTick} - mTick:{Tick - part.Comp.Ai.AiMarkedTick} - sTick:{Tick - part.Comp.Ai.CreatedTick}");
                        }

                        if (consumablePullRequests.Inventories.Count > 0)
                            ConsumableToPullQueue.Add(consumablePullRequests);
                        else
                            InventoryMoveRequestPool.Return(consumablePullRequests);

                    }
                    InvPullClean.Add(part);
                }
            }
            catch (Exception e) { Log.Line($"Error in ConsumablePull: {e}");            }
        }

        internal void ProccessConsumableCallback()
        {
            for (int i = 0; i < InvPullClean.Count; i++) {
                var weapon = InvPullClean[i];
                PartToPullConsumable.Remove(weapon);
            }

            InvPullClean.Clear();
            InvRemoveClean.Clear();

            MoveConsumable();
            InventoryUpdate = false;
        }

        internal void MoveConsumable()
        {
            for (int i = 0; i < ConsumableToPullQueue.Count; i++) {
                
                var partConsumableToPull = ConsumableToPullQueue[i];
                var part = partConsumableToPull.Weapon;
                var inventoriesToPull = partConsumableToPull.Inventories;
                
                if (!part.Comp.InventoryInited || part.Comp.Platform.State != CorePlatform.PlatformState.Ready) {
                    InventoryMoveRequestPool.Return(partConsumableToPull);
                    continue;
                }

                for (int j = 0; j < inventoriesToPull.Count; j++) {
                    
                    var mag = inventoriesToPull[j];
                    var amt = mag.Amount;
                    var item = mag.Item;
                    
                    if (part.Comp.CoreInventory.ItemsCanBeAdded(amt, part.ActiveAmmoDef.AmmoDef.Const.AmmoItem) && mag.Inventory.ItemsCanBeRemoved(amt, item.Item)) {
                        mag.Inventory.RemoveItems(item.Item.ItemId, amt);
                        part.Comp.CoreInventory.Add(part.ActiveAmmoDef.AmmoDef.Const.AmmoItem, amt);
                    }
                }

                part.Ammo.CurrentMags = part.Comp.CoreInventory.GetItemAmount(part.ActiveAmmoDef.AmmoDefinitionId).ToIntSafe();

                InventoryMoveRequestPool.Return(partConsumableToPull);
            }
            ConsumableToPullQueue.Clear();
        }
    }
}
