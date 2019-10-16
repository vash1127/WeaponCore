using System;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
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

        private void GridClose(MyEntity myEntity)
        {
            RegisterMyGridEvents(false);
            UpdateBlockGroups(true);
            WeaponBase.Clear();
            SubGrids.Clear();
            Obstructions.Clear();
            Threats.Clear();
            TargetAis.Clear();
            EntitiesInRange.Clear();
            Sources.Clear();
            Targets.Clear();
            SortedTargets.Clear();
            BlockTypePool.Clean();
            CubePool.Clean();
            MyShieldTmp = null;
            MyShield = null;
            PrimeTarget = null;
            MyPlanetTmp = null;
            MyPlanet = null;
        }
    }
}
