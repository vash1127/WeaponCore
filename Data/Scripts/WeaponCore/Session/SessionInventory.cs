using Sandbox.Game;
using VRage.Game;
using WeaponCore.Platform;
using WeaponCore.Support;

namespace WeaponCore
{
    public partial class Session
    {
        
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
        }

        private static void AmmoPull(WeaponComponent comp, Weapon weapon, bool suspend)
        {
            Log.Line($"[ammo pull] suspend:{suspend}(was:{weapon.AmmoSuspend}) weaponId:{weapon.WeaponId} - weaponDef:{weapon.System.AmmoDefId.SubtypeId.String} - Full:{weapon.AmmoFull} - weaponSuspendAge:{weapon.SuspendAmmoTick} - weaponUnSuspendAge:{weapon.UnSuspendAmmoTick} - multi:{comp.MultiInventory} - ");
            weapon.AmmoSuspend = suspend;

            if (suspend) NextActiveAmmoDef(comp, weapon, true);
            else
            {
                ComputeStorage(weapon);
                weapon.SuspendAmmoTick = 0;
                weapon.UnSuspendAmmoTick = 0;
                comp.LastAmmoUnSuspendTick = Instance.Tick;
                if (!comp.FullInventory && !weapon.AmmoFull)
                    NextActiveAmmoDef(comp, weapon, false);
            }

            if (suspend) comp.PullingAmmoCnt--;
            else comp.PullingAmmoCnt++;
        }

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
                comp.Gun.GunBase.SwitchAmmoMagazine(nextDef);
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

            comp.BlockInventory.Constraint.Remove(comp.Gun.GunBase.CurrentAmmoMagazineId);
            Log.Line($"[returning none] current:{comp.Gun.GunBase.CurrentAmmoMagazineId.SubtypeId.String} - foundFirst:{firstFound} - Full:{oldWeapon.AmmoFull} - foundValid:{validFound} - oldWeaponId:{oldWeapon.WeaponId} - oldSus/oldUnSus:{oldWeapon.SuspendAmmoTick}/{oldWeapon.UnSuspendAmmoTick} - oldAmmoSuspend:{oldWeapon.AmmoSuspend}");
            return null;
        }

        internal static bool ComputeStorage(Weapon weapon)
        {
            var comp = weapon.Comp;
            comp.BlockInventory.Refresh();
            var def = weapon.System.AmmoDefId;
            comp.FullInventory = comp.BlockInventory.CargoPercentage >= 0.5;
            var lastMags = weapon.CurrentMags;
            weapon.CurrentMags = comp.BlockInventory.GetItemAmount(def);

            float itemMass;
            float itemVolume;
            MyInventory.GetItemVolumeAndMass(def, out itemMass, out itemVolume);
            var ammoMass = itemMass * weapon.CurrentMags.ToIntSafe();
            var ammoVolume = itemVolume * weapon.CurrentMags.ToIntSafe();
            weapon.AmmoFull = ammoMass >= comp.MaxAmmoMass || ammoVolume >= comp.MaxAmmoVolume;

            return weapon.CurrentMags > lastMags;
            //Log.Line($"[computed storage] AmmoDef:{def.SubtypeId.String}({weapon.CurrentMags.ToIntSafe()}) - Full:{weapon.AmmoFull} - Mass:<{itemMass}>{ammoMass}({comp.MaxAmmoMass})[{comp.MaxAmmoMass}] - Volume:<{itemVolume}>{ammoVolume}({comp.MaxAmmoVolume})[{comp.MaxInventoryVolume}]");
        }
    }
}
