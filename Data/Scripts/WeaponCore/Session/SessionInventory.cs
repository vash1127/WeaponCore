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
        internal static void ComputeStorage(Weapon weapon)
        {
            var comp = weapon.Comp;
            var s = comp.Session;
            if (weapon.System.DesignatorWeapon) return;

            if (!s.IsClient) {

                if (!comp.MyCube.HasInventory) return;
                var ammo = weapon.ActiveAmmoDef;

                if (!ammo.AmmoDef.Const.EnergyAmmo)
                {
                    if (!s.IsCreative) {

                        weapon.State.Sync.CurrentMags = comp.BlockInventory.GetItemAmount(ammo.AmmoDefinitionId);
                        weapon.CurrentAmmoVolume = (float) weapon.State.Sync.CurrentMags * weapon.ActiveAmmoDef.AmmoDef.Const.MagVolume;

                        weapon.Reload();

                        var freeSpace = weapon.System.MaxAmmoVolume - (float) comp.BlockInventory.CurrentVolume;
                        if (!weapon.PullingAmmo && weapon.CurrentAmmoVolume < 0.25f * weapon.System.MaxAmmoVolume && freeSpace > weapon.ActiveAmmoDef.AmmoDef.Const.MagVolume) {
                            weapon.PullingAmmo = true;
                            s.UniqueListAdd(weapon, s.WeaponToPullAmmoIndexer, s.WeaponToPullAmmo);
                            s.UniqueListAdd(comp.Ai, s.GridsToUpdateInvetoriesIndexer, s.GridsToUpdateInvetories);
                        }
                    }
                    else weapon.Reload();
                }
                else weapon.Reload();
            }
            else if (!weapon.ActiveAmmoDef.AmmoDef.Const.EnergyAmmo) {
                //Log.Line($"ComputeStorage: mags: {weapon.State.Sync.CurrentMags} - ammo: {weapon.State.Sync.CurrentAmmo} - hasInventory: {weapon.State.Sync.HasInventory} - noMagsToload:{weapon.NoMagsToLoad}");
                weapon.Reload();
            }
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

                        var def = weapon.ActiveAmmoDef.AmmoDefinitionId;
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

                                var magsAvailable = item.Amount;

                                if (((IMyInventory)inventory).CanTransferItemTo(weapon.Comp.BlockInventory, def))
                                {

                                    if (magsAvailable >= magsNeeded)
                                    {
                                        ammoPullRequests.Inventories.Add(new InventoryMags { Inventory = inventory, Item = item, Amount = magsNeeded });
                                        magsAdded += magsNeeded;
                                        magsNeeded = 0;
                                        item.Amount -= magsAdded;
                                    }
                                    else
                                    {
                                        ammoPullRequests.Inventories.Add(new InventoryMags { Inventory = inventory, Item = item, Amount = magsAvailable });
                                        magsNeeded -= magsAvailable;
                                        magsAdded += magsAvailable;
                                        item.Amount -= magsAdded;
                                        items.RemoveAtFast(l);
                                        BetterInventoryItems.Push(item);
                                    }
                                    weapon.CurrentAmmoVolume += magsAdded * weapon.ActiveAmmoDef.AmmoDef.Const.MagVolume;
                                }

                                if (magsNeeded <= 0)
                                    break;
                            }
                        }

                        if (magsAdded == 0)
                            weapon.PullingAmmo = false;

                        if (ammoPullRequests.Inventories.Count > 0)
                            AmmoToPullQueue.Add(ammoPullRequests);
                        else
                            InventoryMoveRequestPool.Return(ammoPullRequests);

                        weapon.Comp.Session.AmmoPulls++;
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
                
                var def = weapon.ActiveAmmoDef.AmmoDefinitionId;
                var magItem = weapon.ActiveAmmoDef.AmmoDef.Const.AmmoItem;

                for (int j = 0; j < inventoriesToPull.Count; j++) {
                    var amt = inventoriesToPull[j].Amount;
                    inventoriesToPull[j].Inventory.RemoveItems(inventoriesToPull[j].Item.ItemId, amt);
                    weapon.Comp.BlockInventory.Add(magItem, amt);
                }

                weapon.State.Sync.CurrentMags = weapon.Comp.BlockInventory.GetItemAmount(weapon.ActiveAmmoDef.AmmoDefinitionId);
                weapon.PullingAmmo = false;

                InventoryMoveRequestPool.Return(weaponAmmoToPull);
            }
            AmmoToPullQueue.Clear();
        }

        internal void AmmoToRemove() // In Thread
        {
            InventoryVolume.Clear();
            for (int i = 0; i < WeaponsToRemoveAmmo.Count; i++) {

                var weapon = WeaponsToRemoveAmmo[i];
                var def = weapon.ActiveAmmoDef.AmmoDefinitionId;
                var ai = weapon.Comp.Ai;
                var comp = weapon.Comp;
                var itemVolume = weapon.ActiveAmmoDef.AmmoDef.Const.MagVolume;
                var inventoryMoveRequests = InventoryMoveRequestPool.Get();

                var items = AmmoThreadItemList[comp.BlockInventory];
                for (int j = 0; j < items.Count; j++)
                {
                    for (int l = ai.Inventories.Count - 1; l >= 0; l--)
                    {
                        var inventory = ai.Inventories[l];

                        if (!InventoryVolume.ContainsKey(inventory))
                            InventoryVolume[inventory] = inventory.CurrentVolume;

                        var canMove = (int)Math.Floor((float)(inventory.MaxVolume - InventoryVolume[inventory]) / itemVolume);
                        if (canMove > 0)
                        {
                            if (((IMyInventory)comp.BlockInventory).CanTransferItemTo(inventory, def))
                            {
                                var item = items[l];
                                inventoryMoveRequests.Inventories.Add(new InventoryMags { Inventory = inventory, Item = item, Amount = canMove >= item.Amount ? (int)item.Amount : canMove });
                                AmmoThreadItemList[inventory].Add(item);

                                if (canMove >= item.Amount)
                                {
                                    items.RemoveAtFast(i);
                                    BetterInventoryItems.Push(item);
                                    break;
                                }
                                item.Amount -= canMove;
                            }
                        }
                    }
                }

                inventoryMoveRequests.Weapon = weapon;
                AmmoToRemoveQueue.Add(inventoryMoveRequests);
                UniqueListRemove(weapon, WeaponsToRemoveAmmoIndexer, WeaponsToRemoveAmmo);
            }
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
                    var def = weapon.ActiveAmmoDef.AmmoDefinitionId;
                    var magItem = weapon.ActiveAmmoDef.AmmoDef.Const.AmmoItem;

                    weapon.ChangeActiveAmmo(weapon.System.AmmoTypes[weapon.Set.AmmoTypeId]);
                    for (int j = 0; j < inventoriesToAddTo.Count; j++) {
                        var amt = inventoriesToAddTo[i].Amount;
                        weapon.Comp.BlockInventory.RemoveItems(inventoriesToAddTo[i].Item.ItemId, amt);
                        inventoriesToAddTo[i].Inventory.Add(magItem, amt);
                    }

                    WepUi.SetDps(weapon.Comp, weapon.Comp.Set.Value.DpsModifier, false, true);

                    //ComputeStorage(weapon);

                    request.Inventories.Clear();
                    request.Weapon = null;
                    InventoryMoveRequestPool.Return(request);
                }
                catch (Exception ex) { Log.Line($"Exception in RemoveAmmo: {ex} - { AmmoToRemoveQueue[i] == null} - {AmmoToRemoveQueue[i]?.Weapon == null} - {AmmoToRemoveQueue[i]?.Weapon?.ActiveAmmoDef == null}"); }
            }
            AmmoToRemoveQueue.Clear();
        }
    }
}
