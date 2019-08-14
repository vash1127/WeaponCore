using System;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;

namespace WeaponCore.Support
{
    public partial class GridAi
    {
        public void TargetGridEvents(MyCubeGrid grid, bool register = true)
        {
            if (register)
            {
                grid.OnBlockAdded += BlockAddedEvent;
                grid.OnBlockRemoved += BlockRemovedEvent;
            }
            else
            {
                grid.OnBlockAdded -= BlockAddedEvent;
                grid.OnBlockRemoved -= BlockRemovedEvent;
            }
        }

        private void RegisterMyGridEvents(bool register = true, MyCubeGrid grid = null)
        {
            if (grid == null) grid = MyGrid;
            if (register)
            {
                grid.OnFatBlockAdded += FatBlockAdded;
                grid.OnFatBlockRemoved += FatBlockRemoved;
            }
            else
            {
                grid.OnFatBlockAdded -= FatBlockAdded;
                grid.OnFatBlockRemoved -= FatBlockRemoved;
            }
        }

        private void BlockAddedEvent(IMySlimBlock block)
        {
            try
            {
                //if (SubTick < MySession.Tick + 10) SubGridInfo();
            }
            catch (Exception ex) { Log.Line($"Exception in Controller BlockAdded: {ex}"); }
        }

        private void BlockRemovedEvent(IMySlimBlock block)
        {
            try
            {
                //if (SubTick < MySession.Tick + 10) SubGridInfo();
            }
            catch (Exception ex) { Log.Line($"Exception in Controller BlockRemoved: {ex}"); }
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
                        source.OutputChanged += SourceOutputChanged;
                        UpdatePowerSources = true;
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
            }
            catch (Exception ex) { Log.Line($"Exception in Controller FatBlockRemoved: {ex}"); }
        }

        private void SourceOutputChanged(MyDefinitionId changedResourceId, float oldOutput, MyResourceSourceComponent source)
        {
            if (ResetPowerTick != MySession.Tick && oldOutput > source.CurrentOutput) {
                UpdatePowerSources = true;
                ResetPowerTick = MySession.Tick;
            }
        }
    }
}
