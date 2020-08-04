using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using VRage;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Utils;
using WeaponCore.Platform;
using WeaponCore.Support;

namespace WeaponCore
{
    public partial class Session
    {
        internal static bool ComputeServerStorage(Weapon weapon)
        {
            var comp = weapon.Comp;
            var s = comp.Session;
            if (weapon.System.DesignatorWeapon) return false;
            
            if (!comp.MyCube.HasInventory || weapon.PullingAmmo) return false;

            var ammo = weapon.ActiveAmmoDef;
            if (!ammo.AmmoDef.Const.EnergyAmmo)
            {
                if (!s.IsCreative) {

                    weapon.Reload.CurrentMags = comp.BlockInventory.GetItemAmount(ammo.AmmoDefinitionId);
                    weapon.CurrentAmmoVolume = (float)weapon.Reload.CurrentMags * weapon.ActiveAmmoDef.AmmoDef.Const.MagVolume;
                    var freeSpace = weapon.System.MaxAmmoVolume - (float) comp.BlockInventory.CurrentVolume;
                    if (!weapon.PullingAmmo && weapon.CurrentAmmoVolume < 0.25f * weapon.System.MaxAmmoVolume && freeSpace > weapon.ActiveAmmoDef.AmmoDef.Const.MagVolume && (s.Tick - weapon.LastInventoryTick > 600 || weapon.CheckInventorySystem ))
                    {
                        weapon.CheckInventorySystem = false;
                        weapon.LastInventoryTick = s.Tick;
                        weapon.PullingAmmo = true;
                        s.UniqueListAdd(weapon, s.WeaponToPullAmmoIndexer, s.WeaponToPullAmmo);
                        s.UniqueListAdd(comp.Ai, s.GridsToUpdateInvetoriesIndexer, s.GridsToUpdateInvetories);
                    }
                }
            }
            
            return !weapon.ActiveAmmoDef.AmmoDef.Const.EnergyAmmo && weapon.ServerReload();
        }

        internal void AmmoPull()  // In Thread
        {
            Weapon weapon = null;
            try
            {
                for (int i = WeaponToPullAmmo.Count - 1; i >= 0; i--) {

                    weapon = WeaponToPullAmmo[i];
                    using (weapon.Comp.Ai?.MyGrid.Pin())
                    using (weapon.Comp.MyCube.Pin()) {

                        if (weapon.Comp.MyCube.MarkedForClose || weapon.Comp.Ai == null || weapon.Comp.Ai.MarkedForClose || weapon.Comp.Ai.MyGrid.MarkedForClose || !weapon.Comp.InventoryInited || weapon.Comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) {
                            UniqueListRemove(weapon, WeaponToPullAmmoIndexer, WeaponToPullAmmo);
                            continue;
                        }

                        var defId = weapon.ActiveAmmoDef.AmmoDefinitionId;
                        var fullAmount = 0.75f * weapon.System.MaxAmmoVolume;
                        var magsNeeded = (int)((fullAmount - weapon.CurrentAmmoVolume) / weapon.ActiveAmmoDef.AmmoDef.Const.MagVolume);
                        var magsAdded = 0;

                        if (magsNeeded == 0 && weapon.System.MaxAmmoVolume > weapon.ActiveAmmoDef.AmmoDef.Const.MagVolume)
                            magsNeeded = 1;

                        var ammoPullRequests = InventoryMoveRequestPool.Get();
                        ammoPullRequests.Weapon = weapon;

                        for (int j = 0; j < weapon.Comp.Ai.Inventories.Count; j++) {

                            var inventory = weapon.Comp.Ai.Inventories[j];
                            var items = AmmoThreadItemList[inventory];

                            for (int l = items.Count - 1; l >= 0; l--)
                            {
                                var item = items[l];

                                if (!item.DefId.Equals(defId)) continue;

                                var magsAvailable = item.Amount;

                                if (((IMyInventory)inventory).CanTransferItemTo(weapon.Comp.BlockInventory, defId))
                                {
                                    if (magsAvailable >= magsNeeded)
                                    {
                                        ammoPullRequests.Inventories.Add(new InventoryMags { Inventory = inventory, Item = item, Amount = magsNeeded });
                                        magsAdded += magsNeeded;
                                        item.Amount -= magsAdded;
                                        magsNeeded = 0;
                                    }
                                    else
                                    {
                                        ammoPullRequests.Inventories.Add(new InventoryMags { Inventory = inventory, Item = item, Amount = magsAvailable });

                                        magsNeeded -= magsAvailable;
                                        magsAdded += magsAvailable;
                                        item.Amount -= magsAdded;

                                        items.RemoveAtFast(l);
                                        BetterInventoryItems.Return(item);
                                    }
                                    weapon.CurrentAmmoVolume += magsAdded * weapon.ActiveAmmoDef.AmmoDef.Const.MagVolume;
                                }

                                if (magsNeeded <= 0)
                                    break;
                            }
                        }

                        if (ammoPullRequests.Inventories.Count > 0)
                            AmmoToPullQueue.Add(ammoPullRequests);
                        else
                            InventoryMoveRequestPool.Return(ammoPullRequests);

                        weapon.Comp.Session.AmmoPulls++;

                        if (magsAdded == 0)
                            weapon.PullingAmmo = false;
                    }
                    UniqueListRemove(weapon, WeaponToPullAmmoIndexer, WeaponToPullAmmo);
                }
            }
            catch (Exception e)
            {
                Log.Line($"Error in AmmoPull: {e}");
                if(weapon != null)
                    UniqueListRemove(weapon, WeaponToPullAmmoIndexer, WeaponToPullAmmo);
            }
        }

