using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using VRage;
using VRage.Game;
using VRage.Game.ModAPI;
using WeaponCore.Platform;
using WeaponCore.Support;

namespace WeaponCore
{
    public partial class Session
    {
        internal static void ComputeStorage(Weapon weapon, bool force = false)
        {
            var comp = weapon.Comp;
            var s = comp.Session;
            if (weapon.System.DesignatorWeapon) return;

            if (!s.IsClient) {

                if (!comp.MyCube.HasInventory) return;
                var ammo = weapon.ActiveAmmoDef;

                if (!ammo.AmmoDef.Const.EnergyAmmo) {

                    if (!s.IsCreative) {
                        
                        weapon.State.Sync.CurrentMags = comp.BlockInventory.GetItemAmount(ammo.AmmoDefinitionId);
                        weapon.CurrentAmmoVolume = (float)weapon.State.Sync.CurrentMags * weapon.ActiveAmmoDef.AmmoDef.Const.MagVolume;

                        if (weapon.CanReload)
                            weapon.StartReload();

                        var freeSpace = weapon.System.MaxAmmoVolume - (float)comp.BlockInventory.CurrentVolume;
                        if (weapon.CurrentAmmoVolume < 0.25f * weapon.System.MaxAmmoVolume && freeSpace > weapon.ActiveAmmoDef.AmmoDef.Const.MagVolume) {
                            s.UniqueListAdd(weapon, s.WeaponToPullAmmoIndexer, s.WeaponToPullAmmo);
                            s.UniqueListAdd(comp.Ai, s.GridsToUpdateInvetoriesIndexer, s.GridsToUpdateInvetories);
                        }
                    }
                    else if (weapon.CanReload)
                        weapon.StartReload();
                }
                else if (weapon.CanReload)
                    weapon.StartReload();
            }
            else if (weapon.State.Sync.CurrentAmmo == 0 && !weapon.ActiveAmmoDef.AmmoDef.Const.EnergyAmmo) {
                weapon.State.Sync.CurrentMags = weapon.Comp.BlockInventory.GetItemAmount(weapon.ActiveAmmoDef.AmmoDefinitionId);
                if (weapon.CanReload)
                    weapon.StartReload();
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

                        if (weapon.Comp.MyCube.MarkedForClose || weapon.Comp.Ai == null || weapon.Comp.Ai.MyGrid.MarkedForClose || !weapon.Comp.InventoryInited || weapon.Comp.Platform.State != MyWeaponPlatform.PlatformState.Ready || weapon.Comp.MyCube == null) {
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
                            if (!InventoryItems.ContainsKey(inventory) || !InventoryItems[inventory].ContainsKey(def)) continue;

                            var magsAvailable = (int)InventoryItems[inventory][def];
                            if (((IMyInventory)inventory).CanTransferItemTo(weapon.Comp.BlockInventory, def)) {

                                if (magsAvailable >= magsNeeded) {
                                    ammoPullRequests.Inventories.Add(new InventoryMags { Inventory = inventory, Amount = magsNeeded });
                                    magsAdded += magsNeeded;
                                    magsNeeded = 0;
                                    InventoryItems[inventory][def] -= magsAdded;
                                }
                                else {
                                    ammoPullRequests.Inventories.Add(new InventoryMags { Inventory = inventory, Amount = magsAvailable });
                                    magsNeeded -= magsAvailable;
                                    magsAdded += magsAvailable;
                                    InventoryItems[inventory][def] -= magsAdded;
                                    InventoryItems[inventory].Remove(def);
                                }
                                weapon.CurrentAmmoVolume += magsAdded * weapon.ActiveAmmoDef.AmmoDef.Const.MagVolume;
                            }

                            if (magsNeeded <= 0)
                                break;
                        }

                        if (ammoPullRequests.Inventories.Count > 0)
                            AmmoToPullQueue.Add(ammoPullRequests);
                        else
                            InventoryMoveRequestPool.Return(ammoPullRequests);

                        weapon.Comp.Session.AmmoPulls++;
                    }

                    UniqueListRemove(weapon, WeaponToPullAmmoIndexer, WeaponToPullAmmo);
                }
                AmmoToPullQueue.ApplyAdditions();
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
                if (!weapon.Comp.InventoryInited || weapon?.Comp == null || weapon.Comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) {
                    inventoriesToPull.Clear();
                    weaponAmmoToPull.Weapon = null;
                    InventoryMoveRequestPool.Return(weaponAmmoToPull);
                    continue;
                }
                
                var def = weapon.ActiveAmmoDef.AmmoDefinitionId;
                var magItem = weapon.ActiveAmmoDef.AmmoDef.Const.AmmoItem;

