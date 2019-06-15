using VRage.Game;
using VRage.Game.Entity;
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
                else if (change.Type == InventoryChange.ChangeType.Check)
                {
                    var magWas = gun.CurrentAmmoMagazineId;
                    var nextDef = comp.Platform.Structure.NextAmmoDef[weapon.WeaponSystem.AmmoDefId];
                    comp.BlockInventory.Constraint.Clear();
                    comp.BlockInventory.Constraint.Add(nextDef);
                    comp.Gun.GunBase.SwitchAmmoMagazine(nextDef);
                    weapon.AmmoUpdateTick = Tick;
                    Log.Line($"{magWas.SubtypeId.String} - {nextDef.SubtypeId.String} - {gun.CurrentAmmoMagazineId.SubtypeId.String}");
                }
            }
        }

        private void UpdateWeaponPlatforms()
        {
            if (!GameLoaded) return;
            foreach (var aiPair in GridTargetingAIs)
            {
                //var grid = aiPair.Key;
                var gridAi = aiPair.Value;
                if (!gridAi.Ready) continue;
                foreach (var basePair in gridAi.WeaponBase)
                {
                    //var myCube = basePair.Key;
                    var comp = basePair.Value;
                    var ammoCheck = comp.MultiInventory && !comp.FullInventory;
                    var gun = comp.Gun.GunBase;

                    if (!comp.MainInit || !comp.State.Value.Online) continue;
                    for (int j = 0; j < comp.Platform.Weapons.Length; j++)
                    {
                        var w = comp.Platform.Weapons[j];
                        if (ammoCheck && gun.CurrentAmmoMagazineId == w.WeaponSystem.AmmoDefId && Tick - w.AmmoUpdateTick >= 500)
                            InventoryEvent.Enqueue(new InventoryChange(w, new MyPhysicalInventoryItem(), 0, InventoryChange.ChangeType.Check));

                        if (w.SeekTarget && w.TrackTarget) gridAi.SelectTarget(ref w.Target, w);

                        if (w.AiReady || w.Gunner && (j == 0 && MouseButtonLeft || j == 1 && MouseButtonRight)) w.Shoot();
                    }
                }
                gridAi.Ready = false;
            }
        }

        private void AiLoop()
        {
            if (!GameLoaded) return;
            foreach (var aiPair in GridTargetingAIs)
            {
                //var grid = aiPair.Key;
                var ai = aiPair.Value;
                foreach (var basePair in ai.WeaponBase)
                {
                    //var myCube = basePair.Key;
                    var comp = basePair.Value;
                    if (!comp.MainInit || !comp.State.Value.Online) continue;

                    for (int j = 0; j < comp.Platform.Weapons.Length; j++)
                    {
                        var w = comp.Platform.Weapons[j];
                        w.Gunner = ControlledEntity == comp.MyCube;
                        if (!w.Gunner)
                        {
                            if (w.TrackingAi)
                            {
                                if (w.Target != null && !Weapon.TrackingTarget(w, w.Target, true))
                                    w.Target = null;
                            }
                            else
                            {
                                if (!w.TrackTarget) w.Target = comp.TrackingWeapon.Target;
                                if (w.Target != null && !Weapon.CheckTarget(w, w.Target)) w.Target = null;
                            }

                            if (w != comp.TrackingWeapon && comp.TrackingWeapon.Target == null) w.Target = null;
                        }
                        else
                        {
                            InTurret = true;
                            if (MouseButtonPressed)
                            {
                                var currentAmmo = comp.Gun.GunBase.CurrentAmmo;
                                if (currentAmmo <= 1) comp.Gun.GunBase.CurrentAmmo += 1;
                            }
                        }
                        w.AiReady = w.Target != null && !w.Gunner && w.Comp.TurretTargetLock && !w.Target.MarkedForClose;
                        w.SeekTarget = Tick20 && !w.Gunner && (w.Target == null || w.Target != null && w.Target.MarkedForClose) && w.TrackTarget;
                        if (w.AiReady || w.SeekTarget || w.Gunner) ai.Ready = true;
                    }
                }
            }
        }
    }
}