        internal void MoveAmmo()
        {
            for (int i = 0; i < AmmoToPullQueue.Count; i ++) {

                var weaponAmmoToPull = AmmoToPullQueue[i];
                var weapon = weaponAmmoToPull.Weapon;
                var inventoriesToPull = weaponAmmoToPull.Inventories;
                if (!weapon.Comp.InventoryInited || weapon.Comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) {
                    InventoryMoveRequestPool.Return(weaponAmmoToPull);
                    continue;
                }

                for (int j = 0; j < inventoriesToPull.Count; j++) {
                    var amt = inventoriesToPull[j].Amount;
                    inventoriesToPull[j].Inventory.RemoveItems(inventoriesToPull[j].Item.ItemId, amt);
                    weapon.Comp.BlockInventory.Add(weapon.ActiveAmmoDef.AmmoDef.Const.AmmoItem, amt);
                }

                weapon.Reload.CurrentMags = weapon.Comp.BlockInventory.GetItemAmount(weapon.ActiveAmmoDef.AmmoDefinitionId);
                weapon.PullingAmmo = false;

                InventoryMoveRequestPool.Return(weaponAmmoToPull);
            }
            AmmoToPullQueue.Clear();
        }

        internal void AmmoToRemove() // In Thread
        {
            for (int i = 0; i < WeaponsToRemoveAmmo.Count; i++) {

                var weapon = WeaponsToRemoveAmmo[i];
                var comp = weapon.Comp;
                var defId = weapon.ActiveAmmoDef.AmmoDefinitionId;
                var inventoryMoveRequests = InventoryMoveRequestPool.Get();
                var ammoToMove = false;
                var items = AmmoThreadItemList[comp.BlockInventory];                

                for (int j = 0; j < items.Count; j++)
                {
                    var item = items[j];

                    if (!item.DefId.Equals(defId)) continue;

                    for (int l = weapon.Comp.Ai.Inventories.Count - 1; l >= 0; l--)
                    {
                        var inventory = weapon.Comp.Ai.Inventories[l];

                        if (!InventoryVolume.ContainsKey(inventory))
                            InventoryVolume[inventory] = inventory.CurrentVolume;

                        var canMove = (int)Math.Floor((float)(inventory.MaxVolume - InventoryVolume[inventory]) / weapon.ActiveAmmoDef.AmmoDef.Const.MagVolume);
                        if (canMove > 0)
                        {
                            if (((IMyInventory)comp.BlockInventory).CanTransferItemTo(inventory, defId))
                            {
                                ammoToMove = true;
                                inventoryMoveRequests.Inventories.Add(new InventoryMags { Inventory = inventory, Item = item, Amount = canMove >= item.Amount ? item.Amount : canMove });
                                AmmoThreadItemList[inventory].Add(item);

                                if (canMove >= item.Amount)
                                {
                                    items.RemoveAtFast(i);
                                    BetterInventoryItems.Return(item);
                                    break;
                                }
                                item.Amount -= canMove;
                            }
                            else Log.Line($"Ammoremove cannot transfer");
                        }
                        else Log.Line($"Ammoremove cannot move: {canMove}");
                    }
                }

                inventoryMoveRequests.Weapon = weapon;
                AmmoToRemoveQueue.Add(inventoryMoveRequests);
                UniqueListRemove(weapon, WeaponsToRemoveAmmoIndexer, WeaponsToRemoveAmmo);
                
                if (!ammoToMove)
                    weapon.RemovingAmmo = false;
            }
            InventoryVolume.Clear();
        }

        internal void RemoveAmmo()
        {
            for (int i = AmmoToRemoveQueue.Count - 1; i >= 0 ; i--) {
                try {

                    var request = AmmoToRemoveQueue[i];
                    var weapon = request.Weapon;
                    if (!weapon.Comp.InventoryInited){
                        request.Inventories.Clear();
                        request.Weapon = null;
                        continue;
                    }

                    var inventoriesToAddTo = request.Inventories;
                    var magItem = weapon.ActiveAmmoDef.AmmoDef.Const.AmmoItem;

                    for (int j = 0; j < inventoriesToAddTo.Count; j++) {
                        var amt = inventoriesToAddTo[i].Amount;
                        weapon.Comp.BlockInventory.RemoveItems(inventoriesToAddTo[i].Item.ItemId, amt);
                        inventoriesToAddTo[i].Inventory.Add(magItem, amt);
                    }

                    weapon.RemovingAmmo = false;
                    InventoryMoveRequestPool.Return(request);
                }
                catch (Exception ex) { Log.Line($"Exception in RemoveAmmo: {ex} - { AmmoToRemoveQueue[i] == null} - {AmmoToRemoveQueue[i]?.Weapon == null} - {AmmoToRemoveQueue[i]?.Weapon?.ActiveAmmoDef == null}"); }
            }
            AmmoToRemoveQueue.Clear();
        }
    }
}
