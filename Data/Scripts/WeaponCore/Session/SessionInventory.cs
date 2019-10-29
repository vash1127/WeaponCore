using Sandbox.Game;
using Sandbox.ModAPI;
using System.Collections.Generic;
using VRage;
using VRage.Game.ModAPI;
using WeaponCore.Platform;

namespace WeaponCore
{
    public partial class Session
    {
        /*
        private void UpdateBlockInventories()
        {
            InventoryChange change;
            while (InventoryEvent.TryDequeue(out change))
            {
                var weapon = change.Weapon;
                var comp = weapon.Comp;
                var add = ComputeStorage(weapon);
                if (comp.MultiInventory && add)
                {
                    Log.Line("add mag");
                    var nextDefRaw = NextActiveAmmoDef(comp, weapon);
                    //if (!nextDefRaw.HasValue) return;
                }
            }
        }*/

        /*private static void AmmoPull(WeaponComponent comp, Weapon weapon, bool suspend)
        {
            //Log.Line($"[ammo pull] suspend:{suspend}(was:{weapon.AmmoSuspend}) weaponId:{weapon.WeaponId} - weaponDef:{weapon.System.AmmoDefId.SubtypeId.String} - Full:{weapon.AmmoFull} - weaponSuspendAge:{weapon.SuspendAmmoTick} - weaponUnSuspendAge:{weapon.UnSuspendAmmoTick} - multi:{comp.MultiInventory} - ");
            /*weapon.AmmoSuspend = suspend;

            if (suspend) NextActiveAmmoDef(comp, weapon, true);
            else
            {
                ComputeStorage(weapon);
                weapon.SuspendAmmoTick = 0;
                weapon.UnSuspendAmmoTick = 0;
                comp.LastAmmoUnSuspendTick = comp.Ai.Session.Tick;
                if (!comp.FullInventory && !weapon.AmmoFull)
                    NextActiveAmmoDef(comp, weapon, false);
            }

            if (suspend) comp.PullingAmmoCnt--;
            else comp.PullingAmmoCnt++;*/
        //}

        /*
        private static MyDefinitionId? NextActiveAmmoDef(WeaponComponent comp, Weapon oldWeapon, bool skipOld = false)
        {
            var ammoToWeaponIds = comp.Platform.Structure.AmmoToWeaponIds;
            var end = ammoToWeaponIds.Count;
            var index = 0;

            var firstId = -1;
            var validId = -1;
            var nextId = -1;
            var firstDef = new MyDefinitionId();
            var validDef = new MyDefinitionId();
            var nextDef = new MyDefinitionId();

            var firstFound = false;
            var returnNext = false;
            var nextFound = false;
            var validFound = false;

            foreach (var pair in ammoToWeaponIds)
            {
                index++;
                var weaponId = pair.Value[0];
                var ammoDef = pair.Key;
                var weapon = comp.Platform.Weapons[weaponId];
                if (weapon.AmmoSuspend || weapon.AmmoFull) continue;

                validFound = true;
                validDef = ammoDef;
                validId = weaponId;
                if (returnNext) // found weapon last iteration, return this.
                {
                    nextDef = ammoDef;
                    nextId = weaponId;
                    nextFound = true;
                    //Log.Line($"[returning next] new:{ammoDef.SubtypeId.String} - old:{oldWeapon.WeaponSystem.AmmoDefId.SubtypeId.String} - nextId:{nextId} - Sus/UnSus:{weapon.SuspendAmmoTick}/{weapon.UnSuspendAmmoTick}");
                    break;
                }

                if (index == 1) // save for if oldweapon is last but not only
                {
                    firstDef = ammoDef; 
                    firstId = weaponId;
                    firstFound = true;
                }

                if (weapon == oldWeapon)
                {
                    if (index == end && index != 1 && firstFound) // last but not only
                    {
                        nextDef = firstDef;
                        nextId = firstId;
                        nextFound = true;
                        //Log.Line($"[returning first] new:{firstDef.SubtypeId.String} - old:{oldWeapon.WeaponSystem.AmmoDefId.SubtypeId.String} - firstId:{firstId} - Sus/UnSus:{weapon.SuspendAmmoTick}/{weapon.UnSuspendAmmoTick}");
                        break;
                    }
                    returnNext = true;
                }
            }

            oldWeapon.SuspendAmmoTick = 0;
            oldWeapon.UnSuspendAmmoTick = 0;
            if (nextFound || validFound && (!skipOld || validId != oldWeapon.WeaponId))
            {
                if (!nextFound) // loop had valid def, but did not pick, return the valid def.
                {
                    nextDef = validDef;
                    nextId = validId;
                }

                comp.BlockInventory.Constraint.Clear();
                comp.BlockInventory.Constraint.Add(nextDef);
                //TODO ammo fix gunbase
                //comp.Gun.GunBase.SwitchAmmoMagazine(nextDef);
                foreach (var a in comp.Platform.Structure.AmmoToWeaponIds)
                {
                    var def = a.Key;
                    //Log.Line($"constraint: {def.SubtypeId.String} - isNext:{def == nextDef} - isFull:{testWeapon.AmmoFull}");
                    if (def == nextDef) continue;
                    comp.BlockInventory.Constraint.Add(def);
                }
                var newWeapon = comp.Platform.Weapons[nextId];
                Log.Line($"[sending nextDef] next:{nextDef.SubtypeId.String} - last:{oldWeapon.System.AmmoDefId.SubtypeId.String} - Full:{oldWeapon.AmmoFull} - oldWeaponId:{oldWeapon.WeaponId} - newWeaponId:{newWeapon.WeaponId} - newSus/newUnSus:{newWeapon.SuspendAmmoTick}/{newWeapon.UnSuspendAmmoTick} - oldAmmoSuspend:{oldWeapon.AmmoSuspend} - newAmmoSuspend:{newWeapon.AmmoSuspend} - skipOld:{skipOld}");
                newWeapon.SuspendAmmoTick = 0;
                newWeapon.UnSuspendAmmoTick = 0;
                return nextDef;
            }

            //comp.BlockInventory.Constraint.Remove(comp.Gun.GunBase.CurrentAmmoMagazineId);
            //Log.Line($"[returning none] current:{comp.Gun.GunBase.CurrentAmmoMagazineId.SubtypeId.String} - foundFirst:{firstFound} - Full:{oldWeapon.AmmoFull} - foundValid:{validFound} - oldWeaponId:{oldWeapon.WeaponId} - oldSus/oldUnSus:{oldWeapon.SuspendAmmoTick}/{oldWeapon.UnSuspendAmmoTick} - oldAmmoSuspend:{oldWeapon.AmmoSuspend}");
            return null;
        }*/

