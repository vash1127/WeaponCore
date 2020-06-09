using System;
using System.Collections.Concurrent;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.Entity;

namespace WeaponCore.Support
{
    public partial class GridAi
    {
        internal void RegisterMyGridEvents(bool register = true, MyCubeGrid grid = null)
        {
            if (grid == null) grid = MyGrid;

            if (register) {

                if (Registered)
                    Log.Line($"Ai RegisterMyGridEvents error");

                Registered = true;
                MarkedForClose = false;
                grid.OnFatBlockAdded += FatBlockAdded;
                grid.OnFatBlockRemoved += FatBlockRemoved;
                grid.OnClose += GridClose;
            }
            else {

                if (!Registered)
                    Log.Line($"Ai UnRegisterMyGridEvents error");
                MarkedForClose = true;
                if (Registered) {


                    Registered = false;
                    grid.OnFatBlockAdded -= FatBlockAdded;
                    grid.OnFatBlockRemoved -= FatBlockRemoved;
                    grid.OnClose -= GridClose;
                }
            }
        }

        internal void FatBlockAdded(MyCubeBlock myCubeBlock)
        {
            try
            {
                var isWeaponBase = Session.ReplaceVanilla && myCubeBlock?.BlockDefinition != null && Session.VanillaIds.ContainsKey(myCubeBlock.BlockDefinition.Id) || myCubeBlock?.BlockDefinition != null && Session.WeaponPlatforms.ContainsKey(myCubeBlock.BlockDefinition.Id.SubtypeId);

                if (myCubeBlock is MyBatteryBlock)
                    Session.AiFatBlockChanges.Add(new FatBlockChange {Ai = this, FatBlock = myCubeBlock, State = FatBlockChange.StateChange.BatteryAdd});
                else if (!isWeaponBase && (myCubeBlock is MyConveyor || myCubeBlock is IMyConveyorTube || myCubeBlock is MyConveyorSorter || myCubeBlock is MyCargoContainer || myCubeBlock is MyCockpit || myCubeBlock is IMyAssembler))
                    Session.AiFatBlockChanges.Add(new FatBlockChange { Ai = this, FatBlock = myCubeBlock, State = FatBlockChange.StateChange.InventoryAdd});
                else if (isWeaponBase) ScanBlockGroups = true;

            }
            catch (Exception ex) { Log.Line($"Exception in Controller FatBlockAdded: {ex} - {myCubeBlock?.BlockDefinition == null}"); }
        }


        private void FatBlockRemoved(MyCubeBlock myCubeBlock)
        {
            try
            {
                if (myCubeBlock is MyBatteryBlock)
                    Session.AiFatBlockChanges.Add(new FatBlockChange { Ai = this, FatBlock = myCubeBlock, State = FatBlockChange.StateChange.BatteryRemove });
                else if (myCubeBlock.Components.Has<WeaponComponent>())
                    Session.AiFatBlockChanges.Add(new FatBlockChange { Ai = this, FatBlock = myCubeBlock, State = FatBlockChange.StateChange.CompRemove });
                else if (myCubeBlock.HasInventory)
                    Session.AiFatBlockChanges.Add(new FatBlockChange { Ai = this, FatBlock = myCubeBlock, State = FatBlockChange.StateChange.InventoryRemove });
            }
            catch (Exception ex) { Log.Line($"Exception in Controller FatBlockRemoved: {ex}"); }
        }

        internal void CheckAmmoInventory(MyInventoryBase inventory, MyPhysicalInventoryItem item, MyFixedPoint amount)
        {
            if (amount <= 0) return;
            var itemDef = item.Content.GetObjectId();
            if (Session.AmmoDefIds.Contains(itemDef))
                Session.FutureEvents.Schedule(CheckReload, itemDef, 1);
        }

        internal void GridClose(MyEntity myEntity)
        {
            if (Session == null || MyGrid == null || Closed)
            {
                Log.Line($"[GridClose] Session: {Session != null} - MyGrid:{MyGrid != null} - Closed:{Closed} - myEntity:{myEntity != null}");
                return;
            }

            ProjectileTicker = Session.Tick;
            Session.DelayedGridAiClean.Add(this);
            Session.DelayedGridAiClean.ApplyAdditions();

            if (Session.IsClient)
                Session.SendUpdateRequest(MyGrid.EntityId, PacketType.ClientEntityClosed);
        }
    }
}
