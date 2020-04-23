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

                        if (weapon.CurrentAmmoVolume < 0.25f * weapon.System.MaxAmmoVolume)
                            weapon.Comp.Session.WeaponAmmoPullQueue.Enqueue(weapon);
                        else if (weapon.CanReload)
                            weapon.StartReload();
                    }
                    else if (weapon.CanReload)
                        weapon.StartReload();
                }
                else if (weapon.CanReload)
                    weapon.StartReload();
            }
            else if(weapon.State.Sync.CurrentAmmo == 0 && !weapon.ActiveAmmoDef.AmmoDef.Const.EnergyAmmo)
                comp.Session.MTask = MyAPIGateway.Parallel.Start(weapon.GetAmmoClient, weapon.ReloadClient);
        }

        internal void AmmoPull() {

            var cachedInv = new Dictionary<MyDefinitionId, Dictionary<MyInventory, MyFixedPoint>>();
            var tmpInventories = new List<MyInventory>();

            Weapon weapon;
            
            while (WeaponAmmoPullQueue.TryDequeue(out weapon))
            {
                using (weapon.Comp.Ai?.MyGrid.Pin())
                using (weapon.Comp.MyCube.Pin())
                {
                    if (weapon.Comp.MyCube.MarkedForClose || weapon.Comp.Ai == null || weapon.Comp.Ai.MyGrid.MarkedForClose || !weapon.Comp.InventoryInited || weapon.Comp.Platform.State != MyWeaponPlatform.PlatformState.Ready || weapon.Comp.MyCube == null) continue;
                    var def = weapon.ActiveAmmoDef.AmmoDefinitionId;
                    var fullAmount = 0.75f * weapon.System.MaxAmmoVolume;
                    var weaponInventory = weapon.Comp.BlockInventory;
                    var magsNeeded = (int)((fullAmount - weapon.CurrentAmmoVolume) / weapon.ActiveAmmoDef.AmmoDef.Const.MagVolume);
                    var magsAdded = 0;

                    if (magsNeeded == 0 && weapon.System.MaxAmmoVolume > weapon.ActiveAmmoDef.AmmoDef.Const.MagVolume)
                        magsNeeded = 1;

                    if (!cachedInv.ContainsKey(def))
                    {
                        cachedInv[def] = CachedInvDictPool.Get();
                        foreach (var inventory in weapon.Comp.Ai.Inventories)
                        {
                            var items = inventory.GetItems();
                            for (int i = 0; i < items.Count; i++)
                            {
                                var item = items[i];
                                var ammoMag = item.Content as MyObjectBuilder_AmmoMagazine;
                                if (ammoMag != null && ammoMag.GetObjectId() == def)
                                {
                                    cachedInv[def][inventory] = item.Amount;
                                    tmpInventories.Add(inventory);
                                }
                            }
                        }
                    }
                    else
                        tmpInventories = cachedInv[def].Keys.ToList();

                    if (tmpInventories.Count <= 0) continue;

                    var ammoPullRequests = InventoryMoveRequestPool.Get();
                    ammoPullRequests.weapon = weapon;

                    for (int i = 0; i < tmpInventories.Count; i++)
                    {
                        var inventory = tmpInventories[i];
                        var magsAvailable = (int)cachedInv[def][inventory];

                        Log.Line($"magsAvailable: {magsAvailable}");

                        if (((IMyInventory)inventory).CanTransferItemTo(weaponInventory, def))
                        {
                            var invMags = InventoryMoveInvMagsPool.Get();
                            if (magsAvailable >= magsNeeded)
                            {                                
                                invMags.Inventory = inventory;
                                invMags.Amount = magsNeeded;
                                ammoPullRequests.Inventories.Add(invMags);
                                magsAdded += magsNeeded;
                                magsNeeded = 0;                                    
                            }
                            else
                            {
                                invMags.Inventory = inventory;
                                invMags.Amount = magsAvailable;
                                ammoPullRequests.Inventories.Add(invMags);
                                magsNeeded -= magsAvailable;
                                magsAdded += magsAvailable;
                                cachedInv[def].Remove(inventory);
                            }
                            weapon.CurrentAmmoVolume += magsAdded * weapon.ActiveAmmoDef.AmmoDef.Const.MagVolume;

                            Log.Line($"weapon.CurrentAmmoVolume: {weapon.CurrentAmmoVolume}");

                            cachedInv[def][inventory] -= magsAdded;
                        }

                        if (magsNeeded <= 0)
                            break;
                    }

                    tmpInventories.Clear();

                    if (ammoPullRequests.Inventories.Count > 0)
                        AmmoToPullQueue.Enqueue(ammoPullRequests);
                    else
                        InventoryMoveRequestPool.Return(ammoPullRequests);

                    weapon.Comp.Session.AmmoPulls++;
                }
            }

            foreach (var returnDict in cachedInv)
            {
                returnDict.Value.Clear();
                CachedInvDictPool.Return(returnDict.Value);
            }
            cachedInv.Clear();
        }

        internal void MoveAmmo()
        {
            WeaponAmmoMoveRequest weaponAmmoToPull;
            while (AmmoToPullQueue.TryDequeue(out weaponAmmoToPull))
            {
                var weapon = weaponAmmoToPull.weapon;
                var inventoriesToPull = weaponAmmoToPull.Inventories;
                if (!weapon.Comp.InventoryInited || weapon == null || weapon.Comp == null || weapon.Comp.Platform.State != MyWeaponPlatform.PlatformState.Ready)
                {
                    for (int i = 0; i < inventoriesToPull.Count; i++)
                        InventoryMoveInvMagsPool.Return(inventoriesToPull[i]);

                    inventoriesToPull.Clear();
                    weaponAmmoToPull.weapon = null;
                    InventoryMoveRequestPool.Return(weaponAmmoToPull);
                    continue;
                }
                
                var def = weapon.ActiveAmmoDef.AmmoDefinitionId;
                var magItem = weapon.ActiveAmmoDef.AmmoDef.Const.AmmoItem;

                weapon.Comp.IgnoreInvChange = true;

                for (int i = 0; i < inventoriesToPull.Count; i++)
                {

                    var amt = inventoriesToPull[i].Amount;
                    inventoriesToPull[i].Inventory.RemoveItemsOfType(amt, def);
                    weapon.Comp.BlockInventory.Add(magItem, amt);

                    InventoryMoveInvMagsPool.Return(inventoriesToPull[i]);
                }

                if (inventoriesToPull.Count > 0)
                {
                    weapon.State.Sync.CurrentMags = weapon.Comp.BlockInventory.GetItemAmount(weapon.ActiveAmmoDef.AmmoDefinitionId);
                    if (weapon.CanReload)
                        weapon.StartReload();
                }

                inventoriesToPull.Clear();
                weaponAmmoToPull.weapon = null;
                InventoryMoveRequestPool.Return(weaponAmmoToPull);

                weapon.Comp.IgnoreInvChange = false;
            }
        }

        internal class InventoryVolumes
        {
            internal float CurrentVolume;
            internal float MaxVolume;
        }

        internal void AmmoToRemove()
        {
            var cachedInventories = new Dictionary<MyInventory, float>();

            Weapon weapon;
            while (WeaponAmmoRemoveQueue.TryDequeue(out weapon))
            {
                var def = weapon.ActiveAmmoDef.AmmoDefinitionId;
                var magItem = weapon.ActiveAmmoDef.AmmoDef.Const.AmmoItem;
                var session = weapon.Comp.Session;
                var ai = weapon.Comp.Ai;

                var magsToRemove = (int)weapon.Comp.BlockInventory.GetItemAmount(def);
                float itemMass;
                float itemVolume;

                MyInventory.GetItemVolumeAndMass(def, out itemMass, out itemVolume);

                var magVolume = itemVolume * magsToRemove;

                List<MyTuple<MyInventory, int>> inventories = new List<MyTuple<MyInventory, int>>();

                foreach (var inventory in ai.Inventories)
                {
                    if (!cachedInventories.ContainsKey(inventory))
                        cachedInventories[inventory] = (float)inventory.CurrentVolume;

                    if (((IMyInventory)weapon.Comp.BlockInventory).CanTransferItemTo(inventory, def) && ((float)inventory.MaxVolume - cachedInventories[inventory]) > itemVolume)
                    {

                        var canMove = (int)Math.Floor((float)(inventory.MaxVolume - inventory.CurrentVolume) / itemVolume);

                        if (canMove >= (double)magsToRemove)
                        {
                            inventories.Add(new MyTuple<MyInventory, int>{Item1 = inventory, Item2 = magsToRemove });
                            magsToRemove = 0;
                            break;
                        }
                        else
                        {
                            inventories.Add(new MyTuple<MyInventory, int> { Item1 = inventory, Item2 = canMove });
                            magsToRemove -= canMove;
                        }
                        

                        cachedInventories[inventory] += canMove * itemVolume;
                    }
                }

                session.AmmoToRemoveQueue.Enqueue(new MyTuple<Weapon, MyTuple<MyInventory, int>[]>
                {
                    Item1 = weapon,
                    Item2 = inventories.ToArray(),
                });
            }
            cachedInventories.Clear();
        }

        internal void RemoveAmmo()
        {
            MyTuple<Weapon, MyTuple<MyInventory, int>[]> weaponAmmoToPull;
            while (AmmoToRemoveQueue.TryDequeue(out weaponAmmoToPull))
            {
                var weapon = weaponAmmoToPull.Item1;
                if (!weapon.Comp.InventoryInited) continue;
                var inventoriesToAddTo = weaponAmmoToPull.Item2;
                var def = weapon.ActiveAmmoDef.AmmoDefinitionId;
                var magItem = weapon.ActiveAmmoDef.AmmoDef.Const.AmmoItem;

                weapon.Comp.IgnoreInvChange = true;
                
                weapon.ActiveAmmoDef = weapon.System.WeaponAmmoTypes[weapon.Set.AmmoTypeId];

                for (int i = 0; i < inventoriesToAddTo.Length; i++)
                {
                    var amt = inventoriesToAddTo[i].Item2;
                    weapon.Comp.BlockInventory.RemoveItemsOfType(amt, def);
                    inventoriesToAddTo[i].Item1.Add(magItem, amt);
                }

                WepUi.SetDps(weapon.Comp, weapon.Comp.Set.Value.DpsModifier, false, true);
                weapon.Comp.IgnoreInvChange = false;

                ComputeStorage(weapon);
            }
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
