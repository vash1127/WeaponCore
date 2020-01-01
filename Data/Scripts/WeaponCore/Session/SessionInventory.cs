using Sandbox.Game;
using System.Collections.Generic;
using VRage;
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
            if (!comp.MyCube.HasInventory) return;
            var def = weapon.System.AmmoDefId;
            float itemMass;
            float itemVolume;

            MyInventory.GetItemVolumeAndMass(def, out itemMass, out itemVolume);

            var invWithMagsAvailable = comp.Ai.AmmoInventories[def];

            comp.State.Value.Weapons[weapon.WeaponId].CurrentMags = comp.BlockInventory.GetItemAmount(def);
            weapon.CurrentAmmoVolume = (float)comp.State.Value.Weapons[weapon.WeaponId].CurrentMags * itemVolume;

            if (weapon.CurrentAmmoVolume < 0.25f * weapon.System.MaxAmmoVolume && invWithMagsAvailable.Count > 0)
                weapon.Comp.Ai.Session.WeaponAmmoPullQueue.Enqueue(weapon);

            if (comp.State.Value.Weapons[weapon.WeaponId].CurrentAmmo == 0 && (weapon.System.MustCharge || comp.State.Value.Weapons[weapon.WeaponId].CurrentMags > 0 || comp.Ai.Session.IsCreative))
                weapon.StartReload();
        }

        internal void AmmoPull() {

            Weapon weapon;
            while (WeaponAmmoPullQueue.TryDequeue(out weapon))
            {
                if (!weapon.Comp.InventoryInited) continue;
                var def = weapon.System.AmmoDefId;
                float itemMass;
                float itemVolume;

                MyInventory.GetItemVolumeAndMass(def, out itemMass, out itemVolume);

                var fullAmount = 0.75f * weapon.System.MaxAmmoVolume;
                var weaponInventory = weapon.Comp.BlockInventory;
                var magsNeeded = (int)((fullAmount - weapon.CurrentAmmoVolume) / itemVolume);

                var magsAdded = 0;

                lock (weapon.Comp.Ai.AmmoInventories[def])
                {
                    List<MyTuple<MyInventory, int>> inventories = new List<MyTuple<MyInventory, int>>();
                    foreach(var currentInventory in weapon.Comp.Ai.AmmoInventories[def])
                    {
                        var magsAvailable = (int)currentInventory.Value;
                        var inventory = currentInventory.Key;

                        if (((IMyInventory)inventory).CanTransferItemTo(weaponInventory, def))
                        {
                            if (magsAvailable >= magsNeeded)
                            {
                                inventories.Add(new MyTuple<MyInventory, int> { Item1 = inventory, Item2 = magsNeeded });
                                magsNeeded = 0;
                                magsAdded += magsNeeded;
                            }
                            else
                            {
                                inventories.Add(new MyTuple<MyInventory, int> { Item1 = inventory, Item2 = magsAvailable });
                                magsNeeded -= magsAvailable;
                                magsAdded += magsAvailable;
                            }
                        }
                    }
                    weapon.CurrentAmmoVolume += magsAdded * itemVolume;

                    if (inventories.Count > 0)
                        AmmoToPullQueue.Enqueue(new MyTuple<Weapon, MyTuple<MyInventory, int>[]> { Item1 = weapon, Item2 = inventories.ToArray() });

                    weapon.Comp.Ai.Session.AmmoPulls++;
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
                var def = weapon.System.AmmoDefId;
                var magItem = weapon.System.AmmoItem;

                weapon.Comp.IgnoreInvChange = true;
                for (int i = 0; i < inventoriesToPull.Length; i++)
                {
                    var amt = inventoriesToPull[i].Item2;
                    inventoriesToPull[i].Item1.RemoveItemsOfType(amt, def);
                    weapon.Comp.BlockInventory.Add(magItem, amt);
                    weapon.Comp.Ai.CheckReload = true;
                }
                weapon.Comp.IgnoreInvChange = false;
                
            }
        }
    }
}
