using System;
using System.Collections.Generic;
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
                if (myCubeBlock is IMyCargoContainer || myCubeBlock is IMyAssembler)
                {
                    MyInventory inventory;
                    if (myCubeBlock.TryGetInventory(out inventory))
                        inventory.InventoryContentChanged += CheckAmmoInventory;
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
            Session.DsUtil.Start("AmmoInventory");
            if (item.Content is MyObjectBuilder_AmmoMagazine)
            {
                var myInventory = inventory as MyInventory;
                if (myInventory == null) return;
                var ammoMag = item.Content as MyObjectBuilder_AmmoMagazine;
                var magId = ammoMag.GetObjectId();
                if (AmmoInventories.ContainsKey(magId))
                {
                    var hasIntentory = AmmoInventories[magId].ContainsKey(myInventory);
                    if (!hasIntentory && amount > 0)
                        AmmoInventories[ammoMag.GetObjectId()][myInventory] = amount;

                    else if (hasIntentory && AmmoInventories[magId][myInventory] + amount > 0)
                        AmmoInventories[magId][myInventory] += amount;

                    else if (hasIntentory)
                        AmmoInventories[magId].Remove(myInventory);
                    CheckReload = true;
                    NewAmmoType = magId;
                }
            }
            Session.AmmoMoveTriggered++;
            Session.DsUtil.Complete("AmmoInventory", true, false);
        }

        private void SourceOutputChanged(MyDefinitionId changedResourceId, float oldOutput, MyResourceSourceComponent source)
        {
            if (ResetPowerTick != Session.Tick && oldOutput > source.CurrentOutput)
            {
                UpdatePowerSources = true;
                ResetPowerTick = Session.Tick;
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
