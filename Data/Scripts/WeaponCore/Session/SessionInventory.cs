﻿using Sandbox.Game;
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

            if (!comp.Session.IsClient)
            {
                if (!comp.MyCube.HasInventory) return;

                var oldMags = weapon.State.CurrentMags;
                var def = weapon.System.AmmoDefId;
                var invWithMagsAvailable = comp.Ai.AmmoInventories[def];

                weapon.State.CurrentMags = comp.BlockInventory.GetItemAmount(def);                

                weapon.CurrentAmmoVolume = (float)weapon.State.CurrentMags * weapon.System.MagVolume;

                if (weapon.CurrentAmmoVolume < 0.25f * weapon.System.MaxAmmoVolume && invWithMagsAvailable.Count > 0)
                    weapon.Comp.Session.WeaponAmmoPullQueue.Enqueue(weapon);

                if (comp.Session.MpActive && comp.Session.IsServer && oldMags != weapon.State.CurrentMags && (oldMags > 0 && weapon.State.CurrentMags > 0))
                {
                    comp.Session.PacketsToClient.Add(new PacketInfo
                    {
                        Entity = comp.MyCube,
                        Packet = new MagUpdatePacket
                        {
                            EntityId = comp.MyCube.EntityId,
                            SenderId = 0,
                            Mags = weapon.State.CurrentMags,
                            PType = PacketType.MagUpdate,
                            WeaponId = weapon.WeaponId
                        }
                    });
                }
            }

            var hasMags = weapon.State.CurrentMags > 0;
            var chargeReload = weapon.System.MustCharge && (weapon.System.EnergyAmmo || hasMags);
            var standardReload = !weapon.System.MustCharge && !weapon.System.EnergyAmmo && hasMags;

            if (weapon.State.CurrentAmmo == 0 && (comp.Session.IsCreative || chargeReload || standardReload))
                weapon.StartReload();
        }

        internal void AmmoPull() {

            Weapon weapon;
            while (WeaponAmmoPullQueue.TryDequeue(out weapon))
            {
                using (weapon.Comp.Ai?.MyGrid.Pin())
                using (weapon.Comp.MyCube.Pin())
                {
                    if (weapon.Comp.MyCube.MarkedForClose || weapon.Comp.Ai == null || weapon.Comp.Ai.MyGrid.MarkedForClose || !weapon.Comp.InventoryInited || weapon.Comp.Platform.State != MyWeaponPlatform.PlatformState.Ready || weapon.Comp.MyCube == null) continue;
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

                        foreach (var currentInventory in weapon.Comp.Ai.AmmoInventories[def])
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
