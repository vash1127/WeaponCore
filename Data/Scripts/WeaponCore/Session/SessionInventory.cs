using Sandbox.Game;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
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
            if (weapon.System.DesignatorWeapon) return;

            if (!comp.Session.IsClient)
            {
                if (!comp.MyCube.HasInventory) return;

                var ammo = weapon.ActiveAmmoDef;

                if (!ammo.AmmoDef.Const.EnergyAmmo)
                {
                    if (!comp.Session.IsCreative)
                    {
                        weapon.State.Sync.CurrentMags = comp.BlockInventory.GetItemAmount(ammo.AmmoDefinitionId);

                        weapon.CurrentAmmoVolume = (float)weapon.State.Sync.CurrentMags * weapon.ActiveAmmoDef.AmmoDef.Const.MagVolume;

                        if (weapon.CanReload)
                            weapon.StartReload();

                        if (weapon.CurrentAmmoVolume < 0.25f * weapon.System.MaxAmmoVolume)
                        {
                            weapon.Comp.Session.WeaponToPullAmmo.Add(weapon);
                            weapon.Comp.Session.WeaponToPullAmmo.ApplyAdditions();
                        }
                    }
                    else if (weapon.CanReload)
                        weapon.StartReload();
                }
                else if (weapon.CanReload)
                    weapon.StartReload();
            }
            else if (weapon.State.Sync.CurrentAmmo == 0 && !weapon.ActiveAmmoDef.AmmoDef.Const.EnergyAmmo)
            {
                weapon.Comp.Session.ClientAmmoCheck.Add(weapon);
                weapon.Comp.Session.ClientAmmoCheck.ApplyAdditions();
            }
        }

        internal void AmmoPull() {
            var cachedInv = CachedInvPullDictPool.Get();
            var tmpInventories = TmpInventoryListPool.Get();
            try
            {
                for (int i = 0; i < WeaponToPullAmmo.Count; i++)
                {

                    var weapon = WeaponToPullAmmo[i];
                    using (weapon.Comp.Ai?.MyGrid.Pin())
                    using (weapon.Comp.MyCube.Pin())
                    {
                        if (weapon.Comp.MyCube.MarkedForClose || weapon.Comp.Ai == null || weapon.Comp.Ai.MyGrid.MarkedForClose || !weapon.Comp.InventoryInited || weapon.Comp.Platform.State != MyWeaponPlatform.PlatformState.Ready || weapon.Comp.MyCube == null)
                        {
                            tmpInventories.Clear();
                            WeaponToPullAmmo.Remove(weapon);
                            continue;
                        }

                        var def = weapon.ActiveAmmoDef.AmmoDefinitionId;
                        var fullAmount = 0.75f * weapon.System.MaxAmmoVolume;
                        var weaponInventory = weapon.Comp.BlockInventory;
                        var magsNeeded = (int)((fullAmount - weapon.CurrentAmmoVolume) / weapon.ActiveAmmoDef.AmmoDef.Const.MagVolume);
                        var magsAdded = 0;

                        if (magsNeeded == 0 && weapon.System.MaxAmmoVolume > weapon.ActiveAmmoDef.AmmoDef.Const.MagVolume)
                            magsNeeded = 1;

                        if (!cachedInv.ContainsKey(def))
                        {
                            cachedInv[def] = CachedInvDefDictPool.Get();
                            foreach (var inventory in weapon.Comp.Ai.Inventories)
                            {
                                var items = inventory.GetItems();
                                for (int j = 0; j < items.Count; j++)
                                {
                                    var item = items[j];
                                    var ammoMag = item.Content as MyObjectBuilder_AmmoMagazine;
                                    if (ammoMag != null && ammoMag.GetObjectId() == def)
                                    {
                                        cachedInv[def][inventory] = item.Amount;
                                        tmpInventories.Add(inventory);
                                    }
                                }
                            }
                        }
                        else if(cachedInv[def].Keys.Count > 0)
                            tmpInventories = cachedInv[def].Keys.ToList();


                        if (tmpInventories.Count <= 0)
                        {
                            tmpInventories.Clear();
                            WeaponToPullAmmo.Remove(weapon);
                            continue;
                        }

                        var ammoPullRequests = InventoryMoveRequestPool.Get();
                        ammoPullRequests.Weapon = weapon;

                        for (int j = 0; j < tmpInventories.Count; j++)
                        {
                            var inventory = tmpInventories[j];

                            if (!cachedInv[def].ContainsKey(inventory)) continue;

                            var magsAvailable = (int)cachedInv[def][inventory];

                            if (((IMyInventory)inventory).CanTransferItemTo(weaponInventory, def))
                            {
                                if (magsAvailable >= magsNeeded)
                                {
                                    ammoPullRequests.Inventories.Add(new InventoryMags { Inventory = inventory, Amount = magsNeeded });
                                    magsAdded += magsNeeded;
                                    magsNeeded = 0;
                                }
                                else
                                {
                                    ammoPullRequests.Inventories.Add(new InventoryMags { Inventory = inventory, Amount = magsAvailable });
                                    magsNeeded -= magsAvailable;
                                    magsAdded += magsAvailable;
                                    cachedInv[def].Remove(inventory);
                                }
                                weapon.CurrentAmmoVolume += magsAdded * weapon.ActiveAmmoDef.AmmoDef.Const.MagVolume;

                                cachedInv[def][inventory] -= magsAdded;
                            }

                            if (magsNeeded <= 0)
                                break;
                        }

                        tmpInventories.Clear();

                        if (ammoPullRequests.Inventories.Count > 0)
                            AmmoToPullQueue.Add(ammoPullRequests);
                        else
                            InventoryMoveRequestPool.Return(ammoPullRequests);

                        weapon.Comp.Session.AmmoPulls++;
                    }

                    WeaponToPullAmmo.Remove(weapon);
                }
                AmmoToPullQueue.ApplyAdditions();
                TmpInventoryListPool.Return(tmpInventories);
                WeaponToPullAmmo.ApplyRemovals();

                foreach (var returnDict in cachedInv)
                {
                    returnDict.Value.Clear();
                    CachedInvDefDictPool.Return(returnDict.Value);
                }
                cachedInv.Clear();
                CachedInvPullDictPool.Return(cachedInv);
            }
            catch (Exception e)
            {
                Log.ThreadedWrite($"Exception In Pull: {e}");
                WeaponToPullAmmo.ClearList();
                WeaponToPullAmmo.ApplyChanges();
            }
        }

        internal void MoveAmmo()
        {
            for (int i = 0; i < AmmoToPullQueue.Count; i ++)
            {
                var weaponAmmoToPull = AmmoToPullQueue[i];
                var weapon = weaponAmmoToPull.Weapon;
                var inventoriesToPull = weaponAmmoToPull.Inventories;
                if (!weapon.Comp.InventoryInited || weapon == null || weapon.Comp == null || weapon.Comp.Platform.State != MyWeaponPlatform.PlatformState.Ready)
                {
                    inventoriesToPull.Clear();
                    weaponAmmoToPull.Weapon = null;
                    InventoryMoveRequestPool.Return(weaponAmmoToPull);
                    continue;
                }
                
                var def = weapon.ActiveAmmoDef.AmmoDefinitionId;
                var magItem = weapon.ActiveAmmoDef.AmmoDef.Const.AmmoItem;

                weapon.Comp.IgnoreInvChange = true;

                for (int j = 0; j < inventoriesToPull.Count; j++)
                {
                    var amt = inventoriesToPull[j].Amount;
                    inventoriesToPull[j].Inventory.RemoveItemsOfType(amt, def);
                    weapon.Comp.BlockInventory.Add(magItem, amt);
                }

                if (inventoriesToPull.Count > 0 && weapon.CanReload)
                    weapon.StartReload();

                weapon.State.Sync.CurrentMags = weapon.Comp.BlockInventory.GetItemAmount(weapon.ActiveAmmoDef.AmmoDefinitionId);

                inventoriesToPull.Clear();
                weaponAmmoToPull.Weapon = null;
                InventoryMoveRequestPool.Return(weaponAmmoToPull);

                weapon.Comp.IgnoreInvChange = false;

                AmmoToPullQueue.Remove(weaponAmmoToPull);
            }
            AmmoToPullQueue.ApplyRemovals();
        }

        internal class InventoryVolumes
        {
            internal float CurrentVolume;
            internal float MaxVolume;
        }

        internal void AmmoToRemove()
        {
            var cachedInventories = CachedInvRemoveDictPool.Get();
            
            for (int i = 0; i < WeaponsToRemoveAmmo.Count; i++)
            {
                var weapon = WeaponsToRemoveAmmo[i];
                var def = weapon.ActiveAmmoDef.AmmoDefinitionId;
                var magItem = weapon.ActiveAmmoDef.AmmoDef.Const.AmmoItem;
                var ai = weapon.Comp.Ai;
                var itemVolume = weapon.ActiveAmmoDef.AmmoDef.Const.MagVolume;
                var magsToRemove = (int)weapon.Comp.BlockInventory.GetItemAmount(def);
                var inventoryMoveRequests = InventoryMoveRequestPool.Get();
                

                foreach (var inventory in ai.Inventories)
                {
                    if (!cachedInventories.ContainsKey(inventory))
                        cachedInventories[inventory] = (float)inventory.CurrentVolume;

                    if (((IMyInventory)weapon.Comp.BlockInventory).CanTransferItemTo(inventory, def) && ((float)inventory.MaxVolume - cachedInventories[inventory]) > itemVolume)
                    {

                        var canMove = (int)Math.Floor((float)(inventory.MaxVolume - inventory.CurrentVolume) / itemVolume);

                        if (canMove >= (double)magsToRemove)
                        {
                            inventoryMoveRequests.Inventories.Add(new InventoryMags { Inventory = inventory, Amount = magsToRemove });
                            magsToRemove = 0;
                            break;
                        }
                        else
                        {
                            inventoryMoveRequests.Inventories.Add(new InventoryMags { Inventory = inventory, Amount = canMove });
                            magsToRemove -= canMove;
                        }
                        

                        cachedInventories[inventory] += canMove * itemVolume;
                    }
                }

                AmmoToRemoveQueue.Add(inventoryMoveRequests);
                WeaponsToRemoveAmmo.Remove(weapon);
            }
            AmmoToRemoveQueue.ApplyAdditions();
            WeaponsToRemoveAmmo.ApplyRemovals();
            cachedInventories.Clear();
            CachedInvRemoveDictPool.Return(cachedInventories);
        }

        internal void RemoveAmmo()
        {
            for (int i = 0; i < AmmoToRemoveQueue.Count; i++)
            {
                var request = AmmoToRemoveQueue[i];
                var weapon = request.Weapon;
                if (!weapon.Comp.InventoryInited)
                {
                    request.Inventories.Clear();
                    request.Weapon = null;
                    AmmoToRemoveQueue.Remove(request);
                    continue;
                }

                var inventoriesToAddTo = request.Inventories;
                var def = weapon.ActiveAmmoDef.AmmoDefinitionId;
                var magItem = weapon.ActiveAmmoDef.AmmoDef.Const.AmmoItem;

                weapon.Comp.IgnoreInvChange = true;
                
                weapon.ActiveAmmoDef = weapon.System.WeaponAmmoTypes[weapon.Set.AmmoTypeId];

                for (int j = 0; j < inventoriesToAddTo.Count; j++)
                {
                    var amt = inventoriesToAddTo[i].Amount;
                    weapon.Comp.BlockInventory.RemoveItemsOfType(amt, def);
                    inventoriesToAddTo[i].Inventory.Add(magItem, amt);
                }

                WepUi.SetDps(weapon.Comp, weapon.Comp.Set.Value.DpsModifier, false, true);
                weapon.Comp.IgnoreInvChange = false;

                ComputeStorage(weapon);

                request.Inventories.Clear();
                request.Weapon = null;
                InventoryMoveRequestPool.Return(request);
                AmmoToRemoveQueue.Remove(request);
            }
            AmmoToRemoveQueue.ApplyRemovals();
        }

        internal class InventoryCompare : IEqualityComparer<MyInventory>
        {
            public bool Equals(MyInventory x, MyInventory y)
            {
                if (ReferenceEquals(x, y))
                    return true;
                else
                    return false;
            }
            public int GetHashCode(MyInventory inventory)
            {
                return inventory.GetHashCode();
            }
        }
    }
}
