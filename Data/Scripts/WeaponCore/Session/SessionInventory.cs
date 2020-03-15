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

            if (!comp.Session.IsClient)
            {
                if (!comp.MyCube.HasInventory) return;

                var ammo = weapon.ActiveAmmoDef;

                if (!ammo.AmmoDef.Const.EnergyAmmo)
                {
                    var invWithMagsAvailable = comp.Ai.AmmoInventories[ammo.AmmoDefinitionId];

                    weapon.State.Sync.CurrentMags = comp.BlockInventory.GetItemAmount(ammo.AmmoDefinitionId);
                    
                    weapon.CurrentAmmoVolume = (float)weapon.State.Sync.CurrentMags * weapon.ActiveAmmoDef.AmmoDef.Const.MagVolume;

                    if (weapon.CurrentAmmoVolume < 0.25f * weapon.System.MaxAmmoVolume && invWithMagsAvailable.Count > 0)
                        weapon.Comp.Session.WeaponAmmoPullQueue.Enqueue(weapon);
                }

                if(weapon.State.Sync.CurrentMags > 0 || comp.Session.IsCreative)
                    weapon.CheckReload();
            }
            else
                comp.Session.MTask = MyAPIGateway.Parallel.Start(weapon.GetAmmoClient);
        }

        internal void AmmoPull() {

            var cachedInv = new Dictionary<MyDefinitionId, Dictionary<MyInventory, MyFixedPoint>>();

            Weapon weapon;
            while (WeaponAmmoPullQueue.TryDequeue(out weapon))
            {
                using (weapon.Comp.Ai?.MyGrid.Pin())
                using (weapon.Comp.MyCube.Pin())
                {
                    if (weapon.Comp.MyCube.MarkedForClose || weapon.Comp.Ai == null || weapon.Comp.Ai.MyGrid.MarkedForClose || !weapon.Comp.InventoryInited || weapon.Comp.Platform.State != MyWeaponPlatform.PlatformState.Ready || weapon.Comp.MyCube == null) continue;
                    var def = weapon.ActiveAmmoDef.AmmoDefinitionId;
                    float itemMass;
                    float itemVolume;

                    MyInventory.GetItemVolumeAndMass(def, out itemMass, out itemVolume);

                    var fullAmount = 0.75f * weapon.System.MaxAmmoVolume;
                    var weaponInventory = weapon.Comp.BlockInventory;
                    var magsNeeded = (int)((fullAmount - weapon.CurrentAmmoVolume) / itemVolume);

                    if (magsNeeded == 0 && weapon.System.MaxAmmoVolume > itemVolume)
                        magsNeeded = 1;

                    var magsAdded = 0;
                    lock (weapon.Comp.Ai.AmmoInventories[def])
                    {
                        List<MyTuple<MyInventory, int>> inventories = new List<MyTuple<MyInventory, int>>();

                        if(!cachedInv.ContainsKey(def))
                            cachedInv[def] = weapon.Comp.Ai.AmmoInventories[def].ToDictionary(kvp => kvp.Key, kvp => kvp.Value, new InventoryCompare());

                        foreach (var currentInventory in weapon.Comp.Ai.AmmoInventories[def])
                        {
                            var inventory = currentInventory.Key;
                            var magsAvailable = (int)cachedInv[def][inventory];

                            if (((IMyInventory)inventory).CanTransferItemTo(weaponInventory, def))
                            {
                                if (magsAvailable >= magsNeeded)
                                {
                                    inventories.Add(new MyTuple<MyInventory, int> { Item1 = inventory, Item2 = magsNeeded });
                                    magsAdded += magsNeeded;
                                    magsNeeded = 0;                                    
                                }
                                else
                                {
                                    inventories.Add(new MyTuple<MyInventory, int> { Item1 = inventory, Item2 = magsAvailable });
                                    magsNeeded -= magsAvailable;
                                    magsAdded += magsAvailable;
                                }

                                cachedInv[def][inventory] -= magsAdded;
                            }
                        }
                        weapon.CurrentAmmoVolume += magsAdded * itemVolume;

                        if (inventories.Count > 0)
                            AmmoToPullQueue.Enqueue(new MyTuple<Weapon, MyTuple<MyInventory, int>[]> { Item1 = weapon, Item2 = inventories.ToArray() });

                        weapon.Comp.Session.AmmoPulls++;
                    }
                }
            }
            cachedInv.Clear();
        }

        internal void MoveAmmo()
        {
            MyTuple<Weapon, MyTuple<MyInventory, int>[]> weaponAmmoToPull;
            while (AmmoToPullQueue.TryDequeue(out weaponAmmoToPull))
            {
                var weapon = weaponAmmoToPull.Item1;
                if (!weapon.Comp.InventoryInited) continue;
                var inventoriesToPull = weaponAmmoToPull.Item2;
                var def = weapon.ActiveAmmoDef.AmmoDefinitionId;
                var magItem = weapon.ActiveAmmoDef.AmmoDef.Const.AmmoItem;

                weapon.Comp.IgnoreInvChange = true;

                for (int i = 0; i < inventoriesToPull.Length; i++)
                {
                    var amt = inventoriesToPull[i].Item2;
                    inventoriesToPull[i].Item1.RemoveItemsOfType(amt, def);
                    weapon.Comp.BlockInventory.Add(magItem, amt);
                }

                if(inventoriesToPull.Length > 0)
                {
                    weapon.State.Sync.CurrentMags = weapon.Comp.BlockInventory.GetItemAmount(weapon.ActiveAmmoDef.AmmoDefinitionId);
                    weapon.CheckReload();
                }

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
                
                weapon.State.Sync.Reloading = false;
                ComputeStorage(weapon);

                weapon.Comp.IgnoreInvChange = false;
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
