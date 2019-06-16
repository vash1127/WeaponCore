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
                var blockInventory = comp.BlockInventory;
                comp.FullInventory = blockInventory.CargoPercentage >= 0.5;
                if (change.Type == InventoryChange.ChangeType.Add)
                {
                    var nextDefRaw = NextActiveAmmoDef(comp, weapon);
                    if (!nextDefRaw.HasValue)
                    {
                        Log.Line("Didn't find new ammoDef");
                        return;
                    }
                    Log.Line($"added to inventory: oldWeaponId:{weapon.WeaponId} - oldDef:{weapon.WeaponSystem.AmmoDefId.SubtypeId.String} - newDef:{nextDefRaw.Value.SubtypeId.String}");
                }
            }
        }

        private static void AmmoPull(WeaponComponent comp, Weapon weapon, bool suspend)
        {
            Log.Line($"[ammo pull] suspend:{suspend} - multi:{comp.MultiInventory} - weaponId:{weapon.WeaponId} - weaponDef:{weapon.WeaponSystem.AmmoDefId.SubtypeId.String} - weaponSuspendAge:{weapon.SuspendAmmoTick} - weaponUnSuspendAge:{weapon.UnSuspendAmmoTick}");
            weapon.AmmoSuspend = suspend;

            if (suspend) NextActiveAmmoDef(comp, weapon, true);
            else
            {
                weapon.SuspendAmmoTick = 0;
                weapon.UnSuspendAmmoTick = 0;
            }
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
                var weaponId = pair.Value;
                var ammoDef = pair.Key;
                var weapon = comp.Platform.Weapons[weaponId];
                if (weapon.AmmoSuspend) continue;

                validFound = true;
                validDef = ammoDef;
                validId = weaponId;
                if (returnNext) // found weapon last iteration, return this.
                {
                    nextDef = ammoDef;
                    nextId = weaponId;
                    nextFound = true;
                    Log.Line($"[returning next] new:{ammoDef.SubtypeId.String} - old:{oldWeapon.WeaponSystem.AmmoDefId.SubtypeId.String} - nextId:{nextId} - Sus/UnSus:{weapon.SuspendAmmoTick}/{weapon.UnSuspendAmmoTick}");
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
                        Log.Line($"[returning first] new:{firstDef.SubtypeId.String} - old:{oldWeapon.WeaponSystem.AmmoDefId.SubtypeId.String} - firstId:{firstId} - Sus/UnSus:{weapon.SuspendAmmoTick}/{weapon.UnSuspendAmmoTick}");
                        break;
                    }
                    returnNext = true;
                }
            }

            oldWeapon.SuspendAmmoTick = 0;
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
                var newWeapon = comp.Platform.Weapons[nextId];
                Log.Line($"[sending nextDef] next:{nextDef.SubtypeId.String} - last:{oldWeapon.WeaponSystem.AmmoDefId.SubtypeId.String} - oldWeaponId:{oldWeapon.WeaponId} - newWeaponId:{newWeapon.WeaponId} - oldSus/oldUnSus:{oldWeapon.SuspendAmmoTick}/{oldWeapon.UnSuspendAmmoTick} - newSus/newUnSus:{newWeapon.SuspendAmmoTick}/{newWeapon.UnSuspendAmmoTick} - oldAmmoSuspend:{oldWeapon.AmmoSuspend} - newAmmoSuspend:{newWeapon.AmmoSuspend} - skipOld:{skipOld}");
                newWeapon.SuspendAmmoTick = 0;
                return nextDef;
            }
            Log.Line($"[returning none] foundFirst:{firstFound} - foundValid:{validFound} - oldWeaponId:{oldWeapon.WeaponId} - oldSus/oldUnSus:{oldWeapon.SuspendAmmoTick}/{oldWeapon.UnSuspendAmmoTick} - oldAmmoSuspend:{oldWeapon.AmmoSuspend}");
            return null;
        }

        private static MyDefinitionId? NextActiveAmmoDefBackup(WeaponComponent comp, Weapon oldWeapon, bool skipOld = false)
        {
            var ammoToWeaponIds = comp.Platform.Structure.AmmoToWeaponIds;

            MyDefinitionId firstDef = new MyDefinitionId();
            MyDefinitionId nextDef = new MyDefinitionId();
            MyDefinitionId lastDef = new MyDefinitionId();
            var end = ammoToWeaponIds.Count - 1;
            var index = 0;
            var firstId = -1;
            var lastId = -1;
            var nextId = -1;
            var returnNext = false;
            var foundNext = false;
            var validFound = false;
            var firstFound = false;


            foreach (var pair in ammoToWeaponIds)
            {
                var weaponId = pair.Value;
                var ammoDef = pair.Key;
                var weapon = comp.Platform.Weapons[weaponId];
                if (weapon.AmmoSuspend && weapon.UnSuspendAmmoTick < Weapon.UnSuspendAmmoCount)
                {
                    if (index == end && validFound)
                    {
                        nextDef = lastDef;
                        nextId = lastId;
                        foundNext = true;
                        Log.Line($"[returning last] new:{lastDef.SubtypeId.String} - old:{oldWeapon.WeaponSystem.AmmoDefId.SubtypeId.String} - lastId:{lastId} - Sus/UnSus:{weapon.SuspendAmmoTick}/{weapon.UnSuspendAmmoTick} - (was suspended but earlier found)");
                        break;
                    }
                    index++;
                    continue;
                }

                validFound = true;
                lastDef = ammoDef;
                lastId = weaponId;
                if (returnNext) // found weapon last iteration, this the one.
                {
                    nextDef = ammoDef;
                    nextId = weaponId;
                    foundNext = true;
                    Log.Line($"[returning next] new:{ammoDef.SubtypeId.String} - old:{oldWeapon.WeaponSystem.AmmoDefId.SubtypeId.String} - nextId:{nextId} - Sus/UnSus:{weapon.SuspendAmmoTick}/{weapon.UnSuspendAmmoTick}");
                    break;
                }

                if (index == 0)
                {
                    firstDef = ammoDef; // save if oldweapon is last, but not only
                    firstId = weaponId;
                    firstFound = true;
                }

                if (weapon == oldWeapon)
                {
                    if (index == end && index != 0 && firstFound) // last but not only
                    {
                        nextDef = firstDef;
                        nextId = firstId;
                        foundNext = true;
                        Log.Line($"[returning first] new:{firstDef.SubtypeId.String} - old:{oldWeapon.WeaponSystem.AmmoDefId.SubtypeId.String} - firstId:{firstId} - Sus/UnSus:{weapon.SuspendAmmoTick}/{weapon.UnSuspendAmmoTick}");
                        break;
                    }
                    if (index == end && !skipOld) // last and only, resend own def.
                    {
                        nextDef = ammoDef;
                        nextId = weaponId;
                        foundNext = true;
                        Log.Line($"[returning last] new:{ammoDef.SubtypeId.String} - old:{oldWeapon.WeaponSystem.AmmoDefId.SubtypeId.String} - nextId:{nextId} - Sus/UnSus:{weapon.SuspendAmmoTick}/{weapon.UnSuspendAmmoTick} - (and only)");
                        break;
                    }
                    returnNext = true;
                }
                else if (index == end)
                {
                    nextDef = ammoDef;
                    nextId = weaponId;
                    foundNext = true;
                    Log.Line($"[returning last] new:{ammoDef.SubtypeId.String} - old:{oldWeapon.WeaponSystem.AmmoDefId.SubtypeId.String} - nextId:{nextId} - Sus/UnSus:{weapon.SuspendAmmoTick}/{weapon.UnSuspendAmmoTick} - (only and not old weapon)");
                    break;
                }
                index++;
            }

            oldWeapon.SuspendAmmoTick = 0;
            if (foundNext)
            {
                comp.BlockInventory.Constraint.Clear();
                comp.BlockInventory.Constraint.Add(nextDef);
                comp.Gun.GunBase.SwitchAmmoMagazine(nextDef);
                var newWeapon = comp.Platform.Weapons[nextId];
                Log.Line($"[sending nextDef] next:{nextDef.SubtypeId.String} - oldWeaponId:{oldWeapon.WeaponId} - newWeaponId:{newWeapon.WeaponId} - oldSus/oldUnSus:{oldWeapon.SuspendAmmoTick}/{oldWeapon.UnSuspendAmmoTick} - newSus/newUnSus:{newWeapon.SuspendAmmoTick}/{newWeapon.UnSuspendAmmoTick} - oldAmmoSuspend:{oldWeapon.AmmoSuspend} - newAmmoSuspend:{newWeapon.AmmoSuspend}");
                newWeapon.AmmoSuspend = false;
                newWeapon.SuspendAmmoTick = 0;
                return nextDef;
            }
            Log.Line($"[returning none] foundFirst:{firstFound} - foundValid:{validFound} - oldWeaponId:{oldWeapon.WeaponId} - oldSus/oldUnSus:{oldWeapon.SuspendAmmoTick}/{oldWeapon.UnSuspendAmmoTick} - oldAmmoSuspend:{oldWeapon.AmmoSuspend}");
            return null;
        }
    }
}
