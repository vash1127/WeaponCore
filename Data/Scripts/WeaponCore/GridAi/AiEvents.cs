using System;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;

namespace WeaponCore.Support
{
    public partial class GridAi
    {
        private void RegisterMyGridEvents(bool register = true, MyCubeGrid grid = null)
        {
            if (grid == null) grid = MyGrid;
            if (register)
            {
                grid.OnFatBlockAdded += FatBlockAdded;
                grid.OnFatBlockRemoved += FatBlockRemoved;
                grid.OnMarkForClose += GridClose;
                grid.OnClose += GridClose;

            }
            else
            {
                grid.OnFatBlockAdded -= FatBlockAdded;
                grid.OnFatBlockRemoved -= FatBlockRemoved;
                grid.OnMarkForClose -= GridClose;
                grid.OnClose -= GridClose;
            }
        }

        internal void FatBlockAdded(MyCubeBlock myCubeBlock)
        {
            try
            {
                if (myCubeBlock is IMyPowerProducer)
                {
                    var source = myCubeBlock.Components.Get<MyResourceSourceComponent>();
                    if (source != null)
                    {
                        var type = source.ResourceTypes[0];
                        if (type != MyResourceDistributorComponent.ElectricityId) return;
                        if (Sources.Add(source)) SourceCount++;
                        UpdatePowerSources = true;
                    }
                }
                if (myCubeBlock is IMyCargoContainer || myCubeBlock is IMyAssembler) {
                    MyInventory inventory;
                    if (myCubeBlock.TryGetInventory(out inventory)) {
                        inventory.InventoryContentChanged += CheckAmmoInventory;
                    }
                }
            }
            catch (Exception ex) { Log.Line($"Exception in Controller FatBlockAdded: {ex}"); }
        }

        private void FatBlockRemoved(MyCubeBlock myCubeBlock)
        {
            try
            {
                if (myCubeBlock is IMyPowerProducer)
                {
                    var source = myCubeBlock.Components.Get<MyResourceSourceComponent>();
                    if (source != null)
                    {
                        var type = source.ResourceTypes[0];
                        if (type != MyResourceDistributorComponent.ElectricityId) return;
                        if (Sources.Remove(source)) SourceCount--;
                        UpdatePowerSources = true;
                    }
                    UpdatePowerSources = true;
                }
                if (myCubeBlock is IMyCargoContainer || myCubeBlock is IMyAssembler)
                {
                    MyInventory inventory;
                    if (myCubeBlock.TryGetInventory(out inventory))
                    {
                        inventory.InventoryContentChanged -= CheckAmmoInventory;
                        foreach (var ammoInvetory in AmmoInventories) {
                            if (ammoInvetory.Value.ContainsKey(inventory))
                                ammoInvetory.Value.Remove(inventory);
                        }
                    }
                }
            }
            catch (Exception ex) { Log.Line($"Exception in Controller FatBlockRemoved: {ex}"); }
        }

        private void CheckAmmoInventory(MyInventoryBase inventory, MyPhysicalInventoryItem item, MyFixedPoint amount)
        {
            if (item.Content is MyObjectBuilder_AmmoMagazine)
            {
                var ammoMag = item.Content as MyObjectBuilder_AmmoMagazine;
                var magId = ammoMag.GetObjectId();

                if (AmmoInventories.ContainsKey(magId))
                {
                    if (!AmmoInventories[magId].ContainsKey(inventory) && amount > 0)
                        AmmoInventories[ammoMag.GetObjectId()][inventory] = amount;

                    else if (AmmoInventories[magId][inventory] + amount > 0)
                        AmmoInventories[magId][inventory] += amount;

                    else if (AmmoInventories[magId].ContainsKey(inventory))
                        AmmoInventories[magId].Remove(inventory);
                }
            }
        }

        private void SourceOutputChanged(MyDefinitionId changedResourceId, float oldOutput, MyResourceSourceComponent source)
        {
            if (ResetPowerTick != Session.Instance.Tick && oldOutput > source.CurrentOutput)
            {
                UpdatePowerSources = true;
                ResetPowerTick = Session.Instance.Tick;
            }
        }

        private void GridClose(MyEntity myEntity)
        { 
            RegisterMyGridEvents(false);
            WeaponBase.Clear();
            SubGrids.Clear();
            Obstructions.Clear();
            Threats.Clear();
            TargetAis.Clear();
            EntitiesInRange.Clear();
            Sources.Clear();
            Targets.Clear();
            Targeting = null;
        }
    }
}
