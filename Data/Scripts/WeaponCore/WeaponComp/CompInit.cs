using System;
using Sandbox.Game;
using Sandbox.ModAPI;
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
                if (MyCube.Storage == null)
                    State.StorageInit();

                State.LoadState();
                Set.LoadSettings();

                if (Session.IsServer) {
                    Set.Value.Overrides.TargetPainter = false;
                    Set.Value.Overrides.ManualControl = false;
                    State.Value.ResetToFreshLoadState();
                }

                var maxTrajectory = 0f;
                for (int i = 0; i < Platform.Weapons.Length; i++) {

                    var weapon = Platform.Weapons[i];
                    weapon.Set = Set.Value.Weapons[i];
                    weapon.State = State.Value.Weapons[i];

                    weapon.ChangeActiveAmmo(weapon.System.AmmoTypes.Length > 0 ? weapon.System.AmmoTypes[weapon.Set.AmmoTypeId] : new WeaponSystem.WeaponAmmoTypes());

                    if (weapon.ActiveAmmoDef.AmmoDef == null || !weapon.ActiveAmmoDef.AmmoDef.Const.IsTurretSelectable) {
                        Log.Line($"[{weapon.System.WeaponName}] Your first ammoType is broken, I am crashing now Dave.");
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

        private void DpsAndHeatInit(Weapon weapon, out double maxTrajectory)
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
                EffectiveDps += weapon.ActiveAmmoDef.AmmoDef.Const.EffectiveDps;
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
        }

        private void InventoryInit()
        {
            using (MyCube.Pin())
            {
                if (InventoryInited || !MyCube.HasInventory || MyCube.MarkedForClose || (Platform.State != MyWeaponPlatform.PlatformState.Inited && Platform.State != MyWeaponPlatform.PlatformState.Incomplete) || BlockInventory == null)
                {
                    Log.Line($"InventoryInit failed: IsInitted:{InventoryInited} - NoInventory:{!MyCube.HasInventory} - Marked:{MyCube.MarkedForClose} - PlatformNotReady:{Platform.State != MyWeaponPlatform.PlatformState.Ready}({Platform.State}) - nullInventory:{BlockInventory == null}");
                    return;
                }

                if (MyCube is IMyConveyorSorter || BlockInventory.Constraint == null) BlockInventory.Constraint = new MyInventoryConstraint("ammo");

                BlockInventory.Constraint.m_useDefaultIcon = false;
                BlockInventory.Refresh();
                BlockInventory.Constraint.Clear();

                if (!string.IsNullOrEmpty(CustomIcon)) {
                    var iconPath = Platform.Structure.ModPath + "\\Textures\\GUI\\Icons\\" + CustomIcon;
                    BlockInventory.Constraint.Icon = iconPath;
                    BlockInventory.Constraint.UpdateIcon();
                }

                for (int i = 0; i < Platform.Weapons.Length; i++) {
                    var w = Platform.Weapons[i];

                    if (w == null)
                    {
                        Log.Line($"InventoryInit weapon null");
                        continue;
                    }
                    for (int j = 0; j < w.System.AmmoTypes.Length; j++)
                    {
                        if (w.System.AmmoTypes[j].AmmoDef.Const.MagazineDef != null)
                            BlockInventory.Constraint.Add(w.System.AmmoTypes[j].AmmoDef.Const.MagazineDef.Id);
                    }
                }
                BlockInventory.Refresh();

                InventoryInited = true;
            }
        }
    }
}