        internal static void ComputeStorage(Weapon weapon)
        {
            var comp = weapon.Comp;
            if (!comp.MyCube.HasInventory) return;

            var def = weapon.System.AmmoDefId;
            float itemMass;
            float itemVolume;

            MyInventory.GetItemVolumeAndMass(def, out itemMass, out itemVolume);

            var lastMags = weapon.CurrentMags;
            var invMagsAvailable = comp.Ai.AmmoInventories[def];

            weapon.CurrentMags = comp.BlockInventory.GetItemAmount(def);
            weapon.CurrentAmmoVolume = (float)weapon.CurrentMags * itemVolume;

            if (weapon.CurrentAmmoVolume < 0.25f * weapon.System.MaxAmmoVolume && invMagsAvailable.Count > 0)
                weapon.Comp.Ai.Session.WeaponAmmoPullQueue.Enqueue(weapon);
            
            if (lastMags == 0 && weapon.CurrentMags > 0)
                weapon.Comp.Ai.Reloading = true;

            //Log.Line($"[computed storage] AmmoDef:{def.SubtypeId.String}({weapon.CurrentMags.ToIntSafe()}) - Full:{weapon.AmmoFull} - Mass:<{itemMass}>{ammoMass}({comp.MaxAmmoMass})[{comp.MaxAmmoMass}] - Volume:<{itemVolume}>{ammoVolume}({comp.MaxAmmoVolume})[{comp.MaxInventoryVolume}]");
        }

        internal void AmmoPull() {

            Weapon weapon;
            DsUtil.Start("AmmoPull");
            while (WeaponAmmoPullQueue.TryDequeue(out weapon))
            {
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
            MyAPIGateway.Utilities.InvokeOnGameThread(MoveAmmo);
            DsUtil.Complete("AmmoPull", true, false);
        }

        internal void MoveAmmo()
        {
            MyTuple<Weapon, MyTuple<MyInventory, int>[]> weaponAmmoToPull;
            while (AmmoToPullQueue.TryDequeue(out weaponAmmoToPull))
            {
                var weapon = weaponAmmoToPull.Item1;
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
