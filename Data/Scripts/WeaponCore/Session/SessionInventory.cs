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

            ITask = MyAPIGateway.Parallel.StartBackground(ProccessAmmoMoves, ProccessAmmoCallback);
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

            AmmoPull();

            foreach (var itemList in ConsumableItemList) {
                
                for (int i = 0; i < itemList.Value.Count; i++)
                    BetterInventoryItems.Return(itemList.Value[i]);

                itemList.Value.Clear();
            }
        }
        
        internal void AmmoPull()  // In Thread
        {
            try
            {
                foreach (var weapon in WeaponToPullAmmo) { 

                    using (weapon.Comp.Ai?.TopEntity.Pin())
                    using (weapon.Comp.CoreEntity.Pin()) {

                        if (weapon.Comp.CoreEntity.MarkedForClose || weapon.Comp.Ai == null || weapon.Comp.Ai.MarkedForClose || weapon.Comp.Ai.TopEntity.MarkedForClose || !weapon.Comp.InventoryInited || weapon.Comp.Platform.State != CorePlatform.PlatformState.Ready) {
                            InvPullClean.Add(weapon);
                            continue;
                        }

                        var defId = weapon.ActiveAmmoDef.AmmoDefinitionId;
                        var freeSpace = weapon.System.MaxAmmoVolume - weapon.Comp.CurrentInventoryVolume;
                        var spotsFree = (int)(freeSpace / weapon.ActiveAmmoDef.AmmoDef.Const.MagVolume);
                        var magsNeeded = (int)((weapon.System.FullAmmoVolume - weapon.CurrentAmmoVolume) / weapon.ActiveAmmoDef.AmmoDef.Const.MagVolume);
                        magsNeeded = magsNeeded > spotsFree ? spotsFree : magsNeeded;

                        var ammoPullRequests = InventoryMoveRequestPool.Get();
                        ammoPullRequests.Part = weapon;
                        var magsAdded = 0;
                        var logged = 0;

                        foreach (var inventory in weapon.Comp.Ai.InventoryMonitor.Values) {

                            MyConcurrentList<BetterInventoryItem> items;
                            if (ConsumableItemList.TryGetValue(inventory, out items)) {
                                
                                for (int l = items.Count - 1; l >= 0; l--) {
                                    
                                    var item = items[l];
                                    if (!item.DefId.Equals(defId)) continue;

                                    var magsAvailable = item.Amount;

                                    if (magsAvailable > 0 && magsNeeded > 0 && ((IMyInventory)inventory).CanTransferItemTo(weapon.Comp.CoreInventory, defId)) {
                                        
                                        if (magsAvailable >= magsNeeded) {
                                            
                                            ammoPullRequests.Inventories.Add(new InventoryMags { Inventory = inventory, Item = item, Amount = magsNeeded });
                                            magsAdded += magsNeeded;
                                            item.Amount -= magsNeeded;
                                            magsNeeded = 0;
                                        }
                                        else {
                                            
                                            ammoPullRequests.Inventories.Add(new InventoryMags { Inventory = inventory, Item = item, Amount = magsAvailable });

                                            magsNeeded -= magsAvailable;
                                            magsAdded += magsAvailable;
                                            item.Amount -= magsAvailable;

                                            items.RemoveAtFast(l);
                                            BetterInventoryItems.Return(item);
                                        }
                                        weapon.CurrentAmmoVolume = magsAdded * weapon.ActiveAmmoDef.AmmoDef.Const.MagVolume;
                                    }

                                    if (magsNeeded <= 0)
                                        break;
                                }
                            }
                            else if (logged++ == 0) 
                                Log.Line($"[Inventory invalid in AmmoPull] Weapon:{weapon.Comp.SubtypeName}  - blockMarked:{weapon.Comp.CoreEntity.MarkedForClose} - aiMarked:{weapon.Comp.Ai.MarkedForClose} - cTick:{Tick - weapon.Comp.Ai.AiCloseTick} - mTick:{Tick - weapon.Comp.Ai.AiMarkedTick} - sTick:{Tick - weapon.Comp.Ai.CreatedTick}");
                        }

                        if (ammoPullRequests.Inventories.Count > 0)
                            AmmoToPullQueue.Add(ammoPullRequests);
                        else
                            InventoryMoveRequestPool.Return(ammoPullRequests);

                    }
                    InvPullClean.Add(weapon);
                }
            }
            catch (Exception e) { Log.Line($"Error in AmmoPull: {e}");            }
        }

        internal void ProccessAmmoCallback()
        {
            for (int i = 0; i < InvPullClean.Count; i++) {
                var weapon = InvPullClean[i];
                WeaponToPullAmmo.Remove(weapon);
            }

            InvPullClean.Clear();
            InvRemoveClean.Clear();

            MoveAmmo();
            InventoryUpdate = false;
        }

        internal void MoveAmmo()
        {
            for (int i = 0; i < AmmoToPullQueue.Count; i++) {
                
                var weaponAmmoToPull = AmmoToPullQueue[i];
                var weapon = weaponAmmoToPull.Part;
                var inventoriesToPull = weaponAmmoToPull.Inventories;
                
                if (!weapon.Comp.InventoryInited || weapon.Comp.Platform.State != CorePlatform.PlatformState.Ready) {
                    InventoryMoveRequestPool.Return(weaponAmmoToPull);
                    continue;
                }

                for (int j = 0; j < inventoriesToPull.Count; j++) {
                    
                    var mag = inventoriesToPull[j];
                    var amt = mag.Amount;
                    var item = mag.Item;
                    
                    if (weapon.Comp.CoreInventory.ItemsCanBeAdded(amt, weapon.ActiveAmmoDef.AmmoDef.Const.AmmoItem) && mag.Inventory.ItemsCanBeRemoved(amt, item.Item)) {
                        mag.Inventory.RemoveItems(item.Item.ItemId, amt);
                        weapon.Comp.CoreInventory.Add(weapon.ActiveAmmoDef.AmmoDef.Const.AmmoItem, amt);
                    }
                }

                weapon.Ammo.CurrentMags = weapon.Comp.CoreInventory.GetItemAmount(weapon.ActiveAmmoDef.AmmoDefinitionId).ToIntSafe();

                InventoryMoveRequestPool.Return(weaponAmmoToPull);
            }
            AmmoToPullQueue.Clear();
        }
    }
}
