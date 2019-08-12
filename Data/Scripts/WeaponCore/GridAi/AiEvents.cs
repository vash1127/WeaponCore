using System;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
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
                        Sources.Add(source);
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
                        Sources.Remove(source);
                        UpdatePowerSources = true;
                    }
                    UpdatePowerSources = true;
                }
                else if (MySession.WeaponPlatforms.ContainsKey(myCubeBlock.BlockDefinition.Id.SubtypeId))
                {

                    TotalSinkPower -= PowerPercentAllowed[myCubeBlock.EntityId][0];
                    PowerPercentAllowed.Remove(myCubeBlock.EntityId);
                    Log.Line($"entID: {myCubeBlock.EntityId}");
                    RecalcPowerPercent = true;
                    UpdatePowerSources = true;
                    RecalcPowerDist = true;
                }
            }
            catch (Exception ex) { Log.Line($"Exception in Controller FatBlockRemoved: {ex}"); }
        }
    }
}
