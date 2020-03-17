using System;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.ModAPI;
using WeaponCore.Platform;
using static WeaponCore.Support.WeaponDefinition.AnimationDef.PartAnimationSetDef;
using static WeaponCore.Support.WeaponDefinition.AmmoDef.AreaDamageDef;
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

                    if (maxTrajectory < weapon.ActiveAmmoDef.AmmoDef.Const.MaxTrajectory)
                        maxTrajectory = (float)weapon.ActiveAmmoDef.AmmoDef.Const.MaxTrajectory;

                }

                if (Set.Value.Range == -1)
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

            if (weapon.ActiveAmmoDef.AmmoDef.Const.EnergyAmmo)
                weapon.BaseDamage = weapon.ActiveAmmoDef.AmmoDef.Const.BaseDamage * Set.Value.DpsModifier;
            else
                weapon.BaseDamage = weapon.ActiveAmmoDef.AmmoDef.Const.BaseDamage;

            if (weapon.ActiveAmmoDef.AmmoDef.Const.IsBeamWeapon)
                weapon.BaseDamage *= Set.Value.Overload;

            if (weapon.BaseDamage < 0)
                weapon.BaseDamage = 0;

            if (weapon.RateOfFire < 1)
                weapon.RateOfFire = 1;

            weapon.UpdateShotEnergy();
            weapon.UpdateRequiredPower();
            var mulitplier = (weapon.ActiveAmmoDef.AmmoDef.Const.EnergyAmmo && weapon.ActiveAmmoDef.AmmoDef.Const.BaseDamage > 0) ? weapon.BaseDamage / weapon.ActiveAmmoDef.AmmoDef.Const.BaseDamage : 1;

            if (weapon.BaseDamage > weapon.ActiveAmmoDef.AmmoDef.Const.BaseDamage)
                mulitplier *= mulitplier;

            weapon.HeatPShot = weapon.System.HeatPerShot * mulitplier;
            HeatPerSecond += (weapon.RateOfFire / 60f) * (weapon.HeatPShot * weapon.System.BarrelsPerShot);

            weapon.AreaEffectDmg = weapon.ActiveAmmoDef.AmmoDef.Const.AreaEffectDamage * mulitplier;
            weapon.DetonateDmg = weapon.ActiveAmmoDef.AmmoDef.Const.DetonationDamage * mulitplier;

            weapon.RequiredPower *= mulitplier;
            MaxRequiredPower += weapon.ActiveAmmoDef.AmmoDef.Const.MustCharge ? weapon.ActiveAmmoDef.AmmoDef.Const.EnergyMagSize : weapon.RequiredPower;
            weapon.UseablePower = weapon.RequiredPower;

            weapon.TicksPerShot = (uint)(3600f / weapon.RateOfFire);
            weapon.TimePerShot = (3600d / weapon.RateOfFire);

            weapon.Dps = (60f / weapon.TicksPerShot) * weapon.BaseDamage * weapon.System.BarrelsPerShot;

            if (weapon.ActiveAmmoDef.AmmoDef.AreaEffect.AreaEffect != AreaEffectType.Disabled)
            {
                if (weapon.ActiveAmmoDef.AmmoDef.AreaEffect.Detonation.DetonateOnEnd)
                    weapon.Dps += (weapon.DetonateDmg / 2) * (weapon.ActiveAmmoDef.AmmoDef.Trajectory.DesiredSpeed > 0
                                        ? weapon.ActiveAmmoDef.AmmoDef.Trajectory.AccelPerSec /
                                        weapon.ActiveAmmoDef.AmmoDef.Trajectory.DesiredSpeed
                                        : 1);
                else
                    weapon.Dps += (weapon.AreaEffectDmg / 2) *
                                    (weapon.ActiveAmmoDef.AmmoDef.Trajectory.DesiredSpeed > 0
                                        ? weapon.ActiveAmmoDef.AmmoDef.Trajectory.AccelPerSec /
                                        weapon.ActiveAmmoDef.AmmoDef.Trajectory.DesiredSpeed
                                        : 1);
            }

            OptimalDps += weapon.Dps;


            //range slider fix
            maxTrajectory = 0;
            //if (ob != null && ob.MaxRangeMeters > maxTrajectory)
                //maxTrajectory = ob.MaxRangeMeters;
            if (weapon.ActiveAmmoDef.AmmoDef.Const.MaxTrajectory > maxTrajectory)
                maxTrajectory = weapon.ActiveAmmoDef.AmmoDef.Const.MaxTrajectory;

            if (weapon.TrackProjectiles)
                Ai.PointDefense = true;

            if (!weapon.Comp.Session.IsClient)
                weapon.State.Sync.Reloading = false;

            if (!weapon.ActiveAmmoDef.AmmoDef.Const.EnergyAmmo && !weapon.ActiveAmmoDef.AmmoDef.Const.MustCharge)
                Session.ComputeStorage(weapon);

            if (!weapon.ActiveAmmoDef.AmmoDef.Const.MustCharge && weapon.State.Sync.CurrentAmmo == 0 && !weapon.State.Sync.Reloading)
                weapon.EventTriggerStateChanged(EventTriggers.EmptyOnGameLoad, true);

            else if ((!Session.IsClient || !Session.MpActive) && weapon.ActiveAmmoDef.AmmoDef.Const.MustCharge && ((weapon.ActiveAmmoDef.AmmoDef.Const.EnergyAmmo && weapon.State.Sync.CurrentAmmo != weapon.ActiveAmmoDef.AmmoDef.Const.EnergyMagSize) || (!weapon.ActiveAmmoDef.AmmoDef.Const.EnergyAmmo && weapon.State.Sync.CurrentAmmo != weapon.ActiveAmmoDef.AmmoDef.Const.MagazineDef.Capacity)))
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
        }

        private void InventoryInit()
        {
            if (InventoryInited)
                return;

            //Set.Value.InventoryInited = true;

            if (MyCube is IMyConveyorSorter) BlockInventory.Constraint = new MyInventoryConstraint("ammo");
            BlockInventory.Constraint.m_useDefaultIcon = false;
            BlockInventory.ResetVolume();
            BlockInventory.Refresh();

            if (MyCube.HasInventory)
            {
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
            }
            InventoryInited = true;
        }
    }
}
