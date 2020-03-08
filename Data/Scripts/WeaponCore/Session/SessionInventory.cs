using Sandbox.Game;
using Sandbox.ModAPI;
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
        internal static void ComputeStorage(Weapon weapon)
        {
            var comp = weapon.Comp;
            

            if (!comp.Session.IsClient)
            {
                if (!comp.MyCube.HasInventory) return;

                var def = weapon.System.WeaponAmmoTypes[weapon.Set.AmmoTypeId].AmmoDefinitionId;
                var invWithMagsAvailable = comp.Ai.AmmoInventories[def];

                weapon.State.Sync.CurrentMags = comp.BlockInventory.GetItemAmount(def);                

                weapon.CurrentAmmoVolume = (float)weapon.State.Sync.CurrentMags * weapon.ActiveAmmoDef.Const.MagVolume;

                if (weapon.CurrentAmmoVolume < 0.25f * weapon.System.MaxAmmoVolume && invWithMagsAvailable.Count > 0)
                    weapon.Comp.Session.WeaponAmmoPullQueue.Enqueue(weapon);

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
                    var def = weapon.System.WeaponAmmoTypes[weapon.Set.AmmoTypeId].AmmoDefinitionId;
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
        }

        internal void MoveAmmo()
        {
            MyTuple<Weapon, MyTuple<MyInventory, int>[]> weaponAmmoToPull;
            while (AmmoToPullQueue.TryDequeue(out weaponAmmoToPull))
            {
                var weapon = weaponAmmoToPull.Item1;
                if (!weapon.Comp.InventoryInited) continue;
                var inventoriesToPull = weaponAmmoToPull.Item2;
                var def = weapon.System.WeaponAmmoTypes[weapon.Set.AmmoTypeId].AmmoDefinitionId;
                var magItem = weapon.ActiveAmmoDef.Const.AmmoItem;

                weapon.Comp.IgnoreInvChange = true;

                for (int i = 0; i < inventoriesToPull.Length; i++)
                {
                    var amt = inventoriesToPull[i].Item2;
                    inventoriesToPull[i].Item1.RemoveItemsOfType(amt, def);
                    weapon.Comp.BlockInventory.Add(magItem, amt);
                }
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
