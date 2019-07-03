using System;
using Sandbox.Game.Entities;
using VRage.Game.ModAPI;

namespace WeaponCore.Support
{
    public partial class GridTargetingAi
    {
        public void RegisterGridEvents(MyCubeGrid grid, bool register = true)
        {
            if (register)
            {
                //grid.OnBlockAdded += BlockAddedEvent;
                //grid.OnBlockRemoved += BlockRemovedEvent;
            }
            else
            {
                //grid.OnBlockAdded -= BlockAddedEvent;
                //grid.OnBlockRemoved -= BlockRemovedEvent;
            }
        }

        private void BlockAddedEvent(IMySlimBlock block)
        {
            try
            {
                if (SubTick < MySession.Tick + 10) SubGridInfo();
            }
            catch (Exception ex) { Log.Line($"Exception in Controller BlockAdded: {ex}"); }
        }

        private void BlockRemovedEvent(IMySlimBlock block)
        {
            try
            {
                if (SubTick < MySession.Tick + 10) SubGridInfo();
            }
            catch (Exception ex) { Log.Line($"Exception in Controller BlockRemoved: {ex}"); }
        }

    }
}
