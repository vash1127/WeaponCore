using System;
using System.Collections.Generic;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage;
using VRage.Game.ModAPI;
using WeaponCore.Platform;
namespace WeaponCore.Support
{
    public partial class CoreComponent
    {
        private void PowerInit()
        {
            Cube.ResourceSink.SetRequiredInputFuncByType(GId, () => SinkPower);
            Cube.ResourceSink.SetMaxRequiredInputByType(GId, 0);

            Cube.ResourceSink.Update();
        }

        private void StorageSetup()
        {
            try
            {
                if (CoreEntity.Storage == null)
                    Data.StorageInit();

                Data.Load();

                if (Session.IsServer)
                    Data.Repo.ResetToFreshLoadState();

                var maxTrajectory = 0f;
                for (int i = 0; i < Platform.Weapons.Length; i++) {

                    var weapon = Platform.Weapons[i];

                    if (Session.IsServer)
                        weapon.ChangeActiveAmmoServer();
                    else weapon.ChangeActiveAmmoClient();

                    if (weapon.ActiveAmmoDef.AmmoDef == null || !weapon.ActiveAmmoDef.AmmoDef.Const.IsTurretSelectable && weapon.System.AmmoTypes.Length > 1) {
                        Platform.PlatformCrash(this, false, true, $"[{weapon.System.WeaponName}] Your first ammoType is broken (isNull:{weapon.ActiveAmmoDef.AmmoDef == null}), I am crashing now Dave.");
                        return;
                    }

                    weapon.UpdateWeaponRange();
                    if (maxTrajectory < weapon.MaxTargetDistance)
                        maxTrajectory = (float)weapon.MaxTargetDistance;

                }
                if (Data.Repo.Base.Set.Range <= 0)
                    Data.Repo.Base.Set.Range = maxTrajectory;
                
            }
            catch (Exception ex) { Log.Line($"Exception in StorageSetup: {ex} - StateNull:{Data.Repo == null} - cubeMarked:{CoreEntity.MarkedForClose} - WeaponsNull:{Platform.Weapons == null} - FirstWeaponNull:{Platform.Weapons?[0] == null}"); }
        }

        private void DpsAndHeatInit(Weapon weapon, out double maxTrajectory)
        {
            MaxHeat += weapon.System.MaxHeat;

            weapon.RateOfFire = (int)(weapon.System.RateOfFire * Data.Repo.Base.Set.RofModifier);
            weapon.BarrelSpinRate = (int)(weapon.System.BarrelSpinRate * Data.Repo.Base.Set.RofModifier);
            HeatSinkRate += weapon.HsRate;

            if (weapon.System.HasBarrelRotation) weapon.UpdateBarrelRotation();

            if (weapon.RateOfFire < 1)
                weapon.RateOfFire = 1;

            weapon.SetWeaponDps();

            if (!weapon.System.DesignatorWeapon)
            {
                var patternSize = weapon.ActiveAmmoDef.AmmoDef.Const.AmmoPattern.Length;
                foreach (var ammo in weapon.ActiveAmmoDef.AmmoDef.Const.AmmoPattern)
                {
                    PeakDps += ammo.Const.PeakDps / (float) patternSize;
                    EffectiveDps += ammo.Const.EffectiveDps / (float) patternSize;
                    ShotsPerSec += ammo.Const.ShotsPerSec / (float) patternSize;
                    BaseDps += ammo.Const.BaseDps / (float) patternSize;
                    AreaDps += ammo.Const.AreaDps / (float) patternSize;
                    DetDps += ammo.Const.DetDps / (float) patternSize;
                }
            }

            maxTrajectory = 0;
            if (weapon.ActiveAmmoDef.AmmoDef.Const.MaxTrajectory > maxTrajectory)
                maxTrajectory = weapon.ActiveAmmoDef.AmmoDef.Const.MaxTrajectory;

            if (weapon.System.TrackProjectile)
                Ai.PointDefense = true;
        }

        internal void SubGridInit()
        {
            if (Ai.SubGridInitTick != Session.Tick)
            {
                Ai.SubGridInitTick = Session.Tick;
                using (Ai.DbLock.AcquireExclusiveUsing()) 
                {
                    Ai.PrevSubGrids.Clear();
                    Ai.SubGrids.Clear();

                    MyAPIGateway.GridGroups.GetGroup(Cube.CubeGrid, GridLinkTypeEnum.Mechanical, Ai.PrevSubGrids);
                    
                    foreach (var grid in Ai.PrevSubGrids)
                        Ai.SubGrids.Add((MyCubeGrid)grid);

                    Ai.SubGridDetect();
                    Ai.SubGridChanges(false, true);
                }
            }
        }

        private void InventoryInit()
        {
            using (CoreEntity.Pin())
            {
                if (InventoryInited || !CoreEntity.HasInventory || CoreEntity.MarkedForClose || (Platform.State != CorePlatform.PlatformState.Inited && Platform.State != CorePlatform.PlatformState.Incomplete) || CoreInventory == null)
                {
                    Platform.PlatformCrash(this, false, true, $"InventoryInit failed: IsInitted:{InventoryInited} - NoInventory:{!CoreEntity.HasInventory} - Marked:{CoreEntity.MarkedForClose} - PlatformNotReady:{Platform.State != CorePlatform.PlatformState.Ready}({Platform.State}) - nullInventory:{CoreInventory == null}");
                    return;
                }

                if (CoreEntity is IMyConveyorSorter || CoreInventory.Constraint == null) CoreInventory.Constraint = new MyInventoryConstraint("ammo");

                CoreInventory.Constraint.m_useDefaultIcon = false;
                CoreInventory.Refresh();
                CoreInventory.Constraint.Clear();

                if (!string.IsNullOrEmpty(CustomIcon)) {
                    var iconPath = Platform.Structure.ModPath + "\\Textures\\GUI\\Icons\\" + CustomIcon;
                    CoreInventory.Constraint.Icon = iconPath;
                    CoreInventory.Constraint.UpdateIcon();
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
                            CoreInventory.Constraint.Add(w.System.AmmoTypes[j].AmmoDef.Const.MagazineDef.Id);
                    }
                }
                CoreInventory.Refresh();

                InventoryInited = true;
            }
        }
    }
}
