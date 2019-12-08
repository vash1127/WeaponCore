using Sandbox.Game;
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

            var lastMags = comp.State.Value.Weapons[weapon.WeaponId].CurrentMags;
            var invWithMagsAvailable = comp.Ai.AmmoInventories[def];

            comp.State.Value.Weapons[weapon.WeaponId].CurrentMags = comp.BlockInventory.GetItemAmount(def);
            weapon.CurrentAmmoVolume = (float)comp.State.Value.Weapons[weapon.WeaponId].CurrentMags * itemVolume;

            if (weapon.CurrentAmmoVolume < 0.25f * weapon.System.MaxAmmoVolume && invWithMagsAvailable.Count > 0)
                weapon.Comp.Ai.Session.WeaponAmmoPullQueue.Enqueue(weapon);

            if (lastMags == 0 && comp.State.Value.Weapons[weapon.WeaponId].CurrentMags > 0)
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

                var currentInventory = weapon.Comp.Ai.AmmoInventories[def].GetEnumerator();
                while (magsNeeded > 0 && currentInventory.MoveNext())
                {
                    var magsAvailable = (int)currentInventory.Current.Value;
                    var inventory = currentInventory.Current.Key;

                    if (((IMyInventory)inventory).CanTransferItemTo(weaponInventory, def))
                    {
                        if (magsAvailable > magsNeeded)
                        {
                            _inventoriesToPull.Add(new MyTuple<MyInventory, int> { Item1 = inventory, Item2 = magsNeeded });
                            magsNeeded = 0;
                            magsAdded += magsNeeded;
                        }
                        else
                        {
                            _inventoriesToPull.Add(new MyTuple<MyInventory, int> { Item1 = inventory, Item2 = magsAvailable });
                            magsNeeded -= magsAvailable;
                            magsAdded += magsAvailable;
                        }
                    }
                }
                currentInventory.Dispose();

                weapon.CurrentAmmoVolume += magsAdded * itemVolume;

                if (_inventoriesToPull.Count > 0)
                    AmmoToPullQueue.Enqueue(new MyTuple<Weapon, MyTuple<MyInventory, int>[]> {Item1 = weapon, Item2 = _inventoriesToPull.ToArray() });

                _inventoriesToPull.Clear();
                weapon.Comp.Ai.Session.AmmoPulls++;
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
                }
                weapon.Comp.IgnoreInvChange = false;
            }
        }
    }
}
