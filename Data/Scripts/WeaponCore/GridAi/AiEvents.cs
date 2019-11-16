using System;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.Entity;

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
                var battery = myCubeBlock as MyBatteryBlock;
                if (battery != null)
                {
                    if (Batteries.Add(battery)) SourceCount++;
                    UpdatePowerSources = true;
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
                var battery = myCubeBlock as MyBatteryBlock;
                if (battery != null)
                {
                    if (Batteries.Remove(battery)) SourceCount--;
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

        private void GridClose(MyEntity myEntity)
        { 
            RegisterMyGridEvents(false);
            _possibleTargets.Clear();
            SubGrids.Clear();
            Obstructions.Clear();
            Threats.Clear();
            TargetAis.Clear();
            EntitiesInRange.Clear();
            Batteries.Clear();
            Targets.Clear();
            SortedTargets.Clear();
            BlockTypePool.Clean();
            CubePool.Clean();
            MyShieldTmp = null;
            MyShield = null;
            Focus = null;
            MyPlanetTmp = null;
            MyPlanet = null;
        }
    }
}
