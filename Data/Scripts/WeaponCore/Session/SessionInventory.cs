using System.Linq;
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
                if (change.Type == InventoryChange.ChangeType.Add)
                {
                    weapon.AmmoUpdateTick = Tick;
                    var nextDefRaw = NextActiveAmmoDef(comp, weapon);
                    if (!nextDefRaw.HasValue)
                    {
                        Log.Line("no next active ammo");
                        return;
                    }
                    Log.Line($"added to inventory: oldWeaponId:{weapon.WeaponId} - oldDef:{weapon.WeaponSystem.AmmoDefId.SubtypeId.String} - newDef:{nextDefRaw.Value.SubtypeId.String}");
                    comp.FullInventory = blockInventory.CargoPercentage >= 0.5;
                }
            }
        }
        private static MyDefinitionId? NextActiveAmmoDef(WeaponComponent comp, Weapon oldWeapon, bool skipOld = false)
        {
            var ammoToWeaponIds = comp.Platform.Structure.AmmoToWeaponIds;
            var tick = comp.MyAi.MySession.Tick;

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
                //Log.Line($"loop:{index} - newWeaponId:{weapon.WeaponId} - oldWeaponId:{oldWeapon.WeaponId} - TickAge:{comp.MyAi.MySession.Tick - weapon.AmmoUpdateTick}");
                if (!weapon.PullAmmo && tick - weapon.AmmoUpdateTick < WeaponComponent.UnSuspendAmmoCount)
                {
                    if (index == end && validFound)
                    {
                        nextDef = lastDef;
                        nextId = lastId;
                        foundNext = true;
                        Log.Line($"returning last was paused but earlier found: new:{lastDef.SubtypeId.String} - old:{oldWeapon.WeaponSystem.AmmoDefId.SubtypeId.String} - lastId:{lastId} - TickAge:{tick - weapon.AmmoUpdateTick}");
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
                    Log.Line($"returning next: new:{ammoDef.SubtypeId.String} - old:{oldWeapon.WeaponSystem.AmmoDefId.SubtypeId.String} - nextId:{nextId} - TickAge:{tick - weapon.AmmoUpdateTick}");
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
                        Log.Line($"returning first: new:{firstDef.SubtypeId.String} - old:{oldWeapon.WeaponSystem.AmmoDefId.SubtypeId.String} - firstId:{firstId} - TickAge:{tick - weapon.AmmoUpdateTick}");
                        break;
                    }
                    if (index == end && !skipOld) // last and only, resend own def.
                    {
                        nextDef = ammoDef;
                        nextId = weaponId;
                        foundNext = true;
                        Log.Line($"returning last and only: new:{ammoDef.SubtypeId.String} - old:{oldWeapon.WeaponSystem.AmmoDefId.SubtypeId.String} - nextId:{nextId} - TickAge:{tick - weapon.AmmoUpdateTick}");
                        break;
                    }
                    returnNext = true;
                }
                else if (index == end)
                {
                    nextDef = ammoDef;
                    nextId = weaponId;
                    foundNext = true;
                    Log.Line($"last and only and not old weapon: new:{ammoDef.SubtypeId.String} - old:{oldWeapon.WeaponSystem.AmmoDefId.SubtypeId.String} - nextId:{nextId} - TickAge:{tick - weapon.AmmoUpdateTick}");
                    break;
                }
                index++;
            }

            if (foundNext)
            {
                comp.BlockInventory.Constraint.Clear();
                comp.BlockInventory.Constraint.Add(nextDef);
                comp.Gun.GunBase.SwitchAmmoMagazine(nextDef);
                var newWeapon = comp.Platform.Weapons[nextId];
                Log.Line($"foundNext: {nextDef.SubtypeId.String} - oldWeaponId:{oldWeapon.WeaponId} - newWeaponId:{newWeapon.WeaponId} - OldTickAge {tick - oldWeapon.AmmoUpdateTick} - NewTickAge:{tick - newWeapon.AmmoUpdateTick} - oldPulling:{oldWeapon.PullAmmo} - newPulling:{newWeapon.PullAmmo}");
                newWeapon.PullAmmo = true;
                newWeapon.AmmoUpdateTick = tick;
                return nextDef;
            }
            Log.Line($"didn't find next: foundFirst:{firstFound} - foundValid:{validFound} - oldWeaponId:{oldWeapon.WeaponId} - OldTickAge {tick - oldWeapon.AmmoUpdateTick} - oldWeaponPaused:{!oldWeapon.PullAmmo}");
            return null;
        }

        private static void AmmoPull(WeaponComponent comp, Weapon weapon, bool pause)
        {
            Log.Line($"[AmmoPull] Active:{!pause} - Multi:{comp.MultiInventory} - WeaponId:{weapon.WeaponId} - WeaponDef:{weapon.WeaponSystem.AmmoDefId.SubtypeId.String} - WeaponTickAge:{comp.MyAi.MySession.Tick - weapon.AmmoUpdateTick}");
            weapon.PullAmmo = !pause;
            if (!pause)
                weapon.AmmoUpdateTick = comp.MyAi.MySession.Tick;
            else NextActiveAmmoDef(comp, weapon, true);
        }
    }
}