                for (int j = 0; j < inventoriesToPull.Count; j++) {
                    var amt = inventoriesToPull[j].Amount;
                    inventoriesToPull[j].Inventory.RemoveItemsOfType(amt, def);
                    weapon.Comp.BlockInventory.Add(magItem, amt);
                }

                weapon.State.Sync.CurrentMags = weapon.Comp.BlockInventory.GetItemAmount(weapon.ActiveAmmoDef.AmmoDefinitionId);

                if (inventoriesToPull.Count > 0 && weapon.CanReload)
                    weapon.StartReload();

                inventoriesToPull.Clear();
                weaponAmmoToPull.Weapon = null;
                InventoryMoveRequestPool.Return(weaponAmmoToPull);

                AmmoToPullQueue.Remove(weaponAmmoToPull);
            }
            AmmoToPullQueue.ApplyRemovals();
        }

        internal void AmmoToRemove() // In Thread
        {
            for (int i = 0; i < WeaponsToRemoveAmmo.Count; i++) {

                var weapon = WeaponsToRemoveAmmo[i];
                var def = weapon.ActiveAmmoDef.AmmoDefinitionId;
                var ai = weapon.Comp.Ai;
                var itemVolume = weapon.ActiveAmmoDef.AmmoDef.Const.MagVolume;
                var magsToRemove = weapon.State.Sync.CurrentMags;
                var inventoryMoveRequests = InventoryMoveRequestPool.Get();                

                for (int j = 0; j <  ai.Inventories.Count; j++) {
                    var inventory = ai.Inventories[j];

                    if (!InventoryItems.ContainsKey(inventory))
                        InventoryItems[inventory] = new ConcurrentDictionary<MyDefinitionId, MyFixedPoint>(MyDefinitionId.Comparer);

                    if (((IMyInventory)weapon.Comp.BlockInventory).CanTransferItemTo(inventory, def)) {
                        
                        var canMove = (int)Math.Floor((float)(inventory.MaxVolume - inventory.CurrentVolume) / itemVolume);
                        if (canMove > 0)
                        {
                            if(!InventoryItems[inventory].ContainsKey(def))
                                InventoryItems[inventory][def] = 0;

                            if (canMove >= magsToRemove)
                            {
                                inventoryMoveRequests.Inventories.Add(new InventoryMags { Inventory = inventory, Amount = (int)magsToRemove });
                                InventoryItems[inventory][def] += magsToRemove;
                                break;
                            }

                            inventoryMoveRequests.Inventories.Add(new InventoryMags { Inventory = inventory, Amount = canMove });
                            InventoryItems[inventory][def] += canMove;
                            magsToRemove -= canMove;
                        }
                    }
                }

                inventoryMoveRequests.Weapon = weapon;
                AmmoToRemoveQueue.Add(inventoryMoveRequests);
                UniqueListRemove(weapon, WeaponsToRemoveAmmoIndexer, WeaponsToRemoveAmmo);
            }
            AmmoToRemoveQueue.ApplyAdditions();
        }

        internal void RemoveAmmo()
        {
            for (int i = 0; i < AmmoToRemoveQueue.Count; i++) {
                try {

                    var request = AmmoToRemoveQueue[i];
                    var weapon = request.Weapon;
                    if (!weapon.Comp.InventoryInited){
                        request.Inventories.Clear();
                        request.Weapon = null;
                        AmmoToRemoveQueue.Remove(request);
                        continue;
                    }

                    var inventoriesToAddTo = request.Inventories;
                    var def = weapon.ActiveAmmoDef.AmmoDefinitionId;
                    var magItem = weapon.ActiveAmmoDef.AmmoDef.Const.AmmoItem;

                    weapon.ChangeActiveAmmo(weapon.System.AmmoTypes[weapon.Set.AmmoTypeId]);
                    for (int j = 0; j < inventoriesToAddTo.Count; j++) {
                        var amt = inventoriesToAddTo[i].Amount;
                        weapon.Comp.BlockInventory.RemoveItemsOfType(amt, def);
                        inventoriesToAddTo[i].Inventory.Add(magItem, amt);
                    }

                    WepUi.SetDps(weapon.Comp, weapon.Comp.Set.Value.DpsModifier, false, true);

                    ComputeStorage(weapon);

                    request.Inventories.Clear();
                    request.Weapon = null;
                    InventoryMoveRequestPool.Return(request);
                    AmmoToRemoveQueue.Remove(request);
                }
                catch (Exception ex) { Log.Line($"Exception in RemoveAmmo: {ex} - { AmmoToRemoveQueue[i] == null} - {AmmoToRemoveQueue[i]?.Weapon == null} - {AmmoToRemoveQueue[i]?.Weapon?.ActiveAmmoDef == null}"); }
            }
            AmmoToRemoveQueue.ApplyRemovals();
        }
    }
}
