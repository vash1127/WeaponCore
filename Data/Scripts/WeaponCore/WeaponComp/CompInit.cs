using System;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.ModAPI;
using VRage.Game;
using WeaponCore.Platform;

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
                var isServer = MyAPIGateway.Multiplayer.IsServer;

                if (MyCube.Storage == null)
                    State.StorageInit();

                if (!State.LoadState() && !isServer) _clientNotReady = true;

                Set.LoadSettings();
                Set.SettingsInit();
                UpdateSettings(Set.Value);

                Set.Value.Overrides.TargetPainter = false;
                Set.Value.Overrides.ManualControl = false;
                State.Value.PlayerIdInTerminal = -1;

                for (int i = 0; i < Platform.Weapons.Length; i++)
                {
                    var weapon = Platform.Weapons[i];

                    weapon.Set = Set.Value.Weapons[i];
                    weapon.State = State.Value.Weapons[i];
                    weapon.State.ManualShoot = Weapon.TerminalActionState.ShootOff;
                    weapon.ActiveAmmoDef = weapon.System.WeaponAmmo[new MyDefinitionId()];
                }

                if (Session.MpActive && MyCube?.Storage != null)
                    WeaponValues.Load(this);

                
                /*if (isServer)
                {
                    foreach (var w in State.Value.Weapons)
                    {
                        w.Heat = 0;
                    }
                }*/
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

            if (weapon.System.EnergyAmmo)
                weapon.BaseDamage = weapon.System.BaseDamage * Set.Value.DpsModifier;
            else
                weapon.BaseDamage = weapon.System.BaseDamage;

            if (weapon.System.IsBeamWeapon)
                weapon.BaseDamage *= Set.Value.Overload;

            if (weapon.BaseDamage < 0)
                weapon.BaseDamage = 0;

            if (weapon.RateOfFire < 1)
                weapon.RateOfFire = 1;

            weapon.UpdateShotEnergy();
            weapon.UpdateRequiredPower();
            var mulitplier = (weapon.System.EnergyAmmo && weapon.System.BaseDamage > 0) ? weapon.BaseDamage / weapon.System.BaseDamage : 1;

            if (weapon.BaseDamage > weapon.System.BaseDamage)
                mulitplier *= mulitplier;

            weapon.HeatPShot = weapon.System.HeatPerShot * mulitplier;
            HeatPerSecond += (weapon.RateOfFire / 60f) * (weapon.HeatPShot * weapon.System.BarrelsPerShot);

            weapon.AreaEffectDmg = weapon.System.AreaEffectDamage * mulitplier;
            weapon.DetonateDmg = weapon.System.DetonationDamage * mulitplier;

            weapon.RequiredPower *= mulitplier;
            MaxRequiredPower += weapon.System.MustCharge ? weapon.System.EnergyMagSize : weapon.RequiredPower;
            weapon.UseablePower = weapon.RequiredPower;

            weapon.TicksPerShot = (uint)(3600f / weapon.RateOfFire);
            weapon.TimePerShot = (3600d / weapon.RateOfFire);

            weapon.Dps = (60f / weapon.TicksPerShot) * weapon.BaseDamage * weapon.System.BarrelsPerShot;

            if (weapon.System.Values.Ammo.AreaEffect.AreaEffect != AreaDamage.AreaEffectType.Disabled)
            {
                if (weapon.System.Values.Ammo.AreaEffect.Detonation.DetonateOnEnd)
                    weapon.Dps += (weapon.DetonateDmg / 2) * (weapon.System.Values.Ammo.Trajectory.DesiredSpeed > 0
                                        ? weapon.System.Values.Ammo.Trajectory.AccelPerSec /
                                        weapon.System.Values.Ammo.Trajectory.DesiredSpeed
                                        : 1);
                else
                    weapon.Dps += (weapon.AreaEffectDmg / 2) *
                                    (weapon.System.Values.Ammo.Trajectory.DesiredSpeed > 0
                                        ? weapon.System.Values.Ammo.Trajectory.AccelPerSec /
                                        weapon.System.Values.Ammo.Trajectory.DesiredSpeed
                                        : 1);
            }

            OptimalDps += weapon.Dps;


            //range slider fix
            maxTrajectory = 0;
            if (ob != null && ob.MaxRangeMeters > maxTrajectory)
                maxTrajectory = ob.MaxRangeMeters;
            else if (weapon.System.MaxTrajectory > maxTrajectory)
                maxTrajectory = weapon.System.MaxTrajectory;

            if (weapon.TrackProjectiles)
                Ai.PointDefense = true;

            if (weapon.System.MustCharge)
                State.Value.CurrentCharge += weapon.State.Sync.CurrentCharge;

            if (!weapon.System.EnergyAmmo && !weapon.System.MustCharge)
                Session.ComputeStorage(weapon);

            if (!weapon.System.MustCharge && weapon.State.Sync.CurrentAmmo == 0 && !weapon.State.Sync.Reloading)
                weapon.EventTriggerStateChanged(WeaponDefinition.AnimationDef.PartAnimationSetDef.EventTriggers.EmptyOnGameLoad, true);

            else if ((!Session.IsClient || !Session.MpActive) && weapon.System.MustCharge && ((weapon.System.EnergyAmmo && weapon.State.Sync.CurrentAmmo != weapon.System.EnergyMagSize) || (!weapon.System.EnergyAmmo && weapon.State.Sync.CurrentAmmo != weapon.System.MagazineDef.Capacity)))
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
                    var magId = w.System.MagazineDef.Id;
                    BlockInventory.Constraint.Add(magId);
                }
                BlockInventory.Refresh();
            }
            InventoryInited = true;
        }
    }
}
