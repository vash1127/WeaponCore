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
                var controller = myCubeBlock as MyShipController;
                if (controller != null)
                {
                    if (!MySession.BlockSets.ContainsKey(myCubeBlock.CubeGrid)) MySession.BlockSets.TryAdd(myCubeBlock.CubeGrid, new BlockSets());

                    MySession.BlockSets[myCubeBlock.CubeGrid].ShipControllers.Add(controller);
                    MySession._checkForDistributor = true;
                    return;
                }

                var source = myCubeBlock.Components.Get<MyResourceSourceComponent>();
                if (source != null)
                {
                    if (source.ResourceTypes[0] != MySession.GId) return;

                    if (!MySession.BlockSets.ContainsKey(myCubeBlock.CubeGrid)) MySession.BlockSets.TryAdd(myCubeBlock.CubeGrid, new BlockSets());

                    var battery = myCubeBlock as IMyBatteryBlock;
                    if (battery != null)
                    {
                        
                        MySession.BlockSets[myCubeBlock.CubeGrid].Batteries.Add(new BatteryInfo(source));
                    }

                    MySession.BlockSets[myCubeBlock.CubeGrid].Sources.Add(source);
                    MySession._updatePowerSources = true;

                }
            }
            catch (Exception ex) { Log.Line($"Exception in Controller FatBlockAdded: {ex}"); }
        }

        private void FatBlockRemoved(MyCubeBlock myCubeBlock)
        {
            try
            {
                var controller = myCubeBlock as MyShipController;

                if (controller != null)
                {
                    MySession.BlockSets[myCubeBlock.CubeGrid].ShipControllers.Remove(controller);
                    MySession._checkForDistributor = true;
                    return;
                }
                var source = myCubeBlock.Components.Get<MyResourceSourceComponent>();
                if (source != null)
                {
                    if (source.ResourceTypes[0] != MySession.GId) return;

                    var battery = myCubeBlock as IMyBatteryBlock;
                    if (battery != null)
                    {
                        MySession.BlockSets[myCubeBlock.CubeGrid].Batteries.Remove(new BatteryInfo(source));
                    }

                    MySession.BlockSets[myCubeBlock.CubeGrid].Sources.Remove(source);
                    MySession._updatePowerSources = true;
                }
            }
            catch (Exception ex) { Log.Line($"Exception in Controller FatBlockRemoved: {ex}"); }
        }

    }
}
