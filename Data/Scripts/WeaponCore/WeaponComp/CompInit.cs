using System;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.ModAPI;
using WeaponCore.Platform;
using static WeaponCore.Support.WeaponDefinition.AnimationDef.PartAnimationSetDef;
namespace WeaponCore.Support
{
    public partial class WeaponComponent
    {
        private void PowerInit()
        {
            MyCube.ResourceSink.SetRequiredInputFuncByType(GId, () => SinkPower);
            MyCube.ResourceSink.SetMaxRequiredInputByType(GId, 0);

            MyCube.ResourceSink.Update();
        }

        private void StorageSetup()
        {
            try
            {
                if (MyCube.Storage == null)
                    State.StorageInit();

                State.LoadState();
                Set.LoadSettings();
                CompMids.Load(this);

                if (!Session.IsClient)
                {
                    Set.Value.Overrides.TargetPainter = false;
                    Set.Value.Overrides.ManualControl = false;
                }

                var maxTrajectory = 0f;
                for (int i = 0; i < Platform.Weapons.Length; i++)
                {
                    var weapon = Platform.Weapons[i];
                    weapon.Set = Set.Value.Weapons[i];
                    weapon.State = State.Value.Weapons[i];

                    weapon.ActiveAmmoDef = weapon.System.WeaponAmmoTypes.Length > 0 ? weapon.System.WeaponAmmoTypes[weapon.Set.AmmoTypeId] : new WeaponAmmoTypes();

                    if (weapon.ActiveAmmoDef.AmmoDef == null || !weapon.ActiveAmmoDef.AmmoDef.Const.IsTurretSelectable)
                    {
                        Log.Line($"Your first ammoType is broken, I am crashing now Dave.");
                        return;
                    }

                    weapon.UpdateWeaponRange();
                    if (maxTrajectory < weapon.MaxTargetDistance)
                        maxTrajectory = (float)weapon.MaxTargetDistance;

                }

                if (Set.Value.Range < 0)
                    Set.Value.Range = maxTrajectory;

                WeaponValues.Load(this);
            }
            catch (Exception ex) { Log.Line($"Exception in StorageSetup: {ex} - StateNull:{State == null}({State?.Value == null})[{State?.Value?.Weapons == null}] - SetNull:{Set == null}({Set?.Value == null})[{Set?.Value?.Weapons == null}] - cubeMarked:{MyCube.MarkedForClose} - WeaponsNull:{Platform.Weapons == null} - FirstWeaponNull:{Platform.Weapons?[0] == null}"); }
        }

        private void DpsAndHeatInit(Weapon weapon, MyLargeTurretBaseDefinition ob, out double maxTrajectory)
        {
            MaxHeat += weapon.System.MaxHeat;

            weapon.RateOfFire = (int)(weapon.System.RateOfFire * Set.Value.RofModifier);
            weapon.BarrelSpinRate = (int)(weapon.System.BarrelSpinRate * Set.Value.RofModifier);
            HeatSinkRate += weapon.HsRate;

            if (weapon.System.HasBarrelRotation) weapon.UpdateBarrelRotation();

            if (weapon.RateOfFire < 1)
                weapon.RateOfFire = 1;

            weapon.SetWeaponDps();

            if (!weapon.System.DesignatorWeapon)
            {
                PeakDps += weapon.ActiveAmmoDef.AmmoDef.Const.PeakDps;
                ShotsPerSec += weapon.ActiveAmmoDef.AmmoDef.Const.ShotsPerSec;
                BaseDps += weapon.ActiveAmmoDef.AmmoDef.Const.BaseDps;
                AreaDps += weapon.ActiveAmmoDef.AmmoDef.Const.AreaDps;
                DetDps += weapon.ActiveAmmoDef.AmmoDef.Const.DetDps;
            }

            maxTrajectory = 0;
            if (weapon.ActiveAmmoDef.AmmoDef.Const.MaxTrajectory > maxTrajectory)
                maxTrajectory = weapon.ActiveAmmoDef.AmmoDef.Const.MaxTrajectory;

            if (weapon.TrackProjectiles)
                Ai.PointDefense = true;

            if (!weapon.Comp.Session.IsClient)
                weapon.State.Sync.Reloading = false;

            //if (!weapon.ActiveAmmoDef.AmmoDef.Const.EnergyAmmo && !weapon.ActiveAmmoDef.AmmoDef.Const.MustCharge)
            //Session.ComputeStorage(weapon);            

            //Log.Line($"MustCharge: {weapon.ActiveAmmoDef.AmmoDef.Const.MustCharge} CurrentAmmo: {weapon.State.Sync.CurrentAmmo} MagSize: {weapon.ActiveAmmoDef.AmmoDef.Const.MagazineSize} hybrid: {weapon.ActiveAmmoDef.AmmoDef.Const.IsHybrid} EnergyAmmo: {weapon.ActiveAmmoDef.AmmoDef.Const.EnergyAmmo}");

            if ((!Session.IsClient || !Session.MpActive) && weapon.ActiveAmmoDef.AmmoDef.Const.MustCharge && weapon.State.Sync.CurrentAmmo != weapon.ActiveAmmoDef.AmmoDef.Const.MagazineSize)
            {
                if (weapon.State.Sync.CurrentCharge > 0)
                    State.Value.CurrentCharge -= weapon.State.Sync.CurrentCharge;

                weapon.State.Sync.CurrentCharge = 0;
                weapon.State.Sync.CurrentAmmo = 0;
                weapon.State.Sync.Reloading = false;

                if (!Session.GameLoaded)
                    Session.ChargingWeaponsToReload.Enqueue(weapon);
                else
                    Session.ComputeStorage(weapon);
            }
            else if (!weapon.ActiveAmmoDef.AmmoDef.Const.EnergyAmmo && weapon.State.Sync.CurrentAmmo == 0)
            {
                weapon.EventTriggerStateChanged(EventTriggers.EmptyOnGameLoad, true);
                Session.ComputeStorage(weapon);
            }
        }

        private void InventoryInit()
        {
            using (MyCube?.Pin())
            {
                if (InventoryInited || MyCube == null || !MyCube.HasInventory || MyCube.MarkedForClose) return;

                if (MyCube is IMyConveyorSorter || BlockInventory.Constraint == null) BlockInventory.Constraint = new MyInventoryConstraint("ammo");

                BlockInventory.Constraint.m_useDefaultIcon = false;
                BlockInventory.ResetVolume();
                BlockInventory.Refresh();

                BlockInventory.Constraint.Clear();

                var maxInventoryVolume = 0f;
                for (int i = 0; i < Platform.Weapons.Length; i++)
                {
                    var w = Platform.Weapons[i];

                    for (int j = 0; j < w.System.WeaponAmmoTypes.Length; j++)
                        BlockInventory.Constraint.Add(w.System.WeaponAmmoTypes[j].AmmoDef.Const.MagazineDef.Id);

                    maxInventoryVolume += w.System.MaxAmmoVolume;
                }

                BlockInventory.FixInventoryVolume(maxInventoryVolume);
                BlockInventory.Refresh();

                InventoryInited = true;
            }
        }
    }
}
