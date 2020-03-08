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

                Set.LoadSettings();
                Set.SettingsInit();
                UpdateSettings(Set.Value);

                Set.Value.Overrides.TargetPainter = false;
                Set.Value.Overrides.ManualControl = false;

                for (int i = 0; i < Platform.Weapons.Length; i++)
                {
                    var weapon = Platform.Weapons[i];

                    weapon.Set = Set.Value.Weapons[i];
                    weapon.State = State.Value.Weapons[i];
                    weapon.State.ManualShoot = Weapon.TerminalActionState.ShootOff;
                    weapon.ActiveAmmoDef = weapon.System.WeaponAmmoTypes[weapon.Set.AmmoTypeId].AmmoDef;
                }

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

            if (weapon.ActiveAmmoDef.Const.EnergyAmmo)
                weapon.BaseDamage = weapon.ActiveAmmoDef.Const.BaseDamage * Set.Value.DpsModifier;
            else
                weapon.BaseDamage = weapon.ActiveAmmoDef.Const.BaseDamage;

            if (weapon.ActiveAmmoDef.Const.IsBeamWeapon)
                weapon.BaseDamage *= Set.Value.Overload;

            if (weapon.BaseDamage < 0)
                weapon.BaseDamage = 0;

            if (weapon.RateOfFire < 1)
                weapon.RateOfFire = 1;

            weapon.UpdateShotEnergy();
            weapon.UpdateRequiredPower();
            var mulitplier = (weapon.ActiveAmmoDef.Const.EnergyAmmo && weapon.ActiveAmmoDef.Const.BaseDamage > 0) ? weapon.BaseDamage / weapon.ActiveAmmoDef.Const.BaseDamage : 1;

            if (weapon.BaseDamage > weapon.ActiveAmmoDef.Const.BaseDamage)
                mulitplier *= mulitplier;

            weapon.HeatPShot = weapon.System.HeatPerShot * mulitplier;
            HeatPerSecond += (weapon.RateOfFire / 60f) * (weapon.HeatPShot * weapon.System.BarrelsPerShot);

            weapon.AreaEffectDmg = weapon.ActiveAmmoDef.Const.AreaEffectDamage * mulitplier;
            weapon.DetonateDmg = weapon.ActiveAmmoDef.Const.DetonationDamage * mulitplier;

            weapon.RequiredPower *= mulitplier;
            MaxRequiredPower += weapon.ActiveAmmoDef.Const.MustCharge ? weapon.ActiveAmmoDef.Const.EnergyMagSize : weapon.RequiredPower;
            weapon.UseablePower = weapon.RequiredPower;

            weapon.TicksPerShot = (uint)(3600f / weapon.RateOfFire);
            weapon.TimePerShot = (3600d / weapon.RateOfFire);

            weapon.Dps = (60f / weapon.TicksPerShot) * weapon.BaseDamage * weapon.System.BarrelsPerShot;

            if (weapon.ActiveAmmoDef.AreaEffect.AreaEffect != AreaEffectType.Disabled)
            {
                if (weapon.ActiveAmmoDef.AreaEffect.Detonation.DetonateOnEnd)
                    weapon.Dps += (weapon.DetonateDmg / 2) * (weapon.ActiveAmmoDef.Trajectory.DesiredSpeed > 0
                                        ? weapon.ActiveAmmoDef.Trajectory.AccelPerSec /
                                        weapon.ActiveAmmoDef.Trajectory.DesiredSpeed
                                        : 1);
                else
                    weapon.Dps += (weapon.AreaEffectDmg / 2) *
                                    (weapon.ActiveAmmoDef.Trajectory.DesiredSpeed > 0
                                        ? weapon.ActiveAmmoDef.Trajectory.AccelPerSec /
                                        weapon.ActiveAmmoDef.Trajectory.DesiredSpeed
                                        : 1);
            }

            OptimalDps += weapon.Dps;


            //range slider fix
            maxTrajectory = 0;
            if (ob != null && ob.MaxRangeMeters > maxTrajectory)
                maxTrajectory = ob.MaxRangeMeters;
            else if (weapon.ActiveAmmoDef.Const.MaxTrajectory > maxTrajectory)
                maxTrajectory = weapon.ActiveAmmoDef.Const.MaxTrajectory;

            if (weapon.TrackProjectiles)
                Ai.PointDefense = true;

            if (!weapon.Comp.Session.IsClient)
                weapon.State.Sync.Reloading = false;

            if (!weapon.ActiveAmmoDef.Const.EnergyAmmo && !weapon.ActiveAmmoDef.Const.MustCharge)
                Session.ComputeStorage(weapon);

            if (!weapon.ActiveAmmoDef.Const.MustCharge && weapon.State.Sync.CurrentAmmo == 0 && !weapon.State.Sync.Reloading)
                weapon.EventTriggerStateChanged(EventTriggers.EmptyOnGameLoad, true);

            else if ((!Session.IsClient || !Session.MpActive) && weapon.ActiveAmmoDef.Const.MustCharge && ((weapon.ActiveAmmoDef.Const.EnergyAmmo && weapon.State.Sync.CurrentAmmo != weapon.ActiveAmmoDef.Const.EnergyMagSize) || (!weapon.ActiveAmmoDef.Const.EnergyAmmo && weapon.State.Sync.CurrentAmmo != weapon.ActiveAmmoDef.Const.MagazineDef.Capacity)))
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
            if (MyCube is IMyConveyorSorter) BlockInventory.Constraint = new MyInventoryConstraint("ammo");
            BlockInventory.Constraint.m_useDefaultIcon = false;
            BlockInventory.ResetVolume();
            BlockInventory.Refresh();

            if (Set.Value.Inventory != null)
                BlockInventory.Init(Set.Value.Inventory);
            
            foreach (var weapon in Platform.Weapons)
                MaxInventoryVolume += weapon.System.MaxAmmoVolume;

            if (MyCube.HasInventory)
            {
                BlockInventory.FixInventoryVolume(MaxInventoryVolume);

                BlockInventory.Constraint.Clear();

                foreach (var w in Platform.Weapons)
                {
                    var magId = w.ActiveAmmoDef.Const.MagazineDef.Id;
                    BlockInventory.Constraint.Add(magId);
                }
                BlockInventory.Refresh();
            }
            InventoryInited = true;
        }
    }
}
