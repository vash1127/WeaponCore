using System;
using CoreSystems.Platform;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage;
using VRage.Game.ModAPI;
using static CoreSystems.CompData;
namespace CoreSystems.Support
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
                    BaseData.StorageInit();

                BaseData.DataManager(DataState.Load);

                if (Session.IsServer)
                    BaseData.DataManager(DataState.Reset);
            }
            catch (Exception ex) { Log.Line($"Exception in StorageSetup: {ex} - StateNull:{BaseData.ProtoRepoBase == null} - cubeMarked:{CoreEntity.MarkedForClose} - WeaponsNull:{Platform.Weapons == null} - FirstWeaponNull:{Platform.Weapons?[0] == null}"); }
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

                for (int i = 0; i < Platform.Weapons.Count; i++) {
                    var w = Platform.Weapons[i];

                    if (w == null)
                    {
                        Log.Line("InventoryInit weapon null");
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
