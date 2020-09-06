using System;
using System.Collections.Generic;
using VRage.Game.ModAPI;
using WeaponCore.Platform;
using WeaponCore.Support;

namespace WeaponCore
{
    public partial class Session
    {
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
                            InvPullClean.Add(weapon);
                            continue;
                        }

                        var defId = weapon.ActiveAmmoDef.AmmoDefinitionId;
                        var freeSpace = weapon.System.MaxAmmoVolume - weapon.Comp.CurrentInventoryVolume;
                        var spotsFree = (int)(freeSpace / weapon.ActiveAmmoDef.AmmoDef.Const.MagVolume);
                        var magsNeeded = (int)((weapon.System.FullAmmoVolume - weapon.CurrentAmmoVolume) / weapon.ActiveAmmoDef.AmmoDef.Const.MagVolume);
                        magsNeeded = magsNeeded > spotsFree ? spotsFree : magsNeeded;

                        var ammoPullRequests = InventoryMoveRequestPool.Get();
                        ammoPullRequests.Weapon = weapon;
                        var magsAdded = 0;

                        for (int j = 0; j < weapon.Comp.Ai.Inventories.Count; j++) {

                            var inventory = weapon.Comp.Ai.Inventories[j];
                            var items = AmmoThreadItemList[inventory];

                            for (int l = items.Count - 1; l >= 0; l--)
                            {
                                var item = items[l];

                                if (!item.DefId.Equals(defId)) continue;

                                var magsAvailable = item.Amount;

                                if (magsAvailable > 0 && magsNeeded > 0 && ((IMyInventory)inventory).CanTransferItemTo(weapon.Comp.BlockInventory, defId))
                                {
                                    if (magsAvailable >= magsNeeded)
                                    {
                                        ammoPullRequests.Inventories.Add(new InventoryMags { Inventory = inventory, Item = item, Amount = magsNeeded });
                                        magsAdded += magsNeeded;
                                        item.Amount -= magsNeeded;
                                        magsNeeded = 0;
                                    }
                                    else
                                    {
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

                        if (ammoPullRequests.Inventories.Count > 0)
                            AmmoToPullQueue.Add(ammoPullRequests);
                        else
                            InventoryMoveRequestPool.Return(ammoPullRequests);

                    }
                    InvPullClean.Add(weapon);
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
            for (int i = 0; i < AmmoToPullQueue.Count; i++)
            {
                var weaponAmmoToPull = AmmoToPullQueue[i];
                var weapon = weaponAmmoToPull.Weapon;
                var inventoriesToPull = weaponAmmoToPull.Inventories;
                if (!weapon.Comp.InventoryInited || weapon.Comp.Platform.State != MyWeaponPlatform.PlatformState.Ready)
                {
                    InventoryMoveRequestPool.Return(weaponAmmoToPull);
                    continue;
                }

                for (int j = 0; j < inventoriesToPull.Count; j++)
                {
                    var mag = inventoriesToPull[j];
                    var amt = mag.Amount;
                    var item = mag.Item;
                    if (weapon.Comp.BlockInventory.ItemsCanBeAdded(amt, weapon.ActiveAmmoDef.AmmoDef.Const.AmmoItem) && mag.Inventory.ItemsCanBeRemoved(amt, item.Item))
                    {
                        mag.Inventory.RemoveItems(item.Item.ItemId, amt);
                        weapon.Comp.BlockInventory.Add(weapon.ActiveAmmoDef.AmmoDef.Const.AmmoItem, amt);
                    }
                }

                weapon.Ammo.CurrentMags = weapon.Comp.BlockInventory.GetItemAmount(weapon.ActiveAmmoDef.AmmoDefinitionId).ToIntSafe();

                InventoryMoveRequestPool.Return(weaponAmmoToPull);
            }
            AmmoToPullQueue.Clear();
        }
    }
}
