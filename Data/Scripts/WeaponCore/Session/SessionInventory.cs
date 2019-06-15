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
                var platform = comp.Platform;
                var gun = comp.Gun.GunBase;
                var blockInventory = comp.BlockInventory;
                var tempInventory = comp.TempInventory;
                if (change.Type == InventoryChange.ChangeType.Add)
                {
                    var magWas = gun.CurrentAmmoMagazineId;
                    MyDefinitionId nextDef;
                    if (!comp.Platform.Structure.NextAmmoDef.ContainsKey(magWas))
                        nextDef = comp.Platform.Weapons[0].WeaponSystem.AmmoDefId;
                    else nextDef = comp.Platform.Structure.NextAmmoDef[magWas];
                    comp.BlockInventory.Constraint.Clear();
                    comp.BlockInventory.Constraint.Add(nextDef);
                    comp.Gun.GunBase.SwitchAmmoMagazine(nextDef);
                    comp.FullInventory = blockInventory.CargoPercentage >= 0.5;
                    weapon.AmmoUpdateTick = Tick;
                    Log.Line($"{magWas.SubtypeId.String} - {nextDef.SubtypeId.String} - {gun.CurrentAmmoMagazineId.SubtypeId.String}");
                }
                else if (change.Type == InventoryChange.ChangeType.Pause)
                {
                    var magWas = gun.CurrentAmmoMagazineId;
                    weapon.PullAmmo = false;
                    var nextDef = comp.Platform.Structure.NextAmmoDef[weapon.WeaponSystem.AmmoDefId];
                    comp.BlockInventory.Constraint.Clear();
                    comp.BlockInventory.Constraint.Add(nextDef);
                    comp.Gun.GunBase.SwitchAmmoMagazine(nextDef);
                    weapon.AmmoUpdateTick = Tick;
                    Log.Line($"{magWas.SubtypeId.String} - {nextDef.SubtypeId.String} - {gun.CurrentAmmoMagazineId.SubtypeId.String}");
                }
            }
        }
        /*
        private static MyDefinitionId GetNextAmmoDef(Weapon weapon, WeaponStructure structure)
        {
            var lastId = weapon.WeaponSystem.AmmoDefId;
            while (structure.NextAmmoDef[lastId].)
            {
                
            }
            return 
        }
        */
    }
}
